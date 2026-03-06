using NUnit.Framework;
using NUnit.Framework.Internal;
using System;
using System.Collections.Generic;
using System.Linq;
using Phantasma.Tomb.Compilers;
using Phantasma.Tomb;
using Phantasma.Core.Domain;
using PhantasmaPhoenix.Core;
using PhantasmaPhoenix.Protocol;

namespace TOMBLib.Tests
{
    public class GeneralTests
    {

        [Test]
        public void DeprecatedAssigment()
        {
            var sourceCode = @"
contract test {
    global _addressOwner:address;

    constructor(owner:address)
    {
        _addressOwner := owner;
    }
}
";

            var parser = new TombLangCompiler();

            var exception = Assert.Catch<CompilerException>(() =>
            {
                var contract = parser.Process(sourceCode).First();
            });

            Assert.IsTrue(exception.Message.Contains("deprecated", StringComparison.OrdinalIgnoreCase));
        }
        
        [Test]
        public void AvailableSymbols()
        {
            var tokenSymbol = "TOK";

            string[] sourceCode = new string[]
            {
                "token " + tokenSymbol + " {",
                "   property name: string = \"" + tokenSymbol + "\";",
                "import Token;",
                "public getSymbols() : array<string> {",
                "   local symbols:array<string>;",
                "   symbols = Token.availableSymbols();",
                "return symbols;}",
                "}"
            };

            var parser = new TombLangCompiler();
            var contract = parser.Process(sourceCode).First();

            var storage = new Dictionary<byte[], byte[]>(new ByteArrayComparer());

            TestVM vm;

            var getSymbols = contract.abi.FindMethod("getSymbols");
            Assert.IsNotNull(getSymbols);

            vm = new TestVM(contract, storage, getSymbols);
            var result = vm.Execute();
            Assert.IsTrue(result == ExecutionState.Halt);

            Assert.IsTrue(vm.Stack.Count == 1);

            var obj = vm.Stack.Pop();
            var newVal = obj.ToArray<string>();

            Assert.IsTrue(newVal.Length == 2);
            Assert.IsTrue(newVal[0] == "LOL");
            Assert.IsTrue(newVal[1] == tokenSymbol);
        }
        
        /*        [Test]
                public void AES()
                {
                    var keys = PhantasmaKeys.Generate();
                    var keys2 = PhantasmaKeys.Generate();

                    var nexus = new Nexus("simnet", null, null);
                    nexus.SetOracleReader(new OracleSimulator(nexus));
                    var simulator = new NexusSimulator(nexus, keys, 1234);

                    var sourceCode =
                        "contract test {\n" +
                        "import Runtime;\n" +
                        "import Cryptography;\n" +
                        "global someString: string;\n" +
                        "global someSecret: string;\n" +
                        "global result: string;\n" +
                        "constructor(owner:address)	{\n" +
                        "someString = \"somestring\";\n" +
                        "someSecret = \"somesecret123456somesecret123456\";\n" +
                        "local encrypted: bytes = Cryptography.AESEncrypt(someString.toBytes(), someSecret.toBytes());\n"+
                        "local decrypted: bytes = Cryptography.AESDecrypt(encrypted, someSecret.toBytes());\n"+
                        "result = decrypted.toString();\n" +
                        "}\n" +
                        "public doStuff(from:address)\n" +
                        "{\n" +
                        " Runtime.expect(result == someString, \"decrypted content does not equal original\");\n" +
                        "}\n"+
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

                [Test]
                public void AESAndStorageMap()
                {
                    var keys = PhantasmaKeys.Generate();
                    var keys2 = PhantasmaKeys.Generate();

                    var nexus = new Nexus("simnet", null, null);
                    nexus.SetOracleReader(new OracleSimulator(nexus));
                    var simulator = new NexusSimulator(nexus, keys, 1234);

                    var sourceCode =
                        "contract test {\n" +
                        "import Runtime;\n" +
                        "import Storage;\n" +
                        "import Map;\n" +
                        "import Cryptography;\n" +
                        "global someString: string;\n" +
                        "global someSecret: string;\n" +
                        "global result: string;\n" +
                        "global _lockedStorageMap: storage_map<number, bytes>;\n" +
                        "constructor(owner:address)	{\n" +
                        "someString = \"qwerty\";\n" +
                        "someSecret = \"d25a4cdb3f1b347efabb56da18069dfe\";\n" +
                        "local encrypted: bytes = Cryptography.AESEncrypt(someString.toBytes(), someSecret.toBytes());\n" +
                        "_lockedStorageMap.set(10, encrypted);\n" +
                        "local encryptedContentBytes:bytes = _lockedStorageMap.get(10);\n" +
                        "local decrypted: bytes = Cryptography.AESDecrypt(encryptedContentBytes, someSecret.toBytes());\n" +
                        "result = decrypted.toString();\n" +
                        "}\n" +
                        "public doStuff(from:address)\n" +
                        "{\n" +
                        " Runtime.expect(result == someString, \"decrypted content does not equal original\");\n" +
                        "}\n"+
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

                [Test]
                public void StorageMapHas()
                {
                    var keys = PhantasmaKeys.Generate();
                    var keys2 = PhantasmaKeys.Generate();

                    var nexus = new Nexus("simnet", null, null);
                    nexus.SetOracleReader(new OracleSimulator(nexus));
                    var simulator = new NexusSimulator(nexus, keys, 1234);

                    var sourceCode =
                        "contract test {\n" +
                        "import Runtime;\n" +
                        "import Map;\n" +
                        "global _storageMap: storage_map<number, string>;\n" +
                        "constructor(owner:address)	{\n" +
                        "_storageMap.set(5, \"test1\");\n"+
                        "}\n" +
                        "public doStuff(from:address)\n" +
                        "{\n" +
                        " local test: bool = _storageMap.has(5);\n" +
                        " Runtime.expect(test, \"key 5 doesn't exist! \");\n" +
                        " local test2: bool = _storageMap.has(6);\n" +
                        " Runtime.expect(test2 == false, \"key 6 does exist, but should not! \");\n" +
                        "}\n"+
                        "}\n";
                    var parser = new TombLangCompiler();
                    var contract = parser.Process(sourceCode).First();
                    Console.WriteLine("contract asm: " + contract.asm);

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
                }*/

        

        
        

