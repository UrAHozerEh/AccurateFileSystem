using AccurateFileSystem;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using Windows.Devices.Geolocation;
using Windows.UI;

namespace AccurateReportSystem
{
    public class PGECISIndicationChartSeries : ExceptionsChartSeries
    {

        public struct DataPoint
        {
            public double Footage;
            public double On;
            public double Off;
            public string Comment;
            public DateTime Date;
            public double? Depth;
            public bool IsExtrapolated;
            public double Baseline;
            public BasicGeoposition Gps;
            public string Region;
            public PGESeverity Severity;
            public string Reason;
            public static string Headers => "Footage\tOn\tOff\tComment\tDate\tDepth\tIs Extrapolated\tBaseline\tGps\tRegion\tSeverity\tReason";

            public DataPoint(ExtrapolatedDataPoint extrapolated, double baseline, PGESeverity severity, string reason)
            {
                Footage = extrapolated.Footage;
                On = extrapolated.On;
                Off = extrapolated.Off;
                Comment = extrapolated.Comment;
                Date = extrapolated.Date;
                Depth = extrapolated.Depth;
                IsExtrapolated = extrapolated.IsExtrapolated;
                Baseline = baseline;
                Gps = extrapolated.Gps;
                Region = extrapolated.Region;
                Severity = severity;
                Reason = reason;
            }
            public override string ToString()
            {
                return $"{Footage}\t{On}\t{Off}\t{Comment}\t{Date.ToShortTimeString()}\t{Depth}\t{IsExtrapolated}\t{Baseline}\t{Gps}\t{Region}\t{Severity}\t{Reason}";
            }
        }

        public struct DataPointUpdated
        {
            public double Footage;
            public double On;
            public double Off;
            public string Comment;
            public DateTime Date;
            public double? Depth;
            public bool IsExtrapolated;
            public double Baseline;
            public BasicGeoposition Gps;
            public HcaRegion Region;
            public PGESeverity Severity;
            public string Reason;
            public static string Headers => "Footage\tOn\tOff\tComment\tDate\tDepth\tIs Extrapolated\tBaseline\tLatitude\tLongitude\tRegion\tSeverity\tReason";

            public DataPointUpdated(ExtrapolatedDataPointUpdated extrapolated, double baseline, PGESeverity severity, string reason)
            {
                Footage = extrapolated.Footage;
                On = extrapolated.On;
                Off = extrapolated.Off;
                Comment = extrapolated.Comment;
                Date = extrapolated.Date;
                Depth = extrapolated.Depth;
                IsExtrapolated = extrapolated.IsExtrapolated;
                Baseline = baseline;
                Gps = extrapolated.Gps;
                Region = extrapolated.Region;
                Severity = severity;
                Reason = reason;
            }
            public override string ToString()
            {
                return $"{Footage}\t{On}\t{Off}\t{Comment}\t{Date.ToShortTimeString()}\t{Depth}\t{IsExtrapolated}\t{Baseline}\t{Gps.Latitude}\t{Gps.Longitude}\t{Region.ReportQName}\t{Severity}\t{Reason}";
            }
        }

        public override int NumberOfValues => 3;
        public Color MinorColor { get; set; } = Colors.Blue;
        public Color ModerateColor { get; set; } = Colors.Green;
        public Color SevereColor { get; set; } = Colors.Red;
        public CombinedAllegroCisFile CisFile { get; set; }
        public Hca Hca { get; set; }
        public Skips Skips { get; set; }
        public List<DataPoint> Data { get; set; }
        public List<DataPointUpdated> DataUpdated { get; set; }
        public List<(double Footage, double Value)> Baselines { get; set; }
        public List<(double Footage, double UsedBaselineFootage)> UsedBaselineFootages { get; set; } 
        public List<(double Footage, double Value)> Averages { get; set; }
        public double SkipDistance { get; set; } = 20;
        public List<(double Footage, AllegroDataPoint Point)> RawData { get; set; }
        public List<(BasicGeoposition Start, BasicGeoposition End, string Region)> EcdaRegions { get; set; }

