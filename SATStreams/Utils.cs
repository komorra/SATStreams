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
    }
}
