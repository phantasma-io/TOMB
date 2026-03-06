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

public class AddressTests
{
	[Test]
	public void QueryMethodAddress()
	{
		string[] sourceCode = new string[]
		{
			"token TEST  {",
			"property name:string = \"Unit test\";",
			"   global _feesAddress:address;",
			$"  property feesAddress:address = _feesAddress;",
			"   constructor(owner:address)	{",
			"       _feesAddress = @P2KEYzWsbrMbPNtW1tBzzDKeYxYi4hjzpx4EfiyRyaoLkMM;",
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

		// call getFeesAddress
		var getValue = contract.abi.FindMethod("getFeesAddress");
		Assert.IsNotNull(getValue);

		vm = new TestVM(contract, storage, getValue);
		result = vm.Execute();
		Assert.IsTrue(result == ExecutionState.Halt);

		Assert.IsTrue(vm.Stack.Count == 1);

		var obj = vm.Stack.Pop();
		var newVal = obj.AsString();
		var expectedVal = "P2KEYzWsbrMbPNtW1tBzzDKeYxYi4hjzpx4EfiyRyaoLkMM";

		Assert.IsTrue(newVal == expectedVal);
	}

}
