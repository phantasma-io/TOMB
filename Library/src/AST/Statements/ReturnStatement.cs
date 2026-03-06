using Phantasma.Tomb.AST.Declarations;
using Phantasma.Tomb.CodeGen;
using System;
using System.Linq.Expressions;

namespace Phantasma.Tomb.AST.Statements
{
	public class ReturnStatement : Statement
	{
		public Expression? expression;

		public MethodDeclaration method;

		public ReturnStatement(MethodDeclaration method, Expression? expression) : base()
		{
			this.expression = expression;
			this.method = method;
		}

		public override void Visit(Action<Node> callback)
		{
			callback(this);
			expression?.Visit(callback);
		}

		public override bool IsNodeUsed(Node node)
		{
			return (node == this) || (expression != null && expression.IsNodeUsed(node));
		}

		public override void GenerateCode(CodeGenerator output)
		{
			var returnType = this.method.@interface.ReturnType;
			var methodParentScope = this.method.ParentScope ?? throw new CompilerException($"method scope not initialized for return in {method.Name}");

			var simpleReturn = methodParentScope.Module is Script;
			var isMulti = this.method.@interface.IsMulti;

			if (expression != null)
			{
				if (returnType.Kind == VarKind.None)
				{
					throw new System.Exception($"unexpect return expression for void method: {method.Name}");
				}

				this.expression = Expression.AutoCast(expression, returnType);

				Register? reg = this.expression.GenerateCode(output);
				output.AppendLine(this, $"PUSH {reg}");
				Compiler.Instance.DeallocRegister(ref reg);
			}
			else
			if (returnType.Kind != VarKind.None && !isMulti)
			{
				throw new System.Exception($"expected return expression for non-void method: {method.Name}");
			}

			if (isMulti && expression != null)
			{
				return; // for multi methods a return with expression is basically just a push, nothing more..
			}

			if (simpleReturn)
			{
				output.AppendLine(this, "RET");
			}
			else
			{
				output.AppendLine(this, "JMP @" + this.method.GetExitLabel());
			}
		}
	}

}
