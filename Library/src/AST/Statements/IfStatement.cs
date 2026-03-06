using Phantasma.Tomb.CodeGen;
using System;

namespace Phantasma.Tomb.AST.Statements
{
	public class IfStatement : Statement
	{
		public Expression condition;
		public StatementBlock body;
		public StatementBlock @else;
		public Scope Scope { get; }

		//private int label;

		public IfStatement(Scope parentScope) : base()
		{
			this.Scope = new Scope(parentScope, this.NodeID);
			//this.label = Parser.Instance.AllocateLabel();
		}

		public override void Visit(Action<Node> callback)
		{
			callback(this);

			condition.Visit(callback);
			body.Visit(callback);
			@else?.Visit(callback);
		}

		public override bool IsNodeUsed(Node node)
		{
			if (@else != null && @else.IsNodeUsed(node))
			{
				return true;
			}

			return (node == this) || condition.IsNodeUsed(node) || body.IsNodeUsed(node);
		}

		public override void GenerateCode(CodeGenerator output)
		{
			var reg = condition.GenerateCode(output);

			this.Scope.Enter(output);
			if (@else != null)
			{
				output.AppendLine(this, $"JMPNOT {reg} @else_{this.NodeID}");
				body.GenerateCode(output);
				output.AppendLine(this, $"JMP @then_{this.NodeID}");
				output.AppendLine(this, $"@else_{this.NodeID}: NOP");
				@else.GenerateCode(output);
			}
			else
			{
				output.AppendLine(this, $"JMPNOT {reg} @then_{this.NodeID}");
				body.GenerateCode(output);
			}
			output.AppendLine(this, $"@then_{this.NodeID}: NOP");
			this.Scope.Leave(output);

			Compiler.Instance.DeallocRegister(ref reg);

		}
	}

}

