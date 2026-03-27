using NUnit.Framework;
using System;
using Phantasma.Tomb;

namespace TOMBLib.Tests.Compilers;

public sealed class CompilerCliMetadataTests
{
	[TestCase("--version")]
	[TestCase("-v")]
	[TestCase("version")]
	public void VersionAliasesAreRecognized(string arg)
	{
		Assert.That(CliMetadata.IsVersionArgument(arg), Is.True);
	}

	[TestCase("--help")]
	[TestCase("-h")]
	[TestCase("help")]
	public void HelpAliasesAreRecognized(string arg)
	{
		Assert.That(CliMetadata.IsHelpArgument(arg), Is.True);
	}

	[Test]
	public void CompilerExecutableNameIsPinned()
	{
		Assert.That(CliMetadata.ExecutableName, Is.EqualTo("pha-tomb"));
	}

	[Test]
	public void VersionSurfaceReturnsParseableVersion()
	{
		var version = CliMetadata.GetVersion();
		Assert.That(version, Is.Not.Null.And.Not.Empty);
		Assert.That(Version.TryParse(version, out _), Is.True, $"version='{version}'");
	}
}
