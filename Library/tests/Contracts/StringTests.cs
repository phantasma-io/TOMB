using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PhantasmaPhoenix.Cryptography;
using Phantasma.Core.Domain.Contract;
using PhantasmaPhoenix.Protocol;
using PhantasmaPhoenix.VM;
using PhantasmaPhoenix.Core;
using Phantasma.Tomb.Compilers;

namespace TOMBLib.Tests.Contracts;

public class StringTests
{
	[Test]
	public void StringsSimple()
	{
		var str = "hello";

		var sourceCode =
			"contract test{\n" +
			"global name: string;\n" +
			"constructor(owner:address)	{\n" +
			"name= \"" + str + "\";\n}" +
			"public getLength():number {\n" +
			"return name.length();\n" +
			"}}\n";

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

		// call getLength
		var getLength = contract.abi.FindMethod("getLength");
		Assert.IsNotNull(getLength);

		vm = new TestVM(contract, storage, getLength);
		result = vm.Execute();
		Assert.IsTrue(result == ExecutionState.Halt);

		Assert.IsTrue(storage.Count == 1);

		Assert.IsTrue(vm.Stack.Count == 1);

		var obj = vm.Stack.Pop();
		var len = obj.AsNumber();

		var expectedLength = str.Length;

		Assert.IsTrue(len == expectedLength);
	}

	[Test]
	public void UpdateStringMethod()
	{
		string[] sourceCode = new string[]
		{
			"token TEST  {",
			"property name:string = \"Unit test\";",
			"   global _feesSymbol:string;",
			$"  property feesSymbol:string = _feesSymbol;",
			"   constructor(owner:address)	{",
			"       _feesSymbol = \"KCAL\";",
			"}",
			"public updateFeesSymbol(feesSymbol:string) {",
			"   _feesSymbol= feesSymbol;",
			"}",
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

		// call updateFeesSymbol
		var updateValue = contract.abi.FindMethod("updateFeesSymbol");
		Assert.IsNotNull(updateValue);

		vm = new TestVM(contract, storage, updateValue);
		vm.Stack.Push(VMObject.FromObject("SOUL"));
		result = vm.Execute();
		Assert.IsTrue(result == ExecutionState.Halt);

		Assert.IsTrue(storage.Count == 1);

		// call getFeesSymbol
		var getValue = contract.abi.FindMethod("getFeesSymbol");
		Assert.IsNotNull(getValue);

		vm = new TestVM(contract, storage, getValue);
		result = vm.Execute();
		Assert.IsTrue(result == ExecutionState.Halt);

		Assert.IsTrue(vm.Stack.Count == 1);

		var obj = vm.Stack.Pop();
		var newVal = obj.AsString();
		var expectedVal = "SOUL";

		Assert.IsTrue(newVal == expectedVal);
	}

	[Test]
	public void StringManipulation()
	{
		var sourceCode = @"
contract arrays {
    import Array;

	public test(s:string, idx:number):string {
		local my_array: array<number>;
		my_array = s.toArray();
        my_array[idx] = 42; // replace char in this index with an asterisk (ascii table 42)
		local result:string = String.fromArray(my_array);
		return result;
	}

	public toUpper(s:string):string
	{
		local my_array: array<number>;

		// extract chars from string into an array
		my_array = s.toArray();

		local length :number = Array.length(my_array);
		local idx :number = 0;

		while (idx < length) {
			local ch : number = my_array[idx];

			if (ch >= 97) {
				if (ch <= 122) {
					my_array[idx] = ch - 32;
				}
			}

			idx += 1;
		}

		// convert the array back into a unicode string
		local result:string = String.fromArray(my_array);
		return result;
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
		vm.Stack.Push(VMObject.FromObject("ABCD"));
		var state = vm.Execute();
		Assert.IsTrue(state == ExecutionState.Halt);

		var result = vm.Stack.Pop().AsString();
		Assert.IsTrue(result == "AB*D");

		var toUpper = contract.abi.FindMethod("toUpper");
		Assert.IsNotNull(toUpper);

		vm = new TestVM(contract, storage, toUpper);
		vm.Stack.Push(VMObject.FromObject("abcd"));
		state = vm.Execute();
		Assert.IsTrue(state == ExecutionState.Halt);

		result = vm.Stack.Pop().AsString();
		Assert.IsTrue(result == "ABCD");
	}


	[Test]
	public void StringParameters()
	{
		var sourceCode = @"
contract handlerstring {
	import String;

	public joinStrings(s:string, idx:number, k:string, y:string):string {
		return s + k + y + idx;
	}
}
";

		var parser = new TombLangCompiler();
		var contract = parser.Process(sourceCode).First();

		var storage = new Dictionary<byte[], byte[]>(new ByteArrayComparer());

		TestVM vm;

		var test = contract.abi.FindMethod("joinStrings");
		Assert.IsNotNull(test);

		vm = new TestVM(contract, storage, test);
		vm.Stack.Push(VMObject.FromObject("IJKL"));
		vm.Stack.Push(VMObject.FromObject("EFGH"));
		vm.Stack.Push(VMObject.FromObject(2));
		vm.Stack.Push(VMObject.FromObject("ABCD"));
		var state = vm.Execute();
		Assert.IsTrue(state == ExecutionState.Halt);

		var result = vm.Stack.Pop().AsString();
		Assert.That(result, Is.EqualTo("ABCDEFGHIJKL2"));
	}
}
