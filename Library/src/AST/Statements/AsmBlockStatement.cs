using Phantasma.Tomb.CodeGen;
using System;

namespace Phantasma.Tomb.AST.Statements
{
	public class AsmBlockStatement : Statement
	{
		public string[] lines;

		public AsmBlockStatement(string[] lines) : base()
		{
			this.lines = lines;
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
			foreach (var line in lines)
			{
				output.AppendLine(this, line);
			}
		}
	}

}

