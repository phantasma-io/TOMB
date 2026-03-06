using Phantasma.Tomb.AST.Declarations;
using Phantasma.Tomb.CodeGen;

using System;

namespace Phantasma.Tomb.AST.Expressions
{
	public class ConstExpression : Expression
	{
		public ConstDeclaration decl;

		public ConstExpression(Scope parentScope, ConstDeclaration declaration) : base(parentScope)
		{
			this.decl = declaration;
		}

		public override string ToString()
		{
			return decl.Name;
			//return decl.ToString();
		}

		public override T AsLiteral<T>()
		{
			if (decl.Type.Kind == VarKind.String && typeof(T) == typeof(string))
			{
				return (T)(object)decl.Value;
			}

			return base.AsLiteral<T>();
		}

		public override Register GenerateCode(CodeGenerator output)
		{
			var reg = Compiler.Instance.AllocRegister(output, this, decl.Name);
			output.AppendLine(this, $"LOAD {reg} {decl.Value}");
			this.CallNecessaryConstructors(output, decl.Type, reg);
			return reg;
		}

		public override void Visit(Action<Node> callback)
		{
			callback(this);
			decl.Visit(callback);
		}

		public override bool IsNodeUsed(Node node)
		{
			return (node == this) || node == decl;
		}

		public override VarType ResultType => decl.Type;
	}

}
