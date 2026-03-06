using Phantasma.Tomb.CodeGen;
using System;

namespace Phantasma.Tomb.AST.Statements
{
	public class ThrowStatement : Statement
	{
		public readonly Expression expr;

		public ThrowStatement(Expression expr) : base()
		{
			this.expr = expr;
		}

		public override void Visit(Action<Node> callback)
		{
			callback(this);
		}

		public override bool IsNodeUsed(Node node)
		{
			return (node == this);
		}

		public override void GenerateCode(CodeGenerator output)
		{
			var reg = expr.GenerateCode(output);
			output.AppendLine(this, $"THROW {reg}");
			Compiler.Instance.DeallocRegister(ref reg);
		}
	}

}

