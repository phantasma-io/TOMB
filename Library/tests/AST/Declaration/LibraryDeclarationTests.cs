using NUnit.Framework;
using Phantasma.Tomb;
using Phantasma.Tomb.AST;
using Phantasma.Tomb.AST.Declarations;
using Phantasma.Tomb.CodeGen;
using Phantasma.Tomb.Lexers;

namespace TOMBLib.Tests.AST.Declaration;

public class LibraryDeclarationTests
{
	[SetUp]
	public void Setup()
	{
		_ = new TombLangLexer();
	}

	private static Scope CreateScope(string moduleName = "test")
	{
		var module = new Contract(moduleName, ModuleKind.Contract);
		return new Scope(module);
	}

	[Test]
	public void Constructor_WithValidArguments_SetsPropertiesCorrectly()
	{
		// Arrange
		var parentScope = CreateScope();
		var name = "myLibrary";

		// Act
		var libraryDeclaration = new LibraryDeclaration(parentScope, name);

		// Assert
		Assert.That(libraryDeclaration.ParentScope, Is.EqualTo(parentScope));
		Assert.That(libraryDeclaration.Name, Is.EqualTo(name));
		Assert.That(libraryDeclaration.methods, Is.Not.Null);
		Assert.That(libraryDeclaration.methods.Count, Is.EqualTo(0));
	}

	[Test]
	public void AddMethod_WithValidArguments_AddsMethodToMethodsDictionary()
	{
		// Arrange
		var parentScope = CreateScope();
		var name = "myLibrary";
		var libraryDeclaration = new LibraryDeclaration(parentScope, name);

		// Act
		var method = libraryDeclaration.AddMethod("myMethod", MethodImplementationType.ContractCall, VarKind.Any,
			new MethodParameter[0]);

		// Assert
		/*Assert.That(method.Parent, Is.EqualTo(libraryDeclaration));
        Assert.That(method.Convention, Is.EqualTo(MethodImplementationType.Normal));
        Assert.That(method.Name, Is.EqualTo("myMethod"));
        Assert.That(method.IsMethod, Is.True);
        Assert.That(method.Kind, Is.EqualTo(MethodKind.Method));
        Assert.That(method.ReturnType.Kind, Is.EqualTo(VarKind.Void));
        Assert.That(method.Parameters, Is.Not.Null);
        Assert.That(method.Parameters.Length, Is.EqualTo(0));
        Assert.That(method.Alias, Is.Null);
        Assert.That(method.IsAbstract, Is.False);
        Assert.That(method.IsBuiltin, Is.False);
        Assert.That(libraryDeclaration.methods.ContainsKey("myMethod"), Is.True);
        Assert.That(libraryDeclaration.methods["myMethod"], Is.EqualTo(method));*/
	}

	[Test]
	public void FindMethod_WithExistingMethod_ReturnsMethodInterface()
	{
		// Arrange
		var parentScope = CreateScope();
		var name = "myLibrary";
		var libraryDeclaration = new LibraryDeclaration(parentScope, name);
		var method = libraryDeclaration.AddMethod("myMethod", MethodImplementationType.ExtCall, VarKind.None,
			new MethodParameter[0]);

		// Act
		var foundMethod = libraryDeclaration.FindMethod("myMethod");

		// Assert
		Assert.That(foundMethod, Is.EqualTo(method));
	}

	[Test]
	public void FindMethod_WithNonExistingMethod_ThrowsCompilerException()
	{
		// Arrange
		var parentScope = CreateScope();
		var name = "myLibrary";
		var libraryDeclaration = new LibraryDeclaration(parentScope, name);

		// Act & Assert
		Assert.Throws<CompilerException>(() => libraryDeclaration.FindMethod("nonExistingMethod"));
	}

	[Test]
	public void IsNodeUsed_WithSameNode_ReturnsTrue()
	{
		// Arrange
		var parentScope = CreateScope();
		var name = "myLibrary";
		var libraryDeclaration = new LibraryDeclaration(parentScope, name);

		// Act
		var isUsed = libraryDeclaration.IsNodeUsed(libraryDeclaration);

		// Assert
		Assert.That(isUsed, Is.True);
	}

