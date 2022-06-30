using AccurateFileSystem;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using Windows.Devices.Geolocation;
using Windows.Storage;

namespace AccurateReportSystem
{
    public class PgeEcdaReportInformation
    {
        public CombinedAllegroCISFile CisFile { get; set; }
        public List<PgeEcdaDataPoint> EcdaData { get; set; }
        public HcaInfo HcaInfo { get; set; }
        public Hca Hca { get; set; }
        public bool IsDcvg { get; private set; }
        public bool IsAcvg => !IsDcvg;
        public bool UseMir { get; set; }
        public double MaxSpacing { get; set; }
        public GpsInfo? GpsInfo { get; set; }

        public List<int> GetActualReadFootage()
        {
            var output = Enumerable.Repeat(0, 5).ToList();
            foreach (var point in EcdaData)
            {
                if (point.IsCisSkipped || point.Footage == 0) continue;
                output[0] += 1;
                output[point.Severity] += 1;
            }
            return output;
        }

        public class PgeEcdaDataPoint
        {
            public double Footage { get; set; }
            public double? Depth { get; set; }
            public double? AmpValue { get; set; } = null;
            public double? AmpPercent { get; set; } = null;
            public BasicGeoposition? AmpGps { get; set; } = null;
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
            public HcaRegion RegionUpdated { get; set; }

            public PgeEcdaDataPoint(double footage, double on, double off, double? depth, bool isSkipped, bool isExtrapolated, BasicGeoposition gps, bool isDcvg, string region, HcaRegion regionUpdated = null)
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
                RegionUpdated = regionUpdated;
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
                if (IsCisSkipped && Region.Contains("7"))
                    return (PGESeverity.NRI, "Atmospheric");
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
                if (double.IsNaN(IndicationValue))
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

            public override string ToString()
            {
                return $"{Footage} {Region}{(IsCisSkipped ? "Skipped" : "")}";
            }
        }

        public PgeEcdaReportInformation(CombinedAllegroCISFile cisFile, List<AllegroCISFile> dcvgFiles, List<(double Footage, BasicGeoposition Gps, double Value, double Percent)> ampReads, Hca hca, double maxSpacing, bool useMir = false, GpsInfo? gpsInfo = null)
        {
            GpsInfo = gpsInfo;
            MaxSpacing = maxSpacing;
            IsDcvg = true;
            UseMir = useMir;
            CisFile = cisFile;
            Hca = hca;
            ExtrapolateCisDataUpdated();
            AlignDcvgIndications(dcvgFiles);
            AlignAmpReads(ampReads);
        }

