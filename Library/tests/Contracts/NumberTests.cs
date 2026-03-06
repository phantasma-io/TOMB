using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PhantasmaPhoenix.Cryptography;
using PhantasmaPhoenix.Protocol;
using Phantasma.Core.Domain.Contract;
using PhantasmaPhoenix.VM;
using PhantasmaPhoenix.Core;
using Phantasma.Tomb.Compilers;

namespace TOMBLib.Tests.Contracts;

public class NumberTests
{
    [Test]
    public void TestCounter()
    {
        var sourceCode =
            @"
contract test {
    global counter: number;
    
    constructor(owner:address)	{
        counter= 0; 
    }
    
    public increment() {
        if (counter < 0) {
            throw ""invalid state"";
        }   
                
        counter++;
    }
}";

        var parser = new TombLangCompiler();
        var contract = parser.Process(sourceCode).First();

        var storage = new Dictionary<byte[], byte[]>(new ByteArrayComparer());

        TestVM vm;

        var constructor = contract.abi.FindMethod(SmartContract.ConstructorName);
        Assert.IsNotNull(constructor);

        var keys = PhantasmaKeys.Generate();

        // call constructor
        vm = new TestVM(contract, storage, constructor);
        vm.Stack.Push(VMObject.FromObject(keys.Address));
        var result = vm.Execute();
        Assert.IsTrue(result == ExecutionState.Halt);

        Assert.IsTrue(storage.Count == 1);

        // call increment
        var increment = contract.abi.FindMethod("increment");
        Assert.IsNotNull(increment);

        vm = new TestVM(contract, storage, increment);
        result = vm.Execute();
        Assert.IsTrue(result == ExecutionState.Halt);

        Assert.IsTrue(storage.Count == 1);
    }

    [Test]
    public void MinMax()
    {
        var sourceCode =
            @"
contract test{
    import Math;
    public calculate(a:number, b:number):number {
        return Math.min(a, b);
    }
}";

        var parser = new TombLangCompiler();
        var contract = parser.Process(sourceCode).First();

        var storage = new Dictionary<byte[], byte[]>(new ByteArrayComparer());

        TestVM vm;

        // call increment
        var calculate = contract.abi.FindMethod("calculate");
        Assert.IsNotNull(calculate);

        vm = new TestVM(contract, storage, calculate);
        vm.Stack.Push(VMObject.FromObject(5));
        vm.Stack.Push(VMObject.FromObject(2));
        var state = vm.Execute();
        Assert.IsTrue(state == ExecutionState.Halt);

        var result = vm.Stack.Pop().AsNumber();
        Assert.IsTrue(result == 2);
    }

    [Test]
    public void UpdateNumberMethod()
    {
        string[] sourceCode = new string[]
        {
            "token GHOST {",
            "	global _infuseMultiplier:number;",
            "	property name:string = \"test\";",
            "	property infuseMultiplier:number = _infuseMultiplier;",
            "	constructor (owner:address) { _infuseMultiplier = 1;	}",
            "	public updateInfuseMultiplier(infuseMultiplier:number) 	{	_infuseMultiplier = infuseMultiplier;	}",
            "}"
        };

        var parser = new TombLangCompiler();
        var contract = parser.Process(sourceCode).First();

        var storage = new Dictionary<byte[], byte[]>(new ByteArrayComparer());

        TestVM vm;

        var constructor = contract.abi.FindMethod(SmartContract.ConstructorName);
        Assert.IsNotNull(constructor);

        var keys = PhantasmaKeys.Generate();

        vm = new TestVM(contract, storage, constructor);
        vm.Stack.Push(VMObject.FromObject(keys.Address));
        var result = vm.Execute();
        Assert.IsTrue(result == ExecutionState.Halt);

        Assert.IsTrue(storage.Count == 1);

        // call updateInfuseMultiplier
        var updateValue = contract.abi.FindMethod("updateInfuseMultiplier");
        Assert.IsNotNull(updateValue);

        vm = new TestVM(contract, storage, updateValue);
        vm.Stack.Push(VMObject.FromObject("4"));
        result = vm.Execute();
        Assert.IsTrue(result == ExecutionState.Halt);

        Assert.IsTrue(storage.Count == 1);

        // call getInfuseMultiplier
        var getValue = contract.abi.FindMethod("getInfuseMultiplier");
        Assert.IsNotNull(getValue);

        vm = new TestVM(contract, storage, getValue);
        result = vm.Execute();
        Assert.IsTrue(result == ExecutionState.Halt);

        Assert.IsTrue(vm.Stack.Count == 1);

        var obj = vm.Stack.Pop();
        var newVal = obj.AsNumber();
        var expectedVal = 4;

        Assert.IsTrue(newVal == expectedVal);
    }
    
    [Test]
    public void RandomNumber()
    {
        var str = "hello";

        var sourceCode =
            @"
contract test {
	import Random;
	import Hash;
	import Runtime;

	global my_state: number;

	public mutateState():number
	{
        // use the current transaction hash to provide a random seed. This makes the result deterministic during node consensus
        // 	optionally we can use other value, depending on your needs, eg: Random.seed(16676869); 
        local tx_hash:hash = Runtime.transactionHash();
        local mySeed:number = tx_hash.toNumber();
		Random.seed(mySeed);
		my_state = Random.generate() % 10; // Use modulus operator to constrain the random number to a specific range
		return my_state;
	}

}";

        var parser = new TombLangCompiler();
        var contract = parser.Process(sourceCode).First();

        var storage = new Dictionary<byte[], byte[]>(new ByteArrayComparer());

        TestVM vm;

        var mutateState = contract.abi.FindMethod("mutateState");
        Assert.IsNotNull(mutateState);

        vm = new TestVM(contract, storage, mutateState);
        var result = vm.Execute();
        Assert.IsTrue(result == ExecutionState.Halt);

        Assert.IsTrue(storage.Count == 2);

        Assert.IsTrue(vm.Stack.Count == 1);

        var obj = vm.Stack.Pop();
        var state = obj.AsNumber();

        var expectedState = -4;

        Assert.IsTrue(state == expectedState);
    }
}
