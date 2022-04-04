using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace AccurateFileSystem.Udl
{
    public class UdlFile : GeneralCsv
    {
        public Dictionary<string, List<UdlRead>> Reads = new Dictionary<string, List<UdlRead>>();
        public List<string> Notes = new List<string>();
        private static Regex ShuntRegex = new Regex(@"(?i)shunt\s+?(\d+)(m?v)\s+?-?\s+?(\d+)(m?a)");

        public UdlFile(string name, List<string> lines) : base(name, lines, FileType.Udl)
        {
            ParseData();
            foreach (var reads in Reads.Values)
            {
                reads.Sort((read1, read2) => read1.Time.CompareTo(read2.Time));
            }

            GetNotes();
            GetShuntValues();
        }

        public (double Min, double Max) GetMaxAndMinValues(string readType)
        {
            var max = 0.0;
            var min = 0.0;
            foreach (var read in Reads[readType])
            {
                if (read.Value > max)
                {
                    max = read.Value;
                }
                if (read.Value < min)
                {
                    min = read.Value;
                }
            }

            return (min, max);
        }

        private void GetNotes()
        {
            for (var col = 0; col < Headers.Count; ++col)
            {
                if (Headers[col] == "Notes")
                {
                    for (var row = 0; row < Data.GetLength(0); ++row)
                    {
                        if (!string.IsNullOrWhiteSpace(Data[row, col]))
                        {
                            Notes.Add(Data[row, col]);
                        }
                    }
                }
            }
        }

        private void GetShuntValues()
        {
            var ratio = CheckNotesForShuntRatio();
            if (!ratio.HasValue)
            {
                return;
            }
            var ratioValue = ratio.Value;
            var addedReads = new Dictionary<string, List<UdlRead>>();
            foreach (var (readType, reads) in Reads)
            {
                var name = readType + " Shunt";
                var convertedReads = reads.Select(read => new UdlRead(name, read.Value * ratioValue, "A", read.Time)).ToList();
                var renamedReads = reads.Select(read => new UdlRead(name, read.Value, read.Unit, read.Time)).ToList();
                addedReads.Add(name + " Amps", convertedReads);
                addedReads.Add(name + " Volts", renamedReads);
            }
            foreach (var (newName, newValues) in addedReads)
            {
                Reads.Add(newName, newValues);
            }
        }

        private double? CheckNotesForShuntRatio()
        {
            foreach (var note in Notes)
            {
                var match = ShuntRegex.Match(note);
                if (match.Success)
                {
                    var volt = double.Parse(match.Groups[1].Value);
                    if (match.Groups[2].Value.Contains("m", StringComparison.OrdinalIgnoreCase))
                    {
                        volt /= 1000;
                    }
                    var amp = double.Parse(match.Groups[3].Value);
                    if (match.Groups[4].Value.Contains("m", StringComparison.OrdinalIgnoreCase))
                    {
                        amp /= 1000;
                    }
                    return amp / volt;
                }
            }
            return null;
        }

        private int GetTimeColumn()
        {
            for (var i = 0; i < Headers.Count; ++i)
            {
                var header = Headers[i];
                if (header.StartsWith("Excel Time"))
                {
                    return i;
                }
            }
            return -1;
        }

        private void ParseData()
        {
            var timeColumn = GetTimeColumn();
            for (var readingCol = timeColumn + 1; readingCol < Data.GetLength(1); ++readingCol)
            {
                var readingHeader = Headers[readingCol];
                if (!readingHeader.EndsWith("Reading"))
                {
                    continue;
                }

                var unitHeader = readingHeader.Replace("Reading", "Units");
                for (var unitCol = timeColumn + 1; unitCol < Data.GetLength(1); ++unitCol)
                {
                    if (Headers[unitCol] == unitHeader)
                    {
                        ParseReadColumn(timeColumn, readingCol, unitCol);
                        break;
                    }
                }
            }
        }

        private void ParseReadColumn(int timeColumn, int readingColumn, int unitColumn)
        {
            for (var row = 0; row < Data.GetLength(0); ++row)
            {
                var readString = Data[row, readingColumn];
                if (string.IsNullOrWhiteSpace(readString))
                {
                    continue;
                }
                var read = double.Parse(readString);
                var timeString = Data[row, timeColumn];
                var time = timeString.GetTimeFromExcelTime();
                if (Headers[timeColumn].Contains("(UTC)"))
                {
                    time = time.AddHours(-7);
                }
                var unitString = Data[row, unitColumn];
                var readName = Data[row, 0].Replace(" Reading", "");
                var udlRead = new UdlRead(readName, read, unitString, time);
                AddRead(udlRead);
            }
        }

        private void AddRead(UdlRead read)
        {
            if (!Reads.ContainsKey(read.Name))
            {
                Reads.Add(read.Name, new List<UdlRead>());
            }
            Reads[read.Name].Add(read);
        }

        public Dictionary<string, Dictionary<string, UdlDataSet>> GetDayFullData(double hours)
        {
            var output = new Dictionary<string, Dictionary<string, UdlDataSet>>();
            foreach (var readType in Reads.Keys)
            {
                output.Add(readType, GetDayTypeData(readType, hours));
            }
            return output;
        }

        private Dictionary<string, UdlDataSet> GetDayTypeData(string readType, double hours)
        {
            var output = new Dictionary<string, UdlDataSet>();

            var reads = Reads[readType];
            var startTime = reads[0].Time;
            var endTime = reads[reads.Count - 1].Time;
            var times = GetDayTimes(startTime, endTime, hours);
            var unit = reads[0].Unit;

            for (var cur = 0; cur <= times.Count - 2; ++cur)
            {
                var curStart = times[cur];
                var curEnd = times[cur + 1];
                var filteredReads = GetFilteredReads(reads, curStart, curEnd);
                var section = $" {curStart.ToShortTimeString()} to {curEnd.ToShortTimeString()}";
                if (curStart.AddDays(1) == curEnd)
                {
                    section = "";
                }
                var pageName = $"{curStart.ToShortDateString().Replace("/", "-")}{section}";
                var values = GetHourValuePair(filteredReads);
                output.Add(pageName, new UdlDataSet(values, curStart, curEnd, unit));
            }

            return output;
        }

        private List<DateTime> GetDayTimes(DateTime startTime, DateTime endTime, double hours)
        {
            var output = new List<DateTime>();
            var initialTime = startTime.Date;
            while (initialTime.AddHours(hours) < startTime)
            {
                initialTime = initialTime.AddHours(hours);
            }

            var finalTime = endTime.Date.AddDays(1);
            while (finalTime.AddHours(-hours) > endTime)
            {
                finalTime = finalTime.AddHours(-hours);
            }

            var curTime = initialTime;
            while (curTime <= finalTime)
            {
                output.Add(curTime);
                curTime = curTime.AddHours(hours);
            }

            return output;
        }

        private List<(double Hour, double Value)> GetHourValuePair(List<UdlRead> reads)
        {
            var output = new List<(double Hour, double Value)>();

            foreach (var read in reads)
            {
                var value = read.Value;
                var ticksInDay = (double)read.Time.Ticks - read.Time.Date.Ticks;
                var hour = ticksInDay / TimeSpan.TicksPerHour;
                output.Add((hour, value));
            }

            return output;
        }

        private List<(double Hour, double Value)> GetHourValuePair(List<UdlRead> reads, DateTime startTime)
        {
            var output = new List<(double Hour, double Value)>();

            foreach (var read in reads)
            {
                var value = read.Value;
                var timeSpan = read.Time - startTime;
                output.Add((timeSpan.TotalHours, value));
            }

            return output;
        }

        public UdlDataSet GetFullData(string readType)
        {
            var reads = Reads[readType];
            var start = reads.First().Time;
            var end = reads.Last().Time;
            var values = GetHourValuePair(reads, start.Date);
            var unit = reads[0].Unit;
            return new UdlDataSet(values, start.Date, end, unit);
        }

        private List<UdlRead> GetFilteredReads(List<UdlRead> reads, DateTime startTime, DateTime endTime)
        {
            var output = new List<UdlRead>();

            foreach (var read in reads)
            {
                if (read.Time < startTime)
                {
                    continue;
                }
                if (read.Time > endTime)
                {
                    break;
                }
                output.Add(read);
            }

            return output;
        }
    }
}
