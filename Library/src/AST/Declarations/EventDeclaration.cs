using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Numerics;
using Phantasma.Core.Domain;
using Phantasma.Tomb.CodeGen;
using Phantasma.Business.Blockchain;
using Phantasma.Business.CodeGen.Assembler;
using Phantasma.Business.Blockchain.VM;
using Phantasma.Core.Domain.VM;
using Phantasma.Core.Domain.Interfaces;
using Phantasma.Core.Domain.Token.Structs;

namespace Phantasma.Tomb.AST.Declarations
{
	public class EventDeclaration : Declaration
	{
		public readonly Scope scope;
		public readonly byte value;
		public readonly VarType returnType;
		public readonly byte[] descriptionScript;

		struct StringToken
		{
			public readonly bool dynamic;
			public readonly string value;

			public StringToken(bool dynamic, string value)
			{
				this.dynamic = dynamic;
				this.value = value;
			}

			public override string ToString()
			{
				return value;
			}
		}

		public EventDeclaration(Scope scope, string name, byte value, VarType returnType, byte[] description) : base(scope.Parent, name)
		{
			this.scope = scope;
			this.value = value;
			this.returnType = returnType;
			this.descriptionScript = description;

		}

		public static byte[] GenerateScriptFromString(VarType type, string src)
		{
			src = src.Substring(1, src.Length - 2); // remove "" delimiters

			var tokens = new List<StringToken>();

			var sb = new StringBuilder();

			bool insideTags = false;
			for (int i = 0; i < src.Length; i++)
			{
				var ch = src[i];

				switch (ch)
				{
					case '{':
						if (insideTags)
						{
							throw new CompilerException("Open declaration tag mismatch");
						}

						if (sb.Length > 0)
						{
							tokens.Add(new StringToken(false, sb.ToString()));
							sb.Clear();
						}
						insideTags = true;
						break;

					case '}':
						if (!insideTags)
						{
							throw new CompilerException("Close declaration tag mismatch");
						}

						if (sb.Length == 0)
						{
							throw new CompilerException("Empty declaration tag");
						}
						insideTags = false;
						tokens.Add(new StringToken(true, sb.ToString()));
						sb.Clear();
						break;

					default:
						sb.Append(ch);
						break;
				}
			}

			if (sb.Length > 0)
			{
				tokens.Add(new StringToken(false, sb.ToString()));
			}

			sb.Clear();
			sb.AppendLine("POP r2"); // address
			sb.AppendLine("POP r3"); // data
			sb.AppendLine("LOAD r0 \"\"");
			foreach (var token in tokens)
			{
				if (token.dynamic)
				{
					if (token.value == "address")
					{
						sb.AppendLine($"CAST r2 r1 #String");
					}
					else
					if (token.value == "data")
					{
						if (type.Kind == VarKind.Struct)
						{
							throw new CompilerException($"struct fields not specified");
						}
						else
						{
							sb.AppendLine($"CAST r3 r1 #String");
						}
					}
					else
					if (token.value.StartsWith("data."))
					{
						throw new CompilerException($"Struct tags not implemented");
					}
					else
					{
						throw new CompilerException($"Invalid declaration tag: {token.value}");
					}
				}
				else
				{
					sb.AppendLine($"LOAD r1 \"{token.value}\"");
				}
				sb.AppendLine("ADD r0 r1 r0");
			}
			sb.AppendLine("PUSH r0"); // return result
			sb.AppendLine("RET");

			var asm = sb.ToString();
			var script = AssemblerUtils.BuildScript(asm);


			return script;
		}

		public override void Visit(Action<Node> callback)
		{
			callback(this);
		}

		public override bool IsNodeUsed(Node node)
		{
			return (node == this);
		}

		public void GenerateCode(CodeGenerator output)
		{
			// do nothing
		}

		private VMObject GenerateTestObject(VarType type)
		{
			VMObject obj;

			switch (type.Kind)
			{
				case VarKind.Number:
					obj = VMObject.FromObject(new BigInteger(123));
					break;

				case VarKind.String:
					obj = VMObject.FromObject("test");
					break;

				case VarKind.Bool:
					obj = VMObject.FromObject(true);
					break;

				case VarKind.Struct:
					var fields = new Dictionary<VMObject, VMObject>();

					if (type is not StructVarType structInfo || structInfo.decl == null)
					{
						throw new CompilerException("struct event parameter metadata not initialized");
					}

					foreach (var field in structInfo.decl.fields)
					{
						fields[VMObject.FromObject(field.name)] = GenerateTestObject(field.type);
					}

					obj = new VMObject();
					obj.SetValue(fields);

					using (var stream = new MemoryStream())
					{
						using (var writer = new BinaryWriter(stream))
						{
							obj.SerializeData(writer);
						}

						var bytes = stream.ToArray();
						obj.SetValue(bytes);
					}

					break;


				default:
					throw new CompilerException($"Can't initialize test object with type: {type}");
			}

			return obj;
		}

		private class CustomDescriptionVM : DescriptionVM
		{
			public CustomDescriptionVM(byte[] script, uint offset) : base(script, offset)
			{
			}

			public override IToken FetchToken(string symbol)
			{
				return new TokenInfo(symbol, symbol, Address.FromHash("test"), 0, 8, TokenFlags.Fungible | TokenFlags.Divisible, new byte[] { (byte)Opcode.RET }, ContractInterface.Empty);
			}

			public override string OutputAddress(Address address)
			{
				return address.Text;
			}

			public override string OutputSymbol(string symbol)
			{
				return symbol;
			}
		}

		public void Validate()
		{
			try
			{
				var vm = new CustomDescriptionVM(this.descriptionScript, 0);

				var obj = GenerateTestObject(this.returnType);
				vm.Stack.Push(obj);
				vm.Stack.Push(VMObject.FromObject(Address.Parse("S3dApERMJUMRYECjyKLJioz2PCBUY6HBnktmC9u1obhDAgm", true)));
				var state = vm.Execute();

				if (state != ExecutionState.Halt)
				{
					throw new CompilerException("description script did not execute correctly");
				}

				if (vm.Stack.Count > 0)
				{
					var result = vm.Stack.Pop();
				}
				else
				{
					throw new CompilerException("description script did not return a result");
				}
			}
			catch (Exception e)
			{
				if (e is CompilerException)
				{
					throw;
				}

				throw new CompilerException($"Error validating description script. {e.Message}");
			}
		}

		internal ContractEvent GetABI()
		{
			var type = MethodInterface.ConvertType(this.returnType);
			return new ContractEvent(this.value, this.Name, type, descriptionScript);
		}
	}
}
