using PhantasmaPhoenix.Protocol;
using Phantasma.Tomb.AST;
using Phantasma.Tomb.AST.Declarations;
using Phantasma.Tomb.AST.Expressions;
using Phantasma.Tomb.AST.Statements;
using Phantasma.Tomb.CodeGen;
using Phantasma.Tomb.Validation;

/* NOTE - In order to implement support for another programming language:
 * 1) derive a class from this and implement the abstract method GenerateModules
 * 2) implement the property FileExtension (it will be used to automatically instantiate your class whenever that extension is detected)
 * 3) if the language does not use common operators, override ParseOperator()
 */
namespace Phantasma.Tomb
{
	public enum ExecutionResult
	{
		Break,
		Yield,
	}

	public class CompilerAttribute : Attribute
	{
		public string Extension { get; set; }
	}

	public abstract class Compiler
	{
		public static bool DebugMode = false; // enable this to print some debugging info
											  // Runtime availability checks for native-contract calls are enabled by default to prevent
											  // generating bytecode that is known to fail against the current Carbon runtime baseline.
		public static NativeCheckMode NativeCheckMode { get; set; } = NativeCheckMode.Error;
		public static Action<string> WarningHandler { get; set; } = warning => Console.WriteLine(warning);

		public readonly int TargetProtocolVersion;

		public Lexer Lexer { get; private set; }

		protected List<LexerToken> tokens;
		private int tokenIndex = 0;

		private int currentLabel = 0;

		public int CurrentLine { get; private set; }
		public int CurrentColumn { get; private set; }

		private string[] lines;

		public static Compiler Instance { get; private set; }

		public static void EmitWarning(Node node, string warning)
		{
			var formatted = $"line {node.LineNumber}: {warning}";
			WarningHandler?.Invoke(formatted);
		}

		// This mode is allowed to use things like AddModule() to generate multiple modules
		// Feel also free to use the FetchToken(), Rewind() and the multiple ExpectXXX
		protected abstract void GenerateModules();

		protected abstract Lexer CreateLexer();

		public Compiler(int version = DomainSettings.LatestKnownProtocol)
		{
			TargetProtocolVersion = version;
			Instance = this;
		}

		public Module[] Process(string[] lines)
		{
			var sourceCode = string.Join('\n', lines);
			return Process(sourceCode);
		}

		public Module[] Process(string sourceCode)
		{
			this.Lexer = CreateLexer();

			// Reset deduplicated native-check warnings for each compilation run.
			NativeMethodAvailability.ResetSession();

			this.tokens = Lexer.Process(sourceCode);

			// uncomment this line to print all lexer tokens
			//foreach (var token in tokens) Console.WriteLine(token);

			this.lines = sourceCode.Replace("\r", "").Split('\n');

			_modules.Clear();

			GenerateModules();

			return _modules.ToArray();
		}

		private List<Module> _modules = new List<Module>();


		public void AddModule(Module module)
		{
			_modules.Add(module);
		}

		public Module FindModule(string name, bool mustBeCompiled)
		{
			foreach (var entry in _modules)
			{
				if (entry.Name == name)
				{
					if (mustBeCompiled && entry.script == null)
					{
						return null;
					}

					return entry;
				}
			}

			return null;
		}

		private const int MaxRegisters = VirtualMachine.DefaultRegisterCount;
		private Node[] registerAllocs = new Node[MaxRegisters];
		private string[] registerAlias = new string[MaxRegisters];

		public Register AllocRegister(CodeGenerator generator, Node node, string alias = null)
		{
			if (alias != null)
			{
				foreach (var entry in registerAlias)
				{
					if (entry == alias)
					{
						throw new Exception("alias already exists: " + alias);
					}
				}
			}

			int baseRegister = 1;
			for (int i = baseRegister; i < registerAllocs.Length; i++)
			{
				if (registerAllocs[i] == null)
				{
					registerAllocs[i] = node;
					registerAlias[i] = alias;

					string extra = alias != null ? " => " + alias : "";
					//Console.WriteLine(CodeGenerator.Tabs(CodeGenerator.currentScope.Level) + "alloc r" + i + extra);

					if (alias != null)
					{
						generator.AppendLine(node, $"ALIAS r{i} ${alias}");
					}

					return new Register(i, alias);
				}
			}

			throw new CompilerException("no more available registers");
		}

