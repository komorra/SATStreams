using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SATStreams
{
    public class SATStream
    {
        public Clause Clause { get; } = new Clause();
        public bool IsMarkedForDeletion { get; set; } = false;
    }
}
