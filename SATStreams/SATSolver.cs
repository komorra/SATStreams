using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SATStreams
{
    public class SATSolver
    {
        private CNF cnf;
        private LUT lut;
        private Clause init;

        /// <summary>
        /// Whether to use file checkpoints to save the state of the solver.
        /// </summary>
        public bool UseCheckpoints { get; set; } = true;
        public int SlowSolverThreadCount { get; set; } = 1;
        public int FastSolverThreadCount { get; set; } = 8;
        public int SlowSolverTimeOut { get; set; } = 60000; // in milliseconds
        public int FastSolverTimeOut { get; set; } = 10000; // in milliseconds
        public string ProblemName { get; set; } = "SATSolver";

        public event Action<string> LogMessage;

        public int LowestStreamSize { get; private set; }
        public int HighestStreamSize { get; private set; }
        public int StreamCount { get; private set; }

        public SATSolver(CNF cnf, string problemName = null)
        {
            var sw = Stopwatch.StartNew();

            this.cnf = cnf;
            this.lut = Utils.CreateLUT(cnf);
            this.ProblemName = problemName ?? "SATSolver";

            var units = cnf.Where(x => x.Count == 1).SelectMany(x => x).ToHashSet();
            init = Utils.OutCome(new Clause(), units, lut);
        }

        /// <summary>
        /// Returns solution if found, otherwise returns null if UNSAT.
        /// </summary>        
        public Clause Solve()
        {
            var rootStream = new SATStream();
            rootStream.Clause = new Clause(init);


            return null;
        }

        public void SolverThread()
        {

        }
    }
}
