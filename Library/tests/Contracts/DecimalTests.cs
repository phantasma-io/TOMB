using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using NUnit.Framework;
using PhantasmaPhoenix.Cryptography;
using PhantasmaPhoenix.Protocol;
using Phantasma.Core.Domain.Contract;
using PhantasmaPhoenix.VM;
using PhantasmaPhoenix.Core;
using Phantasma.Tomb;
using Phantasma.Tomb.Compilers;

namespace TOMBLib.Tests.Contracts;

public class DecimalTests
{
    [Test]
    public void DecimalsSimple()
    {
        var valStr = "2.4587";
        var val = decimal.Parse(valStr, CultureInfo.InvariantCulture);
        var decimals = 8;

        var sourceCode =
            "contract test{\n" +
            $"global amount: decimal<{decimals}>;\n" +
            "constructor(owner:address)	{\n" +
            "amount = " + valStr + ";\n}" +
            "public getValue():number {\n" +
            "return amount;\n}" +
            "public getLength():number {\n" +
            "return amount.decimals();\n}" +
            "}\n";

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
        var newVal = obj.AsNumber();
        var expectedVal = UnitConversion.ToBigInteger(val, (uint)decimals);

        Assert.IsTrue(newVal == expectedVal);

        // call getLength
        var getLength = contract.abi.FindMethod("getLength");
        Assert.IsNotNull(getLength);

        vm = new TestVM(contract, storage, getLength);
        result = vm.Execute();
        Assert.IsTrue(result == ExecutionState.Halt);

        Assert.IsTrue(storage.Count == 1);

        Assert.IsTrue(vm.Stack.Count == 1);

        obj = vm.Stack.Pop();
        var len = obj.AsNumber();

        Assert.IsTrue(len == decimals);
    }

    [Test]
    public void DecimalsPrecision()
    {
        var valStr = "2.4587";
        var val = decimal.Parse(valStr, CultureInfo.InvariantCulture);

        var sourceCode =
            "contract test{\n" +
            $"global amount: decimal<3>;\n" +
            "constructor(owner:address)	{\n" +
            "amount = " + valStr + ";\n}" +
            "}\n";

        var parser = new TombLangCompiler();

        try
        {
            var contract = parser.Process(sourceCode).First();
            Assert.Fail("should have throw compile error");
        }
        catch (CompilerException e)
        {
            Assert.IsTrue(e.Message.ToLower().Contains("precision"));
        }
    }
}
