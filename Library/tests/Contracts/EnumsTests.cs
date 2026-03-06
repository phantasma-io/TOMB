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

public class EnumsTests
{
	public enum MyEnum
	{
		A,
		B,
		C,
	}

	[Test]
	public void Enums()
	{
		string[] sourceCode = new string[]
		{
			"enum MyEnum { A, B, C}",
			"contract test{",
			$"global state: MyEnum;",
			"constructor(owner:address)	{",
			"state = MyEnum.B;}",
			"public getValue():MyEnum {",
			"return state;}",
			"public isSet(val:MyEnum):bool {",
			"return state.isSet(val);}",
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

		// call getVal
		var getValue = contract.abi.FindMethod("getValue");
		Assert.IsNotNull(getValue);

		vm = new TestVM(contract, storage, getValue);
		result = vm.Execute();
		Assert.IsTrue(result == ExecutionState.Halt);

		Assert.IsTrue(storage.Count == 1);

		Assert.IsTrue(vm.Stack.Count == 1);

		var obj = vm.Stack.Pop();
		var newVal = obj.AsEnum<MyEnum>();
		var expectedVal = MyEnum.B;

		Assert.IsTrue(newVal == expectedVal);
	}

	private enum ItemKind
	{
		Sword,
		Shield,
		Knife
	}

	[Test]
	public void TestEnumsWithValues()
	{
		string[] sourceCode = new string[]
		{
			"enum ItemKind { Sword=0, Shield=1, Knife=2}",
			"contract test{",
			$"global state: ItemKind;",
			"constructor(owner:address)	{",
			"state = ItemKind.Sword;}",
			"public getValue():ItemKind {",
			"return state;}",
			"public isSet(val:ItemKind):bool {",
			"return state.isSet(val);}",
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

		// call getVal
		var getValue = contract.abi.FindMethod("getValue");
		Assert.IsNotNull(getValue);

		vm = new TestVM(contract, storage, getValue);
		result = vm.Execute();
		Assert.IsTrue(result == ExecutionState.Halt);

		Assert.IsTrue(storage.Count == 1);

		Assert.IsTrue(vm.Stack.Count == 1);

		var obj = vm.Stack.Pop();
		var newVal = obj.AsEnum<ItemKind>();
		var expectedVal = ItemKind.Sword;

		Assert.IsTrue(newVal == expectedVal);
	}
}
