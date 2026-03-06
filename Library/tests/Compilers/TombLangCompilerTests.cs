namespace TOMBLib.Tests.Compilers;

using System.Linq;
using Phantasma.Tomb;
using Phantasma.Tomb.AST;
using Phantasma.Tomb.AST.Declarations;
using Phantasma.Tomb.Compilers;
using Phantasma.Tomb.Validation;
using Phantasma.Tomb.CodeGen;
using Phantasma.Business.Blockchain.Contracts.Native;

public class TombLangCompilerTests
{
	private const string UnsupportedAccountContract = @"
contract test {
    import Account;
    public run(from:address)
    {
        Account.registerName(from, ""name"");
    }
}";

	private const string SupportedStakeContract = @"
contract test {
    import Stake;
    public run(from:address)
    {
        Stake.claim(from, from);
    }
}";

	private const string DynamicUnsupportedAccountMethodContract = @"
contract test {
    import Call;
    public run(from:address)
    {
        Call.contract<none>(""account"", ""RegisterName"", from, ""name"");
    }
}";

	private const string DynamicSupportedStakeMethodContract = @"
contract test {
    import Call;
    public run(from:address)
    {
        Call.contract<none>(""stake"", ""Claim"", from, from);
    }
}";

	private const string DynamicUnknownNativeContractMethodContract = @"
contract test {
    import Call;
    public run(from:address)
    {
        Call.contract<none>(""customcontract"", ""DoStuff"", from);
    }
}";

	private const string DynamicNonLiteralTargetContract = @"
contract test {
    import Call;
    public run(from:address, contractName:string)
    {
        Call.contract<none>(contractName, ""Claim"", from, from);
    }
}";

	private const string DynamicUnsupportedAccountMethodViaContractLibrary = @"
contract test {
    import Contract;
    public run(from:address)
    {
        Contract.call<none>(""account"", ""RegisterName"", from, ""name"");
    }
}";

	private const string DynamicSupportedStakeMethodViaContractLibrary = @"
contract test {
    import Contract;
    public run(from:address)
    {
        Contract.call<none>(""stake"", ""Claim"", from, from);
    }
}";

	private const string DynamicNonLiteralTargetViaContractLibrary = @"
contract test {
    import Contract;
    public run(from:address, contractName:string)
    {
        Contract.call<none>(contractName, ""Claim"", from, from);
    }
}";

	private const string ArrayLengthPropertyContract = @"
contract test {
    import Array;
    public run():number
    {
        local arr1: array<number> = {1};
        return arr1.length();
    }
}";

	private const string StructConstructorAfterImportContract = @"
struct MyLocalStruct {
    name:string;
    age:number;
}

contract test{
    import Struct;
    public run(name:string, age:number):MyLocalStruct {
        local myStruct:MyLocalStruct = Struct.MyLocalStruct(name, age);
        myStruct.age = 20;
        return myStruct;
    }
}";

	private const string ForLoopScopeContract = @"
contract test {
    public run():number
    {
        local sum:number = 0;
        for (local i=0; i<3; i+=1)
        {
            sum += i;
        }

        local i:number = 10;
        return sum + i;
    }
}";

	private const string TokenCreateRuntimeSignatureContract = @"
contract test {
    import Token;
    public run(from:address)
    {
        Token.create(from, 0x00, 0x00);
    }
	}";

	private const string StorageReadContract = @"
contract test {
    import Storage;
    public run():number
    {
        return Storage.read<number>(""test"", ""counter"");
    }
}";

	private const string StorageWriteContract = @"
contract test {
    import Storage;
    public run()
    {
        Storage.write(""counter"", 1);
    }
}";

	private const string MapGetContract = @"
contract test {
    import Map;
    global _map: storage_map<number, number>;
    public run():number
    {
        return _map.get(1);
    }
}";

	private const string MapSetContract = @"
contract test {
    import Map;
    global _map: storage_map<number, number>;
    public run()
    {
        _map.set(1, 2);
    }
}";

	private const string MapSetRepeatedContract = @"
contract test {
    import Map;
    global _map: storage_map<number, number>;
    public run()
    {
        _map.set(1, 2);
        _map.set(2, 3);
    }
}";

	private const string ListGetContract = @"
contract test {
    import List;
    global _list: storage_list<number>;
    public run():number
    {
        return _list.get(0);
    }
}";

	private const string ListAddContract = @"
contract test {
    import List;
    global _list: storage_list<number>;
    public run()
    {
        _list.add(10);
    }
}";

