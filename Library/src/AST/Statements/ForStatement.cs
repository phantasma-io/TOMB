using Phantasma.Tomb.AST.Declarations;
using Phantasma.Tomb.CodeGen;
using System;

namespace Phantasma.Tomb.AST.Statements
{
	public class ForStatement : LoopStatement
	{
		public VarDeclaration? loopVar;
		public Expression? condition;
		public Statement? initStatement;
		public Statement? loopStatement;

		public StatementBlock? body;
		public Scope Scope { get; }

		//private int label;

		public ForStatement(Scope parentScope) : base()
		{
			this.Scope = new Scope(parentScope, this.NodeID);
			//this.label = Parser.Instance.AllocateLabel();
		}

		private VarDeclaration RequireLoopVar()
		{
			if (loopVar != null)
			{
				return loopVar;
			}

			throw new CompilerException("for-loop variable not initialized");
		}

		private Expression RequireCondition()
		{
			if (condition != null)
			{
				return condition;
			}

			throw new CompilerException("for-loop condition not initialized");
		}

		private Statement RequireInitStatement()
		{
			if (initStatement != null)
			{
				return initStatement;
			}

			throw new CompilerException("for-loop init statement not initialized");
		}

		private Statement RequireLoopStatement()
		{
			if (loopStatement != null)
			{
				return loopStatement;
			}

			throw new CompilerException("for-loop iteration statement not initialized");
		}

		private StatementBlock RequireBody()
		{
			if (body != null)
			{
				return body;
			}

			throw new CompilerException("for-loop body not initialized");
		}

		public override void Visit(Action<Node> callback)
		{
			callback(this);

			RequireLoopVar().Visit(callback);
			RequireInitStatement().Visit(callback);
			RequireCondition().Visit(callback);
			RequireLoopStatement().Visit(callback);
			RequireBody().Visit(callback);
		}

		public override bool IsNodeUsed(Node node)
		{
			return
				(node == this) ||
				RequireCondition().IsNodeUsed(node) ||
				RequireBody().IsNodeUsed(node) ||
				RequireLoopVar().IsNodeUsed(node) ||
				RequireLoopStatement().IsNodeUsed(node) ||
				RequireInitStatement().IsNodeUsed(node);
		}

		public override void GenerateCode(CodeGenerator output)
		{
			var forCondition = RequireCondition();
			var forBody = RequireBody();
			var init = RequireInitStatement();
			var loop = RequireLoopStatement();

			init.GenerateCode(output);

			Compiler.Instance.PushLoop(this);

			output.AppendLine(this, $"@loop_start_{this.NodeID}: NOP");

			Register? reg = forCondition.GenerateCode(output);

			this.Scope.Enter(output);

			output.AppendLine(this, $"JMPNOT {reg} @loop_end_{this.NodeID}");
			forBody.GenerateCode(output);
			loop.GenerateCode(output);

			output.AppendLine(this, $"JMP @loop_start_{this.NodeID}");
			output.AppendLine(this, $"@loop_end_{this.NodeID}: NOP");

			this.Scope.Leave(output);

			Compiler.Instance.DeallocRegister(ref reg);
			Compiler.Instance.PopLoop(this);
		}
	}

}
