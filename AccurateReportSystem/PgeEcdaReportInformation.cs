using AccurateFileSystem;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Devices.Geolocation;

namespace AccurateReportSystem
{
    public class PgeEcdaReportInformation
    {
        public CombinedAllegroCISFile CisFile { get; set; }
        public List<PgeEcdaDataPoint> EcdaData { get; set; }
        public HcaInfo HcaInfo { get; set; }
        public bool IsDcvg { get; private set; }
        public bool IsAcvg => !IsDcvg;
        public bool UseMir { get; set; }
        public double MaxSpacing { get; set; }

        public class PgeEcdaDataPoint
        {
            public double Footage { get; set; }
            public double? Depth { get; set; }
            public BasicGeoposition CisGps { get; set; }
            public BasicGeoposition? IndicationGps { get; set; } = null;
            public bool IsCisExtrapolated { get; set; }
            public bool IsCisSkipped { get; set; }
            public bool IsDcvg { get; set; }
            public double On { get; set; }
            public double Off { get; set; }
            public double Baseline { get; set; } = double.NaN;
            public double IndicationValue { get; set; } = double.NaN;
            public string Region { get; set; }

            public PgeEcdaDataPoint(double footage, double on, double off, double? depth, bool isSkipped, bool isExtrapolated, BasicGeoposition gps, bool isDcvg, string region)
            {
                Footage = footage;
                On = on;
                Off = off;
                Depth = depth;
                IsCisSkipped = isSkipped;
                IsCisExtrapolated = isExtrapolated;
                CisGps = gps;
                IsDcvg = isDcvg;
                Region = region;
            }

            public int Severity
            {
                get
                {
                    if (CisSeverity == PGESeverity.Moderate && IndicationSeverity == PGESeverity.Severe)
                        return 1;
                    if (CisSeverity == PGESeverity.Severe && (IndicationSeverity == PGESeverity.Severe || IndicationSeverity == PGESeverity.Moderate))
                        return 1;

                    if (CisSeverity == PGESeverity.NRI && IndicationSeverity == PGESeverity.Severe)
                        return 2;
                    if (CisSeverity == PGESeverity.Minor && IndicationSeverity == PGESeverity.Severe)
                        return 2;
                    if (CisSeverity == PGESeverity.Moderate && (IndicationSeverity == PGESeverity.Moderate || IndicationSeverity == PGESeverity.Minor))
                        return 2;
                    if (CisSeverity == PGESeverity.Severe && (IndicationSeverity == PGESeverity.Minor || IndicationSeverity == PGESeverity.NRI))
                        return 2;

                    if (CisSeverity == PGESeverity.NRI && IndicationSeverity == PGESeverity.Moderate)
                        return 3;
                    if (CisSeverity == PGESeverity.Minor)
                        return 3;
                    if (CisSeverity == PGESeverity.Moderate && IndicationSeverity == PGESeverity.NRI)
                        return 3;

                    return 4;
                }
            }
            public string Priority
            {
                get
                {
                    switch (Severity)
                    {
                        case 1:
                            return "Priority I";
                        case 2:
                            return "Priority II";
                        case 3:
                            return "Priority III";
                        case 4:
                            return "Priority IV";
                        default:
                            throw new Exception();
                    }
                }
            }
            public PGESeverity CisSeverity
            {
                get
                {
                    return GetCisSeverity().Item1;
                }
            }
            public string CisReason
            {
                get
                {
                    return GetCisSeverity().Item2;
                }
            }
            public PGESeverity IndicationSeverity
            {
                get
                {
                    return GetIndicationSeverity().Item1;
                }
            }
            public string IndicationReason
            {
                get
                {
                    return GetIndicationSeverity().Item2;
                }
            }

            private (PGESeverity, string) GetCisSeverity()
            {
                if (IsCisSkipped && Region.Contains("3"))
                    return (PGESeverity.NRI, "Casing");
                if (IsCisSkipped)
                    return (PGESeverity.NRI, "Skip");

                var changeInBaseline = Math.Abs(Off - Baseline);
                if (Off > -0.5)
                    return (PGESeverity.Severe, "Off is less than -0.500");
                if (Off > -0.7 && changeInBaseline >= 0.2)
                    return (PGESeverity.Severe, "Off is between -0.700 and -0.501 and difference in baseline is greater than 0.200");
                if (Off > -0.7)
                    return (PGESeverity.Moderate, "Off is between -0.700 and -0.501");
                if (Off > -0.85 && changeInBaseline >= 0.2)
                    return (PGESeverity.Moderate, "Off is between -0.850 and -0.701 and difference in baseline is greater than 0.200");
                if (Off > -0.85)
                    return (PGESeverity.Minor, "Off is between -0.850 and -0.701");
                if (changeInBaseline >= 0.2)
                    return (PGESeverity.Minor, "difference in baseline is greater than 0.200");
                return (PGESeverity.NRI, "");
            }

