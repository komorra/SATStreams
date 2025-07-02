using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SATStreams
{
    [AttributeUsage(AttributeTargets.Property)]
    internal class PerIntervalAttribute : Attribute
    {
        public TimeSpan Interval { get; private set; }

        public string PropertyName { get; private set; }

        public PerIntervalAttribute(double seconds, string propertyName)
        {
            Interval = TimeSpan.FromSeconds(seconds);
            PropertyName = propertyName;
        }
    }
}
