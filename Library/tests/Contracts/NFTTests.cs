using PhantasmaPhoenix.Cryptography;
using PhantasmaPhoenix.Protocol;
using PhantasmaPhoenix.VM;
using PhantasmaPhoenix.Core;
using Phantasma.Tomb.Compilers;
using System.Diagnostics;
using System.Numerics;
using TOMBLib.Tests.Bridge;

namespace TOMBLib.Tests.Contracts;

public class NFTTests
{

    [Test]
    public void GetInfusions()
    {
        var sourceCode = @"
contract test{

import NFT;
import Array;

public testMethod() : Infusion {
    local nfts: array<Infusion> = NFT.getInfusions(""SOUL"", 123);

    local first: Infusion = nfts[0];

    return first;
    }
}";

        /*
public testMethod2():number {
    local nfts: array<Infusion> = NFT.getInfusions(""SOUL"", 123);
    return Array.length(nfts);
}
         */

        var parser = new TombLangCompiler();
        var contract = parser.Process(sourceCode).First();

        var asm = contract.asm;
        Debug.WriteLine(asm);

        var storage = new Dictionary<byte[], byte[]>(new ByteArrayComparer());

        TestVM vm;

        // call testMethod
        var method = contract.abi.FindMethod("testMethod");
        Assert.IsNotNull(method);

        vm = new TestVM(contract, storage, method);
        var result = vm.Execute();
        Assert.IsTrue(result == ExecutionState.Halt);

        Assert.IsTrue(vm.Stack.Count == 1);

        var obj = vm.Stack.Pop();
        var returnedValue = obj.AsStruct<TokenInfusion>();

        Assert.IsTrue(returnedValue.Symbol == "SOUL");
        Assert.IsTrue(returnedValue.Value == 1234);
    }

    [Test]
    public void GetOwnerships()
    {
        var sourceCode = @"
contract test{

import NFT;
import Array;

public testMethod(from:address) : array<number> {
    local result: array<number> = NFT.getOwnerships(from, ""SOUL"");
    return result;
    }
}";

        var parser = new TombLangCompiler();
        var contract = parser.Process(sourceCode).First();

        var asm = contract.asm;
        Debug.WriteLine(asm);

        var storage = new Dictionary<byte[], byte[]>(new ByteArrayComparer());

        TestVM vm;

        var keys = PhantasmaKeys.Generate();

        // call testMethod
        var method = contract.abi.FindMethod("testMethod");
        Assert.IsNotNull(method);

        vm = new TestVM(contract, storage, method);
        vm.Stack.Push(VMObject.FromObject(keys.Address));
        var result = vm.Execute();
        Assert.IsTrue(result == ExecutionState.Halt);

        Assert.IsTrue(vm.Stack.Count == 1);

        var obj = vm.Stack.Pop();
        var array = obj.ToArray<BigInteger>();

        Assert.IsTrue(array.Length == 3);
    }

