using Phantasma.Tomb.AST;
using System;

namespace Phantasma.Tomb
{
	public class CompilerException : Exception
	{
		private static string FetchCurrentLine()
		{
			return Compiler.HasInstance ? Compiler.Instance.CurrentLine.ToString() : "???";
		}

		public CompilerException(string msg) : base($"line {FetchCurrentLine()}: {msg}")
		{

		}

		public CompilerException(Node node, string msg) : base($"line {node.LineNumber}: {msg}")
		{

		}
	}
}
