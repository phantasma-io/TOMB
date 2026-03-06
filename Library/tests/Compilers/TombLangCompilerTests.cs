namespace TOMBLib.Tests.Compilers;

using Phantasma.Tomb;
using Phantasma.Tomb.Compilers;
using Phantasma.Tomb.Validation;
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

	private NativeCheckMode _originalMode;
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

	[SetUp]
	public void SetUp()
	{
		_originalMode = Compiler.NativeCheckMode;
		_originalWarningHandler = Compiler.WarningHandler;
		_warnings = new List<string>();
		Compiler.WarningHandler = warning => _warnings.Add(warning);
	}

	[TearDown]
	public void TearDown()
	{
		Compiler.NativeCheckMode = _originalMode;
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
		var parser = new TombLangCompiler();

		var modules = parser.Process(DynamicSupportedStakeMethodContract);

		Assert.That(modules.Length, Is.EqualTo(1));
		Assert.That(_warnings, Is.Empty);
	}

	[Test]
	public void DynamicNativeMethod_ErrorMode_ThrowsForNonLiteralTarget()
	{
		// Without literal target names the compiler cannot prove availability against the snapshot.
		Compiler.NativeCheckMode = NativeCheckMode.Error;
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
	public void Snapshot_TracksStakeClaimAsAvailable_AndMasterCountAsMissing()
	{
		// Spot-check two stake methods with opposite runtime states to validate snapshot semantics.
		Assert.That(NativeMethodAvailability.TryGetSnapshotStatus("stake", "Claim", out var claimEntry), Is.True);
		Assert.That(claimEntry.Presence, Is.EqualTo(NativeMethodPresence.Available));

		Assert.That(NativeMethodAvailability.TryGetSnapshotStatus("stake", "GetMasterCount", out var masterCountEntry), Is.True);
		Assert.That(masterCountEntry.Presence, Is.EqualTo(NativeMethodPresence.Missing));
	}
}
