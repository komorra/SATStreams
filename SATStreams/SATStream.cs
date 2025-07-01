using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SATStreams
{
    public class SATStream
    {
        static int nextId = 1;
        private int id = nextId++;

        public SATStream()
        {
        }
        
        public SATStream(SATStream stream)
        {
            Clause = new Clause(stream.Clause); 
        }

        public int Id => id;

        public Clause Clause { get; set; } = new Clause();
        public bool IsMarkedForDeletion { get; set; } = false;
    }
}
