using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PhantasmaPhoenix.Protocol;
using PhantasmaPhoenix.VM;
using PhantasmaPhoenix.Core;
using Phantasma.Tomb.Compilers;

namespace TOMBLib.Tests.Contracts;

public class SwitchTest
{
    [Test]
    public void Switch()
    {
        var sourceCode =
            @"
contract test {
    public check(x:number): string {
        switch (x) {
            case 0: return ""zero"";
            case 1: return ""one"";
            case 2: return ""two"";
            default: return ""other"";
        }                  
     }}";

        var parser = new TombLangCompiler();
        var contract = parser.Process(sourceCode).First();

        var storage = new Dictionary<byte[], byte[]>(new ByteArrayComparer());

        var check = contract.abi.FindMethod("check");
        Assert.IsNotNull(check);

        // test different cases
        for (int i = -1; i <= 4; i++)
        {
            var vm = new TestVM(contract, storage, check);
            vm.Stack.Push(VMObject.FromObject(i));
            var state = vm.Execute();
            Assert.IsTrue(state == ExecutionState.Halt);
            var result = vm.Stack.Pop().AsString();

            string expected;
            switch (i)
            {
                case 0: expected = "zero"; break;
                case 1: expected = "one"; break;
                case 2: expected = "two"; break;
                default: expected = "other"; break;
            }

            Assert.IsTrue(result == expected);
        }
    }
}