		public void DeallocRegister(ref Register register)
		{
			if (register == null)
			{
				return;
			}

			var index = register.Index;

			if (registerAllocs[index] != null)
			{
				var alias = registerAlias[index];

				//Console.WriteLine(CodeGenerator.Tabs(CodeGenerator.currentScope.Level) + "dealloc r" + index + " => "+alias);

				registerAllocs[index] = null;
				registerAlias[index] = null;

				register = null;
				return;
			}

			throw new CompilerException("register not allocated");
		}

		public void VerifyRegisters()
		{

			for (int i = 0; i < registerAllocs.Length; i++)
			{
				if (registerAllocs[i] != null)
				{
					throw new CompilerException($"register r{i} not deallocated");
				}
			}
		}

		public LoopStatement CurrentLoop { get; private set; }
		private Stack<LoopStatement> _loops = new Stack<LoopStatement>();
		public void PushLoop(LoopStatement loop)
		{
			_loops.Push(loop);
			CurrentLoop = loop;
		}

		public void PopLoop(LoopStatement loop)
		{
			if (CurrentLoop != loop)
			{
				throw new CompilerException("error popping loop node");
			}

			_loops.Pop();
			CurrentLoop = _loops.Count > 0 ? _loops.Peek() : null;
		}

		protected void Rewind(int steps = 1)
		{
			tokenIndex -= steps;
			if (tokenIndex < 0)
			{
				throw new CompilerException("unexpected rewind");
			}
		}

		public int AllocateLabel()
		{
			currentLabel++;
			return currentLabel;
		}

		protected bool HasTokens()
		{
			return tokenIndex < tokens.Count;
		}

		protected LexerToken FetchToken()
		{
			if (tokenIndex >= tokens.Count)
			{
				throw new CompilerException("unexpected end of file");
			}

			var token = tokens[tokenIndex];
			tokenIndex++;

			this.CurrentLine = token.line;
			this.CurrentColumn = token.column;

			//Console.WriteLine(token);
			return token;
		}

		protected abstract VarType ExpectType();

		protected void ExpectToken(string val, string msg = null)
		{
			var token = FetchToken();

			if (token.value != val)
			{
				throw new CompilerException(msg != null ? msg : ("expected " + val));
			}
		}

		protected string ExpectKind(TokenKind expectedKind)
		{
			var token = FetchToken();

			if (token.kind != expectedKind)
			{
				if (token.kind == TokenKind.Macro)
				{
					var macro = ResolveMacro(token.value);

					if (macro != null)
					{
						VarKind expectedMacroKind;

						switch (expectedKind)
						{
							case TokenKind.Number: expectedMacroKind = VarKind.Number; break;
							case TokenKind.Bool: expectedMacroKind = VarKind.Bool; break;
							case TokenKind.String: expectedMacroKind = VarKind.String; break;
							case TokenKind.Address: expectedMacroKind = VarKind.Address; break;
							default:
								throw new CompilerException($"expected {expectedKind}, got {token.kind} macro instead: " + token.value);
						}

						if (expectedMacroKind == macro.type.Kind)
						{
							return macro.GetValue();
						}
					}
					else
					{
						throw new CompilerException($"expected {expectedKind}, got unknown macro instead: " + token.value);
					}
				}

				throw new CompilerException($"expected {expectedKind}, got {token.kind} instead: " + token.value);
			}

			return token.value;
		}

		protected string ExpectIdentifier()
		{
			return ExpectKind(TokenKind.Identifier);
		}

		protected string[] ExpectAsm()
		{
			return ExpectKind(TokenKind.Asm).Split('\n').Select(x => x.TrimStart()).ToArray();
		}

		protected string ExpectString()
		{
			return ExpectKind(TokenKind.String);
		}

		protected string ExpectNumber()
		{
			return ExpectKind(TokenKind.Number);
		}

		protected string ExpectBool()
		{
			return ExpectKind(TokenKind.Bool);
		}


