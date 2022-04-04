using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AccurateFileSystem.Udl
{
    public class UdlRead
    {
        public string Name { get; private set; }
        public double Value { get; private set; }
        public string Unit { get; private set; }
        public DateTime Time { get; private set; }

        public UdlRead(string name, double value, string unit, DateTime time)
        {
            Name = name;
            Value = value;
            Unit = unit;
            Time = time;
        }
    }
}
