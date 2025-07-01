using clipr;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SATStreamsSolver
{
    internal class Options
    {
        [PositionalArgument(0, Description = "Input .cnf problem file in DIMACS format")]
        public string InputFile { get; set; } = string.Empty;
    }
}