            private (PGESeverity, string) GetIndicationSeverity()
            {
                if (IsCisSkipped)
                    return (PGESeverity.NRI, "Skip");
                if(double.IsNaN(IndicationValue))
                    return (PGESeverity.NRI, "");
                if (!IsDcvg)
                {
                    //ACVG
                    var severity = PGESeverity.NRI;
                    var reason = "Individual normalized ACVG indications are less than 25";
                    if (IndicationValue >= 75)
                    {
                        severity = PGESeverity.Severe;
                        reason = "Individual normalized ACVG indications are greater than or equal to 75";
                    }
                    else if (IndicationValue >= 50)
                    {
                        severity = PGESeverity.Moderate;
                        reason = "Individual normalized ACVG indications are greater than or equal to 50 and less than 75";
                    }
                    else if (IndicationValue >= 25)
                    {
                        severity = PGESeverity.Minor;
                        reason = "Individual normalized ACVG indications are greater than or equal to 25 and less than 50";
                    }
                    return (severity, reason);
                }
                else
                {
                    //DCVG
                    var severity = PGESeverity.NRI;
                    var reason = "DCVG % IR is greater than 0 and less than or equal to 15";
                    if (IndicationValue > 60)
                    {
                        severity = PGESeverity.Severe;
                        reason = "DCVG % IR is greater than 60";
                    }
                    else if (IndicationValue > 35)
                    {
                        severity = PGESeverity.Moderate;
                        reason = "DCVG % IR is greater than 35 and less than or equal to 60";
                    }
                    else if (IndicationValue > 15)
                    {
                        severity = PGESeverity.Minor;
                        reason = "DCVG % IR is greater than 15 and less than or equal to 35";
                    }
                    return (severity, reason);
                }
            }

            public bool IsEquivalent(PgeEcdaDataPoint other)
            {
                if (CisSeverity != other.CisSeverity)
                    return false;
                if (CisReason != other.CisReason)
                    return false;

                if (IndicationSeverity != other.IndicationSeverity)
                    return false;
                if (IndicationReason != other.IndicationReason)
                    return false;

                if (Region != other.Region)
                    return false;

                return true;
            }
        }

        public PgeEcdaReportInformation(CombinedAllegroCISFile cisFile, List<AllegroCISFile> dcvgFiles, HcaInfo hcaInfo, double maxSpacing, bool useMir = false)
        {
            MaxSpacing = maxSpacing;
            IsDcvg = true;
            UseMir = useMir;
            CisFile = cisFile;
            HcaInfo = hcaInfo;
            ExtrapolateCisData();

            foreach (var file in dcvgFiles)
            {
                AllegroDataPoint lastGpsPoint = null;
                foreach (var (_, point) in file.Points)
                {
                    if (point.HasIndication)
                    {
                        var gps = point.GPS;
                        if (!point.HasGPS)
                        {
                            gps = lastGpsPoint.GPS;
                        }
                        var closestDistance = double.MaxValue;
                        PgeEcdaDataPoint closestPoint = null;
                        foreach (var surveyPoint in EcdaData)
                        {
                            if (surveyPoint.IsCisSkipped)
                                continue;
                            var curDistance = surveyPoint.CisGps.Distance(gps);
                            if (curDistance < closestDistance)
                            {
                                closestDistance = curDistance;
                                closestPoint = surveyPoint;
                            }
                        }
                        if (closestPoint == null)
                            throw new Exception();
                        closestPoint.IndicationValue = point.IndicationPercent;
                        closestPoint.IndicationGps = gps;
                    }
                    if (point.HasGPS)
                        lastGpsPoint = point;
                }
            }
        }

        public PgeEcdaReportInformation(CombinedAllegroCISFile cisFile, List<(BasicGeoposition, double)> acvgIndications, HcaInfo hcaInfo, double maxSpacing, bool useMir = false)
        {
            MaxSpacing = maxSpacing;
            IsDcvg = false;
            UseMir = useMir;
            CisFile = cisFile;
            HcaInfo = hcaInfo;
            ExtrapolateCisData();

            foreach (var (gps, value) in acvgIndications)
            {
                var closestDistance = double.MaxValue;
                PgeEcdaDataPoint closestPoint = null;
                foreach (var surveyPoint in EcdaData)
                {
                    if (surveyPoint.IsCisSkipped)
                        continue;
                    var curDistance = surveyPoint.CisGps.Distance(gps);
                    if (curDistance < closestDistance)
                    {
                        closestDistance = curDistance;
                        closestPoint = surveyPoint;
                    }
                }
                if (closestPoint == null)
                    throw new Exception();
                closestPoint.IndicationValue = value;
                closestPoint.IndicationGps = gps;
            }
        }

