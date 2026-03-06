using Phantasma.Tomb.AST.Expressions;
using Phantasma.Tomb.CodeGen;

namespace Phantasma.Tomb.AST
{
	public abstract class Expression : Node
	{
		public abstract VarType ResultType { get; }
		public Scope ParentScope { get; }

		public Expression(Scope parentScope) : base()
		{
			this.ParentScope = parentScope;
		}

		public virtual T AsLiteral<T>()
		{
			throw new CompilerException(this, $"{this.GetType()} can't be converted to {typeof(T).Name} literal");
		}

		public abstract Register GenerateCode(CodeGenerator output);

		public static Expression AutoCast(Expression expr, VarType expectedType)
		{
			if (expr.ResultType == expectedType || expectedType.Kind == VarKind.Any)
			{
				return expr;
			}

			switch (expr.ResultType.Kind)
			{
				case VarKind.Decimal:
					switch (expectedType.Kind)
					{
						case VarKind.Decimal:
						case VarKind.Number:
							return new CastExpression(expr.ParentScope, expectedType, expr);
					}
					break;

				case VarKind.Any:
					return new CastExpression(expr.ParentScope, expectedType, expr);
			}

			throw new CompilerException($"expected {expectedType} expression, got {expr.ResultType} instead");
		}
	}

}
