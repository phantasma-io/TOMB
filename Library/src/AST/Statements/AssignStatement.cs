using Phantasma.Tomb.AST.Declarations;
using Phantasma.Tomb.CodeGen;
using System;

namespace Phantasma.Tomb.AST.Statements
{
	public class AssignStatement : Statement
	{
		public VarDeclaration variable;
		public Expression valueExpression;
		public Expression keyExpression; // can be null, if not null it should be an expression that resolves into a key (struct field name or array index)

		public AssignStatement() : base()
		{

		}

		public override void Visit(Action<Node> callback)
		{
			callback(this);
			variable.Visit(callback);
			valueExpression.Visit(callback);
			keyExpression?.Visit(callback);
		}

		public override bool IsNodeUsed(Node node)
		{
			return (node == this) || variable.IsNodeUsed(node) || valueExpression.IsNodeUsed(node) || (keyExpression != null && keyExpression.IsNodeUsed(node));
		}

		public override void GenerateCode(CodeGenerator output)
		{
			if (variable.Register == null)
			{
				variable.Register = Compiler.Instance.AllocRegister(output, variable, variable.Name);
			}

			var srcReg = valueExpression.GenerateCode(output);

			if (keyExpression != null)
			{
				var idxReg = keyExpression.GenerateCode(output);

				output.AppendLine(this, $"PUT {srcReg} {variable.Register} {idxReg}");

				Compiler.Instance.DeallocRegister(ref idxReg);
			}
			else
			{
				output.AppendLine(this, $"COPY {srcReg} {variable.Register}");
			}

			Compiler.Instance.DeallocRegister(ref srcReg);
		}
	}

}

