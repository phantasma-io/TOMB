using System;

namespace Phantasma.Tomb.AST
{
	public abstract class Node
	{
		public int LineNumber;
		public int Column;
		public string NodeID;

		public Node()
		{
			if (Compiler.Instance != null)
			{
				this.LineNumber = Compiler.Instance.CurrentLine;
				this.Column = Compiler.Instance.CurrentColumn;
				this.NodeID = this.GetType().Name.ToLower() + Compiler.Instance.AllocateLabel();
			}
			else
			{
				this.LineNumber = -1;
				this.Column = -1;
				this.NodeID = this.GetType().Name.ToLower();
			}
		}

		public abstract bool IsNodeUsed(Node node);

		public abstract void Visit(Action<Node> callback);
	}
}
