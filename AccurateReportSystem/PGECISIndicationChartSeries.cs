using AccurateFileSystem;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Devices.Geolocation;
using Windows.UI;

namespace AccurateReportSystem
{
    public class PGECISIndicationChartSeries : ExceptionsChartSeries
    {
        public override int NumberOfValues => 3;
        public Color MinorColor { get; set; } = Colors.Blue;
        public Color ModerateColor { get; set; } = Colors.Green;
        public Color SevereColor { get; set; } = Colors.Red;
        public List<(double Footage, double On, double Off, string Comment, DateTime Date, double? Depth, bool IsExtrapolated, double Baseline, BasicGeoposition Gps, string Region, PGESeverity Severity, string Reason)> Data { get; set; }
        public double SkipDistance { get; set; } = 10;
        public List<(double Footage, AllegroDataPoint Point)> RawData { get; set; }
        public List<(BasicGeoposition Start, BasicGeoposition End, string Region)> EcdaRegions { get; set; }

        public PGECISIndicationChartSeries(List<(double, AllegroDataPoint)> data, Chart chart, List<(BasicGeoposition Start, BasicGeoposition End, string Region)> regions) : base(chart.LegendInfo, chart.YAxesInfo)
        {
            RawData = data;
            EcdaRegions = regions;
            Data = ExtrapolateData(data);
        }

