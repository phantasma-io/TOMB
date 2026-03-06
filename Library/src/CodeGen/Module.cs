using Phantasma.Business.Blockchain.Tokens;
using Phantasma.Business.CodeGen.Assembler;
using Phantasma.Core.Domain.Contract;
using PhantasmaPhoenix.VM;
using Phantasma.Core.Domain.VM.Structs;
using PhantasmaPhoenix.Core;
using Phantasma.Tomb.AST;
using Phantasma.Tomb.AST.Declarations;
using Phantasma.Tomb.AST.Expressions;

namespace Phantasma.Tomb.CodeGen
{
    public enum ModuleKind
    {
        Contract,
        Script,
        Description,
        Token,
        Account,
        Organization,
        NFT,
        Test,
    }


    public abstract partial class Module : Node
    {
        public readonly string Name;

        public readonly ModuleKind Kind;
        public Scope Scope { get; }

        public readonly Module Parent;

        public readonly Dictionary<string, LibraryDeclaration> Libraries = new Dictionary<string, LibraryDeclaration>();


        public readonly LibraryDeclaration library;

        // only available after compilation
        public byte[] script { get; private set; }
        public string asm { get; private set; }
        public ContractInterface abi { get; private set; }
        public DebugInfo debugInfo { get; private set; }

        private List<Module> _subModules = new List<Module>();
        public IEnumerable<Module> SubModules => _subModules;

        public Module(string name, ModuleKind kind, Module parent = null)
        {
            this.Name = name;
            this.Kind = kind;
            this.Parent = parent;
            this.Scope = new Scope(this);
            this.library = new LibraryDeclaration(Scope, "this");
            this.Libraries[library.Name] = library;

            ImportLibrary("String");
            ImportLibrary("Bytes");
            ImportLibrary("Decimal");
            ImportLibrary("Enum");
        }

        public abstract MethodDeclaration FindMethod(string name);

        public void AddSubModule(Module subModule)
        {
            _subModules.Add(subModule);
        }

        public Module FindModule(string name, bool mustBeCompiled)
        {
            foreach (var module in _subModules)
            {
                if (module.Name == name)
                {
                    return module;
                }
            }

            return Compiler.Instance.FindModule(name, mustBeCompiled);
        }

        public LibraryDeclaration FindLibrary(string name, bool required = true)
        {
            if (name != name.UppercaseFirst() && name != "this")
            {
                if (required)
                {
                    throw new CompilerException("invalid library name: " + name);
                }
                else
                {
                    return null;
                }
            }

            if (Libraries.ContainsKey(name))
            {
                return Libraries[name];
            }

            if (required)
            {
                throw new CompilerException("possibly unimported library: " + name);
            }

            return null;
        }

        private static Register ConvertFieldToStorageAccessRead(CodeGenerator output, Scope scope, Expression expression)
        {
            return ConvertFieldToStorageAccess(output, scope, expression, true);
        }

        private static Register ConvertFieldToStorageAccessWrite(CodeGenerator output, Scope scope, Expression expression)
        {
            return ConvertFieldToStorageAccess(output, scope, expression, false);
        }

        private static Register ConvertFieldToStorageAccess(CodeGenerator output, Scope scope, Expression expression, bool insertContract)
        {
            if (expression.ResultType.Kind != VarKind.String)
            {
                throw new System.Exception("expected string expression for field key");
            }

            var reg = expression.GenerateCode(output);

            //output.AppendLine(expression, $"LOAD {reg} {reg} // field name");

            if (insertContract)
            {
                output.AppendLine(expression, $"PUSH {reg}");
                output.AppendLine(expression, $"LOAD {reg} \"{scope.Module.Name}\" // contract name");
            }

            return reg;
        }

        private static Register ConvertFieldToBytes(CodeGenerator output, Scope scope, Expression expr)
        {
            var reg = expr.GenerateCode(output);
            output.AppendLine(expr, $"CAST {reg} {reg} #{VMType.Bytes}");
            return reg;
        }

        private static Register ConvertFieldToContractWithName(CodeGenerator output, Scope scope, Expression expression)
        {
            return ConvertFieldToContract(output, scope, expression, true);
        }

        private static Register ConvertFieldToContractWithoutName(CodeGenerator output, Scope scope, Expression expression)
        {
            return ConvertFieldToContract(output, scope, expression, false);
        }

        private static Register ConvertFieldToContract(CodeGenerator output, Scope scope, Expression expression, bool withName)
        {
            var literal = expression as LiteralExpression;
            if (literal == null)
            {
                throw new CompilerException("nft argument is not a literal value");
            }

            var module = scope.Module.FindModule(literal.value, true);

            var abi = module.abi;

            if (module.Kind == ModuleKind.NFT)
            {
                var nftStandard = TokenUtils.GetNFTStandard();
                if (!abi.Implements(nftStandard))
                {
                    throw new CompilerException($"nft {literal.value} not does implement NFT standard");
                }
            }

            var reg = Compiler.Instance.AllocRegister(output, expression);

            var abiBytes = module.abi.ToByteArray();

            output.AppendLine(expression, $"LOAD {reg} 0x{Base16.Encode(abiBytes)} // abi");
            output.AppendLine(expression, $"PUSH {reg}");

            output.AppendLine(expression, $"LOAD {reg} 0x{Base16.Encode(module.script)} // script");

            if (withName)
            {
                output.AppendLine(expression, $"PUSH {reg}"); // push script

                output.AppendLine(expression, $"LOAD {reg} \"{module.Name}\" // name");
            }

            //            output.AppendLine(expression, $"PUSH {reg}"); // not necessary because this is done after this call

            return reg;
        }

        private static Register ConvertGenericResult(CodeGenerator output, Scope scope, MethodCallExpression method, Register reg) 
        {
            if (method.ResultType.IsWeird)
            {
                throw new CompilerException($"possible compiler bug detected on call to method {method.method.Name}");
            }

            switch (method.ResultType.Kind)
            {
                case VarKind.Bytes:
                    break;

                case VarKind.Struct:
                    output.AppendLine(method, $"UNPACK {reg} {reg}");
                    break;

                default:
                    method.CallNecessaryConstructors(output, method.ResultType, reg);
                    break;
            }

            return reg;
        }

        public abstract ContractInterface GenerateCode(CodeGenerator output);

        protected virtual void ProcessABI(ContractInterface abi, DebugInfo debugInfo)
        {
            // do nothing
        }

        public void Compile()
        {
            foreach (var subModule in this.SubModules)
            {
                subModule.Compile();
            }

            var codeGen = new CodeGenerator();
            abi = this.GenerateCode(codeGen);

            Compiler.Instance.VerifyRegisters();

            asm = codeGen.ToString();

            var lines = asm.Split('\n');
            DebugInfo temp;
            Dictionary<string, int> labels;


            try
            {
                script = AssemblerUtils.BuildScript(lines, this.Name, out temp, out labels);
            }
            catch (Exception e)
            {
                var outputFile = Path.Combine(Directory.GetCurrentDirectory(), "output.asm");
                System.IO.File.WriteAllText(outputFile, string.Join('\n', lines));
                Console.WriteLine("Dumped into "+ outputFile);
                throw e;
            }

            this.debugInfo = temp;

            lines = AssemblerUtils.CommentOffsets(lines, this.debugInfo).ToArray();

            ProcessABI(abi, this.debugInfo);

            asm = string.Join('\n', lines);
        }

        public void MergeConsts(IEnumerable<ConstDeclaration> consts)
        {
            foreach (var entry in consts)
            {
                entry.ParentScope = this.Scope;
                this.Scope.AddConstant(entry);
            }
        }
    }
}