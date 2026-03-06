
using System;
using System.Collections.Generic;

namespace Phantasma.Tomb.AST.Declarations
{
	public struct EnumEntry
	{
		public readonly string name;
		public readonly uint value;

		public EnumEntry(string name, uint value)
		{
			this.name = name;
			this.value = value;
		}
	}

	public class EnumDeclaration : Declaration
	{
		public Dictionary<string, uint> entryNames;

		public EnumDeclaration(string name, IEnumerable<EnumEntry> entries) : base(null, name)
		{
			entryNames = new Dictionary<string, uint>();

			foreach (var entry in entries)
			{
				if (entryNames.ContainsKey(entry.name))
				{
					throw new CompilerException($"Duplicated entry {entry.value} in enum {name}");
				}

				entryNames[entry.name] = entry.value;
			}
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
