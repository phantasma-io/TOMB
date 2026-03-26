using Phantasma.Tomb.AST;
using Phantasma.Tomb.AST.Expressions;

namespace Phantasma.Tomb.Validation;

public enum NativeMethodPresence
{
	Available,
	Missing,
}

public readonly record struct NativeMethodSnapshotEntry(NativeMethodPresence Presence, string Evidence);

public enum NativeCheckMode
{
	Off,
	Warn,
	Error,
}

public static class NativeMethodAvailability
{
	// Baseline commit used for method-by-method native runtime availability.
	// Update this hash and the snapshot table together after reviewing a newer chain revision.
	public const string ChainBaselineCommit = "d5944305736449aa417be5be8898e05bdb865dac";
	public const string ChainBaselineDate = "2026-03-26";

	private static readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, NativeMethodSnapshotEntry>> Snapshot =
		BuildSnapshot();
	private static readonly IReadOnlySet<string> KnownRuntimeNativeContracts =
		BuildKnownRuntimeNativeContracts();

	// Warnings are deduplicated per method so the same issue is reported once per compile run.
	private static readonly HashSet<string> EmittedWarnings = new(StringComparer.Ordinal);

	public static void ResetSession()
	{
		EmittedWarnings.Clear();
	}

	public static bool IsTrackedNativeContract(string contractName)
	{
		return Snapshot.ContainsKey(contractName);
	}

	public static bool TryGetSnapshotStatus(string contractName, string methodName, out NativeMethodSnapshotEntry entry)
	{
		entry = default;
		if (!Snapshot.TryGetValue(contractName, out var methods))
		{
			return false;
		}

		return methods.TryGetValue(methodName, out entry);
	}

	public static void ValidateOrReport(MethodCallExpression call, MethodInterface method, NativeCheckMode mode)
	{
		if (mode == NativeCheckMode.Off || method.Implementation != MethodImplementationType.ContractCall)
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

		var contract = method.Contract;
		var alias = method.Alias;

		// Call.contract(...) and Contract.call(...) forward to a runtime-selected target.
		// In strict nativecheck modes we require literal contract/method strings so the
		// compiler can validate against the pinned native snapshot and avoid silent bypass.
		if (IsDynamicContractGateway(method))
		{
			if (!TryResolveDynamicCallTarget(call, out contract, out alias))
			{
				key = $"{method.Library.Name}.{method.Name}:{call.NodeID}";
				message = $"native method target for '{method.Library.Name}.{method.Name}' is not a literal " +
						  $"contract/method pair, so availability cannot be validated for chain commit {ChainBaselineCommit} " +
						  $"(baseline date {ChainBaselineDate}); use literal strings for this gateway";
				return true;
			}

			// Dynamic contract gateways can target either builtin lower-case runtime namespaces
			// (stake/account/exchange/...) or regular deployed custom contracts/ticker contracts.
			// Only the builtin namespace family should be validated against the native snapshot;
			// literal custom targets like "SATRN" must not be rejected as if they were native.
			if (!IsKnownRuntimeNativeContract(contract))
			{
				return false;
			}
		}

		if (string.IsNullOrWhiteSpace(contract))
		{
			return false;
		}

		if (!Snapshot.TryGetValue(contract, out var contractMethods))
		{
			key = $"{contract}.{alias}";
			message = $"native contract '{contract}' has no explicit status table in snapshot for chain commit {ChainBaselineCommit} " +
					  $"(baseline date {ChainBaselineDate}); add contract-level method entries before compiling";
			return true;
		}

		if (string.IsNullOrWhiteSpace(alias))
		{
			key = $"{contract}.<empty>";
			message = $"native method alias is empty for contract '{contract}' in snapshot check for chain commit {ChainBaselineCommit} " +
					  $"(baseline date {ChainBaselineDate}); assign a concrete method alias before compiling";
			return true;
		}

		key = $"{contract}.{alias}";

		if (!contractMethods.TryGetValue(alias, out var status))
		{
			message = $"native method '{key}' has no explicit status in native snapshot for chain commit {ChainBaselineCommit} " +
					  $"(baseline date {ChainBaselineDate}); add an explicit Available/Missing entry before compiling";
			return true;
		}

		if (status.Presence == NativeMethodPresence.Missing)
		{
			message = $"native method '{key}' is unavailable in chain commit {ChainBaselineCommit} (baseline date {ChainBaselineDate}); " +
					  status.Evidence;
			return true;
		}

		return false;
	}

	private static bool IsDynamicContractGateway(MethodInterface method)
	{
		return (string.Equals(method.Library.Name, "Call", StringComparison.Ordinal) &&
				string.Equals(method.Name, "contract", StringComparison.Ordinal)) ||
			   (string.Equals(method.Library.Name, "Contract", StringComparison.Ordinal) &&
				string.Equals(method.Name, "call", StringComparison.Ordinal));
	}

