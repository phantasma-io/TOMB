# TOMB

TOMB is the Phantasma smart-contract compiler.

- Repository name: `TOMB`
- Current CLI executable name: `pha-tomb`
- Target VM: Phantasma VM
- Source files: `.tomb`

## CLI Usage

Install `pha-tomb` as a .NET global tool:

```bash
dotnet tool install --global pha-tomb
dotnet tool update --global pha-tomb
```

Then invoke it with the source file as the last argument.

```bash
pha-tomb my_contract.tomb
```

Useful commands:

```bash
pha-tomb --version
pha-tomb --help
pha-tomb output:/tmp/build protocol:19 debug nativecheck:error my_contract.tomb
```

### CLI

```text
pha-tomb <options> <source-file>
```

| Argument | Description |
| -------- | ----------- |
| `--version`, `-v`, `version` | Print the compiler version only. |
| `--help`, `-h`, `help` | Print CLI help. |
| `output:<directory>` | Output root. Artifacts go into `Output/` under this directory. |
| `protocol:<number>` | Target protocol version. If omitted, the compiler uses the latest known protocol from the referenced Phantasma packages. |
| `libpath:<directory>` | Additional ABI search path. Can be specified multiple times. |
| `debug` | Enable debug mode and emit debug artifacts when available. |
| `nativecheck:<off|warn|error>` | Check native-contract and interop usage against the compiler's pinned Carbon snapshots. |

### Output Rules

- The source file must be the last CLI argument.
- If `output:<directory>` is omitted, the compiler uses the source file directory as the output root.
- Artifacts are always written to `Output/` under the selected output root.
- The CLI automatically adds that output directory to the ABI import search paths, so modules compiled in the same pass can be imported by ABI.
- Unknown options are reported as warnings.
- Compile-time errors exit with a non-zero status.

## Generated Artifacts

The compiler emits artifacts per module:

| Artifact | When emitted |
| -------- | ------------ |
| `.asm` | When textual assembly is generated for the module. |
| `.pvm` | For contract, token, NFT submodule, and description modules. |
| `.tx` | For script modules. |
| `.abi` | When ABI metadata exists for the module. |
| `.debug` | When debug info exists for the module. |
| `.pvm.hex`, `.tx.hex` | Hex-encoded script output for `.pvm` / `.tx`. |
| `.abi.hex` | Hex-encoded ABI payload. |

Nested NFT token submodules are exported as separate artifacts together with the parent token module.

## Publishing

To publish a new `pha-tomb` package locally:

```bash
NUGET_API_KEY=... just publish-nuget
```

## Source Language

TOMB source files use the `.tomb` extension.

## Declarations

Top-level declarations:

- `contract`
- `token`
- `script`
- `description`
- `struct`
- `enum`
- `const`

Nested declarations:

- `nft<rom, ram>` submodules inside `token`
- `property`
- `event`
- `constructor`
- `trigger`
- `task`
- `register`

### Triggers

The parser accepts only the trigger names and signatures implemented in `TombLangCompiler`.

| Trigger name | Signature | Status |
| ------------ | --------- | ------ |
| `onMint`, `onBurn`, `onSend`, `onReceive`, `onInfuse` | `(from:address, to:address, symbol:string, amount:number)` | Compiler support exists. Chain support may vary. |
| `onWitness`, `onSeries`, `onKill`, `onUpgrade` | `(from:address)` | Compiler support exists. Chain support may vary. |
| `onMigrate` | `(from:address, to:address)` | Supported by the compiler. |
| `onWrite` | `(from:address, data:any)` | Supported by the compiler. |
| Any other trigger name | n/a | Not supported. |

Notes:

- Trigger names may be written with or without the `on` prefix.
- `break` and `continue` inside triggers are not supported.

## Feature Status

### Supported

- Constants, enums, structs, global and local variables
- Contract methods, properties, constructors, events, triggers
- Script and description modules
- Nested NFT modules inside token modules
- Numbers, strings, bools, timestamps, addresses, hashes, byte arrays
- Maps, lists, arrays, generic types, type inference
- `if`, `while`, `do/while`, `for`, `switch`, `break`, `continue`
- Exceptions via `throw`
- Multi-return methods using `: type*`
- Inline assembly blocks
- External contract calls and interop calls
- ABI generation
- Debug artifact generation
- Postfix `++` and `--`

### Partially Supported

- Native contract and interop calls, because the compiler accepts more than the current Carbon runtime exposes
- Array helper library
- Builtin random helpers

### Important Caveats

- Explicit register allocation syntax still exists, but it is known to be broken because of `CALL` frame allocation. Treat it as unsupported.
- Task syntax is parsed by the compiler, but the current Carbon runtime does not expose the required `Task.*` interops. Treat tasks as unsupported on the current chain baseline.
- `Array.pop()` currently returns the last element without mutating the backing array, because the current Phoenix VM opcode set does not expose array-key removal.

## Libraries

### Importable Libraries

These libraries can be imported directly:

