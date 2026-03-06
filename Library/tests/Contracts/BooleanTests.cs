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

public class BooleanTests
{
	[Test]
	public void Bools()
	{
		string[] sourceCode = new string[]
		{
			"token TEST {",
			"global _contractPaused:bool;",
			"property name: string = \"Ghost\";	",
			"   constructor(owner:address)	{",
			"       _contractPaused= false;",
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
	}

}
