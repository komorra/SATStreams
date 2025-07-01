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
        public int FastSolverThreadCount { get; set; } = 4;
        public int SlowSolverTimeOut { get; set; } = 60000; // in milliseconds
        public int FastSolverTimeOut { get; set; } = 10000; // in milliseconds
        public string ProblemName { get; set; } = "SATSolver";

        public event Action<string> LogMessage;

        public int LowestStreamSize => solvingStreams.Count > 0 ? solvingStreams.Min(x => x.Clause.Count) : 0;
        public int HighestStreamSize => solvingStreams.Count > 0 ? solvingStreams.Max(x => x.Clause.Count) : 0;
        public int StreamCount => solvingStreams.Count;
        public int SolvedVars => init.Count;
        public int TotalVars => variables.Count;


        private List<SATStream> solvingStreams = new List<SATStream>();
        private Clause variables;
        private Random random = new Random(42);

        public SATSolver(CNF cnf, string problemName = null)
        {
            var sw = Stopwatch.StartNew();

            this.cnf = cnf;
            this.lut = Utils.CreateLUT(cnf);
            this.ProblemName = problemName ?? "SATSolver";
            this.variables = Utils.GetVariables(cnf);

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
            solvingStreams.Add(rootStream);

            for (int i = 0; i < FastSolverThreadCount; i++)
            {
                var thread = new Thread(() => SolverThread(random.Next(), FastSolverTimeOut));
                thread.IsBackground = true;
                thread.Start();
            }
            for (int i = 0; i < SlowSolverThreadCount; i++)
            {
                var thread = new Thread(() => SolverThread(random.Next(), SlowSolverTimeOut));
                thread.IsBackground = true;
                thread.Start();
            }


            while (init.Count < variables.Count)
            {

            }

            return init;
        }

        private void SolverThread(int seed, int timeOut)
        {
            var random = new Random(seed);
            var solver = new Z3Solver(cnf, timeOut);

            while(true)
            {
                try
                {
                    var randomStream = solvingStreams                        
                        .OrderBy(x => random.Next())
                        .FirstOrDefault();

                    if(randomStream.IsMarkedForDeletion)
                    {
                        Thread.Sleep(100);
                        continue;
                    }

                    var check = solver.Solve(randomStream.Clause, out var solution);
                    if (check == false)
                    {
                        randomStream.IsMarkedForDeletion = true;
                    }
                    else if(check == true)
                    {
                        init = solution;
                        return;
                    }
                }
                catch { }
            }
        }
    }
}
