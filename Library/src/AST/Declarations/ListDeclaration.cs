namespace Phantasma.Tomb.AST.Declarations
{
	public class ListDeclaration : VarDeclaration
	{
		public VarType ValueKind;

		public ListDeclaration(Scope parentScope, string name, VarType valKind) : base(parentScope, name, VarType.Find(VarKind.Storage_List), VarStorage.Global)
		{
			this.ValueKind = valKind;
		}
	}
}
