using Phantasma.Tomb.AST;
using Phantasma.Tomb.AST.Declarations;
using Phantasma.Tomb.Lexers;

namespace TOMBLib.Tests.AST.Declaration;

public class StructDeclarationTests
{
	[SetUp]
	public void Setup()
	{
		TombLangLexer lexer = new TombLangLexer();
	}

	[Test]
	public void Constructor_WithValidArguments_CreatesFieldsArray()
	{
		// Arrange
		var name = "MyStruct";
		var fields = new List<StructField>()
		{
			new StructField("myInt", VarType.Find(VarKind.Number)),
			new StructField("myFloat", VarType.Find(VarKind.Decimal,2)),
			new StructField("myString", VarType.Find(VarKind.String)),
		};

		// Act
		var structDeclaration = new StructDeclaration(name, fields);

		// Assert
		Assert.That(structDeclaration.Name, Is.EqualTo(name));
		Assert.That(structDeclaration.fields, Is.Not.Null);
		Assert.That(structDeclaration.fields.Length, Is.EqualTo(fields.Count));
		for (int i = 0; i < fields.Count; i++)
		{
			Assert.That(structDeclaration.fields[i].name, Is.EqualTo(fields[i].name));
			Assert.That(structDeclaration.fields[i].type, Is.EqualTo(fields[i].type));
		}
	}

	[Test]
	public void IsNodeUsed_WithSameNode_ReturnsTrue()
	{
		// Arrange
		var structDeclaration = new StructDeclaration("MyStruct", new List<StructField>());

		// Act
		var isUsed = structDeclaration.IsNodeUsed(structDeclaration);

		// Assert
		Assert.That(isUsed, Is.True);
	}

	[Test]
	public void Visit_WithCallback_CallsCallbackWithThisNode()
	{
		// Arrange
		var structDeclaration = new StructDeclaration("MyStruct", new List<StructField>());
		bool isCallbackCalled = false;

		// Act
		structDeclaration.Visit(node => isCallbackCalled = (node == structDeclaration));

		// Assert
		Assert.That(isCallbackCalled, Is.True);
	}
}
