using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Phantasma.Core.Domain.VM.Structs;

/// <summary>
/// Source-to-bytecode mapping emitted by assembler/codegen.
/// Required by compiler diagnostics and ABI offset recovery.
/// </summary>
public class DebugInfo
{
    public readonly string FileName;
    public readonly DebugRange[] Ranges;

    public DebugInfo(string fileName, IEnumerable<DebugRange> ranges)
    {
        FileName = fileName;
        Ranges = ranges.ToArray();
    }

    public int FindLine(int offset)
    {
        foreach (var range in Ranges)
        {
            if (offset >= range.StartOffset && offset <= range.EndOffset)
            {
                return (int)range.SourceLine;
            }
        }

        return -1;
    }

    public int FindOffset(int line)
    {
        foreach (var range in Ranges)
        {
            if (range.SourceLine == line)
            {
                return range.StartOffset;
            }
        }

        return -1;
    }

    public void Serialize(BinaryWriter writer)
    {
        writer.WriteVarString(FileName);
        writer.Write(Ranges.Length);
        foreach (var range in Ranges)
        {
            writer.Write(range.SourceLine);
            writer.Write(range.StartOffset);
            writer.Write(range.EndOffset);
        }
    }

    public byte[] ToByteArray()
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        Serialize(writer);
        return stream.ToArray();
    }

    public string ToJSON()
    {
        var payload = new
        {
            file = FileName,
            ranges = Ranges.Select(r => new
            {
                line = r.SourceLine,
                start = r.StartOffset,
                end = r.EndOffset,
            }),
        };

        return JsonSerializer.Serialize(payload, new JsonSerializerOptions
        {
            WriteIndented = true,
        });
    }
}
