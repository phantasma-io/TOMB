
using System;
using System.Collections.Generic;
using System.Linq;

namespace Phantasma.Tomb.AST.Declarations
{
	public struct StructField
	{
		public readonly string name;
		public readonly VarType type;

		public StructField(string name, VarType type)
		{
			this.name = name;
			this.type = type;
		}

		public StructField(string name, VarKind kind) : this(name, VarType.Find(kind))
		{
		}
	}

	public class StructDeclaration : Declaration
	{
		public StructField[] fields;

		public StructDeclaration(string name, IEnumerable<StructField> fields) : base(null, name)
		{
			this.fields = fields.ToArray();
		}

		public override bool IsNodeUsed(Node node)
		{
			return (node == this);
		}

		public override void Visit(Action<Node> callback)
		{
			callback(this);
		}
	}
}
