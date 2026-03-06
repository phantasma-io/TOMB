using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PhantasmaPhoenix.Protocol;
using PhantasmaPhoenix.Core;
using Phantasma.Tomb;
using Phantasma.Tomb.Compilers;

namespace TOMBLib.Tests.Contracts;

public class ReturnTests
{
	[Test]
	public void MultiResultsSimple()
	{
		var sourceCode =
			@"
contract test{                   
    public getStrings(): string* {
         return ""hello"";
         return ""world"";
    }
}";

		var parser = new TombLangCompiler();
		var contract = parser.Process(sourceCode).First();

		var storage = new Dictionary<byte[], byte[]>(new ByteArrayComparer());

		TestVM vm;

		var getStrings = contract.abi.FindMethod("getStrings");
		Assert.IsNotNull(getStrings);

		vm = new TestVM(contract, storage, getStrings);
		var result = vm.Execute();
		Assert.IsTrue(result == ExecutionState.Halt);

		Assert.IsTrue(vm.Stack.Count == 2);

		var obj = vm.Stack.Pop();
		var x = obj.AsString();
		Assert.IsTrue(x == "world");

		obj = vm.Stack.Pop();
		x = obj.AsString();
		Assert.IsTrue(x == "hello");
	}

	[Test]
	public void MultiResultsEarlyReturn()
	{
		var sourceCode =
			@"
contract test{                   
    public getStrings(): string* {
         return ""ok"";
         return;
         return ""bug""; // this line should not compile
    }
}";

		var parser = new TombLangCompiler();

		Assert.Catch<CompilerException>(() =>
		{
			var contract = parser.Process(sourceCode).First();
		});
	}

}
