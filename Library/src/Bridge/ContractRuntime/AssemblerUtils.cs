using System;
using System.Collections.Generic;
using Phantasma.Core.Domain.VM.Structs;

namespace Phantasma.Business.CodeGen.Assembler
{
    /// <summary>
    /// Text-asm to script compiler used by TOMB code generation.
    /// This stays local because Phoenix SDK does not expose this assembler pipeline.
    /// </summary>
    public static class AssemblerUtils
    {
        public static byte[] BuildScript(string asm)
        {
            var lines = asm.Split('\n');
            return BuildScript(lines);
        }

        public static byte[] BuildScript(IEnumerable<string> lines)
        {
            DebugInfo debugInfo;
            Dictionary<string, int> labels;
            return BuildScript(lines, null, out debugInfo, out labels);
        }

        public static byte[] BuildScript(IEnumerable<string> lines, string fileName, out DebugInfo debugInfo, out Dictionary<string, int> labels)
        {
            // Stage 1: parse asm lines into semantemes (labels/instructions).
            Semanteme[] semantemes;
            try
            {
                semantemes = Semanteme.ProcessLines(lines);
            }
            catch (Exception e)
            {
                throw new Exception("Error parsing the script" + e.ToString());
            }

            var sb = new ScriptBuilder();
            Semanteme tmp;
            byte[] script;

            var debugRanges = new List<DebugRange>();

            try
            {
                // Stage 2: emit bytecode and collect source->offset mapping per semanteme.
                foreach (var entry in semantemes)
                {
                    var startOffset = sb.CurrentSize;

                    tmp = entry;
                    entry.Process(sb);

                    var endOffset = sb.CurrentSize;

                    if (endOffset > startOffset) {
                        endOffset--;
                    }

                    debugRanges.Add(new DebugRange(entry.LineNumber, startOffset, endOffset));
                }
                script = sb.ToScript(out labels);
            }
            catch (Exception e)
            {
                throw new Exception("Error assembling the script: " + e.ToString());
            }

            if (fileName != null)
            {
                debugInfo = new DebugInfo(fileName, debugRanges);
            }
            else
            {
                debugInfo = null;
            }

            return script;
        }

        public static IEnumerable<string> CommentOffsets(IEnumerable<string> lines, DebugInfo debugInfo)
        {
            var offsets = new Dictionary<uint, int>();
            foreach (var range in debugInfo.Ranges)
            {
                offsets[range.SourceLine] = range.StartOffset;
            }

            uint lineNumber = 0;
            var output = new List<string>();
            foreach (var line in lines)
            {
                lineNumber++;

                var temp = line.Replace("\r\n", "").Replace("\n", "").Replace("\r", "");

                if (offsets.ContainsKey(lineNumber))
                {
                    var ofs = offsets[lineNumber];
                    output.Add($"{temp} // {ofs}");
                }
                else
                {
                    output.Add(temp);
                }
            }

            return output;
        }
    }
}
