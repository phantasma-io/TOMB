using System.Collections.Generic;
using Phantasma.Tomb.AST.Declarations;
using Phantasma.Tomb.CodeGen;

namespace Phantasma.Tomb.AST
{
	public sealed class Scope
	{
		public readonly Scope? Parent;
		public readonly Module Module;
		public readonly string Name;

		public MethodDeclaration? Method; // optional

		public int Level
		{
			get
			{
				if (Parent != null)
				{
					return Parent.Level + 1;
				}

				return 0;
			}
		}

		public Scope(Scope parent, string name, MethodParameter[] parameters)
		{
			this.Parent = parent;
			this.Module = parent.Module;
			this.Method = parent.Method;
			this.Name = name;

			foreach (var entry in parameters)
			{
				var varDecl = new VarDeclaration(this, entry.Name, entry.Type, VarStorage.Argument);
				this.AddVariable(varDecl);
			}
		}

		public Scope(Scope parent, string name) : this(parent, name, new MethodParameter[0])
		{
		}

		public Scope(Module module)
		{
			this.Module = module;
			this.Name = module.Name;
		}

		public override string ToString()
		{
			if (Parent != null)
			{
				return Parent.ToString() + "=>" + Name;
			}

			return Name;
		}

		public readonly Dictionary<string, VarDeclaration> Variables = new Dictionary<string, VarDeclaration>();
		public readonly Dictionary<string, ConstDeclaration> Constants = new Dictionary<string, ConstDeclaration>();
		public readonly List<MethodInterface> Methods = new List<MethodInterface>();

		public void AddVariable(VarDeclaration decl)
		{
			if (Variables.ContainsKey(decl.Name))
			{
				throw new CompilerException("duplicated declaration: " + decl.Name);
			}

			if (decl.Name == decl.Name.ToUpper() && decl.Storage != VarStorage.NFT)
			{
				throw new CompilerException("invalid variable name: " + decl.Name);
			}

			Variables[decl.Name] = decl;
		}

		public void AddConstant(ConstDeclaration decl)
		{
			if (Constants.ContainsKey(decl.Name))
			{
				throw new CompilerException("duplicated declaration: " + decl.Name);
			}

			if (decl.Name != decl.Name.ToUpper())
			{
				throw new CompilerException("invalid constant name: " + decl.Name);
			}

			Constants[decl.Name] = decl;
		}

		public VarDeclaration FindVariable(string name)
		{
			var result = FindVariable(name, required: false);
			if (result != null)
			{
				return result;
			}

			throw new CompilerException("variable not declared: " + name);
		}

		public VarDeclaration? FindVariable(string name, bool required)
		{
			if (Variables.ContainsKey(name))
			{
				return Variables[name];
			}

			if (Parent != null)
			{
				return Parent.FindVariable(name, required);
			}

			if (required)
			{
				throw new CompilerException("variable not declared: " + name);
			}

			return null;
		}

		public ConstDeclaration FindConstant(string name)
		{
			var result = FindConstant(name, required: false);
			if (result != null)
			{
				return result;
			}

			throw new CompilerException("constant not declared: " + name);
		}

		public ConstDeclaration? FindConstant(string name, bool required)
		{
			if (Constants.ContainsKey(name))
			{
				return Constants[name];
			}

			if (Parent != null)
			{
				return Parent.FindConstant(name, required);
			}

			if (required)
			{
				throw new CompilerException("constant not declared: " + name);
			}

			return null;
		}

		private Scope? previousScope;

		public void Enter(CodeGenerator output)
		{
			previousScope = CodeGenerator.currentScope;
			CodeGenerator.currentScope = this;

			//Console.WriteLine("entering " + this.Name);
		}

		public void Leave(CodeGenerator output)
		{
			//Console.WriteLine("leaving " + this.Name);

			foreach (var variable in this.Variables.Values)
			{
				if (variable.Storage == VarStorage.Global || variable.Storage == VarStorage.NFT)
				{
					continue;
				}

				if (variable.Register == null)
				{
					throw new CompilerException("unused variable: " + variable.Name);
				}

				Compiler.Instance.DeallocRegister(ref variable.Register);
			}

			CodeGenerator.currentScope = previousScope;
		}
	}
}