        // add simplified version of that test
        //[Test]
        //public void TestGHOST()
        //{
        //    var keys = PhantasmaKeys.Generate();
        //    var keys2 = PhantasmaKeys.Generate();

        //    var nexus = new Nexus("simnet", null, null);
        //    nexus.SetOracleReader(new OracleSimulator(nexus));
        //    var simulator = new NexusSimulator(nexus, keys, 1234);
        //    var mempool = new Mempool(simulator.Nexus, 2, 1, System.Text.Encoding.UTF8.GetBytes("TEST"), 0, new DummyLogger());
        //    mempool?.SetKeys(keys);

        //    var api = new NexusAPI(simulator.Nexus);
        //    api.Mempool = mempool;
        //    mempool.Start();
        //    var sourceCode = System.IO.File.ReadAllLines("/home/merl/source/phantasma/GhostMarketContractPhantasma/GHOST.tomb");
        //    var parser = new TombLangCompiler();
        //    var contract = parser.Process(sourceCode).First();
        //    //Console.WriteLine("contract asm: " + contract.asm);
        //    //System.IO.File.WriteAllText(@"GHOST_series.asm", contract.SubModules.First().asm);

        //    simulator.BeginBlock();
        //    simulator.GenerateCustomTransaction(keys, ProofOfWork.Minimal,
        //            () => ScriptUtils.BeginScript().AllowGas(keys.Address, Address.Null, 1, 9999)
        //            .CallInterop("Nexus.CreateToken", keys.Address, "GHOST", "GHOST", new BigInteger(10000), new BigInteger(0),
        //                TokenFlags.Transferable|TokenFlags.Burnable|TokenFlags.Finite, contract.script, contract.abi.ToByteArray())
        //            .SpendGas(keys.Address)
        //            .EndScript());
        //    simulator.EndBlock();

        //    var token = (TokenResult)api.GetToken("GHOST");
        //    Console.WriteLine("id: " + token.ToString());
        //    Console.WriteLine("address: " + token.address);

        //    simulator.BeginBlock();
        //    simulator.GenerateCustomTransaction(keys, ProofOfWork.Minimal,
        //            () => ScriptUtils.BeginScript().AllowGas(keys.Address, Address.Parse(token.address), 1, 9999)
        //            .CallContract("GHOST", "mintToken", 0, 1, 1,
        //                keys.Address, 0, "GHOST", 1, "testnft", "desc1234567890", 1,
        //                "0", "0", "", "", "", "", "", "", "", 0, "", new Timestamp(1), "", 0)
        //            .SpendGas(keys.Address)
        //            .EndScript());
        //    simulator.EndBlock();

