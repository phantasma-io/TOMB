using System.Linq;
using Phantasma.Core.Domain.Contract;
using Phantasma.Core.Domain.Contract.Structs;
using Phantasma.Core.Domain.Events.Structs;

namespace Phantasma.Business.Blockchain.Tokens
{
	// NFT metadata ABI generation is part of the compiler contract surface.
	public static class TokenUtils
	{
		public static ContractInterface GetNFTStandard()
		{
			var parameters = new ContractParameter[]
			{
				new("tokenID", VMType.Number),
			};

			var methods = new ContractMethod[]
			{
				new("getName", VMType.String, -1, parameters),
				new("getDescription", VMType.String, -1, parameters),
				new("getImageURL", VMType.String, -1, parameters),
				new("getInfoURL", VMType.String, -1, parameters),
			};

			return new ContractInterface(methods, Enumerable.Empty<ContractEvent>());
		}
	}
}
