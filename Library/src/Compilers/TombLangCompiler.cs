using System;
using SdkValidation = PhantasmaPhoenix.Protocol.ValidationUtils;
using System.Collections.Generic;
using System.Linq;

using Phantasma.Tomb.CodeGen;
using Phantasma.Tomb.AST;
using Phantasma.Tomb.AST.Declarations;
using Phantasma.Tomb.AST.Statements;
using Phantasma.Tomb.AST.Expressions;
using Phantasma.Tomb.Lexers;
using PhantasmaPhoenix.Protocol;
using PhantasmaPhoenix.Core;
using Phantasma.Core.Domain.Triggers.Enums;
using Phantasma.Core.Domain.Token.Enums;
using Phantasma.Core.Domain.Tasks.Enum;
using Phantasma.Core.Domain.Contract;
using Phantasma.Core.Domain.Events.Structs;

namespace Phantasma.Tomb.Compilers
{
    [Compiler(Extension = ".tomb")]
    public class TombLangCompiler : Compiler
    {
        public TombLangCompiler(int version = DomainSettings.LatestKnownProtocol) : base(version)
        {
        }

        protected override Lexer CreateLexer()
        {
            return new TombLangLexer();
        }

        private void InitEnums()
        {
            _enums.Clear();
            CreateEnum<TokenFlags>("TokenFlags");
            CreateEnum<TaskFrequencyMode>("TaskMode");
            CreateEnum<TokenSeriesMode>("TokenSeries");
        }

        private void InitStructs()
        {
            _structs.Clear();

            CreateStruct("NFT", new[]
            {
                new StructField("chain", VarKind.Address),
                new StructField("owner", VarKind.Address),
                new StructField("creator", VarKind.Address),
                new StructField("ROM", VarKind.Bytes),
                new StructField("RAM", VarKind.Bytes),
                new StructField("seriesID", VarKind.Number),
                new StructField("mintID", VarKind.Number),
            });

            CreateStruct("Infusion", new[]
            {
                new StructField("Symbol", VarKind.String),
                new StructField("Value", VarKind.Number)
            });
        }


        protected override void GenerateModules()
        {
            InitEnums();
            InitStructs();

            var globalConsts = new List<ConstDeclaration>();

            while (HasTokens())
            {
                var firstToken = FetchToken();

                Module module;

                switch (firstToken.value)
                {
                    case "struct":
                        {
                            module = null;

                            var structName = ExpectIdentifier();

                            var fields = new List<StructField>();

                            ExpectToken("{");
                            do
                            {
                                var next = FetchToken();
                                if (next.value == "}")
                                {
                                    break;
                                }

                                Rewind();

                                var fieldName = ExpectIdentifier();
                                ExpectToken(":");

                                var fieldType = ExpectType();
                                ExpectToken(";");

                                fields.Add(new StructField(fieldName, fieldType));
                            } while (true);

                            CreateStruct(structName, fields);
                            break;
                        }

                    case "enum":
                        {
                            module = null;

                            var enumName = ExpectIdentifier();

                            var entries = new List<EnumEntry>();

                            ExpectToken("{");
                            do
                            {
                                var next = FetchToken();
                                if (next.value == "}")
                                {
                                    break;
                                }

                                Rewind();

                                if (entries.Count > 0)
                                {
                                    ExpectToken(",");
                                }

                                var entryName = ExpectIdentifier();

                                next = FetchToken();

                                string enumValueStr;

                                if (next.value == "=") {
                                    enumValueStr = ExpectNumber();
                                }
                                else
                                {
                                    enumValueStr = entries.Count.ToString();
                                    Rewind();
                                }


                                uint entryValue;
                                if (!uint.TryParse(enumValueStr, out entryValue))
                                {
                                    throw new CompilerException($"Invalid enum value for {entryName} => {enumValueStr}");
                                }

                                entries.Add(new EnumEntry(entryName, entryValue));
                            } while (true);


                            var enumType = (EnumVarType)VarType.Find(VarKind.Enum, enumName);
                            enumType.decl = new EnumDeclaration(enumName, entries);

                            _enums[enumName] = enumType.decl;

                            break;
                        }


                    case "contract":
                    case "token":
                        {
                            var contractName = ExpectIdentifier();

                            if (firstToken.value == "token")
                            {
                                if (!SdkValidation.IsValidTicker(contractName))
                                {
                                    throw new CompilerException("token does not have a valid name: " + contractName);
                                }
                            }
                            else
                            {
                                if (!SdkValidation.IsValidIdentifier(contractName))
                                {
                                    throw new CompilerException("contract does not have a valid name: " + contractName);
                                }
                            }

                            module = new Contract(contractName, firstToken.value == "token" ? ModuleKind.Token: ModuleKind.Contract);
                            module.MergeConsts(globalConsts);
                            ExpectToken("{");
                            ParseModule(module);
                            ExpectToken("}");
                            break;
                        }

                    case "script":
                    case "description":
                        {
                            var scriptName = ExpectIdentifier();

                            module = new Script(scriptName, firstToken.value == "description" ? ModuleKind.Description : ModuleKind.Script);
                            module.MergeConsts(globalConsts);

                            ExpectToken("{");
                            ParseModule(module);
                            ExpectToken("}");
                            break;
                        }


                    case "const":
                        {
                            module = null;
                            var constDecl = ParseConstant(null);
                            globalConsts.Add(constDecl);
                            break;
                        }

                    default:
                        throw new CompilerException("Unexpected token: " + firstToken.value);
                }

                if (module != null)
                {
                    module.Compile();
                    AddModule(module);
                }
            }
        }