        //    Console.WriteLine("+++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++");
        //    var nft = (TokenDataResult)api.GetNFT("GHOST", "80807712912753409015029052615541912663228133032695758696669246580757047529373", true);
        //    Console.WriteLine("nft series: " + nft.series);
        //}

        /*[Test]
        public void TestCROWN()
        {
            var keys = PhantasmaKeys.Generate();
            var keys2 = PhantasmaKeys.Generate();

            var nexus = new Nexus("simnet", null, null);
            nexus.SetOracleReader(new OracleSimulator(nexus));
            var simulator = new NexusSimulator(nexus, keys, 1234);
            var mempool = new Mempool(simulator.Nexus, 2, 1, System.Text.Encoding.UTF8.GetBytes("TEST"), 0, new DummyLogger());
            mempool?.SetKeys(keys);

            var api = new NexusAPI(simulator.Nexus);
            api.Mempool = mempool;
            mempool.Start();

            var token = (TokenResult)api.GetToken("CROWN");
            Console.WriteLine("id: " + token.ToString());
            Console.WriteLine("address: " + token.address);

            simulator.TimeSkipDays(200);
            var nft = (TokenDataResult)api.GetNFT("CROWN", "64648043722874601761586352284082823113174122931185981250820896676646424691598", true);
            Console.WriteLine("nft series: " + nft.properties.ToString());
            foreach (var a in nft.properties)
            {
                Console.WriteLine($"res {a.Key}:{a.Value}");

            }
        }

        [Test]
        public void SimpleTest()
        {
            var keys = PhantasmaKeys.Generate();
            var sourceCode =
                @"
                contract test { 
                    import Time;
    
                    global time:number;

                    public constructor(owner:address){
                        time = 10000;
                    }
                        
                    public updateTime(newTime:number){
                        time = newTime;
                    }  

                    public getTime():timestamp {
                        local myTime:timestamp = time;
                        return myTime;
                    }
                }";
            var nexus = new Nexus("simnet", null, null);
            nexus.SetOracleReader(new OracleSimulator(nexus));
            var simulator = new NexusSimulator(nexus, keys, 1234);


            var parser = new TombLangCompiler();
            var contract = parser.Process(sourceCode).First();
            Console.WriteLine("contract asm: " + contract.asm);

            simulator.BeginBlock();
            simulator.GenerateCustomTransaction(keys, ProofOfWork.Minimal,
                    () => ScriptUtils.BeginScript().AllowGas(keys.Address, Address.Null, 1, 9999)
                    .CallInterop("Runtime.DeployContract", keys.Address, "test", contract.script, contract.abi.ToByteArray())
                    .SpendGas(keys.Address)
                    .EndScript());
            simulator.EndBlock();

            DateTime time = DateTime.Today.AddDays(-1);
            DateTimeOffset utcTime2 = time;

            simulator.BeginBlock();
            var tx = simulator.GenerateCustomTransaction(keys, ProofOfWork.Minimal, () =>
                    ScriptUtils.BeginScript().
                    AllowGas(keys.Address, Address.Null, 1, 9999).
                    CallContract("test", "updateTime", utcTime2.ToUnixTimeSeconds()).
                    SpendGas(keys.Address).
                    EndScript());
            simulator.EndBlock();

            simulator.BeginBlock();
            simulator.GenerateCustomTransaction(keys, ProofOfWork.Minimal, () =>
                    ScriptUtils.BeginScript().
                    AllowGas(keys.Address, Address.Null, 1, 9999).
                    CallContract("test", "getTime").
                    SpendGas(keys.Address).
                    EndScript());
            var block = simulator.EndBlock().First();

            var vmObj = simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, "test", "getTime");
            var temp = vmObj.AsNumber();
            Console.WriteLine($"\n\n\nTemp:{temp}");

            var convert = DateTimeOffset.FromUnixTimeSeconds((long)long.Parse(temp.ToDecimal()));
            Console.WriteLine($"\n\n\nTemp:{convert}");

            //var vmObj = simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, "test", "getTime");
            //var temp = vmObj.AsNumber();
            //
        }


        [Test]
        public void TestMintInsideOnBurn()
        {
            var keys = PhantasmaKeys.Generate();
            var symbol = "TEST";
            var name = "Test On Burn";
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
                    import List;
                    import Map;
                    global _address:address;
                    global _owner:address;
                    global _unlockStorageMap: storage_map<number, number>;
                    global _nft_list: storage_map<number, number>;

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
                    }

                    import Call;

                    constructor(owner:address)	{
                        _address = @P2KEYzWsbrMbPNtW1tBzzDKeYxYi4hjzpx4EfiyRyaoLkMM;
                        _owner= owner;
                        NFT.createSeries(owner, $THIS_SYMBOL, 0, 999, TokenSeries.Unique, myNFT);
                    }

                    public mint(dest:address):number {
                        local rom:someStruct = Struct.someStruct(Time.now(), _address, 1, ""hello"", ""desc"", ""imgURL"", ""info"");
                        local tokenID:number = NFT.mint(_owner, dest, $THIS_SYMBOL, rom, 0, 0);
                        _unlockStorageMap.set(tokenID, 0);
                        _nft_list.set(tokenID, 1);
                        Call.interop<none>(""Map.Set"",  ""_unlockStorageMap"", tokenID, 111);
                        return tokenID;
                    }

                    public burn(from:address, symbol:string, id:number) {
                        NFT.burn(from, symbol, id);
                    }

                    trigger onBurn(from:address, to:address, symbol:string, tokenID:number)
                    {
                        if (symbol != $THIS_SYMBOL) {
                            return;
                        }

                        _nft_list.remove(tokenID);
                        local rom:someStruct = Struct.someStruct(Time.now(), _address, 1, ""hello"", ""desc"", ""imgURL"", ""info"");

                        local newID:number = NFT.mint(_owner, to, $THIS_SYMBOL, rom, 0, 0);          
                        _nft_list.set(newID, 1);

                        return;
                    }

                    public exist(nftID:number): bool {
                        local myNumber : number = _nft_list.get(nftID);
                        if ( myNumber != 0 ) {
                            return true;
                        }

                        return false;
                    }
                }";
            var nexus = new Nexus("simnet", null, null);
            nexus.SetOracleReader(new OracleSimulator(nexus));
            var simulator = new NexusSimulator(nexus, keys, 1234);
            var user = PhantasmaKeys.Generate();

            simulator.BeginBlock();
            simulator.MintTokens(keys, user.Address, "KCAL", UnitConversion.ToBigInteger(1000, 10));
            simulator.EndBlock();


            var parser = new TombLangCompiler();
            var contract = parser.Process(sourceCode).First();
            //Console.WriteLine("contract asm: " + contract.asm);

            simulator.BeginBlock();
            simulator.GenerateCustomTransaction(keys, ProofOfWork.Minimal,
                    () => ScriptUtils.BeginScript().AllowGas(keys.Address, Address.Null, 1, 9999)
                    //.CallInterop("Runtime.createToken", keys.Address, symbol, contract.script, contract.abi.ToByteArray())
                    .CallInterop("Nexus.CreateToken", keys.Address, contract.script, contract.abi.ToByteArray())
                    .SpendGas(keys.Address)
                    .EndScript());
            simulator.EndBlock();

            DateTime time = DateTime.Today.AddDays(-1);
            DateTimeOffset utcTime2 = time;

            simulator.BeginBlock();
            var tx = simulator.GenerateCustomTransaction(user, ProofOfWork.Minimal, () =>
                    ScriptUtils.BeginScript().
                    AllowGas(user.Address, Address.Null, 1, 9999).
                    CallContract(symbol, "mint", user.Address).
                    SpendGas(user.Address).
                    EndScript());
            var block = simulator.EndBlock().FirstOrDefault();

            var callResultBytes = block.GetResultForTransaction(tx.Hash);
            var callResult = Serialization.Unserialize<VMObject>(callResultBytes);
            var nftID = callResult.AsNumber();

            Assert.IsTrue(nftID != 0, "NFT error");

            simulator.BeginBlock();
            simulator.GenerateCustomTransaction(user, ProofOfWork.Minimal, () =>
                    ScriptUtils.BeginScript().
                    AllowGas(user.Address, Address.Null, 1, 9999).
                    CallContract(symbol, "burn", user.Address, symbol, nftID).
                    SpendGas(user.Address).
                    EndScript());
            block = simulator.EndBlock().First();

            simulator.BeginBlock();
            tx = simulator.GenerateCustomTransaction(user, ProofOfWork.Minimal, () =>
                    ScriptUtils.BeginScript().
                    AllowGas(user.Address, Address.Null, 1, 9999).
                    CallContract(symbol, "exist", nftID).
                    SpendGas(user.Address).
                    EndScript());
            block = simulator.EndBlock().First();

            callResultBytes = block.GetResultForTransaction(tx.Hash);
            callResult = Serialization.Unserialize<VMObject>(callResultBytes);
            var exists = callResult.AsBool();
            Assert.IsFalse(exists, "It shouldn't exist...");
        }*/
        
    }
}
