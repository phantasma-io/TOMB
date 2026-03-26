using Phantasma.Tomb.Compilers;
using PhantasmaPhoenix.Core;
using PhantasmaPhoenix.Protocol;
using PhantasmaPhoenix.VM;

namespace TOMBLib.Tests.Contracts;

public class TokenTests
{
	[Test]
	public void GetOwner()
	{
		var sourceCode = @"
contract test{

import Token;

public testMethod() : address {
    return Token.getOwner(""SOUL"");
    }
}";

		var parser = new TombLangCompiler();
		var contract = parser.Process(sourceCode).First();

		var storage = new Dictionary<byte[], byte[]>(new ByteArrayComparer());

		var method = contract.abi.FindMethod("testMethod");
		Assert.IsNotNull(method);

		var vm = new TestVM(contract, storage, method);
		var result = vm.Execute();
		Assert.IsTrue(result == ExecutionState.Halt);

		Assert.IsTrue(vm.Stack.Count == 1);

		// Token.getOwner intentionally exposes the current owner lookup for a symbol.
		var owner = vm.Stack.Pop().AsAddress();
		Assert.IsTrue(owner == TestVM.GetTokenOwnerForSymbol("SOUL"));
	}
}