	private const string AddressIsNullContract = @"
contract test {
    import Address;
    public run(target:address):bool
    {
        return Address.isNull(target);
    }
}";

	private const string UnknownInteropAliasContract = @"
contract test {
    import Call;
    public run()
    {
        Call.interop<none>(""Runtime.UnknownMethod"");
    }
}";

	private NativeCheckMode _originalMode;
	private NativeCheckMode _originalInteropMode;
	private Action<string> _originalWarningHandler = static _ => { };
	private List<string> _warnings = new();

	private static readonly (Type Type, string Contract)[] PlaceholderContracts =
	{
		(typeof(AccountContract), "account"),
		(typeof(GovernanceContract), "governance"),
		(typeof(MailContract), "mail"),
		(typeof(MarketContract), "market"),
		(typeof(RankingContract), "ranking"),
		(typeof(RelayContract), "relay"),
		(typeof(SaleContract), "sale"),
		(typeof(StakeContract), "stake"),
		(typeof(StorageContract), "storage"),
	};

	private static bool IsDynamicNativeGateway(MethodInterface method)
	{
		return (string.Equals(method.Library.Name, "Call", StringComparison.Ordinal) &&
				string.Equals(method.Name, "contract", StringComparison.Ordinal)) ||
			   (string.Equals(method.Library.Name, "Contract", StringComparison.Ordinal) &&
				string.Equals(method.Name, "call", StringComparison.Ordinal));
	}

	private static bool IsDynamicInteropGateway(MethodInterface method)
	{
		return string.Equals(method.Library.Name, "Call", StringComparison.Ordinal) &&
			   string.Equals(method.Name, "interop", StringComparison.Ordinal);
	}

	private static IEnumerable<LibraryDeclaration> LoadContractLibrariesForCoverage()
	{
		var module = new Script("snapshot_coverage", ModuleKind.Contract);
		var names = Module.AvailableLibraries.Distinct(StringComparer.Ordinal);

		foreach (var name in names)
		{
			LibraryDeclaration lib;
			try
			{
				lib = Module.LoadLibrary(name, module.Scope, module.Kind);
			}
			catch (CompilerException ex) when (ex.Message.Contains("unknown library:", StringComparison.Ordinal))
			{
				// Some available names are module-kind specific (for example Format for Description modules).
				continue;
			}

			yield return lib;
		}
	}

	[SetUp]
	public void SetUp()
	{
		_originalMode = Compiler.NativeCheckMode;
		_originalInteropMode = Compiler.InteropCheckMode;
		_originalWarningHandler = Compiler.WarningHandler;
		_warnings = new List<string>();
		Compiler.WarningHandler = warning => _warnings.Add(warning);
	}

	[TearDown]
	public void TearDown()
	{
		Compiler.NativeCheckMode = _originalMode;
		Compiler.InteropCheckMode = _originalInteropMode;
		Compiler.WarningHandler = _originalWarningHandler;
	}

	private static CompilerException ExpectCompilerException(TestDelegate code)
	{
		var exception = Assert.Throws<CompilerException>(code);
		if (exception is null)
		{
			throw new AssertionException("Expected CompilerException, but no exception was captured.");
		}

		return exception;
	}

	[Test]
	public void UnsupportedNativeMethod_DefaultErrorMode_Throws()
	{
		// Default policy is strict: known-missing native methods must fail compilation.
		Compiler.NativeCheckMode = NativeCheckMode.Error;
		Compiler.InteropCheckMode = NativeCheckMode.Off;
		var parser = new TombLangCompiler();

		var ex = ExpectCompilerException(() => parser.Process(UnsupportedAccountContract));

		Assert.That(ex.Message, Does.Contain("account.RegisterName"));
		Assert.That(ex.Message, Does.Contain(NativeMethodAvailability.ChainBaselineCommit));
	}

	[Test]
	public void UnsupportedNativeMethod_WarnMode_CompilesAndWarnsOnce()
	{
		// Warn mode keeps compilation successful but emits one actionable warning per method.
		Compiler.NativeCheckMode = NativeCheckMode.Warn;
		Compiler.InteropCheckMode = NativeCheckMode.Off;
		var parser = new TombLangCompiler();

		var modules = parser.Process(UnsupportedAccountContract);

		Assert.That(modules.Length, Is.EqualTo(1));
		Assert.That(_warnings.Count, Is.EqualTo(1));
		Assert.That(_warnings[0], Does.Contain("account.RegisterName"));
		Assert.That(_warnings[0], Does.Contain(NativeMethodAvailability.ChainBaselineCommit));
	}