        private ConstDeclaration ParseConstant(Scope scope)
        {
            var constName = ExpectIdentifier();
            ExpectToken(":");
            var type = ExpectType();
            ExpectToken("=");

            string constVal;

            switch (type.Kind)
            {
                case VarKind.String:
                    constVal = ExpectString();
                    break;

                case VarKind.Number:
                    constVal = ExpectNumber();
                    break;

                case VarKind.Bool:
                    constVal = ExpectBool();
                    break;

                default:
                    constVal = ExpectIdentifier();
                    break;
            }

            ExpectToken(";");

            var constDecl = new ConstDeclaration(scope, constName, type, constVal);

            return constDecl;
        }

        private void ParseModule(Module module)
        {
            var structLibName = "Struct";
            module.Libraries[structLibName] = Module.LoadLibrary(structLibName, null, ModuleKind.Script);
            var structLib = module.FindLibrary(structLibName);

            foreach (var structInfo in _structs.Values)
            {
                var args = new List<MethodParameter>();

                foreach (var field in structInfo.fields)
                {
                    args.Add(new MethodParameter(field.name, field.type));
                }

                structLib.AddMethod(structInfo.Name, MethodImplementationType.Custom, VarType.Find(VarKind.Struct, structInfo.Name), args.ToArray()).SetPreCallback(
                    (output, scope, expr) =>
                    {
                        var reg = Compiler.Instance.AllocRegister(output, expr);
                        var keyReg = Compiler.Instance.AllocRegister(output, expr);

                        output.AppendLine(expr, $"CLEAR {reg}");

                        int index = -1;
                        foreach (var field in structInfo.fields)
                        {
                            index++;
                            var argument = expr.arguments[index];

                            var exprReg = argument.GenerateCode(output);
                            output.AppendLine(expr, $"LOAD {keyReg} \"{field.name}\"");
                            output.AppendLine(expr, $"PUT {exprReg} {reg} {keyReg}");

                            Compiler.Instance.DeallocRegister(ref exprReg);
                        }

                        Compiler.Instance.DeallocRegister(ref keyReg);

                        return reg;

                    });
            }

            do
            {
                var token = FetchToken();

                switch (token.value)
                {
                    case "}":
                        Rewind();
                        return;

                    case "const":
                        {
                            var constDecl = ParseConstant(module.Scope);
                            module.Scope.AddConstant(constDecl);
                            break;
                        }

                    case "global":
                        {
                            var varName = ExpectIdentifier();
                            ExpectToken(":");
                            var type = ExpectType();

                            VarDeclaration varDecl;

                            switch (type.Kind)
                            {
                                case VarKind.Storage_Map:
                                    {
                                        ExpectToken("<");
                                        var map_key = ExpectType();
                                        ExpectToken(",");
                                        var map_val = ExpectType();
                                        ExpectToken(">");

                                        varDecl = new MapDeclaration(module.Scope, varName, map_key, map_val);
                                        break;
                                    }

                                case VarKind.Storage_List:
                                    {
                                        ExpectToken("<");
                                        var list_val = ExpectType();
                                        ExpectToken(">");

                                        varDecl = new ListDeclaration(module.Scope, varName, list_val);
                                        break;
                                    }

                                case VarKind.Storage_Set:
                                    {
                                        ExpectToken("<");
                                        var set_val = ExpectType();
                                        ExpectToken(">");

                                        varDecl = new SetDeclaration(module.Scope, varName, set_val);
                                        break;
                                    }

                                default:
                                    {
                                        varDecl = new VarDeclaration(module.Scope, varName, type, VarStorage.Global);
                                        break;
                                    }
                            }

                            ExpectToken(";");
                            module.Scope.AddVariable(varDecl);
                            break;
                        }

                    case "register":
                        {
                            var varName = ExpectIdentifier();
                            ExpectToken(":");
                            var type = ExpectType();

                            switch (type.Kind)
                            {
                                case VarKind.Storage_Map:
                                case VarKind.Storage_List:
                                case VarKind.Storage_Set:
                                    {
                                        throw new CompilerException($"Type {type} cannot be used as register");
                                    }

                                default:
                                    {
                                        var varDecl = new VarDeclaration(module.Scope, varName, type, VarStorage.Register);
                                        module.Scope.AddVariable(varDecl);
                                        break;
                                    }
                            }

                            ExpectToken(";");
                            break;
                        }

                    case "import":
                        {
                            var libName = ExpectIdentifier();
                            do
                            {
                                var next = FetchToken();
                                
                                if (next.value == ";")
                                {
                                    break;
                                }
                                
                                if (next.value == ".")
                                {
                                    var subPath = ExpectIdentifier();
                                    libName = libName + "." + subPath;
                                }
                                else
                                {
                                    throw new CompilerException("Import syntax error");
                                }
                            } while (true);

                            var libDecl = Contract.LoadLibrary(libName, module.Scope, module.Kind);
                            module.Libraries[libName] = libDecl;

                            break;
                        }

                    case "event":
                        {
                            var contract = module as Contract;
                            if (contract != null)
                            {
                                var eventName = ExpectIdentifier();
                                ExpectToken(":");
                                var eventType = ExpectType();
                                ExpectToken("=");

                                if (eventType.IsWeird)
                                {
                                    throw new CompilerException("invalid event type: " + eventType);
                                }

                                var temp = FetchToken();
                                byte[] description;
                                switch (temp.kind)
                                {
                                    case TokenKind.String:
                                        description = EventDeclaration.GenerateScriptFromString(eventType, temp.value);
                                        break;

                                    case TokenKind.Bytes:
                                        description = Base16.Decode(temp.value);
                                        break;

                                    case TokenKind.Identifier:
                                        {
                                            var descModule = FindModule(temp.value, true) as Script;
                                            if (descModule != null)
                                            {
                                                var descSecondType = descModule.Parameters[1].Type;
                                                if (descSecondType != eventType)
                                                {
                                                    throw new CompilerException($"descriptions second parameter has type {descSecondType}, does not match event type: {eventType}");
                                                }

                                                if (descModule.script != null)
                                                {
                                                    description = descModule.script;
                                                }
                                                else
                                                {
                                                    throw new CompilerException($"description module not ready: {temp.value}");
                                                }
                                            }
                                            else
                                            {
                                                throw new CompilerException($"description module not found: {temp.value}");
                                            }
                                            break;
                                        }

                                    default:
                                        throw new CompilerException($"expected valid event description, got {temp.kind} instead");
                                }

                                ExpectToken(";");

                                var value = (byte)((byte)EventKind.Custom + contract.Events.Count);

                                var eventDecl = new EventDeclaration(module.Scope, eventName, value, eventType, description);

                                if (contract.Events.ContainsKey(eventName))
                                {
                                    throw new CompilerException($"duplicated event: {eventName}");
                                }

                                contract.Events[eventName] = eventDecl;
                            }
                            else
                            {
                                throw new CompilerException("unexpected token: " + token.value);
                            }
                            break;
                        }


                    case "constructor":
                        {
                            var contract = module as Contract;
                            if (contract != null)
                            {
                                var line = this.CurrentLine;
                                var name = SmartContract.ConstructorName;
                                var parameters = ParseParameters(module.Scope);
                                var scope = new Scope(module.Scope, name, parameters);

                                if (parameters.Length != 1 || parameters[0].Type.Kind != VarKind.Address)
                                {
                                    throw new CompilerException("constructor must have only one parameter of type address");
                                }

                                var method = contract.AddMethod(line, name, true, MethodKind.Constructor, VarType.Find(VarKind.None), parameters, scope, false);

                                ExpectToken("{");

                                contract.SetMethodBody(name, ParseCommandBlock(scope, method));
                                ExpectToken("}");
                                break;
                            }
                            else
                            {
                                throw new CompilerException("unexpected token: " + token.value);                                    
                            }

                        }

                    case "property":
                        {
                            var contract = module as Contract;
                            if (contract != null)
                            {
                                var line = this.CurrentLine;
                                var propertyName = ExpectIdentifier();

                                var parameters = new MethodParameter[0];
                                var scope = new Scope(module.Scope, propertyName, parameters);

                                ExpectToken(":");

                                var returnType = ExpectType();

                                var methodName = propertyName;

                                if (!methodName.StartsWith("is") || char.IsLower(methodName[2]))
                                {
                                    methodName = "get" + char.ToUpper(methodName[0]) + methodName.Substring(1);
                                }

                                var method = contract.AddMethod(line, methodName, true, MethodKind.Property, returnType, parameters, scope, false);

                                var next = FetchToken();
                                if (next.value == "=")
                                {
                                    var literal = ExpectExpression(scope);

                                    if (literal.ResultType != returnType)
                                    {
                                        throw new CompilerException($"Expected expression of type {returnType} for property {propertyName}, found {literal.ResultType} instead");
                                    }

                                    var litExpr = literal as LiteralExpression;
                                    if (litExpr != null)
                                    {
                                        RegisterMacro($"{contract.Name}_{propertyName}", litExpr.value, litExpr.ResultType);
                                    }

                                    var block = new StatementBlock(scope);
                                    block.Commands.Add(new ReturnStatement(method, literal));
                                    contract.SetMethodBody(methodName, block);

                                    ExpectToken(";");
                                }
                                else
                                {
                                    Rewind();

                                    ExpectToken("{");
                                    contract.SetMethodBody(methodName, ParseCommandBlock(scope, method));
                                    ExpectToken("}");
                                }


                                contract.library.AddMethod(propertyName, MethodImplementationType.LocalCall, returnType, parameters);
                                break;
                            }
                            else
                            {
                                throw new CompilerException("unexpected token: " + token.value);
                            }

                        }

                    case "public":
                    case "private":
                        {
                            var contract = module as Contract;
                            if (contract != null)
                            {
                                var line = this.CurrentLine;

                                var next = FetchToken();

                                string name = next.value;

                                if (next.value != "constructor")
                                {
                                    Rewind();
                                    name = ExpectIdentifier();
                                }

                                var parameters = ParseParameters(module.Scope);
                                var scope = new Scope(module.Scope, name, parameters);

                                var returnType = VarType.Find(VarKind.None);

                                var isMulti = false;

                                next = FetchToken();
                                if (next.value == ":")
                                {
                                    returnType = ExpectType();

                                    next = FetchToken();
                                    if (next.value == "*")
                                    {
                                        isMulti = true;
                                    }
                                    else
                                    {
                                        Rewind();
                                    }
                                }
                                else
                                {
                                    Rewind();
                                }

                                var method = contract.AddMethod(line, name, token.value == "public", MethodKind.Method, returnType, parameters, scope, isMulti);

                                ExpectToken("{");
                                contract.SetMethodBody(name, ParseCommandBlock(scope, method));
                                ExpectToken("}");

                                contract.library.AddMethod(name, MethodImplementationType.LocalCall, returnType, parameters);
                                break;
                            }
                            else
                            {
                                throw new CompilerException("unexpected token: " + token.value);
                            }

                        }

                    case "task":
                        {
                            var contract = module as Contract;
                            if (contract != null)
                            {
                                var line = this.CurrentLine;
                                var name = ExpectIdentifier();

                                var parameters = ParseParameters(module.Scope);

                                if (parameters.Length != 0)
                                {
                                    throw new CompilerException("task should not have parameters");
                                }

                                var scope = new Scope(module.Scope, name, parameters);

                                var method = contract.AddMethod(line, name, true, MethodKind.Task, VarType.Find(VarKind.Bool), parameters, scope, false);

                                ExpectToken("{");
                                contract.SetMethodBody(name, ParseCommandBlock(scope, method));
                                ExpectToken("}");

                                break;
                            }
                            else
                            {
                                throw new CompilerException("unexpected token: " + token.value);
                            }
                        }

                    case "trigger":
                        {
                            var contract = module as Contract;
                            if (contract != null)
                            {
                                var line = this.CurrentLine;
                                var name = ExpectIdentifier();

                                if (!name.StartsWith("on"))
                                {
                                    name = "on" + name;
                                }

                                var isValid = false;

                                string[] validTriggerNames;

                                switch (module.Kind)
                                {
                                    case ModuleKind.Account:
                                    case ModuleKind.Contract:
                                        validTriggerNames = Enum.GetNames(typeof(ContractTrigger)).ToArray();
                                        break;

                                    case ModuleKind.Token:
                                        validTriggerNames = Enum.GetNames(typeof(TokenTrigger)).ToArray();
                                        break;

                                    case ModuleKind.Organization:
                                        validTriggerNames = Enum.GetNames(typeof(OrganizationTrigger)).ToArray();
                                        break;

                                    default:
                                        throw new CompilerException("Triggers not supported in " + module.Kind);
                                }

                                foreach (var allowedName in validTriggerNames)
                                {
                                    if (allowedName.Equals(name, StringComparison.OrdinalIgnoreCase))
                                    {
                                        isValid = true;
                                        break;
                                    }
                                }

                                if (!isValid)
                                {
                                    throw new CompilerException("invalid trigger name:" + name);
                                }

                                var parameters = ParseParameters(module.Scope);

                                switch (name)
                                {
                                    case "onMint":
                                    case "onBurn":
                                    case "onSend":
                                    case "onReceive": // address, symbol, amount
                                    case "onInfuse":
                                        CheckParameters(name, parameters, new[] { VarKind.Address, VarKind.Address, VarKind.String, VarKind.Number });
                                        break;

                                    case "onWitness":
                                    case "onSeries":
                                    case "onKill":
                                    case "onUpgrade": // address
                                        CheckParameters(name, parameters, new[] { VarKind.Address });
                                        break;

                                    case "onMigrate": // from, to
                                        CheckParameters(name, parameters, new[] { VarKind.Address, VarKind.Address });
                                        break;

                                    case "onWrite": // address, data
                                        CheckParameters(name, parameters, new[] { VarKind.Address, VarKind.Any });
                                        break;

                                    default:
                                        throw new CompilerException($"Proper trigger support for trigger {name} is not implemented");
                                }

                                var scope = new Scope(module.Scope, name, parameters);

                                var method = contract.AddMethod(line, name, true, MethodKind.Trigger, VarType.Find(VarKind.None), parameters, scope, false);

                                ExpectToken("{");
                                contract.SetMethodBody(name, ParseCommandBlock(scope, method));
                                ExpectToken("}");

                                break;
                            }
                            else
                            {
                                throw new CompilerException("unexpected token: " + token.value);
                            }
                        }

                    case "code":
                        {
                            var script = module as Script;
                            if (script != null)
                            {
                                var blockName = "main";
                                script.Parameters = ParseParameters(module.Scope);

                                if (module.Kind == ModuleKind.Description) 
                                {
                                    CheckParameters(blockName, script.Parameters, new[] { VarKind.Address, VarKind.Any });
                                }

                                var scope = new Scope(module.Scope, blockName, script.Parameters);

                                script.ReturnType = VarType.Find(VarKind.None);

                                var next = FetchToken();
                                if (next.value == ":")
                                {
                                    script.ReturnType = ExpectType();
                                }
                                else
                                {
                                    Rewind();
                                }

                                var method = new MethodInterface(script.library, MethodImplementationType.Custom, blockName, true, MethodKind.Method, script.ReturnType, new MethodParameter[0]);

                                // TODO does this have to be added somewhere or its ok to just do this?
                                var decl = new MethodDeclaration(scope, method);

                                ExpectToken("{");
                                script.main = ParseCommandBlock(scope, decl);
                                ExpectToken("}");

                                break;
                            }
                            else
                            {
                                throw new CompilerException("unexpected token: " + token.value);
                            }
                        }

                    case "nft":
                        {
                            if (module.Kind != ModuleKind.Token)
                            {

                                throw new CompilerException("unexpected token: " + token.value);
                            }

                            var nftName = ExpectIdentifier();

                            ExpectToken("<");
                            var romType = ExpectType();
                            ExpectToken(",");
                            var ramType = ExpectType();
                            ExpectToken(">");

                            var subModule = new NFT(nftName, romType, ramType, module);
                            ExpectToken("{");
                            ParseModule(subModule);
                            ExpectToken("}");

                            module.AddSubModule(subModule);

                            break;
                        }


                    default:
                        throw new CompilerException("unexpected token: " + token.value);
                }

            } while (true);
        }

