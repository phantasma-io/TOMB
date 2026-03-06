using NUnit.Framework;
using Phantasma.Tomb.AST;
using Phantasma.Tomb.AST.Declarations;
using Phantasma.Tomb.CodeGen;
using Phantasma.Tomb.Lexers;

namespace TOMBLib.Tests.AST.Declaration;

public class DecimalDeclarationTests
{
	[SetUp]
	public void Setup()
	{
		_ = new TombLangLexer();
	}

	[Test]
	public void DecimalDeclaration_Constructor_SetsProperties()
	{
		// Arrange
		var module = new Contract("myself", ModuleKind.Contract);
		var parentScope = new Scope(module);
		var name = "myDecimal";
		var type = VarType.Find(VarKind.Decimal, 2);
		var storage = VarStorage.Local;
		var decimals = 2;

		// Act
		var constDeclaration = new DecimalDeclaration(parentScope, name, decimals, storage);

		// Assert
		Assert.That(constDeclaration.ParentScope, Is.EqualTo(parentScope));
		Assert.That(constDeclaration.Name, Is.EqualTo(name));
		Assert.That(constDeclaration.Type, Is.EqualTo(type));
		Assert.That(constDeclaration.Decimals, Is.EqualTo(decimals));
	}
}
