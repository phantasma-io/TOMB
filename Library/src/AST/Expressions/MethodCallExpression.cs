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
		public MethodInterface? method;
		public List<Expression> arguments = new List<Expression>();

		public List<VarType> generics = new List<VarType>();

		public override VarType ResultType => RequireMethod().ReturnType;

		public MethodCallExpression(Scope parentScope) : base(parentScope)
		{

		}

		private MethodInterface RequireMethod()
		{
			if (method != null)
			{
				return method;
			}

			throw new CompilerException("method call target not initialized");
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

			var currentMethod = RequireMethod();
			var targetMethod = currentMethod.Clone(currentMethod.Library);
			this.method = targetMethod;

			// auto patch storage methods
			if (this.generics.Count == 0)
			{
				var genericLib = targetMethod.Library as GenericLibraryDeclaration;
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
					throw new CompilerException($"weird generic index for return type of method {targetMethod.Name}, compiler bug?");
				}

				if (generic.index >= this.generics.Count)
				{
					throw new CompilerException($"missing generic declaration with index {generic.index} when calling method {targetMethod.Name}");
				}

				requiredGenerics = Math.Max(requiredGenerics, generic.index + 1);

				targetMethod.ReturnType = this.generics[generic.index];
			}

			for (int paramIndex = 0; paramIndex < targetMethod.Parameters.Length; paramIndex++)
			{
				var parameter = targetMethod.Parameters[paramIndex];
				if (parameter.Type.Kind == VarKind.Generic)
				{
					var generic = (GenericVarType)parameter.Type;

					if (generic.index < 0)
					{
						throw new CompilerException($"weird generic index for parameter {parameter.Name} of method {targetMethod.Name}, compiler bug?");
					}

					if (generic.index >= this.generics.Count)
					{
						throw new CompilerException($"missing generic declaration with index {generic.index} when calling method {targetMethod.Name}");
					}

					requiredGenerics = Math.Max(requiredGenerics, generic.index + 1);

					targetMethod.Parameters[paramIndex] = new MethodParameter(parameter.Name, this.generics[generic.index]);
				}
			}


			if (requiredGenerics > generics.Count)
			{
				throw new CompilerException($"call to method {targetMethod.Name} expected {requiredGenerics} generics, got {generics.Count} instead");
			}
		}

		public override Register GenerateCode(CodeGenerator output)
		{
			var targetMethod = RequireMethod();

			// Validate native-contract availability before emitting call opcodes so the
			// failure mode is explicit at compile time instead of surfacing only at runtime.
			NativeMethodAvailability.ValidateOrReport(this, targetMethod, Compiler.NativeCheckMode);

			Register? reg;

			if (targetMethod.PreCallback != null)
			{
				reg = targetMethod.PreCallback(output, ParentScope, this);
			}
			else
			{
				reg = Compiler.Instance.AllocRegister(output, this, this.NodeID);
			}

			bool isDynamicContractGateway =
				(targetMethod.Library.Name == "Call" && targetMethod.Name == "contract") ||
				(targetMethod.Library.Name == "Contract" && targetMethod.Name == "call");

			bool isCallLibrary = targetMethod.Library.Name == "Call";
			bool usesDynamicTargetLiteral = isCallLibrary || isDynamicContractGateway;

			string? customAlias = null;

			if (targetMethod.Implementation != MethodImplementationType.Custom)
			{
				for (int i = arguments.Count - 1; i >= 0; i--)
				{
					var arg = arguments[i];

					Register? argReg;

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
						var parameter = targetMethod.Parameters[i];
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

			switch (targetMethod.Implementation)
			{
				case MethodImplementationType.LocalCall:
					{
						if (targetMethod.IsBuiltin)
						{
							output.AppendLine(this, $"CALL @entry_{targetMethod.Alias}");
							output.IncBuiltinReference(targetMethod.Alias);
						}
						else
						{
							output.AppendLine(this, $"CALL @entry_{targetMethod.Name}");
						}
						break;
					}

				case MethodImplementationType.ExtCall:
					{
						var extCall = customAlias != null ? customAlias : $"\"{targetMethod.Alias}\"";
						output.AppendLine(this, $"LOAD {reg} {extCall}");
						output.AppendLine(this, $"EXTCALL {reg}");
						break;
					}

				case MethodImplementationType.ContractCall:
					{
						if (customAlias == null)
						{
							output.AppendLine(this, $"LOAD {reg} \"{targetMethod.Alias}\"");
							output.AppendLine(this, $"PUSH {reg}");
						}

						var contractCall = customAlias != null ? customAlias : $"\"{targetMethod.Contract}\"";
						output.AppendLine(this, $"LOAD {reg} {contractCall}");
						output.AppendLine(this, $"CTX {reg} {reg}");
						output.AppendLine(this, $"SWITCH {reg}");
						break;
					}

				case MethodImplementationType.Custom:

					if (targetMethod.PreCallback == null && targetMethod.PostCallback == null)
					{
						output.AppendLine(this, $"LOAD r0 \"{targetMethod.Library.Name}.{targetMethod.Name} not implemented\"");
						output.AppendLine(this, $"THROW r0");
					}

					break;
			}

			if (targetMethod.ReturnType.Kind != VarKind.None && targetMethod.Implementation != MethodImplementationType.Custom)
			{
				output.AppendLine(this, $"POP {reg}");
			}

			if (targetMethod.PostCallback != null)
			{
				reg = targetMethod.PostCallback(output, ParentScope, this, reg);
			}

			return reg ?? throw new CompilerException("method call register allocation failed");
		}

		public override string ToString()
		{
			var sb = new StringBuilder();

			sb.Append(RequireMethod().Name);
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