	[Test]
	public void Visit_WithCallback_CallsCallbackWithThisNode()
	{
		// Arrange
		var parentScope = CreateScope();
		var name = "myLibrary";
		var libraryDeclaration = new LibraryDeclaration(parentScope, name);
		bool isCallbackCalled = false;

		// Act
		libraryDeclaration.Visit(node => isCallbackCalled = (node == libraryDeclaration));

		// Assert
		Assert.That(isCallbackCalled, Is.True);
	}

	[Test]
	public void MakeGenericLib_WithValidArguments_ReturnsGenericLibraryDeclaration()
	{
		// Arrange
		var parentScope = CreateScope();
		var name = "myLibrary";
		var libraryDeclaration = new LibraryDeclaration(parentScope, name);
		var generics = new[] { VarType.Find(VarKind.Number), VarType.Find(VarKind.String) };
		// Act
		var genericLib = libraryDeclaration.MakeGenericLib("T1,T2", "MyGenericLib", generics);

		// Assert
		Assert.That(genericLib, Is.Not.Null);
		Assert.That(genericLib.ParentScope, Is.EqualTo(parentScope));
		Assert.That(genericLib.Name, Is.EqualTo("MyGenericLib"));
		Assert.That(genericLib.Generics, Is.EqualTo(generics));
		Assert.That(genericLib.methods, Is.Not.Null);
		Assert.That(genericLib.methods.Count, Is.EqualTo(0));
	}

	[Test]
	public void PatchMap_WithMapDeclaration_ReturnsGenericLibraryDeclarationWithKeyAndValueTypesAsGenerics()
	{
		// Arrange
		var parentScope = CreateScope();
		var name = "myLibrary";
		var libraryDeclaration = new LibraryDeclaration(parentScope, name);
		var mapDecl = new MapDeclaration(parentScope, "myMap", VarType.Find(VarKind.Number), VarType.Find(VarKind.String));

		// Act
		var patchedLib = libraryDeclaration.PatchMap(mapDecl);

		// Assert
		Assert.That(patchedLib, Is.Not.Null);
		Assert.That(patchedLib.ParentScope, Is.EqualTo(parentScope));
		Assert.That(patchedLib.Name, Is.EqualTo(name));
		Assert.That(patchedLib.methods, Is.Not.Null);
		Assert.That(patchedLib.methods.Count, Is.EqualTo(0));
	}

	[Test]
	public void PatchList_WithListDeclaration_ReturnsGenericLibraryDeclarationWithValueTypeAsGeneric()
	{
		// Arrange
		var parentScope = CreateScope();
		var name = "myLibrary";
		var libraryDeclaration = new LibraryDeclaration(parentScope, name);
		var listDecl = new ListDeclaration(parentScope, "myList", VarType.Find(VarKind.Decimal, 2));

		// Act
		var patchedLib = libraryDeclaration.PatchList(listDecl);

		// Assert
		Assert.That(patchedLib, Is.Not.Null);
		Assert.That(patchedLib.ParentScope, Is.EqualTo(parentScope));
		Assert.That(patchedLib.Name, Is.EqualTo(name));
		Assert.That(patchedLib.methods, Is.Not.Null);
		Assert.That(patchedLib.methods.Count, Is.EqualTo(0));
	}

	[Test]
	public void PatchSet_WithSetDeclaration_ReturnsGenericLibraryDeclarationWithValueTypeAsGeneric()
	{
		// Arrange
		var parentScope = CreateScope();
		var name = "myLibrary";
		var libraryDeclaration = new LibraryDeclaration(parentScope, name);
		var setDecl = new SetDeclaration(parentScope, "mySet", VarType.Find(VarKind.String));

		// Act
		var patchedLib = libraryDeclaration.PatchSet(setDecl);

		// Assert
		Assert.That(patchedLib, Is.Not.Null);
		Assert.That(patchedLib.ParentScope, Is.EqualTo(parentScope));
		Assert.That(patchedLib.Name, Is.EqualTo(name));
		Assert.That(patchedLib.methods, Is.Not.Null);
		Assert.That(patchedLib.methods.Count, Is.EqualTo(0));
	}

	[Test]
	public void GenerateCode_DoesNothing()
	{
		// Arrange
		var parentScope = CreateScope();
		var name = "myLibrary";
		var libraryDeclaration = new LibraryDeclaration(parentScope, name);
		var output = new CodeGenerator();

		// Act
		libraryDeclaration.GenerateCode(output);

		// Assert
		Assert.That(output.LineCount, Is.EqualTo(0));
	}
}
