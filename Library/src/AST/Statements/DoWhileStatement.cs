using Phantasma.Tomb.CodeGen;
using System;

namespace Phantasma.Tomb.AST.Statements
{
	public class DoWhileStatement : LoopStatement
	{
		public Expression? condition;
		public StatementBlock? body;
		public Scope Scope { get; }

		//private int label;

		public DoWhileStatement(Scope parentScope) : base()
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

			throw new CompilerException("do-while condition not initialized");
		}

		private StatementBlock RequireBody()
		{
			if (body != null)
			{
				return body;
			}

			throw new CompilerException("do-while body not initialized");
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

			this.Scope.Enter(output);

			var doWhileBody = RequireBody();
			doWhileBody.GenerateCode(output);

			var doWhileCondition = RequireCondition();
			Register? reg = doWhileCondition.GenerateCode(output);
			output.AppendLine(this, $"JMPIF {reg} @loop_start_{this.NodeID}");

			output.AppendLine(this, $"@loop_end_{this.NodeID}: NOP");

			this.Scope.Leave(output);

			Compiler.Instance.DeallocRegister(ref reg);
			Compiler.Instance.PopLoop(this);
		}
	}

}
