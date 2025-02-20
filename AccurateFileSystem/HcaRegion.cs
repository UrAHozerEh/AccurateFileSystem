﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Devices.Geolocation;
using Windows.Devices.Lights.Effects;

namespace AccurateFileSystem
{
    public class HcaRegion
    {
        public List<BasicGeoposition> GpsPoints { get; }
        public BasicGeoposition StartGps
        {
            get
            {
                return GpsPoints.First();
            }
            set
            {
                GpsPoints.RemoveAt(0);
                GpsPoints.Insert(0, value);
            }
        }
        public BasicGeoposition EndGps
        {
            get
            {
                return GpsPoints.Last();
            }
            set
            {
                GpsPoints.RemoveAt(GpsPoints.Count - 1);
                GpsPoints.Add(value);
            }
        }
        public double GpsLength { get; }
        public string Name { get; }
        public string StartMp { get; }
        public string EndMp { get; }
        public string ReportQName => IsBuffer ? "Non-HCA" : Name.Replace("P", "");
        public bool ShouldSkip { get; }
        public bool IsBuffer => Name == "0" || Name == "0P";
        private static (string Value, string ShortReason, string LongReason)[] SkipRegions { get; } = { ("6A", "Atmospheric", "Atmospheric Corrosion Inspection"), ("3", "Casing", "Casing Inspection") };
        public string ShortSkipReason { get; }
        public string LongSkipReason { get; }
        public bool? FirstTime { get; }
        public string FirstTimeString => FirstTime.HasValue ? (FirstTime.Value ? "Y" : "N") : "N/A";

        public HcaRegion(List<BasicGeoposition> gpsPoints, string name, string startMp, string endMp, bool? firstTime)
        {
            GpsPoints = gpsPoints;
            GpsLength = gpsPoints.TotalDistance();
            Name = name;
            if (double.TryParse(startMp, out var startMpDouble))
                startMp = startMpDouble.ToString("F4");
            if (double.TryParse(endMp, out var endMpDouble))
                endMp = endMpDouble.ToString("F4");
            StartMp = startMp;
            EndMp = endMp;
            FirstTime = firstTime;
            (ShouldSkip, ShortSkipReason, LongSkipReason) = CheckShouldSkip(name);
        }

        public HcaRegion(string name, bool? firstTime)
        {
            Name = name;
            FirstTime = firstTime;
            (ShouldSkip, ShortSkipReason, LongSkipReason) = CheckShouldSkip(name);
        }

        public HcaRegion()
        {

        }

        public void ShiftGps(double latitudeShift, double longitudeShift)
        {
            var updated = new List<BasicGeoposition>();
            foreach (var gps in GpsPoints)
            {
                var newGps = new BasicGeoposition
                {
                    Latitude = gps.Latitude + latitudeShift,
                    Longitude = gps.Longitude + longitudeShift
                };
                updated.Add(newGps);
            }
            GpsPoints.Clear();
            GpsPoints.AddRange(updated);
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
            for (var i = 0; i < GpsPoints.Count - 1; ++i)
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
