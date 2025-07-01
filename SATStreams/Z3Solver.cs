using Microsoft.Z3;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SATStreams
{
    internal class Z3Solver
    {
        private Context context;
        private Solver solver;
        private Dictionary<int, BoolExpr> varsDict = new Dictionary<int, BoolExpr>();

        public Z3Solver(CNF cnf, int timeOut)
        {
            context = new Context();
            solver = context.MkSolver();
            solver.Set("timeout", timeOut);

            var variables = Utils.GetVariables(cnf);
            varsDict = variables.ToDictionary(
                v => Math.Abs(v),
                v => context.MkBoolConst($"var_{Math.Abs(v)}"));

            foreach (var clause in cnf)
            {
                var or = context.MkOr(clause.Select(lit => lit > 0 ?
                    varsDict[Math.Abs(lit)] :
                    context.MkNot(varsDict[Math.Abs(lit)])));

                solver.Assert(or);
            }
        }

        public bool? Solve(Clause assumption, out Clause solution)
        {
            solution = null;
            solver.Push();

            foreach(var lt in assumption)
            {
                if (lt > 0)
                {
                    solver.Assert(varsDict[Math.Abs(lt)]);
                }
                else
                {
                    solver.Assert(context.MkNot(varsDict[Math.Abs(lt)]));
                }
            }

            var check = solver.Check();
            if(check == Status.SATISFIABLE)
            {
                var model = solver.Model;
                solution = new Clause();
                foreach (var kvp in varsDict)
                {
                    if (model.Evaluate(kvp.Value, true).IsTrue)
                    {
                        solution.Add(kvp.Key);
                    }
                    else if(model.Evaluate(kvp.Value, true).IsFalse)
                    {
                        solution.Add(-kvp.Key);
                    }
                }
                solver.Pop();
                return true;
            }
            else if(check == Status.UNSATISFIABLE)
            {
                solver.Pop();
                return false;
            }

            solver.Pop();
            return null;
        }
    }
}
