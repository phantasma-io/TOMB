using Phantasma.Core.Domain;
using PhantasmaPhoenix.VM;
using Phantasma.Tomb.CodeGen;

using System;

namespace Phantasma.Tomb.AST.Expressions
{
	public class BinaryExpression : Expression
	{
		private OperatorKind op;
		private Expression left;
		private Expression right;

		public override VarType ResultType => op.IsLogicalOperator() ? VarType.Find(VarKind.Bool) : left.ResultType;

		public BinaryExpression(Scope parentScope, OperatorKind op, Expression leftSide, Expression rightSide) : base(parentScope)
		{
			if (op == OperatorKind.Unknown)
			{
				throw new CompilerException("implementation failure");
			}

			this.op = op;
			this.left = leftSide;
			this.right = rightSide;
		}

		public override T AsLiteral<T>()
		{
			if (op == OperatorKind.Addition)
			{
				if (typeof(T) == typeof(string) && ResultType.Kind == VarKind.String)
					return (T)(object)(left.AsLiteral<string>() + right.AsLiteral<string>());

				if (typeof(T) == typeof(int) && ResultType.Kind == VarKind.Number)
					return (T)(object)(left.AsLiteral<int>() + right.AsLiteral<int>());
			}


			return base.AsLiteral<T>();
		}

		public override bool IsNodeUsed(Node node)
		{
			return (node == this) || left.IsNodeUsed(node) || right.IsNodeUsed(node);
		}

		public override void Visit(Action<Node> callback)
		{
			callback(this);
			left.Visit(callback);
			right.Visit(callback);
		}

		public override Register GenerateCode(CodeGenerator output)
		{
			if (this.op == OperatorKind.Addition && left.ResultType.Kind == VarKind.String && right.ResultType.Kind != VarKind.String)
			{
				this.right = new CastExpression(this.ParentScope, VarType.Find(VarKind.String), right);
			}

			var regLeft = left.GenerateCode(output);
			var regRight = right.GenerateCode(output);
			var regResult = Compiler.Instance.AllocRegister(output, this);

			Opcode opcode;
			switch (this.op)
			{
				case OperatorKind.Addition: opcode = Opcode.ADD; break;
				case OperatorKind.Subtraction: opcode = Opcode.SUB; break;
				case OperatorKind.Multiplication: opcode = Opcode.MUL; break;
				case OperatorKind.Division: opcode = Opcode.DIV; break;
				case OperatorKind.Modulus: opcode = Opcode.MOD; break;
				case OperatorKind.Power: opcode = Opcode.POW; break;

				case OperatorKind.Equal: opcode = Opcode.EQUAL; break;
				case OperatorKind.Less: opcode = Opcode.LT; break;
				case OperatorKind.LessOrEqual: opcode = Opcode.LTE; break;
				case OperatorKind.Greater: opcode = Opcode.GT; break;
				case OperatorKind.GreaterOrEqual: opcode = Opcode.GTE; break;

				case OperatorKind.ShiftRight: opcode = Opcode.SHR; break;
				case OperatorKind.ShiftLeft: opcode = Opcode.SHL; break;

				case OperatorKind.Or: opcode = Opcode.OR; break;
				case OperatorKind.And: opcode = Opcode.AND; break;
				case OperatorKind.Xor: opcode = Opcode.XOR; break;

				default:
					throw new CompilerException("not implemented vmopcode for " + op);
			}

			output.AppendLine(this, $"{opcode} {regLeft} {regRight} {regResult}");

			Compiler.Instance.DeallocRegister(ref regRight);
			Compiler.Instance.DeallocRegister(ref regLeft);

			return regResult;
		}

		public override string ToString()
		{
			return $"{left} {op} {right}";
		}
	}

}
