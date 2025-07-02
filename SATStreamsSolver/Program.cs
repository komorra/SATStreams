using clipr;
using SATStreams;
using System.Diagnostics;

namespace SATStreamsSolver
{
    internal class Program
    {
        static void Main(string[] args)
        {
            var options = CliParser.Parse<Options>(args);

            var cnf = Utils.FromFile(options.InputFile);  
            var hash = Utils.GetCNFHash(cnf);

            var solver = new SATSolver(cnf, options.InputFile);
            solver.LogMessage += (m) => Solver_LogMessage(hash, m);
            var solution = solver.Solve();
            Solver_LogMessage(hash, $"v {string.Join(" ", solution)}");
        }

        private static void Solver_LogMessage(string name, string message)
        {
            if(message.Contains("deleted", StringComparison.OrdinalIgnoreCase))
            {
                Console.ForegroundColor = ConsoleColor.Green;
            }
            if(message.Contains("merged", StringComparison.OrdinalIgnoreCase))
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
            }
            Console.WriteLine(message);
            Console.ResetColor();

            var dir = Path.Combine("checkpoints", name);
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            var logFile = Path.Combine(dir, $"{name}.log");
            using (var writer = new StreamWriter(logFile, true))
            {
                writer.WriteLine($"{DateTime.Now} - {message}");
            }
        }
    }
}
