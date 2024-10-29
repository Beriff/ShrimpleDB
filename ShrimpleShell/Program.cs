using ShrimpleDB;
using ConFlag;

namespace ShrimpleShell
{
	internal class Program
	{
		static string prefix_in = "ebi |< ";
		static string prefix_out = "ebi >| ";

		static bool EmptyReturns = true;

		static void Prompt(Interpreter interpreter)
		{

			Console.Write(prefix_in);

			var lexemes = interpreter.LexicalAnalysis(Console.ReadLine());

			ASTNode tree;
			try
			{
				tree = Interpreter.Parse(lexemes)[0];
				Interpreter.StaticAnalysis([tree]);
				ASTNode? final = interpreter.Evaluate(tree);
				if (final == null && EmptyReturns)
				{
					Console.WriteLine(prefix_out + "(returned nothing)");
				}
				else if (final != null)
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
		static void Main(string[] args)
		{
			//processing the cmd flags
			var arguments = new Arguments(args);

			if(arguments.Options.ContainsKey("no-prefix"))
				prefix_in = prefix_out = "";

			if (arguments.Options.ContainsKey("no-empty-return"))
				EmptyReturns = false;

			var interpreter = new Interpreter();

			while(!interpreter.ShutdownSignal)
			{
				Prompt(interpreter);
			}
			
		}
	}
}
