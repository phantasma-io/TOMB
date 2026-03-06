using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using PhantasmaPhoenix.Core;
using PhantasmaPhoenix.Protocol;
using PhantasmaPhoenix.VM;
using Phantasma.Tomb.Compilers;

namespace TOMBLib.Tests.Contracts;

public class StructTests
{
    public struct MyLocalStruct
    {
        public string name;
        public BigInteger age;
    }

    [Test]
    public void TestStructChanging()
    {
        var sourceCode =
            @"
struct MyLocalStruct {
    name:string;
    age:number;
}

contract test{
    import Struct;
    public testMyStruct (name:string, age:number) : MyLocalStruct {
        local myStruct : MyLocalStruct = Struct.MyLocalStruct(name, age);
        if ( myStruct.age == 10 ) {
            myStruct.age = 20;
        }
        return myStruct;
    }
}";

        var parser = new TombLangCompiler();
        var contract = parser.Process(sourceCode).First();

        //File.WriteAllText(@"c:\code\output.asm", contract.asm); // for debugging

        var storage = new Dictionary<byte[], byte[]>(new ByteArrayComparer());

        TestVM vm;
        var method = contract.abi.FindMethod("testMyStruct");
        // Age 10
        var myStruct = new MyLocalStruct();
        myStruct.name = "John";
        myStruct.age = 10;
        vm = new TestVM(contract, storage, method);
        vm.Stack.Push(VMObject.FromObject(myStruct.age));
        vm.Stack.Push(VMObject.FromObject(myStruct.name));
        var result = vm.Execute();
        Assert.IsTrue(result == ExecutionState.Halt);

        Assert.IsTrue(vm.Stack.Count == 1);

        var obj = vm.Stack.Pop();
        var returnObject = obj.AsStruct<MyLocalStruct>();
        Assert.AreEqual(returnObject.name, myStruct.name);
        Assert.AreEqual(returnObject.age, (BigInteger)20);

        myStruct.name = "BartSimpson";
        myStruct.age = 50;
        vm = new TestVM(contract, storage, method);
        vm.Stack.Push(VMObject.FromObject(myStruct.age));
        vm.Stack.Push(VMObject.FromObject(myStruct.name));
        result = vm.Execute();
        Assert.IsTrue(result == ExecutionState.Halt);

        Assert.IsTrue(vm.Stack.Count == 1);

        obj = vm.Stack.Pop();
        returnObject = obj.AsStruct<MyLocalStruct>();
        Assert.AreEqual(myStruct.name,returnObject.name );
        Assert.AreEqual(myStruct.age, returnObject.age);
    }


    public enum MyEnum
    {
        First,
        Second,
        Third
    }

    public struct MyStructWithEnum
    {
        public string name;
        public BigInteger age;
        public MyEnum myEnum;
        public MyLocalStruct localStruct;
    }

    private struct MyComplexStruct
    {
        public string name;
        public BigInteger age;
        public MyLocalStruct localStruct;
        public MyEnum myEnum;
        public MyStructWithEnum myStructWithEnum;
    }


    [Test]
    public void TestComplexStructWithEnumsAndOtherStructs()
    {
        var sourceCode =
            @"

enum MyEnum
{
    First,
    Second,
    Third
}

struct MyLocalStruct {
    name:string;
    age:number;
}

struct MyStructWithEnum {
    name:string;
    age:number;
    myEnum:MyEnum;
    localStruct:MyLocalStruct;
}

struct MyComplexStruct {
    name:string;
    age:number;
    localStruct:MyLocalStruct;
    myEnum:MyEnum;
    myStructWithEnum:MyStructWithEnum;
}

contract test{
    import Struct;
    public testMyComplexStruct (name:string, age:number, _myEnum: MyEnum) : MyComplexStruct {
        local myStruct : MyLocalStruct = Struct.MyLocalStruct(name, age);
        local myStructWithEnum : MyStructWithEnum = Struct.MyStructWithEnum(name, age, _myEnum, myStruct);
        local myComplextStruct : MyComplexStruct = Struct.MyComplexStruct(name, age, myStruct, MyEnum.Second, myStructWithEnum);
        return myComplextStruct;
    }
}";

        var parser = new TombLangCompiler();
        var contract = parser.Process(sourceCode).First();

        var storage = new Dictionary<byte[], byte[]>(new ByteArrayComparer());

        TestVM vm;
        var method = contract.abi.FindMethod("testMyComplexStruct");
        // Age 10
        var myStruct = new MyLocalStruct();
        myStruct.name = "John";
        myStruct.age = 10;
        vm = new TestVM(contract, storage, method);
        vm.Stack.Push(VMObject.FromObject(MyEnum.First));
        vm.Stack.Push(VMObject.FromObject(myStruct.age));
        vm.Stack.Push(VMObject.FromObject(myStruct.name));
        var result = vm.Execute();
        Assert.IsTrue(result == ExecutionState.Halt);

        Assert.IsTrue(vm.Stack.Count == 1);

        var obj = vm.Stack.Pop();
        var returnObject = obj.AsStruct<MyComplexStruct>();
        Assert.AreEqual(myStruct.name,returnObject.name );
        Assert.AreEqual(myStruct.age, (BigInteger)10);
        Assert.AreEqual(MyEnum.Second, returnObject.myEnum);
        Assert.AreEqual(myStruct.name,returnObject.myStructWithEnum.name );
        Assert.AreEqual(myStruct.age, returnObject.myStructWithEnum.age);
        Assert.AreEqual(MyEnum.First, returnObject.myStructWithEnum.myEnum);
        Assert.AreEqual(myStruct.name,returnObject.myStructWithEnum.localStruct.name );
        Assert.AreEqual(myStruct.age, returnObject.myStructWithEnum.localStruct.age);
    }

