namespace TOMBLib.Tests.CodeGen;

using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PhantasmaPhoenix.Protocol;
using PhantasmaPhoenix.Cryptography;
using PhantasmaPhoenix.VM;
using PhantasmaPhoenix.Core;
using Phantasma.Tomb.Compilers;

public class CodeGeneratorTests
{
    [Test]
    public void AutoCasts()
    {
        var sourceCode =
            @"
contract test {
    import Address;

    public castTest(x:address): string {
        return Address.toString(x);
    }
}";

        var parser = new TombLangCompiler();
        var contract = parser.Process(sourceCode).First();

        var storage = new Dictionary<byte[], byte[]>(new ByteArrayComparer());

        var cast = contract.abi.FindMethod("castTest");
        Assert.IsNotNull(cast);

        var expected = "P2K7GyVMC3f9XxKRji5gfg91WvutoHs2RyB6KzQxuaAUUeo";
        var addr = Address.Parse(expected, true);

        var vm = new TestVM(contract, storage, cast);
        vm.Stack.Push(VMObject.FromObject(addr));
        var result = vm.Execute();
        Assert.IsTrue(result == ExecutionState.Halt);

        Assert.IsTrue(vm.Stack.Count == 1);
        var val = vm.Stack.Pop().AsString();
        Assert.IsTrue(val == expected);
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
