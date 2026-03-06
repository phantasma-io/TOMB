using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PhantasmaPhoenix.Protocol;
using PhantasmaPhoenix.Core;
using Phantasma.Tomb.Compilers;

namespace TOMBLib.Tests.Contracts;

public class ForTests
{
    [Test]
    public void ForLoop()
    {
        var sourceCode =
            @"
contract test {
    public countStuff():number {
        local x:number = 0;
        for (local i=0; i<9; i+=1)
        {
            x+=2;
        }
        return x;
    }
}";

        var parser = new TombLangCompiler();
        var contract = parser.Process(sourceCode).First();

        var storage = new Dictionary<byte[], byte[]>(new ByteArrayComparer());

        var countStuff = contract.abi.FindMethod("countStuff");
        Assert.IsNotNull(countStuff);

        var vm = new TestVM(contract, storage, countStuff);
        var result = vm.Execute();
        Assert.IsTrue(result == ExecutionState.Halt);

        Assert.IsTrue(vm.Stack.Count == 1);
        var val = vm.Stack.Pop().AsNumber();
        Assert.IsTrue(val == 18);
    }
}
