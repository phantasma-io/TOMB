namespace Phantasma.Tomb.AST.Declarations
{
	public class DecimalDeclaration : VarDeclaration
	{
		public readonly int Decimals;

		public DecimalDeclaration(Scope parentScope, string name, int decimals, VarStorage storage) : base(parentScope, name, VarType.Find(VarKind.Decimal, decimals), storage)
		{
			this.Decimals = decimals;
		}
	}
}
