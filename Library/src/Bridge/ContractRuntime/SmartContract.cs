using System.Collections.Generic;
using System.Text;

namespace Phantasma.Core.Domain.Contract
{
	// Compiler macros depend on deterministic contract-address/key derivation.
	public static class SmartContract
	{
		public const string ConstructorName = "Initialize";

		private static readonly Dictionary<string, Address> ContractNameMap = new(System.StringComparer.Ordinal);

		public static Address GetAddressFromContractName(string name)
		{
			if (ContractNameMap.TryGetValue(name, out var address))
			{
				return address;
			}

			address = Address.FromHash(name);
			ContractNameMap[name] = address;
			return address;
		}

		public static byte[] GetKeyForField(string contractName, string fieldName, bool isProtected)
		{
			var visibility = isProtected ? "protected" : "public";
			return Encoding.UTF8.GetBytes($"{contractName}:{visibility}:{fieldName}");
		}
	}
}