		public string GetLine(int index)
		{
			if (index <= 0 || index > lines.Length)
			{
				return "";
			}

			return lines[index - 1];
		}

		protected void SkipUntilToken(string expectedToken)
		{
			while (HasTokens())
			{
				var next = FetchToken();
				if (next.value == expectedToken)
				{
					break;
				}
			}
		}

		protected AssignStatement BuildPostfixStatement(Scope scope, string varName, string postfixOp)
		{
			var setCommand = new AssignStatement();

			var op = postfixOp == "++" ? OperatorKind.Addition : OperatorKind.Subtraction;

			setCommand.variable = scope.FindVariable(varName);
			var leftSide = new VarExpression(scope, setCommand.variable);
			var rightSide = new LiteralExpression(scope, "1", VarType.Find(VarKind.Number));
			setCommand.valueExpression = new BinaryExpression(scope, op, leftSide, rightSide);

			return setCommand;
		}

		protected Statement BuildBinaryShortAssigment(Scope scope, string varName, string opStr)
		{
			var setCommand = new AssignStatement();

			VarType expressionExpectedType;

			var indexerPos = varName.IndexOf("[");
			if (indexerPos >= 0)
			{
				if (!varName.EndsWith("]"))
				{
					throw new CompilerException("expected ] in array indexer");
				}

				var tmp = varName.Substring(indexerPos + 1, (varName.Length - indexerPos) - 2);

				varName = varName.Substring(0, indexerPos);

				var targetVarDecl = scope.FindVariable(varName);

				setCommand.variable = targetVarDecl;

				var arrayType = setCommand.variable.Type as ArrayVarType;

				if (arrayType == null)
				{
					var primitiveType = setCommand.variable.Type as PrimitiveVarType;
					switch (primitiveType.Kind)
					{
						case VarKind.Storage_Map:
							{
								var mapDecl = targetVarDecl as MapDeclaration;
								var mapLib = scope.Module.FindLibrary("Map");
								mapLib = mapLib.PatchMap(mapDecl);

								var methodCall = new MethodCallStatement();

								var keyExpr = ParseArrayIndexingExpression(scope, tmp, VarType.Find(VarKind.Any));

								var valExpr = ParseExpression(scope);

								var callExpr = new MethodCallExpression(scope);
								callExpr.method = mapLib.FindMethod("set");
								callExpr.arguments = new List<Expression>()
								{
									new LiteralExpression(scope, $"\"{mapDecl.Name}\"", VarType.Find(VarKind.String)),
									keyExpr,
									valExpr
								};

								callExpr.PatchGenerics();
								methodCall.expression = callExpr;

								return methodCall;
							}


						default:
							throw new CompilerException($"variable {setCommand.variable} cannot be indexed");
					}
				}

				expressionExpectedType = arrayType.elementType;
				setCommand.keyExpression = ParseArrayIndexingExpression(scope, tmp, expressionExpectedType);
			}
			else
			{
				setCommand.variable = scope.FindVariable(varName);
				expressionExpectedType = setCommand.variable.Type;
			}

			var expr = ParseAssignmentExpression(scope, opStr, setCommand.variable, expressionExpectedType);
			setCommand.valueExpression = expr;

			return setCommand;
		}

		protected Expression ExpectExpression(Scope scope)
		{
			var expr = ParseExpression(scope);
			if (expr == null)
			{
				throw new CompilerException("expected expression");
			}

			var macro = expr as MacroExpression;
			if (macro != null)
			{
				return macro.Unfold(scope);
			}

			return expr;
		}


		protected Expression ParseAssignmentExpression(Scope scope, string opStr, VarDeclaration varDecl, VarType expectedType)
		{
			var expr = ExpectExpression(scope);
			if (opStr != Lexer.AssignmentOperator)
			{
				var str = opStr.Substring(0, opStr.Length - 1);
				var op = ParseOperator(str);

				if (op == OperatorKind.Unknown)
				{
					throw new CompilerException("unknown operator: " + opStr);
				}

				expr = new BinaryExpression(scope, op, new VarExpression(scope, varDecl), expr);
			}

			expr = Expression.AutoCast(expr, expectedType);

			return expr;
		}

