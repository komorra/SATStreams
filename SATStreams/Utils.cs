global using Clause = System.Collections.Generic.HashSet<int>;
global using CNF = System.Collections.Generic.HashSet<System.Collections.Generic.HashSet<int>>;
global using LUT = System.Collections.Generic.Dictionary<int, System.Collections.Generic.HashSet<System.Collections.Generic.HashSet<int>>>;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SATStreams
{
    public static class Utils
    {
        static Random random = new Random(42);
        public static LUT CreateLUT(CNF cnf)
        {
            var lut = new LUT();
            foreach (var cl in cnf)
            {
                foreach (var lt in cl)
                {                    
                    if (!lut.ContainsKey(lt))
                    {
                        lut.Add(lt, new CNF());
                    }
                    lut[lt].Add(cl);
                }
            }

            return lut;
        }

        public static CNF FromFile(string filePath)
        {
            var cnf = new CNF();
            var lines = System.IO.File.ReadAllLines(filePath);
            foreach (var line in lines)
            {
                if (line.StartsWith("p cnf"))
                {
                    continue; // Skip problem line
                }
                if (line.StartsWith("c"))
                {
                    continue; // Skip comment line
                }
                var clause = new Clause();
                var literals = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var lit in literals)
                {
                    if (lit == "0") break; // End of clause
                    clause.Add(int.Parse(lit));
                }
                cnf.Add(clause);
            }
            return cnf;
        }

        public static void ToFile(CNF cnf, string filePath)
        {
            var sb = new StringBuilder();
            sb.AppendLine("p cnf " + cnf.SelectMany(x => x).Distinct().Count() + " " + cnf.Count);
            foreach (var cl in cnf)
            {
                sb.AppendLine(string.Join(" ", cl) + " 0");
            }
            System.IO.File.WriteAllText(filePath, sb.ToString());
        }

        public static void FastBCP(Clause init, Clause added, Dictionary<int, CNF> lut)
        {
            var stack = new Stack<int>(added);
            
            while (stack.Count > 0)
            {
                var p = stack.Pop();
                init.Add(p);

                if (lut.ContainsKey(-p))
                {
                    foreach (var cl in lut[-p])
                    {
                        if (cl.Any(x => init.Contains(x)))
                        {
                            continue;
                        }
                        var t = cl.Where(x => !init.Contains(-x)).ToHashSet();
                        if (t.Count == 1)
                        {
                            if (init.Contains(t.Single()))
                            {
                                continue;
                            }
                            
                            stack.Push(t.Single());
                        }
                    }
                }
            }
        }

        internal static Clause OutCome(Clause init, Clause added, Dictionary<int, CNF> lut)
        {
            var outcome = new Clause(init);
            FastBCP(outcome, added, lut);

            return outcome;
        }

        internal static IEnumerable<Clause> GetCombinations(IEnumerable<int> rv)
        {
            var r = new List<Clause>();
            var n = rv.Count();
            var max = 1L << n;
            for (int i = 0; i < max; i++)
            {
                var c = new Clause();
                for (int j = 0; j < n; j++)
                {
                    if ((i & (1 << j)) > 0)
                    {
                        c.Add(rv.ElementAt(j));
                    }
                    else
                    {
                        c.Add(-rv.ElementAt(j));
                    }
                }
                yield return c;
            }
        }

        internal static Clause RangedPropagation(Clause init, Clause rv, Dictionary<int, CNF> lut)
        {
            Clause ok = null;
            foreach (var comb in GetCombinations(rv).OrderBy(x => random.Next()))
            {
                var oc = OutCome(init, comb, lut);
                if (oc.Any(x => oc.Contains(-x)))
                {
                    continue;
                }
                
                ok = ok ?? oc;
                ok.IntersectWith(oc);

                if (ok.Count <= init.Count)
                {
                    break;
                }
            }

            return ok;
        }

        public static Clause GetVariables(CNF cnf)
        {
            var variables = new Clause();
            foreach (var cl in cnf)
            {
                foreach (var lit in cl)
                {
                    variables.Add(Math.Abs(lit));
                }
            }
            return variables;
        }

        public static string GetCNFHash(CNF cnf)
        {
            var sb = new StringBuilder();
            foreach (var cl in cnf.OrderBy(x => x.Max(o=>o)))
            {
                foreach (var lit in cl.OrderBy(x => x))
                {
                    sb.Append(lit).Append(" ");
                }
                sb.Append("0\n");
            }
            var bytes = Encoding.UTF8.GetBytes(sb.ToString());
            using (var sha256 = System.Security.Cryptography.SHA256.Create())
            {
                var hashBytes = sha256.ComputeHash(bytes);
                return BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
            }
        }

        public static void SaveCheckPoint(string name, List<SATStream> streams)
        {
            name = $"checkpoints/{name}";
            var init = streams.Select(x => x.Clause).Aggregate((x, y) => x.Intersect(y).ToHashSet());
            if(!Directory.Exists(name))
            {
                Directory.CreateDirectory(name);
            }
            foreach (var stream in streams)
            { 
                var cnf = stream.Clause.Select(x => new Clause { x }).ToHashSet();
                var filePath = Path.Combine(name, $"{stream.Id}.cnf");
                ToFile(cnf, filePath);
            }
            var solutionFilePath = Path.Combine(name, "solution.cnf");
            var solutionCNF = init.Select(x => new Clause { x }).ToHashSet();
            ToFile(solutionCNF, solutionFilePath);

            var cnfFiles = Directory.GetFiles(name, "*.cnf");
            foreach(var file in cnfFiles)
            {
                var fileName = Path.GetFileName(file);
                if (fileName == "solution.cnf" || fileName == "activeids.txt")
                {
                    continue;
                }
                var id = int.Parse(Path.GetFileNameWithoutExtension(fileName));
                if (!streams.Any(x => x.Id == id))
                {
                    File.Delete(file);
                }
            }

            File.WriteAllLines(Path.Combine(name, "activeids.txt"),
                streams.Where(x => !x.IsMarkedForDeletion).Select(x => x.Id.ToString()).ToArray());
        }

        public static List<SATStream> LoadCheckPoint(string name)
        {
            name = $"checkpoints/{name}";
            var activeIdsFile = Path.Combine(name, "activeids.txt");
            var activeIds = new HashSet<int>();
            if (File.Exists(activeIdsFile))
            {
                var lines = File.ReadAllLines(activeIdsFile);
                foreach (var line in lines)
                {
                    if (int.TryParse(line, out int id))
                    {
                        activeIds.Add(id);
                    }
                }
            }
            var streams = new List<SATStream>();
            if (!Directory.Exists(name))
            {
                return null;
            }
            foreach (var activeId in activeIds)
            {
                var cnf = FromFile(Path.Combine(name,activeId+".cnf"));
                if (cnf.Count == 0)
                {
                    continue;
                }
                var stream = new SATStream();
                stream.Clause = cnf.Where(x=>x.Count == 1).SelectMany(x => x).ToHashSet();
                stream.SetID(activeId);
                streams.Add(stream);
            }
            return streams;
        }
    }
}
