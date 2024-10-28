using System;

namespace ShrimpleDB
{
	public struct ASTNode
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

		public readonly void Traverse(Action<ASTNode, int> f, int foldedness)
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
		Comma
	}

	public class Interpreter
	{
		private const string se_str = "\e[41m SYNTAX ERROR \e[0m ";
		public List<string> LexicalAnalysis(string instruction)
		{
			string buffer = "";
			List<string> lexemes = [];

			for (int index = 0; index < instruction.Length; index++)
			{
				char current_character = instruction[index];

				switch (current_character)
				{
					case ' ':
						if (buffer != "")
						{
							lexemes.Add(buffer);
							buffer = "";
						}
						break;

					case '(' or ')' or ',':
						if (buffer != "")
						{
							lexemes.Add(buffer);
							buffer = "";
						}
						lexemes.Add(current_character.ToString());
						break;

					case '\n':
						if (buffer != "")
						{
							lexemes.Add(buffer);
							buffer = "";
						}
						break;

					default:
						buffer += current_character;
						break;
				}
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
						int parity = 1;
						List<string> sublexemes = [];
						for (int j = 2; j < lexemes.Count; j++)
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

						List<ASTNode> parameter_nodes = Parse(sublexemes, ParserHint.ParameterList);
						function.Children = parameter_nodes;
					}
					return [function];

				}

				// PARSING IDENTIFIER
				else if (GetTokenType(lexeme) == ParserToken.Identifier)
				{
					return [new ASTNode(ParserToken.Identifier, lexeme)];
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

					if (GetTokenType(lexeme) == ParserToken.Function)
					{
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

					}

					else if (GetTokenType(lexeme) == ParserToken.Identifier)
					{
						parameters.Add(new(ParserToken.Identifier, lexeme));
					}

					else if (GetTokenType(lexeme) == ParserToken.Comma)
					{
						continue;
					}

					else
					{
						throw new SDBException(se_str + "Unexpected '(' or ')' ");
					}
				}

				return parameters;
			}
		}

	}
}