    [Test]
    public void TestComplexSendStructOverAMethod()
    {
        var sourceCode =
            @"

enum MyEnum
{
    First,
    Second,
    Third
}

struct MyLocalStruct {
    name:string;
    age:number;
}

struct MyStructWithEnum {
    name:string;
    age:number;
    myEnum:MyEnum;
    localStruct:MyLocalStruct;
}

struct MyComplexStruct {
    name:string;
    age:number;
    localStruct:MyLocalStruct;
    myEnum:MyEnum;
    myStructWithEnum:MyStructWithEnum;
}

contract test{
    import Struct;
    public testMyComplexStruct (myComplexStruct:MyComplexStruct) : MyComplexStruct {
        myComplexStruct.age = 20;
        myComplexStruct.myEnum = MyEnum.Second;
        return myComplexStruct;
    }
}";

        var parser = new TombLangCompiler();
        var contract = parser.Process(sourceCode).First();

        var storage = new Dictionary<byte[], byte[]>(new ByteArrayComparer());

        TestVM vm;
        var method = contract.abi.FindMethod("testMyComplexStruct");
        // Age 10
        var myComplexStructStruct = new MyComplexStruct();
        myComplexStructStruct.name = "John";
        myComplexStructStruct.age = 10;
        myComplexStructStruct.myEnum = MyEnum.First;
        myComplexStructStruct.myStructWithEnum.name = "John V2";
        myComplexStructStruct.myStructWithEnum.age = 50;
        myComplexStructStruct.myStructWithEnum.myEnum = MyEnum.Second;
        myComplexStructStruct.myStructWithEnum.localStruct.name = "John V3";
        myComplexStructStruct.myStructWithEnum.localStruct.age = 100;
        myComplexStructStruct.localStruct.name = "John V4";
        myComplexStructStruct.localStruct.age = 200;
        vm = new TestVM(contract, storage, method);
        vm.Stack.Push(VMObject.FromStruct(myComplexStructStruct));
        var result = vm.Execute();
        Assert.IsTrue(result == ExecutionState.Halt);

        Assert.IsTrue(vm.Stack.Count == 1);

        var obj = vm.Stack.Pop();
        var myResultStruct = obj.AsStruct<MyComplexStruct>();
        Assert.AreEqual(myComplexStructStruct.name,myResultStruct.name );
        Assert.AreEqual((BigInteger)20, myResultStruct.age);
        Assert.AreEqual(MyEnum.Second, myResultStruct.myEnum);
        Assert.AreEqual(myComplexStructStruct.myStructWithEnum.name, myResultStruct.myStructWithEnum.name);
        Assert.AreEqual(myComplexStructStruct.myStructWithEnum.age, myResultStruct.myStructWithEnum.age);
        Assert.AreEqual(myComplexStructStruct.myStructWithEnum.myEnum, myResultStruct.myStructWithEnum.myEnum);
        Assert.AreEqual(myComplexStructStruct.myStructWithEnum.localStruct.name, myResultStruct.myStructWithEnum.localStruct.name);
        Assert.AreEqual(myComplexStructStruct.myStructWithEnum.localStruct.age, myResultStruct.myStructWithEnum.localStruct.age);
        Assert.AreEqual(myComplexStructStruct.localStruct.name, myResultStruct.localStruct.name);
        Assert.AreEqual(myComplexStructStruct.localStruct.age, myResultStruct.localStruct.age);
    }
}
