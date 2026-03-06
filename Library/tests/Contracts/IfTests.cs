using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PhantasmaPhoenix.Protocol;
using PhantasmaPhoenix.VM;
using PhantasmaPhoenix.Core;
using Phantasma.Tomb.Compilers;

namespace TOMBLib.Tests.Contracts;

public class IfTests
{
    [Test]
    public void IfChained()
    {
        var sourceCode =
            @"
contract test {
    public sign(x:number): number {
        if (x > 0)
        {
            return 1;
        }
        else
        if (x < 0)
        {
            return -1;
        }
        else
        {
            return 0;
        }
    }
}";

        var parser = new TombLangCompiler();
        var contract = parser.Process(sourceCode).First();

        var storage = new Dictionary<byte[], byte[]>(new ByteArrayComparer());

        var countStuff = contract.abi.FindMethod("sign");
        Assert.IsNotNull(countStuff);

        var vm = new TestVM(contract, storage, countStuff);
        vm.Stack.Push(VMObject.FromObject(-3));
        var result = vm.Execute();
        Assert.IsTrue(result == ExecutionState.Halt);

        Assert.IsTrue(vm.Stack.Count == 1);
        var val = vm.Stack.Pop().AsNumber();
        Assert.IsTrue(val == -1);
    }

    [Test]
    public void IfWithOr()
    {
        var sourceCode =
            @"
contract test {
    public check(x:number, y:number): bool {
            return (x > 0) && (y < 0);
    }
}";

        var parser = new TombLangCompiler();
        var contract = parser.Process(sourceCode).First();

        var storage = new Dictionary<byte[], byte[]>(new ByteArrayComparer());

        var countStuff = contract.abi.FindMethod("check");
        Assert.IsNotNull(countStuff);

        var vm = new TestVM(contract, storage, countStuff);
        // NOTE - due to using a stack, we're pushing the second argument first (y), then the first argument (y)
        vm.Stack.Push(VMObject.FromObject(-3));
        vm.Stack.Push(VMObject.FromObject(3));
        var result = vm.Execute();
        Assert.IsTrue(result == ExecutionState.Halt);

        Assert.IsTrue(vm.Stack.Count == 1);
        var val = vm.Stack.Pop().AsBool();
        Assert.IsTrue(val);

        vm = new TestVM(contract, storage, countStuff);
        // NOTE - here we invert the order, in this case should fail due to the condition in the contract inside check()
        vm.Stack.Push(VMObject.FromObject(3));
        vm.Stack.Push(VMObject.FromObject(-3));
        result = vm.Execute();
        Assert.IsTrue(result == ExecutionState.Halt);

        Assert.IsTrue(vm.Stack.Count == 1);
        val = vm.Stack.Pop().AsBool();
        Assert.IsFalse(val);
    }
}