	[Test]
	public void UnsupportedNativeMethod_OffMode_CompilesWithoutWarning()
	{
		// Off mode fully disables availability checks for workflows that need unrestricted emission.
		Compiler.NativeCheckMode = NativeCheckMode.Off;
		Compiler.InteropCheckMode = NativeCheckMode.Off;
		var parser = new TombLangCompiler();

		var modules = parser.Process(UnsupportedAccountContract);

		Assert.That(modules.Length, Is.EqualTo(1));
		Assert.That(_warnings, Is.Empty);
	}

	[Test]
	public void SupportedStakeMethod_ErrorMode_CompilesWithoutWarning()
	{
		// Supported native methods should remain unaffected by strict error mode.
		Compiler.NativeCheckMode = NativeCheckMode.Error;
		Compiler.InteropCheckMode = NativeCheckMode.Off;
		var parser = new TombLangCompiler();

		var modules = parser.Process(SupportedStakeContract);

		Assert.That(modules.Length, Is.EqualTo(1));
		Assert.That(_warnings, Is.Empty);
	}

	[Test]
	public void ArrayLengthProperty_Compiles()
	{
		// Regression guard for lexer tokenization: arr.length() must stay a property call,
		// not a malformed numeric token.
		Compiler.NativeCheckMode = NativeCheckMode.Off;
		Compiler.InteropCheckMode = NativeCheckMode.Off;
		var parser = new TombLangCompiler();

		var modules = parser.Process(ArrayLengthPropertyContract);

		Assert.That(modules.Length, Is.EqualTo(1));
	}

	[Test]
	public void StructConstructorAfterImport_Compiles()
	{
		// Importing Struct must preserve generated constructors like Struct.MyLocalStruct(...).
		var parser = new TombLangCompiler();

		var modules = parser.Process(StructConstructorAfterImportContract);

		Assert.That(modules.Length, Is.EqualTo(1));
	}

	[Test]
	public void ForLoopScope_DoesNotLeakLoopVariable()
	{
		// Regression guard: "for (local i ...)" must not pollute parent scope.
		// Redeclaring i after the loop should compile.
		var parser = new TombLangCompiler();

		var modules = parser.Process(ForLoopScopeContract);

		Assert.That(modules.Length, Is.EqualTo(1));
	}

	[Test]
	public void TokenCreate_UsesRuntimeThreeArgumentSignature()
	{
		// Regression guard for Nexus.CreateToken mapping: Token.create must use
		// (from, script, abiBytes) to match runtime interop arity.
		var parser = new TombLangCompiler();

		var modules = parser.Process(TokenCreateRuntimeSignatureContract);

		Assert.That(modules.Length, Is.EqualTo(1));
	}

	[Test]
	public void DynamicNativeMethod_ErrorMode_ThrowsForMissingMethod()
	{
		// Dynamic contract calls with literal native targets must be validated like direct wrappers.
		Compiler.NativeCheckMode = NativeCheckMode.Error;
		Compiler.InteropCheckMode = NativeCheckMode.Off;
		var parser = new TombLangCompiler();

		var ex = ExpectCompilerException(() => parser.Process(DynamicUnsupportedAccountMethodContract));

		Assert.That(ex.Message, Does.Contain("account.RegisterName"));
		Assert.That(ex.Message, Does.Contain(NativeMethodAvailability.ChainBaselineCommit));
	}

	[Test]
	public void DynamicNativeMethod_ErrorMode_CompilesForSupportedMethod()
	{
		// Literal dynamic calls to supported native methods should compile in strict mode.
		Compiler.NativeCheckMode = NativeCheckMode.Error;
		Compiler.InteropCheckMode = NativeCheckMode.Off;
		var parser = new TombLangCompiler();

		var modules = parser.Process(DynamicSupportedStakeMethodContract);

		Assert.That(modules.Length, Is.EqualTo(1));
		Assert.That(_warnings, Is.Empty);
	}

	[Test]
	public void DynamicNativeMethod_ErrorMode_ThrowsForUnknownNativeContract()
	{
		// Fail-closed policy: dynamic native targets must map to a contract table in snapshot.
		Compiler.NativeCheckMode = NativeCheckMode.Error;
		Compiler.InteropCheckMode = NativeCheckMode.Off;
		var parser = new TombLangCompiler();

		var ex = ExpectCompilerException(() => parser.Process(DynamicUnknownNativeContractMethodContract));

		Assert.That(ex.Message, Does.Contain("native contract 'customcontract' has no explicit status table"));
		Assert.That(ex.Message, Does.Contain(NativeMethodAvailability.ChainBaselineCommit));
	}

