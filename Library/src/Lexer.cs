using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Phantasma.Tomb.AST;

namespace Phantasma.Tomb
{
	public enum TokenKind
	{
		Invalid,
		Separator,
		Operator,
		Selector,
		Keyword,
		Identifier,
		Type,
		String,
		Number,
		Decimal,
		Bool,
		Address,
		Hash,
		Bytes,
		Asm,
		Macro,
		Postfix,
	}

	public struct LexerToken
	{
		public readonly int column;
		public readonly int line;
		public readonly string value;
		public readonly TokenKind kind;

		public LexerToken(int column, int line, string value, TokenKind kind)
		{
			this.column = column;
			this.line = line;
			this.value = value;
			this.kind = kind;
		}

		public override string ToString()
		{
			return $"{kind} => {value}";
		}
	}

	public abstract class Lexer
	{
		public static readonly string AsmTag = ":ASM:";

		public static Lexer? Instance { get; private set; }
		public static Lexer Active
		{
			get
			{
				if (Instance != null)
				{
					return Instance;
				}

				throw new InvalidOperationException("lexer instance not initialized");
			}
		}

		public abstract string[] Keywords { get; }
		public abstract string[] VarTypeNames { get; }

		public abstract string AssignmentOperator { get; }

		public abstract string[] ComparisonOperators { get; }
		public abstract string[] ArithmeticOperators { get; }
		public abstract string[] LogicalOperators { get; }
		public abstract string[] BitshiftOperators { get; }
		public abstract string[] CompoundAssigmentOperators { get; }

		public abstract char[] Separators { get; }

		public string[] Operators => GetOperators();

		private string[]? _operators;

		public Lexer()
		{
			Instance = this;
		}

		private string[] GetOperators()
		{
			if (_operators != null)
			{
				return _operators;
			}

			_operators = new[] { AssignmentOperator }.
			Concat(ComparisonOperators).
			Concat(ArithmeticOperators).
			Concat(LogicalOperators).
			Concat(BitshiftOperators).
			Concat(CompoundAssigmentOperators).
			ToArray();

			return _operators;
		}

		protected abstract LexerToken GenerateToken(int column, int line, string value);

		internal int GetOperatorMatches(string val)
		{
			if (string.IsNullOrEmpty(val))
			{
				return 0;
			}

			int result = 0;

			for (int i = 0; i < Operators.Length; i++)
			{
				if (Operators[i].StartsWith(val))
				{
					result++;
				}
			}

			return result;
		}

		internal bool IsSeparatorSymbol(char ch)
		{
			return Separators.Any(x => x == ch);
		}

		public enum CommentMode
		{
			None,
			Single,
			Multi
		}

		public bool IsValidIdentifier(string identifier)
		{
			if (string.IsNullOrEmpty(identifier))
			{
				return false;
			}

			for (int i = 0; i < identifier.Length; i++)
			{
				var ch = identifier[i];

				if (char.IsLetter(ch) || ch == '_')
				{
					continue;
				}

				if (i > 0 && char.IsDigit(ch))
				{
					continue;
				}

				if (i > 0 && (ch == '[' || ch == ']')) // Array support
				{
					continue;
				}

				return false;
			}

			if (Keywords.Contains(identifier))
			{
				return false;
			}

			return true;
		}

