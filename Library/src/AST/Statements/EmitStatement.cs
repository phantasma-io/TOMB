using Phantasma.Tomb.AST.Declarations;
using Phantasma.Tomb.CodeGen;
using System;

namespace Phantasma.Tomb.AST.Statements
{
	public class EmitStatement : Statement
	{
		public EventDeclaration eventDecl;

		public Expression valueExpr;
		public Expression addressExpr;

		public EmitStatement(EventDeclaration evt, Expression addrExpr, Expression valueExpr) : base()
		{
			this.addressExpr = addrExpr;
			this.valueExpr = valueExpr;
			this.eventDecl = evt;
		}

		public override void Visit(Action<Node> callback)
		{
			callback(this);
			eventDecl.Visit(callback);
			addressExpr.Visit(callback);
			valueExpr.Visit(callback);
		}

		public override bool IsNodeUsed(Node node)
		{
			return (node == this) || eventDecl.IsNodeUsed(node) || addressExpr.IsNodeUsed(node) || valueExpr.IsNodeUsed(node);
		}

		public override void GenerateCode(CodeGenerator output)
		{
			var reg = valueExpr.GenerateCode(output);
			output.AppendLine(this, $"PUSH {reg}");
			Compiler.Instance.DeallocRegister(ref reg);

			reg = addressExpr.GenerateCode(output);
			output.AppendLine(this, $"PUSH {reg}");

			output.AppendLine(this, $"LOAD {reg} {eventDecl.value}");
			output.AppendLine(this, $"PUSH {reg}");

			output.AppendLine(this, $"LOAD {reg} \"Runtime.Notify\"");
			output.AppendLine(this, $"EXTCALL {reg}");

			Compiler.Instance.DeallocRegister(ref reg);
		}
	}

}