        public List<(double, double, BasicGeoposition)> GetIndicationData()
        {
            var output = new List<(double, double, BasicGeoposition)>();
            foreach(var point in EcdaData)
            {
                if(!double.IsNaN(point.IndicationValue))
                {
                    output.Add((point.Footage, point.IndicationValue, point.IndicationGps.Value));
                }
            }
            return output;
        }

        private void ExtrapolateCisData()
        {
            var startGps = HcaInfo.Regions[0].Start;
            int start = 0;
            int end = CisFile.Points.Count - 1;
            var startPoint = CisFile.Points[start];
            while (!startPoint.Point.HasGPS)
            {
                ++start;
                startPoint = CisFile.Points[start];
            }
            var endPoint = CisFile.Points[end];
            while (!endPoint.Point.HasGPS)
            {
                --end;
                endPoint = CisFile.Points[end];
            }
            var startDist = startPoint.Point.GPS.Distance(startGps);
            var endDist = endPoint.Point.GPS.Distance(startGps);
            if (endDist < startDist)
            {
                if (HcaInfo.HcaId.Contains("51"))
                    CisFile = CisFile;
                CisFile.Reverse();
            }

            double lastFootage = double.NaN;
            AllegroDataPoint lastPoint = null;
            EcdaData = new List<PgeEcdaDataPoint>();
            double? lastDepth = null;
            foreach (var (curFootage, _, curPoint, _, _) in CisFile.Points)
            {
                if (curPoint.Depth.HasValue)
                {
                    lastDepth = curPoint.Depth.Value;
                    break;
                }
            }
            foreach (var (curFootage, _, curPoint, _, _) in CisFile.Points)
            {
                if (!curPoint.HasGPS)
                    throw new Exception();
                var curOn = UseMir ? curPoint.MirOn : curPoint.On;
                var curOff = UseMir ? curPoint.MirOff : curPoint.Off;
                var curGps = curPoint.GPS;
                var curDepth = curPoint.Depth;
                if (!curDepth.HasValue)
                    curDepth = lastDepth;
                var newPoint = new PgeEcdaDataPoint(curFootage, curOn, curOff, curDepth, false, false, curGps, IsDcvg, HcaInfo.ClosestRegion(curGps));
                EcdaData.Add(newPoint);

                if (lastPoint == null)
                {
                    lastFootage = curFootage;
                    lastPoint = curPoint;
                    continue;
                }

                var lastOn = UseMir ? curPoint.MirOn : curPoint.On;
                var lastOff = UseMir ? curPoint.MirOff : curPoint.Off;
                var lastGps = lastPoint.GPS;

                var footDist = curFootage - lastFootage;

                var latFactor = (curGps.Latitude - lastGps.Latitude) / footDist;
                var lonFactor = (curGps.Longitude - lastGps.Longitude) / footDist;
                var onFactor = (curOn - lastOn) / footDist;
                double? depthFactor = curDepth.HasValue ? ((curDepth.Value - lastDepth.Value) / footDist) : (double?)null;
                var offFactor = (curOff - lastOff) / footDist;
                var isSkipped = footDist > MaxSpacing;

                for (int j = 1; j < footDist; ++j)
                {
                    var fakeGps = new BasicGeoposition()
                    {
                        Latitude = lastGps.Latitude + latFactor * j,
                        Longitude = lastGps.Longitude + lonFactor * j
                    };
                    var fakeFoot = lastFootage + j;
                    var fakeOn = lastOn + onFactor * j;
                    var fakeOff = lastOff + offFactor * j;
                    var fakeDepth = depthFactor.HasValue ? lastDepth.Value + depthFactor.Value * j : (double?)null;
                    newPoint = new PgeEcdaDataPoint(fakeFoot, fakeOn, fakeOff, fakeDepth, isSkipped, true, fakeGps, IsDcvg, HcaInfo.ClosestRegion(fakeGps));
                    EcdaData.Add(newPoint);
                }

                lastFootage = curFootage;
                lastPoint = curPoint;
            }

            EcdaData.Sort((first, second) => first.Footage.CompareTo(second.Footage));

            CalcualteBaselines();
        }

        private bool Within100(double footage, double checkFootage)
        {
            return Math.Abs(footage - checkFootage) <= 100;
        }