		public List<LexerToken> Process(string sourceCode)
		{
			var tokens = new List<LexerToken>();
			int i = 0;

			int tokenX = 0;
			int tokenY = 0;

			int col = 0;
			int line = 1;
			var sb = new StringBuilder();

			bool insideString = false;
			bool insideAsm = false;
			bool insideNumber = false;
			var insideComment = CommentMode.None;

			LexerToken prevToken = new LexerToken(0, 0, "", TokenKind.Invalid);
			LexerToken curToken = prevToken;

			Action finishToken = () =>
			{
				if (sb.Length == 0)
				{
					return;
				}

				var val = sb.ToString();

				prevToken = curToken;
				curToken = GenerateToken(tokenX, tokenY, val);
				tokens.Add(curToken);
				sb.Clear();

				insideNumber = false;

				if (val == "{" && prevToken.value == "asm")
				{
					insideAsm = true;
					sb.Append(AsmTag); // hack for lexer Token to detect this later as "asm"
				}

				tokenX = col;
				tokenY = line;
			};


			char lastChar = '\0';
			while (i < sourceCode.Length)
			{
				var ch = sourceCode[i];
				i++;
				col++;

				if (insideString)
				{
					sb.Append(ch);

					if (ch == '"')
					{
						insideString = false;
					}

					continue;
				}
				else
				if (insideAsm)
				{
					if (ch == '}')
					{
						insideAsm = false;
					}
					else
					{
						if (ch == '\n')
						{
							line++;
						}

						sb.Append(ch);
						continue;
					}
				}
				else
				if (insideComment == CommentMode.Single)
				{
					if (ch == '\n')
					{
						insideComment = CommentMode.None;
					}
					else
					{
						continue;
					}
				}
				else
				if (insideComment == CommentMode.Multi)
				{
					if (ch == '/' && lastChar == '*')
					{
						insideComment = CommentMode.None;
					}
					else
					{
						if (ch == '\n')
						{
							line++;
						}
						lastChar = ch;
					}
					continue;
				}

				if (ch == '/' && lastChar == ch)
				{
					sb.Length--;
					insideComment = CommentMode.Single;
					continue;
				}

				if (ch == '*' && lastChar == '/')
				{
					sb.Length--;
					insideComment = CommentMode.Multi;
					continue;
				}

				switch (ch)
				{
					case '\t':
					case ' ':
						{
							finishToken();
							break;
						}

					case '\r':
						break;

					case '\n':
						col = 0;
						line++;
						break;

					default:

						if (ch == '\"')
						{
							finishToken();

							sb.Append(ch);
							insideString = true;
						}
						else
						if (IsSeparatorSymbol(ch))
						{
							if (ch == '.' && insideNumber)
							{
								sb.Append(ch);
							}
							else
							{
								finishToken();
								sb.Append(ch);

								finishToken();
							}
						}
						else
						{
							var prev = sb.ToString();
							var prevOpMatches = GetOperatorMatches(prev);

							var chOpMatches = GetOperatorMatches("" + ch);

							if (prevOpMatches == 0 && chOpMatches > 0)
							{
								finishToken();
							}
							else
							if (prevOpMatches > 0 && chOpMatches == 0)
							{
								if (!Operators.Contains(prev))
								{
									throw new Exception("Possible operator bug in lexer?");
								}

								finishToken();
							}

							sb.Append(ch);

							var tmp = sb.ToString();
							var opMatches = insideNumber ? 0 : GetOperatorMatches(tmp);

							if (opMatches == 1 && Operators.Contains(tmp))
							{
								finishToken();
							}
						}

						// Prevent a property accessor token (eg: "arr.length") from being
						// reclassified as numeric only because it currently contains a dot.
						if (sb.Length != 0 && char.IsDigit(ch) && CheckIfStringIsNumber(sb.ToString()))
						{
							insideNumber = true;
						}

						break;
				}

				lastChar = ch;
			}

			if (sb.Length > 0)
			{
				var val = sb.ToString();
				tokens.Add(GenerateToken(tokenX, tokenY, val));
			}

			return tokens;
		}

		private static bool CheckIfStringIsNumber(string str)
		{
			var foundDot = false;

			for (int i = 0; i < str.Length; i++)
			{
				var ch = str[i];
				if (char.IsDigit(ch))
				{
					continue;
				}

				if (ch == '.')
				{
					if (i == 0 || foundDot)
					{
						return false;
					}

					foundDot = true;
					continue;
				}

				if (ch == '-')
				{
					if (i > 0)
					{
						return false;
					}

					continue;
				}

				return false;
			}

			return true;
		}
	}
}