        public PGECISIndicationChartSeries(List<(double, AllegroDataPoint)> data, LegendInfo masterLegendInfo, YAxesInfo masterYAxesInfo) : base(masterLegendInfo, masterYAxesInfo)
        {
            RawData = data;
            Data = ExtrapolateData(data);
        }

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
                var curDistance = gps.DistanceToSegment(start, end);
                if (curDistance < closestDist)
                {
                    closestDist = curDistance;
                    closestRegion = region;
                }
            }
            return closestRegion;
        }

        private List<(double Footage, double On, double Off, string Comment, DateTime Date, double? Depth, bool IsExtrapolated, double Baseline, BasicGeoposition Gps, string Region, PGESeverity Severity, string Reason)> ExtrapolateData(List<(double Footage, AllegroDataPoint Point)> data)
        {
            var extrapolatedData = new List<(double Footage, double On, double Off, string Comment, DateTime Date, double? Depth, BasicGeoposition GpsPoint, string Region, bool IsExtrapolated, bool isSkipped)>();
            string curRegion;
            for (int i = 0; i < data.Count - 1; ++i)
            {
                var (startFoot, startPoint) = data[i];
                var (endFoot, endPoint) = data[i + 1];
                curRegion = GetClosestRegion(startPoint.GPS);
                extrapolatedData.Add((startFoot, startPoint.MirOn, startPoint.MirOff, startPoint.OriginalComment, startPoint.Times[0], startPoint.Depth, startPoint.GPS, curRegion, false, false));
                var dist = endFoot - startFoot;

                var onDiff = endPoint.MirOn - startPoint.MirOn;
                var offDiff = endPoint.MirOff - startPoint.MirOff;
                var onPerFoot = onDiff / dist;
                var offPerFoot = offDiff / dist;

                var latDiff = endPoint.GPS.Latitude - startPoint.GPS.Latitude;
                var latPerFoot = latDiff / dist;

                var longDiff = endPoint.GPS.Longitude - startPoint.GPS.Longitude;
                var longPerFoot = longDiff / dist;

                for (int offset = 1; offset < dist; ++offset)
                {
                    var newOn = onPerFoot * offset + startPoint.MirOn;
                    var newOff = offPerFoot * offset + startPoint.MirOff;
                    var newFoot = startFoot + offset;

                    var newLat = latPerFoot * offset + startPoint.GPS.Latitude;
                    var newLong = longPerFoot * offset + startPoint.GPS.Longitude;
                    var newGps = new BasicGeoposition() { Latitude = newLat, Longitude = newLong };

                    extrapolatedData.Add((newFoot, newOn, newOff, startPoint.OriginalComment, startPoint.Times[0], startPoint.Depth, newGps, curRegion, true, dist > SkipDistance));
                }
            }
            var (foot, point) = data.Last();
            curRegion = GetClosestRegion(point.GPS);
            extrapolatedData.Add((foot, point.MirOn, point.MirOff, point.OriginalComment, point.Times[0], point.Depth, point.GPS, curRegion, false, false));
            bool extrapolated, skipped;
            BasicGeoposition gps;
            double on, off;
            double? depth;
            string comment;
            DateTime date;
            var output = new List<(double, double, double, string, DateTime, double?, bool, double, BasicGeoposition, string, PGESeverity, string)>(extrapolatedData.Count);
            if (foot <= 200)
            {
                var average = extrapolatedData.Average(value => value.Off);
                for (int i = 0; i < extrapolatedData.Count; ++i)
                {
                    (foot, on, off, comment, date, depth, gps, curRegion, extrapolated, skipped) = extrapolatedData[i];
                    var changeInBaseline = Math.Abs(off - average);
                    var (severity, reason) = GetSeverity(off, average);
                    if (skipped)
                    {
                        severity = PGESeverity.NRI;
                        reason = "SKIP";
                    }
                    output.Add((foot, on, off, comment, date, depth, extrapolated, average, gps, curRegion, severity, reason));
                }
                return output;
            }
            var curAverages = new List<double>(extrapolatedData.Count);
            for (int i = 0; i < extrapolatedData.Count; ++i)
            {
                (foot, on, off, _, _, _, _, _, extrapolated, skipped) = extrapolatedData[i];
                var within100 = extrapolatedData.Where(value => Within100(foot, value.Footage) && !value.isSkipped);
                var average = (within100.Count() != 0 ? within100.Average(value => value.Off) : off);
                curAverages.Add(average);
            }
            var curBaselines = Enumerable.Repeat(double.NaN, extrapolatedData.Count).ToList();
            for (int center = 0; center < extrapolatedData.Count; ++center)
            {
                var start = Math.Max(center - 105, 0);
                var end = Math.Min(center + 105, extrapolatedData.Count - 1);
                var (centerFoot, centerOn, centerOff, centerComment, centerDate, centerDepth, centerGps, centerRegion, centerExtrapolated, centerSkipped) = extrapolatedData[center];
                var centerAverage = curAverages[center];
                for (int i = start; i <= end; ++i)
                {
                    var (curFoot, _, _, _, _, _, _, _, _, _) = extrapolatedData[i];
                    var curAverage = curAverages[i];
                    if (Within100(centerFoot, curFoot))
                    {
                        var curBaseline = curBaselines[center];
                        if (double.IsNaN(curBaseline))
                        {
                            curBaselines[center] = curAverage;
                            continue;
                        }
                        var diffFromBaseline = Math.Abs(centerOff - curAverage);
                        var diffFromCurBaseline = Math.Abs(centerOff - curBaselines[center]);
                        if (diffFromBaseline > diffFromCurBaseline)
                        {
                            curBaselines[center] = curAverage;
                        }
                    }
                }
                var baseline = curBaselines[center];
                var (severity, reason) = GetSeverity(centerOff, baseline);
                if (centerSkipped)
                {
                    severity = PGESeverity.NRI;
                    reason = "SKIP";
                }
                output.Add((centerFoot, centerOn, centerOff, centerComment, centerDate, centerDepth, centerExtrapolated, baseline, centerGps, centerRegion, severity, reason));
            }
            return output;
        }

        private (PGESeverity, string) GetSeverity(double off, double baseline)
        {
            var changeInBaseline = Math.Abs(off - baseline);
            if (off > -0.5)
                return (PGESeverity.Severe, "Off is less than -0.500");
            if (off > -0.7 && changeInBaseline >= 0.2)
                return (PGESeverity.Severe, "Off is between -0.700 and -0.501 and difference in baseline is greater than 0.200");
            if (off > -0.7)
                return (PGESeverity.Moderate, "Off is between -0.700 and -0.501");
            if (off > -0.85 && changeInBaseline >= 0.2)
                return (PGESeverity.Moderate, "Off is between -0.850 and -0.701 and difference in baseline is greater than 0.200");
            if (off > -0.85)
                return (PGESeverity.Minor, "Off is between -0.850 and -0.701");
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
            var colors = new List<(double Start, double End, Color Color)>();
            (double Start, double end, PGESeverity Severity)? prevData = null;
            (double Footage, PGESeverity Severity)? firstData = null;
            (double Footage, PGESeverity Severity)? lastData = null;
            for (int i = 0; i < Data.Count; ++i)
            {
                var (curFoot, _, _, _, _, _, _, _, _, _, severity, _) = Data[i];
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
