using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using NUnit.Framework;
using PhantasmaPhoenix.Protocol;
using PhantasmaPhoenix.VM;
using PhantasmaPhoenix.Core;
using Phantasma.Tomb.Compilers;

namespace TOMBLib.Tests.Contracts;

public class ArrayTests
{
    [Test]
    public void StringArray()
    {
        var str = "hello";

        var sourceCode =
            @"contract test{
    public getStrings(): array<string> {
        local result:array<string> = {""A"", ""B"", ""C""};
        return result;
    }}";

        var parser = new TombLangCompiler();
        var contract = parser.Process(sourceCode).First();

        var storage = new Dictionary<byte[], byte[]>(new ByteArrayComparer());

        TestVM vm;

        var getStrings = contract.abi.FindMethod("getStrings");
        Assert.IsNotNull(getStrings);

        vm = new TestVM(contract, storage, getStrings);
        var result = vm.Execute();
        Assert.IsTrue(result == ExecutionState.Halt);

        Assert.IsTrue(vm.Stack.Count == 1);

        var obj = vm.Stack.Pop();

        var array = obj.AsArray(VMType.String);
        Assert.IsTrue(array.Length == 3);
    }

    [Test]
    public void ArraySimple()
    {
        // TODO make other tests also use multiline strings for source code, much more readable...
        var sourceCode = @"
contract arrays {
    import Array;

	public test(x:number):number {
		local my_array: array<number>;
		my_array[1] = x;
		return Array.length(my_array);
	}
}
";

        var parser = new TombLangCompiler();
        var contract = parser.Process(sourceCode).First();

        var storage = new Dictionary<byte[], byte[]>(new ByteArrayComparer());

        TestVM vm;

        var test = contract.abi.FindMethod("test");
        Assert.IsNotNull(test);

        vm = new TestVM(contract, storage, test);
        vm.Stack.Push(VMObject.FromObject(5));
        var state = vm.Execute();
        Assert.IsTrue(state == ExecutionState.Halt);

        var result = vm.Stack.Pop().AsNumber();
        Assert.IsTrue(result == 1);
    }

    [Test]
    public void ArrayVariableIndex()
    {
        // TODO make other tests also use multiline strings for source code, much more readable...
        var sourceCode = @"
contract arrays {
	public test(x:number, idx:number):number {
		local my_array: array<number>;
		my_array[idx] = x;
		local num:number = my_array[idx];
		return num + 1;
	}
}
";

        var parser = new TombLangCompiler();
        var contract = parser.Process(sourceCode).First();

        var storage = new Dictionary<byte[], byte[]>(new ByteArrayComparer());

        TestVM vm;

        var test = contract.abi.FindMethod("test");
        Assert.IsNotNull(test);

        vm = new TestVM(contract, storage, test);
        vm.Stack.Push(VMObject.FromObject(2));
        vm.Stack.Push(VMObject.FromObject(5));
        var state = vm.Execute();
        Assert.IsTrue(state == ExecutionState.Halt);

        var result = vm.Stack.Pop().AsNumber();
        Assert.IsTrue(result == 6);
    }

    [Test]
    public void ArrayVariablePop()
    {
	    // TODO make other tests also use multiline strings for source code, much more readable...
	    var sourceCode = @"
contract arrays {
	import Array;
	public test(x:number, idx:number):number {
		local my_array: array<number> = {1};

		for (local i = 0; i < 2; i+=1) {
		    my_array[i] = x + i;
		}

		local num : number = Array.pop<number>(my_array);
		return num;
	}
}
";

	    var parser = new TombLangCompiler();
	    var contract = parser.Process(sourceCode).First();

	    var storage = new Dictionary<byte[], byte[]>(new ByteArrayComparer());

	    TestVM vm;

	    var test = contract.abi.FindMethod("test");
	    Assert.IsNotNull(test);

	    vm = new TestVM(contract, storage, test);
	    vm.Stack.Push(VMObject.FromObject(2));
	    vm.Stack.Push(VMObject.FromObject(2));
	    var state = vm.Execute();
	    Assert.IsTrue(state == ExecutionState.Halt);

	    var result = vm.Stack.Pop().AsNumber();
		    Assert.IsTrue(result == 3, "Expected 3, got " + result);
    }


    [Test]
    public void ArraySizeTest()
    {
        // TODO make other tests also use multiline strings for source code, much more readable...
        var sourceCode = @"
contract arrays {
	import Array;
	public test(x:number):number {
		local my_array: array<number> = {1};
		for (local i = 0; i < 10000; i+=1) {
		    my_array[i] = x;
		}
		return Array.length(my_array);
	}
}
";

        var parser = new TombLangCompiler();
        var contract = parser.Process(sourceCode).First();

        var storage = new Dictionary<byte[], byte[]>(new ByteArrayComparer());

        TestVM vm;

        var test = contract.abi.FindMethod("test");
        Assert.IsNotNull(test);

        vm = new TestVM(contract, storage, test);
        vm.Stack.Push(VMObject.FromObject(1));
        var state = vm.Execute();
        Assert.IsTrue(state == ExecutionState.Halt);

        var result = vm.Stack.Pop().AsNumber();
        Assert.IsTrue(result == 10000);
    }

    [Test]
    public void ArraySumAllMembers()
    {
	    // TODO make other tests also use multiline strings for source code, much more readable...
	    var sourceCode = @"
contract arrays {
	import Array;
	public test(x:number):number {
		local my_array: array<number> = {1};
		for (local i = 0; i < 100; i+=1) {
		    my_array[i] = i;
		}

		local total = 0;
		for(local i = 0; i < 100; i+=1) {
		    total += my_array[i];
		}
		return total;
	}
}
";

	    var parser = new TombLangCompiler();
	    var contract = parser.Process(sourceCode).First();

	    var storage = new Dictionary<byte[], byte[]>(new ByteArrayComparer());

	    TestVM vm;

	    var test = contract.abi.FindMethod("test");
	    Assert.IsNotNull(test);

	    vm = new TestVM(contract, storage, test);
	    vm.Stack.Push(VMObject.FromObject(1));
	    var state = vm.Execute();
	    Assert.IsTrue(state == ExecutionState.Halt);

        BigInteger sum = Enumerable.Range(0, 100).Sum();

	    var result = vm.Stack.Pop().AsNumber();
	    Assert.AreEqual(result, sum);
    }
}
