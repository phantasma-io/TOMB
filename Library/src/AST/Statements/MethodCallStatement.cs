using Phantasma.Tomb.AST.Expressions;
using Phantasma.Tomb.CodeGen;
using System;

namespace Phantasma.Tomb.AST.Statements
{
	public class MethodCallStatement : Statement
	{
		public MethodCallExpression? expression;

		public MethodCallStatement() : base()
		{

		}

		private MethodCallExpression RequireExpression()
		{
			if (expression != null)
			{
				return expression;
			}

			throw new CompilerException("method call expression not initialized");
		}

		public override void Visit(Action<Node> callback)
		{
			callback(this);
			RequireExpression().Visit(callback);
		}

		public override bool IsNodeUsed(Node node)
		{
			return (node == this) || RequireExpression().IsNodeUsed(node);
		}

		public override void GenerateCode(CodeGenerator output)
		{
			Register? reg = RequireExpression().GenerateCode(output);
			Compiler.Instance.DeallocRegister(ref reg);
		}
	}

}
