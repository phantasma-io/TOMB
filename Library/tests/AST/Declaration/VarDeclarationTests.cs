using Phantasma.Tomb.AST;
using Phantasma.Tomb.AST.Declarations;
using Phantasma.Tomb.Lexers;

namespace TOMBLib.Tests.AST.Declaration;

public class VarDeclarationTests
{
	[SetUp]
	public void Setup()
	{
		TombLangLexer lexer = new TombLangLexer();
	}

	[Test]
	public void Constructor_WithValidArguments_SetsPropertiesCorrectly()
	{
		// Arrange
		Scope parentScope = null;
		var name = "myVar";
		var type = VarType.Find(VarKind.Any);
		var storage = VarStorage.Global;

		// Act
		var varDeclaration = new VarDeclaration(parentScope, name, type, storage);

		// Assert
		Assert.That(varDeclaration.ParentScope, Is.EqualTo(parentScope));
		Assert.That(varDeclaration.Name, Is.EqualTo(name));
		Assert.That(varDeclaration.Type, Is.EqualTo(type));
		Assert.That(varDeclaration.Storage, Is.EqualTo(storage));
		Assert.That(varDeclaration.Register, Is.Null);
	}

	[Test]
	public void ToString_WithValidVarDeclaration_ReturnsExpectedString()
	{
		// Arrange
		Scope parentScope = null;
		var name = "myVar";
		var type = VarType.Find(VarKind.Bool);
		var storage = VarStorage.Local;
		var varDeclaration = new VarDeclaration(parentScope, name, type, storage);

		// Act
		var result = varDeclaration.ToString();

		// Assert
		Assert.That(result, Is.EqualTo("var myVar:Bool"));
	}

	[Test]
	public void Visit_WithCallback_CallsCallbackWithThisNode()
	{
		// Arrange
		Scope parentScope = null;
		var name = "myVar";
		var type = VarType.Find(VarKind.Decimal, 2);
		var storage = VarStorage.Register;
		var varDeclaration = new VarDeclaration(parentScope, name, type, storage);
		bool isCallbackCalled = false;

		// Act
		varDeclaration.Visit(node => isCallbackCalled = (node == varDeclaration));

		// Assert
		Assert.That(isCallbackCalled, Is.True);
	}

	[Test]
	public void IsNodeUsed_WithSameNode_ReturnsTrue()
	{
		// Arrange
		Scope parentScope = null;
		var name = "myVar";
		var type = VarType.Find(VarKind.String);
		var storage = VarStorage.Global;
		var varDeclaration = new VarDeclaration(parentScope, name, type, storage);

		// Act
		var isUsed = varDeclaration.IsNodeUsed(varDeclaration);

		// Assert
		Assert.That(isUsed, Is.True);
	}
}
