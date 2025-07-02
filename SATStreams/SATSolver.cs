using Newtonsoft.Json;
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

        [JsonIgnore]
        public bool HostVisualisationServer { get; set; } = true;

        /// <summary>
        /// Whether to use file checkpoints to save the state of the solver.
        /// </summary>
        [JsonIgnore]
        public bool UseCheckpoints { get; set; } = true;

        [JsonIgnore]
        public int SlowSolverThreadCount { get; set; } = 1;

        [JsonIgnore]
        public int FastSolverThreadCount { get; set; } = 4;

        [JsonIgnore]
        public int SlowSolverTimeOut { get; set; } = 60000; // in milliseconds

        [JsonIgnore]
        public int FastSolverTimeOut { get; set; } = 10000; // in milliseconds

        [JsonIgnore]
        public string ProblemName { get; set; } = "SATSolver";

        [JsonIgnore]
        public double BranchingChance { get; set; } = 0.01;

        [JsonIgnore]
        public int CombinedVarCount { get; set; } = 6;

        public event Action<string> LogMessage;

        public int LowestStreamSize => solvingStreams.Count > 0 ? solvingStreams.Min(x => x.Clause.Count) : 0;
        public int HighestStreamSize => solvingStreams.Count > 0 ? solvingStreams.Max(x => x.Clause.Count) : 0;
        public int StreamCount => solvingStreams.Count;
        public int SolvedVars => init.Count;
        public int TotalVars => variables.Count;
        public int Deletions { get; private set; }
        public int Additions { get; private set; }

        [PerInterval(60, nameof(Deletions))]
        public double DeletionsPerMinute { get; private set; }

        [PerInterval(60, nameof(Additions))]
        public double AdditionsPerMinute { get; private set; }

        [PerInterval(60, nameof(SolvedVars))]
        public double SolvedVarsPerMinute { get; private set; }

        public double Progress => ((double)SolvedVars / TotalVars) + ((double)LowestStreamSize / TotalVars) / 2.0;
        private string progressString => $"[{Progress:P4}|({SolvedVars}/{TotalVars})]  ";



        private List<SATStream> solvingStreams = new List<SATStream>();
        private Clause variables;
        private Random random = new Random(42);
        private Stopwatch watch;
        private DateTime lastCheckpointTime = DateTime.UtcNow;

        public SATSolver(CNF cnf, string problemName = null)
        {
            watch = new Stopwatch();

            this.cnf = cnf;
            this.lut = Utils.CreateLUT(cnf);
            this.ProblemName = problemName ?? "SATSolver";
            this.variables = Utils.GetVariables(cnf).OrderByDescending(x=>x).ToHashSet();

            var units = cnf.Where(x => x.Count == 1).SelectMany(x => x).ToHashSet();
            init = Utils.OutCome(new Clause(), units, lut);
        }

        /// <summary>
        /// Returns solution if found, otherwise returns null if UNSAT.
        /// </summary>        
        public Clause Solve()
        {
            if(HostVisualisationServer)
            {
                StartVisualizationServer();
            }

            watch.Start();
            var rootStream = new SATStream();
            rootStream.Clause = new Clause(init);
            solvingStreams.Add(rootStream);

            var hash = Utils.GetCNFHash(cnf);
            if(UseCheckpoints)
            {
                var streams = Utils.LoadCheckPoint(hash);
                if(streams != null && streams.Count > 0)
                {
                    solvingStreams = streams;
                    init = solvingStreams.Select(x => x.Clause).Aggregate((x, y) => x.Intersect(y).ToHashSet());
                    LogMessage?.Invoke($"Loaded checkpoint with {solvingStreams.Count} streams.");                    
                }
            }

            for (int i = 0; i < FastSolverThreadCount; i++)
            {
                var seed = random.Next();
                var thread = new Thread(() => SolverThread(seed, FastSolverTimeOut));
                thread.IsBackground = true;
                thread.Start();
            }
            for (int i = 0; i < SlowSolverThreadCount; i++)
            {
                var seed = random.Next();
                var thread = new Thread(() => SolverThread(seed, SlowSolverTimeOut));
                thread.IsBackground = true;
                thread.Start();
            }

            var fastSolver = new Z3Solver(cnf, 10);            
            while (init.Count < variables.Count)
            {
                TryMerge();

                Clause lastGoodRV = null;

                foreach(var stream in solvingStreams.OrderBy(_=>random.Next()).ToList())
                {
                    var toDelete = solvingStreams.FirstOrDefault(x => x.IsMarkedForDeletion);
                    if (toDelete != null)
                    {
                        DeleteStream(toDelete);
                        if (toDelete == stream)
                        {
                            continue;
                        }
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

                    for (int i = 0; i < 2; i++)
                    {
                        var rv = (i == 0 || lastGoodRV == null) ? variables.Skip(random.Next(variables.Count))
                                    .Where(x => !stream.Clause.Contains(x) && !stream.Clause.Contains(-x))
                                    .Take(CombinedVarCount)
                                    .ToHashSet() : lastGoodRV;

                        var prop = Utils.RangedPropagation(stream.Clause, rv, lut);
                        if (prop == null)
                        {
                            lastGoodRV = rv;

                            DeleteStream(stream);
                            break;
                        }

                        if (prop.Count > stream.Clause.Count)
                        {
                            lastGoodRV = rv;

                            Utils.FastBCP(stream.Clause, prop, lut);
                            if (stream.Clause.Any(x => stream.Clause.Contains(-x)) || fastSolver.Solve(stream.Clause, out _) == false)
                            {
                                DeleteStream(stream);
                                break;
                            }

                            LogMessage?.Invoke($"{progressString}{watch.Elapsed}: Propagated stream id:{stream.Id} to {stream.Clause.Count} vars. Total streams: {solvingStreams.Count}");
                        } 
                    }

                    if (random.NextDouble() < BranchingChance)
                    {
                        var lowestStream = solvingStreams.OrderBy(x => x.Clause.Count).FirstOrDefault(x => !x.IsMarkedForDeletion);
                        if (lowestStream != null)
                        {
                            var branch = new SATStream(lowestStream);
                            var branchVar = variables.FirstOrDefault(x => !branch.Clause.Contains(x) && !branch.Clause.Contains(-x));
                            Utils.FastBCP(branch.Clause, new Clause { branchVar }, lut);
                            solvingStreams.Add(branch);

                            Utils.FastBCP(lowestStream.Clause, new Clause { -branchVar }, lut);
                            Additions++;

                            LogMessage?.Invoke($"{progressString}{watch.Elapsed}: Branching branch of id:{lowestStream.Id} on {branchVar} with {branch.Clause.Count} vars. Total streams: {solvingStreams.Count}");
                        }
                    }

                    UpdateTemporalProperties();
                }

                

                if(DateTime.UtcNow - lastCheckpointTime > TimeSpan.FromMinutes(5) && UseCheckpoints)
                {
                    Utils.SaveCheckPoint(hash, solvingStreams);
                    lastCheckpointTime = DateTime.UtcNow;
                    LogMessage?.Invoke($"Saved checkpoint with {solvingStreams.Count} streams.");
                }
            }
            watch.Stop();
            solvingStreams.Clear();
            solvingStreams.Add(new SATStream { Clause = new Clause(init) });
            if (UseCheckpoints)
            {
                Utils.SaveCheckPoint(hash, solvingStreams);
                LogMessage?.Invoke($"Saved final checkpoint with {solvingStreams.Count} streams.");
            }

            LogMessage?.Invoke($"Solved in {watch.Elapsed}");

            return init;
        }

        private void StartVisualizationServer()
        {
            var port = 8888;
            var server = new VisualizationServer(this, port);
            _ = server.Start();
        }

        private Dictionary<string, (DateTime last, double prevValue)> temporalPropertiesValues = new Dictionary<string, (DateTime last, double prevValue)>();
        private void UpdateTemporalProperties()
        {
            var temporalProperties = GetType().GetProperties()
                .Where(p => p.GetCustomAttributes(typeof(PerIntervalAttribute), false).Any())
                .ToList();

            foreach(var prop in temporalProperties)
            {
                var attribute = prop.GetCustomAttributes(typeof(PerIntervalAttribute), false).FirstOrDefault() as PerIntervalAttribute;
                if (attribute == null)
                {
                    continue;
                }

                var value = Convert.ToDouble(GetType().GetProperty(attribute.PropertyName).GetValue(this));

                if (!temporalPropertiesValues.ContainsKey(prop.Name))
                {
                    temporalPropertiesValues[prop.Name] = (DateTime.UtcNow, value);
                }

                var (last, prevValue) = temporalPropertiesValues[prop.Name];
                if (DateTime.UtcNow - last >= attribute.Interval)
                {
                    var delta = value - prevValue;
                    var perMinute = delta / (DateTime.UtcNow - last).TotalMinutes;
                    prop.SetValue(this, perMinute);
                    temporalPropertiesValues[prop.Name] = (DateTime.UtcNow, value);                    
                }
            }   
        }

        private void TryMerge()
        {
            var removed = new HashSet<SATStream>();
            var processed = new HashSet<(SATStream, SATStream)>();
            var avgStreamSize = solvingStreams.Count > 0 ? solvingStreams.Average(x => x.Clause.Count) : 0;
            foreach (var streamA in solvingStreams.ToList())
            {
                foreach (var streamB in solvingStreams.ToList())
                {
                    if (streamA == streamB || streamA.IsMarkedForDeletion || streamB.IsMarkedForDeletion || removed.Contains(streamA) || removed.Contains(streamB))
                    {
                        continue;
                    }
                    if (processed.Contains((streamA, streamB)) || processed.Contains((streamB, streamA)))
                    {
                        continue;
                    }
                    processed.Add((streamA, streamB));
                    if(streamA.Clause.Count <= avgStreamSize || streamB.Clause.Count <= avgStreamSize)
                    {
                        continue;
                    }

                    var merged = streamA.Clause.Intersect(streamB.Clause).ToHashSet();
                    if (merged.Count <= avgStreamSize)
                    {
                        continue;
                    }

                    var mergedStream = new SATStream { Clause = merged };
                    solvingStreams.Add(mergedStream);
                    solvingStreams.Remove(streamA);
                    solvingStreams.Remove(streamB);
                    removed.Add(streamA);
                    removed.Add(streamB);

                    LogMessage?.Invoke($"{progressString}{watch.Elapsed}: Merged streams {streamA.Id} and {streamB.Id} into new stream id:{mergedStream.Id} with {merged.Count} vars. Total streams: {solvingStreams.Count}");
                }
            }
        }

        private void DeleteStream(SATStream stream)
        {
            stream.IsMarkedForDeletion = true;
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