        private void CheckParameters(string name, MethodParameter[] parameters, VarKind[] expected)
        {
            if (parameters.Length != expected.Length)
            {
                throw new CompilerException($"Expected {expected.Length} parameters for {name}, found {parameters.Length} instead");
            }

            for (int i=0; i<parameters.Length; i++)
            if (parameters[i].Type.Kind != expected[i] && expected[i] != VarKind.Any)
            {
                throw new CompilerException($"Expected parameter #{i+1} to be {expected[i]} got {parameters[i].Type} instead");
            }
        }

        private DecimalVarType ParseDecimal()
        {
            ExpectToken("<");
            var decimals = int.Parse(ExpectNumber());
            ExpectToken(">");

            if (decimals < 1 || decimals > 32)
            {
                throw new CompilerException("invalid decimals: " + decimals);
            }

            return (DecimalVarType)VarType.Find(VarKind.Decimal, decimals);
        }

        private ArrayVarType ParseArray()
        {
            ExpectToken("<");
            var elementType = ExpectType();
            ExpectToken(">");

            return (ArrayVarType)VarType.Find(VarKind.Array, elementType);
        }

        private MethodParameter[] ParseParameters(Scope scope)
        {
            var list = new List<MethodParameter>();

            ExpectToken("(");

            do
            {
                var token = FetchToken();

                if (token.value == ")")
                {
                    break;                
                }
                else
                {
                    Rewind();
                }

                if (list.Count > 0)
                {
                    ExpectToken(",");
                }

                var name = ExpectIdentifier();
                ExpectToken(":");
                var type = ExpectType();

                list.Add(new MethodParameter(name, type));

            } while (true);


            return list.ToArray();
        }

