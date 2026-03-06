using Phantasma.Tomb.AST.Declarations;
using Phantasma.Tomb.CodeGen;
using System;

namespace Phantasma.Tomb.AST.Statements
{
	public class AssignStatement : Statement
	{
		public VarDeclaration? variable;
		public Expression? valueExpression;
		public Expression? keyExpression; // can be null, if not null it should be an expression that resolves into a key (struct field name or array index)

		public AssignStatement() : base()
		{

		}

		private VarDeclaration RequireVariable()
		{
			if (variable != null)
			{
				return variable;
			}

			throw new CompilerException("assignment target not initialized");
		}

		private Expression RequireValueExpression()
		{
			if (valueExpression != null)
			{
				return valueExpression;
			}

			throw new CompilerException("assignment value not initialized");
		}

		public override void Visit(Action<Node> callback)
		{
			callback(this);
			RequireVariable().Visit(callback);
			RequireValueExpression().Visit(callback);
			keyExpression?.Visit(callback);
		}

		public override bool IsNodeUsed(Node node)
		{
			var targetVariable = RequireVariable();
			var targetValue = RequireValueExpression();
			return (node == this) || targetVariable.IsNodeUsed(node) || targetValue.IsNodeUsed(node) || (keyExpression != null && keyExpression.IsNodeUsed(node));
		}

		public override void GenerateCode(CodeGenerator output)
		{
			var targetVariable = RequireVariable();
			var targetValue = RequireValueExpression();

			if (targetVariable.Register == null)
			{
				targetVariable.Register = Compiler.Instance.AllocRegister(output, targetVariable, targetVariable.Name);
			}

			Register? srcReg = targetValue.GenerateCode(output);

			if (keyExpression != null)
			{
				Register? idxReg = keyExpression.GenerateCode(output);

				output.AppendLine(this, $"PUT {srcReg} {targetVariable.Register} {idxReg}");

				Compiler.Instance.DeallocRegister(ref idxReg);
			}
			else
			{
				output.AppendLine(this, $"COPY {srcReg} {targetVariable.Register}");
			}

			Compiler.Instance.DeallocRegister(ref srcReg);
		}
	}

}
