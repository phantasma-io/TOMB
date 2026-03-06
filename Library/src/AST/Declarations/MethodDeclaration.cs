using Phantasma.Core.Domain;
using Phantasma.Core.Domain.Contract;
using Phantasma.Core.Domain.Contract.Structs;
using PhantasmaPhoenix.VM;
using Phantasma.Tomb.AST.Statements;
using Phantasma.Tomb.CodeGen;

using System;
using System.Collections.Generic;
using System.Linq;

namespace Phantasma.Tomb.AST.Declarations
{
	public class MethodDeclaration : Declaration
	{
		public readonly MethodInterface @interface;
		public readonly Scope scope;

		public StatementBlock body { get; internal set; }

		public MethodDeclaration(Scope scope, MethodInterface @interface) : base(scope.Parent, @interface.Name)
		{
			this.body = null;
			this.scope = scope;
			this.@interface = @interface;
		}

		protected override void ValidateName()
		{
			if (Name != "constructor")
			{
				base.ValidateName();
			}
		}

		public override void Visit(Action<Node> callback)
		{
			callback(this);
			body.Visit(callback);
		}

		public override bool IsNodeUsed(Node node)
		{
			return (node == this) || (body.IsNodeUsed(node));
		}

		public void GenerateCode(CodeGenerator output)
		{
			output.AppendLine(this);
			output.AppendLine(this, $"// ********* {this.Name} {this.@interface.Kind} ***********");

			if (this.Name.StartsWith("tomb_"))
			{
				var split = this.Name.Split(new char[] { '_' }, 3);
				if (split.Length != 3)
				{
					throw new CompilerException("Builtin method name not following required convention tomb_LIB_METHOD");
				}

				output.AppendLine(this, $"// #BUILTIN");
				output.AppendLine(this, $"// LIBRARY:" + split[1]);
				output.AppendLine(this, $"// METHOD:" + split[2]);
				output.AppendLine(this, $"// RETURN:" + this.@interface.ReturnType);
				output.AppendLine(this, $"// ARGS:");
				foreach (var arg in this.@interface.Parameters)
				{
					output.AppendLine(this, "// " + arg.Name + ":" + arg.Type);
				}
			}

			output.AppendLine(this, $"@{GetEntryLabel()}:");

			this.@interface.StartAsmLine = output.LineCount;

			Register tempReg1 = null;
			Register tempReg2 = null;

			bool isConstructor = this.@interface.Kind == MethodKind.Constructor;

			// protocol version check
			if (isConstructor)
			{
				var varName = "nexus_protocol_version";
				var tempP = Compiler.Instance.AllocRegister(output, this, varName);

				var warning = $"Current nexus protocol version should be {Compiler.Instance.TargetProtocolVersion} or more";

				output.AppendLine(this, $"// validate protocol version");
				output.AppendLine(this, $"LOAD r0 \"Runtime.Version\"");
				output.AppendLine(this, $"EXTCALL r0");
				output.AppendLine(this, $"POP r0");
				output.AppendLine(this, $"LOAD {tempP} {Compiler.Instance.TargetProtocolVersion}");
				output.AppendLine(this, $"LT r0 {tempP} r0");
				output.AppendLine(this, $"JMPNOT r0 @protocol_version_validated");
				output.AppendLine(this, $"LOAD r0 \"{warning}\"");
				output.AppendLine(this, $"THROW r0");
				output.AppendLine(this, $"@protocol_version_validated: NOP");
				Compiler.Instance.DeallocRegister(ref tempP);
			}

			// here we generate code that runs at the entry point of this method
			// we need to fetch the global variables from storage and allocate registers for them
			foreach (var variable in this.scope.Parent.Variables.Values)
			{
				// validate NFT implicit variables
				if (variable.Storage != VarStorage.Global)
				{
					if (variable.Storage == VarStorage.NFT && this.scope.Module.Kind != ModuleKind.NFT)
					{
						throw new CompilerException($"implicit variable '{variable.Name}' not allowed in {this.scope.Module.Kind}");
					}

					continue;
				}

				// for maps/lists/sets we clear them here. Should not be necessary, but just in case
				// this is REQUIRED right now until chain implementation of Contract.Kill is improved
				if (isConstructor && variable.Type.IsStorageBound)
				{
					var storageClassName = variable.Type.ToString().Replace("Storage_", "");

					output.AppendLine(this, $"// clearing {variable.Name} storage");
					output.AppendLine(this, $"LOAD r0 \"{variable.Name}\"");
					output.AppendLine(this, $"PUSH r0");
					output.AppendLine(this, "LOAD r0 \"" + storageClassName + ".Clear\"");
					output.AppendLine(this, $"EXTCALL r0");
				}

				if (!this.IsNodeUsed(variable))
				{
					variable.Register = null;
					continue;
				}

				// generate code for loading globals from contract storage via Data.Get extcall
				if (tempReg1 == null && !isConstructor)
				{
					tempReg1 = Compiler.Instance.AllocRegister(output, this, "dataGet");
					output.AppendLine(this, $"LOAD {tempReg1} \"Data.Get\"");

					tempReg2 = Compiler.Instance.AllocRegister(output, this, "contractName");
					output.AppendLine(this, $"LOAD {tempReg2} \"{this.scope.Module.Name}\"");
				}

				var reg = Compiler.Instance.AllocRegister(output, variable, variable.Name);
				variable.Register = reg;

				if (isConstructor)
				{
					// don't do anything more, since in a constructor we don't need to read the vars from storage as they dont exist yet
					continue;
				}

				//var fieldKey = SmartContract.GetKeyForField(this.scope.Root.Name, variable.Name, false);

				VMType vmType = MethodInterface.ConvertType(variable.Type);

				output.AppendLine(this, $"// reading global: {variable.Name}");
				output.AppendLine(this, $"LOAD r0 {(int)vmType}");
				output.AppendLine(this, $"PUSH r0");
				output.AppendLine(this, $"LOAD r0 \"{variable.Name}\"");
				output.AppendLine(this, $"PUSH r0");
				output.AppendLine(this, $"PUSH {tempReg2}");
				output.AppendLine(this, $"EXTCALL {tempReg1}");
				output.AppendLine(this, $"POP {reg}");
				variable.CallNecessaryConstructors(output, variable.Type, reg);
			}

			var implicits = new List<VarDeclaration>();

			if (this.scope.Module.Kind == ModuleKind.NFT)
			{
				var idReg = Compiler.Instance.AllocRegister(output, this);
				output.AppendLine(this, $"POP {idReg} // get nft tokenID from stack");
				implicits = this.scope.Parent.Variables.Values.Where(x => x.Storage == VarStorage.NFT && this.IsNodeUsed(x) && !x.Name.Equals("_tokenID", StringComparison.OrdinalIgnoreCase)).ToList();

				VarDeclaration tokenIDVar = this.scope.Parent.Variables.Values.Where(x => x.Storage == VarStorage.NFT && this.IsNodeUsed(x) && x.Name.Equals("_tokenID", StringComparison.OrdinalIgnoreCase)).FirstOrDefault();

				if (implicits.Count > 0)
				{
					var fieldStr = string.Join(',', implicits.Select(x => x.Name.Substring(1)));
					output.AppendLine(this, $"// reading nft data");
					output.AppendLine(this, $"LOAD r0 \"" + fieldStr + "\"");
					output.AppendLine(this, $"PUSH r0 // fields");
					output.AppendLine(this, $"PUSH {idReg} // tokenID");
					output.AppendLine(this, $"LOAD r0 \"" + this.scope.Module.Parent.Name + "\"");
					output.AppendLine(this, $"PUSH r0 // symbol");
					output.AppendLine(this, $"LOAD r0 \"Runtime.ReadToken\"");
					output.AppendLine(this, $"EXTCALL r0");

					var dataReg = Compiler.Instance.AllocRegister(output, this);
					output.AppendLine(this, $"POP {dataReg}");
					output.AppendLine(this, $"UNPACK {dataReg} {dataReg}");

					foreach (var variable in implicits)
					{
						var fieldName = variable.Name.Substring(1);

						var reg = Compiler.Instance.AllocRegister(output, variable, variable.Name);
						variable.Register = reg;

						output.AppendLine(this, $"LOAD r0 \"{fieldName}\"");
						output.AppendLine(this, $"GET {dataReg} {reg} r0");

						if (variable.Type.Kind == VarKind.Struct)
						{
							output.AppendLine(this, $"UNPACK {reg} {reg}");
						}
					}

					Compiler.Instance.DeallocRegister(ref dataReg);
				}


				if (tokenIDVar != null)
				{
					var reg = Compiler.Instance.AllocRegister(output, tokenIDVar, tokenIDVar.Name);
					tokenIDVar.Register = reg;
					output.AppendLine(this, $"COPY {idReg} {reg} // tokenID");

					implicits.Add(tokenIDVar);
				}

				Compiler.Instance.DeallocRegister(ref idReg);
			}

			Compiler.Instance.DeallocRegister(ref tempReg1);
			Compiler.Instance.DeallocRegister(ref tempReg2);

			foreach (var variable in this.scope.Variables.Values)
			{
				if (variable.Storage != VarStorage.Argument)
				{
					continue;
				}

				variable.Register = Compiler.Instance.AllocRegister(output, variable, variable.Name);
				output.AppendLine(this, $"POP {variable.Register}");

				switch (variable.Type.Kind)
				{
					// TODO this fixes numbers passed as strings, but maybe other types would benefit from this...
					case VarKind.Number:
						output.AppendLine(this, $"CAST {variable.Register} {variable.Register} #Number");
						break;
				}

				variable.CallNecessaryConstructors(output, variable.Type, variable.Register);
			}

			this.scope.Enter(output);
			body.GenerateCode(output);
			this.scope.Leave(output);

			foreach (var variable in this.scope.Variables.Values)
			{
				if (variable.Storage != VarStorage.Argument)
				{
					continue;
				}

				Compiler.Instance.DeallocRegister(ref variable.Register);
			}

			foreach (var variable in implicits)
			{
				Compiler.Instance.DeallocRegister(ref variable.Register);
			}

			output.AppendLine(this, $"@{GetExitLabel()}:");

			// NOTE we don't need to dealloc anything here besides the global vars
			foreach (var variable in this.scope.Parent.Variables.Values)
			{
				if (variable.Storage != VarStorage.Global)
				{
					continue;
				}

				if (variable.Register == null)
				{
					if (isConstructor && !variable.Type.IsStorageBound)
					{
						if (variable.Type.Kind == VarKind.Array)
						{
							// HACK: quick solution to prevent global arrasy, it might be possible to support then as globals, but I did not have time yet to study if feasible...
							throw new CompilerException($"instead of array use storage_map type for variable: '{variable.Name}'");
						}
						else
						{
							throw new CompilerException($"global variable '{variable.Name}' not assigned in constructor of {this.scope.Module.Name}");
						}
					}

					continue; // if we hit this, means it went unused 
				}

				bool isAssigned = false;
				this.body.Visit((node) =>
				{
					var assignement = node as AssignStatement;
					if (assignement != null && assignement.variable == variable)
					{
						isAssigned = true;
					}
				});

				// if the global variable is not assigned within the current method, no need to save it value back to the storage
				if (isAssigned)
				{
					if (tempReg1 == null)
					{
						tempReg1 = Compiler.Instance.AllocRegister(output, this);
						output.AppendLine(this, $"LOAD {tempReg1} \"Data.Set\"");
					}

					// NOTE we could keep this key loaded in a register if we had enough spare registers..
					output.AppendLine(this, $"// writing global: {variable.Name}");
					output.AppendLine(this, $"PUSH {variable.Register}");
					output.AppendLine(this, $"LOAD r0 \"{variable.Name}\"");
					output.AppendLine(this, $"PUSH r0");
					output.AppendLine(this, $"EXTCALL {tempReg1}");
				}

				if (variable.Register != null)
				{
					Compiler.Instance.DeallocRegister(ref variable.Register);
				}
			}
			Compiler.Instance.DeallocRegister(ref tempReg1);

			output.AppendLine(this, "RET");
			this.@interface.EndAsmLine = output.LineCount;

			var returnType = this.@interface.ReturnType.Kind;
			if (returnType != VarKind.None)
			{
				// TODO validate if all possible paths have a return 
				bool hasReturn = false;
				this.body.Visit((node) =>
				{
					if (node is ReturnStatement)
					{
						hasReturn = true;
					}
				});

				if (!hasReturn)
				{
					throw new CompilerException($"not all paths of method {@interface.Name} return a value of type {returnType}");
				}
			}
		}

		internal string GetEntryLabel()
		{
			if (@interface.Kind == MethodKind.Constructor)
			{
				return "entry_constructor";
			}
			else
			{
				return "entry_" + this.Name;
			}
		}

		internal string GetExitLabel()
		{
			if (@interface.Kind == MethodKind.Constructor)
			{
				return "exit_constructor";
			}
			else
			{
				return "exit_" + this.Name;
			}
		}

		internal ContractMethod GetABI()
		{
			var temp = new List<ContractParameter>();

			foreach (var entry in this.@interface.Parameters)
			{
				temp.Add(new ContractParameter(entry.Name, MethodInterface.ConvertType(entry.Type)));
			}

			return new ContractMethod(this.Name, MethodInterface.ConvertType(this.@interface.ReturnType), -1, temp.ToArray());
		}
	}
}
