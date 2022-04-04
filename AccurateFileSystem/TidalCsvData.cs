using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AccurateFileSystem
{
    public class TidalCsvData : GeneralCsv
    {
        public List<(DateTime Time, double Height)> TidalData { get; set; }
        public double Max { get; set; } = double.MinValue;
        public double Min { get; set; } = double.MaxValue;
        public double Diff => Max - Min;
        public double BufferPercentOfDiff { get; set; } = 0.05;
        public double Buffer => Diff * BufferPercentOfDiff;
        public double MaxWithBuffer => Max + Buffer;
        public double MinWithBuffer => Min - Buffer;

        public TidalCsvData(string name, List<string> lines) : base(name, lines)
        {
            ProcessData();
        }

        private void ProcessData()
        {
            var dateCol = -1;
            var timeCol = -1;
            var heightCol = -1;
            for(int i = 0; i < Headers.Count; ++i)
            {
                var header = Headers[i];
                switch (header)
                {
                    case "Date":
                        dateCol = i;
                        break;
                    case "Time (GMT)":
                        timeCol = i;
                        break;
                    case "Preliminary (ft)":
                        heightCol = i;
                        break;
                    default:
                        break;
                }
            }
            TidalData = new List<(DateTime Time, double Height)>();
            for (int r = 0; r < Data.GetLength(0); ++r)
            {
                var curDate = Data[r, dateCol];
                var curTime = Data[r, timeCol];
                var curHeight = double.Parse(Data[r, heightCol]);
                var curDateTime = DateTime.Parse($"{curDate} {curTime}");
                curDateTime = curDateTime.AddHours(-8);
                Max = Math.Max(Max, curHeight);
                Min = Math.Min(Min, curHeight);
                TidalData.Add((curDateTime, curHeight));
            }
            TidalData.Sort((first, second) => first.Time.CompareTo(second.Time));
        }

        public List<(double Hour, double Height)> GetGraphData(DateTime start)
        {
            var output = new List<(double, double)>();

            foreach(var (Time, Height) in TidalData)
            {
                var curTimespan = Time - start;
                output.Add((curTimespan.TotalHours, Height));
            }

            return output;
        }
    }
}
