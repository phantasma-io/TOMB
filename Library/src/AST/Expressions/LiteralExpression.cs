using System.Numerics;
using Phantasma.Tomb.CodeGen;

using System;
using System.Globalization;
using PhantasmaPhoenix.Core;

namespace Phantasma.Tomb.AST.Expressions
{
	public class LiteralExpression : Expression
	{
		public string value;
		public VarType type;

		public LiteralExpression(Scope parentScope, string value, VarType type) : base(parentScope)
		{
			this.value = value;
			this.type = type;
		}

		public override string ToString()
		{
			return "literal: " + value;
		}

		public override T AsLiteral<T>()
		{
			if ((this.type.Kind == VarKind.String || this.type.Kind == VarKind.Method) && typeof(T) == typeof(string))
			{
				return (T)(object)this.value;
			}

			if (this.type.Kind == VarKind.Module && typeof(T) == typeof(Module))
			{
				var module = Compiler.Instance.FindModule(this.value, true);
				if (module == null)
				{
					throw new CompilerException("module not found: " + this.value);
				}

				return (T)(object)module;
			}

			return base.AsLiteral<T>();
		}

		public override Register GenerateCode(CodeGenerator output)
		{
			var reg = Compiler.Instance.AllocRegister(output, this, this.NodeID);

			string val;

			switch (this.type.Kind)
			{
				case VarKind.Decimal:
					{
						if (this.type is not DecimalVarType decType)
						{
							throw new CompilerException("decimal literal expected decimal type metadata");
						}

						decimal temp;

						if (!decimal.TryParse(this.value, NumberStyles.Number, CultureInfo.InvariantCulture, out temp))
						{
							throw new CompilerException("Invalid decimal literal: " + this.value);
						}

						val = UnitConversion.ToBigInteger(temp, (uint)decType.decimals).ToString();
						break;
					}

				case VarKind.Enum:
					{
						val = $"{this.value} Enum";
						break;
					}

				case VarKind.Type:
					{
						var srcType = (VarKind)Enum.Parse(typeof(VarKind), this.value);
						var vmType = MethodInterface.ConvertType(srcType);
						val = $"{(int)vmType} Enum";
						break;
					}

				default:
					{
						switch (this.type.Kind)
						{
							case VarKind.Bool:
								if (!(this.value.Equals("false", StringComparison.OrdinalIgnoreCase) || this.value.Equals("true", StringComparison.OrdinalIgnoreCase)))
								{
									throw new CompilerException("Invalid bool literal: " + this.value);
								}
								break;

							case VarKind.Number:
								{
									BigInteger temp;

									if (!BigInteger.TryParse(this.value, out temp))
									{
										throw new CompilerException("Invalid number literal: " + this.value);
									}
									break;
								}
						}

						val = this.value;
						break;
					}

			}

			output.AppendLine(this, $"LOAD {reg} {val}");

			this.CallNecessaryConstructors(output, type, reg);

			return reg;
		}

		public override void Visit(Action<Node> callback)
		{
			callback(this);
		}

		public override bool IsNodeUsed(Node node)
		{
			return (node == this);
		}

		public override VarType ResultType => type;
	}

}