		private Expression ParseExpressionFromToken(LexerToken first, Scope scope)
		{
			switch (first.kind)
			{
				case TokenKind.Identifier:
					{
						var constDecl = scope.FindConstant(first.value, false);
						if (constDecl != null)
						{
							return new ConstExpression(scope, constDecl);
						}
						else
						{
							var indexerPos = first.value.IndexOf("[");
							if (indexerPos >= 0)
							{
								if (!first.value.EndsWith("]"))
								{
									throw new CompilerException("expected ] in array indexer");
								}

								var varName = first.value;

								var tmp = first.value.Substring(indexerPos + 1, (varName.Length - indexerPos) - 2);

								varName = varName.Substring(0, indexerPos);

								var arrayVar = scope.FindVariable(varName);

								var arrayType = arrayVar.Type as ArrayVarType;

								if (arrayType == null)
								{
									var primitiveType = arrayVar.Type as PrimitiveVarType;
									switch (primitiveType.Kind)
									{
										case VarKind.Storage_Map:
											{
												var mapDecl = arrayVar as MapDeclaration;
												var mapLib = scope.Module.FindLibrary("Map");
												mapLib = mapLib.PatchMap(mapDecl);

												var methodCall = new MethodCallExpression(scope);

												var keyExpr = ParseArrayIndexingExpression(scope, tmp, VarType.Find(VarKind.Any));

												methodCall.method = mapLib.FindMethod("get");

												methodCall.arguments = new List<Expression>()
												{
													new LiteralExpression(scope, $"\"{mapDecl.Name}\"", VarType.Find(VarKind.String)),
													keyExpr
												};

												methodCall.PatchGenerics();

												return methodCall;
											}

										default:
											throw new CompilerException($"variable {arrayVar} cannot be indexed");
									}
								}

								var indexExpression = ParseArrayIndexingExpression(scope, tmp, VarType.Find(VarKind.Number));
								return new ArrayElementExpression(scope, arrayVar, indexExpression);
							}

							var varDecl = scope.FindVariable(first.value, false);
							if (varDecl != null)
							{
								return new VarExpression(scope, varDecl);
							}

							var libDecl = scope.Module.FindLibrary(first.value, false);
							if (libDecl != null)
							{
								throw new NotImplementedException();
							}

							var method = scope.Module.FindMethod(first.value);
							if (method != null)
							{
								return new LiteralExpression(scope, first.value, VarType.Find(VarKind.Method, method));
							}

							var module = scope.Module.FindModule(first.value, true);
							if (module != null)
							{
								return new LiteralExpression(scope, first.value, VarType.Find(VarKind.Module, method));
							}

							throw new CompilerException("unknown identifier: " + first.value);
						}
					}

				case TokenKind.Number:
					{
						return new LiteralExpression(scope, first.value, VarType.Find(VarKind.Number));
					}

				case TokenKind.Decimal:
					{
						var temp = first.value.Split('.')[1];
						var decimals = temp.Length;
						return new LiteralExpression(scope, first.value, VarType.Find(VarKind.Decimal, decimals));
					}

				case TokenKind.String:
					{
						return new LiteralExpression(scope, first.value, VarType.Find(VarKind.String));
					}

				case TokenKind.Bool:
					{
						return new LiteralExpression(scope, first.value, VarType.Find(VarKind.Bool));
					}

				case TokenKind.Address:
					{
						return new LiteralExpression(scope, first.value, VarType.Find(VarKind.Address));
					}

				case TokenKind.Hash:
					{
						return new LiteralExpression(scope, first.value, VarType.Find(VarKind.Hash));
					}

				case TokenKind.Bytes:
					{
						return new LiteralExpression(scope, first.value, VarType.Find(VarKind.Bytes));
					}

				case TokenKind.Macro:
					{
						var args = new List<string>();

						var next = this.FetchToken();
						if (next.value == "(")
						{
							do
							{
								next = this.FetchToken();
								if (next.value == ")")
								{
									break;
								}
								else
								{
									switch (next.kind)
									{
										case TokenKind.Asm:
										case TokenKind.Macro:
										case TokenKind.Operator:
										case TokenKind.Separator:
										case TokenKind.Selector:
											throw new CompilerException($"not valid macro argument: {next.kind}");


										default:
											args.Add(next.value);
											break;
									}
								}
							} while (true);
						}
						else
						{
							Rewind();
						}

						var macro = new MacroExpression(scope, first.value, args);

						return macro.Unfold(scope);
					}

				default:
					throw new CompilerException($"cannot turn {first.kind} to an expression");
			}
		}