        //public PGECISIndicationChartSeries(List<(double, AllegroDataPoint)> data, Chart chart, List<(BasicGeoposition Start, BasicGeoposition End, string Region)> regions) : base(chart.LegendInfo, chart.YAxesInfo)
        //{
        //    RawData = data;
        //    EcdaRegions = regions;
        //    Data = ExtrapolateData(data);
        //}

        public PGECISIndicationChartSeries(CombinedAllegroCisFile cisFile, Chart chart, Hca hca, Skips skips) : base(chart.LegendInfo, chart.YAxesInfo)
        {
            CisFile = cisFile;
            RawData = cisFile.GetPoints();
            Hca = hca;
            Skips = skips;
            DataUpdated = ExtrapolateDataUpdated(RawData, skips);
        }

        //public PGECISIndicationChartSeries(List<(double, AllegroDataPoint)> data, LegendInfo masterLegendInfo, YAxesInfo masterYAxesInfo) : base(masterLegendInfo, masterYAxesInfo)
        //{
        //    RawData = data;
        //    Data = ExtrapolateData(data);
        //}

        private string GetClosestRegion(BasicGeoposition gps)
        {
            if (EcdaRegions == null)
                return "";
            if (EcdaRegions.Count == 0)
                return "";
            if (EcdaRegions.Count == 1)
                return EcdaRegions[0].Region;
            var closestDist = double.MaxValue;
            var closestRegion = EcdaRegions[0].Region;
            foreach (var (start, end, region) in EcdaRegions)
            {
                var curDistance = gps.DistanceToSegment(start, end).Distance;
                if (curDistance < closestDist)
                {
                    closestDist = curDistance;
                    closestRegion = region;
                }
                if (curDistance == closestDist)
                {
                    var startDist = gps.Distance(start);
                    var endDist = gps.Distance(end);
                    if (startDist < endDist)
                    {
                        closestDist = curDistance;
                        closestRegion = region;
                    }
                }
            }
            return closestRegion;
        }

        private HcaRegion GetClosestRegionUpdated(BasicGeoposition gps)
        {
            return Hca.GetClosestRegion(gps);
        }

        public struct ExtrapolatedDataPoint
        {
            public double Footage;
            public double On;
            public double Off;
            public string Comment;
            public DateTime Date;
            public double? Depth;
            public BasicGeoposition Gps;
            public string Region;
            public bool IsExtrapolated;
            public bool IsSkipped;

            public ExtrapolatedDataPoint(double footage, AllegroDataPoint point, string region)
            {
                Footage = footage;
                On = point.On;
                Off = point.Off;
                Comment = point.OriginalComment;
                Date = point.Times[0];
                Depth = point.Depth;
                Gps = point.GPS;
                Region = region;
                IsExtrapolated = false;
                IsSkipped = false;
            }
        }

        public struct ExtrapolatedDataPointUpdated
        {
            public double Footage;
            public double On;
            public double Off;
            public string Comment;
            public DateTime Date;
            public double? Depth;
            public BasicGeoposition Gps;
            public HcaRegion Region;
            public bool IsExtrapolated;
            public bool IsOnOff;
            public bool IsSkipped { get; set; }

            public ExtrapolatedDataPointUpdated(double footage, bool isOnOff, AllegroDataPoint point, HcaRegion region, bool shouldSkip = false)
            {
                Footage = footage;
                On = point.On;
                Off = point.Off;
                IsOnOff = isOnOff;
                Comment = point.OriginalComment;
                Date = point.Times[0];
                Depth = point.Depth;
                Gps = point.GPS;
                Region = region;
                IsExtrapolated = false;
                IsSkipped = Region.ShouldSkip || shouldSkip;
            }
        }

