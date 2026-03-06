using System.Collections.Generic;
using System.Linq;
using PhantasmaPhoenix.Core;
using PhantasmaPhoenix.Protocol;
using PhantasmaPhoenix.VM;
using Phantasma.Tomb.Compilers;

namespace TOMBLib.Tests.Contracts;

public class ABITests
{
    [Test]
    public void ABITest()
    {
        var sourceCode = @"
token MTEST {
	public getName(): string {
		return ""Unit test"";
	}
}

contract mytests {
	import Module;
	public test():number {
		local myABI = Module.getABI(MTEST);
		return 0;
	}
}
";

        var parser = new TombLangCompiler();
        var contract = parser.Process(sourceCode).First(x => x.Name == "mytests");

        var storage = new Dictionary<byte[], byte[]>(new ByteArrayComparer());

        TestVM vm;

        var testMethod = contract.abi.FindMethod("test");
        Assert.IsNotNull(testMethod);

        vm = new TestVM(contract, storage, testMethod);
        var result = vm.Execute();
        Assert.IsTrue(result == ExecutionState.Halt);

        Assert.IsTrue(vm.Stack.Count == 1);

        var obj = vm.Stack.Pop();
        Assert.IsTrue(obj.AsNumber() == 0);
    }
}
