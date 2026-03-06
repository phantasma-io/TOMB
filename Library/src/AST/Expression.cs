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
			if (IsCompatibleType(expr.ResultType, expectedType))
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

		public static bool IsCompatibleType(VarType actualType, VarType expectedType)
		{
			if (actualType == expectedType || expectedType.Kind == VarKind.Any)
			{
				return true;
			}

			if (actualType.Kind != expectedType.Kind)
			{
				return false;
			}

			// Built-in library signatures intentionally use unresolved placeholders
			// (for example Module/Method without a concrete declaration node).
			// Those placeholders must accept any concrete value of the same kind.
			switch (expectedType)
			{
				case StructVarType structType when string.IsNullOrEmpty(structType.name):
					return true;

				case EnumVarType enumType when string.IsNullOrEmpty(enumType.name):
					return true;

				case MethodVarType methodType when methodType.method == null:
					return true;

				case ModuleVarType moduleType when moduleType.module == null:
					return true;
			}

			return false;
		}
	}

}
