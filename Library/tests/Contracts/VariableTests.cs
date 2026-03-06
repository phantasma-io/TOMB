using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PhantasmaPhoenix.Cryptography;
using PhantasmaPhoenix.Protocol;
using PhantasmaPhoenix.VM;
using PhantasmaPhoenix.Core;
using Phantasma.Tomb.Compilers;

namespace TOMBLib.Tests.Contracts;

public class VariableTests
{
    [Test]
    public void TypeOf()
    {
        var sourceCode = new string[]
        {
            "contract test{",
            "public returnType() : type	{",
            "return $TYPE_OF(string);",
            "}}"
        };

        var parser = new TombLangCompiler();
        var contract = parser.Process(sourceCode).First();

        var storage = new Dictionary<byte[], byte[]>(new ByteArrayComparer());

        TestVM vm;

        var keys = PhantasmaKeys.Generate();

        // call returnType
        var returnType = contract.abi.FindMethod("returnType");
        Assert.IsNotNull(returnType);

        vm = new TestVM(contract, storage, returnType);
        var result = vm.Execute();
        Assert.IsTrue(result == ExecutionState.Halt);

        Assert.IsTrue(storage.Count == 0);

        Assert.IsTrue(vm.Stack.Count == 1);

        var obj = vm.Stack.Pop();
        var vmType = obj.AsEnum<VMType>();

        Assert.IsTrue(vmType == VMType.String);
    }
    
    [Test]
    public void TypeInferenceInVarDecls()
    {
        var sourceCode =
            @"
contract test{                   
    public calculate():string {
         local a = ""hello "";
         local b = ""world"";
        return a + b;
    }
}";

        var parser = new TombLangCompiler();
        var contract = parser.Process(sourceCode).First();

        var storage = new Dictionary<byte[], byte[]>(new ByteArrayComparer());

        TestVM vm;

        var method = contract.abi.FindMethod("calculate");
        Assert.IsNotNull(method);

        vm = new TestVM(contract, storage, method);
        var result = vm.Execute();
        Assert.IsTrue(result == ExecutionState.Halt);

        Assert.IsTrue(vm.Stack.Count == 1);

        var obj = vm.Stack.Pop();
        var str = obj.AsString();

        Assert.IsTrue(str == "hello world");
    }
}