        private StatementBlock ParseCommandBlock(Scope scope, MethodDeclaration method)
        {
            var block = new StatementBlock(scope);
            var terminateEarly = false;

            do
            {
                var token = FetchToken();

                switch (token.value)
                {
                    case "}":
                        Rewind();
                        return block;

                    case "emit":
                        {
                            var contract = scope.Module as Contract;
                            if (contract != null)
                            {
                                var eventName = ExpectIdentifier();

                                ExpectToken("(");

                                var addrExpr = ExpectExpression(scope);
                                ExpectToken(",");
                                var valueExpr = ExpectExpression(scope);
                                ExpectToken(")");
                                ExpectToken(";");

                                if (contract.Events.ContainsKey(eventName))
                                {
                                    var evt = contract.Events[eventName];

                                    if (addrExpr.ResultType.Kind != VarKind.Address)
                                    {
                                        throw new CompilerException($"Expected first argument of type {VarKind.Address}, got {addrExpr.ResultType} instead");
                                    }

                                    if (evt.returnType != valueExpr.ResultType)
                                    {
                                        throw new CompilerException($"Expected second argument of type {evt.returnType}, got {valueExpr.ResultType} instead");
                                    }

                                    block.Commands.Add(new EmitStatement(evt, addrExpr, valueExpr));
                                }
                                else
                                {
                                    throw new CompilerException($"Undeclared event: {eventName}");
                                }

                            }
                            else
                            {
                                throw new CompilerException("Emit statments only allowed in contracts");
                            }

                            break;
                        }

                    case "return":
                        {
                            var temp = FetchToken();
                            Rewind();

                            Expression expr;
                            if (temp.value != ";")
                            {
                                expr = ExpectExpression(scope);
                            }
                            else
                            {
                                expr = null;
                            }

                            block.Commands.Add(new ReturnStatement(method, expr));
                            ExpectToken(";");

                            if (!method.@interface.IsMulti || expr == null)
                            {
                                terminateEarly = true;
                            }
                            break;
                        }

                    case "throw":
                        {
                            var expr = ExpectExpression(scope);

                            if (expr.ResultType.Kind != VarKind.String)
                            {
                                throw new CompilerException("string expression expected");
                            }

                            block.Commands.Add(new ThrowStatement(expr));
                            ExpectToken(";");

                            terminateEarly = true;
                            break;
                        }

                    case "asm":
                        {
                            ExpectToken("{");
                            var lines = ExpectAsm();
                            ExpectToken("}");

                            block.Commands.Add(new AsmBlockStatement(lines));
                            break;
                        }

                    case "local":
                        {
                            AssignStatement initCmd;
                            ParseVariableDeclaration(scope, out initCmd);
                            if (initCmd != null)
                            {
                                block.Commands.Add(initCmd);
                            }

                            break;
                        }

                    case "if":
                        {
                            var ifCommand = new IfStatement(scope);

                            ExpectToken("(");
                            ifCommand.condition = ExpectExpression(scope);

                            if (ifCommand.condition.ResultType.Kind != VarKind.Bool)
                            {
                                throw new CompilerException($"condition must be boolean expression");
                            }

                            ExpectToken(")");

                            ExpectToken("{");

                            ifCommand.body = ParseCommandBlock(ifCommand.Scope, method);

                            ExpectToken("}");

                            var next = FetchToken();

                            if (next.value == "else")
                            {
                                bool skipBraces = false;

                                var ahead = FetchToken();
                                if (ahead.value == "if")
                                {
                                    skipBraces = true;
                                }

                                Rewind();

                                if (!skipBraces) ExpectToken("{");

                                ifCommand.@else = ParseCommandBlock(ifCommand.Scope, method);

                                if (!skipBraces) ExpectToken("}");
                            }
                            else
                            {
                                Rewind();
                            }

                            block.Commands.Add(ifCommand);
                            break;
                        }

                    case "switch":
                        {
                            var switchCommand = new SwitchStatement(scope);

                            ExpectToken("(");
                            switchCommand.variable = ExpectExpression(scope) as VarExpression;

                            if (switchCommand.variable == null)
                            {
                                throw new CompilerException($"switch condition must be variable expression");
                            }

                            ExpectToken(")");

                            ExpectToken("{");

                            while (true)
                            {
                                var next = FetchToken();

                                if (next.value == "}")
                                {
                                    break;
                                }
                                else
                                if (next.value == "default")
                                {
                                    ExpectToken(":");
                                    switchCommand.@default = ParseCommandBlock(switchCommand.Scope, method);
                                    break;
                                }

                                Rewind();
                                ExpectToken("case");

                                var literal = ParseExpression(scope, false) as LiteralExpression;

                                if (literal == null)
                                {
                                    throw new CompilerException($"switch case condition must be literal expression");
                                }

                                ExpectToken(":");

                                var body = ParseCommandBlock(switchCommand.Scope, method);

                                switchCommand.cases.Add(new CaseStatement(literal, body));
                            }

                            ExpectToken("}");

                            block.Commands.Add(switchCommand);
                            break;
                        }

                    case "for":
                        {
                            var forCommand = new ForStatement(scope);

                            ExpectToken("(");

                            bool mustExist;

                            var next = FetchToken();
                            if (next.value == "local")
                            {
                                mustExist = false;
                            }
                            else
                            {
                                mustExist = true;
                                Rewind();
                            }

                            AssignStatement initCmd;
                            forCommand.loopVar = ParseVariableDeclaration(scope, out initCmd, mustExist);

                            if (initCmd == null)
                            {
                                throw new CompilerException("variable missing initialization statement in for loop");
                            }
                            else
                            {
                                forCommand.initStatement = initCmd;
                            }

                            forCommand.condition = ExpectExpression(scope);

                            if (forCommand.condition.ResultType.Kind != VarKind.Bool)
                            {
                                throw new CompilerException($"condition must be boolean expression");
                            }

                            ExpectToken(";");

                            var varName = ExpectIdentifier();
                            if (varName != forCommand.loopVar.Name)
                            {
                                throw new CompilerException($"expected variable {varName} (temporary compiler limitation, no complex for statements supported!)");
                            }

                            next = FetchToken();

                            if (next.kind == TokenKind.Postfix)
                            {
                                forCommand.loopStatement = BuildPostfixStatement(scope, varName, next.value);
                            }
                            else
                            if (next.kind == TokenKind.Operator && next.value.EndsWith("="))
                            {
                                forCommand.loopStatement = BuildBinaryShortAssigment(scope, varName, next.value);
                            }
                            else
                            {
                                throw new CompilerException($"expected simple statement using {varName} (temporary compiler limitation, no complex for statements supported!)");
                            }

                            ExpectToken(")");

                            ExpectToken("{");

                            forCommand.body = ParseCommandBlock(forCommand.Scope, method);

                            ExpectToken("}");

                            block.Commands.Add(forCommand);
                            break;
                        }


                    case "while":
                        {
                            var whileCommand = new WhileStatement(scope);

                            ExpectToken("(");
                            whileCommand.condition = ExpectExpression(scope);

                            if (whileCommand.condition.ResultType.Kind != VarKind.Bool)
                            {
                                throw new CompilerException($"condition must be boolean expression");
                            }

                            ExpectToken(")");

                            ExpectToken("{");

                            whileCommand.body = ParseCommandBlock(whileCommand.Scope, method);

                            ExpectToken("}");

                            block.Commands.Add(whileCommand);
                            break;
                        }

                    case "do":
                        {
                            var whileCommand = new DoWhileStatement(scope);

                            ExpectToken("{");

                            whileCommand.body = ParseCommandBlock(whileCommand.Scope, method);

                            ExpectToken("}");

                            ExpectToken("while");

                            ExpectToken("(");
                            whileCommand.condition = ExpectExpression(scope);

                            if (whileCommand.condition.ResultType.Kind != VarKind.Bool)
                            {
                                throw new CompilerException($"condition must be boolean expression");
                            }

                            ExpectToken(")");
                            ExpectToken(";");

                            block.Commands.Add(whileCommand);
                            break;
                        }

                    case "break":
                        {
                            ExpectToken(";");

                            block.Commands.Add(new BreakStatement(scope));
                            terminateEarly = true;
                            break;
                        }

                    case "continue":
                        {
                            ExpectToken(";");

                            block.Commands.Add(new ContinueStatement(scope));
                            terminateEarly = true;
                            break;
                        }

                    default:
                        if (token.kind == TokenKind.Identifier)
                        {
                            var next = FetchToken();

                            if (next.kind == TokenKind.Postfix)
                            {
                                var setCommand = BuildPostfixStatement(scope, token.value, next.value);
                                block.Commands.Add(setCommand);
                            }
                            else
                            if (next.kind == TokenKind.Operator && next.value.EndsWith("="))
                            {
                                var setCommand = BuildBinaryShortAssigment(scope, token.value, next.value);
                                block.Commands.Add(setCommand);
                            }
                            else
                            if (next.kind == TokenKind.Selector)
                            {
                                var varDecl = scope.FindVariable(token.value, false);
                                bool isStructField = false;

                                LibraryDeclaration libDecl;

                                if (varDecl != null)
                                {
                                    switch (varDecl.Type.Kind)
                                    {
                                        case VarKind.Storage_Map:
                                            {
                                                var mapDecl = (MapDeclaration)varDecl;
                                                libDecl = scope.Module.FindLibrary("Map");
                                                libDecl = libDecl.PatchMap(mapDecl);
                                                break;
                                            }

                                        case VarKind.Storage_List:
                                            {
                                                var listDecl = (ListDeclaration)varDecl;
                                                libDecl = scope.Module.FindLibrary("List");
                                                libDecl = libDecl.PatchList(listDecl);
                                                break;
                                            }

                                        case VarKind.Storage_Set:
                                            {
                                                var setDecl = (SetDeclaration)varDecl;
                                                libDecl = scope.Module.FindLibrary("Set");
                                                libDecl = libDecl.PatchSet(setDecl);
                                                break;
                                            }

                                        case VarKind.Struct:
                                            {
                                                libDecl = null;
                                                isStructField = true;
                                                break;
                                            }


                                        default:
                                            throw new CompilerException($"expected {token.value} to be generic type, but is {varDecl.Type} instead");
                                    }
                                }
                                else
                                {
                                    libDecl = scope.Module.FindLibrary(token.value);
                                }

                                if (isStructField)
                                {
                                    var fieldName = ExpectIdentifier();

                                    next = FetchToken();

                                    if (next.kind == TokenKind.Operator && next.value.EndsWith("="))
                                    {
                                        var structName = ((StructVarType)varDecl.Type).name;
                                        if (!_structs.ContainsKey(structName))
                                        {
                                            throw new CompilerException("unknown struct: " + structName);
                                        }

                                        var structDecl = _structs[structName];
                                        var fieldDecl = structDecl.fields.FirstOrDefault(x => x.name == fieldName);

                                        var assignment = new AssignStatement();
                                        assignment.variable = varDecl;
                                        assignment.keyExpression = new LiteralExpression(scope, "\"" + fieldName + "\"" , fieldDecl.type);
                                        assignment.valueExpression = ParseAssignmentExpression(scope, next.value, varDecl, fieldDecl.type);
                                        block.Commands.Add(assignment);
                                    }
                                    else
                                    {
                                        throw new CompilerException($"expected assignment operator");
                                    }
                                }
                                else
                                {
                                    var methodCall = new MethodCallStatement();
                                    methodCall.expression = ParseMethodExpression(scope, libDecl, varDecl);

                                    block.Commands.Add(methodCall);
                                }
                            }
                            else
                            {
                                if (next.kind == TokenKind.Separator)
                                {
                                    var next2 = FetchToken();

                                    if (next2.value == "=")
                                    {
                                        throw new CompilerException("deprecated assignment operator ':='. Use just '=' instead");
                                    }
                                }

                                throw new CompilerException("unexpected token: " + token.value);
                            }

                            ExpectToken(";");
                        }
                        else
                        {
                            throw new CompilerException("unexpected token: " + token.value);
                        }

                        break;
                }
            } while (!terminateEarly);

            if (terminateEarly)
            {
                if (block.Commands.Count > 1)
                {
                    ExpectToken("}");
                    Rewind();
                }

                return block;
            }
            else
            {
                throw new CompilerException("weird compiler flow detected, contact devs");
            }
        }