	[Test]
	public void DynamicNativeMethod_ErrorMode_ThrowsForNonLiteralTarget()
	{
		// Without literal target names the compiler cannot prove availability against the snapshot.
		Compiler.NativeCheckMode = NativeCheckMode.Error;
		Compiler.InteropCheckMode = NativeCheckMode.Off;
		var parser = new TombLangCompiler();

		var ex = ExpectCompilerException(() => parser.Process(DynamicNonLiteralTargetContract));

		Assert.That(ex.Message, Does.Contain("is not a literal contract/method pair"));
		Assert.That(ex.Message, Does.Contain(NativeMethodAvailability.ChainBaselineCommit));
	}

	[Test]
	public void DynamicNativeMethod_ContractLibrary_ErrorMode_ThrowsForMissingMethod()
	{
		// Contract.call(...) must be validated exactly like Call.contract(...).
		Compiler.NativeCheckMode = NativeCheckMode.Error;
		Compiler.InteropCheckMode = NativeCheckMode.Off;
		var parser = new TombLangCompiler();

		var ex = ExpectCompilerException(() => parser.Process(DynamicUnsupportedAccountMethodViaContractLibrary));

		Assert.That(ex.Message, Does.Contain("account.RegisterName"));
		Assert.That(ex.Message, Does.Contain(NativeMethodAvailability.ChainBaselineCommit));
	}

	[Test]
	public void DynamicNativeMethod_ContractLibrary_ErrorMode_CompilesForSupportedMethod()
	{
		// Supported native targets must remain usable through Contract.call(...).
		Compiler.NativeCheckMode = NativeCheckMode.Error;
		Compiler.InteropCheckMode = NativeCheckMode.Off;
		var parser = new TombLangCompiler();

		var modules = parser.Process(DynamicSupportedStakeMethodViaContractLibrary);

		Assert.That(modules.Length, Is.EqualTo(1));
		Assert.That(_warnings, Is.Empty);
	}

	[Test]
	public void DynamicNativeMethod_ContractLibrary_WarnMode_EmitsWarningBeforeLiteralRequirementFailure()
	{
		// Warn mode still reports unresolved dynamic targets, but generation currently
		// requires literal contract selectors for this gateway.
		Compiler.NativeCheckMode = NativeCheckMode.Warn;
		Compiler.InteropCheckMode = NativeCheckMode.Off;
		var parser = new TombLangCompiler();

		var ex = ExpectCompilerException(() => parser.Process(DynamicNonLiteralTargetViaContractLibrary));

		Assert.That(ex.Message, Does.Contain("can't be converted to String literal"));
		Assert.That(_warnings.Count, Is.EqualTo(1));
		Assert.That(_warnings[0], Does.Contain("Contract.call"));
		Assert.That(_warnings[0], Does.Contain("is not a literal contract/method pair"));
		Assert.That(_warnings[0], Does.Contain(NativeMethodAvailability.ChainBaselineCommit));
	}

	[Test]
	public void Snapshot_CoversAllBridgePlaceholderMethods()
	{
		// Every placeholder method must have an explicit Available/Missing status in the snapshot.
		var missingStatuses = new List<string>();

		foreach (var (type, contract) in PlaceholderContracts)
		{
			var methods = type.GetMethods(System.Reflection.BindingFlags.Public |
										  System.Reflection.BindingFlags.Static |
										  System.Reflection.BindingFlags.DeclaredOnly);

			foreach (var method in methods)
			{
				if (!NativeMethodAvailability.TryGetSnapshotStatus(contract, method.Name, out _))
				{
					missingStatuses.Add($"{contract}.{method.Name}");
				}
			}
		}

		Assert.That(missingStatuses, Is.Empty,
			"Native snapshot is incomplete. Missing entries: " + string.Join(", ", missingStatuses));
	}

