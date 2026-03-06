using Phantasma.Tomb.AST.Declarations;
using Phantasma.Tomb.CodeGen;
using Phantasma.Tomb.Validation;

using System;
using System.Collections.Generic;
using System.Text;

namespace Phantasma.Tomb.AST.Expressions
{
	public class MethodCallExpression : Expression
	{
		public MethodInterface method;
		public List<Expression> arguments = new List<Expression>();

		public List<VarType> generics = new List<VarType>();

		public override VarType ResultType => method.ReturnType;

		public MethodCallExpression(Scope parentScope) : base(parentScope)
		{

		}

		public override void Visit(Action<Node> callback)
		{
			callback(this);

			foreach (var arg in arguments)
			{
				arg.Visit(callback);
			}
		}

		public override bool IsNodeUsed(Node node)
		{
			if (node == this)
			{
				return true;
			}

			foreach (var arg in arguments)
			{
				if (arg.IsNodeUsed(node))
				{
					return true;
				}
			}

			return false;
		}

		public void PatchGenerics()
		{
			int requiredGenerics = 0;

			this.method = this.method.Clone(this.method.Library);

			// auto patch storage methods
			if (this.generics.Count == 0)
			{
				var genericLib = this.method.Library as GenericLibraryDeclaration;
				if (genericLib != null)
				{
					foreach (var type in genericLib.Generics)
					{
						this.generics.Add(type);
					}
				}
			}

			if (ResultType.Kind == VarKind.Generic)
			{
				var generic = (GenericVarType)ResultType;

				if (generic.index < 0)
				{
					throw new CompilerException($"weird generic index for return type of method {this.method.Name}, compiler bug?");
				}

				if (generic.index >= this.generics.Count)
				{
					throw new CompilerException($"missing generic declaration with index {generic.index} when calling method {this.method.Name}");
				}

				requiredGenerics = Math.Max(requiredGenerics, generic.index + 1);

				this.method.ReturnType = this.generics[generic.index];
			}

			for (int paramIndex = 0; paramIndex < this.method.Parameters.Length; paramIndex++)
			{
				var parameter = this.method.Parameters[paramIndex];
				if (parameter.Type.Kind == VarKind.Generic)
				{
					var generic = (GenericVarType)parameter.Type;

					if (generic.index < 0)
					{
						throw new CompilerException($"weird generic index for parameter {parameter.Name} of method {this.method.Name}, compiler bug?");
					}

					if (generic.index >= this.generics.Count)
					{
						throw new CompilerException($"missing generic declaration with index {generic.index} when calling method {this.method.Name}");
					}

					requiredGenerics = Math.Max(requiredGenerics, generic.index + 1);

					this.method.Parameters[paramIndex] = new MethodParameter(parameter.Name, this.generics[generic.index]);
				}
			}


			if (requiredGenerics > generics.Count)
			{
				throw new CompilerException($"call to method {this.method.Name} expected {requiredGenerics} generics, got {generics.Count} instead");
			}
		}

		public override Register GenerateCode(CodeGenerator output)
		{
			// Validate native-contract availability before emitting call opcodes so the
			// failure mode is explicit at compile time instead of surfacing only at runtime.
			NativeMethodAvailability.ValidateOrReport(this, method, Compiler.NativeCheckMode);

			Register reg;

			if (this.method.PreCallback != null)
			{
				reg = this.method.PreCallback(output, ParentScope, this);
			}
			else
			{
				reg = Compiler.Instance.AllocRegister(output, this, this.NodeID);
			}

			bool isDynamicContractGateway =
				(method.Library.Name == "Call" && method.Name == "contract") ||
				(method.Library.Name == "Contract" && method.Name == "call");

			bool isCallLibrary = method.Library.Name == "Call";
			bool usesDynamicTargetLiteral = isCallLibrary || isDynamicContractGateway;

			string customAlias = null;

			if (method.Implementation != MethodImplementationType.Custom)
			{
				for (int i = arguments.Count - 1; i >= 0; i--)
				{
					var arg = arguments[i];

					Register argReg;

					if (usesDynamicTargetLiteral)
					{
						if (i == 0)
						{
							customAlias = arg.AsLiteral<string>();
							argReg = null;
						}
						else
						{
							argReg = arg.GenerateCode(output);
						}
					}
					else
					{
						var parameter = this.method.Parameters[i];
						if (parameter.Callback != null)
						{
							argReg = parameter.Callback(output, ParentScope, arg);
						}
						else
						{
							argReg = arg.GenerateCode(output);
						}
					}

					if (argReg != null)
					{
						output.AppendLine(arg, $"PUSH {argReg}");
						Compiler.Instance.DeallocRegister(ref argReg);
					}
				}
			}

			switch (this.method.Implementation)
			{
				case MethodImplementationType.LocalCall:
					{
						if (this.method.IsBuiltin)
						{
							output.AppendLine(this, $"CALL @entry_{this.method.Alias}");
							output.IncBuiltinReference(this.method.Alias);
						}
						else
						{
							output.AppendLine(this, $"CALL @entry_{this.method.Name}");
						}
						break;
					}

				case MethodImplementationType.ExtCall:
					{
						var extCall = customAlias != null ? customAlias : $"\"{this.method.Alias}\"";
						output.AppendLine(this, $"LOAD {reg} {extCall}");
						output.AppendLine(this, $"EXTCALL {reg}");
						break;
					}

				case MethodImplementationType.ContractCall:
					{
						if (customAlias == null)
						{
							output.AppendLine(this, $"LOAD {reg} \"{this.method.Alias}\"");
							output.AppendLine(this, $"PUSH {reg}");
						}

						var contractCall = customAlias != null ? customAlias : $"\"{this.method.Contract}\"";
						output.AppendLine(this, $"LOAD {reg} {contractCall}");
						output.AppendLine(this, $"CTX {reg} {reg}");
						output.AppendLine(this, $"SWITCH {reg}");
						break;
					}

				case MethodImplementationType.Custom:

					if (this.method.PreCallback == null && this.method.PostCallback == null)
					{
						output.AppendLine(this, $"LOAD r0 \"{this.method.Library.Name}.{this.method.Name} not implemented\"");
						output.AppendLine(this, $"THROW r0");
					}

					break;
			}

			if (this.method.ReturnType.Kind != VarKind.None && this.method.Implementation != MethodImplementationType.Custom)
			{
				output.AppendLine(this, $"POP {reg}");
			}

			if (this.method.PostCallback != null)
			{
				reg = this.method.PostCallback(output, ParentScope, this, reg);
			}

			return reg;
		}

		public override string ToString()
		{
			var sb = new StringBuilder();

			sb.Append(method.Name);
			sb.Append('(');

			int count = 0;
			foreach (var arg in arguments)
			{
				if (count > 0) sb.Append(", ");
				sb.Append(arg.ToString());
				count++;
			}

			sb.Append(')');
			return sb.ToString();
		}
	}

}
