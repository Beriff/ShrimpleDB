using ShrimpleDB;

namespace ShrimpleShell
{
	internal class Program
	{
		static void Main(string[] args)
		{
			const string prefix_in = "ShrimpShell|> ";
			const string prefix_out = "ShrimpShell<| ";

			Console.Write(prefix_in);
			ConsoleKeyInfo input;
			var buffer = "";
			var processed = prefix_in;

			while ( (input = Console.ReadKey(true)).Key != ConsoleKey.Enter )
			{
				Console.SetCursorPosition(0,0);

				char a = input.KeyChar;
				if (input.Modifiers == ConsoleModifiers.Shift)
					a = a.ToString().ToUpper()[0];

				buffer += a;

				string highlight = "";
				bool str_highlight = false;
				for (int i = 0; i < buffer.Length; i++)
				{
					if (buffer[i] == '"') { str_highlight = !str_highlight; }
					else
					{
						if (buffer[i] == '$') { highlight = "\e[36;1m"; }
						else if (buffer[i] == '(') { highlight = ""; }
					}

					if (buffer[i] == '"') { processed += "\e[32m\"\e[0m"; } else
					{
						processed += (str_highlight ? "\e[32m" : highlight) + buffer[i] + "\e[0m";
					}
				}

				Console.Write(processed);
				processed = prefix_in;
			}

			Console.Write('\n');

			var interpreter = new Interpreter();
			var lexemes = interpreter.LexicalAnalysis(buffer);

			ASTNode tree;
			try
			{
				tree = Interpreter.Parse(lexemes)[0];
				Interpreter.StaticAnalysis([tree]);
				ASTNode? final = Interpreter.Evaluate(tree);
				if(final == null)
				{
					Console.WriteLine(prefix_out + "(returned nothing)");
				} else
				{
					Console.WriteLine(prefix_out + final.Value);
				}
				


				/*tree.Traverse((n, f) => 
				{
					Console.WriteLine($"{string.Concat(Enumerable.Repeat("  ", f))}[{n.Type}: {n.Value}]"); 
				},
				0);*/



			}
			catch (SDBException e)
			{
				Console.WriteLine($"{e.Message}");
				return;
			}
		}
	}
}