		// TODO: here we only suppprt variables or literal expressions as index, but could be expand later...
		private Expression ParseArrayIndexingExpression(Scope scope, string exprStr, VarType elementType)
		{
			var varDecl = scope.FindVariable(exprStr, false);
			if (varDecl != null)
			{
				return new VarExpression(scope, varDecl);
			}

			return new LiteralExpression(scope, exprStr, elementType);
		}

		private Expression ParseBinaryExpression(Scope scope, LexerToken opToken, Expression leftSide, Expression rightSide)
		{
			if (opToken.kind != TokenKind.Operator)
			{
				throw new CompilerException("expected operator, got " + opToken.kind);
			}

			var op = ParseOperator(opToken.value);

			if (op == OperatorKind.Unknown)
			{
				throw new CompilerException("unknown operator: " + opToken.value);
			}

			if (rightSide.ResultType != leftSide.ResultType)
			{
				if (leftSide.ResultType.Kind == VarKind.String && op == OperatorKind.Addition)
				{
					rightSide = new CastExpression(scope, VarType.Find(VarKind.String), rightSide);
				}
				else
				if (leftSide.ResultType.Kind == VarKind.Number && rightSide.ResultType.Kind == VarKind.Timestamp && op == OperatorKind.Subtraction)
				{
					rightSide = new CastExpression(scope, VarType.Find(VarKind.Number), rightSide);
				}
				else
				if (leftSide.ResultType.Kind == VarKind.Timestamp && rightSide.ResultType.Kind == VarKind.Number && op == OperatorKind.Addition)
				{
					rightSide = new CastExpression(scope, VarType.Find(VarKind.Timestamp), rightSide);
				}
				else
				if (leftSide.ResultType.Kind == VarKind.Generic || rightSide.ResultType.Kind == VarKind.Generic)
				{
					throw new Exception("Compiler bug, probably missing a call to PatchGenerics() in a methodcallexpression somewhere?");
					// DO NOTHING, we could issue an warning here (better would be to resolve the generic type into primitive if possible)
				}
				else
				{
					throw new CompilerException($"type mistmatch, {leftSide.ResultType} on left, {rightSide.ResultType} on right");
				}
			}

			if (op == OperatorKind.Different)
			{
				var innerExpr = new BinaryExpression(scope, OperatorKind.Equal, leftSide, rightSide);

				return new NegationExpression(scope, innerExpr);
			}

			return new BinaryExpression(scope, op, leftSide, rightSide);
		}

