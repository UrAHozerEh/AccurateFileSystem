using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AccurateFileSystem.Udl
{
    public class UdlDataSet
    {
        public DateTime StartTime { get; private set; }
        public DateTime EndTime { get; private set; }
        public List<(double Hour, double Value)> Values { get; private set; }
        public string Unit { get; private set; }

        public UdlDataSet(List<(double Hour, double Value)> values, DateTime startTime, DateTime endTime, string unit)
        {
            StartTime = startTime;
            EndTime = endTime;
            Values = values;
            Unit = unit;
        }
    }
}
