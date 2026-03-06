using Phantasma.Tomb.AST.Declarations;
using Phantasma.Tomb.CodeGen;

using System;

namespace Phantasma.Tomb.AST.Expressions
{
	public class VarExpression : Expression
	{
		public VarDeclaration decl;

		public VarExpression(Scope parentScope, VarDeclaration declaration) : base(parentScope)
		{
			this.decl = declaration;
		}

		public override string ToString()
		{
			return decl.Name;
		}

		public override Register GenerateCode(CodeGenerator output)
		{
			if (decl.Register == null)
			{
				throw new CompilerException(this, $"var not initialized:" + decl.Name);
			}

			var reg = Compiler.Instance.AllocRegister(output, this);
			output.AppendLine(this, $"COPY {decl.Register} {reg}");
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
