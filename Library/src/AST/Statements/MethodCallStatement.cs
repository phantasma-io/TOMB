using Phantasma.Tomb.AST.Expressions;
using Phantasma.Tomb.CodeGen;
using System;

namespace Phantasma.Tomb.AST.Statements
{
	public class MethodCallStatement : Statement
	{
		public MethodCallExpression expression;

		public MethodCallStatement() : base()
		{

		}

		public override void Visit(Action<Node> callback)
		{
			callback(this);
			expression.Visit(callback);
		}

		public override bool IsNodeUsed(Node node)
		{
			return (node == this) || expression.IsNodeUsed(node);
		}

		public override void GenerateCode(CodeGenerator output)
		{
			var reg = expression.GenerateCode(output);
			Compiler.Instance.DeallocRegister(ref reg);
		}
	}

}

