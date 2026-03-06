using PhantasmaPhoenix.Protocol;
using Phantasma.Core.Domain.Contract;
using Phantasma.Core.Domain.VM.Structs;
using Phantasma.Tomb.AST;
using Phantasma.Tomb.AST.Declarations;
using Phantasma.Tomb.AST.Expressions;
using Phantasma.Tomb.AST.Statements;
using Phantasma.Tomb.Validation;

namespace Phantasma.Tomb.CodeGen
{
    public class Contract : Module
    {
        public readonly Dictionary<string, MethodDeclaration> Methods = new Dictionary<string, MethodDeclaration>();
        public readonly Dictionary<string, EventDeclaration> Events = new Dictionary<string, EventDeclaration>();


        public Contract(string name, ModuleKind kind, Module parent = null) : base(name, kind, parent)
        {
        }

        public override MethodDeclaration FindMethod(string name)
        {
            if (Methods.ContainsKey(name))
            {
                return Methods[name];
            }

            return null;
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

        private void ExpectMethodType(MethodDeclaration method, VarKind kind)
        {
            var returnType = method.@interface.ReturnType.Kind;

            if (returnType != kind)
            {
                throw new CompilerException($"expected return with type {kind} for method {method.Name}, got {returnType} instead");
            }
        }

        public override ContractInterface GenerateCode(CodeGenerator output)
        {
            foreach (var evt in Events.Values)
            {
                evt.Validate();
            }

            if (this.Kind == ModuleKind.Token)
            {
                bool hasSymbol = false;
                bool hasName = false;

                foreach (var method in this.Methods.Values)
                {
                    string name;

                    bool checkForGlobals = false;

                    if (method.Name.StartsWith("get"))
                    {
                        name = method.Name.Substring(3);

                        switch (name)
                        {
                            case "Name":
                                hasName = true;
                                checkForGlobals = true;
                                ExpectMethodType(method, VarKind.String);
                                break;

                            case "MaxSupply":
                                checkForGlobals = true;
                                ExpectMethodType(method, VarKind.Number);
                                break;

                            case "Symbol":
                                hasSymbol = true;
                                checkForGlobals = true;
                                ExpectMethodType(method, VarKind.String);
                                break;

                            case "Decimals":
                                checkForGlobals = true;
                                ExpectMethodType(method, VarKind.Number);
                                break;
                        }
                    }
                    else
                    {
                        if (method.Name.StartsWith("is") && method.Name.Length > 2 && char.IsUpper(method.Name[2]))
                        {
                            ExpectMethodType(method, VarKind.Bool);
                            checkForGlobals = true;

                            if (method.Name.Equals("IsSwappable", StringComparison.OrdinalIgnoreCase))
                            {
                                throw new CompilerException(this, $"Please remove the method '{method.Name}' from the token contract '{this.Name}'");
                            }
                        }
                    }

                    if (checkForGlobals)
                    {
                        method.Visit((x) =>
                        {
                            VarDeclaration decl = null;

                            if (x is VarExpression)
                            {
                                var varExpr = (VarExpression)x;
                                decl = varExpr.decl;
                            }

                            if (decl != null && decl.Storage == VarStorage.Global)
                            {
                                throw new CompilerException($"Access to global variable {decl.Name} is not allowed in property {method.Name}");
                            }
                        });
                    }
                }

                if (!hasName)
                {
                    throw new CompilerException($"token {this.Name} is missing property 'name'");
                }

                if (!hasSymbol)
                {
                    var type = VarType.Find(VarKind.String);
                    var symbolScope = new Scope(this.Scope, "getSymbol");

                    var symbolProperty = AddMethod(this.LineNumber, "getSymbol", true, MethodKind.Property, type, new MethodParameter[0], symbolScope, false);

                    var body = new StatementBlock(symbolScope);
                    body.Commands.Add(new ReturnStatement(symbolProperty, new LiteralExpression(symbolScope, $"\"{Name}\"", type)));

                    this.SetMethodBody(symbolProperty.Name, body);
                }
            }

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

            var usedBuiltinMethods = new List<MethodInterface>();

            foreach (var libDecl in this.Libraries.Values)
            {
                foreach (var method in libDecl.methods.Values)
                {
                    if (method.IsBuiltin)
                    {
                        usedBuiltinMethods.Add(method);
                    }
                }
            }

            foreach (var methodDecl in usedBuiltinMethods)
            {
                if (methodDecl.IsBuiltin)
                {
                    var builtin = Builtins.GetMethod(methodDecl.Alias);

                    foreach (var aliasVar in builtin.InternalVariables)
                    {
                        // if we already have one with same name, skip => This expects it to be created by the code below, but if created other way... bug!
                        if (Scope.FindVariable(aliasVar, false) != null)
                        {
                            continue;
                        }

                        var aliasType = VarType.Find(VarKind.Number); // HACK this wont work in every case
                        var varDecl = new VarDeclaration(this.Scope, aliasVar, aliasType, VarStorage.Register);
                        this.Scope.AddVariable(varDecl);
                    }
                }
            }

            foreach (var varDecl in this.Scope.Variables.Values)
            {
                if (varDecl.Storage == VarStorage.Register)
                {
                    output.AppendLine(this, $"// Explicit register allocation for \"{varDecl.Name}");
                    varDecl.Register = Compiler.Instance.AllocRegister(output, this, varDecl.Name);
                }
            }

            foreach (var entry in Methods.Values)
            {
                entry.GenerateCode(output);
            }

            this.Scope.Leave(output);

            var methods = Methods.Values.Where(x => x.@interface.IsPublic).Select(x => x.GetABI());
            var events = Events.Values.Select(x => x.GetABI());
            var abi = new ContractInterface(methods, events);

            Compiler.Instance.RegisterMacro($"{Name}_address", SmartContract.GetAddressFromContractName(Name), VarKind.Address);

            if (this.Kind == ModuleKind.Token)
            {
                Compiler.Instance.RegisterMacro($"{Name}_symbol", Name, VarKind.String);
            }

            return abi;
        }

        public MethodDeclaration AddMethod(int line, string name, bool isPublic, MethodKind kind, VarType returnType, MethodParameter[] parameters, Scope scope, bool isMulti)
        {
            if (Methods.Count == 0)
            {
                this.LineNumber = line;
            }

            var vmType = MethodInterface.ConvertType(returnType);
            if (!MethodNameValidation.IsValidMethod(name, vmType))
            {
                throw new CompilerException($"Invalid method definition: {name}:{returnType}");
            }

            if (Methods.ContainsKey(name))
            {
                throw new CompilerException($"Duplicated method name: {name}:{returnType}");
            }

            var method = new MethodInterface(this.library, MethodImplementationType.Custom, name, isPublic, kind, returnType, parameters, null, isMulti);
            this.Scope.Methods.Add(method);

            var decl = new MethodDeclaration(scope, method);
            decl.LineNumber = line;
            this.Methods[name] = decl;

            scope.Method = decl;

            return decl;
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