        private void CalcualteBaselines()
        {
            var curAverages = new List<double>(EcdaData.Count);
            for (int i = 0; i < EcdaData.Count; ++i)
            {
                var point = EcdaData[i];
                var within100 = EcdaData.Where(value => Within100(point.Footage, value.Footage) && !value.IsCisSkipped);
                var average = (within100.Count() != 0 ? within100.Average(value => value.Off) : point.Off);
                curAverages.Add(average);
            }
            var curBaselines = Enumerable.Repeat(double.NaN, EcdaData.Count).ToList();
            for (int center = 0; center < EcdaData.Count; ++center)
            {
                var start = Math.Max(center - 105, 0);
                var end = Math.Min(center + 105, EcdaData.Count - 1);
                var centerPoint = EcdaData[center];
                var centerAverage = curAverages[center];
                for (int i = start; i <= end; ++i)
                {
                    var curPoint = EcdaData[i];
                    var curAverage = curAverages[i];
                    if (Within100(centerPoint.Footage, curPoint.Footage))
                    {
                        var curBaseline = curBaselines[center];
                        if (double.IsNaN(curBaseline))
                        {
                            curBaselines[center] = curAverage;
                            continue;
                        }
                        var diffFromBaseline = Math.Abs(centerPoint.Off - curAverage);
                        var diffFromCurBaseline = Math.Abs(centerPoint.Off - curBaselines[center]);
                        if (diffFromBaseline > diffFromCurBaseline)
                        {
                            curBaselines[center] = curAverage;
                        }
                    }
                }
                var baseline = curBaselines[center];
            }
        }

        public string GetReportQ()
        {
            var output = new StringBuilder();

            var areas = new List<ReportQArea>();
            var curArea = new ReportQArea(EcdaData.First());
            for (int i = 1; i < EcdaData.Count; ++i)
            {
                var curPoint = EcdaData[i];
                curArea.End = curPoint;
                if (!curArea.IsEquivalent(curPoint))
                {
                    areas.Add(curArea);
                    curArea = new ReportQArea(curPoint);
                }
            }
            areas.Add(curArea);
            var hcaInfo = $"{HcaInfo.HcaId}\t{HcaInfo.Route}\t{HcaInfo.StartMilepost}\t{HcaInfo.EndMilepost}\t";
            foreach (var area in areas)
            {
                output.Append(hcaInfo);
                output.Append($"{ToStationing(area.Start.Footage)}\t");
                output.Append($"{ToStationing(area.End.Footage)}\t");
                output.Append($"{area.Start.Region}\t");
                var dist = area.End.Footage - area.Start.Footage;
                output.Append($"{dist.ToString("F0")}\t");
                var depthString = area.MinDepth.HasValue ? area.MinDepth.Value.ToString("F0") : "N/A";
                output.Append($"{depthString}\t");
                output.Append($"{area.Start.CisGps.Latitude.ToString("F8")}\t");
                output.Append($"{area.Start.CisGps.Longitude.ToString("F8")}\t");
                output.Append($"{area.End.CisGps.Latitude.ToString("F8")}\t");
                output.Append($"{area.End.CisGps.Longitude.ToString("F8")}\t");

                output.Append($"{area.Start.CisSeverity.ToString()}\t");
                output.Append($"{area.Start.IndicationSeverity.ToString()}\t");
                output.Append($"{area.Start.Priority}\t");
                var reason = area.Start.CisReason + " " + area.Start.IndicationReason;
                output.AppendLine($"{reason.Trim()}\t");
            }
            return output.ToString();
        }

        private string ToStationing(double footage)
        {
            int hundred = (int)footage / 100;
            int tens = (int)footage % 100;
            return hundred.ToString().PadLeft(1, '0') + "+" + tens.ToString().PadLeft(2, '0');
        }

        public struct ReportQArea
        {
            public PgeEcdaDataPoint Start { get; set; }
            private PgeEcdaDataPoint end;
            public PgeEcdaDataPoint End
            {
                get { return end; }
                set
                {
                    CompareDepth(value.Depth);
                    end = value;
                }
            }
            public double? MinDepth { get; set; }

            public ReportQArea(PgeEcdaDataPoint point)
            {
                Start = point;
                end = point;
                MinDepth = point.Depth;
            }

            public void CompareDepth(double? newDepth)
            {
                if (newDepth.HasValue)
                {
                    if ((MinDepth ?? double.MaxValue) > newDepth.Value)
                        MinDepth = newDepth;
                }
            }

            public bool IsEquivalent(PgeEcdaDataPoint other)
            {
                return Start.IsEquivalent(other);
            }
        }
    }



    public struct HcaInfo
    {
        public string HcaId;
        public string Route;
        public string StartMilepost;
        public string EndMilepost;
        public List<RegionInfo> Regions;

        public string ClosestRegion(BasicGeoposition gps)
        {
            var closestDistance = double.MaxValue;
            var closestRegion = "";
            foreach (var regionInfo in Regions)
            {
                var curDist = gps.DistanceToSegment(regionInfo.Start, regionInfo.End);
                if (curDist < closestDistance)
                {
                    closestDistance = curDist;
                    closestRegion = regionInfo.Name;
                }
            }
            return closestRegion;
        }
    }

    public struct RegionInfo
    {
        public BasicGeoposition Start;
        public BasicGeoposition End;
        public string Name;
    }
}
