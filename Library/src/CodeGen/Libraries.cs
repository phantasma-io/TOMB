using Phantasma.Business.Blockchain.Contracts.Native;
using Phantasma.Core.Domain.Contract;
using PhantasmaPhoenix.VM;
using PhantasmaPhoenix.Core;
using Phantasma.Tomb.AST;
using Phantasma.Tomb.AST.Declarations;
using Phantasma.Tomb.AST.Expressions;

namespace Phantasma.Tomb.CodeGen
{
    public partial class Module
    {
        private static List<string> _abiPaths = new List<string>();

        public static void AddLibraryPath(string path)
        {
            if (!path.EndsWith(Path.DirectorySeparatorChar))
            {
                path += Path.DirectorySeparatorChar;
            }

            _abiPaths.Add(path);
        }

        public void ImportLibrary(string name)
        {
            var lib = LoadLibrary(name, this.Scope, this.Kind);
            Libraries[lib.Name] = lib;
        }

        public static string[] AvailableLibraries = new[] {
            "Call", "Runtime", "Math","Token", "NFT", "Organization", "Oracle", "Storage", "Contract", "Array",
            "Leaderboard", "Market", "Account", "Crowdsale", "Stake", "Governance", "Relay", "Mail",
            "Time", "Task", "UID", "Map", "List", "String", "Bytes", "Decimal", "Enum", "Address", "Module",  FormatLibraryName };

        public const string FormatLibraryName = "Format";

        private static void GenerateCasts(LibraryDeclaration libDecl, VarKind baseType, VarKind[] types)
        {
            foreach (var kind in types)
            {
                if (kind == VarKind.Bytes)
                {
                    throw new CompilerException("Don't try to generate toBytes, it will be automatically generated");
                }

                var castName = "to" + kind;

                if (Compiler.DebugMode)
                {
                    Console.WriteLine($"Found cast: {baseType}.{castName}()");
                }

                libDecl.AddMethod(castName, MethodImplementationType.Custom, kind, new[] { new MethodParameter("target", baseType) }).
                    SetPreCallback((output, scope, expr) =>
                    {
                        var vmType = MethodInterface.ConvertType(kind);
                        var reg = expr.arguments[0].GenerateCode(output);
                        output.AppendLine(expr, $"CAST {reg} {reg} #{vmType}");
                        return reg;
                    });
            }
        }

