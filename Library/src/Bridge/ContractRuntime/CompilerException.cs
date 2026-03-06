using System;

namespace Phantasma.Core.Domain.Exceptions;

public sealed class CompilerException : Exception
{
    public uint LineNumber { get; }

    public CompilerException(uint lineNumber, string message) : base($"Line {lineNumber}: {message}")
    {
        LineNumber = lineNumber;
    }

    public CompilerException(string message) : base(message)
    {
        LineNumber = 0;
    }
}
