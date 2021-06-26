using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Devices.Geolocation;

namespace AccurateFileSystem
{
    public class HcaRegion
    {
        public List<BasicGeoposition> GpsPoints { get; }
        public BasicGeoposition StartGps { get; set; }
        public BasicGeoposition EndGps { get; set; }
        public double GpsLength { get; }
        public string Name { get; }
        public string StartMp { get; }
        public string EndMp { get; }
        public string ReportQName => Name == "0" ? "Non-HCA" : Name;
        public bool ShouldSkip { get; }
        public bool IsBuffer => Name == "0";
        private static (string Value, string ShortReason, string LongReason)[] SkipRegions { get; } = new (string Value, string ShortReason, string LongReason)[] { ("7A", "Atmospheric", "Atmospheric Corrosion Inspection"), ("6A", "Atmospheric", "Atmospheric Corrosion Inspection"), ("3", "Casing", "Casing Inspection") };
        public string ShortSkipReason { get; }
        public string LongSkipReason { get; }

        public HcaRegion(List<BasicGeoposition> gpsPoints, string name, string startMp, string endMp)
        {
            GpsPoints = gpsPoints;
            StartGps = gpsPoints.First();
            EndGps = gpsPoints.Last();
            GpsLength = gpsPoints.TotalDistance();
            Name = name;
            StartMp = startMp;
            EndMp = endMp;
            (ShouldSkip, ShortSkipReason, LongSkipReason) = CheckShouldSkip(name);
        }

        public HcaRegion()
        {

        }

        private static (bool ShouldSkip, string ShortReason, string LongReason) CheckShouldSkip(string name)
        {
            foreach (var (skip, shortReason, longReason) in SkipRegions)
            {
                if (name.Contains(skip))
                {
                    return (true, shortReason, longReason);
                }
            }
            return (false, "", "");
        }

        public double DistanceToGps(BasicGeoposition gps)
        {
            var closestDistance = double.MaxValue;
            for(int i = 0; i < GpsPoints.Count-1; ++i)
            {
                var start = GpsPoints[i];
                var end = GpsPoints[i + 1];
                var curDistance = gps.DistanceToSegment(start, end).Distance;
                if (curDistance < closestDistance)
                    closestDistance = curDistance;
            }
            return closestDistance;
        }

        public override string ToString()
        {
            return $"{ReportQName} from MP {StartMp} to MP {EndMp}. {GpsLength:F1} feet long.";
        }

        public override bool Equals(object obj)
        {
            var other = obj as HcaRegion;
            if (other == null)
                return false;
            var otherValue = other;
            if (StartMp != otherValue.StartMp)
                return false;
            if (EndMp != otherValue.EndMp)
                return false;
            if (Name != otherValue.Name)
                return false;
            return true;
        }
    }
}
