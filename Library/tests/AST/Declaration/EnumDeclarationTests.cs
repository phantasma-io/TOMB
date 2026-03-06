using System;
using System.Collections.Generic;
using NUnit.Framework;
using Phantasma.Tomb;
using Phantasma.Tomb.AST.Declarations;
using Phantasma.Tomb.Lexers;

namespace TOMBLib.Tests.AST.Declaration;

public class EnumDeclarationTests
{
	[SetUp]
	public void Setup()
	{
		TombLangLexer lexer = new TombLangLexer();
	}

	[Test]
	public void Constructor_WithDuplicateEntryName_ThrowsCompilerException()
	{
		// Arrange
		var entries = new List<EnumEntry>()
		{
			new EnumEntry("Red", 1),
			new EnumEntry("Green", 2),
			new EnumEntry("Red", 3)
		};

		// Act & Assert
		Assert.Throws<CompilerException>(() => new EnumDeclaration("Colors", entries));
	}

	[Test]
	public void Constructor_WithValidEntries_CreatesEntryNamesDictionary()
	{
		// Arrange
		var entries = new List<EnumEntry>()
		{
			new EnumEntry("Red", 1),
			new EnumEntry("Green", 2),
			new EnumEntry("Blue", 3)
		};

		// Act
		var enumDeclaration = new EnumDeclaration("Colors", entries);


		// Assert
		Assert.That(enumDeclaration.entryNames, Is.Not.Null);
		Assert.That(enumDeclaration.entryNames.Count, Is.EqualTo(entries.Count));
		foreach (var entry in entries)
		{
			Assert.That(enumDeclaration.entryNames.ContainsKey(entry.name), Is.True);
			Assert.That(enumDeclaration.entryNames[entry.name], Is.EqualTo(entry.value));
		}
	}

	[Test]
	public void IsNodeUsed_WithSameNode_ReturnsTrue()
	{
		// Arrange
		var enumDeclaration = new EnumDeclaration("Colors", new List<EnumEntry>());

		// Act
		var isUsed = enumDeclaration.IsNodeUsed(enumDeclaration);

		// Assert
		Assert.That(isUsed, Is.True);
	}

	[Test]
	public void Visit_WithCallback_CallsCallbackWithThisNode()
	{
		// Arrange
		var enumDeclaration = new EnumDeclaration("Colors", new List<EnumEntry>());
		bool isCallbackCalled = false;

		// Act
		enumDeclaration.Visit(node => isCallbackCalled = (node == enumDeclaration));

		// Assert
		Assert.That(isCallbackCalled, Is.True);
	}
}
