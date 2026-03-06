using Phantasma.Tomb.AST.Declarations;
using Phantasma.Tomb.CodeGen;
using System;

namespace Phantasma.Tomb.AST.Statements
{
	public class ForStatement : LoopStatement
	{
		public VarDeclaration loopVar;
		public Expression condition;
		public Statement initStatement;
		public Statement loopStatement;

		public StatementBlock body;
		public Scope Scope { get; }

		//private int label;

		public ForStatement(Scope parentScope) : base()
		{
			this.Scope = new Scope(parentScope, this.NodeID);
			//this.label = Parser.Instance.AllocateLabel();
		}

		public override void Visit(Action<Node> callback)
		{
			callback(this);

			loopVar.Visit(callback);
			initStatement.Visit(callback);
			condition.Visit(callback);
			loopStatement.Visit(callback);
			body.Visit(callback);
		}

		public override bool IsNodeUsed(Node node)
		{
			return (node == this) || condition.IsNodeUsed(node) || body.IsNodeUsed(node) || loopVar.IsNodeUsed(node) || loopStatement.IsNodeUsed(node) || initStatement.IsNodeUsed(node);
		}

		public override void GenerateCode(CodeGenerator output)
		{
			initStatement.GenerateCode(output);

			Compiler.Instance.PushLoop(this);

			output.AppendLine(this, $"@loop_start_{this.NodeID}: NOP");

			var reg = condition.GenerateCode(output);

			this.Scope.Enter(output);

			output.AppendLine(this, $"JMPNOT {reg} @loop_end_{this.NodeID}");
			body.GenerateCode(output);
			loopStatement.GenerateCode(output);

			output.AppendLine(this, $"JMP @loop_start_{this.NodeID}");
			output.AppendLine(this, $"@loop_end_{this.NodeID}: NOP");

			this.Scope.Leave(output);

			Compiler.Instance.DeallocRegister(ref reg);
			Compiler.Instance.PopLoop(this);
		}
	}

}

