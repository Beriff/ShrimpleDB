using ShrimpleDB;

namespace ShrimpleShell
{
	internal class Program
	{
		static void Main(string[] args)
		{
			Console.Write("ShrimpleShell >> ");
			var buffer = Console.ReadLine();
			var processed = "ShrimpleShell >> ";

			bool highlight = false;
			for (int i = 0; i < buffer.Length; i++)
			{
				if (buffer[i] == '$') { highlight = true; }
				else if (buffer[i] == '(') { highlight = false; }

				processed += (highlight ? "\e[36;1m" : "\e[37m") + buffer[i] + "\e[0m";
			}
			Console.Clear();
			Console.WriteLine(processed + '\n');

			var interpreter = new Interpreter();
			var lexemes = interpreter.LexicalAnalysis(buffer);

			ASTNode tree = new();
			try
			{
				tree = Interpreter.Parse(lexemes)[0];
			}
			catch (SDBException e)
			{
				Console.WriteLine($"{e.Message}");
				return;
			}
			


			tree.Traverse((n, f) => 
			{
				Console.WriteLine($"{string.Concat(Enumerable.Repeat("  ", f))}[{n.Type}: {n.Value}]"); 
			}, 
			0);
		}
	}
}
