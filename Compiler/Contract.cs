using Phantasma.Domain;
using Phantasma.VM;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Phantasma.Tomb.Compiler
{
    public class Contract : Module
    {
        public readonly Dictionary<string, MethodDeclaration> Methods = new Dictionary<string, MethodDeclaration>();


        public Contract(string name) : base(name)
        {
        }

        public override void Visit(Action<Node> callback)
        {
            foreach (var lib in Libraries.Values)
            {
                lib.Visit(callback);
            }

            callback(this);

            foreach (var method in Methods.Values)
            {
                method.Visit(callback);
            }
        }

        public override bool IsNodeUsed(Node node)
        {
            if (node == this)
            {
                return true;
            }

            foreach (var method in Methods.Values)
            {
                if (method.IsNodeUsed(node))
                {
                    return true;
                }
            }

            foreach (var lib in Libraries.Values)
            {
                if (lib.IsNodeUsed(node))
                {
                    return true;
                }
            }

            return false;
        }

        public override ContractInterface GenerateCode(CodeGenerator output)
        {
            this.Scope.Enter(output);

            /*{
                var reg = Parser.Instance.AllocRegister(output, this, "methodName");
                output.AppendLine(this, $"POP {reg}");
                foreach (var entry in Methods.Values)
                {
                    output.AppendLine(this, $"LOAD r0, \"{entry.Name}\"");
                    output.AppendLine(this, $"EQUAL r0, {reg}");
                    output.AppendLine(this, $"JMPIF r0, @{entry.GetEntryLabel()}");
                }
                Parser.Instance.DeallocRegister(reg);
                output.AppendLine(this, "THROW \"unknown method was called\"");
            }*/

            foreach (var entry in Methods.Values)
            {
                entry.GenerateCode(output);
            }

            this.Scope.Leave(output);

            var methods = Methods.Values.Select(x => x.GetABI());
            var abi = new ContractInterface(methods);

            return abi;
        }

        public MethodInterface AddMethod(int line, string name, MethodKind kind, VarKind returnType, MethodParameter[] parameters, Scope scope)
        {
            if (Methods.Count == 0)
            {
                this.LineNumber = line;
            }

            var method = new MethodInterface(this.library, MethodImplementationType.Custom, name, kind, returnType, parameters);
            this.Scope.Methods.Add(method);

            var decl = new MethodDeclaration(scope, method);
            decl.LineNumber = line;
            this.Methods[name] = decl;

            return method;
        }

        public void SetMethodBody(string name, StatementBlock body)
        {
            if (this.Methods.ContainsKey(name))
            {
                this.Methods[name].body = body;
            }
            else
            {
                throw new System.Exception("Cannot set body for unknown method: " + name);
            }

        }


        protected override void ProcessABI(ContractInterface abi, DebugInfo debugInfo)
        {
            base.ProcessABI(abi, debugInfo);

            // here we lookup the script start offset for each method based on debug info obtained from the assembler
            foreach (var abiMethod in abi.Methods)
            {
                var method = this.Methods[abiMethod.name];
                abiMethod.offset = debugInfo.FindOffset(method.@interface.StartAsmLine);

                if (abiMethod.offset < 0)
                {
                    throw new Exception("Could not calculate script offset for method: " + abiMethod.name);
                }
            }
        }
    }
}
