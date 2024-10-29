
using System;

namespace ShrimpleDB
{
	public class ASTNode
	{
		public ParserToken Type;
		public string Value;
		public List<ASTNode> Children;

		public ASTNode(ParserToken type, string value)
		{
			Type = type;
			Value = value;
			Children = [];
		}

		public void Traverse(Action<ASTNode, int> f, int foldedness)
		{
			f(this, foldedness);
			foreach (ASTNode node in Children) { node.Traverse(f, foldedness + 1); }
		}

	}

	public enum ParserHint
	{
		ParameterList,
		Neutral
	}

	public enum ParserToken
	{
		Function,
		ParenthesisOpen,
		ParenthesisClose,
		Identifier,
		Comma,
		Number,
		Str,
		Any,
		None
	}

	public static class QLStd
	{
		public class FunctionInfo(Action action, ParserToken[] arguments, ParserToken? ret)
		{
			public Action Action = action;
			public ParserToken[] FnArgs = arguments;
			public ParserToken? ReturnType = ret;
		}

		public static readonly List<ASTNode> Arguments = [];
		public static ASTNode? Return;
		public static Interpreter BoundInterpreter;

		public static readonly Dictionary<string, FunctionInfo> FuncDispatchTable = new()
		{
			{
				"PRINT", new(() =>
				{
					Console.WriteLine(Arguments[0].Value);
				}, [ParserToken.Any], null)
			},
			{
				"CONCAT", new(() =>
				{
					Return = new(ParserToken.Str, Arguments[0].Value + Arguments[1].Value);
				}, [ParserToken.Str, ParserToken.Str], ParserToken.Str)
			},
			{
				"END", new(() => BoundInterpreter.ShutdownSignal = true, [], null)
			},
			{
				"VAR", new(() =>
				{
					BoundInterpreter.Variables[Arguments[0].Value] = Arguments[1];
				}, [ParserToken.Str, ParserToken.Any], null)
			},
			{
				"ADD", new(() =>
				{
					Return = new(ParserToken.Number, 
						(float.Parse(Arguments[0].Value) + 
						float.Parse(Arguments[1].Value)).ToString());
				}, [ParserToken.Number, ParserToken.Number], ParserToken.Number)
			}
		};
	}

	public class Interpreter
	{
		private const string se_str = "\e[41mSYNTAX ERROR\e[0m ";
		private const string ste_str = "\e[41mSTATIC ERROR\e[0m ";
		private const string re_str = "\e[41mRUNTIME ERROR\e[0m ";
		public Dictionary<string, ASTNode> Variables = [];
		public bool ShutdownSignal = false;

		public Interpreter() { QLStd.BoundInterpreter = this; }

		public List<string> LexicalAnalysis(string instruction)
		{
			bool string_flag = false;

			string buffer = "";
			List<string> lexemes = [];



			for (int index = 0; index < instruction.Length; index++)
			{
				char current_character = instruction[index];

				// I refuse to be controlled by the big "structured control flow"
				// happy goto to everyone
				#pragma warning disable S907

				switch (current_character)
				{
					case ' ':
						if (buffer != "" && !string_flag)
						{
							lexemes.Add(buffer);
							buffer = "";
						}
						else if (string_flag) goto default;
						break;

					case '(' or ')' or ',':
						if (buffer != "" && !string_flag)
						{
							lexemes.Add(buffer);
							buffer = "";
						}
						else if (string_flag) goto default;
						lexemes.Add(current_character.ToString());
						break;

					case '\n':
						if (buffer != "" && !string_flag)
						{
							lexemes.Add(buffer);
							buffer = "";
						}
						else if (string_flag) goto default;
						break;

					case '"':
						string_flag = !string_flag;
						buffer += current_character;
						break;

					default:
						buffer += current_character;
						break;
				}

				#pragma warning restore S907
			}

			return lexemes;
		}

		private static ParserToken GetTokenType(string token)
		{
			if (token.StartsWith('$'))
				return ParserToken.Function;
			else if (token == "(") return ParserToken.ParenthesisOpen;
			else if (token == ")") return ParserToken.ParenthesisClose;
			else if (token == ",") return ParserToken.Comma;
			else if (
				token.StartsWith('"') && token.EndsWith('"')) return ParserToken.Str;
			else if (float.TryParse(token, out _)) return ParserToken.Number;
			else return ParserToken.Identifier;
		}

