using Phantasma.Tomb.AST.Expressions;
using Phantasma.Tomb.CodeGen;
using System;

namespace Phantasma.Tomb.AST.Statements
{
	public class CaseStatement : Statement
	{
		public LiteralExpression value;
		public StatementBlock body;

		internal Register variable;
		internal string endLabel;

		public CaseStatement(LiteralExpression value, StatementBlock body) : base()
		{
			this.value = value;
			this.body = body;
		}

		public override void GenerateCode(CodeGenerator output)
		{
			var reg = value.GenerateCode(output);

			output.AppendLine(this, $"EQUAL {variable} {reg} {reg}");

			output.AppendLine(this, $"JMPNOT {reg} @skip_{this.NodeID}");
			body.GenerateCode(output);
			output.AppendLine(this, $"JMP {endLabel}");
			output.AppendLine(this, $"@skip_{this.NodeID}: NOP");

			Compiler.Instance.DeallocRegister(ref reg);
		}

		public override bool IsNodeUsed(Node node)
		{
			return value.IsNodeUsed(node) || body.IsNodeUsed(node);
		}

		public override void Visit(Action<Node> callback)
		{
			value.Visit(callback);
			body.Visit(callback);
		}
	}

}

