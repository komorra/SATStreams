using System;
using System.Collections.Generic;
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

        public SATSolver(CNF cnf)
        {
            this.cnf = cnf;
            this.lut = Utils.CreateLUT(cnf);
        }
    }
}