        //private List<DataPoint> ExtrapolateData(List<(double Footage, AllegroDataPoint Point)> data)
        //{
        //    //var extrapolatedData = new List<(double Footage, double On, double Off, string Comment, DateTime Date, double? Depth, BasicGeoposition GpsPoint, string Region, bool IsExtrapolated, bool isSkipped)>();
        //    var extrapolatedData = new List<ExtrapolatedDataPoint>();
        //    string curRegion;
        //    ExtrapolatedDataPoint curExtrapPoint;
        //    for (var i = 0; i < data.Count - 1; ++i)
        //    {
        //        var (startFoot, startPoint) = data[i];
        //        var (endFoot, endPoint) = data[i + 1];
        //        curRegion = GetClosestRegion(startPoint.GPS);
        //        curExtrapPoint = new ExtrapolatedDataPoint(startFoot, startPoint, curRegion);
        //        extrapolatedData.Add(curExtrapPoint);
        //        var dist = endFoot - startFoot;

        //        var onDiff = endPoint.On - startPoint.On;
        //        var offDiff = endPoint.Off - startPoint.Off;
        //        var onPerFoot = onDiff / dist;
        //        var offPerFoot = offDiff / dist;

        //        var latDiff = endPoint.GPS.Latitude - startPoint.GPS.Latitude;
        //        var latPerFoot = latDiff / dist;

        //        var longDiff = endPoint.GPS.Longitude - startPoint.GPS.Longitude;
        //        var longPerFoot = longDiff / dist;

        //        if (dist > SkipDistance)
        //        {
        //            var midGps = startPoint.GPS.MiddleTowards(endPoint.GPS);
        //            curRegion = GetClosestRegion(midGps);
        //        }

        //        for (var offset = 1; offset < dist; ++offset)
        //        {
        //            var newOn = onPerFoot * offset + startPoint.On;
        //            var newOff = offPerFoot * offset + startPoint.Off;
        //            var newFoot = startFoot + offset;

        //            var newLat = latPerFoot * offset + startPoint.GPS.Latitude;
        //            var newLong = longPerFoot * offset + startPoint.GPS.Longitude;
        //            var newGps = new BasicGeoposition() { Latitude = newLat, Longitude = newLong };
        //            curExtrapPoint = new ExtrapolatedDataPoint()
        //            {
        //                Footage = newFoot,
        //                On = newOn,
        //                Off = newOff,
        //                Comment = startPoint.OriginalComment,
        //                Date = startPoint.Times[0],
        //                Depth = startPoint.Depth,
        //                Gps = newGps,
        //                Region = curRegion,
        //                IsExtrapolated = true,
        //                IsSkipped = dist > SkipDistance
        //            };
        //            extrapolatedData.Add(curExtrapPoint);
        //        }
        //    }
        //    var (foot, point) = data.Last();
        //    curRegion = GetClosestRegion(point.GPS);
        //    curExtrapPoint = new ExtrapolatedDataPoint(foot, point, curRegion);
        //    extrapolatedData.Add(curExtrapPoint);
        //    var output = new List<DataPoint>(extrapolatedData.Count);
        //    DataPoint curPoint;
        //    if (foot <= 200)
        //    {
        //        var average = extrapolatedData.Average(value => value.Off);
        //        for (var i = 0; i < extrapolatedData.Count; ++i)
        //        {
        //            curExtrapPoint = extrapolatedData[i];
        //            foot = curExtrapPoint.Footage;
        //            curRegion = curExtrapPoint.Region;
        //            var changeInBaseline = Math.Abs(curExtrapPoint.Off - average);
        //            var (severity, reason) = GetSeverity(curExtrapPoint.Off, average);
        //            if (curExtrapPoint.IsSkipped)
        //            {
        //                severity = PGESeverity.NRI;
        //                reason = "SKIP";
        //            }
        //            curPoint = new DataPoint(curExtrapPoint, average, severity, reason);
        //            output.Add(curPoint);
        //        }
        //        return output;
        //    }
        //    var curAverages = new List<double>(extrapolatedData.Count);
        //    for (var i = 0; i < extrapolatedData.Count; ++i)
        //    {
        //        curExtrapPoint = extrapolatedData[i];
        //        foot = curExtrapPoint.Footage;
        //        var within100 = extrapolatedData.Where(value => Within100(foot, value.Footage) && !value.IsSkipped);
        //        var average = (within100.Count() != 0 ? within100.Average(value => value.Off) : curExtrapPoint.Off);
        //        curAverages.Add(average);
        //    }
        //    var curBaselines = Enumerable.Repeat(double.NaN, extrapolatedData.Count).ToList();
        //    for (var center = 0; center < extrapolatedData.Count; ++center)
        //    {
        //        var start = Math.Max(center - 105, 0);
        //        var end = Math.Min(center + 105, extrapolatedData.Count - 1);
        //        var centerExtrap = extrapolatedData[center];
        //        var centerAverage = curAverages[center];
        //        for (var i = start; i <= end; ++i)
        //        {
        //            var curFoot = extrapolatedData[i].Footage;
        //            var curAverage = curAverages[i];
        //            if (Within100(centerExtrap.Footage, curFoot))
        //            {
        //                var curBaseline = curBaselines[center];
        //                if (double.IsNaN(curBaseline))
        //                {
        //                    curBaselines[center] = curAverage;
        //                    continue;
        //                }
        //                var diffFromBaseline = Math.Abs(centerExtrap.Off - curAverage);
        //                var diffFromCurBaseline = Math.Abs(centerExtrap.Off - curBaselines[center]);
        //                if (diffFromBaseline > diffFromCurBaseline)
        //                {
        //                    curBaselines[center] = curAverage;
        //                }
        //            }
        //        }
        //        var baseline = curBaselines[center];
        //        var (severity, reason) = GetSeverity(centerExtrap.Off, baseline);
        //        if (centerExtrap.IsSkipped)
        //        {
        //            severity = PGESeverity.NRI;
        //            reason = "SKIP";
        //        }
        //        curPoint = new DataPoint(centerExtrap, baseline, severity, reason);
        //        output.Add(curPoint);
        //    }
        //    return output;
        //}