        private VarDeclaration ParseVariableDeclaration(Scope scope, out AssignStatement assignment, bool mustExist = false)
        {
            var varName = ExpectIdentifier();

            var tmp = FetchToken();
            VarType type = null;

            if (tmp.value == ":")
            {
                type = ExpectType();
            }
            else
            {
                Rewind();
            }

            Expression initExpr = ParseVariableInitialization(scope, ref type);

            VarDeclaration varDecl;

            if (mustExist)
            {
                varDecl = scope.FindVariable(varName);
            }
            else
            {
                varDecl = new VarDeclaration(scope, varName, type, VarStorage.Local);
                scope.AddVariable(varDecl);
            }

            if (initExpr != null)
            {
                var initCmd = new AssignStatement();
                initCmd.variable = varDecl;
                initCmd.valueExpression = initExpr;
                assignment = initCmd;
            }
            else
            {
                assignment = null;
            }

            ExpectToken(";");

            return varDecl;
        }


        protected override VarType ExpectType()
        {
            var token = FetchToken();

            if (token.kind != TokenKind.Type)
            {
                if (token.value.Equals("None", StringComparison.OrdinalIgnoreCase))
                {
                    return VarType.Find(VarKind.None);
                }

                if (_structs.ContainsKey(token.value))
                {
                    return VarType.Find(VarKind.Struct, token.value);
                }

                if (_enums.ContainsKey(token.value))
                {
                    return VarType.Find(VarKind.Enum, token.value);
                }

                throw new CompilerException($"expected type, got {token.kind} [{token.value}]");
            }

            if (token.value == "decimal")
            {
                return ParseDecimal();
            }

            if (token.value == "array")
            {
                return ParseArray();
            }

            var kind = (VarKind)Enum.Parse(typeof(VarKind), token.value, true);
            return VarType.Find(kind);
        }
    }
}
