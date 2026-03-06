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

public class PropertiesTests
{
	[Test]
	public void Properties()
	{
		string[] sourceCode = new string[]
		{
			"token TEST  {",
			"property name:string = \"Unit test\";",
			"   global _feesSymbol:string;",
			$"  property feesSymbol:string = _feesSymbol;",
			"   constructor(owner:address)	{",
			"       _feesSymbol = \"KCAL\";",
			"}}"
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

		// call getFeesSymbol
		var getValue = contract.abi.FindMethod("getFeesSymbol");
		Assert.IsNotNull(getValue);

		vm = new TestVM(contract, storage, getValue);
		result = vm.Execute();
		Assert.IsTrue(result == ExecutionState.Halt);

		Assert.IsTrue(vm.Stack.Count == 1);

		var obj = vm.Stack.Pop();
		var newVal = obj.AsString();
		var expectedVal = "KCAL";

		Assert.IsTrue(newVal == expectedVal);
	}
}