        private List<DataPointUpdated> ExtrapolateDataUpdated(List<(double Footage, AllegroDataPoint Point)> data, Skips cisSkips)
        {
            //var extrapolatedData = new List<(double Footage, double On, double Off, string Comment, DateTime Date, double? Depth, BasicGeoposition GpsPoint, string Region, bool IsExtrapolated, bool isSkipped)>();
            List<HcaRegion> allRegions = new List<HcaRegion>();
            if (Hca.HasStartBuffer)
                allRegions.Add(Hca.StartBuffer);
            allRegions.AddRange(Hca.Regions);
            if (Hca.HasEndBuffer)
                allRegions.Add(Hca.EndBuffer);

            Queue<(double Footage, HcaRegion Region)> regionEnds = new Queue<(double Footage, HcaRegion Region)>();
            var lastFoot = double.MinValue;
            foreach (var region in allRegions)
            {
                var end = data.Where(d => d.Footage > lastFoot).OrderBy(d => d.Point.GPS.Distance(region.EndGps)).First();
                regionEnds.Enqueue((end.Footage, region));
                lastFoot = end.Footage;
            }

            var extrapolatedData = new List<ExtrapolatedDataPointUpdated>();
            var (regionEnd, curRegion) = regionEnds.Dequeue();
            ExtrapolatedDataPointUpdated curExtrapPoint;
            for (var i = 0; i < data.Count - 1; ++i)
            {
                var (startFoot, startPoint) = data[i];
                if(startFoot == regionEnd)
                {
                    (regionEnd, curRegion) = regionEnds.Dequeue();
                }
                var (endFoot, endPoint) = data[i + 1];
                var shouldSkipExtrap = false;
                HcaRegion skipRegion = null;
                if (cisSkips != null && cisSkips.Footages.Any(f => f.Footage >= startFoot && f.Footage <= endFoot))
                {
                    var skip = cisSkips.Footages.First(f => f.Footage >= startFoot && f.Footage <= endFoot);
                    shouldSkipExtrap = true;
                    skipRegion = skip.Region;
                }
                curRegion = GetClosestRegionUpdated(startPoint.GPS);
                curExtrapPoint = new ExtrapolatedDataPointUpdated(startFoot, CisFile.Type == FileType.OnOff, startPoint, curRegion);
                extrapolatedData.Add(curExtrapPoint);
                var dist = endFoot - startFoot;

                var onDiff = endPoint.On - startPoint.On;
                var offDiff = endPoint.Off - startPoint.Off;
                var onPerFoot = onDiff / dist;
                var offPerFoot = offDiff / dist;

                var latDiff = endPoint.GPS.Latitude - startPoint.GPS.Latitude;
                var latPerFoot = latDiff / dist;

                var longDiff = endPoint.GPS.Longitude - startPoint.GPS.Longitude;
                var longPerFoot = longDiff / dist;

                if (dist > SkipDistance)
                {
                    var midGps = startPoint.GPS.MiddleTowards(endPoint.GPS);
                    curRegion = GetClosestRegionUpdated(midGps);
                }

                for (var offset = 1; offset < dist; ++offset)
                {
                    var newOn = onPerFoot * offset + startPoint.On;
                    var newOff = offPerFoot * offset + startPoint.Off;
                    var newFoot = startFoot + offset;

                    var newLat = latPerFoot * offset + startPoint.GPS.Latitude;
                    var newLong = longPerFoot * offset + startPoint.GPS.Longitude;
                    var newGps = new BasicGeoposition() { Latitude = newLat, Longitude = newLong };
                    var extrapRegion = GetClosestRegionUpdated(newGps);
                    if (shouldSkipExtrap && skipRegion != null)
                        extrapRegion = skipRegion;
                    curExtrapPoint = new ExtrapolatedDataPointUpdated()
                    {
                        Footage = newFoot,
                        On = newOn,
                        Off = newOff,
                        Comment = startPoint.OriginalComment,
                        Date = startPoint.Times[0],
                        Depth = startPoint.Depth,
                        Gps = newGps,
                        Region = extrapRegion,
                        IsExtrapolated = true,
                        IsOnOff = CisFile.Type == FileType.OnOff,
                        IsSkipped = dist > SkipDistance || curRegion.ShouldSkip || shouldSkipExtrap,
                        
                    };
                    extrapolatedData.Add(curExtrapPoint);
                }
            }
            var (foot, point) = data.Last();
            curRegion = GetClosestRegionUpdated(point.GPS);
            curExtrapPoint = new ExtrapolatedDataPointUpdated(foot, CisFile.Type == FileType.OnOff, point, curRegion);
            extrapolatedData.Add(curExtrapPoint);
            var output = new List<DataPointUpdated>(extrapolatedData.Count);
            DataPointUpdated curPoint;

            Averages = new List<(double Footage, double Value)>(extrapolatedData.Count);
            Baselines = Enumerable.Repeat((double.NaN, double.NaN), extrapolatedData.Count).ToList();
            UsedBaselineFootages = Enumerable.Repeat((double.NaN, double.NaN), extrapolatedData.Count).ToList();
            var useBaselines = true;
            if (foot <= 200)
            {
                var average = extrapolatedData.Average(value => curExtrapPoint.IsOnOff ? value.Off : value.On);
                for (var i = 0; i < extrapolatedData.Count; ++i)
                {
                    curExtrapPoint = extrapolatedData[i];
                    if (!useBaselines)
                        average = curExtrapPoint.IsOnOff ? curExtrapPoint.Off : curExtrapPoint.On;
                    foot = curExtrapPoint.Footage;
                    Averages.Add((foot, average));
                    Baselines[i] = (foot, average);
                    UsedBaselineFootages[i] = (foot, foot);
                    curRegion = curExtrapPoint.Region;
                    var (severity, reason) = curExtrapPoint.IsOnOff ? GetOnOffSeverity(curExtrapPoint.Off, average) : GetOnSeverity(curExtrapPoint.On, average);
                    if (curExtrapPoint.IsSkipped)
                    {
                        severity = PGESeverity.NRI;
                        reason = "SKIP";
                    }
                    curPoint = new DataPointUpdated(curExtrapPoint, average, severity, reason);
                    output.Add(curPoint);
                }
                return output;
            }
            for (var i = 0; i < extrapolatedData.Count; ++i)
            {
                curExtrapPoint = extrapolatedData[i];
                foot = curExtrapPoint.Footage;
                var within100 = extrapolatedData.Where(value => Within100(foot, value.Footage) && !value.IsSkipped);
                var average = (within100.Count() != 0 ? within100.Average(value => curExtrapPoint.IsOnOff ? value.Off : value.On) : curExtrapPoint.IsOnOff ? curExtrapPoint.Off : curExtrapPoint.On);
                Averages.Add((foot, average));
            }
            Baselines = Enumerable.Repeat((double.NaN, double.NaN), extrapolatedData.Count).ToList();
            UsedBaselineFootages = Enumerable.Repeat((double.NaN, double.NaN), extrapolatedData.Count).ToList();
            for (var center = 0; center < extrapolatedData.Count; ++center)
            {
                var start = Math.Max(center - 105, 0);
                var end = Math.Min(center + 105, extrapolatedData.Count - 1);
                var centerExtrap = extrapolatedData[center];
                var centerAverage = Averages[center];
                for (var i = start; i <= end; ++i)
                {
                    var curFoot = extrapolatedData[i].Footage;
                    var curAverage = Averages[i];
                    if (Within100(centerExtrap.Footage, curFoot))
                    {
                        var curBaseline = Baselines[center];
                        if (double.IsNaN(curBaseline.Value))
                        {
                            Baselines[center] = (centerExtrap.Footage, curAverage.Value);
                            UsedBaselineFootages[center] = (centerExtrap.Footage, curFoot);
                            continue;
                        }
                        var diffFromBaseline = Math.Abs(curExtrapPoint.IsOnOff ? centerExtrap.Off : centerExtrap.On - curAverage.Value);
                        var diffFromCurBaseline = Math.Abs(curExtrapPoint.IsOnOff ? centerExtrap.Off : centerExtrap.On - Baselines[center].Value);
                        if (diffFromBaseline > diffFromCurBaseline)
                        {
                            Baselines[center] = (centerExtrap.Footage, curAverage.Value);
                            UsedBaselineFootages[center] = (centerExtrap.Footage, curFoot);
                        }
                    }
                }
                var baseline = Baselines[center];
                var baselineValue = useBaselines ? baseline.Value : centerExtrap.IsOnOff ? centerExtrap.Off : centerExtrap.On;
                var (severity, reason) = centerExtrap.IsOnOff ? GetOnOffSeverity(centerExtrap.Off, baselineValue) : GetOnSeverity(centerExtrap.On, baselineValue);//GetSeverity(centerExtrap.Off, baseline.Value);
                
                if (centerExtrap.IsSkipped)
                {
                    severity = PGESeverity.NRI;
                    reason = "SKIP";
                }
                curPoint = new DataPointUpdated(centerExtrap, baselineValue, severity, reason);//new DataPointUpdated(centerExtrap, baseline.Value, severity, reason);
                output.Add(curPoint);
            }
            return output;
        }

