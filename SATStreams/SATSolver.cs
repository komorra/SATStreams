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
        public double BranchingChance { get; set; } = 0.002;

        public int CombinedVarCount { get; set; } = 6;

        public event Action<string> LogMessage;

        public int LowestStreamSize => solvingStreams.Count > 0 ? solvingStreams.Min(x => x.Clause.Count) : 0;
        public int HighestStreamSize => solvingStreams.Count > 0 ? solvingStreams.Max(x => x.Clause.Count) : 0;
        public int StreamCount => solvingStreams.Count;
        public int SolvedVars => init.Count;
        public int TotalVars => variables.Count;
        public int Deletions { get; private set; }
        public int Additions { get; private set; }
        public double Progress => (double)SolvedVars / TotalVars;

        private string progressString => $"[{Progress:P2}|({SolvedVars}/{TotalVars})]  ";



        private List<SATStream> solvingStreams = new List<SATStream>();
        private Clause variables;
        private Random random = new Random(42);
        private Stopwatch watch;

        public SATSolver(CNF cnf, string problemName = null)
        {
            watch = new Stopwatch();

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
            watch.Start();
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

            var fastSolver = new Z3Solver(cnf, 10);

            while (init.Count < variables.Count)
            {
                foreach(var stream in solvingStreams.ToList())
                {
                    if (stream.IsMarkedForDeletion)
                    {
                        DeleteStream(stream);
                        continue;
                    }

                    if(stream.Clause.Count == variables.Count)
                    {
                        if(stream.Clause.Any(x=> stream.Clause.Contains(-x)))
                        {
                            DeleteStream(stream);
                            continue;
                        }
                        init = stream.Clause;
                        break;
                    }

                    var rv = variables.Skip(random.Next(variables.Count))
                        .Where(x => !stream.Clause.Contains(x) && !stream.Clause.Contains(-x))
                        .Take(CombinedVarCount)
                        .ToHashSet();

                    var prop = Utils.RangedPropagation(stream.Clause, rv, lut);
                    if(prop == null)
                    {
                        DeleteStream(stream);
                        continue;
                    }

                    if(prop.Count > stream.Clause.Count)
                    {
                        Utils.FastBCP(stream.Clause, prop, lut);
                        if (stream.Clause.Any(x => stream.Clause.Contains(-x)) || fastSolver.Solve(stream.Clause, out _) == false)
                        {
                            DeleteStream(stream);
                            continue;
                        }

                        LogMessage?.Invoke($"{progressString}{watch.Elapsed}: Propagated stream id:{stream.Id} to {stream.Clause.Count} vars. Total streams: {solvingStreams.Count}");
                    }

                    if(!stream.IsMarkedForDeletion && random.NextDouble() < BranchingChance)
                    {
                        var branch = new SATStream(stream);
                        var branchVar = variables.FirstOrDefault(x=> !branch.Clause.Contains(x) && !branch.Clause.Contains(-x));
                        Utils.FastBCP(branch.Clause, new Clause { branchVar }, lut);
                        solvingStreams.Add(branch);

                        Utils.FastBCP(stream.Clause, new Clause { -branchVar }, lut);
                        Additions++;

                        LogMessage?.Invoke($"{progressString}{watch.Elapsed}: Branching on {branchVar} with {branch.Clause.Count} vars. Total streams: {solvingStreams.Count}");
                    }
                }
            }
            watch.Stop();

            LogMessage?.Invoke($"Solved in {watch.Elapsed}");

            return init;
        }

        private void DeleteStream(SATStream stream)
        {
            solvingStreams.Remove(stream);
            Deletions++;
            LogMessage?.Invoke($"{progressString}{watch.Elapsed}: Deleted stream id:{stream.Id} with {stream.Clause.Count} vars.");
            init = solvingStreams.Select(x => x.Clause).Aggregate((x, y) => x.Intersect(y).ToHashSet());
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
