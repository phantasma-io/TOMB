using Phantasma.Tomb.CodeGen;
using System;

namespace Phantasma.Tomb.AST.Statements
{
	public class WhileStatement : LoopStatement
	{
		public Expression? condition;
		public StatementBlock? body;
		public Scope Scope { get; }

		//private int label;

		public WhileStatement(Scope parentScope) : base()
		{
			this.Scope = new Scope(parentScope, this.NodeID);
			//this.label = Parser.Instance.AllocateLabel();
		}

		private Expression RequireCondition()
		{
			if (condition != null)
			{
				return condition;
			}

			throw new CompilerException("while-loop condition not initialized");
		}

		private StatementBlock RequireBody()
		{
			if (body != null)
			{
				return body;
			}

			throw new CompilerException("while-loop body not initialized");
		}

		public override void Visit(Action<Node> callback)
		{
			callback(this);

			RequireCondition().Visit(callback);
			RequireBody().Visit(callback);
		}

		public override bool IsNodeUsed(Node node)
		{
			return (node == this) || RequireCondition().IsNodeUsed(node) || RequireBody().IsNodeUsed(node);
		}

		public override void GenerateCode(CodeGenerator output)
		{
			Compiler.Instance.PushLoop(this);

			output.AppendLine(this, $"@loop_start_{this.NodeID}: NOP");

			var whileCondition = RequireCondition();
			var whileBody = RequireBody();
			Register? reg = whileCondition.GenerateCode(output);

			this.Scope.Enter(output);

			output.AppendLine(this, $"JMPNOT {reg} @loop_end_{this.NodeID}");
			whileBody.GenerateCode(output);

			output.AppendLine(this, $"JMP @loop_start_{this.NodeID}");
			output.AppendLine(this, $"@loop_end_{this.NodeID}: NOP");

			this.Scope.Leave(output);

			Compiler.Instance.DeallocRegister(ref reg);
			Compiler.Instance.PopLoop(this);
		}
	}

}