        public static LibraryDeclaration LoadLibrary(string name, Scope scope, ModuleKind moduleKind)
        {
            if (name != name.UppercaseFirst() && name != "this")
            {
                throw new CompilerException("invalid library name: " + name);
            }

            if (name.Contains("."))
            {
                return FindExternalLibrary(name, scope, moduleKind);
            }

            var libDecl = new LibraryDeclaration(scope, name);

            Builtins.FillLibrary(libDecl);

            VarKind libKind;
            if (Enum.TryParse<VarKind>(name, out libKind) && libKind != VarKind.Bytes && libKind != VarKind.Method && libKind != VarKind.Task && libKind != VarKind.Array)
            {
                switch (libKind)
                {
                    case VarKind.Decimal:
                    case VarKind.Struct:
                    case VarKind.Module:
                        libKind = VarKind.Any;
                        break;
                }

                libDecl.AddMethod("toBytes", MethodImplementationType.Custom, VarKind.Bytes, new[] { new MethodParameter("target", libKind) }).
                SetPreCallback((output, scope, expr) =>
                {
                    var reg = expr.arguments[0].GenerateCode(output);
                    output.AppendLine(expr, $"CAST {reg} {reg} #{VMType.Bytes}");
                    return reg;
                });
            }

            switch (name)
            {
                case "Module":
                    libDecl.AddMethod("getScript", MethodImplementationType.Custom, VarKind.Bytes, new[] { new MethodParameter("target", VarKind.Module) }).
                    SetPreCallback((output, scope, expr) =>
                    {
                        var reg = Compiler.Instance.AllocRegister(output, expr);
                        var module = expr.arguments[0].AsLiteral<Module>();
                        var script = Base16.Encode(module.script);
                        output.AppendLine(expr, $"LOAD {reg} 0x{script}");
                        return reg;
                    });

                    libDecl.AddMethod("getABI", MethodImplementationType.Custom, VarKind.Bytes, new[] { new MethodParameter("target", VarKind.Module) }).
                    SetPreCallback((output, scope, expr) =>
                    {
                        var reg = Compiler.Instance.AllocRegister(output, expr);
                        var module = expr.arguments[0].AsLiteral<Module>();
                        var abiBytes = module.abi.ToByteArray();
                        var script = Base16.Encode(abiBytes);
                        output.AppendLine(expr, $"LOAD {reg} 0x{script}");
                        return reg;
                    });

                    return libDecl;

                case "Struct":
                    libDecl.AddMethod("fromBytes", MethodImplementationType.Custom, VarKind.Struct, new[] { new MethodParameter("source", VarKind.Bytes) });
                    return libDecl;

                case "String":
                    // NOTE those are builtins, so they are no longer declared here
                    //libDecl.AddMethod("toUpper", MethodImplementationType.LocalCall, VarKind.String, new[] { new MethodParameter("s", VarKind.String) }).SetAlias("string_upper");
                    //libDecl.AddMethod("toLower", MethodImplementationType.LocalCall, VarKind.String, new[] { new MethodParameter("s", VarKind.String) }).SetAlias("string_lower");

                    libDecl.AddMethod("length", MethodImplementationType.Custom, VarKind.Number, new[] { new MethodParameter("target", VarKind.String) }).
                        SetPreCallback((output, scope, expr) =>
                        {
                            var reg = expr.arguments[0].GenerateCode(output);
                            output.AppendLine(expr, $"SIZE {reg} {reg}");
                            return reg;
                        });

                    libDecl.AddMethod("substr", MethodImplementationType.Custom, VarKind.String, new[] { new MethodParameter("target", VarKind.String), new MethodParameter("index", VarKind.Number), new MethodParameter("length", VarKind.Number) }).
                        SetPreCallback((output, scope, expr) =>
                        {
                            var reg = expr.arguments[0].GenerateCode(output);
                            var regB = expr.arguments[1].GenerateCode(output);
                            var regC = expr.arguments[2].GenerateCode(output);
                            output.AppendLine(expr, $"RANGE {reg} {reg} {regB} {regC}");
                            Compiler.Instance.DeallocRegister(ref regB);
                            Compiler.Instance.DeallocRegister(ref regC);
                            return reg;
                        });

                    libDecl.AddMethod("toArray", MethodImplementationType.Custom, VarType.Find(VarKind.Array, VarType.Find(VarKind.Number)), new[] { new MethodParameter("target", VarKind.String) }).
                        SetPreCallback((output, scope, expr) =>
                        {
                            var reg = expr.arguments[0].GenerateCode(output);
                            output.AppendLine(expr, $"CAST {reg} {reg} #{VMType.Struct}");
                            return reg;
                        });

                    libDecl.AddMethod("fromArray", MethodImplementationType.Custom, VarKind.String, new[] { new MethodParameter("target", VarType.Find(VarKind.Array, VarType.Find(VarKind.Number))) }).
                        SetPreCallback((output, scope, expr) =>
                        {
                            var reg = expr.arguments[0].GenerateCode(output);
                            output.AppendLine(expr, $"CAST {reg} {reg} #{VMType.String}");
                            return reg;
                        });

                    GenerateCasts(libDecl, VarKind.String, new VarKind[] { VarKind.Bool, VarKind.Number });

                    return libDecl;

                case "Bytes":
                    GenerateCasts(libDecl, VarKind.Bytes, new VarKind[] { VarKind.Bool, VarKind.String, VarKind.Number });
                    return libDecl;

                case "Number":
                    GenerateCasts(libDecl, VarKind.Number, new VarKind[] { VarKind.String, VarKind.Timestamp, VarKind.Bool });
                    return libDecl;

                case "Hash":
                    GenerateCasts(libDecl, VarKind.Hash, new VarKind[] { VarKind.String, VarKind.Number });
                    return libDecl;

                case "Enum":
                    GenerateCasts(libDecl, VarKind.Enum, new VarKind[] { VarKind.String, VarKind.Number });

                    libDecl.AddMethod("isSet", MethodImplementationType.Custom, VarKind.Bool, new[] { new MethodParameter("target", VarKind.Enum), new MethodParameter("flag", VarKind.Enum) }).
                        SetPreCallback((output, scope, expr) =>
                        {
                            var regA = expr.arguments[0].GenerateCode(output);
                            var regB = expr.arguments[1].GenerateCode(output);

                            output.AppendLine(expr, $"AND {regA} {regB} {regA}");

                            Compiler.Instance.DeallocRegister(ref regB);
                            return regA;
                        });
                    return libDecl;


                case "Decimal":
                    libDecl.AddMethod("decimals", MethodImplementationType.Custom, VarKind.Number, new[] { new MethodParameter("target", VarKind.Any) }).
                        SetPreCallback((output, scope, expr) =>
                        {
                            var reg = Compiler.Instance.AllocRegister(output, expr);
                            var arg = expr.arguments[0];
                            var decType = (DecimalVarType)arg.ResultType;
                            output.AppendLine(expr, $"LOAD {reg} {decType.decimals}");
                            return reg;
                        });

                    libDecl.AddMethod("convert", MethodImplementationType.Custom, VarKind.Number, new[] { new MethodParameter("decimalPlaces", VarKind.Number), new MethodParameter("value", VarKind.Number) }).
                        SetPreCallback((output, scope, expr) =>
                        {
                            var regA = expr.arguments[0].GenerateCode(output);
                            var regB = Compiler.Instance.AllocRegister(output, expr);

                            output.AppendLine(expr, $"LOAD {regB} 10");
                            output.AppendLine(expr, $"POW {regB} {regA} {regA}");

                            Compiler.Instance.DeallocRegister(ref regB);

                            regB = expr.arguments[1].GenerateCode(output);
                            output.AppendLine(expr, $"MUL {regB} {regA} {regA}");
                            Compiler.Instance.DeallocRegister(ref regB);

                            return regA;
                        });
                    return libDecl;

                case "Array":
                    {
                        libDecl.AddMethod("length", MethodImplementationType.Custom, VarKind.Number, new[] { new MethodParameter("target", VarKind.Any) }).
                            SetPreCallback((output, scope, expr) =>
                            {
                                var reg = expr.arguments[0].GenerateCode(output);
                                output.AppendLine(expr, $"COUNT {reg} {reg}");
                                return reg;
                            });

                        libDecl.AddMethod("get", MethodImplementationType.Custom, VarType.Generic(0), new[] { new MethodParameter("array", VarKind.Any), new MethodParameter("index", VarKind.Number) })
                            .SetPreCallback((output, scope, expr) =>
                            {
                                var vmType = MethodInterface.ConvertType(expr.method.ReturnType);
                                var reg = Compiler.Instance.AllocRegister(output, expr);

                                output.AppendLine(expr, $"LOAD {reg} {(int)vmType} // field type");
                                output.AppendLine(expr, $"PUSH {reg}");

                                return reg;
                            })
                            .SetPostCallback(ConvertGenericResult);

                        // TODO not implemented yet... for now use builtin TOMB array support, eg: local temp: array<number>
                        libDecl.AddMethod("set", MethodImplementationType.Custom, VarKind.None, new[] { new MethodParameter("array", VarKind.Any), new MethodParameter("index", VarKind.Number), new MethodParameter("value", VarType.Generic(0)) });
                        libDecl.AddMethod("remove", MethodImplementationType.Custom, VarKind.None, new[] { new MethodParameter("array", VarKind.Any), new MethodParameter("index", VarKind.Number) });
                        libDecl.AddMethod("clear", MethodImplementationType.Custom, VarKind.None, new[] { new MethodParameter("array", VarKind.Any) });
                        libDecl.AddMethod("push", MethodImplementationType.Custom, VarKind.None, new[] { new MethodParameter("array", VarKind.Any), new MethodParameter("value", VarType.Generic(0)) })
                            .SetPreCallback((output, scope, expr) =>
                            {
                                var arrayReg = expr.arguments[0].GenerateCode(output);
                                var elem = expr.arguments[1].GenerateCode(output);
                                var sizeReg = Compiler.Instance.AllocRegister(output, expr);

                                output.AppendLine(expr, $"COUNT {arrayReg} {sizeReg}");
                                output.AppendLine(expr, $"PUT {elem} {arrayReg} {sizeReg}");

                                Compiler.Instance.DeallocRegister(ref sizeReg);
                                Compiler.Instance.DeallocRegister(ref elem);
                                return arrayReg;
                            });
                        libDecl.AddMethod("pop", MethodImplementationType.Custom, VarType.Generic(0),
                                new[] { new MethodParameter("array", VarKind.Any) })
                            .SetPreCallback((output, scope, expr) =>
                            {
                                var arrayReg = expr.arguments[0].GenerateCode(output);
                                var sizeReg = Compiler.Instance.AllocRegister(output, expr);

                                output.AppendLine(expr, $"COUNT {arrayReg} {sizeReg}");
                                output.AppendLine(expr, $"DEC {sizeReg}");

                                var elementReg = Compiler.Instance.AllocRegister(output, expr);
                                output.AppendLine(expr, $"GET {arrayReg} {elementReg} {sizeReg}");
                                // Phoenix VM opcode set does not expose array-key removal, so pop currently
                                // returns the last element without mutating the backing array.

                                Compiler.Instance.DeallocRegister(ref sizeReg);
                                Compiler.Instance.DeallocRegister(ref arrayReg);
                                return elementReg;
                            }).SetPostCallback(ConvertGenericResult);

                        return libDecl;
                    }


                case "Address":
                    // TODO implementations of those
                    libDecl.AddMethod("isNull", MethodImplementationType.Custom, VarKind.Bool, new[] { new MethodParameter("target", VarKind.Address) });
                    libDecl.AddMethod("isUser", MethodImplementationType.Custom, VarKind.Bool, new[] { new MethodParameter("target", VarKind.Address) });
                    libDecl.AddMethod("isSystem", MethodImplementationType.Custom, VarKind.Bool, new[] { new MethodParameter("target", VarKind.Address) });
                    libDecl.AddMethod("isInterop", MethodImplementationType.Custom, VarKind.Bool, new[] { new MethodParameter("target", VarKind.Address) });

                    GenerateCasts(libDecl, VarKind.Address, new VarKind[] { VarKind.String });

                    return libDecl;
            }

            if (moduleKind == ModuleKind.Description)
            {
                switch (name)
                {
                    case FormatLibraryName:
                        libDecl.AddMethod("decimals", MethodImplementationType.ExtCall, VarKind.String, new[] { new MethodParameter("value", VarKind.Number), new MethodParameter("symbol", VarKind.String) });
                        libDecl.AddMethod("symbol", MethodImplementationType.ExtCall, VarKind.String, new[] { new MethodParameter("symbol", VarKind.String) });
                        libDecl.AddMethod("account", MethodImplementationType.ExtCall, VarKind.String, new[] { new MethodParameter("address", VarKind.Address) });
                        break;

                    default:
                        throw new CompilerException("unknown library: " + name);
                }

                return libDecl;
            }

            switch (name)
            {
                case "Call":
                    libDecl.AddMethod("interop", MethodImplementationType.ExtCall, VarType.Generic(0), new[] { new MethodParameter("method", VarKind.String), new MethodParameter("...", VarKind.Any) });
                    libDecl.AddMethod("contract", MethodImplementationType.ContractCall, VarType.Generic(0), new[] { new MethodParameter("contract", VarKind.String), new MethodParameter("method", VarKind.String), new MethodParameter("...", VarKind.Any) });
                    libDecl.AddMethod("method", MethodImplementationType.Custom, VarType.Generic(0), new[] { new MethodParameter("method", VarKind.Method), new MethodParameter("...", VarKind.Any) }).
                    SetPreCallback((output, scope, expr) =>
                    {
                        var contract = scope.Module as Contract;
                        if (contract == null)
                        {
                            throw new CompilerException("Cannot use Call.method outside of a contract");
                        }

                        var methodName = expr.arguments[0].AsLiteral<string>();
                        var method = contract.FindMethod(methodName);
                        if (method == null)
                        {
                            throw new CompilerException($"Cannot find local method '{methodName}' in contract '{contract.Name}'");
                        }

                        var label = method.GetEntryLabel();

                        // push the method arguments into the stack, in the proper order
                        for (int i = expr.arguments.Count - 1; i >= 1; i--)
                        {
                            var argReg = expr.arguments[i].GenerateCode(output);
                            output.AppendLine(expr, $"PUSH {argReg}");
                            Compiler.Instance.DeallocRegister(ref argReg);
                        }

                        var reg = Compiler.Instance.AllocRegister(output, expr, expr.NodeID);
                        output.AppendLine(expr, $"CALL @{label}");
                        output.AppendLine(expr, $"POP {reg}");
                        return reg;
                    });
                    break;

                case "Chain":
                    {
                        libDecl.AddMethod("create", MethodImplementationType.ExtCall, VarKind.None, new[] { new MethodParameter("source", VarKind.Address), new MethodParameter("organization", VarKind.String), new MethodParameter("name", VarKind.String), new MethodParameter("parentName", VarKind.String) }).SetAlias("Nexus.CreateChain");
                        break;
                    }

                case "Cryptography":
                    {
                        libDecl.AddMethod("AESDecrypt", MethodImplementationType.ExtCall, VarKind.Bytes, new[] { new MethodParameter("data", VarKind.Bytes), new MethodParameter("key", VarKind.Bytes) }).SetAlias("Runtime.AESDecrypt");
                        libDecl.AddMethod("AESEncrypt", MethodImplementationType.ExtCall, VarKind.Bytes, new[] { new MethodParameter("data", VarKind.Bytes), new MethodParameter("key", VarKind.Bytes) }).SetAlias("Runtime.AESEncrypt");
                        break;
                    }

                case "Platform":
                    {
                        libDecl.AddMethod("create", MethodImplementationType.ExtCall, VarKind.None, new[] { new MethodParameter("source", VarKind.Address), new MethodParameter("name", VarKind.String), new MethodParameter("externalAddress", VarKind.String), new MethodParameter("interopAddress", VarKind.Address), new MethodParameter("symbol", VarKind.String) }).SetAlias("Nexus.CreatePlatform");
                        libDecl.AddMethod("setTokenHash", MethodImplementationType.ExtCall, VarKind.None, new[] { new MethodParameter("symbol", VarKind.String), new MethodParameter("platform", VarKind.String), new MethodParameter("hash", VarKind.Bytes) }).SetAlias("Nexus.SetTokenPlatformHash");
                        break;
                    }

                case "Runtime":
                    libDecl.AddMethod("expect", MethodImplementationType.Custom, VarKind.None, new[] { new MethodParameter("condition", VarKind.Bool), new MethodParameter("error", VarKind.String) }).
                        SetPreCallback((output, scope, expr) =>
                        {
                            var reg = expr.arguments[0].GenerateCode(output);
                            output.AppendLine(expr, $"JMPIF {reg} @expect_{expr.NodeID}");

                            var reg2 = expr.arguments[1].GenerateCode(output);
                            output.AppendLine(expr, $"THROW {reg2}");

                            Compiler.Instance.DeallocRegister(ref reg2);

                            output.AppendLine(expr, $"@expect_{expr.NodeID}: NOP");
                            return reg;
                        });
                    libDecl.AddMethod("log", MethodImplementationType.ExtCall, VarKind.None, new[] { new MethodParameter("message", VarKind.String) });
                    libDecl.AddMethod("isWitness", MethodImplementationType.ExtCall, VarKind.Bool, new[] { new MethodParameter("address", VarKind.Address) });
                    libDecl.AddMethod("isTrigger", MethodImplementationType.ExtCall, VarKind.Bool, new MethodParameter[] { });
                    libDecl.AddMethod("transactionHash", MethodImplementationType.ExtCall, VarKind.Hash, new MethodParameter[] { });
                    libDecl.AddMethod("deployContract", MethodImplementationType.ExtCall, VarKind.None, new[] { new MethodParameter("from", VarKind.Address), new MethodParameter("contract", VarKind.Module) }).SetParameterCallback("contract", ConvertFieldToContractWithName);
                    libDecl.AddMethod("upgradeContract", MethodImplementationType.ExtCall, VarKind.None, new[] { new MethodParameter("from", VarKind.Address), new MethodParameter("contract", VarKind.Module) }).SetParameterCallback("contract", ConvertFieldToContractWithName);
                    libDecl.AddMethod("gasTarget", MethodImplementationType.ExtCall, VarKind.Address, new MethodParameter[] { }).SetAlias("Runtime.GasTarget");
                    libDecl.AddMethod("context", MethodImplementationType.ExtCall, VarKind.String, new MethodParameter[] { }).SetAlias("Runtime.Context");
                    libDecl.AddMethod("previousContext", MethodImplementationType.ExtCall, VarKind.String, new MethodParameter[] { }).SetAlias("Runtime.PreviousContext");
                    libDecl.AddMethod("version", MethodImplementationType.ExtCall, VarKind.Number, new MethodParameter[] { }).SetAlias("Runtime.Version");
                    libDecl.AddMethod("getGovernanceValue", MethodImplementationType.ExtCall, VarKind.Number, new MethodParameter[] { new MethodParameter("tag", VarKind.String) }).SetAlias("Nexus.GetGovernanceValue");
                    break;

                case "Task":
                    libDecl.AddMethod("start", MethodImplementationType.ExtCall, VarKind.Task, new MethodParameter[] { new MethodParameter("method", VarKind.Method), new MethodParameter("from", VarKind.Address), new MethodParameter("frequency", VarKind.Number), new MethodParameter("mode", VarType.Find(VarKind.Enum, "TaskMode")), new MethodParameter("gasLimit", VarKind.Number) }).SetAlias("Task.Start");
                    libDecl.AddMethod("stop", MethodImplementationType.ExtCall, VarKind.None, new MethodParameter[] { new MethodParameter("task", VarKind.Address) }).SetAlias("Task.Stop");
                    libDecl.AddMethod("current", MethodImplementationType.ExtCall, VarKind.Task, new MethodParameter[] { }).SetAlias("Task.Current");
                    break;

                case "Math":
                    libDecl.AddMethod("min", MethodImplementationType.Custom, VarKind.Number, new[] { new MethodParameter("valA", VarKind.Number), new MethodParameter("valB", VarKind.Number) }).
                    SetPreCallback((output, scope, expr) =>
                    {
                        var regA = expr.arguments[0].GenerateCode(output);
                        var regB = expr.arguments[1].GenerateCode(output);
                        output.AppendLine(expr, $"MIN {regA} {regA} {regB}");
                        Compiler.Instance.DeallocRegister(ref regB);
                        return regA;
                    });

                    libDecl.AddMethod("max", MethodImplementationType.Custom, VarKind.Number, new[] { new MethodParameter("valA", VarKind.Number), new MethodParameter("valB", VarKind.Number) }).
                    SetPreCallback((output, scope, expr) =>
                    {
                        var regA = expr.arguments[0].GenerateCode(output);
                        var regB = expr.arguments[1].GenerateCode(output);
                        output.AppendLine(expr, $"MAX {regA} {regA} {regB}");
                        Compiler.Instance.DeallocRegister(ref regB);
                        return regA;
                    });
                    libDecl.AddMethod("abs", MethodImplementationType.Custom, VarKind.Number, new[] { new MethodParameter("val", VarKind.Number) }).
                    SetPreCallback((output, scope, expr) =>
                    {
                        var reg = expr.arguments[0].GenerateCode(output);
                        output.AppendLine(expr, $"ABS {reg} {reg}");
                        return reg;
                    });
                    libDecl.AddMethod("pow", MethodImplementationType.Custom, VarKind.Number, new[] { new MethodParameter("val", VarKind.Number), new MethodParameter("exp", VarKind.Number) }).
                    SetPreCallback((output, scope, expr) =>
                    {
                        var regResult = Compiler.Instance.AllocRegister(output, expr);
                        var regA = expr.arguments[0].GenerateCode(output);
                        var regB = expr.arguments[1].GenerateCode(output);
                        output.AppendLine(expr, $"POW {regA} {regB} {regResult}");
                        Compiler.Instance.DeallocRegister(ref regA);
                        Compiler.Instance.DeallocRegister(ref regB);
                        return regResult;
                    });

                    // NOTE those are builtins, so they are no longer declared here
                    //libDecl.AddMethod("sqrt", MethodImplementationType.LocalCall, VarKind.Number, new[] { new MethodParameter("n", VarKind.Number) }).SetAlias("math_sqrt");
                    break;

                case "Time":
                    GenerateCasts(libDecl, VarKind.Timestamp, new VarKind[] { VarKind.String, VarKind.Number });

                    libDecl.AddMethod("now", MethodImplementationType.ExtCall, VarKind.Timestamp, new MethodParameter[] { }).SetAlias("Runtime.Time");
                    libDecl.AddMethod("unix", MethodImplementationType.Custom, VarKind.Timestamp, new[] { new MethodParameter("value", VarKind.Number) }).SetPostCallback((output, scope, method, reg) =>
                    {
                        var nameExpr = method.arguments[0] as LiteralExpression;
                        if (nameExpr != null && nameExpr.type.Kind == VarKind.Number)
                        {
                            var timestamp = uint.Parse(nameExpr.value);
                            output.AppendLine(method, $"LOAD {reg} {timestamp}");
                            method.CallNecessaryConstructors(output, VarKind.Timestamp, reg);
                            return reg;
                        }
                        else
                        {
                            throw new Exception("Expected literal number expression");
                        }
                    });
                    break;

                case "Account":
                    {
                        libDecl.AddMethod("getName", MethodImplementationType.ExtCall, VarKind.String, new MethodParameter[] { new MethodParameter("from", VarKind.Address) }).SetAlias("Account.Name");
                        libDecl.AddMethod("getLastActivity", MethodImplementationType.ExtCall, VarKind.Timestamp, new MethodParameter[] { new MethodParameter("from", VarKind.Address) }).SetAlias("Account.LastActivity");
                        var contract = NativeContractKind.Account.ToString().ToLower();
                        libDecl.AddMethod("registerName", MethodImplementationType.ContractCall, VarKind.None, new[] { new MethodParameter("target", VarKind.Address), new MethodParameter("name", VarKind.String) }).SetContract(contract).SetAlias(nameof(AccountContract.RegisterName));
                        libDecl.AddMethod("unregisterName", MethodImplementationType.ContractCall, VarKind.None, new[] { new MethodParameter("target", VarKind.Address) }).SetContract(contract).SetAlias(nameof(AccountContract.UnregisterName));
                        libDecl.AddMethod("registerScript", MethodImplementationType.ContractCall, VarKind.None, new[] { new MethodParameter("target", VarKind.Address), new MethodParameter("script", VarKind.Bytes), new MethodParameter("abiBytes", VarKind.Bytes) }).SetContract(contract).SetAlias(nameof(AccountContract.RegisterScript));
                        libDecl.AddMethod("hasScript", MethodImplementationType.ContractCall, VarKind.Bool, new[] { new MethodParameter("target", VarKind.Address) }).SetContract(contract).SetAlias(nameof(AccountContract.HasScript));
                        //libDecl.AddMethod("lookUpAddress", MethodImplementationType.ContractCall, VarKind.String, new[] { new MethodParameter("target", VarKind.Address) }).SetContract(contract).SetAlias(nameof(AccountContract.LookUpAddress));
                        libDecl.AddMethod("lookUpScript", MethodImplementationType.ContractCall, VarKind.Bytes, new[] { new MethodParameter("target", VarKind.Address) }).SetContract(contract).SetAlias(nameof(AccountContract.LookUpScript));
                        libDecl.AddMethod("lookUpABI", MethodImplementationType.ContractCall, VarKind.Bytes, new[] { new MethodParameter("target", VarKind.Address) }).SetContract(contract).SetAlias(nameof(AccountContract.LookUpABI));
                        libDecl.AddMethod("lookUpName", MethodImplementationType.ContractCall, VarKind.Address, new[] { new MethodParameter("name", VarKind.String) }).SetContract(contract).SetAlias(nameof(AccountContract.LookUpName));
                        libDecl.AddMethod("migrate", MethodImplementationType.ContractCall, VarKind.None, new[] { new MethodParameter("from", VarKind.Address), new MethodParameter("target", VarKind.Address) }).SetContract(contract).SetAlias(nameof(AccountContract.Migrate));
                        // TODO ContractMethod GetTriggerForABI -> libDecl.AddMethod("GetTriggerForABI", MethodImplementationType.ContractCall, VarKind.Struct, new[] { new MethodParameter("trigger", VarKind.Struct) }).SetContract(contract).SetAlias(nameof(AccountContract.GetTriggersForABI));
                        // TODO IEnumerable<ContractMethod> GetTriggersForABI -> libDecl.AddMethod("GetTriggerForABI", MethodImplementationType.ContractCall, VarKind.Struct, new[] { new MethodParameter("triggers", VarKind.Struct) }).SetContract(contract).SetAlias(nameof(AccountContract.GetTriggersForABI));
                        // TODO IEnumerable<ContractMethod> GetTriggersForABI -> libDecl.AddMethod("GetTriggerForABI", MethodImplementationType.ContractCall, VarKind.Struct, new[] { new MethodParameter("triggers", VarKind.Address) }).SetContract(contract).SetAlias(nameof(AccountContract.GetTriggersForABI));
                        break;
                    }

                case "UID":
                    {
                        libDecl.AddMethod("generate", MethodImplementationType.ExtCall, VarKind.Number, new MethodParameter[] { }).SetAlias("Runtime.GenerateUID");
                        break;
                    }

                case "Random":
                    // NOTE those are builtins, so they are no longer declared here
                    //libDecl.AddMethod("generate", MethodImplementationType.LocalCall, VarKind.Number, new MethodParameter[] { }).SetAlias("random_generate");
                    //libDecl.AddMethod("seed", MethodImplementationType.LocalCall, VarKind.None, new[] { new MethodParameter("seed", VarKind.Number) }).SetAlias("random_seed");
                    break;

                case "Token":
                    // Nexus.CreateToken currently accepts (from, script, abiBytes) in the chain runtime.
                    // Keeping this signature aligned avoids runtime CALL failures due to wrong argument count.
                    libDecl.AddMethod("create", MethodImplementationType.ExtCall, VarKind.None, new[] { new MethodParameter("from", VarKind.Address), new MethodParameter("script", VarKind.Bytes), new MethodParameter("abiBytes", VarKind.Bytes) }).SetAlias("Nexus.CreateToken");
                    libDecl.AddMethod("exists", MethodImplementationType.ExtCall, VarKind.Bool, new[] { new MethodParameter("symbol", VarKind.String) }).SetAlias("Runtime.TokenExists");
                    libDecl.AddMethod("getDecimals", MethodImplementationType.ExtCall, VarKind.Number, new[] { new MethodParameter("symbol", VarKind.String) }).SetAlias("Runtime.GetTokenDecimals");
                    libDecl.AddMethod("getFlags", MethodImplementationType.ExtCall, VarType.Find(VarKind.Enum, "TokenFlag"), new[] { new MethodParameter("symbol", VarKind.String) }).SetAlias("Runtime.GetTokenFlags");
                    libDecl.AddMethod("transfer", MethodImplementationType.ExtCall, VarKind.None, new[] { new MethodParameter("from", VarKind.Address), new MethodParameter("to", VarKind.Address), new MethodParameter("symbol", VarKind.String), new MethodParameter("amount", VarKind.Number) }).SetAlias("Runtime.TransferTokens");
                    libDecl.AddMethod("transferAll", MethodImplementationType.ExtCall, VarKind.None, new[] { new MethodParameter("from", VarKind.Address), new MethodParameter("to", VarKind.Address), new MethodParameter("symbol", VarKind.String) }).SetAlias("Runtime.TransferBalance");
                    libDecl.AddMethod("mint", MethodImplementationType.ExtCall, VarKind.None, new[] { new MethodParameter("from", VarKind.Address), new MethodParameter("to", VarKind.Address), new MethodParameter("symbol", VarKind.String), new MethodParameter("amount", VarKind.Number) }).SetAlias("Runtime.MintTokens");
                    libDecl.AddMethod("write", MethodImplementationType.ExtCall, VarKind.None, new[] { new MethodParameter("address", VarKind.Address), new MethodParameter("symbol", VarKind.String), new MethodParameter("token ID", VarKind.Number), new MethodParameter("ram", VarKind.Any) }).SetParameterCallback("ram", ConvertFieldToBytes).SetAlias("Runtime.WriteToken");
                    libDecl.AddMethod("burn", MethodImplementationType.ExtCall, VarKind.None, new[] { new MethodParameter("from", VarKind.Address), new MethodParameter("symbol", VarKind.String), new MethodParameter("amount", VarKind.Number) }).SetAlias("Runtime.BurnTokens");
                    libDecl.AddMethod("swap", MethodImplementationType.ExtCall, VarKind.Number, new[] { new MethodParameter("targetChain", VarKind.String), new MethodParameter("source", VarKind.Address), new MethodParameter("destination", VarKind.Address), new MethodParameter("symbol", VarKind.String), new MethodParameter("amount", VarKind.Number) }).SetAlias("Runtime.SwapTokens");
                    libDecl.AddMethod("getBalance", MethodImplementationType.ExtCall, VarKind.Number, new[] { new MethodParameter("from", VarKind.Address), new MethodParameter("symbol", VarKind.String) }).SetAlias("Runtime.GetBalance");
                    libDecl.AddMethod("isMinter", MethodImplementationType.ExtCall, VarKind.Bool, new[] { new MethodParameter("address", VarKind.Address), new MethodParameter("symbol", VarKind.String) }).SetAlias("Runtime.IsMinter");
                    libDecl.AddMethod("getCurrentSupply", MethodImplementationType.ExtCall, VarKind.Number, new[] { new MethodParameter("symbol", VarKind.String) }).SetAlias("Runtime.GetTokenSupply");
                    libDecl.AddMethod("availableSymbols", MethodImplementationType.ExtCall, VarType.FindArray(VarKind.String), new MethodParameter[0] { }).SetAlias("Runtime.GetAvailableTokenSymbols");
                    break;

                case "NFT":
                    libDecl.AddMethod("transfer", MethodImplementationType.ExtCall, VarKind.None, new[] { new MethodParameter("from", VarKind.Address), new MethodParameter("to", VarKind.Address), new MethodParameter("symbol", VarKind.String), new MethodParameter("id", VarKind.Number) }).SetAlias("Runtime.TransferToken");
                    libDecl.AddMethod("mint", MethodImplementationType.ExtCall, VarKind.Number, new[] { new MethodParameter("from", VarKind.Address), new MethodParameter("to", VarKind.Address), new MethodParameter("symbol", VarKind.String), new MethodParameter("rom", VarKind.Any), new MethodParameter("ram", VarKind.Any), new MethodParameter("seriesID", VarKind.Any) })
                        .SetParameterCallback("rom", ConvertFieldToBytes).SetParameterCallback("ram", ConvertFieldToBytes).SetAlias("Runtime.MintToken");
                    libDecl.AddMethod("write", MethodImplementationType.ExtCall, VarKind.None, new[] { new MethodParameter("address", VarKind.Address), new MethodParameter("symbol", VarKind.String), new MethodParameter("tokenID", VarKind.Number), new MethodParameter("ram", VarKind.Any) }).SetParameterCallback("ram", ConvertFieldToBytes).SetAlias("Runtime.WriteToken");
                    libDecl.AddMethod("readROM", MethodImplementationType.ExtCall, VarType.Generic(0), new[] { new MethodParameter("symbol", VarKind.String), new MethodParameter("id", VarKind.Number) }).SetAlias("Runtime.ReadTokenROM").SetPostCallback(ConvertGenericResult);
                    libDecl.AddMethod("readRAM", MethodImplementationType.ExtCall, VarType.Generic(0), new[] { new MethodParameter("symbol", VarKind.String), new MethodParameter("id", VarKind.Number) }).SetAlias("Runtime.ReadTokenRAM").SetPostCallback(ConvertGenericResult);
                    libDecl.AddMethod("burn", MethodImplementationType.ExtCall, VarKind.None, new[] { new MethodParameter("from", VarKind.Address), new MethodParameter("symbol", VarKind.String), new MethodParameter("id", VarKind.Number) }).SetAlias("Runtime.BurnToken");
                    libDecl.AddMethod("infuse", MethodImplementationType.ExtCall, VarKind.None, new[] { new MethodParameter("from", VarKind.Address), new MethodParameter("symbol", VarKind.String), new MethodParameter("id", VarKind.Number), new MethodParameter("infuseSymbol", VarKind.String), new MethodParameter("infuseValue", VarKind.Number) }).SetAlias("Runtime.InfuseToken");

                    var nftArray = VarType.Find(VarKind.Array, VarType.Find(VarKind.Struct, "NFT"));
                    libDecl.AddMethod("getInfusions", MethodImplementationType.ExtCall, nftArray, new[] { new MethodParameter("symbol", VarKind.String), new MethodParameter("token_id", VarKind.Number)}).SetAlias("Runtime.ReadInfusions");

                    var numberArray = VarType.Find(VarKind.Array, VarType.Find(VarKind.Number));
                    libDecl.AddMethod("getOwnerships", MethodImplementationType.ExtCall, numberArray, new[] { new MethodParameter("from", VarKind.Address), new MethodParameter("symbol", VarKind.String)}).SetAlias("Runtime.GetOwnerships");

                    libDecl.AddMethod("availableSymbols", MethodImplementationType.ExtCall, VarType.FindArray(VarKind.String), new MethodParameter[0] { }).SetAlias("Runtime.GetAvailableNFTSymbols");

                    libDecl.AddMethod("createSeries", MethodImplementationType.ExtCall, VarKind.None, new[] { new MethodParameter("from", VarKind.Address), new MethodParameter("symbol", VarKind.String), new MethodParameter("seriesID", VarKind.Number), new MethodParameter("maxSupply", VarKind.Number), new MethodParameter("mode", VarType.Find(VarKind.Enum, "TokenSeries")), new MethodParameter("nft", VarKind.Module) }).
                        SetAlias("Nexus.CreateTokenSeries").SetParameterCallback("nft", ConvertFieldToContractWithoutName);

                    libDecl.AddMethod("read", MethodImplementationType.ExtCall, VarType.Find(VarKind.Struct, "NFT"), new[] { new MethodParameter("symbol", VarKind.String), new MethodParameter("id", VarKind.Number) }).SetAlias("Runtime.ReadToken")
                        .SetPreCallback((output, scope, expr) =>
                        {
                            var nftStruct = VarType.Find(VarKind.Struct, "NFT") as StructVarType;
                            var reg = Compiler.Instance.AllocRegister(output, expr);

                            var fields = '\"' + string.Join(',', nftStruct.decl.fields.Select(x => x.name)) + '\"';

                            output.AppendLine(expr, $"LOAD {reg} {fields} // field list");
                            output.AppendLine(expr, $"PUSH {reg}");

                            return reg;
                        })
                        .SetPostCallback((output, scope, method, reg) =>
                        {
                            output.AppendLine(method, $"UNPACK {reg} {reg}");
                            return reg;
                        });
                    break;

                case "Organization":
                    {
                        libDecl.AddMethod("create", MethodImplementationType.ExtCall, VarKind.None, new[] { new MethodParameter("from", VarKind.Address), new MethodParameter("id", VarKind.String), new MethodParameter("name", VarKind.String), new MethodParameter("script", VarKind.Bytes) }).SetAlias("Nexus.CreateOrganization");
                        libDecl.AddMethod("addMember", MethodImplementationType.ExtCall, VarKind.None, new[] { new MethodParameter("from", VarKind.Address), new MethodParameter("name", VarKind.String), new MethodParameter("target", VarKind.Address) });
                        break;
                    }

                case "Oracle":
                    {
                        libDecl.AddMethod("read", MethodImplementationType.ExtCall, VarKind.None, new[] { new MethodParameter("url", VarKind.String) });
                        libDecl.AddMethod("price", MethodImplementationType.ExtCall, VarKind.None, new[] { new MethodParameter("symbol", VarKind.String) });
                        libDecl.AddMethod("quote", MethodImplementationType.ExtCall, VarKind.None, new[] { new MethodParameter("baseSymbol", VarKind.String), new MethodParameter("quoteSymbol", VarKind.String), new MethodParameter("amount", VarKind.Number) });
                        break;
                    }

                case "Storage":
                    {
                        libDecl.AddMethod("read", MethodImplementationType.ExtCall, VarType.Generic(0), new[] { new MethodParameter("contract", VarKind.String), new MethodParameter("field", VarKind.String) }).SetAlias("Data.Get")
                        .SetPreCallback((output, scope, expr) =>
                        {
                            var vmType = MethodInterface.ConvertType(expr.method.ReturnType);
                            var reg = Compiler.Instance.AllocRegister(output, expr);

                            output.AppendLine(expr, $"LOAD {reg} {(int)vmType} // field type");
                            output.AppendLine(expr, $"PUSH {reg}");

                            return reg;
                        })
                        .SetPostCallback(ConvertGenericResult);

                        libDecl.AddMethod("write", MethodImplementationType.ExtCall, VarKind.None, new[] { new MethodParameter("field", VarKind.String), new MethodParameter("value", VarKind.Any) }).SetAlias("Data.Set");
                        libDecl.AddMethod("delete", MethodImplementationType.ExtCall, VarKind.None, new[] { new MethodParameter("field", VarKind.String) }).SetAlias("Data.Delete");
                        var contract = NativeContractKind.Storage.ToString().ToLower();
                        libDecl.AddMethod("calculateStorageSizeForStake", MethodImplementationType.ContractCall, VarKind.Number, new[] { new MethodParameter("stakeAmount", VarKind.Number) }).SetContract(contract).SetAlias(nameof(StorageContract.CalculateStorageSizeForStake));
                        libDecl.AddMethod("createFile", MethodImplementationType.ContractCall, VarKind.None, new[] { new MethodParameter("target", VarKind.Address), new MethodParameter("fileName", VarKind.String), new MethodParameter("fileSize", VarKind.Number), new MethodParameter("contentMerkle", VarKind.Bytes), new MethodParameter("encryptionContent", VarKind.Bytes) }).SetContract(contract).SetAlias(nameof(StorageContract.CreateFile));
                        libDecl.AddMethod("hasFile", MethodImplementationType.ContractCall, VarKind.Bool, new[] { new MethodParameter("target", VarKind.Address), new MethodParameter("hash", VarKind.Hash) }).SetContract(contract).SetAlias(nameof(StorageContract.HasFile));
                        libDecl.AddMethod("addFile", MethodImplementationType.ContractCall, VarKind.None, new[] { new MethodParameter("from", VarKind.Address), new MethodParameter("target", VarKind.Address), new MethodParameter("archiveHash", VarKind.Hash) }).SetContract(contract).SetAlias(nameof(StorageContract.AddFile));
                        libDecl.AddMethod("deleteFile", MethodImplementationType.ContractCall, VarKind.None, new[] { new MethodParameter("from", VarKind.Address), new MethodParameter("targetHash", VarKind.Hash) }).SetContract(contract).SetAlias(nameof(StorageContract.DeleteFile));
                        libDecl.AddMethod("hasPermission", MethodImplementationType.ContractCall, VarKind.Bool, new[] { new MethodParameter("external", VarKind.Address), new MethodParameter("target", VarKind.Address) }).SetContract(contract).SetAlias(nameof(StorageContract.HasPermission));
                        libDecl.AddMethod("addPermission", MethodImplementationType.ContractCall, VarKind.None, new[] { new MethodParameter("from", VarKind.Address), new MethodParameter("externalAddr", VarKind.Address) }).SetContract(contract).SetAlias(nameof(StorageContract.AddPermission));
                        libDecl.AddMethod("deletePermission", MethodImplementationType.ContractCall, VarKind.None, new[] { new MethodParameter("from", VarKind.Address), new MethodParameter("externalAddr", VarKind.Address) }).SetContract(contract).SetAlias(nameof(StorageContract.DeletePermission));
                        //libDecl.AddMethod("migratePermission", MethodImplementationType.ContractCall, VarKind.None, new[] { new MethodParameter("target", VarKind.Address), new MethodParameter("oldAddr", VarKind.Address), new MethodParameter("newAddr", VarKind.Address) }).SetContract(contract).SetAlias(nameof(StorageContract.MigratePermission));
                        //libDecl.AddMethod("migrate", MethodImplementationType.ContractCall, VarKind.None, new[] { new MethodParameter("from", VarKind.Address), new MethodParameter("target", VarKind.Address) }).SetContract(contract).SetAlias(nameof(StorageContract.Migrate));
                        libDecl.AddMethod("getUsedSpace", MethodImplementationType.ContractCall, VarKind.Number, new[] { new MethodParameter("from", VarKind.Address) }).SetContract(contract).SetAlias(nameof(StorageContract.GetUsedSpace));
                        libDecl.AddMethod("getAvailableSpace", MethodImplementationType.ContractCall, VarKind.Number, new[] { new MethodParameter("from", VarKind.Address) }).SetContract(contract).SetAlias(nameof(StorageContract.GetAvailableSpace));
                        // TODO Hash[] GetFiles(Address from) -> libDecl.AddMethod("getFiles", MethodImplementationType.ContractCall, VarKind.Hash, new[] { new MethodParameter("from", VarKind.Address) }).SetContract(contract).SetAlias(nameof(StorageContract.GetFiles));
                        libDecl.AddMethod("getUsedDataQuota", MethodImplementationType.ContractCall, VarKind.Number, new[] { new MethodParameter("address", VarKind.Address) }).SetContract(contract).SetAlias(nameof(StorageContract.GetUsedDataQuota));
                        //libDecl.AddMethod("writeData", MethodImplementationType.ContractCall, VarKind.None, new[] { new MethodParameter("target", VarKind.Address), new MethodParameter("key", VarKind.Bytes), new MethodParameter("value", VarKind.Bytes) }).SetContract(contract).SetAlias(nameof(StorageContract.WriteData));
                        //libDecl.AddMethod("deleteData", MethodImplementationType.ContractCall, VarKind.None, new[] { new MethodParameter("target", VarKind.Address), new MethodParameter("key", VarKind.Bytes) }).SetContract(contract).SetAlias(nameof(StorageContract.DeleteData));

                        break;
                    }

                case "Contract":
                    libDecl.AddMethod("call", MethodImplementationType.ContractCall, VarType.Generic(0), new[] { new MethodParameter("contract", VarKind.String), new MethodParameter("method", VarKind.String), new MethodParameter("...", VarKind.Any) });
                    libDecl.AddMethod("exists", MethodImplementationType.ExtCall, VarKind.Bool, new[] { new MethodParameter("name", VarKind.String) }).SetAlias("Runtime.ContractExists");

                    libDecl.AddMethod("address", MethodImplementationType.Custom, VarKind.Address, new[] { new MethodParameter("name", VarKind.String) }).SetPostCallback((output, scope, method, reg) =>
                    {
                        var nameExpr = method.arguments[0] as LiteralExpression;
                        if (nameExpr != null && nameExpr.type.Kind == VarKind.String)
                        {
                            var address = SmartContract.GetAddressFromContractName(nameExpr.value);
                            var hex = Base16.Encode(address.ToByteArray());
                            output.AppendLine(method, $"LOAD {reg} 0x{hex}");
                            return reg;
                        }
                        else
                        {
                            throw new Exception("Expected literal string expression");
                        }
                    });
                    break;

                case "Leaderboard":
                    {
                        var contract = NativeContractKind.Ranking.ToString().ToLower();
                        libDecl.AddMethod("exists", MethodImplementationType.ContractCall, VarKind.Bool, new[] { new MethodParameter("boardName", VarKind.String) }).SetContract(contract).SetAlias(nameof(RankingContract.Exists));
                        libDecl.AddMethod("create", MethodImplementationType.ContractCall, VarKind.None, new[] { new MethodParameter("from", VarKind.Address), new MethodParameter("boardName", VarKind.String), new MethodParameter("capacity", VarKind.Number) }).SetContract(contract).SetAlias(nameof(RankingContract.CreateLeaderboard));
                        libDecl.AddMethod("getAddress", MethodImplementationType.ContractCall, VarKind.Address, new[] { new MethodParameter("boardName", VarKind.String), new MethodParameter("index", VarKind.Number) }).SetContract(contract).SetAlias(nameof(RankingContract.GetAddressByIndex));
                        libDecl.AddMethod("getScoreByIndex", MethodImplementationType.ContractCall, VarKind.Number, new[] { new MethodParameter("boardName", VarKind.String), new MethodParameter("index", VarKind.Number) }).SetContract(contract).SetAlias(nameof(RankingContract.GetScoreByIndex));
                        libDecl.AddMethod("getScoreByAddress", MethodImplementationType.ContractCall, VarKind.Number, new[] { new MethodParameter("boardName", VarKind.String), new MethodParameter("target", VarKind.Address) }).SetContract(contract).SetAlias(nameof(RankingContract.GetScoreByAddress));
                        libDecl.AddMethod("getSize", MethodImplementationType.ContractCall, VarKind.Number, new[] { new MethodParameter("boardName", VarKind.String) }).SetContract(contract).SetAlias(nameof(RankingContract.GetSize));
                        libDecl.AddMethod("insert", MethodImplementationType.ContractCall, VarKind.None, new[] { new MethodParameter("from", VarKind.Address), new MethodParameter("target", VarKind.Address), new MethodParameter("boardName", VarKind.String), new MethodParameter("score", VarKind.Number) }).SetContract(contract).SetAlias(nameof(RankingContract.InsertScore));
                        libDecl.AddMethod("reset", MethodImplementationType.ContractCall, VarKind.None, new[] { new MethodParameter("from", VarKind.Address), new MethodParameter("boardName", VarKind.String) }).SetContract(contract).SetAlias(nameof(RankingContract.ResetLeaderboard));
                        break;
                    }

                case "Market":
                    {
                        var contract = NativeContractKind.Market.ToString().ToLower();
                        libDecl.AddMethod("sell", MethodImplementationType.ContractCall, VarKind.None, new[] { new MethodParameter("from", VarKind.Address), new MethodParameter("baseSymbol", VarKind.String), new MethodParameter("quoteSymbol", VarKind.String), new MethodParameter("tokenID", VarKind.Number), new MethodParameter("price", VarKind.Number), new MethodParameter("endDate", VarKind.Timestamp) }).SetContract(contract).SetAlias(nameof(MarketContract.SellToken));
                        libDecl.AddMethod("buy", MethodImplementationType.ContractCall, VarKind.None, new[] { new MethodParameter("from", VarKind.Address), new MethodParameter("symbol", VarKind.String), new MethodParameter("tokenID", VarKind.Number) }).SetContract(contract).SetAlias(nameof(MarketContract.BuyToken));
                        libDecl.AddMethod("cancel", MethodImplementationType.ContractCall, VarKind.None, new[] { new MethodParameter("symbol", VarKind.String), new MethodParameter("tokenID", VarKind.Number) }).SetContract(contract).SetAlias(nameof(MarketContract.CancelSale));
                        libDecl.AddMethod("hasAuction", MethodImplementationType.ContractCall, VarKind.Bool, new[] { new MethodParameter("symbol", VarKind.String), new MethodParameter("tokenID", VarKind.Number) }).SetContract(contract).SetAlias(nameof(MarketContract.HasAuction));
                        libDecl.AddMethod("bid", MethodImplementationType.ContractCall, VarKind.None, new[] { new MethodParameter("from", VarKind.Address), new MethodParameter("symbol", VarKind.String), new MethodParameter("tokenID", VarKind.Number), new MethodParameter("price", VarKind.Number), new MethodParameter("buyingFee", VarKind.Number), new MethodParameter("buyingFeeAddress", VarKind.Address) }).SetContract(contract).SetAlias(nameof(MarketContract.BidToken));
                        // TODO GetAuction -> libDecl.AddMethod("getAuction", MethodImplementationType.ContractCall,  VarType.Find(VarKind.Struct, "MarketAuction"), new[] { new MethodParameter("symbol", VarKind.String), new MethodParameter("tokenID", VarKind.Number)}).SetContract(contract).SetAlias(nameof(MarketContract.GetAuction));
                        // TODO GetAuctions -> libDecl.AddMethod("getAuctions", MethodImplementationType.ContractCall, VarType.Find(VarKind.Storage_list, "MarketAuction"), new[] {} ).SetContract(contract).SetAlias(nameof(MarketContract.GetAuctions));
                        libDecl.AddMethod("listToken", MethodImplementationType.ContractCall, VarKind.None, new[] { new MethodParameter("from", VarKind.Address), new MethodParameter("baseSymbol", VarKind.String), new MethodParameter("quoteSymbol", VarKind.String), new MethodParameter("tokenID", VarKind.Number), new MethodParameter("price", VarKind.Number), new MethodParameter("endPrice", VarKind.Number), new MethodParameter("startDate", VarKind.Timestamp), new MethodParameter("endDate", VarKind.Timestamp), new MethodParameter("extensionPeriod", VarKind.Number), new MethodParameter("typeAuction", VarKind.Number), new MethodParameter("listingFee", VarKind.Number), new MethodParameter("listingFeeAddress", VarKind.Address) }).SetContract(contract).SetAlias(nameof(MarketContract.ListToken));
                        libDecl.AddMethod("editAuction", MethodImplementationType.ContractCall, VarKind.None, new[] { new MethodParameter("from", VarKind.Address), new MethodParameter("baseSymbol", VarKind.String), new MethodParameter("quoteSymbol", VarKind.String), new MethodParameter("tokenID", VarKind.Number), new MethodParameter("price", VarKind.Number), new MethodParameter("endPrice", VarKind.Number), new MethodParameter("startDate", VarKind.Timestamp), new MethodParameter("endDate", VarKind.Timestamp), new MethodParameter("extensionPeriod", VarKind.Number), }).SetContract(contract).SetAlias(nameof(MarketContract.EditAuction));
                        break;
                    }

                case "Map":
                    libDecl.AddMethod("get", MethodImplementationType.ExtCall, VarType.Generic(1), new[] { new MethodParameter("map", VarKind.String), new MethodParameter("key", VarType.Generic(0)) }).SetParameterCallback("map", ConvertFieldToStorageAccessRead)
                        .SetPreCallback((output, scope, expr) =>
                        {
                            var vmType = MethodInterface.ConvertType(expr.method.ReturnType);
                            var reg = Compiler.Instance.AllocRegister(output, expr);

                            output.AppendLine(expr, $"LOAD {reg} {(int)vmType} // field type");
                            output.AppendLine(expr, $"PUSH {reg}");

                            return reg;
                        })
                        .SetPostCallback(ConvertGenericResult);
                    libDecl.AddMethod("set", MethodImplementationType.ExtCall, VarKind.None, new[] { new MethodParameter("map", VarKind.String), new MethodParameter("key", VarType.Generic(0)), new MethodParameter("value", VarType.Generic(1)) }).SetParameterCallback("map", ConvertFieldToStorageAccessWrite);
                    libDecl.AddMethod("remove", MethodImplementationType.ExtCall, VarKind.None, new[] { new MethodParameter("map", VarKind.String), new MethodParameter("key", VarType.Generic(0)) }).SetParameterCallback("map", ConvertFieldToStorageAccessWrite);
                    libDecl.AddMethod("clear", MethodImplementationType.ExtCall, VarKind.None, new[] { new MethodParameter("map", VarKind.String) }).SetParameterCallback("map", ConvertFieldToStorageAccessWrite);
                    libDecl.AddMethod("count", MethodImplementationType.ExtCall, VarKind.Number, new[] { new MethodParameter("map", VarKind.String) }).SetParameterCallback("map", ConvertFieldToStorageAccessRead);
                    libDecl.AddMethod("has", MethodImplementationType.ExtCall, VarKind.Bool, new[] { new MethodParameter("map", VarKind.String), new MethodParameter("key", VarType.Generic(0)) }).SetParameterCallback("map", ConvertFieldToStorageAccessRead)
                        .SetPreCallback((output, scope, expr) =>
                        {
                            var vmType = MethodInterface.ConvertType(expr.method.ReturnType);
                            var reg = Compiler.Instance.AllocRegister(output, expr);

                            output.AppendLine(expr, $"LOAD {reg} {(int)vmType} // field type");
                            output.AppendLine(expr, $"PUSH {reg}");

                            return reg;
                        });
                    break;

                case "List":
                    libDecl.AddMethod("get", MethodImplementationType.ExtCall, VarType.Generic(0), new[] { new MethodParameter("list", VarKind.String), new MethodParameter("index", VarKind.Number) }).SetParameterCallback("list", ConvertFieldToStorageAccessRead)
                        .SetPreCallback((output, scope, expr) =>
                        {
                            var vmType = MethodInterface.ConvertType(expr.method.ReturnType);
                            var reg = Compiler.Instance.AllocRegister(output, expr);

                            output.AppendLine(expr, $"LOAD {reg} {(int)vmType} // field type");
                            output.AppendLine(expr, $"PUSH {reg}");

                            return reg;
                        })
                        .SetPostCallback(ConvertGenericResult);

                    libDecl.AddMethod("add", MethodImplementationType.ExtCall, VarKind.None, new[] { new MethodParameter("list", VarKind.String), new MethodParameter("value", VarType.Generic(0)) }).SetParameterCallback("list", ConvertFieldToStorageAccessWrite);
                    libDecl.AddMethod("replace", MethodImplementationType.ExtCall, VarKind.None, new[] { new MethodParameter("list", VarKind.String), new MethodParameter("index", VarKind.Number), new MethodParameter("value", VarType.Generic(0)) }).SetParameterCallback("list", ConvertFieldToStorageAccessWrite);
                    libDecl.AddMethod("removeAt", MethodImplementationType.ExtCall, VarKind.None, new[] { new MethodParameter("list", VarKind.String), new MethodParameter("index", VarKind.Number) }).SetParameterCallback("list", ConvertFieldToStorageAccessWrite);
                    libDecl.AddMethod("count", MethodImplementationType.ExtCall, VarKind.Number, new[] { new MethodParameter("list", VarKind.String) }).SetParameterCallback("list", ConvertFieldToStorageAccessRead);
                    libDecl.AddMethod("clear", MethodImplementationType.ExtCall, VarKind.None, new[] { new MethodParameter("list", VarKind.String) }).SetParameterCallback("list", ConvertFieldToStorageAccessWrite);
                    break;

                case "Crowdsale":
                    {
                        var contract = NativeContractKind.Sale.ToString().ToLower();
                        // TODO GetSales -> libDecl.AddMethod("getSales", MethodImplementationType.ContractCall, VarType.Find(VarKind.Storage_List, "SaleInfo"), new MethodParameter[] { } ).SetContract(contract).SetAlias(nameof(SaleContract.GetSales));
                        // TODO GetSale -> libDecl.AddMethod("getSale", MethodImplementationType.ContractCall, VarType.Find(VarKind.Struct, "SaleInfo"), new[] { new MethodParameter("saleHash", VarKind.Hash) }).SetContract(contract).SetAlias(nameof(SaleContract.GetSale));
                        libDecl.AddMethod("isSeller", MethodImplementationType.ContractCall, VarKind.Bool, new[] { new MethodParameter("target", VarKind.Address) }).SetContract(contract).SetAlias(nameof(SaleContract.IsSeller));
                        libDecl.AddMethod("isSaleActive", MethodImplementationType.ContractCall, VarKind.Bool, new[] { new MethodParameter("saleHash", VarKind.Hash) }).SetContract(contract).SetAlias(nameof(SaleContract.IsSaleActive));
                        libDecl.AddMethod("create", MethodImplementationType.ContractCall, VarKind.Hash, new[] { new MethodParameter("from", VarKind.Address), new MethodParameter("name", VarKind.String), new MethodParameter("flags", VarKind.Struct), new MethodParameter("startDate", VarKind.Timestamp), new MethodParameter("endDate", VarKind.Timestamp), new MethodParameter("sellSymbol", VarKind.String), new MethodParameter("receiveSymbol", VarKind.String), new MethodParameter("price", VarKind.Number), new MethodParameter("globalSoftCap", VarKind.Number), new MethodParameter("globalHardCap", VarKind.Number), new MethodParameter("userSoftCap", VarKind.Number), new MethodParameter("userHardCap", VarKind.Number) }).SetContract(contract).SetAlias(nameof(SaleContract.CreateSale)); // TODO
                        // TODO getSaleParticipants -> libDecl.AddMethod("getSaleParticipants", MethodImplementationType.ContractCall, VarKind.Storage_List, new[] { new MethodParameter("saleHash", VarKind.Hash) }).SetContract(contract).SetAlias(nameof(SaleContract.GetSaleParticipants)); 
                        // TODO getSaleWhitelists -> libDecl.AddMethod("getSaleWhitelists", MethodImplementationType.ContractCall, VarKind.Storage_List, new[] { new MethodParameter("saleHash", VarKind.Hash) }).SetContract(contract).SetAlias(nameof(SaleContract.GetSaleWhitelists));
                        libDecl.AddMethod("isWhitelisted", MethodImplementationType.ContractCall, VarKind.Bool, new[] { new MethodParameter("saleHash", VarKind.Hash), new MethodParameter("address", VarKind.Address) }).SetContract(contract).SetAlias(nameof(SaleContract.IsWhitelisted));
                        libDecl.AddMethod("addToWhitelist", MethodImplementationType.ContractCall, VarKind.None, new[] { new MethodParameter("saleHash", VarKind.Hash), new MethodParameter("target", VarKind.Address) }).SetContract(contract).SetAlias(nameof(SaleContract.AddToWhitelist));
                        libDecl.AddMethod("removeFromWhitelist", MethodImplementationType.ContractCall, VarKind.None, new[] { new MethodParameter("saleHash", VarKind.Hash), new MethodParameter("target", VarKind.Address) }).SetContract(contract).SetAlias(nameof(SaleContract.RemoveFromWhitelist));
                        libDecl.AddMethod("getPurchasedAmount", MethodImplementationType.ContractCall, VarKind.Number, new[] { new MethodParameter("saleHash", VarKind.Hash), new MethodParameter("address", VarKind.Address) }).SetContract(contract).SetAlias(nameof(SaleContract.GetPurchasedAmount));
                        libDecl.AddMethod("getSoldAmount", MethodImplementationType.ContractCall, VarKind.Number, new[] { new MethodParameter("saleHash", VarKind.Hash) }).SetContract(contract).SetAlias(nameof(SaleContract.GetSoldAmount));
                        libDecl.AddMethod("purchase", MethodImplementationType.ContractCall, VarKind.None, new[] { new MethodParameter("from", VarKind.Address), new MethodParameter("saleHash", VarKind.Hash), new MethodParameter("quoteSymbol", VarKind.String), new MethodParameter("quoteAmount", VarKind.Number) }).SetContract(contract).SetAlias(nameof(SaleContract.Purchase));
                        libDecl.AddMethod("closeSale", MethodImplementationType.ContractCall, VarKind.None, new[] { new MethodParameter("from", VarKind.Address), new MethodParameter("saleHash", VarKind.Hash) }).SetContract(contract).SetAlias(nameof(SaleContract.CloseSale));
                        // Keep method-to-alias mapping 1:1 so diagnostics and runtime call targets
                        // reflect the actual API surface instead of silently rerouting to another method.
                        libDecl.AddMethod("getLatestSaleHash", MethodImplementationType.ContractCall, VarKind.Hash, new MethodParameter[] { }).SetContract(contract).SetAlias(nameof(SaleContract.GetLatestSaleHash));
                        libDecl.AddMethod("editSalePrice", MethodImplementationType.ContractCall, VarKind.Hash, new[] { new MethodParameter("saleHash", VarKind.Hash), new MethodParameter("price", VarKind.Number) }).SetContract(contract).SetAlias(nameof(SaleContract.EditSalePrice));
                        break;
                    }

                case "Stake":
                    {
                        var contract = NativeContractKind.Stake.ToString().ToLower();
                        libDecl.AddMethod("getMasterThreshold", MethodImplementationType.ContractCall, VarKind.Number, new MethodParameter[] { }).SetContract(contract).SetAlias(nameof(StakeContract.GetMasterThreshold));
                        libDecl.AddMethod("isMaster", MethodImplementationType.ContractCall, VarKind.Bool, new[] { new MethodParameter("address", VarKind.Address) }).SetContract(contract).SetAlias(nameof(StakeContract.IsMaster));
                        libDecl.AddMethod("getMasterCount", MethodImplementationType.ContractCall, VarKind.Number, new MethodParameter[] { }).SetContract(contract).SetAlias(nameof(StakeContract.GetMasterCount));
                        libDecl.AddMethod("getMasterAddresses", MethodImplementationType.ContractCall, VarKind.Number, new MethodParameter[] { }).SetContract(contract).SetAlias(nameof(StakeContract.GetMasterAddresses));
                        libDecl.AddMethod("getClaimMasterCount", MethodImplementationType.ContractCall, VarKind.Number, new[] { new MethodParameter("claimDate", VarKind.Timestamp) }).SetContract(contract).SetAlias(nameof(StakeContract.GetClaimMasterCount));
                        libDecl.AddMethod("getMasterClaimDate", MethodImplementationType.ContractCall, VarKind.Timestamp, new[] { new MethodParameter("claimDistance", VarKind.Number) }).SetContract(contract).SetAlias(nameof(StakeContract.GetMasterClaimDate));
                        libDecl.AddMethod("getMasterDate", MethodImplementationType.ContractCall, VarKind.Timestamp, new[] { new MethodParameter("target", VarKind.Address) }).SetContract(contract).SetAlias(nameof(StakeContract.GetMasterDate));
                        libDecl.AddMethod("getMasterClaimDateFromReference", MethodImplementationType.ContractCall, VarKind.Timestamp, new[] { new MethodParameter("claimDistance", VarKind.Number), new MethodParameter("referenceTime", VarKind.Timestamp) }).SetContract(contract).SetAlias(nameof(StakeContract.GetMasterClaimDateFromReference));
                        libDecl.AddMethod("getMasterRewards", MethodImplementationType.ContractCall, VarKind.Number, new[] { new MethodParameter("from", VarKind.Address) }).SetContract(contract).SetAlias(nameof(StakeContract.GetMasterRewards));
                        libDecl.AddMethod("masterClaim", MethodImplementationType.ContractCall, VarKind.None, new[] { new MethodParameter("from", VarKind.Address) }).SetContract(contract).SetAlias(nameof(StakeContract.MasterClaim));
                        libDecl.AddMethod("stake", MethodImplementationType.ContractCall, VarKind.None, new[] { new MethodParameter("from", VarKind.Address), new MethodParameter("stakeAmount", VarKind.Number) }).SetContract(contract).SetAlias(nameof(StakeContract.Stake));
                        libDecl.AddMethod("unstake", MethodImplementationType.ContractCall, VarKind.None, new[] { new MethodParameter("from", VarKind.Address), new MethodParameter("unstakeAmount", VarKind.Number) }).SetContract(contract).SetAlias(nameof(StakeContract.Unstake));
                        libDecl.AddMethod("getTimeBeforeUnstake", MethodImplementationType.ContractCall, VarKind.Number, new[] { new MethodParameter("from", VarKind.Address) }).SetContract(contract).SetAlias(nameof(StakeContract.GetTimeBeforeUnstake));
                        libDecl.AddMethod("getStakeTimestamp", MethodImplementationType.ContractCall, VarKind.Timestamp, new[] { new MethodParameter("from", VarKind.Address) }).SetContract(contract).SetAlias(nameof(StakeContract.GetStakeTimestamp));
                        libDecl.AddMethod("getUnclaimed", MethodImplementationType.ContractCall, VarKind.Number, new[] { new MethodParameter("from", VarKind.Address) }).SetContract(contract).SetAlias(nameof(StakeContract.GetUnclaimed));
                        libDecl.AddMethod("claim", MethodImplementationType.ContractCall, VarKind.None, new[] { new MethodParameter("from", VarKind.Address), new MethodParameter("stakeAddress", VarKind.Address) }).SetContract(contract).SetAlias(nameof(StakeContract.Claim));
                        libDecl.AddMethod("getStake", MethodImplementationType.ContractCall, VarKind.Number, new[] { new MethodParameter("address", VarKind.Address) }).SetContract(contract).SetAlias(nameof(StakeContract.GetStake));
                        libDecl.AddMethod("getStorageStake", MethodImplementationType.ContractCall, VarKind.Number, new[] { new MethodParameter("address", VarKind.Address) }).SetContract(contract).SetAlias(nameof(StakeContract.GetStorageStake));
                        libDecl.AddMethod("fuelToStake", MethodImplementationType.ContractCall, VarKind.Number, new[] { new MethodParameter("fuelAmount", VarKind.Number) }).SetContract(contract).SetAlias(nameof(StakeContract.FuelToStake));
                        libDecl.AddMethod("stakeToFuel", MethodImplementationType.ContractCall, VarKind.Number, new[] { new MethodParameter("stakeAmount", VarKind.Number) }).SetContract(contract).SetAlias(nameof(StakeContract.StakeToFuel));
                        libDecl.AddMethod("getAddressVotingPower", MethodImplementationType.ContractCall, VarKind.Number, new[] { new MethodParameter("address", VarKind.Address) }).SetContract(contract).SetAlias(nameof(StakeContract.GetAddressVotingPower));
                        break;
                    }

                case "Governance":
                    {
                        var contract = NativeContractKind.Governance.ToString().ToLower();
                        libDecl.AddMethod("hasName", MethodImplementationType.ContractCall, VarKind.Bool, new[] { new MethodParameter("name", VarKind.String) }).SetContract(contract).SetAlias(nameof(GovernanceContract.HasName));
                        // TODO GetNames -> libDecl.AddMethod("getNames", MethodImplementationType.ContractCall, VarKind.Storage_List, new MethodParameter[] {}).SetContract(contract).SetAlias(nameof(GovernanceContract.GetNames));
                        // TODO GetValues -> libDecl.AddMethod("getValues", MethodImplementationType.ContractCall, VarType.Find(VarKind.Storage_List, "GovernancePair"), new [] { new MethodParameter("name", VarKind.String )}).SetContract(contract).SetAlias(nameof(GovernanceContract.GetValues));
                        libDecl.AddMethod("hasValue", MethodImplementationType.ContractCall, VarKind.Bool, new[] { new MethodParameter("name", VarKind.String) }).SetContract(contract).SetAlias(nameof(GovernanceContract.HasValue));
                        libDecl.AddMethod("createValue", MethodImplementationType.ContractCall, VarKind.None, new[] { new MethodParameter("name", VarKind.String), new MethodParameter("initial", VarKind.Number), new MethodParameter("serializedConstraints", VarKind.Bytes) }).SetContract(contract).SetAlias(nameof(GovernanceContract.CreateValue));
                        libDecl.AddMethod("getValue", MethodImplementationType.ContractCall, VarKind.Number, new[] { new MethodParameter("name", VarKind.String) }).SetContract(contract).SetAlias(nameof(GovernanceContract.GetValue));
                        libDecl.AddMethod("setValue", MethodImplementationType.ContractCall, VarKind.None, new[] { new MethodParameter("name", VarKind.String), new MethodParameter("value", VarKind.Number) }).SetContract(contract).SetAlias(nameof(GovernanceContract.SetValue));
                        break;
                    }

                case "Relay":
                    {
                        var contract = NativeContractKind.Relay.ToString().ToLower();
                        libDecl.AddMethod("getBalance", MethodImplementationType.ContractCall, VarKind.Number, new[] { new MethodParameter("from", VarKind.Address) }).SetContract(contract).SetAlias(nameof(RelayContract.GetBalance));
                        libDecl.AddMethod("getIndex", MethodImplementationType.ContractCall, VarKind.Number, new[] { new MethodParameter("from", VarKind.Address), new MethodParameter("to", VarKind.Address) }).SetContract(contract).SetAlias(nameof(RelayContract.GetIndex));
                        libDecl.AddMethod("getTopUpAddress", MethodImplementationType.ContractCall, VarKind.Address, new[] { new MethodParameter("from", VarKind.Address) }).SetContract(contract).SetAlias(nameof(RelayContract.GetTopUpAddress));
                        libDecl.AddMethod("openChannel", MethodImplementationType.ContractCall, VarKind.None, new[] { new MethodParameter("from", VarKind.Address), new MethodParameter("publicKey", VarKind.Bytes) }).SetContract(contract).SetAlias(nameof(RelayContract.OpenChannel));
                        libDecl.AddMethod("getKey", MethodImplementationType.ContractCall, VarKind.Bytes, new[] { new MethodParameter("from", VarKind.Address) }).SetContract(contract).SetAlias(nameof(RelayContract.GetKey));
                        libDecl.AddMethod("topUpChannel", MethodImplementationType.ContractCall, VarKind.None, new[] { new MethodParameter("from", VarKind.Address), new MethodParameter("count", VarKind.Number) }).SetContract(contract).SetAlias(nameof(RelayContract.TopUpChannel));
                        // TODO settleChannel -> libDecl.AddMethod("settleChannel", MethodImplementationType.ContractCall, VarKind.None, new[] { new MethodParameter("from", VarKind.Struct) } ).SetContract(contract).SetAlias(nameof(RelayContract.SettleChannel));
                        break;
                    }

                case "Mail":
                    {
                        var contract = NativeContractKind.Mail.ToString().ToLower();
                        libDecl.AddMethod("pushMessage", MethodImplementationType.ContractCall, VarKind.None, new[] { new MethodParameter("from", VarKind.Address), new MethodParameter("target", VarKind.Address), new MethodParameter("archiveHash", VarKind.Hash) }).SetContract(contract).SetAlias(nameof(MailContract.PushMessage));
                        libDecl.AddMethod("domainExists", MethodImplementationType.ContractCall, VarKind.Bool, new[] { new MethodParameter("domainName", VarKind.String) }).SetContract(contract).SetAlias(nameof(MailContract.DomainExists));
                        libDecl.AddMethod("registerDomain", MethodImplementationType.ContractCall, VarKind.None, new[] { new MethodParameter("from", VarKind.Address), new MethodParameter("domainName", VarKind.String) }).SetContract(contract).SetAlias(nameof(MailContract.RegisterDomain));
                        libDecl.AddMethod("unregisterDomain", MethodImplementationType.ContractCall, VarKind.None, new[] { new MethodParameter("domainName", VarKind.String) }).SetContract(contract).SetAlias(nameof(MailContract.UnregisterDomain));
                        libDecl.AddMethod("migrateDomain", MethodImplementationType.ContractCall, VarKind.None, new[] { new MethodParameter("domainName", VarKind.String), new MethodParameter("target", VarKind.Address) }).SetContract(contract).SetAlias(nameof(MailContract.MigrateDomain));
                        // TODO Address[] GetDomainUsers(string domainName) -> libDecl.AddMethod("getDomainUsers", MethodImplementationType.ContractCall, VarType.Find(VarKind.Storage_List, "Address") , new[] { new MethodParameter("domainName", VarKind.String) }).SetContract(contract).SetAlias(nameof(MailContract.GetDomainUsers));
                        libDecl.AddMethod("joinDomain", MethodImplementationType.ContractCall, VarKind.None, new[] { new MethodParameter("from", VarKind.Address), new MethodParameter("domainName", VarKind.String) }).SetContract(contract).SetAlias(nameof(MailContract.JoinDomain));
                        libDecl.AddMethod("leaveDomain", MethodImplementationType.ContractCall, VarKind.None, new[] { new MethodParameter("from", VarKind.Address), new MethodParameter("domainName", VarKind.String) }).SetContract(contract).SetAlias(nameof(MailContract.LeaveDomain));
                        libDecl.AddMethod("getUserDomain", MethodImplementationType.ContractCall, VarKind.String, new[] { new MethodParameter("target", VarKind.Address) }).SetContract(contract).SetAlias(nameof(MailContract.GetUserDomain));
                        break;
                    }
                default:
                    return FindExternalLibrary(name, scope, moduleKind);
            }

            return libDecl;
        }