    /*
               [Test]
               public void NFTs()
               {
                   var keys = PhantasmaKeys.Generate();
                   var keys2 = PhantasmaKeys.Generate();

                   var nexus = new Nexus("simnet", null, null);
                   nexus.SetOracleReader(new OracleSimulator(nexus));
                   var simulator = new NexusSimulator(nexus, keys, 1234);

                   string symbol = "ATEST";
                   string name = "Test";

                   var sourceCode =
                       @"struct someStruct
                       {
                           created:timestamp;
                           creator:address;
                           royalties:number;
                           name:string;
                           description:string;
                           imageURL:string;
                           infoURL:string;
                       }
                       token " + symbol + @" {
                           import Runtime;
                           import Time;
                           import NFT;
                           import Map;
                           global _address:address;
                           global _owner:address;
                           global _unlockStorageMap: storage_map<number, number>;

                           property symbol:string = """ + symbol + @""";
                           property name:string = """ + name + @""";
                           property isBurnable:bool = true;
                           property isTransferable:bool = true;

                           nft myNFT<someStruct, number> {

                               import Call;
                               import Map;

                               property name:string {
                                   return _ROM.name;
                               }

                               property description:string {
                                   return _ROM.description;
                               }

                               property imageURL:string {
                                   return _ROM.imageURL;
                               }

                               property infoURL:string {
                                   return _ROM.infoURL;
                               }

                               property unlockCount:number {
                                   local count:number = Call.interop<number>(""Map.Get"",  ""ATEST"", ""_unlockStorageMap"", _tokenID, $TYPE_OF(number));
                                   return count;
                               }
                           }

                           import Call;
                           constructor(owner:address)	{
                               _address = @P2KEYzWsbrMbPNtW1tBzzDKeYxYi4hjzpx4EfiyRyaoLkMM;
                               _owner= owner;
                               NFT.createSeries(owner, $THIS_SYMBOL, 0, 999, TokenSeries.Unique, myNFT);
                           }

                           public mint(dest:address):number {
                               local rom:someStruct = Struct.someStruct(Time.now(), _address, 1, ""hello"", ""desc"", ""imgURL"", ""info"");
                               local tokenID:number = NFT.mint(_address, dest, $THIS_SYMBOL, rom, 0, 0);
                               _unlockStorageMap.set(tokenID, 0);
                               Call.interop<none>(""Map.Set"",  ""_unlockStorageMap"", tokenID, 111);
                               return tokenID;
                           }

                           public readName(nftID:number): string {
                               local romInfo:someStruct = NFT.readROM<someStruct>($THIS_SYMBOL, nftID);
                               return romInfo.name;
                           }

                           public readOwner(nftID:number): address {
                               local nftInfo:NFT = NFT.read($THIS_SYMBOL, nftID);
                               return nftInfo.owner;
                           }
                       }";

                   var parser = new TombLangCompiler();
                   var contract = parser.Process(sourceCode).First();
                   //System.IO.File.WriteAllText(@"/tmp/asm.asm", contract..asm);
                   //System.IO.File.WriteAllText(@"/tmp/asm.asm", contract.SubModules.First().asm);

                   simulator.BeginBlock();
                   simulator.GenerateCustomTransaction(keys, ProofOfWork.Minimal,
                           () => ScriptUtils.BeginScript().AllowGas(keys.Address, Address.Null, 1, 9999)
                           .CallInterop("Nexus.CreateToken", keys.Address, contract.script, contract.abi.ToByteArray())
                           .SpendGas(keys.Address)
                           .EndScript());
                   simulator.EndBlock();

                   var otherKeys = PhantasmaKeys.Generate();

                   simulator.BeginBlock();
                   var tx = simulator.GenerateCustomTransaction(keys, ProofOfWork.None, () =>
                           ScriptUtils.BeginScript().
                           AllowGas(keys.Address, Address.Null, 1, 9999).
                           CallContract(symbol, "mint", otherKeys.Address).
                           SpendGas(keys.Address).
                           EndScript());
                   var block = simulator.EndBlock().First();

                   var result = block.GetResultForTransaction(tx.Hash);
                   Assert.NotNull(result);
                   var obj = VMObject.FromBytes(result);
                   var nftID = obj.AsNumber();
                   Assert.IsTrue(nftID > 0);

                   simulator.BeginBlock();
                   tx = simulator.GenerateCustomTransaction(keys, ProofOfWork.None, () =>
                           ScriptUtils.BeginScript().
                           AllowGas(keys.Address, Address.Null, 1, 9999).
                           CallContract(symbol, "readName",nftID).
                           SpendGas(keys.Address).
                           EndScript());
                   block = simulator.EndBlock().First();

                   result = block.GetResultForTransaction(tx.Hash);
                   Assert.NotNull(result);
                   obj = VMObject.FromBytes(result);
                   var nftName = obj.AsString();
                   Assert.IsTrue(nftName == "hello");

                   simulator.BeginBlock();
                   tx = simulator.GenerateCustomTransaction(keys, ProofOfWork.None, () =>
                       ScriptUtils.BeginScript().
                           AllowGas(keys.Address, Address.Null, 1, 9999).
                           CallContract(symbol, "readOwner", nftID).
                           SpendGas(keys.Address).
                           EndScript());
                   block = simulator.EndBlock().First();

                   result = block.GetResultForTransaction(tx.Hash);
                   Assert.NotNull(result);
                   obj = VMObject.FromBytes(result);
                   var nftOwner = obj.AsAddress();
                   Assert.IsTrue(nftOwner == otherKeys.Address);

                   var mempool = new Mempool(simulator.Nexus, 2, 1, System.Text.Encoding.UTF8.GetBytes(symbol), 0, new DummyLogger());
                   mempool?.SetKeys(keys);

                   var api = new NexusAPI(simulator.Nexus);

                   var nft = (TokenDataResult)api.GetNFT(symbol, nftID.ToString(), true);
                   foreach (var a in nft.properties)
                   {
                       switch (a.Key)
                       {
                           case "Name":
                               Assert.IsTrue(a.Value == "hello");
                               break;
                           case "Description":
                               Assert.IsTrue(a.Value == "desc");
                               break;
                           case "ImageURL":
                               Assert.IsTrue(a.Value == "imgURL");
                               break;
                           case "InfoURL":
                               Assert.IsTrue(a.Value == "info");
                               break;
                           case "UnlockCount":
                               Assert.IsTrue(a.Value == "111");
                               break;

                       }
                   }
               }

               [Test]
               public void NFTWrite()
               {
                   var keys = PhantasmaKeys.Generate();
                   var keys2 = PhantasmaKeys.Generate();

                   var nexus = new Nexus("simnet", null, null);
                   nexus.SetOracleReader(new OracleSimulator(nexus));
                   var simulator = new NexusSimulator(nexus, keys, 1234);

                   string symbol = "ATEST";
                   string name = "Test";

                   var sourceCode =
                       @"struct someStruct
                       {
                           created:timestamp;
                           creator:address;
                           royalties:number;
                           name:string;
                           description:string;
                           imageURL:string;
                           infoURL:string;
                       }
                       token " + symbol + @" {
                           import Runtime;
                           import Time;
                           import NFT;
                           import Map;
                           global _address:address;
                           global _owner:address;
                           global _unlockStorageMap: storage_map<number, number>;

                           property symbol:string = """ + symbol+ @""";
                           property name:string = """ + name + @""";
                           property isBurnable:bool = true;
                           property isTransferable:bool = true;

                           nft myNFT<someStruct, number> {

                               import Call;
                               import Map;

                               property name:string {
                                   return _ROM.name;
                               }

                               property description:string {
                                   return _ROM.description;
                               }

                               property imageURL:string {
                                   return _ROM.imageURL;
                               }

                               property infoURL:string {
                                   return _ROM.infoURL;
                               }

                               property unlockCount:number {
                                      local count:number = Call.interop<number>(""Map.Get"",  ""ATEST"", ""_unlockStorageMap"", _tokenID, $TYPE_OF(number));
                                   return count;
                               }
                           }

                           import Call;
                           constructor(owner:address)	{
                               _address = @P2KEYzWsbrMbPNtW1tBzzDKeYxYi4hjzpx4EfiyRyaoLkMM;
                               _owner= owner;
                               NFT.createSeries(owner, $THIS_SYMBOL, 0, 999, TokenSeries.Unique, myNFT);
                           }

                           public mint(dest:address):number {
                               local rom:someStruct = Struct.someStruct(Time.now(), _address, 1, ""hello"", ""desc"", ""imgURL"", ""info"");
                               local tokenID:number = NFT.mint(_address, dest, $THIS_SYMBOL, rom, 0, 0);
                               _unlockStorageMap.set(tokenID, 0);
                               Call.interop<none>(""Map.Set"",  ""_unlockStorageMap"", tokenID, 111);
                               return tokenID;
                           }

                           public updateNFT(from:address, nftID:number) {
                               local symbol : string = $THIS_SYMBOL;
                               NFT.write(from, $THIS_SYMBOL, nftID, 1);
                           }

                           public readNFTRAM(nftID:number): number{
                               local ramInfo : number = NFT.readRAM<number>($THIS_SYMBOL, nftID);
                               return ramInfo;
                           }

                           public readName(nftID:number): string {
                               local romInfo:someStruct = NFT.readROM<someStruct>($THIS_SYMBOL, nftID);
                               return romInfo.name;
                           }

                           public readOwner(nftID:number): address {
                               local nftInfo:NFT = NFT.read($THIS_SYMBOL, nftID);
                               return nftInfo.owner;
                           }
                       }";

                   var parser = new TombLangCompiler();
                   var contract = parser.Process(sourceCode).First();
                   //System.IO.File.WriteAllText(@"/tmp/asm.asm", contract..asm);
                   //System.IO.File.WriteAllText(@"/tmp/asm.asm", contract.SubModules.First().asm);

                   simulator.BeginBlock();
                   simulator.GenerateCustomTransaction(keys, ProofOfWork.Minimal,
                           () => ScriptUtils.BeginScript().AllowGas(keys.Address, Address.Null, 1, 9999)
                           .CallInterop("Nexus.CreateToken", keys.Address, contract.script, contract.abi.ToByteArray())
                           .SpendGas(keys.Address)
                           .EndScript());
                   simulator.EndBlock();

                   var otherKeys = PhantasmaKeys.Generate();

                   simulator.BeginBlock();
                   var tx = simulator.GenerateCustomTransaction(keys, ProofOfWork.None, () =>
                           ScriptUtils.BeginScript().
                           AllowGas(keys.Address, Address.Null, 1, 9999).
                           CallContract(symbol, "mint", otherKeys.Address).
                           SpendGas(keys.Address).
                           EndScript());
                   var block = simulator.EndBlock().First();

                   var result = block.GetResultForTransaction(tx.Hash);
                   Assert.NotNull(result);
                   var obj = VMObject.FromBytes(result);
                   var nftID = obj.AsNumber();
                   Assert.IsTrue(nftID > 0);

                   simulator.BeginBlock();
                   tx = simulator.GenerateCustomTransaction(keys, ProofOfWork.None, () =>
                           ScriptUtils.BeginScript().
                           AllowGas(keys.Address, Address.Null, 1, 9999).
                           CallContract(symbol, "readName", nftID).
                           SpendGas(keys.Address).
                           EndScript());
                   block = simulator.EndBlock().First();

                   result = block.GetResultForTransaction(tx.Hash);
                   Assert.NotNull(result);
                   obj = VMObject.FromBytes(result);
                   var nftName = obj.AsString();
                   Assert.IsTrue(nftName == "hello");

                   simulator.BeginBlock();
                   tx = simulator.GenerateCustomTransaction(keys, ProofOfWork.None, () =>
                       ScriptUtils.BeginScript().
                           AllowGas(keys.Address, Address.Null, 1, 9999).
                           CallContract(symbol, "readOwner", nftID).
                           SpendGas(keys.Address).
                           EndScript());
                   block = simulator.EndBlock().First();

                   result = block.GetResultForTransaction(tx.Hash);
                   Assert.NotNull(result);
                   obj = VMObject.FromBytes(result);
                   var nftOwner = obj.AsAddress();
                   Assert.IsTrue(nftOwner == otherKeys.Address);

                   // update ram
                   simulator.BeginBlock();
                   tx = simulator.GenerateCustomTransaction(keys, ProofOfWork.None, () =>
                           ScriptUtils.BeginScript().
                           AllowGas(keys.Address, Address.Null, 1, 9999).
                           CallContract(symbol, "updateNFT", otherKeys.Address.Text, nftID).
                           SpendGas(keys.Address).
                           EndScript());
                   block = simulator.EndBlock().First();

                   // Read RAM
                   simulator.BeginBlock();
                   tx = simulator.GenerateCustomTransaction(keys, ProofOfWork.None, () =>
                       ScriptUtils.BeginScript().
                           AllowGas(keys.Address, Address.Null, 1, 9999).
                           CallContract(symbol, "readNFTRAM", nftID).
                           SpendGas(keys.Address).
                           EndScript());
                   block = simulator.EndBlock().First();

                   result = block.GetResultForTransaction(tx.Hash);
                   Assert.NotNull(result);
                   obj = VMObject.FromBytes(result);
                   var ram = obj.AsNumber();
                   Assert.IsTrue(ram == 1);

                   var mempool = new Mempool(simulator.Nexus, 2, 1, System.Text.Encoding.UTF8.GetBytes(symbol), 0, new DummyLogger());
                   mempool?.SetKeys(keys);

                   var api = new NexusAPI(simulator.Nexus);

                   var nft = (TokenDataResult)api.GetNFT(symbol, nftID.ToString(), true);
                   foreach (var a in nft.properties)
                   {
                       switch (a.Key)
                       {
                           case "Name":
                               Assert.IsTrue(a.Value == "hello");
                               break;
                           case "Description":
                               Assert.IsTrue(a.Value == "desc");
                               break;
                           case "ImageURL":
                               Assert.IsTrue(a.Value == "imgURL");
                               break;
                           case "InfoURL":
                               Assert.IsTrue(a.Value == "info");
                               break;
                           case "UnlockCount":
                               Assert.IsTrue(a.Value == "111");
                               break;

                       }
                   }
               }

               [Test]
               public void Triggers()
               {
                   var keys = PhantasmaKeys.Generate();
                   var keys2 = PhantasmaKeys.Generate();

                   var nexus = new Nexus("simnet", null, null);
                   nexus.SetOracleReader(new OracleSimulator(nexus));
                   var simulator = new NexusSimulator(nexus, keys, 1234);

                   var sourceCode =
                       "contract test {\n" +
                       "import Runtime;\n" +
                       "import Time;\n" +
                       "global _address:address;" +
                       "global _owner:address;" +
                       "constructor(owner:address)	{\n" +
                       "_address = @P2KEYzWsbrMbPNtW1tBzzDKeYxYi4hjzpx4EfiyRyaoLkMM;\n" +
                       "_owner= owner;\n" +
                       "}\n" +
                       "public doStuff(from:address)\n" +
                       "{\n" +
                       "}\n"+
                       "trigger onUpgrade(from:address)\n" +
                       "{\n" +
                       "    Runtime.expect(from == _address, \"invalid owner address\"\n);" +
                       "	 Runtime.expect(Runtime.isWitness(from), \"invalid witness\"\n);" +
                       "}\n" +
                       "}\n";

                   var parser = new TombLangCompiler();
                   var contract = parser.Process(sourceCode).First();

                   simulator.BeginBlock();
                   simulator.GenerateCustomTransaction(keys, ProofOfWork.Minimal,
                           () => ScriptUtils.BeginScript().AllowGas(keys.Address, Address.Null, 1, 9999)
                           .CallInterop("Runtime.DeployContract", keys.Address, "test", contract.script, contract.abi.ToByteArray())
                           .SpendGas(keys.Address)
                           .EndScript());
                   simulator.EndBlock();

                   simulator.BeginBlock();
                   simulator.GenerateCustomTransaction(keys, ProofOfWork.Minimal, () =>
                           ScriptUtils.BeginScript().
                           AllowGas(keys.Address, Address.Null, 1, 9999).
                           CallInterop("Runtime.UpgradeContract", keys.Address, "test", contract.script, contract.abi.ToByteArray()).
                           SpendGas(keys.Address).
                           EndScript());
                   var ex = Assert.Throws<ChainException>(() => simulator.EndBlock());
                   Assert.That(ex.Message, Is.EqualTo("add block @ main failed, reason: OnUpgrade trigger failed @ Runtime_UpgradeContract"));

               }

               [Test]
               public void StorageList()
               {
                   var keys = PhantasmaKeys.Generate();
                   var keys2 = PhantasmaKeys.Generate();

                   var nexus = new Nexus("simnet", null, null);
                   nexus.SetOracleReader(new OracleSimulator(nexus));
                   var simulator = new NexusSimulator(nexus, keys, 1234);

                   var sourceCode =
                       "contract test {\n" +
                       "import Runtime;\n" +
                       "import Time;\n" +
                       "import List;\n" +
                       "global myList: storage_list<string>;\n" +
                       "public getCount():number\n" +
                       "{\n" +
                       " return myList.count();\n" +
                       "}\n" +
                       "public getStuff(index:number):string \n" +
                       "{\n" +
                       " return myList.get(index);\n" +
                       "}\n"+
                       "public removeStuff(index:number) \n" +
                       "{\n" +
                       " myList.removeAt(index);\n" +
                       "}\n" +
                       "public clearStuff() \n" +
                       "{\n" +
                       " myList.clear();\n" +
                       "}\n" +
                       "public addStuff(stuff:string) \n" +
                       "{\n" +
                       " myList.add(stuff);\n" +
                       "}\n" +
                       "public replaceStuff(index:number, stuff:string) \n" +
                       "{\n" +
                       " myList.replace(index, stuff);\n" +
                       "}\n" +
                       "constructor(owner:address)	{\n" +
                       "   this.addStuff(\"hello\");\n" +
                       "   this.addStuff(\"world\");\n" +
                       "}\n" +
                       "}\n";

                   var parser = new TombLangCompiler();
                   var contract = parser.Process(sourceCode).First();

                   simulator.BeginBlock();
                   simulator.GenerateCustomTransaction(keys, ProofOfWork.Minimal,
                           () => ScriptUtils.BeginScript().AllowGas(keys.Address, Address.Null, 1, 9999)
                           .CallInterop("Runtime.DeployContract", keys.Address, "test", contract.script, contract.abi.ToByteArray())
                           .SpendGas(keys.Address)
                           .EndScript());
                   simulator.EndBlock();

                   Func<int, string> fetchListItem = (index) =>
                   {
                       simulator.BeginBlock();
                       var tx = simulator.GenerateCustomTransaction(keys, ProofOfWork.Minimal, () =>
                               ScriptUtils.BeginScript().
                               AllowGas(keys.Address, Address.Null, 1, 9999).
                               CallContract("test", "getStuff", index).
                               SpendGas(keys.Address).
                               EndScript());
                       var block = simulator.EndBlock().FirstOrDefault();

                       var bytes = block.GetResultForTransaction(tx.Hash);
                       Assert.IsTrue(bytes != null);

                       var vmObj = Serialization.Unserialize<VMObject>(bytes);

                       return  vmObj.AsString();
                   };

                   Func<int> fetchListCount = () =>
                   {
                       simulator.BeginBlock();
                       var tx = simulator.GenerateCustomTransaction(keys, ProofOfWork.Minimal, () =>
                               ScriptUtils.BeginScript().
                               AllowGas(keys.Address, Address.Null, 1, 9999).
                               CallContract("test", "getCount").
                               SpendGas(keys.Address).
                               EndScript());
                       var block = simulator.EndBlock().FirstOrDefault();

                       var bytes = block.GetResultForTransaction(tx.Hash);
                       Assert.IsTrue(bytes != null);

                       var vmObj = Serialization.Unserialize<VMObject>(bytes);

                       return (int)vmObj.AsNumber();
                   };

                   string str;
                   int count;

                   str = fetchListItem(0);
                   Assert.IsTrue(str == "hello");

                   str = fetchListItem(1);
                   Assert.IsTrue(str == "world");

                   count = fetchListCount();
                   Assert.IsTrue(count == 2);

                   simulator.BeginBlock();
                   simulator.GenerateCustomTransaction(keys, ProofOfWork.Minimal, () =>
                           ScriptUtils.BeginScript().
                           AllowGas(keys.Address, Address.Null, 1, 9999).
                           CallContract("test", "removeStuff", 0).
                           SpendGas(keys.Address).
                           EndScript());
                   simulator.EndBlock();

                   count = fetchListCount();
                   Assert.IsTrue(count == 1);

                   str = fetchListItem(0);
                   Assert.IsTrue(str == "world");

                   simulator.BeginBlock();
                   simulator.GenerateCustomTransaction(keys, ProofOfWork.Minimal, () =>
                           ScriptUtils.BeginScript().
                           AllowGas(keys.Address, Address.Null, 1, 9999).
                           CallContract("test", "replaceStuff", 0, "A").
                           CallContract("test", "addStuff", "B").
                           CallContract("test", "addStuff", "C").
                           SpendGas(keys.Address).
                           EndScript());
                   simulator.EndBlock();

                   count = fetchListCount();
                   Assert.IsTrue(count == 3);

                   str = fetchListItem(0);
                   Assert.IsTrue(str == "A");

                   str = fetchListItem(1);
                   Assert.IsTrue(str == "B");

                   str = fetchListItem(2);
                   Assert.IsTrue(str == "C");

                   simulator.BeginBlock();
                   simulator.GenerateCustomTransaction(keys, ProofOfWork.Minimal, () =>
                           ScriptUtils.BeginScript().
                           AllowGas(keys.Address, Address.Null, 1, 9999).
                           CallContract("test", "clearStuff").
                           SpendGas(keys.Address).
                           EndScript());
                   simulator.EndBlock();

                   count = fetchListCount();
                   Assert.IsTrue(count == 0);
               }

               [Test]
               public void StorageMap()
               {
                   var keys = PhantasmaKeys.Generate();
                   var keys2 = PhantasmaKeys.Generate();

                   var nexus = new Nexus("simnet", null, null);
                   nexus.SetOracleReader(new OracleSimulator(nexus));
                   var simulator = new NexusSimulator(nexus, keys, 1234);

                   var sourceCode =
                       "contract test {\n" +
                       "import Runtime;\n" +
                       "import Time;\n" +
                       "import Map;\n" +
                       "global _storageMap: storage_map<number, string>;\n" +
                       "constructor(owner:address)	{\n" +
                       "_storageMap.set(5, \"test1\");\n" +
                       "}\n" +
                       "public doStuff(from:address)\n" +
                       "{\n" +
                       " local test:string = _storageMap.get(5);\n" +
                       " Runtime.log(\"this log: \");\n" +
                       " Runtime.log(test);\n" +
                       "}\n" +
                       "}\n";

                   var parser = new TombLangCompiler();
                   var contract = parser.Process(sourceCode).First();

                   simulator.BeginBlock();
                   simulator.GenerateCustomTransaction(keys, ProofOfWork.Minimal,
                           () => ScriptUtils.BeginScript().AllowGas(keys.Address, Address.Null, 1, 9999)
                           .CallInterop("Runtime.DeployContract", keys.Address, "test", contract.script, contract.abi.ToByteArray())
                           .SpendGas(keys.Address)
                           .EndScript());
                   simulator.EndBlock();

                   simulator.BeginBlock();
                   simulator.GenerateCustomTransaction(keys, ProofOfWork.Minimal, () =>
                           ScriptUtils.BeginScript().
                           AllowGas(keys.Address, Address.Null, 1, 9999).
                           CallContract("test", "doStuff", keys.Address).
                           SpendGas(keys.Address).
                           EndScript());
                   simulator.EndBlock();
               }

               public struct My_Struct
               {
                   public string name;
                   public BigInteger value;
               }


               [Test]
               public void StorageMapAndStruct()
               {
                   var keys = PhantasmaKeys.Generate();
                   var keys2 = PhantasmaKeys.Generate();

                   var nexus = new Nexus("simnet", null, null);
                   //nexus.SetOracleReader(new OracleSimulator(nexus));
                   var simulator = new NexusSimulator(nexus, keys, 1234);

                   var sourceCode =
                       "struct my_struct\n{" +
                           "name:string;\n" +
                           "value:number;\n" +
                       "}\n" +
                       "contract test {\n" +
                       "import Runtime;\n" +
                       "import Time;\n" +
                       "import Map;\n" +
                       "global _storageMap: storage_map<number, my_struct>;\n" +
                       "public createStruct(key:number, s:string, val:number)\n" +
                       "{\n" +
                       "local temp: my_struct = Struct.my_struct(s, val);\n" +
                       "_storageMap.set(key, temp);\n" +
                       "}\n" +
                       "public getStruct(key:number):my_struct\n" +
                       "{\n" +
                       "return _storageMap.get(key);\n" +
                       "}\n" +
                       "}\n";

                   var parser = new TombLangCompiler();
                   var contract = parser.Process(sourceCode).First();

                   simulator.BeginBlock();
                   simulator.GenerateCustomTransaction(keys, ProofOfWork.Minimal,
                           () => ScriptUtils.BeginScript().AllowGas(keys.Address, Address.Null, 1, 9999)
                           .CallInterop("Runtime.DeployContract", keys.Address, "test", contract.script, contract.abi.ToByteArray())
                           .SpendGas(keys.Address)
                           .EndScript());
                   simulator.EndBlock();

                   simulator.BeginBlock();
                   simulator.GenerateCustomTransaction(keys, ProofOfWork.Minimal, () =>
                           ScriptUtils.BeginScript().
                           AllowGas(keys.Address, Address.Null, 1, 9999).
                           CallContract("test", "createStruct", 5, "hello", 123).
                           SpendGas(keys.Address).
                           EndScript());
                   simulator.EndBlock();

                   var vmObj = simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, "test", "getStruct", 5);
                   var temp = vmObj.AsStruct<My_Struct>();
                   Assert.IsTrue(temp.name == "hello");
                   Assert.IsTrue(temp.value == 123);
               }*/
}
