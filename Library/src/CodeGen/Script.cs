using System;
using Phantasma.Tomb.AST;
using Phantasma.Tomb.AST.Statements;
using Phantasma.Tomb.AST.Declarations;
using Phantasma.Core.Domain.Contract;

namespace Phantasma.Tomb.CodeGen
{
	public class Script : Module
	{
		public StatementBlock? main;

		public MethodParameter[]? Parameters { get; internal set; }
		public VarType? ReturnType;

		public Script(string name, ModuleKind kind) : base(name, kind)
		{

		}

		private StatementBlock RequireMain()
		{
			if (main != null)
			{
				return main;
			}

			throw new CompilerException("script body not initialized");
		}

		private MethodParameter[] RequireParameters()
		{
			if (Parameters != null)
			{
				return Parameters;
			}

			throw new CompilerException("script parameters not initialized");
		}

		private VarType RequireReturnType()
		{
			if (ReturnType != null)
			{
				return ReturnType;
			}

			throw new CompilerException("script return type not initialized");
		}

		public override MethodDeclaration? FindMethod(string name)
		{
			return null;
		}

		public override bool IsNodeUsed(Node node)
		{
			if (node == this)
			{
				return true;
			}

			foreach (var lib in Libraries.Values)
			{
				if (lib.IsNodeUsed(node))
				{
					return true;
				}
			}

			return RequireMain().IsNodeUsed(node);
		}

		public override void Visit(Action<Node> callback)
		{
			foreach (var lib in Libraries.Values)
			{
				lib.Visit(callback);
			}

			callback(this);
			RequireMain().Visit(callback);
		}


		public override ContractInterface GenerateCode(CodeGenerator output)
		{
			this.Scope.Enter(output);

			var scriptMain = RequireMain();
			var parameters = RequireParameters();
			var returnType = RequireReturnType();

			scriptMain.ParentScope.Enter(output);

			foreach (var parameter in parameters)
			{
				var reg = Compiler.Instance.AllocRegister(output, this, parameter.Name);
				output.AppendLine(this, $"POP {reg}");

				this.CallNecessaryConstructors(output, parameter.Type, reg);

				if (!scriptMain.ParentScope.Variables.ContainsKey(parameter.Name))
				{
					throw new CompilerException("script parameter not initialized: " + parameter.Name);
				}

				var varDecl = scriptMain.ParentScope.Variables[parameter.Name];
				varDecl.Register = reg;
			}

			scriptMain.GenerateCode(output);
			scriptMain.ParentScope.Leave(output);

			if (returnType.Kind == VarKind.None)
			{
				output.AppendLine(this, "RET");
			}
			else
			{
				bool hasReturn = false;
				scriptMain.Visit((node) =>
				{
					if (node is ReturnStatement)
					{
						hasReturn = true;
					}
				});

				if (!hasReturn)
				{
					throw new Exception("Script is missing return statement");
				}
			}

			this.Scope.Leave(output);

			return ContractInterface.Empty;
		}

	}
}