        private static Dictionary<string, LibraryDeclaration> _externalLibs = new Dictionary<string, LibraryDeclaration>(StringComparer.OrdinalIgnoreCase);

        private static LibraryDeclaration FindExternalLibrary(string importName, Scope scope, ModuleKind moduleKind)
        {
            if (_externalLibs.ContainsKey(importName))
            {
                return _externalLibs[importName];
            }

            string libraryFileName = null;

            var libNamePrefix = importName.Replace('.', Path.DirectorySeparatorChar);

            foreach (var path in _abiPaths)
            {
                var possibleName = $"{path}{libNamePrefix}.abi"; 
                if (File.Exists(possibleName))
                {
                    libraryFileName = possibleName;
                    break;
                }
            }

            if (!string.IsNullOrEmpty(libraryFileName))
            {
                try
                {
                    var bytes = File.ReadAllBytes(libraryFileName);
                    var abi = ContractInterface.FromBytes(bytes);

                    var name = Path.GetFileNameWithoutExtension(libraryFileName);

                    var libDecl = new LibraryDeclaration(scope, name);

                    foreach (var method in abi.Methods)
                    {
                        var returnType = MethodInterface.ConvertType(method.returnType);

                        var parameters = new List<MethodParameter>();

                        foreach (var param in method.parameters)
                        {
                            var paramType = MethodInterface.ConvertType(param.type);
                            parameters.Add(new MethodParameter(param.name, paramType));
                        }

                        libDecl.AddMethod(method.name, MethodImplementationType.ContractCall, returnType, parameters.ToArray()).SetContract(name);
                    }

                    Builtins.FillLibrary(libDecl);

                    _externalLibs[importName] = libDecl;

                    return libDecl;
                }
                catch (Exception e)
                {
                    throw new CompilerException("unable to load library: " + libraryFileName);
                }
            }

            throw new CompilerException("unknown library: " + libraryFileName);
        }
    }
}
