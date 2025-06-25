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
        public static bool IsPge { get; } = true;
        public CombinedAllegroCisFile CisFile { get; set; }
        public List<PgeEcdaDataPoint> EcdaData { get; set; }
        public HcaInfo HcaInfo { get; set; }
        public Hca Hca { get; set; }
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
            public bool? AmpIsReverse { get; set; } = null;
            public string AmpReadDate { get; set; } = null;
            public BasicGeoposition? AmpGps { get; set; } = null;
            public BasicGeoposition CisGps { get; set; }
            public BasicGeoposition? DcvgGps { get; set; } = null;
            public BasicGeoposition? AcvgGps { get; set; } = null;
            public bool IsCisExtrapolated { get; set; }
            public bool IsCisSkipped { get; set; }
            public bool IsOnOff { get; set; }
            public double On { get; set; }
            public double Off { get; set; }
            public double Baseline { get; set; } = double.NaN;
            public double DcvgValue { get; set; } = double.NaN;
            public double AcvgValue { get; set; } = double.NaN;
            public string Region { get; set; }
            public HcaRegion RegionUpdated { get; set; }

            public PgeEcdaDataPoint(double footage, double on, double off, bool isOnOff, double? depth, bool isSkipped, bool isExtrapolated, BasicGeoposition gps, string region, HcaRegion regionUpdated = null)
            {
                Footage = footage;
                On = on;
                Off = off;
                IsOnOff = isOnOff;
                Depth = depth;
                IsCisSkipped = isSkipped;
                IsCisExtrapolated = isExtrapolated;
                CisGps = gps;
                Region = region;
                RegionUpdated = regionUpdated;
            }

            public int Severity
            {
                get
                {
                    var worseSeverity = (PGESeverity)Math.Max((int)DcvgSeverity, (int)AcvgSeverity);
                    if (CisSeverity == PGESeverity.Moderate && worseSeverity == PGESeverity.Severe)
                        return 1;
                    if (CisSeverity == PGESeverity.Severe && (worseSeverity == PGESeverity.Severe || worseSeverity == PGESeverity.Moderate))
                        return 1;

                    if (CisSeverity == PGESeverity.NRI && worseSeverity == PGESeverity.Severe)
                        return 2;
                    if (CisSeverity == PGESeverity.Minor && worseSeverity == PGESeverity.Severe)
                        return 2;
                    if (CisSeverity == PGESeverity.Moderate && (worseSeverity == PGESeverity.Moderate || worseSeverity == PGESeverity.Minor))
                        return 2;
                    if (CisSeverity == PGESeverity.Severe && (worseSeverity == PGESeverity.Minor || worseSeverity == PGESeverity.NRI))
                        return 2;

                    if (CisSeverity == PGESeverity.NRI && worseSeverity == PGESeverity.Moderate)
                        return 3;
                    if (CisSeverity == PGESeverity.Minor)
                        return 3;
                    if (CisSeverity == PGESeverity.Moderate && worseSeverity == PGESeverity.NRI)
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
            public PGESeverity DcvgSeverity
            {
                get
                {
                    return GetDcvgSeverity().Severity;
                }
            }
            public string DcvgReason
            {
                get
                {
                    return GetDcvgSeverity().Reason;
                }
            }
            public PGESeverity AcvgSeverity
            {
                get
                {
                    return GetAcvgSeverity().Severity;
                }
            }
            public string AcvgReason
            {
                get
                {
                    return GetAcvgSeverity().Reason;
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

                if (IsOnOff)
                {
                    var changeInBaseline = Math.Abs(Off - Baseline);
                    if (Off > -0.5)
                        return (PGESeverity.Severe, "Off is more positive than -0.500");
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
                else
                {
                    var changeInBaseline = Math.Abs(On - Baseline);
                    if (On > -0.6)
                        return (PGESeverity.Severe, "On is more positive than -0.600");
                    if (On > -0.8 && changeInBaseline >= 0.2)
                        return (PGESeverity.Severe, "On is between -0.800 and -0.601 and difference in baseline is greater than 0.200");
                    if (On > -0.8)
                        return (PGESeverity.Moderate, "On is between -0.800 and -0.600");
                    if (On > -0.95 && changeInBaseline >= 0.2)
                        return (PGESeverity.Moderate, "On is between -0.950 and -0.801 and difference in baseline is greater than 0.200");
                    if (On > -0.95)
                        return (PGESeverity.Minor, "On is between -0.950 and -0.801");
                    if (changeInBaseline >= 0.2)
                        return (PGESeverity.Minor, "difference in baseline is greater than 0.200");
                    return (PGESeverity.NRI, "");
                }
            }

            private (PGESeverity Severity, string Reason) GetDcvgSeverity()
            {
                if (IsCisSkipped)
                    return (PGESeverity.NRI, "Skip");
                if (double.IsNaN(DcvgValue))
                    return (PGESeverity.NRI, "");
                //DCVG
                var severity = PGESeverity.NRI;
                var reason = "DCVG % IR is greater than 0 and less than or equal to 15";
                if (DcvgValue > 60)
                {
                    severity = PGESeverity.Severe;
                    reason = "DCVG % IR is greater than 60";
                }
                else if (DcvgValue > 35)
                {
                    severity = PGESeverity.Moderate;
                    reason = "DCVG % IR is greater than 35 and less than or equal to 60";
                }
                else if (DcvgValue > 15)
                {
                    severity = PGESeverity.Minor;
                    reason = "DCVG % IR is greater than 15 and less than or equal to 35";
                }
                return (severity, reason);
            }

            private (PGESeverity Severity, string Reason) GetAcvgSeverity()
            {
                if (IsCisSkipped)
                    return (PGESeverity.NRI, "Skip");
                if (double.IsNaN(AcvgValue))
                    return (PGESeverity.NRI, "");
                //ACVG
                var severity = PGESeverity.NRI;
                var reason = "Individual normalized ACVG indications are less than 25";
                if (AcvgValue >= 75)
                {
                    severity = PGESeverity.Severe;
                    reason = "Individual normalized ACVG indications are greater than or equal to 75";
                }
                else if (AcvgValue >= 50)
                {
                    severity = PGESeverity.Moderate;
                    reason = "Individual normalized ACVG indications are greater than or equal to 50 and less than 75";
                }
                else if (AcvgValue >= 25)
                {
                    severity = PGESeverity.Minor;
                    reason = "Individual normalized ACVG indications are greater than or equal to 25 and less than 50";
                }
                return (severity, reason);
            }

            public bool IsEquivalent(PgeEcdaDataPoint other)
            {
                if (CisSeverity != other.CisSeverity)
                    return false;
                if (CisReason != other.CisReason)
                    return false;

                if (DcvgSeverity != other.DcvgSeverity)
                    return false;
                if (DcvgReason != other.DcvgReason)
                    return false;

                if (AcvgSeverity != other.AcvgSeverity)
                    return false;
                if (AcvgReason != other.AcvgReason)
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
                        closestPoint.DcvgValue = point.IndicationPercent;
                        //var date = point.HasTime ? point.Times[0].ToString("MM/dd/yyyy") : "";
                        //closestPoint.DcvgDate = date;
                        closestPoint.DcvgGps = closestPoint.CisGps;
                    }
                    if (point.HasGPS)
                        lastGpsPoint = point;
                }
            }
        }

        private void AlignAmpReads(List<(double Footage, BasicGeoposition gps, double Value, double Percent, bool IsRevese, string ReadDate)> pcmReads)
        {
            foreach (var (footage, gps, value, percent, isReverse, readDate) in pcmReads)
            {
                PgeEcdaDataPoint closestPoint = null;
                foreach (var surveyPoint in EcdaData)
                {
                    if((int)surveyPoint.Footage == (int)footage)
                        closestPoint = surveyPoint;
                }
                if (closestPoint == null)
                    continue;
                if (Math.Abs(closestPoint.Footage - footage) > 20)
                    continue;
                if (closestPoint.AmpPercent.HasValue)
                    continue;
                closestPoint.AmpPercent = percent;
                closestPoint.AmpIsReverse = isReverse;
                closestPoint.AmpReadDate = readDate;
                closestPoint.AmpValue = value;
                var middleGps = closestPoint.CisGps.MiddleTowards(gps);
                closestPoint.AmpGps = middleGps;
            }
        }

        public PgeEcdaReportInformation(CombinedAllegroCisFile cisFile, List<AllegroCISFile> dcvgFiles, List<(BasicGeoposition Gps, string Date, double dB)> acvgIndications, List<(double Footage, BasicGeoposition Gps, double Value, double Percent, bool IsReverse, string ReadDate)> ampReads, Hca hca, double maxSpacing, bool useMir = false)
        {
            MaxSpacing = maxSpacing;
            UseMir = useMir;
            CisFile = cisFile;
            Hca = hca;
            ExtrapolateCisDataUpdated();
            AlignDcvgIndications(dcvgFiles);
            AlignAmpReads(ampReads);

            foreach (var (gps, date, value) in acvgIndications)
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
                closestPoint.AcvgValue = value;
                //closestPoint.AcvgDate = date;
                closestPoint.AcvgGps = gps;
            }
        }
        public List<(double, double, BasicGeoposition)> GetDcvgData()
        {
            var output = new List<(double, double, BasicGeoposition)>();
            foreach (var point in EcdaData)
            {
                if (!double.IsNaN(point.DcvgValue))
                {
                    output.Add((point.Footage, point.DcvgValue, point.DcvgGps.Value));
                }
            }
            return output;
        }

        public List<(double, double, BasicGeoposition)> GetAcvgData()
        {
            var output = new List<(double, double, BasicGeoposition)>();
            foreach (var point in EcdaData)
            {
                if (!double.IsNaN(point.AcvgValue))
                {
                    output.Add((point.Footage, point.AcvgValue, point.AcvgGps.Value));
                }
            }
            return output;
        }

        public void StraightenGps(CombinedAllegroCisFile file, GpsInfo info)
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

        public void StraightenGpsUpdated(CombinedAllegroCisFile file, GpsInfo info)
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
                var newPoint = new PgeEcdaDataPoint(curFootage, curOn, curOff, CisFile.Type == FileType.OnOff, curDepth, false, false, curGps, HcaInfo.ClosestRegion(curGps));
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
                    newPoint = new PgeEcdaDataPoint(fakeFoot, fakeOn, fakeOff, CisFile.Type == FileType.OnOff, fakeDepth, isSkipped, true, fakeGps, HcaInfo.ClosestRegion(fakeGps));
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
                if (point.AmpValue.HasValue && !double.IsNaN(point.AmpValue.Value))
                    output.Add((point.Footage, point.AmpValue.Value));
            }
            return output;
        }

        public List<(double Footage, BasicGeoposition Gps, double Value, double Percent, bool IsReverse, string ReadDate)> GetFullAmpData()
        {
            var output = new List<(double Footage, BasicGeoposition Gps, double Value, double Percent, bool IsReverse, string ReadDate)>();
            foreach (var point in EcdaData)
            {
                if (point.AmpValue.HasValue || point.AmpPercent.HasValue)
                    output.Add((point.Footage, point.AmpGps.Value, point.AmpValue.Value, point.AmpPercent.Value, point.AmpIsReverse.Value, point.AmpReadDate));
            }
            return output;
        }

        public List<(double Footage, bool IsReverse, string ReadDate)> GetAmpDirectionData()
        {
            var output = new List<(double Footage, bool IsReverse, string ReadDate)>();
            foreach (var point in EcdaData)
            {
                if (point.AmpValue.HasValue && !double.IsNaN(point.AmpValue.Value))
                    output.Add((point.Footage, point.AmpIsReverse.Value, point.AmpReadDate));
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
                var newPoint = new PgeEcdaDataPoint(curFootage, curOn, curOff, CisFile.Type == FileType.OnOff, curDepth, closeRegion.ShouldSkip, false, curGps, closeRegion.ReportQName, closeRegion);
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
                    newPoint = new PgeEcdaDataPoint(fakeFoot, fakeOn, fakeOff, CisFile.Type == FileType.OnOff, fakeDepth, isSkipped, true, fakeGps, closeRegion.ReportQName, closeRegion);
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
                if(CisFile.Type == FileType.Native)
                    average = (within100.Count() != 0 ? within100.Average(value => value.On) : point.On);
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
                        var valForBaseline = CisFile.Type == FileType.OnOff ? centerPoint.Off : centerPoint.On;
                        var diffFromBaseline = Math.Abs(valForBaseline - curAverage);
                        var diffFromCurBaseline = Math.Abs(valForBaseline - curBaselines[center]);
                        if (diffFromBaseline > diffFromCurBaseline)
                        {
                            curBaselines[center] = curAverage;
                        }
                    }
                }
                var baseline = curBaselines[center];
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