		protected Expression ParseExpression(Scope scope, bool allowBinary = true)
		{
			var first = FetchToken();

			if (first.kind == TokenKind.Operator && (first.value == "!" || first.value == "-"))
			{
				var expr = ParseExpression(scope, allowBinary);
				return new NegationExpression(scope, expr);
			}

			var second = FetchToken();

			switch (second.kind)
			{
				case TokenKind.Separator:
					{
						Rewind();
						return ParseExpressionFromToken(first, scope);
					}

				case TokenKind.Operator:
					{
						var leftSide = ParseExpressionFromToken(first, scope);

						if (!allowBinary)
						{
							Rewind();
							return leftSide;
						}

						var rightSide = ParseExpression(scope, false);
						if (rightSide == null)
						{
							Rewind();
							var token = FetchToken();

							if (token.value != null)
							{
								if (token.value.Equals("and", StringComparison.OrdinalIgnoreCase))
								{
									throw new CompilerException($"Operator 'and' is obsolete, please use && instead");
								}

								if (token.value.Equals("or", StringComparison.OrdinalIgnoreCase))
								{
									throw new CompilerException($"Operator 'or' is obsolete, please use || instead");
								}
							}

							throw new CompilerException($"expected right side expression. Possible compiler error?");
						}

						var result = ParseBinaryExpression(scope, second, leftSide, rightSide);

						var next = FetchToken();
						if (next.kind == TokenKind.Operator)
						{
							var third = ParseExpression(scope);
							var op = ParseOperator(next.value);
							return new BinaryExpression(scope, op, result, third);
						}
						else
						{
							Rewind();
							return result;
						}
					}

				case TokenKind.Selector:
					{
						var varDecl = scope.FindVariable(first.value, false);

						LibraryDeclaration libDecl;

						Expression leftSide = null;

						bool implicitIsLiteral = true;

						if (varDecl != null)
						{
							// TODO this code is duplicated, copypasted from other method above, refactor this later...
							switch (varDecl.Type.Kind)
							{
								case VarKind.Storage_Map:
									{
										var mapDecl = (MapDeclaration)varDecl;
										libDecl = scope.Module.FindLibrary("Map");
										libDecl = libDecl.PatchMap(mapDecl);
										break;
									}

								case VarKind.Storage_List:
									{
										var listDecl = (ListDeclaration)varDecl;
										libDecl = scope.Module.FindLibrary("List");
										libDecl = libDecl.PatchList(listDecl);
										break;
									}

								case VarKind.Storage_Set:
									{
										var setDecl = (SetDeclaration)varDecl;
										libDecl = scope.Module.FindLibrary("Set");
										libDecl = libDecl.PatchSet(setDecl);
										break;
									}

								case VarKind.Struct:
									{
										libDecl = null;

										leftSide = ParseStructFieldExpression(scope, varDecl);
										break;
									}

								default:
									{
										var typeName = varDecl.Type.Kind.ToString();
										libDecl = scope.Module.FindLibrary(typeName, false);
										if (libDecl != null)
										{
											implicitIsLiteral = false;

											if (varDecl.Type.Kind == VarKind.Enum)
											{
												// Patch Enum methods to use proper type
												foreach (var method in libDecl.methods.Values)
												{
													foreach (var arg in method.Parameters)
													{
														if (arg.Type.Kind == VarKind.Enum)
														{
															arg.Type = varDecl.Type;
														}
													}
												}
											}

											break;
										}

										throw new CompilerException($"expected {first.value} to be generic type, but is {varDecl.Type} instead. Or you might be missing \"Import {varDecl.Type.Kind}\"");
									}

							}
						}
						else
						if (_enums.ContainsKey(first.value))
						{
							var enumDecl = _enums[first.value];
							var entryName = ExpectIdentifier();
							if (enumDecl.entryNames.ContainsKey(entryName))
							{
								var enumValue = enumDecl.entryNames[entryName].ToString();
								return new LiteralExpression(scope, enumValue, VarType.Find(VarKind.Enum, first.value));
							}
							else
							{
								throw new CompilerException($"Enum {first.value} does not contain member {entryName}");

							}
						}
						else
						{
							libDecl = scope.Module.FindLibrary(first.value);
						}

						if (leftSide == null)
						{
							leftSide = ParseMethodExpression(scope, libDecl, varDecl, implicitIsLiteral);
						}

						second = FetchToken();
						if (second.kind == TokenKind.Operator)
						{
							var rightSide = ExpectExpression(scope);
							return ParseBinaryExpression(scope, second, leftSide, rightSide);
						}
						else
						{
							Rewind();
							return leftSide;
						}
					}

				default:
					if (first.kind == TokenKind.Separator)
					{
						switch (first.value)
						{
							case "(":
								{
									Rewind();
									var leftSide = ExpectExpression(scope);
									ExpectToken(")");
									var op = FetchToken();
									if (op.kind == TokenKind.Operator)
									{
										var rightSide = ExpectExpression(scope);
										return ParseBinaryExpression(scope, op, leftSide, rightSide);
									}

									Rewind();
									return leftSide;
								}

							case "{":
								{
									Rewind();

									var result = new ArrayExpression(scope);
									do
									{
										var token = FetchToken();
										if (token.value == "}")
										{
											break;
										}

										Rewind();

										if (result.elements.Count > 0)
										{
											ExpectToken(",");
										}

										var element = ExpectExpression(scope);

										if (result.elements.Count > 0)
										{
											var firstType = result.elements[0].ResultType;
											if (element.ResultType != firstType)
											{
												throw new CompilerException($"Elements of array initialization must all have type {firstType} but at least one element is of type {element.ResultType}");
											}
										}

										result.elements.Add(element);

									} while (true);
									return result;
								}

							default:
								throw new CompilerException($"unexpected token: {first.value}");
						}

					}
					break;
			}

			return null;
		}

