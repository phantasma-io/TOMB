namespace Phantasma.Tomb.AST
{
	public abstract class Declaration : Node
	{
		public readonly string Name;
		public Scope? ParentScope { get; internal set; }

		protected Declaration(Scope? parentScope, string name)
		{
			Name = name;
			ParentScope = parentScope;
			ValidateName();
		}

		protected virtual void ValidateName()
		{
			if (!Lexer.Active.IsValidIdentifier(Name))
			{
				throw new CompilerException("Invalid identifier: " + Name);
			}
		}
	}
}