        private void AlignDcvgIndications(List<AllegroCISFile> dcvgFiles)
        {
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
                        var middleGps = closestPoint.CisGps.MiddleTowards(gps);
                        closestPoint.IndicationGps = middleGps;
                    }
                    if (point.HasGPS)
                        lastGpsPoint = point;
                }
            }
        }

        private void AlignAmpReads(List<(double Footage, BasicGeoposition gps, double Value, double Percent)> pcmReads)
        {
            foreach (var (footage, gps, value, percent) in pcmReads)
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
                if (Math.Abs(closestPoint.Footage - footage) > 20)
                    continue;
                if (closestPoint.AmpPercent.HasValue)
                    continue;
                closestPoint.AmpPercent = percent;
                closestPoint.AmpValue = value;
                var middleGps = closestPoint.CisGps.MiddleTowards(gps);
                closestPoint.AmpGps = middleGps;
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

        public PgeEcdaReportInformation(CombinedAllegroCISFile cisFile, List<(BasicGeoposition, double)> acvgIndications, List<(double Footage, BasicGeoposition Gps, double Value, double Percent)> ampReads, Hca hca, double maxSpacing, bool useMir = false)
        {
            MaxSpacing = maxSpacing;
            IsDcvg = false;
            UseMir = useMir;
            CisFile = cisFile;
            Hca = hca;
            ExtrapolateCisDataUpdated();
            AlignAmpReads(ampReads);

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
            foreach (var point in EcdaData)
            {
                if (!double.IsNaN(point.IndicationValue))
                {
                    output.Add((point.Footage, point.IndicationValue, point.IndicationGps.Value));
                }
            }
            return output;
        }

        public void StraightenGps(CombinedAllegroCISFile file, GpsInfo info)
        {
            foreach (var point in file.Points)
            {
                var curPoint = point.Point;
                var region = HcaInfo.ClosestRegion(curPoint.GPS);
                if (region == "0" || region.ToLower() == "buffer")
                    continue;
                if ((!string.IsNullOrWhiteSpace(curPoint.OriginalComment) || curPoint.Depth.HasValue) && curPoint.HasGPS)
                {
                    var (newPoint, distance) = info.GetClosestGps(curPoint.GPS);
                    var lat = (curPoint.GPS.Latitude + (newPoint.Latitude * 9)) / 10;
                    var lon = (curPoint.GPS.Longitude + (newPoint.Longitude * 9)) / 10;
                    curPoint.GPS = new BasicGeoposition() { Latitude = lat, Longitude = lon };
                }
            }
            file.StraightenGps();
        }

        public void StraightenGpsUpdated(CombinedAllegroCISFile file, GpsInfo info)
        {
            foreach (var point in file.Points)
            {
                var curPoint = point.Point;
                var region = Hca.GetClosestRegion(curPoint.GPS);
                if (region.Name == "0")
                    continue;
                if ((!string.IsNullOrWhiteSpace(curPoint.OriginalComment) || curPoint.Depth.HasValue) && curPoint.HasGPS)
                {
                    var (newPoint, _) = info.GetClosestGps(curPoint.GPS);
                    var lat = (curPoint.GPS.Latitude + (newPoint.Latitude * 9)) / 10;
                    var lon = (curPoint.GPS.Longitude + (newPoint.Longitude * 9)) / 10;
                    curPoint.GPS = new BasicGeoposition() { Latitude = lat, Longitude = lon };
                }
            }
            file.StraightenGps();
        }

        private void ExtrapolateCisData()
        {
            var startGps = HcaInfo.Regions[0].Start;
            var start = 0;
            var end = CisFile.Points.Count - 1;
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
                CisFile.Reverse();
            }
            if (CisFile.Points.Count > 10)
            {
                if (GpsInfo == null)
                    CisFile.StraightenGps();
                else
                    StraightenGps(CisFile, GpsInfo.Value);
                CisFile.SetFootageFromGps();
            }
            var lastFootage = double.NaN;
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
                var depthFactor = curDepth.HasValue ? ((curDepth.Value - lastDepth.Value) / footDist) : (double?)null;
                var offFactor = (curOff - lastOff) / footDist;
                var isSkipped = footDist > MaxSpacing;

                for (var j = 1; j < footDist; ++j)
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

        public List<(double Footage, double Value)> GetOnData()
        {
            var output = new List<(double Footage, double Value)>();
            foreach (var point in EcdaData)
            {
                if (!point.IsCisExtrapolated && !point.IsCisSkipped)
                    output.Add((point.Footage, point.On));
            }
            return output;
        }

        public List<(double Footage, double Value)> GetOffData()
        {
            var output = new List<(double Footage, double Value)>();
            foreach (var point in EcdaData)
            {
                if (!point.IsCisExtrapolated && !point.IsCisSkipped)
                    output.Add((point.Footage, point.Off));
            }
            return output;
        }

        public List<(double Footage, double On, double Off)> GetOnOffData()
        {
            var output = new List<(double Footage, double On, double Off)>();
            foreach (var point in EcdaData)
            {
                if (!point.IsCisExtrapolated && !point.IsCisSkipped)
                    output.Add((point.Footage, point.On, point.Off));
            }
            return output;
        }

        public List<(double Footage, double Value)> GetAmpData()
        {
            var output = new List<(double Footage, double Value)>();
            foreach (var point in EcdaData)
            {
                if (point.AmpValue.HasValue)
                    output.Add((point.Footage, point.AmpValue.Value));
            }
            return output;
        }

        public List<(double Footage, BasicGeoposition Gps, double Value, double Percent)> GetFullAmpData()
        {
            var output = new List<(double Footage, BasicGeoposition Gps, double Value, double Percent)>();
            foreach (var point in EcdaData)
            {
                if (point.AmpValue.HasValue)
                    output.Add((point.Footage, point.AmpGps.Value, point.AmpValue.Value, point.AmpPercent.Value));
            }
            return output;
        }

        private void ExtrapolateCisDataUpdated()
        {
            var start = CisFile.HasStartSkip ? 1 : 0;
            var end = CisFile.Points.Count - (CisFile.HasEndSkip ? 2 : 1);
            var startFootage = CisFile.Points[start].Footage;
            var endFootage = CisFile.Points[end].Footage;
            var lastFootage = double.NaN;
            AllegroDataPoint lastPoint = null;
            EcdaData = new List<PgeEcdaDataPoint>();
            double? lastDepth = null;
            foreach (var (curFootage, _, curPoint, _, _) in CisFile.Points)
            {
                if (curFootage < startFootage || curFootage > endFootage)
                    continue;
                if (curPoint.Depth.HasValue)
                {
                    lastDepth = curPoint.Depth.Value;
                    break;
                }
            }
            foreach (var (curFootage, _, curPoint, _, _) in CisFile.Points)
            {
                if (curFootage < startFootage || curFootage > endFootage)
                    continue;
                if (!curPoint.HasGPS)
                    throw new Exception();
                var curOn = UseMir ? curPoint.MirOn : curPoint.On;
                var curOff = UseMir ? curPoint.MirOff : curPoint.Off;
                var curGps = curPoint.GPS;
                var curDepth = curPoint.Depth;
                if (!curDepth.HasValue)
                    curDepth = lastDepth;
                var closeRegion = Hca.GetClosestRegion(curGps);
                var newPoint = new PgeEcdaDataPoint(curFootage, curOn, curOff, curDepth, closeRegion.ShouldSkip, false, curGps, IsDcvg, closeRegion.ReportQName, closeRegion);
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
                var depthFactor = curDepth.HasValue ? ((curDepth.Value - lastDepth.Value) / footDist) : (double?)null;
                var offFactor = (curOff - lastOff) / footDist;
                var isSkipped = footDist > MaxSpacing || closeRegion.ShouldSkip;

                for (var j = 1; j < footDist; ++j)
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
                    closeRegion = Hca.GetClosestRegion(curGps);
                    newPoint = new PgeEcdaDataPoint(fakeFoot, fakeOn, fakeOff, fakeDepth, isSkipped, true, fakeGps, IsDcvg, closeRegion.ReportQName, closeRegion);
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
            for (var i = 0; i < EcdaData.Count; ++i)
            {
                var point = EcdaData[i];
                var within100 = EcdaData.Where(value => Within100(point.Footage, value.Footage) && !value.IsCisSkipped);
                var average = (within100.Count() != 0 ? within100.Average(value => value.Off) : point.Off);
                curAverages.Add(average);
            }
            var curBaselines = Enumerable.Repeat(double.NaN, EcdaData.Count).ToList();
            for (var center = 0; center < EcdaData.Count; ++center)
            {
                var start = Math.Max(center - 105, 0);
                var end = Math.Min(center + 105, EcdaData.Count - 1);
                var centerPoint = EcdaData[center];
                var centerAverage = curAverages[center];
                for (var i = start; i <= end; ++i)
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
            for (var i = 1; i < EcdaData.Count; ++i)
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
                if (area.Start.RegionUpdated != null)
                    output.Append($"{area.Start.RegionUpdated.Name}\t");
                else
                    output.Append($"{area.Start.Region}\t");
                var dist = area.End.Footage - area.Start.Footage;
                output.Append($"{dist.ToString("F0")}\t");
                var depthString = area.MinDepth.HasValue ? area.MinDepth.Value.ToString("F0") : "N/A";
                output.Append($"{depthString}\t");
                output.Append($"{area.Start.CisGps.Latitude:F8)}\t");
                output.Append($"{area.Start.CisGps.Longitude:F8)}\t");
                output.Append($"{area.End.CisGps.Latitude:F8)}\t");
                output.Append($"{area.End.CisGps.Longitude:F8)}\t");

                output.Append($"{area.Start.CisSeverity}\t");
                output.Append($"{area.Start.IndicationSeverity}\t");
                output.Append($"{area.Start.Priority}\t");
                var reason = area.Start.CisReason + " " + area.Start.IndicationReason;
                output.AppendLine($"{reason.Trim().Replace("..", ".")}\t");
            }
            return output.ToString();
        }

        private string ToStationing(double footage)
        {
            var hundred = (int)footage / 100;
            var tens = (int)footage % 100;
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
                var curDist = gps.DistanceToSegment(regionInfo.Start, regionInfo.End).Distance;
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

    public struct ScopeKml
    {
        public readonly Dictionary<string, GpsInfo> GpsInfos;

        public ScopeKml(Dictionary<string, List<GpsLine>> info)
        {
            GpsInfos = new Dictionary<string, GpsInfo>();
            foreach (var (name, lines) in info)
            {
                GpsInfos.Add(name, new GpsInfo(lines));
            }
        }

        public async static Task<ScopeKml> GetScopeKmlAsync(StorageFile file)
        {
            var buffer = await FileIO.ReadBufferAsync(file);
            using (var dataReader = Windows.Storage.Streams.DataReader.FromBuffer(buffer))
            {
                var text = dataReader.ReadString(buffer.Length);
                var xml = new XmlDocument();
                try
                {
                    xml.LoadXml(text);
                }
                catch (Exception e)
                {
                    Debug.WriteLine(e.Message);
                }

                var curNode = FirstNodeWithName(xml, "kml");
                curNode = FirstNodeWithName(curNode, "Document");
                curNode = FirstNodeWithName(curNode, "Folder");
                var gpsInfos = new Dictionary<string, List<GpsLine>>();
                foreach (XmlNode node in curNode.ChildNodes)
                {
                    if (node.Name == "Placemark")
                    {
                        var name = FirstNodeWithName(node, "name").InnerText.Trim();
                        var miltiGeo = FirstNodeWithName(node, "MultiGeometry");
                        if (miltiGeo == null)
                            continue;
                        var lineString = FirstNodeWithName(miltiGeo, "LineString");
                        var coordsNode = FirstNodeWithName(lineString, "coordinates");
                        var coords = GetCoords(coordsNode.InnerText.Trim().Split(' '));
                        if (!gpsInfos.ContainsKey(name))
                            gpsInfos.Add(name, new List<GpsLine>());
                        gpsInfos[name].Add(new GpsLine(coords));
                    }
                }
                return new ScopeKml(gpsInfos);
            }
        }

        private static XmlNode FirstNodeWithName(XmlNode node, string name)
        {
            foreach (XmlNode curNode in node)
            {
                if (curNode.Name == name)
                    return curNode;
            }
            return null;
        }

        private static List<BasicGeoposition> GetCoords(string[] split)
        {
            var output = new List<BasicGeoposition>();
            foreach (var value in split)
            {
                var valueSplit = value.Split(',');
                var lon = double.Parse(valueSplit[0]);
                var lat = double.Parse(valueSplit[1]);
                output.Add(new BasicGeoposition() { Latitude = lat, Longitude = lon });
            }
            return output;
        }
    }

    public struct GpsInfo
    {
        readonly List<GpsLine> Lines;

        public GpsInfo(List<GpsLine> lines)
        {
            Lines = lines;
        }

        public (BasicGeoposition ClosestGps, double Distance) GetClosestGps(BasicGeoposition point)
        {
            var distance = double.NaN;
            var newPoint = new BasicGeoposition();
            foreach (var line in Lines)
            {
                var (curPoint, curDistance) = line.GetClosestGps(point);
                if (double.IsNaN(distance) || curDistance < distance)
                {
                    distance = curDistance;
                    newPoint = curPoint;
                }
            }
            if (double.IsNaN(distance))
            {
                Debug.WriteLine("FAIL!");
                throw new Exception();
            }
            return (newPoint, distance);
        }
    }

    public struct GpsLine
    {
        readonly List<BasicGeoposition> Points;

        public GpsLine(List<BasicGeoposition> points)
        {
            Points = points;
        }

        public (BasicGeoposition ClosestGps, double Distance) GetClosestGps(BasicGeoposition point)
        {
            var distance = double.NaN;
            var newPoint = new BasicGeoposition();
            for (var i = 1; i < Points.Count; ++i)
            {
                var start = Points[i - 1];
                var end = Points[i];
                var (curDistance, curPoint) = point.DistanceToSegment(start, end);
                if (double.IsNaN(distance) || curDistance < distance)
                {
                    distance = curDistance;
                    newPoint = curPoint;
                }
            }
            if (double.IsNaN(distance))
            {
                Debug.WriteLine("FAIL@");
                throw new Exception();
            }
            return (newPoint, distance);
        }
    }
}
