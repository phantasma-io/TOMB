using Phantasma.Tomb.CodeGen;
using System;
using System.Collections.Generic;

namespace Phantasma.Tomb.AST
{
	public abstract class Statement : Node
	{
		public abstract void GenerateCode(CodeGenerator output);

	}

	public class StatementBlock : Node
	{
		public readonly List<Statement> Commands = new List<Statement>();

		public Scope ParentScope { get; }

		public StatementBlock(Scope scope) : base()
		{
			this.ParentScope = scope;
		}

		public void GenerateCode(CodeGenerator output)
		{
			foreach (var cmd in Commands)
			{
				cmd.GenerateCode(output);
			}
		}

		public override void Visit(Action<Node> callback)
		{
			callback(this);
			foreach (var cmd in Commands)
			{
				cmd.Visit(callback);
			}
		}

		public override bool IsNodeUsed(Node node)
		{
			if (node == this)
			{
				return true;
			}

			foreach (var cmd in Commands)
			{
				if (cmd.IsNodeUsed(node))
				{
					return true;
				}
			}

			return false;
		}
	}

	public abstract class LoopStatement : Statement
	{
	}

}

