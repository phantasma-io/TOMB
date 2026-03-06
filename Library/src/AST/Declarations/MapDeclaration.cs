namespace Phantasma.Tomb.AST.Declarations
{
	public class MapDeclaration : VarDeclaration
	{
		public VarType KeyKind;
		public VarType ValueKind;

		public MapDeclaration(Scope parentScope, string name, VarType keyKind, VarType valKind) : base(parentScope, name, VarType.Find(VarKind.Storage_Map), VarStorage.Global)
		{
			this.KeyKind = keyKind;
			this.ValueKind = valKind;
		}
	}
}
