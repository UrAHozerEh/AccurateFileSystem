using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Devices.Geolocation;

namespace AccurateFileSystem
{
    public class Hca
    {
        public List<HcaRegion> Regions { get; set; }
        public HcaRegion StartBuffer { get; private set; }
        public HcaRegion EndBuffer { get; private set; }
        public string LineName { get; private set; }
        public string Name { get; private set; }
        public double StartBufferGpsLength => StartBuffer?.GpsLength ?? 0;
        public double EndBufferGpsLength => EndBuffer?.GpsLength ?? 0;
        public double HcaGpsLength => Regions.Sum(region => region.GpsLength);
        public double TotalGpsLength => StartBufferGpsLength + HcaGpsLength + EndBufferGpsLength;


        public Hca(string name, string lineName, List<string[]> lines)
        {
            Regions = new List<HcaRegion>();
            Name = name;
            LineName = lineName;
            StartBuffer = null;
            EndBuffer = null;
            //lines = SortLines(lines);
            ParseLines(lines);
        }

        public int GetStartFootageGap()
        {
            if (StartBuffer != null)
                return 0;
            var output = 0.0;
            foreach (var region in Regions)
            {
                if (region.ShouldSkip)
                    output += region.GpsLength;
                else
                    break;
            }
            return (int)output;
        }

        public BasicGeoposition GetStartGps()
        {
            var startRegion = StartBuffer ?? Regions.First();
            return startRegion.StartGps;
        }

        public BasicGeoposition GetEndGps()
        {
            var endRegion = EndBuffer ?? Regions.Last();
            return endRegion.EndGps;
        }

        public HcaRegion GetClosestRegion(BasicGeoposition gps)
        {
            var closestRegion = Regions.First();
            foreach (var region in Regions)
            {
                closestRegion = GetCloserRegion(closestRegion, region, gps);
            }

            if (StartBuffer != null)
            {
                closestRegion = GetCloserRegion(closestRegion, StartBuffer, gps);
            }
            if (EndBuffer != null)
            {
                closestRegion = GetCloserRegion(closestRegion, EndBuffer, gps);
            }
            return closestRegion;
        }

        public (string StartMp, string EndMp) GetMpForRegion(HcaRegion region)
        {
            if (region.Equals(StartBuffer))
                return (StartBuffer.StartMp, StartBuffer.EndMp);
            if (region.Equals(EndBuffer))
                return (EndBuffer.StartMp, EndBuffer.EndMp);
            return GetMpForHca();
        }

        public (string StartMp, string EndMp) GetMpForHca()
        {
            var startMp = Regions.First().StartMp;
            var endMp = Regions.Last().EndMp;
            return (startMp, endMp);
        }

        private HcaRegion GetCloserRegion(HcaRegion region1, HcaRegion region2, BasicGeoposition gps)
        {
            var firstDist = region1.DistanceToGps(gps);
            var secondDist = region2.DistanceToGps(gps);
            if (Math.Abs(secondDist - firstDist) < 1)
            {
                if (region1.ShouldSkip && !region2.ShouldSkip)
                    return region2;
                if (!region1.ShouldSkip && region2.ShouldSkip)
                    return region1;
                var startDist = gps.Distance(region1.StartGps);
                var endDist = gps.Distance(region1.EndGps);
                var region1Start = startDist < endDist;
                return region1Start ? region1 : region2;
            }
            var firstCloser = firstDist < secondDist;
            return firstCloser ? region1 : region2;
        }

        public BasicGeoposition GetFirstNonSkipGps()
        {
            foreach (var region in Regions)
            {
                if (!region.ShouldSkip)
                    return region.StartGps;
            }
            return new BasicGeoposition();
        }

        public BasicGeoposition GetLastNonSkipGps()
        {
            var output = new BasicGeoposition();
            foreach (var region in Regions)
            {
                if (!region.ShouldSkip)
                    output = region.EndGps;
            }
            return output;
        }

