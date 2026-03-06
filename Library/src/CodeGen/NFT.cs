using System.Collections.Generic;
using Phantasma.Business.Blockchain.Tokens;
using PhantasmaPhoenix.Protocol;
using Phantasma.Core.Domain.Contract;
using Phantasma.Core.Domain.Contract.Structs;
using PhantasmaPhoenix.VM;
using Phantasma.Tomb.AST;
using Phantasma.Tomb.AST.Declarations;

namespace Phantasma.Tomb.CodeGen
{
    public class NFT : Contract
    {
        public VarType romType;
        public VarType ramType;

        public StructVarType nftType;

        public NFT(string name, VarType romType, VarType ramType, Module parent) : base(name, ModuleKind.NFT, parent)
        {
            this.romType = romType;
            this.ramType = ramType;

            this.nftType = (StructVarType)VarType.Find(VarKind.Struct, name);
            //this.nftType.decl = new StructDeclaration(name, new[] { new StructField("id", VarKind.Number), new StructField("owner", VarKind.Address), new StructField("chain", VarKind.String), new StructField("rom", romType), new StructField("ram", ramType) });

            this.Scope.AddVariable(new VarDeclaration(this.Scope, "_tokenID", VarType.Find(VarKind.Number), VarStorage.NFT));
            this.Scope.AddVariable(new VarDeclaration(this.Scope, "_seriesID", VarType.Find(VarKind.Number), VarStorage.NFT));
            this.Scope.AddVariable(new VarDeclaration(this.Scope, "_mintID", VarType.Find(VarKind.Number), VarStorage.NFT));
            this.Scope.AddVariable(new VarDeclaration(this.Scope, "_ROM", romType, VarStorage.NFT));
            this.Scope.AddVariable(new VarDeclaration(this.Scope, "_RAM", ramType, VarStorage.NFT));
        }

        public override ContractInterface GenerateCode(CodeGenerator output)
        {
            var abi = base.GenerateCode(output);

            var nftStandard = TokenUtils.GetNFTStandard();

            // convert ABI parameters
            var methods = new List<ContractMethod>();
            foreach (var method in abi.Methods)
            {
                if (nftStandard.HasMethod(method.name))
                {
                    var convertedMethod = new ContractMethod(method.name, method.returnType, method.offset, new[] { new ContractParameter("tokenID", VMType.Number) });
                    methods.Add(convertedMethod);
                }
                else
                {
                    methods.Add(method);
                }
            }

            abi = new ContractInterface(methods, abi.Events);
            return abi;
        }
    }
}
