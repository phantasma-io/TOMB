namespace Phantasma.Tomb.AST.Declarations
{
	public class SetDeclaration : VarDeclaration
	{
		public VarType ValueKind;

		public SetDeclaration(Scope parentScope, string name, VarType valKind) : base(parentScope, name, VarType.Find(VarKind.Storage_Set), VarStorage.Global)
		{
			this.ValueKind = valKind;
		}
	}
}