        private HcaRegion GetNextRegion(int startIndex, List<string[]> lines, out int endIndex)
        {
            var line = lines[startIndex];
            var name = line[8].Trim();
            var lat = double.Parse(line[4]);
            var lon = double.Parse(line[5]);
            var startGps = new BasicGeoposition() { Latitude = lat, Longitude = lon };
            var startMp = line[2];

            lat = double.Parse(line[6]);
            lon = double.Parse(line[7]);
            var endGps = new BasicGeoposition() { Latitude = lat, Longitude = lon };
            var endMp = line[3];
            endIndex = startIndex;
            bool? isFirstTime = null;
            if (!line[9].Contains("n/a", StringComparison.OrdinalIgnoreCase))
                isFirstTime = line[9].Contains("y", StringComparison.OrdinalIgnoreCase);

            var middleGps = new List<BasicGeoposition>();

            for (int i = startIndex + 1; i < lines.Count; ++i)
            {
                line = lines[i];
                if (line.Length != 10)
                    line = line;
                bool? curIsFirstTime = null;
                if (!line[9].Contains("n/a", StringComparison.OrdinalIgnoreCase))
                    curIsFirstTime = line[9].Contains("y", StringComparison.OrdinalIgnoreCase);
                if (line[8].Trim() == name && IsSameFirstTime(isFirstTime, curIsFirstTime))
                {
                    lat = double.Parse(line[6]);
                    lon = double.Parse(line[7]);
                    middleGps.Add(endGps);
                    endGps = new BasicGeoposition() { Latitude = lat, Longitude = lon };
                    endMp = line[3];
                    endIndex = i;
                    continue;
                }
                break;
            }

            var allGps = new List<BasicGeoposition>() { startGps };
            allGps.AddRange(middleGps);
            allGps.Add(endGps);
            return new HcaRegion(allGps, name, startMp, endMp, isFirstTime);
        }

        private bool IsSameFirstTime(bool? first, bool? second)
        {
            if (first.HasValue ^ second.HasValue)
                return false;
            if (!first.HasValue && !second.HasValue)
                return true;
            return first.Value == second.Value;
        }

        private List<string[]> SortLines(List<string[]> lines)
        {
            var output = new List<(BasicGeoposition StartGps, BasicGeoposition EndGps, string[] Line)>();

            foreach (var line in lines)
            {
                var lat = double.Parse(line[4]);
                var lon = double.Parse(line[5]);
                var startGps = new BasicGeoposition() { Latitude = lat, Longitude = lon };

                lat = double.Parse(line[6]);
                lon = double.Parse(line[7]);
                var endGps = new BasicGeoposition() { Latitude = lat, Longitude = lon };
                (BasicGeoposition StartGps, BasicGeoposition EndGps, string[] Line) cur = (startGps, endGps, line);

                var hasAdded = false;
                var isCloseBefore = false;
                var closestDist = double.MaxValue;
                var closestIndex = -1;
                for (int i = 0; i < output.Count; ++i)
                {
                    var other = output[i];
                    if (other.EndGps.Equals(cur.StartGps))
                    {
                        output.Insert(i + 1, cur);
                        hasAdded = true;
                        break;
                    }
                    if (other.StartGps.Equals(cur.EndGps))
                    {
                        output.Insert(i + 1, cur);
                        hasAdded = true;
                        break;
                    }
                    var beforeDist = other.StartGps.Distance(cur.EndGps);
                    var afterDist = other.EndGps.Distance(cur.StartGps);
                    if (beforeDist < closestDist)
                    {
                        closestIndex = i;
                        isCloseBefore = true;
                    }
                    if (afterDist < closestDist)
                    {
                        closestIndex = i;
                        isCloseBefore = false;
                    }
                }
                if (!hasAdded)
                {
                    if (closestIndex == -1)
                    {
                        output.Add((startGps, endGps, line));
                        continue;
                    }
                    if (isCloseBefore)
                    {
                        output.Insert(closestIndex, cur);
                    }
                    else
                    {
                        output.Insert(closestIndex + 1, cur);
                    }
                }
            }

            return output.Select(temp => temp.Line).ToList();
        }

        private void ParseLines(List<string[]> lines)
        {
            var curRegion = GetNextRegion(0, lines, out int startIndex);
            if (curRegion.IsBuffer)
            {
                StartBuffer = curRegion;
            }
            else
            {
                Regions.Add(curRegion);
            }
            for (int i = startIndex + 1; i < lines.Count; ++i)
            {
                curRegion = GetNextRegion(i, lines, out i);
                if (!curRegion.IsBuffer)
                {
                    Regions.Add(curRegion);
                }
            }
            if (curRegion.IsBuffer)
            {
                EndBuffer = curRegion;
            }
        }

        public List<string> GetUniqueRegions()
        {
            var output = new List<string>();
            if (StartBuffer != null || EndBuffer != null)
                output.Add("Buffer");
            var uniqueRegions = Regions.GroupBy(region => region.Name)
                .Select(group => group.First().Name);
            output.AddRange(uniqueRegions);
            return output;
        }

        public override string ToString()
        {
            var startBufferString = (StartBuffer != null ? $" with {StartBuffer.GpsLength:F0} feet of starting buffer" : "");
            var endBufferString = (EndBuffer != null ? $" with {EndBuffer.GpsLength:F0} feet of ending buffer" : "");
            return $"HCA {Name}{startBufferString} {Regions.Count} regions with {HcaGpsLength:F0} feet of HCA{endBufferString}.";
        }
    }
}
