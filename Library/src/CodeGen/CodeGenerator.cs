using Phantasma.Tomb.AST;
using System.Text;
using System.Collections.Generic;

namespace Phantasma.Tomb.CodeGen
{
	public class CodeGenerator
	{
		private StringBuilder _sb = new StringBuilder();
		private StringBuilder _includedBuiltinCodeBuffer = new StringBuilder(); // used for builtins

		public static Scope currentScope = null;
		public static int currentSourceLine = 0;

		public int LineCount;

		private Dictionary<string, string> _builtinMethodMap = new Dictionary<string, string>();

		public CodeGenerator()
		{
		}

		public static string Tabs(int n)
		{
			return new string('\t', n);
		}

		public void AppendLine(Node node, string line = "")
		{
			if (node.LineNumber <= 0)
			{
				throw new CompilerException("line number failed for " + node.GetType().Name);
			}

			while (currentSourceLine <= node.LineNumber)
			{
				if (currentSourceLine > 0)
				{
					var lineContent = Compiler.Instance.GetLine(currentSourceLine);
					_sb.AppendLine($"// Line {currentSourceLine}:" + lineContent);
					LineCount++;
				}
				currentSourceLine++;
			}

			line = Tabs(currentScope.Level) + line;
			_sb.AppendLine(line);
			LineCount++;
		}

		public void IncBuiltinReference(string builtinMethodName)
		{
			if (_builtinMethodMap.ContainsKey(builtinMethodName))
			{
				return;
			}

			var builtin = Builtins.GetMethod(builtinMethodName);
			var code = builtin.Code;

			_builtinMethodMap[builtinMethodName] = code;

			if (_includedBuiltinCodeBuffer.Length == 0)
			{
				_includedBuiltinCodeBuffer.AppendLine();
				_includedBuiltinCodeBuffer.AppendLine("// =======> BUILTINS SECTION");
			}

			_includedBuiltinCodeBuffer.Append(code);
		}

		public override string ToString()
		{
			return _sb.ToString() + _includedBuiltinCodeBuffer.ToString();
		}

	}
}
