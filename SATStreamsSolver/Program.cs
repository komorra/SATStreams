using clipr;
using SATStreams;

namespace SATStreamsSolver
{
    internal class Program
    {
        static void Main(string[] args)
        {
            var options = CliParser.Parse<Options>(args);

            var cnf = Utils.FromFile(options.InputFile);

            var solver = new SATSolver(cnf);
        }
    }
}
