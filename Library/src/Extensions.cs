using Phantasma.Tomb.CodeGen;
using Phantasma.Tomb.AST;

namespace Phantasma.Tomb
{
	public static class Extensions
	{
		public static string UppercaseFirst(this string s)
		{
			if (string.IsNullOrEmpty(s))
			{
				return string.Empty;
			}
			char[] a = s.ToCharArray();
			a[0] = char.ToUpper(a[0]);
			return new string(a);
		}
		public static bool IsLogicalOperator(this OperatorKind op)
		{
			return op != OperatorKind.Unknown && op < OperatorKind.Addition;
		}

		public static void CallNecessaryConstructors(this Node node, CodeGenerator output, VarType type, Register reg)
		{
			CallNecessaryConstructors(node, output, type.Kind, reg);
		}

		public static void CallNecessaryConstructors(this Node node, CodeGenerator output, VarKind kind, Register reg)
		{
			switch (kind)
			{
				case VarKind.Hash:
				case VarKind.Address:
				case VarKind.Timestamp:
					{
						var constructorName = kind.ToString();
						output.AppendLine(node, $"PUSH {reg}");
						output.AppendLine(node, $"EXTCALL \"{constructorName}()\"");
						output.AppendLine(node, $"POP {reg}");
						break;
					}

				case VarKind.Struct:
					{
						output.AppendLine(node, $"UNPACK {reg} {reg}");
						break;
					}
			}
		}

	}
}
