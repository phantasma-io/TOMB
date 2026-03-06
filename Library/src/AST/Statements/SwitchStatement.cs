using Phantasma.Tomb.AST.Expressions;
using Phantasma.Tomb.CodeGen;
using System;
using System.Collections.Generic;

namespace Phantasma.Tomb.AST.Statements
{
	public class SwitchStatement : Statement
	{
		public VarExpression? variable;
		public StatementBlock? @default;
		public List<CaseStatement> cases = new List<CaseStatement>();
		public Scope Scope { get; }

		//private int label;

		public SwitchStatement(Scope parentScope) : base()
		{
			this.Scope = new Scope(parentScope, this.NodeID);
			//this.label = Parser.Instance.AllocateLabel();
		}

		private VarExpression RequireVariable()
		{
			if (variable != null)
			{
				return variable;
			}

			throw new CompilerException("switch variable not initialized");
		}

		public override void Visit(Action<Node> callback)
		{
			callback(this);

			RequireVariable().Visit(callback);
			foreach (var entry in cases)
			{
				entry.Visit(callback);
			}

			@default?.Visit(callback);
		}

		public override bool IsNodeUsed(Node node)
		{
			foreach (var entry in cases)
			{
				if (entry.IsNodeUsed(node))
				{
					return true;
				}
			}

			if (@default != null && @default.IsNodeUsed(node))
			{
				return true;
			}

			return (node == this);
		}

		public override void GenerateCode(CodeGenerator output)
		{
			Register? reg = RequireVariable().GenerateCode(output);
			var endLabel = $"@end_case_{this.NodeID}";

			this.Scope.Enter(output);

			foreach (var entry in cases)
			{
				entry.variable = reg;
				entry.endLabel = endLabel;
				entry.GenerateCode(output);
			}

			if (@default != null)
			{
				@default.GenerateCode(output);
			}

			output.AppendLine(this, $"{endLabel}: NOP");
			this.Scope.Leave(output);

			Compiler.Instance.DeallocRegister(ref reg);

		}
	}

}
