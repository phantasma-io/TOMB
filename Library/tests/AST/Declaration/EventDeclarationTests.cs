using NUnit.Framework;
using Phantasma.Tomb.AST;
using Phantasma.Tomb.AST.Declarations;
using Phantasma.Tomb.Lexers;

namespace TOMBLib.Tests.AST.Declaration;

public class EventDeclarationTests
{
	private VarType _returnType;
	private byte[] _descriptionScript;

	[SetUp]
	public void Setup()
	{
		TombLangLexer lexer = new TombLangLexer();
		_returnType = VarType.Find(VarKind.String);
		_descriptionScript = EventDeclaration.GenerateScriptFromString(_returnType, "\"{address}: {data}\"");
	}

	/*[Test]
    public void TestEventDeclarationConstructor()
    {
        var eventDeclaration = new EventDeclaration(_scope, "TestEvent", 1, _returnType, _descriptionScript);

        Assert.AreEqual(_scope, eventDeclaration.scope);
        Assert.AreEqual(1, eventDeclaration.value);
        Assert.AreEqual(_returnType, eventDeclaration.returnType);
        Assert.AreEqual(_descriptionScript, eventDeclaration.descriptionScript);
    }
    
    [Test]
    public void TestValidate()
    {
        var eventDeclaration = new EventDeclaration(_scope, "TestEvent", 1, _returnType, _descriptionScript);
        Assert.DoesNotThrow(() => eventDeclaration.Validate());
    }*/
}
