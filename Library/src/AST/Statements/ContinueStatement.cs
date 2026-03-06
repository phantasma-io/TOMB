using Phantasma.Tomb.CodeGen;
using System;

namespace Phantasma.Tomb.AST.Statements
{
	public class ContinueStatement : Statement
	{
		public readonly Scope scope;

		public ContinueStatement(Scope scope) : base()
		{
			this.scope = scope;
		}

		public override bool IsNodeUsed(Node node)
		{
			return (node == this);
		}

		public override void Visit(Action<Node> callback)
		{
			callback(this);
		}

		public override void GenerateCode(CodeGenerator output)
		{
			if (Compiler.Instance.CurrentLoop == null)
			{
				if (this.scope.Method != null && this.scope.Method.@interface.Kind == MethodKind.Trigger)
				{
					throw new CompilerException("trigger continuenot implemented");
				}

				throw new CompilerException("not inside a loop");
			}

			output.AppendLine(this, $"JMP @loop_start_{Compiler.Instance.CurrentLoop.NodeID}");
		}
	}

}