		protected MethodCallExpression ParseMethodExpression(Scope scope, LibraryDeclaration library, VarDeclaration implicitArg = null, bool implicitAsLiteral = true)
		{
			var expr = new MethodCallExpression(scope);

			string methodName;

			if (library != null && library.Name == "Call")
			{
				var tmp = FetchToken();
				methodName = tmp.value;
			}
			else
			{
				methodName = ExpectIdentifier();
			}

			expr.method = library.FindMethod(methodName);

			var next = FetchToken();

			if (next.value == "<")
			{
				do
				{
					next = FetchToken();
					if (next.value == ">")
					{
						break;
					}

					Rewind();

					if (expr.generics.Count > 0)
					{
						ExpectToken(",");
					}

					var genType = ExpectType();

					if (genType.IsStorageBound || (genType.Kind != VarKind.None && genType.IsWeird))
					{
						throw new CompilerException($"{genType.Kind} can't be used as generic type");
					}

					expr.generics.Add(genType);
				} while (true);
			}
			else
			{
				Rewind();
			}

			expr.PatchGenerics();

			ExpectToken("(");

			var firstIndex = implicitArg != null ? 1 : 0;

			bool isVariadicContractGateway =
				(expr.method.Library.Name == "Call" && expr.method.Name == "contract") ||
				(expr.method.Library.Name == "Contract" && expr.method.Name == "call");

			// Both gateways share the same runtime semantics (contract + method + variadic args),
			// so parse them with the same variadic branch.
			bool isCallLibrary = expr.method.Library.Name == "Call";

			if (isCallLibrary || isVariadicContractGateway)
			{
				int i = 0;
				do
				{
					next = FetchToken();
					if (next.value == ")")
					{
						break;
					}
					else
					{
						Rewind();
					}

					if (i > firstIndex)
					{
						ExpectToken(",");
					}

					Expression arg;

					if (i == 0 && implicitArg != null)
					{
						arg = new LiteralExpression(scope, $"\"{implicitArg.Name}\"", VarType.Find(VarKind.String));
					}
					else
					{
						arg = ExpectExpression(scope);
					}

					expr.arguments.Add(arg);

					i++;
				} while (true);
			}
			else
			{
				var paramCount = expr.method.Parameters.Length;
				for (int i = 0; i < paramCount; i++)
				{
					if (i > firstIndex)
					{
						ExpectToken(",", $"missing arguments for {expr.method.Library.Name}.{methodName}(), got {expr.arguments.Count} but expected {paramCount}");
					}

					Expression arg;

					if (i == 0 && implicitArg != null)
					{
						if (implicitAsLiteral)
						{
							arg = new LiteralExpression(scope, $"\"{implicitArg.Name}\"", VarType.Find(VarKind.String));
						}
						else
						{
							arg = new VarExpression(scope, implicitArg);
						}
					}
					else
					{
						arg = ExpectExpression(scope);
					}

					expr.arguments.Add(arg);

					var expectedType = expr.method.Parameters[i].Type;

					arg = Expression.AutoCast(arg, expectedType);

					if (arg.ResultType != expectedType && expectedType.Kind != VarKind.Any)
					{
						throw new CompilerException($"expected argument of type {expectedType}, got {arg.ResultType} instead");
					}
				}

				//ExpectToken(")");
				var endToken = FetchToken();

				if (endToken.value != ")")
				{
					throw new CompilerException($"expected {paramCount} arguments to method {expr.method.Name}, but got {paramCount + 1} (or more) instead");
				}
			}

			return expr;
		}


		private StructFieldExpression ParseStructFieldExpression(Scope scope, VarDeclaration varDecl)
		{
			var fieldName = ExpectIdentifier();
			var expr = new StructFieldExpression(scope, varDecl, fieldName);
			return expr;
		}

