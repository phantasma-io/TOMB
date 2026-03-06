using System.Numerics;
using Phantasma.Core.Domain.Contract;
using Phantasma.Core.Domain.Interfaces;

namespace Phantasma.Core.Domain.Token.Structs;

public struct TokenInfo : IToken
{
	public string Symbol { get; private set; }
	public string Name { get; private set; }
	public Address Owner { get; set; }
	public TokenFlags Flags { get; set; }
	public BigInteger MaxSupply { get; private set; }
	public int Decimals { get; private set; }
	public byte[] Script { get; private set; }
	public ContractInterface ABI { get; private set; }

	public TokenInfo(string symbol, string name, Address owner, BigInteger maxSupply, int decimals, TokenFlags flags, byte[] script, ContractInterface abi)
	{
		Symbol = symbol;
		Name = name;
		Owner = owner;
		Flags = flags;
		MaxSupply = maxSupply;
		Decimals = decimals;
		Script = script;
		ABI = abi;
	}
}
