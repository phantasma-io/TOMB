using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using NUnit.Framework;
using PhantasmaPhoenix.Cryptography;
using PhantasmaPhoenix.Protocol;
using PhantasmaPhoenix.VM;
using PhantasmaPhoenix.Core;
using Phantasma.Tomb;
using Phantasma.Tomb.Compilers;

namespace TOMBLib.Tests.Contracts;

public class MethodTests
{
    [Test]
    public void DuplicatedMethodNames()
    {
        var sourceCode =
            @"
contract test {
    public testme(x:number): number {
         return 5;
    }

    public testme(x:number): string {
         return ""zero"";
     }}";

        var parser = new TombLangCompiler();

        Assert.Catch<CompilerException>(() =>
        {
            var contract = parser.Process(sourceCode).First();
        });
    }

    [Test]
    public void TooManyArgs()
    {
        var sourceCode = @"
contract arrays {
    import Array;

	public mycall(x:number):number {
        return x+ 1;
    }

	public something():number {
		return this.mycall(2, 3); // extra argument here, should not compile
	}
}
";

        var parser = new TombLangCompiler();

        Assert.Catch<CompilerException>(() =>
        {
            var contract = parser.Process(sourceCode).First();
        });
    }

    [Test]
    public void TestLocalCallViaThis()
    {
        var sourceCode =
            @"
contract test {
    private sum(x:number, y:number) : number
    { return x + y; }

    public fetch(val:number) : number
    {
        return this.sum(val, 1);
    }
}";

        var parser = new TombLangCompiler();
        var contract = parser.Process(sourceCode).First();

        var storage = new Dictionary<byte[], byte[]>(new ByteArrayComparer());

        TestVM vm;

        var keys = PhantasmaKeys.Generate();

        // call fetch
        var fetch = contract.abi.FindMethod("fetch");
        Assert.IsNotNull(fetch);

        vm = new TestVM(contract, storage, fetch);
        vm.Stack.Push(VMObject.FromObject(10));
        var state = vm.Execute();
        Assert.IsTrue(state == ExecutionState.Halt);
        var result = vm.Stack.Pop().AsNumber();

        Assert.IsTrue(result == 11);
    }

    [Test]
    public void TestContractCallViaCallMethod()
    {
        var sourceCode =
            @"
contract test {
    import Call;

    private sum(x:number, y:number) : number
    { return x + y; }

    public fetch(val:number) : number
    {
        return Call.method<number>(sum, val, 1);
    }
}";

        var parser = new TombLangCompiler();
        var contract = parser.Process(sourceCode).First();

        var storage = new Dictionary<byte[], byte[]>(new ByteArrayComparer());

        TestVM vm;

        var keys = PhantasmaKeys.Generate();

        // call fetch
        var fetch = contract.abi.FindMethod("fetch");
        Assert.IsNotNull(fetch);

        vm = new TestVM(contract, storage, fetch);
        vm.Stack.Push(VMObject.FromObject(10));
        var state = vm.Execute();
        Assert.IsTrue(state == ExecutionState.Halt);
        var result = vm.Stack.Pop().AsNumber();

        Assert.IsTrue(result == 11);
    }

    [Test]
    public void TestContractLengthViaParamNameWithNumber()
    {
        var sourceCode =
            @"
contract test {
    import Call;

    private countLetters(x1:string) : number
    {
        if ( x1.length() == 0 ) {
            return 0;
        }

        return x1.length();
    }

    public fetch(val:string) : number
    {
        return this.countLetters(val);
    }
}";

        var parser = new TombLangCompiler();
        var contract = parser.Process(sourceCode).First();

        var storage = new Dictionary<byte[], byte[]>(new ByteArrayComparer());

        TestVM vm;

        var keys = PhantasmaKeys.Generate();

        // call fetch
        var fetch = contract.abi.FindMethod("fetch");
        Assert.IsNotNull(fetch);

        vm = new TestVM(contract, storage, fetch);
        vm.Stack.Push(VMObject.FromObject("helloworld"));
        var state = vm.Execute();
        Assert.IsTrue(state == ExecutionState.Halt);
        var result = vm.Stack.Pop().AsNumber();
        BigInteger expected = "helloworld".Length;


        Assert.AreEqual(expected, result);
    }
}
