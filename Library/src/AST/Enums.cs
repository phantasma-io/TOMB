using Phantasma.Tomb.AST.Declarations;
using Phantasma.Tomb.CodeGen;
using System.Collections.Generic;

namespace Phantasma.Tomb.AST
{
	public enum VarKind
	{
		None,
		Unknown,
		Generic,
		Number,
		Bool,
		String,
		Timestamp,
		Address,
		Hash,
		Bytes,
		Enum,
		Task,
		Type,
		Any,
		Method,
		Module,
		Struct,
		Decimal,
		Storage_Map,
		Storage_List,
		Storage_Set,
		Array,
	}

	public abstract class VarType
	{
		public readonly VarKind Kind;

		/*public readonly static VarType None = Find(VarKind.None);
        public readonly static VarType Address = Find(VarKind.Address);
        public readonly static VarType Bool = Find(VarKind.Bool);*/

		protected VarType(VarKind kind)
		{
			Kind = kind;
		}

		private static Dictionary<string, VarType> _cache = new Dictionary<string, VarType>();

		public bool IsStorageBound => Kind == VarKind.Storage_Map || Kind == VarKind.Storage_List || Kind == VarKind.Storage_Set;

		public bool IsWeird => Kind == VarKind.None || Kind == VarKind.Unknown || Kind == VarKind.Generic || Kind == VarKind.Any;

		public static VarType Generic(int index)
		{
			return Find(VarKind.Generic, index);
		}

		public static VarType FindArray(VarKind kind)
		{
			return Find(VarKind.Array, kind.ToString());
		}

		public static VarType Find(VarKind kind, object? extra = null)
		{
			var key = kind.ToString();
			if (extra != null)
			{
				key = $"{key}<{extra}>";
			}

			if (_cache.ContainsKey(key))
			{
				return _cache[key];
			}

			VarType result;

			switch (kind)
			{
				case VarKind.Number:
				case VarKind.Bool:
				case VarKind.String:
				case VarKind.Timestamp:
				case VarKind.Address:
				case VarKind.Hash:
				case VarKind.Bytes:
				case VarKind.Task:
				case VarKind.Type:
				case VarKind.None:
				case VarKind.Any:
				case VarKind.Storage_List:
				case VarKind.Storage_Map:
				case VarKind.Storage_Set:
				case VarKind.Unknown:
					result = new PrimitiveVarType(kind);
					break;

				case VarKind.Decimal:
					if (extra is not int decimals)
					{
						throw new CompilerException(kind + " type requires decimal precision for initialization");
					}

					result = new DecimalVarType(decimals);
					break;

				case VarKind.Struct:
					if (extra != null && extra is not string)
					{
						throw new CompilerException("struct type requires a struct name");
					}

					// Library ABI declarations use anonymous struct placeholders that are
					// resolved by call-site context later (for example Runtime.fromBytes).
					var structName = extra as string ?? string.Empty;
					result = new StructVarType(structName);
					break;

				case VarKind.Enum:
					// Unscoped enum parameters (without concrete enum name) are valid in
					// library declarations and are resolved later by caller context.
					var enumName = extra as string ?? string.Empty;
					result = new EnumVarType(enumName);
					break;

				case VarKind.Method:
					if (extra != null && extra is not MethodDeclaration)
					{
						throw new CompilerException("method type requires a method declaration");
					}

					// A method-type placeholder is valid in built-in signatures (Task.start).
					var methodDeclaration = extra as MethodDeclaration;
					result = new MethodVarType(methodDeclaration);
					break;

				case VarKind.Module:
					if (extra != null && extra is not Module)
					{
						throw new CompilerException("module type requires a module declaration");
					}

					// Module payload types may be declared before the concrete module node exists.
					var module = extra as Module;
					result = new ModuleVarType(module);
					break;

				case VarKind.Generic:
					if (extra is not int genericIndex)
					{
						throw new CompilerException("generic type requires generic index");
					}

					result = new GenericVarType(genericIndex);
					break;

				case VarKind.Array:
					VarType? elementType = extra as VarType;

					if (elementType == null)
					{
						VarKind elementKind;

						var extraStr = extra?.ToString();

						if (string.IsNullOrEmpty(extraStr))
						{
							throw new CompilerException($"Untype arrays not supported");
						}

						if (System.Enum.TryParse<VarKind>(extraStr, out elementKind))
						{
							elementType = Find(elementKind);
						}

						if (elementType == null)
						{
							throw new CompilerException($"Could not initialize array element type: {extra}");
						}
					}

					result = new ArrayVarType(elementType);
					break;

				default:
					throw new CompilerException($"Could not initialize type: {kind}");
			}

			_cache[key] = result;
			return result;
		}
	}

