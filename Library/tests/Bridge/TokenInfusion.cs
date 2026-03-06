using System.IO;
using System.Numerics;
using PhantasmaPhoenix.Core;

namespace TOMBLib.Tests.Bridge
{
    // Test-only shape used for NFT.getInfusions() assertions.
    public struct TokenInfusion : ISerializable
    {
        public string Symbol;
        public BigInteger Value;

        public TokenInfusion(string symbol, BigInteger value)
        {
            Symbol = symbol;
            Value = value;
        }

        public void SerializeData(BinaryWriter writer)
        {
            writer.Write(Symbol ?? string.Empty);

            var bytes = Value.ToByteArray();
            writer.Write(bytes.Length);
            writer.Write(bytes);
        }

        public void UnserializeData(BinaryReader reader)
        {
            Symbol = reader.ReadString();

            var byteCount = reader.ReadInt32();
            var bytes = reader.ReadBytes(byteCount);
            Value = new BigInteger(bytes);
        }
    }
}
