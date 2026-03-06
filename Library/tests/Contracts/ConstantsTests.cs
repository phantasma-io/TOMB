using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PhantasmaPhoenix.Protocol;
using PhantasmaPhoenix.Core;
using Phantasma.Tomb.Compilers;

namespace TOMBLib.Tests.Contracts;

public class ConstantsTests
{
	[Test]
	public void Constants()
	{
		var VAL_A = 30;
		var VAL_B = 4;

		string[] sourceCode = new string[]
		{
			$"const VAL_A : number = {VAL_A};",
			"contract test{",
			$"const VAL_B : number = {VAL_B};",
			"public getValue() : number	{",
			"return VAL_A + VAL_B;}",
			"}"
		};

		var expectedVal = VAL_A + VAL_B;

		var parser = new TombLangCompiler();
		var contract = parser.Process(sourceCode).First();

		var storage = new Dictionary<byte[], byte[]>(new ByteArrayComparer());

		TestVM vm;

		var getValue = contract.abi.FindMethod("getValue");
		Assert.IsNotNull(getValue);

		vm = new TestVM(contract, storage, getValue);
		var result = vm.Execute();
		Assert.IsTrue(result == ExecutionState.Halt);

		Assert.IsTrue(vm.Stack.Count == 1);

		var obj = vm.Stack.Pop();
		var newVal = obj.AsNumber();

		Assert.IsTrue(newVal == expectedVal);
	}
}
