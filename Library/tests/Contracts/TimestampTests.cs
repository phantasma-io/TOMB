namespace TOMBLib.Tests.Contracts;

public class TimestampTests
{
	/*[Test]
    public void TestContractTimestamp()
    {
        var keys = PhantasmaKeys.Generate();
        var sourceCode =
            @"
                contract test { 
                    import Time;
    
                    global time:timestamp;

                    public constructor(owner:address){
                        time = Time.now();
                    }
                        
                    public updateTime(newTime:timestamp){
                        time = newTime;
                    }  

                    public getTime():timestamp {
                        return time;
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
                .CallInterop("Runtime.DeployContract", keys.Address, "test", contract.script,
                    contract.abi.ToByteArray())
                .SpendGas(keys.Address)
                .EndScript());
        simulator.EndBlock();

        // test dateTime to timestamp
        Timestamp time = DateTime.Today.AddDays(-1);

        simulator.BeginBlock();
        simulator.GenerateCustomTransaction(keys, ProofOfWork.Minimal, () =>
            ScriptUtils.BeginScript().AllowGas(keys.Address, Address.Null, 1, 9999)
                .CallContract("test", "updateTime", time).SpendGas(keys.Address).EndScript());
        simulator.EndBlock();


        simulator.BeginBlock();
        simulator.GenerateCustomTransaction(keys, ProofOfWork.Minimal, () =>
            ScriptUtils.BeginScript().AllowGas(keys.Address, Address.Null, 1, 9999).CallContract("test", "getTime")
                .SpendGas(keys.Address).EndScript());
        simulator.EndBlock();
    }

    [Test]
    public void TestTimeStampFromNumber()
    {
        var keys = PhantasmaKeys.Generate();
        var sourceCode =
            @"
                contract test { 
                    import Time;
    
                    global time:timestamp;

                    public constructor(owner:address){
                        time = Time.now();
                    }
                        
                    public updateTime(newTime:number){
                        local newTimer:timestamp = newTime;
                        time = newTimer;
                    }  

                    public getTime():timestamp {
                        return time;
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
                .CallInterop("Runtime.DeployContract", keys.Address, "test", contract.script,
                    contract.abi.ToByteArray())
                .SpendGas(keys.Address)
                .EndScript());
        simulator.EndBlock();

        // test dateTime to timestamp
        //DateTime time = DateTime.Today.AddDays(-1);
        //DateTimeOffset utcTime2 = time;
        //BigInteger timeBig = (BigInteger)time.Ticks;
        //
        //simulator.BeginBlock();
        //var tx = simulator.GenerateCustomTransaction(keys, ProofOfWork.Minimal, () =>
        //        ScriptUtils.BeginScript().
        //        AllowGas(keys.Address, Address.Null, 1, 9999).
        //        CallContract("test", "updateTime", timeBig).
        //        SpendGas(keys.Address).
        //        EndScript());
        //simulator.EndBlock();
        //
        //
        //
        //
        //var block = simulator.EndBlock().First();
        //
        //var result = block.GetResultForTransaction(tx.Hash);
        //Assert.NotNull(result);
        //var obj = VMObject.FromBytes(result);
        //var ram = obj.AsTimestamp();
        //Assert.IsTrue(ram == 1);


        //var vmObj = simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, "test", "getTime");
        //var temp = vmObj.AsTimestamp();
        //Assert.IsTrue(temp == 123);


        simulator.BeginBlock();
        simulator.GenerateCustomTransaction(keys, ProofOfWork.Minimal, () =>
            ScriptUtils.BeginScript().AllowGas(keys.Address, Address.Null, 1, 9999).CallContract("test", "getTime")
                .SpendGas(keys.Address).EndScript());
        var block = simulator.EndBlock().First();

        var vmObj = simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, "test", "getTime");
        var temp = vmObj.AsTimestamp();
        Console.WriteLine($"\n\n\nTemp:{temp}");
    }*/
}
