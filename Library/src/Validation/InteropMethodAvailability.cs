using Phantasma.Tomb.AST;
using Phantasma.Tomb.AST.Expressions;

namespace Phantasma.Tomb.Validation;

public static class InteropMethodAvailability
{
	// Fail-closed policy: every extcall alias used under interop checks must have an
	// explicit status in this snapshot for the pinned chain commit.
	private static readonly IReadOnlyDictionary<string, NativeMethodSnapshotEntry> Snapshot = BuildSnapshot();

	// Warn mode should emit each method warning once per compilation run.
	private static readonly HashSet<string> EmittedWarnings = new(StringComparer.Ordinal);

	public static void ResetSession()
	{
		EmittedWarnings.Clear();
	}

	public static bool TryGetSnapshotStatus(string alias, out NativeMethodSnapshotEntry entry)
	{
		return Snapshot.TryGetValue(alias, out entry);
	}

	public static void ValidateOrReport(MethodCallExpression call, MethodInterface method, NativeCheckMode mode)
	{
		if (mode == NativeCheckMode.Off)
		{
			return;
		}

		if (!TryBuildDiagnostic(call, method, out var key, out var message))
		{
			return;
		}

		switch (mode)
		{
			case NativeCheckMode.Warn:
				if (EmittedWarnings.Add(key))
				{
					Compiler.EmitWarning(call, message);
				}
				break;

			case NativeCheckMode.Error:
				throw new CompilerException(call, message);
		}
	}

	private static bool TryBuildDiagnostic(
		MethodCallExpression call,
		MethodInterface method,
		out string key,
		out string message)
	{
		key = string.Empty;
		message = string.Empty;

		// Custom methods without callbacks are known placeholders:
		// code generation falls back to an explicit THROW block.
		if (method.Implementation == MethodImplementationType.Custom &&
			method.PreCallback == null &&
			method.PostCallback == null)
		{
			key = $"custom:{method.Library.Name}.{method.Name}";
			message = $"method '{method.Library.Name}.{method.Name}' has no compile-time implementation in TOMB and emits runtime THROW; " +
					  $"unavailable for chain commit {NativeMethodAvailability.ChainBaselineCommit} " +
					  $"(baseline date {NativeMethodAvailability.ChainBaselineDate})";
			return true;
		}

		if (method.Implementation != MethodImplementationType.ExtCall)
		{
			return false;
		}

		if (!TryResolveInteropAlias(call, method, out var alias))
		{
			key = $"{method.Library.Name}.{method.Name}:{call.NodeID}";
			message = $"interop target for '{method.Library.Name}.{method.Name}' is not a literal method name, so availability " +
					  $"cannot be validated for chain commit {NativeMethodAvailability.ChainBaselineCommit} " +
					  $"(baseline date {NativeMethodAvailability.ChainBaselineDate})";
			return true;
		}

		if (string.IsNullOrWhiteSpace(alias))
		{
			return false;
		}

		key = alias;

		if (!Snapshot.TryGetValue(alias, out var status))
		{
			message = $"interop method '{alias}' has no explicit status in interop snapshot for chain commit " +
					  $"{NativeMethodAvailability.ChainBaselineCommit} (baseline date {NativeMethodAvailability.ChainBaselineDate}); " +
					  "add an explicit Available/Missing entry before compiling (fail-closed policy)";
			return true;
		}

		if (status.Presence == NativeMethodPresence.Missing)
		{
			message = $"interop method '{alias}' is unavailable in chain commit {NativeMethodAvailability.ChainBaselineCommit} " +
					  $"(baseline date {NativeMethodAvailability.ChainBaselineDate}); {status.Evidence}";
			return true;
		}

		return false;
	}

	private static bool TryResolveInteropAlias(
		MethodCallExpression call,
		MethodInterface method,
		out string alias)
	{
		alias = method.Alias;

		// Call.interop(methodName, ...) forwards to an arbitrary interop method,
		// so we can validate only when the method name is a literal string.
		if (string.Equals(method.Library.Name, "Call", StringComparison.Ordinal) &&
			string.Equals(method.Name, "interop", StringComparison.Ordinal))
		{
			if (call.arguments.Count == 0)
			{
				return false;
			}

			try
			{
				alias = NormalizeStringLiteral(call.arguments[0].AsLiteral<string>());
			}
			catch (CompilerException)
			{
				return false;
			}
		}

		return true;
	}

	private static string NormalizeStringLiteral(string value)
	{
		if (string.IsNullOrEmpty(value))
		{
			return value;
		}

		if (value.Length >= 2 && value[0] == '"' && value[^1] == '"')
		{
			return value.Substring(1, value.Length - 2);
		}

		return value;
	}

