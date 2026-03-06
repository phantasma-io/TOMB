using Phantasma.Tomb.CodeGen;

using System;

namespace Phantasma.Tomb.AST.Declarations
{
	public class VarDeclaration : Declaration
	{
		public VarType Type;
		public VarStorage Storage;
		public Register Register = null;

		public VarDeclaration(Scope parentScope, string name, VarType type, VarStorage storage) : base(parentScope, name)
		{
			this.Type = type;
			this.Storage = storage;
		}

		public override string ToString()
		{
			return $"var {Name}:{Type}";
		}

		public override void Visit(Action<Node> callback)
		{
			callback(this);
		}

		public override bool IsNodeUsed(Node node)
		{
			return node == this;
		}
	}
}
