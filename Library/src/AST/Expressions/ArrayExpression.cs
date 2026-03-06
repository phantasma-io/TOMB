using Phantasma.Tomb.CodeGen;
using System;
using System.Collections.Generic;

namespace Phantasma.Tomb.AST.Expressions
{
	public class ArrayExpression : Expression
	{
		public List<Expression> elements;

		public ArrayExpression(Scope parentScope) : base(parentScope)
		{
			this.elements = new List<Expression>();
		}

		public override string ToString()
		{
			return $"array[{ResultType}: {elements.Count}]";
		}

		public override Register GenerateCode(CodeGenerator output)
		{
			var reg = Compiler.Instance.AllocRegister(output, this, this.NodeID);

			output.AppendLine(this, $"CLEAR {reg}");

			var idxReg = Compiler.Instance.AllocRegister(output, this, "_array_init_idx");

			for (int i = 0; i < elements.Count; i++)
			{
				var reg2 = elements[i].GenerateCode(output);
				output.AppendLine(this, $"LOAD {idxReg} {i}");
				output.AppendLine(this, $"PUT {reg2} {reg} {idxReg}");
				Compiler.Instance.DeallocRegister(ref reg2);
			}

			Compiler.Instance.DeallocRegister(ref idxReg);
			return reg;
		}

		public override void Visit(Action<Node> callback)
		{
			callback(this);
		}

		public override bool IsNodeUsed(Node node)
		{
			return (node == this);
		}

		public override VarType ResultType
		{
			get
			{
				var elementType = elements.Count > 0 ? elements[0].ResultType : VarType.Find(VarKind.Unknown);
				return VarType.Find(VarKind.Array, elementType);
			}
		}
	}

}