	private static IReadOnlyDictionary<string, NativeMethodSnapshotEntry> BuildSnapshot()
	{
		const string sourceFile = "node_cpp/src/carbon/contracts/phantasma/phantasma_interop.cpp";

		return new Dictionary<string, NativeMethodSnapshotEntry>(StringComparer.Ordinal)
		{
			["Runtime.TransactionHash"] = new NativeMethodSnapshotEntry(
				NativeMethodPresence.Missing,
				$"{sourceFile} Runtime.TransactionHash case currently routes to runtime error (goto error)"),
			["Runtime.Time"] = new NativeMethodSnapshotEntry(
				NativeMethodPresence.Missing,
				$"{sourceFile} Runtime.Time case currently routes to runtime error (goto error)"),
			["Runtime.Version"] = new NativeMethodSnapshotEntry(
				NativeMethodPresence.Missing,
				$"{sourceFile} Runtime.Version case currently routes to runtime error (goto error)"),
			["Runtime.GasTarget"] = new NativeMethodSnapshotEntry(
				NativeMethodPresence.Missing,
				$"{sourceFile} Runtime.GasTarget case currently routes to runtime error (goto error)"),
			["Runtime.Validator"] = new NativeMethodSnapshotEntry(
				NativeMethodPresence.Missing,
				$"{sourceFile} Runtime.Validator case currently routes to runtime error (goto error)"),
			["Runtime.Context"] = new NativeMethodSnapshotEntry(
				NativeMethodPresence.Missing,
				$"{sourceFile} Runtime.Context case currently routes to runtime error (goto error)"),
			["Runtime.PreviousContext"] = new NativeMethodSnapshotEntry(
				NativeMethodPresence.Missing,
				$"{sourceFile} Runtime.PreviousContext case currently routes to runtime error (goto error)"),
			["Runtime.GenerateUID"] = new NativeMethodSnapshotEntry(
				NativeMethodPresence.Missing,
				$"{sourceFile} Runtime.GenerateUID case currently routes to runtime error (goto error)"),
			["Runtime.Random"] = new NativeMethodSnapshotEntry(
				NativeMethodPresence.Missing,
				$"{sourceFile} Runtime.Random case currently routes to runtime error (goto error)"),
			["Runtime.SetSeed"] = new NativeMethodSnapshotEntry(
				NativeMethodPresence.Missing,
				$"{sourceFile} Runtime.SetSeed case currently routes to runtime error (goto error)"),
			["Runtime.IsWitness"] = new NativeMethodSnapshotEntry(
				NativeMethodPresence.Missing,
				$"{sourceFile} Runtime.IsWitness case currently routes to runtime error (goto error)"),
			["Runtime.IsTrigger"] = new NativeMethodSnapshotEntry(
				NativeMethodPresence.Missing,
				$"{sourceFile} Runtime.IsTrigger case currently routes to runtime error (goto error)"),
			["Runtime.IsMinter"] = new NativeMethodSnapshotEntry(
				NativeMethodPresence.Missing,
				$"{sourceFile} Runtime.IsMinter case currently routes to runtime error (goto error)"),
			["Runtime.Log"] = new NativeMethodSnapshotEntry(
				NativeMethodPresence.Missing,
				$"{sourceFile} Runtime.Log case currently routes to runtime error (goto error)"),
			["Runtime.Notify"] = new NativeMethodSnapshotEntry(
				NativeMethodPresence.Missing,
				$"{sourceFile} Runtime.Notify case currently routes to runtime error (goto error)"),
			["Runtime.DeployContract"] = new NativeMethodSnapshotEntry(
				NativeMethodPresence.Available,
				$"implemented in {sourceFile} (Runtime.DeployContract dispatch)"),
			["Runtime.UpgradeContract"] = new NativeMethodSnapshotEntry(
				NativeMethodPresence.Available,
				$"implemented in {sourceFile} (Runtime.UpgradeContract dispatch)"),
			["Runtime.KillContract"] = new NativeMethodSnapshotEntry(
				NativeMethodPresence.Missing,
				$"{sourceFile} Runtime.KillContract case currently routes to runtime error (goto error)"),
			["Runtime.GetBalance"] = new NativeMethodSnapshotEntry(
				NativeMethodPresence.Missing,
				$"{sourceFile} Runtime.GetBalance case currently routes to runtime error (goto error)"),
			["Runtime.TransferTokens"] = new NativeMethodSnapshotEntry(
				NativeMethodPresence.Available,
				$"implemented in {sourceFile} (Runtime.TransferTokens dispatch)"),
			["Runtime.TransferBalance"] = new NativeMethodSnapshotEntry(
				NativeMethodPresence.Missing,
				$"{sourceFile} Runtime.TransferBalance case currently routes to runtime error (goto error)"),
			["Runtime.MintTokens"] = new NativeMethodSnapshotEntry(
				NativeMethodPresence.Missing,
				$"{sourceFile} Runtime.MintTokens case currently routes to runtime error (goto error)"),
			["Runtime.BurnTokens"] = new NativeMethodSnapshotEntry(
				NativeMethodPresence.Available,
				$"implemented in {sourceFile} (Runtime.BurnTokens dispatch)"),
			["Runtime.SwapTokens"] = new NativeMethodSnapshotEntry(
				NativeMethodPresence.Missing,
				$"{sourceFile} Runtime.SwapTokens case currently routes to runtime error (goto error)"),
			["Runtime.TransferToken"] = new NativeMethodSnapshotEntry(
				NativeMethodPresence.Available,
				$"implemented in {sourceFile} (Runtime.TransferToken dispatch)"),
			["Runtime.MintToken"] = new NativeMethodSnapshotEntry(
				NativeMethodPresence.Missing,
				$"{sourceFile} Runtime.MintToken case currently routes to runtime error (goto error)"),
			["Runtime.BurnToken"] = new NativeMethodSnapshotEntry(
				NativeMethodPresence.Missing,
				$"{sourceFile} Runtime.BurnToken case currently routes to runtime error (goto error)"),
			["Runtime.InfuseToken"] = new NativeMethodSnapshotEntry(
				NativeMethodPresence.Available,
				$"implemented in {sourceFile} (Runtime.InfuseToken dispatch)"),
			["Runtime.ReadTokenROM"] = new NativeMethodSnapshotEntry(
				NativeMethodPresence.Missing,
				$"{sourceFile} Runtime.ReadTokenROM case currently routes to runtime error (goto error)"),
			["Runtime.ReadTokenRAM"] = new NativeMethodSnapshotEntry(
				NativeMethodPresence.Missing,
				$"{sourceFile} Runtime.ReadTokenRAM case currently routes to runtime error (goto error)"),
			["Runtime.ReadToken"] = new NativeMethodSnapshotEntry(
				NativeMethodPresence.Missing,
				$"{sourceFile} Runtime.ReadToken case currently routes to runtime error (goto error)"),
			["Runtime.WriteToken"] = new NativeMethodSnapshotEntry(
				NativeMethodPresence.Missing,
				$"{sourceFile} Runtime.WriteToken case currently routes to runtime error (goto error)"),
			["Runtime.TokenExists"] = new NativeMethodSnapshotEntry(
				NativeMethodPresence.Missing,
				$"{sourceFile} Runtime.TokenExists case currently routes to runtime error (goto error)"),
			["Runtime.GetTokenDecimals"] = new NativeMethodSnapshotEntry(
				NativeMethodPresence.Missing,
				$"{sourceFile} Runtime.GetTokenDecimals case currently routes to runtime error (goto error)"),
			["Runtime.GetTokenFlags"] = new NativeMethodSnapshotEntry(
				NativeMethodPresence.Missing,
				$"{sourceFile} Runtime.GetTokenFlags case currently routes to runtime error (goto error)"),
			["Runtime.AESDecrypt"] = new NativeMethodSnapshotEntry(
				NativeMethodPresence.Missing,
				$"{sourceFile} Runtime.AESDecrypt case currently routes to runtime error (goto error)"),
			["Runtime.AESEncrypt"] = new NativeMethodSnapshotEntry(
				NativeMethodPresence.Missing,
				$"{sourceFile} Runtime.AESEncrypt case currently routes to runtime error (goto error)"),
			["Runtime.GetTokenSupply"] = new NativeMethodSnapshotEntry(
				NativeMethodPresence.Missing,
				$"{sourceFile} has no Runtime.GetTokenSupply switch case"),
			["Runtime.GetAvailableTokenSymbols"] = new NativeMethodSnapshotEntry(
				NativeMethodPresence.Missing,
				$"{sourceFile} has no Runtime.GetAvailableTokenSymbols switch case"),
			["Runtime.GetAvailableNFTSymbols"] = new NativeMethodSnapshotEntry(
				NativeMethodPresence.Missing,
				$"{sourceFile} has no Runtime.GetAvailableNFTSymbols switch case"),
			["Runtime.ReadInfusions"] = new NativeMethodSnapshotEntry(
				NativeMethodPresence.Missing,
				$"{sourceFile} has no Runtime.ReadInfusions switch case"),
			["Runtime.GetOwnerships"] = new NativeMethodSnapshotEntry(
				NativeMethodPresence.Missing,
				$"{sourceFile} has no Runtime.GetOwnerships switch case"),

			["Nexus.CreateToken"] = new NativeMethodSnapshotEntry(
				NativeMethodPresence.Available,
				$"implemented in {sourceFile} (Nexus.CreateToken dispatch)"),
			["Nexus.GetGovernanceValue"] = new NativeMethodSnapshotEntry(
				NativeMethodPresence.Missing,
				$"{sourceFile} Nexus.GetGovernanceValue case currently routes to runtime error (goto error)"),
			["Nexus.BeginInit"] = new NativeMethodSnapshotEntry(
				NativeMethodPresence.Missing,
				$"{sourceFile} Nexus.BeginInit case currently routes to runtime error (goto error)"),
			["Nexus.EndInit"] = new NativeMethodSnapshotEntry(
				NativeMethodPresence.Missing,
				$"{sourceFile} Nexus.EndInit case currently routes to runtime error (goto error)"),
			["Nexus.MigrateToken"] = new NativeMethodSnapshotEntry(
				NativeMethodPresence.Missing,
				$"{sourceFile} Nexus.MigrateToken case currently routes to runtime error (goto error)"),
			["Nexus.CreateTokenSeries"] = new NativeMethodSnapshotEntry(
				NativeMethodPresence.Missing,
				$"{sourceFile} Nexus.CreateTokenSeries case currently routes to runtime error (goto error)"),
			["Nexus.CreateChain"] = new NativeMethodSnapshotEntry(
				NativeMethodPresence.Missing,
				$"{sourceFile} Nexus.CreateChain case currently routes to runtime error (goto error)"),
			["Nexus.CreatePlatform"] = new NativeMethodSnapshotEntry(
				NativeMethodPresence.Missing,
				$"{sourceFile} Nexus.CreatePlatform case currently routes to runtime error (goto error)"),
			["Nexus.CreateOrganization"] = new NativeMethodSnapshotEntry(
				NativeMethodPresence.Missing,
				$"{sourceFile} Nexus.CreateOrganization case currently routes to runtime error (goto error)"),
			["Nexus.SetPlatformTokenHash"] = new NativeMethodSnapshotEntry(
				NativeMethodPresence.Missing,
				$"{sourceFile} Nexus.SetPlatformTokenHash case currently routes to runtime error (goto error)"),
			["Nexus.SetTokenPlatformHash"] = new NativeMethodSnapshotEntry(
				NativeMethodPresence.Missing,
				$"{sourceFile} has Nexus.SetPlatformTokenHash case, but no Nexus.SetTokenPlatformHash case"),

			["Organization.AddMember"] = new NativeMethodSnapshotEntry(
				NativeMethodPresence.Missing,
				$"{sourceFile} Organization.AddMember case currently routes to runtime error (goto error)"),
			["Organization.RemoveMember"] = new NativeMethodSnapshotEntry(
				NativeMethodPresence.Missing,
				$"{sourceFile} Organization.RemoveMember case currently routes to runtime error (goto error)"),
			["Organization.Kill"] = new NativeMethodSnapshotEntry(
				NativeMethodPresence.Missing,
				$"{sourceFile} Organization.Kill case currently routes to runtime error (goto error)"),

			["Task.Start"] = new NativeMethodSnapshotEntry(
				NativeMethodPresence.Missing,
				$"{sourceFile} Task.Start case currently routes to runtime error (goto error)"),
			["Task.Stop"] = new NativeMethodSnapshotEntry(
				NativeMethodPresence.Missing,
				$"{sourceFile} Task.Stop case currently routes to runtime error (goto error)"),
			["Task.Get"] = new NativeMethodSnapshotEntry(
				NativeMethodPresence.Missing,
				$"{sourceFile} Task.Get case currently routes to runtime error (goto error)"),
			["Task.Current"] = new NativeMethodSnapshotEntry(
				NativeMethodPresence.Missing,
				$"{sourceFile} Task.Current case currently routes to runtime error (goto error)"),

			["Account.Name"] = new NativeMethodSnapshotEntry(
				NativeMethodPresence.Missing,
				$"{sourceFile} Account.Name case currently routes to runtime error (goto error)"),
			["Account.LastActivity"] = new NativeMethodSnapshotEntry(
				NativeMethodPresence.Missing,
				$"{sourceFile} Account.LastActivity case currently routes to runtime error (goto error)"),
			["Account.Transactions"] = new NativeMethodSnapshotEntry(
				NativeMethodPresence.Missing,
				$"{sourceFile} Account.Transactions case currently routes to runtime error (goto error)"),

			["Oracle.Read"] = new NativeMethodSnapshotEntry(
				NativeMethodPresence.Missing,
				$"{sourceFile} Oracle.Read case currently routes to runtime error (goto error)"),
			["Oracle.Price"] = new NativeMethodSnapshotEntry(
				NativeMethodPresence.Missing,
				$"{sourceFile} Oracle.Price case currently routes to runtime error (goto error)"),
			["Oracle.Quote"] = new NativeMethodSnapshotEntry(
				NativeMethodPresence.Missing,
				$"{sourceFile} Oracle.Quote case currently routes to runtime error (goto error)"),

			["ABI()"] = new NativeMethodSnapshotEntry(
				NativeMethodPresence.Missing,
				$"{sourceFile} ABI() case currently routes to runtime error (goto error)"),
			["Address()"] = new NativeMethodSnapshotEntry(
				NativeMethodPresence.Available,
				$"implemented in {sourceFile} (Address() constructor dispatch)"),
			["Hash()"] = new NativeMethodSnapshotEntry(
				NativeMethodPresence.Available,
				$"implemented in {sourceFile} (Hash() constructor dispatch)"),
			["Timestamp()"] = new NativeMethodSnapshotEntry(
				NativeMethodPresence.Available,
				$"implemented in {sourceFile} (Timestamp() constructor dispatch)"),

			["Data.Get"] = new NativeMethodSnapshotEntry(
				NativeMethodPresence.Available,
				$"implemented in {sourceFile} (Data.Get case reads contract variable)"),
			["Data.Set"] = new NativeMethodSnapshotEntry(
				NativeMethodPresence.Missing,
				$"{sourceFile} Data.Set case currently routes to runtime error (goto error)"),
			["Data.Delete"] = new NativeMethodSnapshotEntry(
				NativeMethodPresence.Missing,
				$"{sourceFile} Data.Delete case currently routes to runtime error (goto error)"),

			["Map.Has"] = new NativeMethodSnapshotEntry(
				NativeMethodPresence.Missing,
				$"{sourceFile} Map.Has case currently aborts with CarbonAssert(false)"),
			["Map.Get"] = new NativeMethodSnapshotEntry(
				NativeMethodPresence.Available,
				$"implemented in {sourceFile} (Map.Get case reads contract table entry)"),
			["Map.Set"] = new NativeMethodSnapshotEntry(
				NativeMethodPresence.Missing,
				$"{sourceFile} Map.Set case currently routes to runtime error (goto error)"),
			["Map.Remove"] = new NativeMethodSnapshotEntry(
				NativeMethodPresence.Missing,
				$"{sourceFile} Map.Remove case currently routes to runtime error (goto error)"),
			["Map.Count"] = new NativeMethodSnapshotEntry(
				NativeMethodPresence.Missing,
				$"{sourceFile} Map.Count case currently aborts with CarbonAssert(false)"),
			["Map.Clear"] = new NativeMethodSnapshotEntry(
				NativeMethodPresence.Missing,
				$"{sourceFile} Map.Clear case currently routes to runtime error (goto error)"),
			["Map.Keys"] = new NativeMethodSnapshotEntry(
				NativeMethodPresence.Missing,
				$"{sourceFile} Map.Keys case currently aborts with CarbonAssert(false)"),

			["List.Get"] = new NativeMethodSnapshotEntry(
				NativeMethodPresence.Available,
				$"implemented in {sourceFile} (List.Get case reads contract table entry by index)"),
			["List.Add"] = new NativeMethodSnapshotEntry(
				NativeMethodPresence.Missing,
				$"{sourceFile} List.Add case currently routes to runtime error (goto error)"),
			["List.Replace"] = new NativeMethodSnapshotEntry(
				NativeMethodPresence.Missing,
				$"{sourceFile} List.Replace case currently routes to runtime error (goto error)"),
			["List.RemoveAt"] = new NativeMethodSnapshotEntry(
				NativeMethodPresence.Missing,
				$"{sourceFile} List.RemoveAt case currently routes to runtime error (goto error)"),
			["List.Count"] = new NativeMethodSnapshotEntry(
				NativeMethodPresence.Missing,
				$"{sourceFile} List.Count case currently aborts with CarbonAssert(false)"),
			["List.Clear"] = new NativeMethodSnapshotEntry(
				NativeMethodPresence.Missing,
				$"{sourceFile} List.Clear case currently routes to runtime error (goto error)"),
		};
	}
}