		protected Expression ParseVariableInitialization(Scope scope, ref VarType type)
		{
			var next = FetchToken();

			Expression initExpr;
			if (next.kind == TokenKind.Operator)
			{
				if (next.value == Lexer.AssignmentOperator)
				{
					initExpr = ExpectExpression(scope);

					if (type == null)
					{
						type = initExpr.ResultType;
					}
				}
				else
				{
					throw new CompilerException($"Expected {Lexer.AssignmentOperator} in variable initialization");
				}
			}
			else
			if (type == null)
			{
				throw new CompilerException($"Type for variable must be specified!");
			}
			else
			{
				initExpr = null;
				Rewind();
			}

			return initExpr;
		}

		protected virtual OperatorKind ParseOperator(string opStr)
		{
			switch (opStr)
			{
				case "<":
					return OperatorKind.Less;

				case "<=":
					return OperatorKind.LessOrEqual;

				case ">":
					return OperatorKind.Greater;

				case ">=":
					return OperatorKind.GreaterOrEqual;

				case "==":
					return OperatorKind.Equal;

				case "!=":
					return OperatorKind.Different;

				case "/":
					return OperatorKind.Division;

				case "%":
					return OperatorKind.Modulus;

				case "*":
					return OperatorKind.Multiplication;

				case "+":
					return OperatorKind.Addition;

				case "-":
					return OperatorKind.Subtraction;

				case "^":
					return OperatorKind.Power;

				case "<<":
					return OperatorKind.ShiftLeft;

				case ">>":
					return OperatorKind.ShiftRight;

				case "||":
					return OperatorKind.Or;

				case "&&":
					return OperatorKind.And;

				case "xor":
					return OperatorKind.Xor;

				default:
					return OperatorKind.Unknown;
			}
		}

		#region STRUCTS
		protected Dictionary<string, StructDeclaration> _structs = new Dictionary<string, StructDeclaration>();
		protected void CreateStruct(string structName, IEnumerable<StructField> fields)
		{
			var structType = (StructVarType)VarType.Find(VarKind.Struct, structName);
			structType.decl = new StructDeclaration(structName, fields);

			_structs[structName] = structType.decl;
		}

		#endregion

		#region ENUMS
		protected Dictionary<string, EnumDeclaration> _enums = new Dictionary<string, EnumDeclaration>();

		protected void CreateEnum<T>(string name)
		{
			CreateEnum(name, typeof(T));
		}

		protected void CreateEnum(string enumName, Type enumType)
		{
			var tokenFlagsNames = Enum.GetNames(enumType).Cast<string>().ToArray();
			var tokenFlagsEntries = new List<EnumEntry>();
			foreach (var name in tokenFlagsNames)
			{
				var temp = Enum.Parse(enumType, name);
				var value = Convert.ToUInt32(temp);
				tokenFlagsEntries.Add(new EnumEntry(name, value));
			}
			var tokenFlagsDecl = new EnumDeclaration(enumName, tokenFlagsEntries);
			_enums[tokenFlagsDecl.Name] = tokenFlagsDecl;
		}

		#endregion

		#region MACROS
		private readonly Dictionary<string, Macro> _macros = new Dictionary<string, Macro>();

		public void RegisterMacro(string macroName, object value, VarKind macroKind)
		{
			RegisterMacro(macroName, value, VarType.Find(macroKind));
		}

		public void RegisterMacro(string macroName, object value, VarType macroType)
		{
			macroName = macroName.ToUpper();

			if (macroName.Contains(' '))
			{
				throw new CompilerException("Invalid macro name: " + macroName);
			}

			_macros[macroName] = new Macro(value.ToString(), macroType);
		}

		public Macro ResolveMacro(string macroName)
		{
			if (_macros.ContainsKey(macroName))
			{
				return _macros[macroName];
			}

			return null;
		}
		#endregion
	}

	public class Macro
	{
		private readonly string value;
		public readonly VarType type;

		public Macro(string value, VarType type)
		{
			this.value = value;
			this.type = type;
		}

		public string GetValue()
		{
			if (type.Kind == VarKind.String)
			{
				return $"\"{value}\"";
			}
			else
			{
				return value;
			}
		}
	}
}
