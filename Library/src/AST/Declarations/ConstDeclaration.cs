using Phantasma.Tomb.CodeGen;

using System;

namespace Phantasma.Tomb.AST.Declarations
{
	public class ConstDeclaration : Declaration
	{
		public VarType Type;
		public string Value;

		public ConstDeclaration(Scope? parentScope, string name, VarType kind, string value) : base(parentScope, name)
		{
			this.Type = kind;
			this.Value = value;
		}

		public void GenerateCode(CodeGenerator output)
		{
			// DO NOTHING
		}

		public override string ToString()
		{
			return $"const {Name}:{Type}";
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
