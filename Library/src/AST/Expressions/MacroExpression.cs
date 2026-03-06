using Phantasma.Core.Domain;
using Phantasma.Core.Domain.Contract;
using PhantasmaPhoenix.Core;
using Phantasma.Tomb.CodeGen;

using System;
using System.Collections.Generic;
using System.Linq;

namespace Phantasma.Tomb.AST.Expressions
{
    public class MacroExpression : Expression
    {
        public string value;
        public string[] args;

        public MacroExpression(Scope parentScope, string value, IEnumerable<string> args) : base(parentScope)
        {
            this.value = value;
            this.args = args.ToArray();
        }

        public override string ToString()
        {
            return "macro: " + value;
        }

        public override Register GenerateCode(CodeGenerator output)
        {
            throw new System.Exception($"macro {value} was not unfolded!");
        }

        public override void Visit(Action<Node> callback)
        {
            callback(this);
        }

        public override bool IsNodeUsed(Node node)
        {
            return (node == this);
        }

        public LiteralExpression Unfold(Scope scope)
        {
            switch (value)
            {
                case "THIS_ADDRESS":
                    {
                        var addr = SmartContract.GetAddressFromContractName(scope.Module.Name);
                        var hex = Base16.Encode(addr.ToByteArray());
                        return new LiteralExpression(scope, "0x" + hex, VarType.Find(VarKind.Address));
                    }

                case "THIS_SYMBOL":
                    {
                        var module = scope.Module;

                        while (module.Kind != ModuleKind.Token && module.Parent != null)
                        {
                            module = module.Parent;
                        }

                        if (module.Kind == ModuleKind.Token)
                        {
                            return new LiteralExpression(scope, '\"' + module.Name + '\"', VarType.Find(VarKind.String));
                        }

                        throw new CompilerException($"macro {value} is not available here");
                    }

                case "TYPE_OF":
                    {
                        if (args.Length != 1)
                        {
                            throw new CompilerException($"macro {value} requires 1 argument");
                        }

                        var target = args[0];

                        VarKind kind;
                        if (!Enum.TryParse<VarKind>(target, true, out kind))
                        {
                            var decl = scope.FindVariable(target, false);
                            if (decl == null)
                            {
                                throw new CompilerException($"unknown identifier: {target}");
                            }

                            kind = decl.Type.Kind;
                        }

                        return new LiteralExpression(scope, kind.ToString(), VarType.Find(VarKind.Type));
                    }

                default:
                    {
                        var macro = Compiler.Instance.ResolveMacro(value);

                        if (macro != null)
                        {
                            return new LiteralExpression(scope, macro.GetValue(), macro.type);
                        }
                        else
                        {
                            throw new CompilerException($"unknown compile time macro: {value}");
                        }
                    }
            }
        }

        public override VarType ResultType => VarType.Find(VarKind.Unknown);
    }

}