	[Test]
	public void Snapshot_CoversAllContractCallAliases_FromContractLibraries()
	{
		// Every static ContractCall alias exposed to regular contract modules must be explicitly
		// represented in the native snapshot table. Dynamic gateways are validated by literal
		// target resolution at compile time and are checked separately.
		var missingStatuses = new List<string>();

		foreach (var lib in LoadContractLibrariesForCoverage())
		{
			foreach (var method in lib.methods.Values)
			{
				if (method.Implementation != MethodImplementationType.ContractCall)
				{
					continue;
				}

				if (IsDynamicNativeGateway(method))
				{
					continue;
				}

				if (string.IsNullOrWhiteSpace(method.Contract) ||
					string.IsNullOrWhiteSpace(method.Alias) ||
					!NativeMethodAvailability.TryGetSnapshotStatus(method.Contract, method.Alias, out _))
				{
					missingStatuses.Add($"{method.Library.Name}.{method.Name} -> {method.Contract}.{method.Alias}");
				}
			}
		}

		Assert.That(missingStatuses, Is.Empty,
			"Native snapshot is missing contract-call alias entries: " + string.Join(", ", missingStatuses));
	}

	[Test]
	public void Snapshot_CoversAllExtCallAliases_FromContractLibraries()
	{
		// Every static ExtCall alias exposed to regular contract modules must be explicitly
		// represented in the interop snapshot table. Dynamic Call.interop(...) is validated by
		// literal-method checks and does not have a fixed alias entry.
		var missingStatuses = new List<string>();

		foreach (var lib in LoadContractLibrariesForCoverage())
		{
			foreach (var method in lib.methods.Values)
			{
				if (method.Implementation != MethodImplementationType.ExtCall)
				{
					continue;
				}

				if (IsDynamicInteropGateway(method))
				{
					continue;
				}

				if (string.IsNullOrWhiteSpace(method.Alias) ||
					!InteropMethodAvailability.TryGetSnapshotStatus(method.Alias, out _))
				{
					missingStatuses.Add($"{method.Library.Name}.{method.Name} -> {method.Alias}");
				}
			}
		}

		Assert.That(missingStatuses, Is.Empty,
			"Interop snapshot is missing extcall alias entries: " + string.Join(", ", missingStatuses));
	}

	[Test]
	public void Snapshot_TracksStakeClaimAsAvailable_AndMasterCountAsMissing()
	{
		// Spot-check two stake methods with opposite runtime states to validate snapshot semantics.
		Assert.That(NativeMethodAvailability.TryGetSnapshotStatus("stake", "Claim", out var claimEntry), Is.True);
		Assert.That(claimEntry.Presence, Is.EqualTo(NativeMethodPresence.Available));

		Assert.That(NativeMethodAvailability.TryGetSnapshotStatus("stake", "GetMasterCount", out var masterCountEntry), Is.True);
		Assert.That(masterCountEntry.Presence, Is.EqualTo(NativeMethodPresence.Missing));
	}

	[Test]
	public void BasicInterop_MapGet_ErrorMode_CompilesWithoutWarning()
	{
		// Map.Get is currently implemented in Carbon interop and should compile in strict mode.
		Compiler.NativeCheckMode = NativeCheckMode.Off;
		Compiler.InteropCheckMode = NativeCheckMode.Error;
		var parser = new TombLangCompiler();

		var modules = parser.Process(MapGetContract);

		Assert.That(modules.Length, Is.EqualTo(1));
		Assert.That(_warnings, Is.Empty);
	}

	[Test]
	public void BasicInterop_MapSet_ErrorMode_Throws()
	{
		// Map.Set currently routes to runtime error in Carbon and must be blocked at compile time.
		Compiler.NativeCheckMode = NativeCheckMode.Off;
		Compiler.InteropCheckMode = NativeCheckMode.Error;
		var parser = new TombLangCompiler();

		var ex = ExpectCompilerException(() => parser.Process(MapSetContract));

		Assert.That(ex.Message, Does.Contain("Map.Set"));
		Assert.That(ex.Message, Does.Contain(NativeMethodAvailability.ChainBaselineCommit));
	}

	[Test]
	public void BasicInterop_MapSet_WarnMode_CompilesAndWarnsOnce()
	{
		// Warn mode keeps emission enabled while reporting one deduplicated warning for Map.Set.
		Compiler.NativeCheckMode = NativeCheckMode.Off;
		Compiler.InteropCheckMode = NativeCheckMode.Warn;
		var parser = new TombLangCompiler();

		var modules = parser.Process(MapSetRepeatedContract);

		Assert.That(modules.Length, Is.EqualTo(1));
		Assert.That(_warnings.Count, Is.EqualTo(1));
		Assert.That(_warnings[0], Does.Contain("Map.Set"));
		Assert.That(_warnings[0], Does.Contain(NativeMethodAvailability.ChainBaselineCommit));
	}

