using System;
using System.IO;
using System.Reflection;

namespace Phantasma.Tomb
{
	internal static class CliMetadata
	{
		internal const string ExecutableName = "pha-tomb";

		internal static bool IsVersionArgument(string arg)
		{
			return string.Equals(arg, "--version", StringComparison.OrdinalIgnoreCase)
				|| string.Equals(arg, "-v", StringComparison.OrdinalIgnoreCase)
				|| string.Equals(arg, "version", StringComparison.OrdinalIgnoreCase);
		}

		internal static bool IsHelpArgument(string arg)
		{
			return string.Equals(arg, "--help", StringComparison.OrdinalIgnoreCase)
				|| string.Equals(arg, "-h", StringComparison.OrdinalIgnoreCase)
				|| string.Equals(arg, "help", StringComparison.OrdinalIgnoreCase);
		}

		internal static string GetVersion()
		{
			var entryAssembly = typeof(Program).Assembly;
			var informationalVersion =
				entryAssembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

			if (!string.IsNullOrWhiteSpace(informationalVersion))
			{
				return NormalizeVersion(informationalVersion);
			}

			var libraryAssembly = typeof(Compiler).Assembly;
			informationalVersion =
				libraryAssembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

			if (!string.IsNullOrWhiteSpace(informationalVersion))
			{
				return NormalizeVersion(informationalVersion);
			}

			var assemblyVersion = entryAssembly.GetName().Version?.ToString();

			if (!string.IsNullOrWhiteSpace(assemblyVersion))
			{
				return NormalizeVersion(assemblyVersion);
			}

			throw new InvalidOperationException("Compiler version metadata is missing.");
		}

		internal static string NormalizeVersion(string version)
		{
			var trimmed = version.Trim();
			var plusIndex = trimmed.IndexOf('+');
			return plusIndex >= 0 ? trimmed[..plusIndex] : trimmed;
		}

		internal static void WriteVersion(TextWriter writer)
		{
			writer.WriteLine(GetVersion());
		}

		internal static void WriteHelp(TextWriter writer)
		{
			writer.WriteLine($"{ExecutableName} <options> <source-file>");
			writer.WriteLine();
			writer.WriteLine("Options:");
			writer.WriteLine("  --version, -v          Print compiler version");
			writer.WriteLine("  --help, -h             Show help");
			writer.WriteLine("  protocol:<number>      Target protocol version");
			writer.WriteLine("  output:<directory>     Output directory");
			writer.WriteLine("  libpath:<directory>    Additional library search path");
			writer.WriteLine("  debug                  Enable debug output artifacts");
			writer.WriteLine("  nativecheck:<mode>     Native/foundational interop checks (off|warn|error)");
		}
	}
}