	private static bool TryResolveDynamicCallTarget(
		MethodCallExpression call,
		out string contract,
		out string methodName)
	{
		contract = string.Empty;
		methodName = string.Empty;

		if (call.arguments.Count < 2)
		{
			return false;
		}

		try
		{
			contract = NormalizeStringLiteral(call.arguments[0].AsLiteral<string>());
			methodName = NormalizeStringLiteral(call.arguments[1].AsLiteral<string>());
		}
		catch (CompilerException)
		{
			return false;
		}

		return !string.IsNullOrWhiteSpace(contract) && !string.IsNullOrWhiteSpace(methodName);
	}

	private static bool IsKnownRuntimeNativeContract(string contract)
	{
		return !string.IsNullOrWhiteSpace(contract) && KnownRuntimeNativeContracts.Contains(contract);
	}

	private static string NormalizeStringLiteral(string value)
	{
		if (string.IsNullOrEmpty(value))
		{
			return value;
		}

		// Tomb literals keep quote delimiters in AST string tokens; remove only matching
		// outer quotes so snapshot matching uses raw contract/method names.
		if (value.Length >= 2 && value[0] == '"' && value[^1] == '"')
		{
			return value.Substring(1, value.Length - 2);
		}

		return value;
	}

	private static IReadOnlyDictionary<string, IReadOnlyDictionary<string, NativeMethodSnapshotEntry>> BuildSnapshot()
	{
		var snapshot = new Dictionary<string, IReadOnlyDictionary<string, NativeMethodSnapshotEntry>>(StringComparer.Ordinal)
		{
			["account"] = CreateContractTable(
				allMissingReason: "runtime load-context for contract 'account' rejects calls in node_cpp/src/carbon/contracts/phantasma/phantasma_runtime.cpp",
				missingMethods: new[]
				{
					"GetTriggersForABI",
					"HasScript",
					"LookUpABI",
					"LookUpAddress",
					"LookUpName",
					"LookUpScript",
					"Migrate",
					"RegisterName",
					"RegisterScript",
					"UnregisterName",
				}),

			["governance"] = CreateContractTable(
				allMissingReason: "runtime load-context for contract 'governance' rejects calls in node_cpp/src/carbon/contracts/phantasma/phantasma_runtime.cpp",
				missingMethods: new[]
				{
					"CreateValue",
					"GetNames",
					"GetValue",
					"GetValues",
					"HasName",
					"HasValue",
					"SetValue",
				}),

			["mail"] = CreateContractTable(
				allMissingReason: "runtime load-context for contract 'mail' rejects calls in node_cpp/src/carbon/contracts/phantasma/phantasma_runtime.cpp",
				missingMethods: new[]
				{
					"DomainExists",
					"GetDomainUsers",
					"GetUserDomain",
					"JoinDomain",
					"LeaveDomain",
					"MigrateDomain",
					"PushMessage",
					"RegisterDomain",
					"UnregisterDomain",
				}),

			["market"] = CreateContractTable(
				allMissingReason: "runtime load-context for contract 'market' rejects calls in node_cpp/src/carbon/contracts/phantasma/phantasma_runtime.cpp",
				missingMethods: new[]
				{
					"BidToken",
					"BuyToken",
					"CancelSale",
					"EditAuction",
					"GetAuction",
					"GetAuctions",
					"HasAuction",
					"ListToken",
					"SellToken",
				}),

			["ranking"] = CreateContractTable(
				allMissingReason: "runtime load-context for contract 'ranking' rejects calls in node_cpp/src/carbon/contracts/phantasma/phantasma_runtime.cpp",
				missingMethods: new[]
				{
					"CreateLeaderboard",
					"Exists",
					"GetAddressByIndex",
					"GetScoreByAddress",
					"GetScoreByIndex",
					"GetSize",
					"InsertScore",
					"ResetLeaderboard",
				}),

			["relay"] = CreateContractTable(
				allMissingReason: "runtime load-context for contract 'relay' rejects calls in node_cpp/src/carbon/contracts/phantasma/phantasma_runtime.cpp",
				missingMethods: new[]
				{
					"GetBalance",
					"GetIndex",
					"GetKey",
					"GetTopUpAddress",
					"OpenChannel",
					"SettleChannel",
					"TopUpChannel",
				}),

			["sale"] = CreateContractTable(
				allMissingReason: "runtime load-context for contract 'sale' rejects calls in node_cpp/src/carbon/contracts/phantasma/phantasma_runtime.cpp",
				missingMethods: new[]
				{
					"AddToWhitelist",
					"CloseSale",
					"CreateSale",
					"EditSalePrice",
					"GetPurchasedAmount",
					"GetLatestSaleHash",
					"GetSale",
					"GetSaleParticipants",
					"GetSaleWhitelists",
					"GetSales",
					"GetSoldAmount",
					"IsSaleActive",
					"IsSeller",
					"IsWhitelisted",
					"Purchase",
					"RemoveFromWhitelist",
				}),

			["storage"] = CreateContractTable(
				allMissingReason: "runtime load-context for contract 'storage' rejects calls in node_cpp/src/carbon/contracts/phantasma/phantasma_runtime.cpp",
				missingMethods: new[]
				{
					"AddFile",
					"AddPermission",
					"CalculateStorageSizeForStake",
					"CreateFile",
					"DeleteData",
					"DeleteFile",
					"DeletePermission",
					"GetAvailableSpace",
					"GetFiles",
					"GetUsedDataQuota",
					"GetUsedSpace",
					"HasFile",
					"HasPermission",
					"Migrate",
					"MigratePermission",
					"WriteData",
				}),

			["stake"] = CreateStakeTable(),
		};

		return snapshot;
	}

