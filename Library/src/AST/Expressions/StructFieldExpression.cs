using Phantasma.Tomb.AST.Declarations;
using Phantasma.Tomb.CodeGen;

using System;

namespace Phantasma.Tomb.AST.Expressions
{
	public class StructFieldExpression : Expression
	{
		public VarDeclaration varDecl;
		public string fieldName;

		public override VarType ResultType { get; }

		public StructFieldExpression(Scope parentScope, VarDeclaration varDecl, string fieldName) : base(parentScope)
		{
			var structInfo = ((StructVarType)varDecl.Type);

			VarType fieldType = null;

			foreach (var field in structInfo.decl.fields)
			{
				if (field.name == fieldName)
				{
					fieldType = field.type;
				}
			}

			if (fieldType == null)
			{
				throw new CompilerException($"Struct {varDecl.Type} does not contain field: {fieldName}");
			}

			this.varDecl = varDecl;
			this.fieldName = fieldName;
			this.ResultType = fieldType;
		}

		public override void Visit(Action<Node> callback)
		{
			callback(this);
			varDecl.Visit(callback);
		}

		public override bool IsNodeUsed(Node node)
		{
			return (node == this || node == varDecl);
		}

		public override Register GenerateCode(CodeGenerator output)
		{
			var reg = Compiler.Instance.AllocRegister(output, this/*, $"{varDecl.Name}.{fieldName}"*/);

			var tempReg = Compiler.Instance.AllocRegister(output, this);

			output.AppendLine(this, $"COPY {varDecl.Register} {reg}");
			output.AppendLine(this, $"LOAD {tempReg} \"{fieldName}\"");
			output.AppendLine(this, $"GET {reg} {reg} {tempReg}");

			Compiler.Instance.DeallocRegister(ref tempReg);

			return reg;
		}
	}

}