		public static List<ASTNode> Parse(List<string> lexemes, ParserHint hint = ParserHint.Neutral)
		{
			if (hint == ParserHint.Neutral)
			{
				if (lexemes.Count == 0) { throw new SDBException(se_str + "Parenthesis expected"); }

				var lexeme = lexemes[0];

				// PARSING $FUNCTION
				if (GetTokenType(lexeme) == ParserToken.Function)
				{
					ASTNode function = new(ParserToken.Function, lexeme[1..]);

					if (lexemes.Count == 1)
						throw new SDBException(se_str + "Token expected after " + lexeme);

					else if (GetTokenType(lexemes[1]) == ParserToken.ParenthesisOpen)
					{
						//traverse until the paired ')' is encountered
						int parity = 0;
						List<string> sublexemes = [];
						for (int j = 1; j < lexemes.Count; j++)
						{
							switch (GetTokenType(lexemes[j]))
							{
								case ParserToken.ParenthesisOpen: parity++; break;
								case ParserToken.ParenthesisClose: parity--; break;
								default: break;
							}
							if (parity == 0) break;
							sublexemes.Add(lexemes[j]);
						}

						if (parity != 0)
							throw new SDBException(se_str + "Unmatched parenthesis");

						List<ASTNode> parameter_nodes = Parse(sublexemes[1..], ParserHint.ParameterList);
						function.Children = parameter_nodes;
					}
					return [function];

				}

				// PARSING IDENTIFIER
				else if (GetTokenType(lexeme) == ParserToken.Identifier)
				{
					return [new ASTNode(ParserToken.Identifier, lexeme)];
				}

				else if (GetTokenType(lexeme) == ParserToken.Str)
				{
					return [new ASTNode(ParserToken.Str, lexeme[1..^1])];
				}

				else
				{
					throw new SDBException(se_str + "Unexpected Identifier");
				}
			}

			

			else // if (hint == ParserHint.ParameterList)
			{
				// PARSING PARAMETER LIST
				List<ASTNode> parameters = [];

				for (int index = 0; index < lexemes.Count; index++)
				{
					var lexeme = lexemes[index];

					// a parameter might be in form of $FUNCTION(x,y...)
					// to avoid interpreting those commas as the ones
					// belonging to the current parameter list, count
					// the parenthesis

					switch(GetTokenType(lexeme))
					{
						case ParserToken.Function:
							if (GetTokenType(lexemes[index + 1]) != ParserToken.ParenthesisOpen)
								throw new SDBException(se_str + "Expected parenthesis");

							ASTNode function = new(ParserToken.Function, lexeme[1..]);

							List<string> parameter_lexemes = [];
							int parity = 1;
							int new_index = 0;
							for (int j = index + 2; j < lexemes.Count; j++)
							{
								switch (GetTokenType(lexemes[j]))
								{
									case ParserToken.ParenthesisOpen: parity++; break;
									case ParserToken.ParenthesisClose: parity--; break;
									case ParserToken.Comma: break;
									default: break;
								}

								if (parity == 0)
								{
									new_index = j;
									break;
								}
								parameter_lexemes.Add(lexemes[j]);
							}

							if (parity != 0)
								throw new SDBException(se_str + "Unmatched parenthesis");

							index = new_index + 1;
							var parsed_parameters = Parse(parameter_lexemes, ParserHint.ParameterList);
							function.Children = parsed_parameters;
							parameters.Add(function);

							break;

						case ParserToken.Identifier:
							parameters.Add(new(ParserToken.Identifier, lexeme)); break;
						case ParserToken.Comma: continue;
						case ParserToken.Str:
							parameters.Add(new ASTNode(ParserToken.Str, lexeme[1..^1]));
							break;
						case ParserToken.Number:
							parameters.Add(new ASTNode(ParserToken.Number, lexeme));
							break;
						default:
							throw new SDBException(se_str + "Unexpected '(' or ')' ");
					}

				}

				return parameters;
			}
		}

		public static void StaticAnalysis(List<ASTNode> nodes)
		{
			nodes[0].Traverse((node, _) =>
			{
				if(node.Type ==  ParserToken.Function)
				{
					// - Sorry TDD i don't think you can qualify as a development practice
					// - This is bullshit, you let defensive programming in
					// - DP is an important step in developer's life how about you read a book
					if (!QLStd.FuncDispatchTable.TryGetValue(node.Value, out QLStd.FunctionInfo? f_info))
						throw new SDBException(ste_str + $"Function not found: ${node.Value}");

					var argcount = node.Children.Count;

					if(argcount != f_info.FnArgs.Length)
						throw new SDBException(ste_str + $"${node.Value} argument count mismatch: {argcount} ({f_info.FnArgs.Length} expected)");

					int argindex = 0;
					foreach( var arg in node.Children )
					{
						var expected_arg_type = f_info.FnArgs[argindex];
						if (arg.Type == ParserToken.Function)
						{
							if (!QLStd.FuncDispatchTable.TryGetValue(arg.Value, out QLStd.FunctionInfo? subfn))
								throw new SDBException(ste_str + $"Function not found: ${node.Value}");

							if (subfn.ReturnType != expected_arg_type && (expected_arg_type != ParserToken.Any || subfn.ReturnType == null) )
								throw new SDBException(
									ste_str
									+ $"Invalid ${arg.Value} return type for enclosing argument {argindex + 1}: "  +
									$"{subfn.ReturnType ?? ParserToken.None} ({expected_arg_type} expected) in ${node.Value}");
						}
						else if (expected_arg_type == ParserToken.Any
						|| arg.Type == ParserToken.Identifier) { }
						else if (arg.Type != expected_arg_type)
							throw new SDBException(
									ste_str
									+ $"Invalid type for enclosing argument {argindex + 1}: " +
									$"{arg.Type} ({expected_arg_type} expected) in ${node.Value}");
						++argindex; 
					}
				}
			}, 0);
		}

		public ASTNode? Evaluate(ASTNode node)
		{
			QLStd.Return = null;
			switch (node.Type)
			{
				case ParserToken.Function:
					var f_info = QLStd.FuncDispatchTable[node.Value];
					foreach( var child in node.Children)
					{
						QLStd.Arguments.Add(Evaluate(child)!);
					}
					f_info.Action();
					QLStd.Arguments.Clear();
					return QLStd.Return;
				case ParserToken.Identifier:
					if (Variables.TryGetValue(node.Value, out ASTNode? value))
					{ return value; }
					else
					{ throw new SDBException(re_str + $"Unknown identifier: {node.Value}"); }
				default: return node;
			}

		}
	}
}
