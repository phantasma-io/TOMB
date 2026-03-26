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
				NativeMethodPresence.Available,
				$"implemented in {sourceFile} (Runtime.Time dispatch)"),
			["Runtime.Version"] = new NativeMethodSnapshotEntry(
				NativeMethodPresence.Available,
				$"implemented in {sourceFile} (Runtime.Version dispatch)"),
			["Runtime.GasTarget"] = new NativeMethodSnapshotEntry(
				NativeMethodPresence.Available,
				$"implemented in {sourceFile} (Runtime.GasTarget dispatch)"),
			["Runtime.Validator"] = new NativeMethodSnapshotEntry(
				NativeMethodPresence.Missing,
				$"{sourceFile} Runtime.Validator case currently routes to runtime error (goto error)"),
			["Runtime.Context"] = new NativeMethodSnapshotEntry(
				NativeMethodPresence.Available,
				$"implemented in {sourceFile} (Runtime.Context dispatch)"),
			["Runtime.PreviousContext"] = new NativeMethodSnapshotEntry(
				NativeMethodPresence.Available,
				$"implemented in {sourceFile} (Runtime.PreviousContext dispatch)"),
			["Runtime.GenerateUID"] = new NativeMethodSnapshotEntry(
				NativeMethodPresence.Available,
				$"implemented in {sourceFile} (Runtime.GenerateUID dispatch)"),
			["Runtime.Random"] = new NativeMethodSnapshotEntry(
				NativeMethodPresence.Missing,
				$"{sourceFile} Runtime.Random case currently routes to runtime error (goto error)"),
			["Runtime.SetSeed"] = new NativeMethodSnapshotEntry(
				NativeMethodPresence.Missing,
				$"{sourceFile} Runtime.SetSeed case currently routes to runtime error (goto error)"),
			["Runtime.IsWitness"] = new NativeMethodSnapshotEntry(
				NativeMethodPresence.Available,
				$"implemented in {sourceFile} (Runtime.IsWitness dispatch)"),
			["Runtime.IsTrigger"] = new NativeMethodSnapshotEntry(
				NativeMethodPresence.Available,
				$"implemented in {sourceFile} (Runtime.IsTrigger dispatch)"),
			["Runtime.IsMinter"] = new NativeMethodSnapshotEntry(
				NativeMethodPresence.Missing,
				$"{sourceFile} Runtime.IsMinter case currently routes to runtime error (goto error)"),
			["Runtime.Log"] = new NativeMethodSnapshotEntry(
				NativeMethodPresence.Available,
				$"implemented in {sourceFile} (Runtime.Log dispatch)"),
			["Runtime.Notify"] = new NativeMethodSnapshotEntry(
				NativeMethodPresence.Available,
				$"implemented in {sourceFile} (Runtime.Notify dispatch)"),
			["Runtime.DeployContract"] = new NativeMethodSnapshotEntry(
				NativeMethodPresence.Available,
				$"implemented in {sourceFile} (Runtime.DeployContract dispatch)"),
			["Runtime.UpgradeContract"] = new NativeMethodSnapshotEntry(
				NativeMethodPresence.Available,
				$"implemented in {sourceFile} (Runtime.UpgradeContract dispatch)"),
			["Runtime.KillContract"] = new NativeMethodSnapshotEntry(
				NativeMethodPresence.Missing,
				$"{sourceFile} Runtime.KillContract case currently routes to runtime error (goto error)"),
			["Runtime.ContractExists"] = new NativeMethodSnapshotEntry(
				NativeMethodPresence.Missing,
				$"{sourceFile} has no Runtime.ContractExists switch case"),
			["Runtime.GetBalance"] = new NativeMethodSnapshotEntry(
				NativeMethodPresence.Available,
				$"implemented in {sourceFile} (Runtime.GetBalance dispatch)"),
			["Runtime.TransferTokens"] = new NativeMethodSnapshotEntry(
				NativeMethodPresence.Available,
				$"implemented in {sourceFile} (Runtime.TransferTokens dispatch)"),
			["Runtime.TransferBalance"] = new NativeMethodSnapshotEntry(
				NativeMethodPresence.Available,
				$"implemented in {sourceFile} (Runtime.TransferBalance dispatch)"),
			["Runtime.MintTokens"] = new NativeMethodSnapshotEntry(
				NativeMethodPresence.Available,
				$"implemented in {sourceFile} (Runtime.MintTokens dispatch)"),
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
				NativeMethodPresence.Available,
				$"implemented in {sourceFile} (Runtime.MintToken dispatch)"),
			["Runtime.BurnToken"] = new NativeMethodSnapshotEntry(
				NativeMethodPresence.Available,
				$"implemented in {sourceFile} (Runtime.BurnToken dispatch)"),
			["Runtime.InfuseToken"] = new NativeMethodSnapshotEntry(
				NativeMethodPresence.Available,
				$"implemented in {sourceFile} (Runtime.InfuseToken dispatch)"),
			["Runtime.ReadTokenROM"] = new NativeMethodSnapshotEntry(
				NativeMethodPresence.Available,
				$"implemented in {sourceFile} (Runtime.ReadTokenROM dispatch)"),
			["Runtime.ReadTokenRAM"] = new NativeMethodSnapshotEntry(
				NativeMethodPresence.Available,
				$"implemented in {sourceFile} (Runtime.ReadTokenRAM dispatch)"),
			["Runtime.ReadToken"] = new NativeMethodSnapshotEntry(
				NativeMethodPresence.Available,
				$"implemented in {sourceFile} (Runtime.ReadToken dispatch)"),
			["Runtime.WriteToken"] = new NativeMethodSnapshotEntry(
				NativeMethodPresence.Missing,
				$"{sourceFile} Runtime.WriteToken case currently routes to runtime error (goto error)"),
			["Runtime.TokenExists"] = new NativeMethodSnapshotEntry(
				NativeMethodPresence.Available,
				$"implemented in {sourceFile} (Runtime.TokenExists dispatch)"),
			["Runtime.GetTokenDecimals"] = new NativeMethodSnapshotEntry(
				NativeMethodPresence.Available,
				$"implemented in {sourceFile} (Runtime.GetTokenDecimals dispatch)"),
			["Runtime.GetTokenFlags"] = new NativeMethodSnapshotEntry(
				NativeMethodPresence.Available,
				$"implemented in {sourceFile} (Runtime.GetTokenFlags dispatch)"),
			["Runtime.GetTokenOwner"] = new NativeMethodSnapshotEntry(
				NativeMethodPresence.Available,
				$"implemented in {sourceFile} (Runtime.GetTokenOwner dispatch)"),
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
				NativeMethodPresence.Available,
				$"implemented in {sourceFile} (Runtime.ReadInfusions dispatch)"),
			["Runtime.GetOwnerships"] = new NativeMethodSnapshotEntry(
				NativeMethodPresence.Available,
				$"implemented in {sourceFile} (Runtime.GetOwnerships dispatch)"),

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
				NativeMethodPresence.Available,
				$"implemented in {sourceFile} (Nexus.CreateTokenSeries dispatch)"),
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
				NativeMethodPresence.Available,
				$"implemented in {sourceFile} (Account.Name dispatch)"),
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
				NativeMethodPresence.Available,
				$"implemented in {sourceFile} (Data.Set case writes current-contract storage)"),
			["Data.Delete"] = new NativeMethodSnapshotEntry(
				NativeMethodPresence.Available,
				$"implemented in {sourceFile} (Data.Delete case removes current-contract storage)"),

			["Map.Has"] = new NativeMethodSnapshotEntry(
				NativeMethodPresence.Available,
				$"implemented in {sourceFile} (Map.Has case checks contract table presence)"),
			["Map.Get"] = new NativeMethodSnapshotEntry(
				NativeMethodPresence.Available,
				$"implemented in {sourceFile} (Map.Get case reads contract table entry)"),
			["Map.Set"] = new NativeMethodSnapshotEntry(
				NativeMethodPresence.Available,
				$"implemented in {sourceFile} (Map.Set case writes contract table entry)"),
			["Map.Remove"] = new NativeMethodSnapshotEntry(
				NativeMethodPresence.Available,
				$"implemented in {sourceFile} (Map.Remove case deletes contract table entry)"),
			["Map.Count"] = new NativeMethodSnapshotEntry(
				NativeMethodPresence.Available,
				$"implemented in {sourceFile} (Map.Count case reads canonical table count)"),
			["Map.Clear"] = new NativeMethodSnapshotEntry(
				NativeMethodPresence.Available,
				$"implemented in {sourceFile} (Map.Clear case deletes all contract table entries)"),
			["Map.Keys"] = new NativeMethodSnapshotEntry(
				NativeMethodPresence.Available,
				$"implemented in {sourceFile} (Map.Keys case enumerates contract table keys)"),

			["List.Get"] = new NativeMethodSnapshotEntry(
				NativeMethodPresence.Available,
				$"implemented in {sourceFile} (List.Get case reads contract table entry by index)"),
			["List.Add"] = new NativeMethodSnapshotEntry(
				NativeMethodPresence.Available,
				$"implemented in {sourceFile} (List.Add case appends list entry)"),
			["List.Replace"] = new NativeMethodSnapshotEntry(
				NativeMethodPresence.Available,
				$"implemented in {sourceFile} (List.Replace case rewrites list entry)"),
			["List.RemoveAt"] = new NativeMethodSnapshotEntry(
				NativeMethodPresence.Available,
				$"implemented in {sourceFile} (List.RemoveAt case removes list entry)"),
			["List.Count"] = new NativeMethodSnapshotEntry(
				NativeMethodPresence.Available,
				$"implemented in {sourceFile} (List.Count case reads canonical list count)"),
			["List.Clear"] = new NativeMethodSnapshotEntry(
				NativeMethodPresence.Available,
				$"implemented in {sourceFile} (List.Clear case deletes list entries)"),
		};
	}
}