	[Test]
	public void BasicInterop_StorageRead_ErrorMode_CompilesWithoutWarning()
	{
		// Data.Get is implemented in Carbon and underpins Storage.read.
		Compiler.NativeCheckMode = NativeCheckMode.Off;
		Compiler.InteropCheckMode = NativeCheckMode.Error;
		var parser = new TombLangCompiler();

		var modules = parser.Process(StorageReadContract);

		Assert.That(modules.Length, Is.EqualTo(1));
		Assert.That(_warnings, Is.Empty);
	}

	[Test]
	public void BasicInterop_StorageWrite_ErrorMode_Throws()
	{
		// Data.Set currently routes to runtime error in Carbon and must be flagged at compile time.
		Compiler.NativeCheckMode = NativeCheckMode.Off;
		Compiler.InteropCheckMode = NativeCheckMode.Error;
		var parser = new TombLangCompiler();

		var ex = ExpectCompilerException(() => parser.Process(StorageWriteContract));

		Assert.That(ex.Message, Does.Contain("Data.Set"));
		Assert.That(ex.Message, Does.Contain(NativeMethodAvailability.ChainBaselineCommit));
	}

	[Test]
	public void BasicInterop_ListGet_ErrorMode_CompilesWithoutWarning()
	{
		// List.Get is currently implemented in Carbon interop and should remain usable.
		Compiler.NativeCheckMode = NativeCheckMode.Off;
		Compiler.InteropCheckMode = NativeCheckMode.Error;
		var parser = new TombLangCompiler();

		var modules = parser.Process(ListGetContract);

		Assert.That(modules.Length, Is.EqualTo(1));
		Assert.That(_warnings, Is.Empty);
	}

	[Test]
	public void BasicInterop_ListAdd_ErrorMode_Throws()
	{
		// List.Add currently routes to runtime error in Carbon and must be blocked at compile time.
		Compiler.NativeCheckMode = NativeCheckMode.Off;
		Compiler.InteropCheckMode = NativeCheckMode.Error;
		var parser = new TombLangCompiler();

		var ex = ExpectCompilerException(() => parser.Process(ListAddContract));

		Assert.That(ex.Message, Does.Contain("List.Add"));
		Assert.That(ex.Message, Does.Contain(NativeMethodAvailability.ChainBaselineCommit));
	}

	[Test]
	public void BasicInterop_UnimplementedCustomMethod_ErrorMode_Throws()
	{
		// Methods without callbacks are placeholders that emit runtime THROW and must be rejected.
		Compiler.NativeCheckMode = NativeCheckMode.Off;
		Compiler.InteropCheckMode = NativeCheckMode.Error;
		var parser = new TombLangCompiler();

		var ex = ExpectCompilerException(() => parser.Process(AddressIsNullContract));

		Assert.That(ex.Message, Does.Contain("Address.isNull"));
		Assert.That(ex.Message, Does.Contain("runtime THROW"));
		Assert.That(ex.Message, Does.Contain(NativeMethodAvailability.ChainBaselineCommit));
	}

	[Test]
	public void BasicInterop_Snapshot_TracksMapGetAvailable_AndMapSetMissing()
	{
		// Spot-check storage interop snapshot semantics at the pinned chain baseline.
		Assert.That(InteropMethodAvailability.TryGetSnapshotStatus("Map.Get", out var mapGetEntry), Is.True);
		Assert.That(mapGetEntry.Presence, Is.EqualTo(NativeMethodPresence.Available));

		Assert.That(InteropMethodAvailability.TryGetSnapshotStatus("Map.Set", out var mapSetEntry), Is.True);
		Assert.That(mapSetEntry.Presence, Is.EqualTo(NativeMethodPresence.Missing));
	}

	[Test]
	public void BasicInterop_StrictMode_FailClosed_WhenAliasMissingFromSnapshot()
	{
		// Fail-closed policy: even methods not yet classified must be rejected in strict mode
		// until the snapshot gets an explicit Available/Missing entry.
		Compiler.NativeCheckMode = NativeCheckMode.Off;
		Compiler.InteropCheckMode = NativeCheckMode.Error;
		var parser = new TombLangCompiler();

		var ex = ExpectCompilerException(() => parser.Process(UnknownInteropAliasContract));

		Assert.That(ex.Message, Does.Contain("Runtime.UnknownMethod"));
		Assert.That(ex.Message, Does.Contain("no explicit status"));
		Assert.That(ex.Message, Does.Contain(NativeMethodAvailability.ChainBaselineCommit));
	}
}
