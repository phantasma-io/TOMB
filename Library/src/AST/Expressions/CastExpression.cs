using Phantasma.Tomb.CodeGen;

using System;

namespace Phantasma.Tomb.AST.Expressions
{
	public class CastExpression : Expression
	{
		public Expression expr;

		public override VarType ResultType { get; }

		public CastExpression(Scope parentScope, VarType resultType, Expression expr) : base(parentScope)
		{
			this.expr = expr;
			this.ResultType = resultType;
		}

		public override Register GenerateCode(CodeGenerator output)
		{
			var reg = expr.GenerateCode(output);

			switch (expr.ResultType.Kind)
			{
				case VarKind.Decimal:
					{
						switch (this.ResultType.Kind)
						{
							case VarKind.Number:
								return reg;

							case VarKind.Decimal:
								{
									var srcDecimals = ((DecimalVarType)expr.ResultType).decimals;
									var dstDecimals = ((DecimalVarType)this.ResultType).decimals;

									if (srcDecimals == dstDecimals)
									{
										return reg;
									}
									else
									if (srcDecimals < dstDecimals)
									{
										var diff = (dstDecimals - srcDecimals);
										var mult = (int)Math.Pow(10, diff);
										output.AppendLine(this, $"LOAD r0 {mult}");
										output.AppendLine(this, $"MUL {reg} r0 {reg}");
										return reg;
									}
									else
									{
										throw new CompilerException($"Decimal precision failure: {expr.ResultType} => {this.ResultType}");
									}
								}

							default:
								throw new CompilerException($"Unsupported cast: {expr.ResultType} => {this.ResultType}");
						}
					}
			}

			var vmType = MethodInterface.ConvertType(ResultType);
			output.AppendLine(this, $"CAST {reg} {reg} #{vmType}");
			return reg;
		}


		public override void Visit(Action<Node> callback)
		{
			callback(this);
			expr.Visit(callback);
		}

		public override bool IsNodeUsed(Node node)
		{
			return (node == this) || expr.IsNodeUsed(node);
		}
	}

}
