using Phantasma.Tomb.CodeGen;

using System;

namespace Phantasma.Tomb.AST.Expressions
{
	public class NegationExpression : Expression
	{
		public Expression expr;
		public override VarType ResultType => expr.ResultType;

		public NegationExpression(Scope parentScope, Expression expr) : base(parentScope)
		{
			this.expr = expr;
		}

		public override Register GenerateCode(CodeGenerator output)
		{
			var type = expr.ResultType;

			var reg = expr.GenerateCode(output);

			switch (type.Kind)
			{
				case VarKind.Bool:
					output.AppendLine(this, $"NOT {reg} {reg}");
					break;

				case VarKind.Number:
					output.AppendLine(this, $"NEGATE {reg} {reg}");
					break;

				default:
					throw new CompilerException("Cannot negate expression of type: " + type);
			}

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

		public override string ToString()
		{
			return "!" + expr.ToString();
		}
	}

}
