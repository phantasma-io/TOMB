using System.IO;

namespace Phantasma.Core.Domain.Events.Structs;

/// <summary>
/// Minimal ABI event descriptor used by compiler artifacts.
/// This model stays in TOMB as part of the compiler ABI serialization contract.
/// </summary>
public class ContractEvent
{
	public readonly byte value;
	public readonly string name;
	public readonly VMType returnType;
	public readonly byte[] description;

	public ContractEvent(byte value, string name, VMType returnType, byte[] description)
	{
		this.value = value;
		this.name = name;
		this.returnType = returnType;
		this.description = description;
	}

	public static ContractEvent Unserialize(BinaryReader reader)
	{
		var value = reader.ReadByte();
		var name = reader.ReadVarString();
		var returnType = (VMType)reader.ReadByte();
		var description = reader.ReadByteArray();
		return new ContractEvent(value, name, returnType, description);
	}

	public void Serialize(BinaryWriter writer)
	{
		writer.Write(value);
		writer.WriteVarString(name);
		writer.Write((byte)returnType);
		writer.WriteByteArray(description);
	}
}