        private (PGESeverity, string) GetOnOffSeverity(double off, double baseline)
        {
            var changeInBaseline = Math.Abs(off - baseline);
            if (off > -0.5)
                return (PGESeverity.Severe, "Off is less than -0.500V");
            if (off > -0.7 && changeInBaseline >= 0.2)
                return (PGESeverity.Severe, "Off is between -0.700V and -0.501V and difference in baseline is greater than 0.200V");
            if (off > -0.7)
                return (PGESeverity.Moderate, "Off is between -0.700V and -0.501V");
            if (off > -0.85 && changeInBaseline >= 0.2)
                return (PGESeverity.Moderate, "Off is between -0.850V and -0.701V and difference in baseline is greater than 0.200V");
            if (off > -0.85)
                return (PGESeverity.Minor, "Off is between -0.850V and -0.701V");
            if (changeInBaseline >= 0.2)
                return (PGESeverity.Minor, "Difference in baseline is greater than 0.200V");
            return (PGESeverity.NRI, "");
        }

        private (PGESeverity, string) GetOnSeverity(double on, double baseline)
        {
            var changeInBaseline = Math.Abs(on - baseline);
            if (on > -0.6)
                return (PGESeverity.Severe, "On is more positive than -0.600");
            if (on > -0.8 && changeInBaseline >= 0.2)
                return (PGESeverity.Severe, "On is between -0.800 and -0.601 and difference in baseline is greater than 0.200");
            if (on > -0.8)
                return (PGESeverity.Moderate, "On is between -0.800 and -0.600");
            if (on > -0.95 && changeInBaseline >= 0.2)
                return (PGESeverity.Moderate, "On is between -0.950 and -0.801 and difference in baseline is greater than 0.200");
            if (on > -0.95)
                return (PGESeverity.Minor, "On is between -0.950 and -0.801");
            if (changeInBaseline >= 0.2)
                return (PGESeverity.Minor, "difference in baseline is greater than 0.200");
            return (PGESeverity.NRI, "");
        }

