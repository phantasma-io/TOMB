namespace Phantasma.Core.Domain.Contract.Structs;

/// <summary>
/// ABI method parameter descriptor used by local ContractMethod/ContractInterface models.
/// </summary>
public struct ContractParameter
{
	public readonly string name;
	public readonly VMType type;

	public ContractParameter(string name, VMType vmtype)
	{
		this.name = name;
		this.type = vmtype;
	}
}