	public class PrimitiveVarType : VarType
	{
		public PrimitiveVarType(VarKind kind) : base(kind)
		{

		}

		public override string ToString()
		{
			return $"{Kind}";
		}
	}

	public class StructVarType : VarType
	{
		public readonly string name;

		public StructDeclaration? decl;

		public StructVarType(string name) : base(VarKind.Struct)
		{
			this.name = name;
		}

		public override string ToString()
		{
			if (string.IsNullOrEmpty(name))
			{
				return Kind.ToString();
			}

			return $"{Kind}<{name}>";
		}
	}

	public class EnumVarType : VarType
	{
		public readonly string name;

		public EnumDeclaration? decl;

		public EnumVarType(string name) : base(VarKind.Enum)
		{
			this.name = name;
		}

		public override string ToString()
		{
			if (string.IsNullOrEmpty(name))
			{
				return Kind.ToString();
			}

			return $"{Kind}<{name}>";
		}
	}

	public class DecimalVarType : VarType
	{
		public readonly int decimals;

		public DecimalVarType(int decimals) : base(VarKind.Decimal)
		{
			this.decimals = decimals;
		}

		public override string ToString()
		{
			return $"{Kind}<{decimals}>";
		}
	}

	public class GenericVarType : VarType
	{
		public readonly int index;

		public GenericVarType(int decimals) : base(VarKind.Generic)
		{
			this.index = decimals;
		}

		public override string ToString()
		{
			return $"{Kind}<{index}>";
		}
	}

	public class ArrayVarType : VarType
	{
		public readonly VarType elementType;

		public ArrayVarType(VarType elementType) : base(VarKind.Array)
		{
			this.elementType = elementType;
		}

		public override string ToString()
		{
			return $"{Kind}<{elementType}>";
		}
	}

	public class MethodVarType : VarType
	{
		public readonly MethodDeclaration? method;
		public MethodVarType(MethodDeclaration? method) : base(VarKind.Method)
		{
			this.method = method;
		}

		public override string ToString()
		{
			if (method != null)
			{
				return $"{Kind}<{method.Name}>";
			}
			return $"{Kind}";
		}
	}

	public class ModuleVarType : VarType
	{
		public readonly Module? module;

		public ModuleVarType(Module? module) : base(VarKind.Module)
		{
			this.module = module;
		}

		public override string ToString()
		{
			if (module != null)
			{
				return $"{Kind}<{module.Name}>";
			}
			return $"{Kind}";
		}
	}


	public enum VarStorage
	{
		Global,
		Local,
		Argument,
		NFT,
		Register,
	}

	public enum OperatorKind
	{
		Unknown,
		Assignment,
		Equal,
		Different,
		Less,
		LessOrEqual,
		Greater,
		GreaterOrEqual,
		Addition,
		Subtraction,
		Multiplication,
		Division,
		Modulus,
		Power,
		ShiftRight,
		ShiftLeft,
		Or,
		And,
		Xor,
	}

	public enum MethodKind
	{
		Method,
		Constructor,
		Task,
		Trigger,
		Property,
	}
}