        private bool Within100(double footage, double checkFootage)
        {
            return Math.Abs(footage - checkFootage) <= 100;
        }

        public override List<(double Start, double End, Color Color)> GetColorBounds(PageInformation page)
        {
            if (Hca != null)
                return GetColorBoundsUpdated(page);
            return GetColorBoundsOld(page);
        }

        private List<(double Start, double End, Color Color)> GetColorBoundsUpdated(PageInformation page)
        {
            var colors = new List<(double Start, double End, Color Color)>();
            (double Start, double end, PGESeverity Severity)? prevData = null;
            (double Footage, PGESeverity Severity)? firstData = null;
            (double Footage, PGESeverity Severity)? lastData = null;
            for (var i = 0; i < DataUpdated.Count; ++i)
            {
                var curFoot = DataUpdated[i].Footage;
                var severity = DataUpdated[i].Severity;
                if (curFoot < page.StartFootage)
                {
                    firstData = (curFoot, severity);
                    continue;
                }
                if (curFoot > page.EndFootage)
                {
                    lastData = (curFoot, severity);
                    break;
                }
                if (!prevData.HasValue)
                {
                    prevData = (curFoot, curFoot, severity);
                    if (curFoot != page.StartFootage && firstData.HasValue)
                    {
                        var (firstFoot, firstColor) = firstData.Value;
                        if (firstColor == severity)
                        {
                            prevData = (firstFoot, curFoot, severity);
                        }
                        else
                        {
                            var color = GetColor(severity);
                            if (color.HasValue)
                                colors.Add((firstFoot, curFoot, color.Value));
                        }
                    }
                    continue;
                }

                var (prevStart, prevEnd, prevSeverity) = prevData.Value;
                var withinSkipDist = curFoot - prevEnd <= SkipDistance;
                if (severity == prevSeverity && withinSkipDist)
                {
                    prevData = (prevStart, curFoot, prevSeverity);
                }
                else
                {
                    var middleFoot = (prevEnd + curFoot) / 2;
                    var prevColor = GetColor(prevSeverity);
                    if (prevColor.HasValue)
                    {
                        if (withinSkipDist)
                            colors.Add((prevStart, middleFoot, prevColor.Value));
                        else
                            colors.Add((prevStart, prevEnd, prevColor.Value));
                    }
                    if (withinSkipDist)
                        prevData = (middleFoot, curFoot, severity);
                    else
                        prevData = (curFoot, curFoot, severity);
                }
            }
            if (prevData.HasValue)
            {
                var (prevStart, prevEnd, prevSeverity) = prevData.Value;
                if (lastData.HasValue)
                {
                    var (lastFoot, lastSeverity) = lastData.Value;
                    if (prevSeverity == lastSeverity)
                    {
                        prevEnd = lastFoot;
                    }
                    else
                    {
                        var color = GetColor(lastSeverity);
                        if (color.HasValue)
                            colors.Add((prevEnd, lastFoot, color.Value));
                    }
                }
                var prevColor = GetColor(prevSeverity);
                if (prevColor.HasValue)
                    colors.Add((prevStart, prevEnd, prevColor.Value));
            }

            return colors;
        }