	private static IReadOnlySet<string> BuildKnownRuntimeNativeContracts()
	{
		var contracts = new HashSet<string>(Snapshot.Keys, StringComparer.Ordinal)
		{
			"gas",
			"block",
			"swap",
			"token",
			"validator",
			"consensus",
			"interop",
			"exchange",
			"privacy",
			"friends",
		};

		return contracts;
	}

	private static IReadOnlyDictionary<string, NativeMethodSnapshotEntry> CreateContractTable(string allMissingReason, IEnumerable<string> missingMethods)
	{
		var methods = new Dictionary<string, NativeMethodSnapshotEntry>(StringComparer.Ordinal);
		foreach (var method in missingMethods)
		{
			methods[method] = new NativeMethodSnapshotEntry(NativeMethodPresence.Missing, allMissingReason);
		}

		return methods;
	}

	private static IReadOnlyDictionary<string, NativeMethodSnapshotEntry> CreateStakeTable()
	{
		const string MissingReason =
			"method is not handled in node_cpp/src/carbon/contracts/phantasma/phantasma_stake.cpp switch dispatch";

		var methods = new Dictionary<string, NativeMethodSnapshotEntry>(StringComparer.Ordinal)
		{
			["Claim"] = new NativeMethodSnapshotEntry(
				NativeMethodPresence.Available,
				"implemented in node_cpp/src/carbon/contracts/phantasma/phantasma_stake.cpp (case \"Claim\")"),
			["GetUnclaimed"] = new NativeMethodSnapshotEntry(
				NativeMethodPresence.Available,
				"implemented in node_cpp/src/carbon/contracts/phantasma/phantasma_stake.cpp (case \"GetUnclaimed\")"),
			["Stake"] = new NativeMethodSnapshotEntry(
				NativeMethodPresence.Available,
				"implemented in node_cpp/src/carbon/contracts/phantasma/phantasma_stake.cpp (case \"Stake\")"),
			["Unstake"] = new NativeMethodSnapshotEntry(
				NativeMethodPresence.Available,
				"implemented in node_cpp/src/carbon/contracts/phantasma/phantasma_stake.cpp (case \"Unstake\")"),

			["FuelToStake"] = new NativeMethodSnapshotEntry(NativeMethodPresence.Missing, MissingReason),
			["GetAddressVotingPower"] = new NativeMethodSnapshotEntry(NativeMethodPresence.Missing, MissingReason),
			["GetClaimMasterCount"] = new NativeMethodSnapshotEntry(NativeMethodPresence.Missing, MissingReason),
			["GetMasterClaimDate"] = new NativeMethodSnapshotEntry(NativeMethodPresence.Missing, MissingReason),
			["GetMasterClaimDateFromReference"] = new NativeMethodSnapshotEntry(NativeMethodPresence.Missing, MissingReason),
			["GetMasterCount"] = new NativeMethodSnapshotEntry(NativeMethodPresence.Missing, MissingReason),
			["GetMasterAddresses"] = new NativeMethodSnapshotEntry(NativeMethodPresence.Missing, MissingReason),
			["GetMasterDate"] = new NativeMethodSnapshotEntry(NativeMethodPresence.Missing, MissingReason),
			["GetMasterRewards"] = new NativeMethodSnapshotEntry(NativeMethodPresence.Missing, MissingReason),
			["GetMasterThreshold"] = new NativeMethodSnapshotEntry(NativeMethodPresence.Missing, MissingReason),
			["GetStake"] = new NativeMethodSnapshotEntry(NativeMethodPresence.Missing, MissingReason),
			["GetStakeTimestamp"] = new NativeMethodSnapshotEntry(NativeMethodPresence.Missing, MissingReason),
			["GetStorageStake"] = new NativeMethodSnapshotEntry(NativeMethodPresence.Missing, MissingReason),
			["GetTimeBeforeUnstake"] = new NativeMethodSnapshotEntry(NativeMethodPresence.Missing, MissingReason),
			["IsMaster"] = new NativeMethodSnapshotEntry(NativeMethodPresence.Missing, MissingReason),
			["MasterClaim"] = new NativeMethodSnapshotEntry(NativeMethodPresence.Missing, MissingReason),
			["StakeToFuel"] = new NativeMethodSnapshotEntry(NativeMethodPresence.Missing, MissingReason),
		};

		return methods;
	}
}