- `Call`
- `Runtime`
- `Math`
- `Token`
- `NFT`
- `Organization`
- `Oracle`
- `Storage`
- `Contract`
- `Array`
- `Leaderboard`
- `Market`
- `Account`
- `Crowdsale`
- `Stake`
- `Governance`
- `Relay`
- `Mail`
- `Time`
- `Task`
- `UID`
- `Map`
- `List`
- `String`
- `Bytes`
- `Decimal`
- `Enum`
- `Address`
- `Module`
- `Format`

### Auto-Imported Libraries

These libraries are imported automatically in every module:

- `String`
- `Bytes`
- `Decimal`
- `Enum`

### Additional Helpers

The compiler also has a few special-case libraries and helpers outside the main import list:

- `Random`
- `Chain`
- `Platform`
- `Cryptography`
- per-struct constructor helpers

### Current Library Caveats

- `Format` is only valid in `description` modules.
- `Math.sqrt`, `String.toUpper`, `String.toLower`, `String.indexOf`, `Random.seed`, and `Random.generate` are builtin helpers compiled from embedded builtin TOMB code.
- `Address.isNull`, `Address.isUser`, `Address.isSystem`, and `Address.isInterop` are not supported. They compile to explicit runtime `THROW`.
- `Array.set`, `Array.remove`, and `Array.clear` are not supported. They compile to explicit runtime `THROW`.
- `Struct.fromBytes` is not supported. It compiles to explicit runtime `THROW`.
- `Token.create` uses the signature `Token.create(from:Address, script:Bytes, abiBytes:Bytes)`.
- `Runtime.deployContract` and `Runtime.upgradeContract` accept `Module` values, not raw `(script, abi)` argument pairs.

### Runtime Availability

The compiler can parse calls that the current Carbon runtime still does not expose.

`nativecheck:<off|warn|error>` checks:

- native contract calls against `Library/src/Validation/NativeMethodAvailability.cs`
- interops against `Library/src/Validation/InteropMethodAvailability.cs`

For real contracts, use `nativecheck:error`.

Examples that are not available on the current Carbon baseline:

- `Task.*`
- `Oracle.*`
- `Organization.*`
- `Contract.exists`
- `Runtime.transactionHash`
- `Runtime.getGovernanceValue`
- `Token.isMinter`
- `Token.swap`
- `Token.write`
- `Token.getCurrentSupply`
- `Token.availableSymbols`
- `NFT.write`
- `NFT.availableSymbols`

Some advanced methods in `Stake`, `Account`, `Relay`, `Market`, `Crowdsale`, `Mail`, and `Storage` are only partially available. Use `nativecheck:error` to catch exact mismatches against the compiler's pinned runtime snapshots.

## Macros

Builtin compiler macros handled by `MacroExpression`:

- `$THIS_ADDRESS`
- `$THIS_SYMBOL`
- `$TYPE_OF(...)`

Notes:

- `$THIS_SYMBOL` is only available inside token context.
- The compiler also registers per-module macros such as `<CONTRACT_NAME>_ADDRESS`.
- Token modules also register `<TOKEN_SYMBOL>_SYMBOL`.
- Literal token properties are also exposed as macros of the form `<TOKEN_NAME>_<property>`.
- Host applications embedding the compiler can register additional macros through `Compiler.RegisterMacro(...)`.

## Importing External ABIs

`libpath:<directory>` adds an ABI search root.

External library imports are resolved as:

```text
<libpath>/<import-name-with-dots-replaced-by-path-separators>.abi
```

For example, with:

```bash
pha-tomb libpath:/opt/phantasma/abi my_contract.tomb
```

an import such as:

```csharp
import Foo.Bar;
```

is resolved from:

```text
/opt/phantasma/abi/Foo/Bar.abi
```

The CLI also adds the current output directory to the ABI lookup paths automatically.

## Minimal Examples

These are small examples that use the current syntax.

### Simple Contract

```csharp
contract hello {
	import Runtime;

	public ping(from:address): number {
		Runtime.expect(Runtime.isWitness(from), "witness failed");
		Runtime.log("ping");
		return 1;
	}
}
```

### Deploying A Contract Module

```csharp
contract sample_contract {
	public hello(): number {
		return 1;
	}
}

script deploy_sample {
	import Runtime;

	code(from:address) {
		Runtime.deployContract(from, sample_contract);
	}
}
```

### Creating A Token With The Current Signature

```csharp
token GHOST {
	property name:string = "Ghost";
}

script create_token {
	import Token;
	import Module;

	code(from:address) {
		Token.create(from, Module.getScript(GHOST), Module.getABI(GHOST));
	}
}
```

## Builtins

TOMB includes builtin methods implemented as embedded TOMB code, compiled into assembly, and injected during compilation.

Current builtin methods in this repository:

- `Math.sqrt`
- `String.toUpper`
- `String.toLower`
- `String.indexOf`
- `Random.seed`
- `Random.generate`

If you extend builtins locally, update the builtin source and the embedded builtin assembly together.

## More Documentation

Additional Phantasma smart-contract documentation:

- <https://phantasma.gitbook.io/main/development/tomb-lang>
