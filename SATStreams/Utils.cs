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
    }
}
