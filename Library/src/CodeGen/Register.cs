namespace Phantasma.Tomb.CodeGen
{
	public class Register
	{
		public readonly int Index;
		public readonly string? Alias;

		public readonly static Register Temporary = new Register(0);

		public Register(int index, string? alias = null)
		{
			Index = index;
			Alias = alias;
		}

		public override string ToString()
		{
			if (Alias != null)
			{
				return "$" + Alias;

			}
			return "r" + Index;
		}
	}
}