        private List<(double Start, double End, Color Color)> GetColorBoundsOld(PageInformation page)
        {
            var colors = new List<(double Start, double End, Color Color)>();
            (double Start, double end, PGESeverity Severity)? prevData = null;
            (double Footage, PGESeverity Severity)? firstData = null;
            (double Footage, PGESeverity Severity)? lastData = null;
            for (var i = 0; i < Data.Count; ++i)
            {
                //var (curFoot, _, _, _, _, _, _, _, _, _, severity, _) = Data[i];
                var curFoot = Data[i].Footage;
                var severity = Data[i].Severity;
                if (curFoot < page.StartFootage)
                {
                    firstData = (curFoot, severity);
                    continue;
                }
                if (curFoot > page.EndFootage)
                {
                    lastData = (curFoot, severity);
                    break;
                }
                if (!prevData.HasValue)
                {
                    prevData = (curFoot, curFoot, severity);
                    if (curFoot != page.StartFootage && firstData.HasValue)
                    {
                        var (firstFoot, firstColor) = firstData.Value;
                        if (firstColor == severity)
                        {
                            prevData = (firstFoot, curFoot, severity);
                        }
                        else
                        {
                            var color = GetColor(severity);
                            if (color.HasValue)
                                colors.Add((firstFoot, curFoot, color.Value));
                        }
                    }
                    continue;
                }

                var (prevStart, prevEnd, prevSeverity) = prevData.Value;
                var withinSkipDist = curFoot - prevEnd <= SkipDistance;
                if (severity == prevSeverity && withinSkipDist)
                {
                    prevData = (prevStart, curFoot, prevSeverity);
                }
                else
                {
                    var middleFoot = (prevEnd + curFoot) / 2;
                    var prevColor = GetColor(prevSeverity);
                    if (prevColor.HasValue)
                    {
                        if (withinSkipDist)
                            colors.Add((prevStart, middleFoot, prevColor.Value));
                        else
                            colors.Add((prevStart, prevEnd, prevColor.Value));
                    }
                    if (withinSkipDist)
                        prevData = (middleFoot, curFoot, severity);
                    else
                        prevData = (curFoot, curFoot, severity);
                }
            }
            if (prevData.HasValue)
            {
                var (prevStart, prevEnd, prevSeverity) = prevData.Value;
                if (lastData.HasValue)
                {
                    var (lastFoot, lastSeverity) = lastData.Value;
                    if (prevSeverity == lastSeverity)
                    {
                        prevEnd = lastFoot;
                    }
                    else
                    {
                        var color = GetColor(lastSeverity);
                        if (color.HasValue)
                            colors.Add((prevEnd, lastFoot, color.Value));
                    }
                }
                var prevColor = GetColor(prevSeverity);
                if (prevColor.HasValue)
                    colors.Add((prevStart, prevEnd, prevColor.Value));
            }

            return colors;
        }

        private Color? GetColor(PGESeverity severity)
        {
            switch (severity)
            {
                case PGESeverity.NRI:
                    return null;
                case PGESeverity.Minor:
                    return MinorColor;
                case PGESeverity.Moderate:
                    return ModerateColor;
                default:
                    return SevereColor;
            }
        }

        protected override List<(string, Color)> LegendInfo()
        {
            return new List<(string, Color)>()
            {
                ("Minor", MinorColor),
                ("Moderate", ModerateColor),
                ("Severe", SevereColor)
            };
        }
    }

    public enum PGESeverity : int
    {
        NRI = 0,
        Minor,
        Moderate,
        Severe
    }
}
