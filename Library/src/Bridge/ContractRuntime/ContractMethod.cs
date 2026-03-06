using System;
using System.Collections.Generic;
using System.IO;
using Phantasma.Core.Domain.Contract.Structs;

namespace Phantasma.Core.Domain.Contract;

/// <summary>
/// Compiler-facing ABI method descriptor.
/// Kept in TOMB because Phoenix SDK does not provide this compiler ContractMethod model.
/// </summary>
public class ContractMethod
{
    public readonly string name;
    public readonly VMType returnType;
    public readonly ContractParameter[] parameters;
    public int offset;

    public ContractMethod(string name, VMType returnType, Dictionary<string, int> labels, params ContractParameter[] parameters)
    {
        if (!labels.ContainsKey(name))
        {
            throw new Exception("Missing offset in label map for method " + name);
        }

        this.name = name;
        this.offset = labels[name];
        this.returnType = returnType;
        this.parameters = parameters;
    }

    public ContractMethod(string name, VMType returnType, int offset, params ContractParameter[] parameters)
    {
        this.name = name;
        this.offset = offset;
        this.returnType = returnType;
        this.parameters = parameters;
    }

    public bool IsProperty()
    {
        // Property naming convention is compiler contract semantics.
        if (name.Length >= 4 && name.StartsWith("get") && char.IsUpper(name[3])) return true;
        if (name.Length >= 3 && name.StartsWith("is") && char.IsUpper(name[2])) return true;
        return false;
    }

    public bool IsTrigger()
    {
        // Trigger naming convention is compiler contract semantics.
        return name.Length >= 3 && name.StartsWith("on") && char.IsUpper(name[2]);
    }

    public override string ToString()
    {
        return $"{name} : {returnType}";
    }

    public static ContractMethod Unserialize(BinaryReader reader)
    {
        var name = reader.ReadVarString();
        var returnType = (VMType)reader.ReadByte();
        var offset = reader.ReadInt32();
        var len = reader.ReadByte();
        var parameters = new ContractParameter[len];
        for (int i = 0; i < len; i++)
        {
            var pName = reader.ReadVarString();
            var pVMType = (VMType)reader.ReadByte();
            parameters[i] = new ContractParameter(pName, pVMType);
        }

        return new ContractMethod(name, returnType, offset, parameters);
    }

    public void Serialize(BinaryWriter writer)
    {
        writer.WriteVarString(name);
        writer.Write((byte)returnType);
        writer.Write(offset);
        writer.Write((byte)parameters.Length);
        foreach (var entry in parameters)
        {
            writer.WriteVarString(entry.name);
            writer.Write((byte)entry.type);
        }
    }

    public byte[] ToArray()
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        Serialize(writer);
        return stream.ToArray();
    }
}
