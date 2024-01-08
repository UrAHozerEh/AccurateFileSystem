using AccurateFileSystem;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Devices.Geolocation;
using Windows.UI;
using CisSeries = AccurateReportSystem.PGECISIndicationChartSeries;
using DcvgSeries = AccurateReportSystem.PgeDcvgIndicationChartSeries;

namespace AccurateReportSystem
{
    public class PGEDirectExaminationPriorityChartSeries : ExceptionsChartSeries
    {
        public bool IsPge { get; } = true;
        public override int NumberOfValues => 3;
        public Color OneColor { get; set; } = Colors.Red;
        public Color TwoColor { get; set; } = Colors.Green;
        public Color ThreeColor { get; set; } = Colors.Blue;
        public CisSeries CisSeries { get; set; } = null;
        public DcvgSeries DcvgSeries { get; set; } = null;
        private List<(double Footage, BasicGeoposition Gps, double Value, double Percent, bool isReverse, string ReadDate)> AmpReads { get; set; } = null;
        public List<string[]> CISShapeFileOutput { get; set; }
        public List<string[]> IndicationShapeFileOutput { get; set; }
        public List<string[]> AmpsShapeFileOutput { get; set; }

        private static int LABEL = 0;
        private static int STATION = 1;
        private static int DATE = 2;
        private static int PRIMARYDES = 3;
        private static int DCVGREMOTE = 6;
        private static int DEPTH = 7;
        private static int ECDAREGION { get; } = 8;
        private static int LAT = 12;
        private static int LON = 13;
        private static int ECDACAT = 14;
        private static int PCMCAT = 16;
        private static int DCVGCAT = 17;
        private static int CISCAT = 18;
        private static int ON = 19;
        private static int OFF = 20;
        private static int PCM = 28;
        private static int ACVG = 32;
        private static int ACVGCAT = 33;

        public PGEDirectExaminationPriorityChartSeries(Chart chart, CisSeries cisSeries = null, DcvgSeries dcvgSeries = null, List<(double Footage, BasicGeoposition Gps, double Value, double Percent, bool isReverse, string ReadDate)> ampReads = null) : base(chart.LegendInfo, chart.YAxesInfo)
        {
            CisSeries = cisSeries;
            DcvgSeries = dcvgSeries;
            AmpReads = ampReads;
            OutlineColor = null;
        }

        private void GenerateShapeFileTemplate()
        {
            var header = new string[34];
            header[0] = "LABEL";
            header[1] = "STATION";
            header[2] = "DATEOFCIS";
            header[3] = "PRIMARYDES";
            header[4] = "SECONDDESC";
            header[5] = "TOPO";
            header[6] = "DCVGREMOTE";
            header[7] = "DEPTH";
            header[8] = "ECDAREGION";
            header[9] = "SEGMENT";
            header[10] = "NORTHING";
            header[11] = "EASTING";
            header[12] = "LATITUDE";
            header[13] = "LONGITUDE";
            header[14] = "ECDACAT";
            header[15] = "ILICAT";
            header[16] = "PCMCAT";
            header[17] = "DCVGCAT";
            header[18] = "CISCAT";
            header[19] = "ONREAD";
            header[20] = "OFFREAD";
            header[21] = "STATICREAD";
            header[22] = "ELEVATION";
            header[23] = "LINE_NUM";
            header[24] = "NSEG";
            header[25] = "PROCESS";
            header[26] = "ROUTE";
            header[27] = "IMA";
            header[28] = "PCM";
            header[29] = "META";
            header[30] = "PRIMARYGPS";
            header[31] = "SECONDGPS";
            header[32] = "ACVG";
            header[33] = "ACVGCAT";
            CISShapeFileOutput = new List<string[]>
            {
                header
            };
            IndicationShapeFileOutput = new List<string[]>
            {
                header
            };
            AmpsShapeFileOutput = new List<string[]>
            {
                header
            };
        }

        private Dictionary<int, (double Value, PGESeverity Severity, string Reason, BasicGeoposition Gps)> GetPcmSeverities()
        {
            var output = new Dictionary<int, (double, PGESeverity, string, BasicGeoposition)>();
            if (AmpReads == null)
                return output;
            foreach (var (footage, gps, value, percent, _, _) in AmpReads)
            {
                var (severity, reason) = GetAmpSeverity(percent);
                output.Add((int)footage, (value, severity, reason, gps));
            }

            return output;
        }

        private static (PGESeverity Severity, string Reason) GetAmpSeverity(double percent)
        {
            if (percent < 10)
                return (PGESeverity.NRI, "");
            if (percent < 30)
                return (PGESeverity.Minor, "Amp % decrease between 10% and 30%");
            if (percent < 50)
                return (PGESeverity.Moderate, "Amp % decrease between 30% and 50%");

            return (PGESeverity.Severe, "Amp % decrease above 50%");
        }

        public List<(double Start, double End, PGESeverity CisSeverity, PGESeverity DcvgSeverity, PGESeverity ThirdToolSeverity, HcaRegion Region, int Overall, string Comments)> GetUpdatedReportQ()
        {
            var output = new List<(double, double, PGESeverity, PGESeverity, PGESeverity, HcaRegion, int, string)>();
            var lastFoot = 0.0;
            var cisSeverities = new Dictionary<int, (double, double, string, DateTime, double?, bool, PGESeverity, string, BasicGeoposition, HcaRegion)>();
            GenerateShapeFileTemplate();

            if (CisSeries != null)
            {
                foreach (var dataPoint in CisSeries.DataUpdated)
                {
                    if (dataPoint.Footage > lastFoot)
                        lastFoot = dataPoint.Footage;
                    cisSeverities.Add((int)dataPoint.Footage, (dataPoint.On, dataPoint.Off, dataPoint.Comment, dataPoint.Date, dataPoint.Depth, dataPoint.IsExtrapolated, dataPoint.Severity, dataPoint.Reason, dataPoint.Gps, dataPoint.Region));
                }
            }
            var dcvgSeverities = new Dictionary<int, (double, PGESeverity, string, BasicGeoposition)>();
            if (DcvgSeries != null)
            {
                foreach (var (_, foot, value, severity, reason, gps) in DcvgSeries.Data)
                {
                    if (foot > lastFoot)
                        lastFoot = foot;
                    if (!dcvgSeverities.ContainsKey((int)foot))
                        dcvgSeverities.Add((int)foot, (value, severity, reason, gps));
                }
            }
            var pcmSeverities = GetPcmSeverities();

            var lastStartFoot = 0;
            var lastEndFoot = 0;

            var lastAmpSeverity = PGESeverity.NRI;
            var lastAmpReason = "";
            var lastAmpValue = 0.0;
            var lastAmpGps = new BasicGeoposition();
            if (pcmSeverities.ContainsKey(0))
                (lastAmpValue, lastAmpSeverity, lastAmpReason, lastAmpGps) = pcmSeverities[0];

            var lastDcvgSeverity = PGESeverity.NRI;
            var lastDcvgReason = "";
            var lastDcvgValue = 0.0;
            var lastDcvgGps = new BasicGeoposition();
            if (dcvgSeverities.ContainsKey(0))
                (lastDcvgValue, lastDcvgSeverity, lastDcvgReason, lastDcvgGps) = dcvgSeverities[0];

            var lastCisSeverity = PGESeverity.NRI;
            var lastCisReason = "";
            var lastRegion = new HcaRegion();
            var lastIsExtrapolated = true;
            var lastOn = 0.0;
            var lastOff = 0.0;
            var lastGps = new BasicGeoposition();
            var lastPrimaryDes = "";
            var lastDate = new DateTime();
            double? lastDepth = null;
            if (cisSeverities.ContainsKey(0))
                (lastOn, lastOff, lastPrimaryDes, lastDate, lastDepth, lastIsExtrapolated, lastCisSeverity, lastCisReason, lastGps, lastRegion) = cisSeverities[0];

            var lastPrio = GetPriority(lastCisSeverity, lastDcvgSeverity, lastAmpSeverity);
            if (!lastIsExtrapolated)
            {
                var firstShapeValues = new string[34];
                firstShapeValues[LABEL] = $"On: {lastOn}, Off: {lastOff}";
                firstShapeValues[STATION] = "0";
                firstShapeValues[DATE] = lastDate.ToShortDateString();
                firstShapeValues[PRIMARYDES] = lastPrimaryDes;
                if (lastDepth.HasValue)
                    firstShapeValues[DEPTH] = lastDepth.Value.ToString("F0");
                firstShapeValues[ECDAREGION] = lastRegion.ReportQName;
                firstShapeValues[LAT] = lastGps.Latitude.ToString("F8");
                firstShapeValues[LON] = lastGps.Longitude.ToString("F8");
                firstShapeValues[ECDACAT] = "Priority " + lastPrio.ToString();
                firstShapeValues[CISCAT] = lastCisSeverity.GetDisplayName();
                firstShapeValues[ON] = lastOn.ToString("F4");
                firstShapeValues[OFF] = lastOff.ToString("F4");
                CISShapeFileOutput.Add(firstShapeValues);
                if (dcvgSeverities.ContainsKey(0) && DcvgSeries.IsDcvg)
                {
                    firstShapeValues = new string[34];

                    firstShapeValues[CISCAT] = lastCisSeverity.GetDisplayName();
                    firstShapeValues[ON] = lastOn.ToString("F4");
                    firstShapeValues[OFF] = lastOff.ToString("F4");

                    firstShapeValues[LABEL] = IsPge ? $"DCVG: {lastDcvgValue:F1}%" : "DCVG Indication";
                    firstShapeValues[STATION] = "0";
                    firstShapeValues[DATE] = lastDate.ToShortDateString();
                    firstShapeValues[DCVGREMOTE] = IsPge ? lastDcvgValue.ToString("F1") : "";
                    firstShapeValues[ECDAREGION] = lastRegion.ReportQName;
                    firstShapeValues[LAT] = lastDcvgGps.Latitude.ToString("F8");
                    firstShapeValues[LON] = lastDcvgGps.Longitude.ToString("F8");
                    firstShapeValues[ECDACAT] = "Priority " + lastPrio.ToString();
                    firstShapeValues[DCVGCAT] = IsPge ? lastDcvgSeverity.GetDisplayName() : "Indication";
                    IndicationShapeFileOutput.Add(firstShapeValues);
                }
                if (dcvgSeverities.ContainsKey(0) && !DcvgSeries.IsDcvg)
                {
                    firstShapeValues = new string[34];

                    firstShapeValues[CISCAT] = lastCisSeverity.GetDisplayName();
                    firstShapeValues[ON] = lastOn.ToString("F4");
                    firstShapeValues[OFF] = lastOff.ToString("F4");

                    firstShapeValues[LABEL] = $"ACVG: {lastDcvgValue:F2}";
                    firstShapeValues[STATION] = "0";
                    firstShapeValues[DATE] = lastDate.ToShortDateString();
                    firstShapeValues[ECDAREGION] = lastRegion.ReportQName;
                    firstShapeValues[LAT] = lastDcvgGps.Latitude.ToString("F8");
                    firstShapeValues[LON] = lastDcvgGps.Longitude.ToString("F8");
                    firstShapeValues[ECDACAT] = "Priority " + lastPrio.ToString();
                    firstShapeValues[ACVG] = lastDcvgValue.ToString("F2");
                    firstShapeValues[ACVGCAT] = lastDcvgSeverity.GetDisplayName();
                    IndicationShapeFileOutput.Add(firstShapeValues);
                }
                if (pcmSeverities.ContainsKey(0) && !Double.IsNaN(lastAmpValue))
                {
                    firstShapeValues = new string[34];

                    firstShapeValues[CISCAT] = lastCisSeverity.GetDisplayName();
                    firstShapeValues[ON] = lastOn.ToString("F4");
                    firstShapeValues[OFF] = lastOff.ToString("F4");

                    firstShapeValues[LABEL] = $"PCM: {lastAmpValue:F0} mA";
                    firstShapeValues[STATION] = "0";
                    firstShapeValues[DATE] = lastDate.ToShortDateString();
                    firstShapeValues[ECDAREGION] = lastRegion.ReportQName;
                    firstShapeValues[LAT] = lastAmpGps.Latitude.ToString("F8");
                    firstShapeValues[LON] = lastAmpGps.Longitude.ToString("F8");
                    firstShapeValues[ECDACAT] = "Priority " + lastPrio.ToString();
                    firstShapeValues[PCM] = lastAmpValue.ToString("F0");
                    firstShapeValues[PCMCAT] = lastAmpSeverity.GetDisplayName();
                    AmpsShapeFileOutput.Add(firstShapeValues);
                }
            }

            for (var curFoot = 1; curFoot <= lastFoot; ++curFoot)
            {
                var (curDcvgValue, curDcvgSeverity, curDcvgReason, curDcvgGps) = (0.0, PGESeverity.NRI, "", new BasicGeoposition());
                if (dcvgSeverities.ContainsKey(curFoot))
                    (curDcvgValue, curDcvgSeverity, curDcvgReason, curDcvgGps) = dcvgSeverities[curFoot];

                var (curAmpValue, curAmpSeverity, curAmpReason, curAmpgGps) = (0.0, PGESeverity.NRI, "", new BasicGeoposition());
                if (pcmSeverities.ContainsKey(curFoot))
                    (curAmpValue, curAmpSeverity, curAmpReason, curAmpgGps) = pcmSeverities[curFoot];

                var (curOn, curOff, curPrimaryDes, curDate, curDepth, curIsExtrapolated, curCisSeverity, curCisReason, curGps, curRegion) = (0.0, 0.0, "", new DateTime(), (double?)null, true, PGESeverity.NRI, "", new BasicGeoposition(), new HcaRegion());
                if (cisSeverities.ContainsKey(curFoot))
                    (curOn, curOff, curPrimaryDes, curDate, curDepth, curIsExtrapolated, curCisSeverity, curCisReason, curGps, curRegion) = cisSeverities[curFoot];

                var curPrio = GetPriority(curCisSeverity, curDcvgSeverity, curAmpSeverity);
                if (!curIsExtrapolated && !curRegion.ShouldSkip)
                {
                    var shapeValues = new string[34];
                    shapeValues[LABEL] = $"On: {curOn}, Off: {curOff}";
                    shapeValues[STATION] = curFoot.ToString("F0");
                    shapeValues[DATE] = curDate.ToShortDateString();
                    shapeValues[PRIMARYDES] = curPrimaryDes;
                    if (curDepth.HasValue)
                        shapeValues[DEPTH] = curDepth.Value.ToString("F0");
                    shapeValues[ECDAREGION] = curRegion.ReportQName;
                    if (!curIsExtrapolated || curDcvgGps.Equals(new BasicGeoposition()))
                    {
                        shapeValues[LAT] = curGps.Latitude.ToString("F8");
                        shapeValues[LON] = curGps.Longitude.ToString("F8");
                    }
                    else
                    {
                        shapeValues[LAT] = curDcvgGps.Latitude.ToString("F8");
                        shapeValues[LON] = curDcvgGps.Longitude.ToString("F8");
                    }
                    shapeValues[ECDACAT] = "Priority " + curPrio.ToString();
                    shapeValues[CISCAT] = curCisSeverity.GetDisplayName();
                    shapeValues[ON] = curOn.ToString("F4");
                    shapeValues[OFF] = curOff.ToString("F4");
                    CISShapeFileOutput.Add(shapeValues);
                }
                if (dcvgSeverities.ContainsKey(curFoot) && !curRegion.ShouldSkip)
                {
                    var shapeValues = new string[34];

                    shapeValues[CISCAT] = curCisSeverity.GetDisplayName();
                    shapeValues[ON] = curOn.ToString("F4");
                    shapeValues[OFF] = curOff.ToString("F4");

                    shapeValues[LABEL] = DcvgSeries.IsDcvg ? (IsPge ? $"DCVG: {curDcvgValue:F1}%" : "DCVG Indication") : $"ACVG: {curDcvgValue:F2}";
                    shapeValues[STATION] = curFoot.ToString("F0");
                    shapeValues[DATE] = curDate.ToShortDateString();
                    shapeValues[ECDAREGION] = curRegion.ReportQName;
                    shapeValues[LAT] = curDcvgGps.Latitude.ToString("F8");
                    shapeValues[LON] = curDcvgGps.Longitude.ToString("F8");
                    shapeValues[ECDACAT] = "Priority " + curPrio.ToString();
                    if (DcvgSeries.IsDcvg)
                    {
                        if (IsPge)
                        {
                            shapeValues[DCVGCAT] = curDcvgSeverity.GetDisplayName();
                            shapeValues[DCVGREMOTE] = curDcvgValue.ToString("F1");
                        }
                        else
                        {
                            shapeValues[DCVGCAT] = "";
                            shapeValues[DCVGCAT] = "Indication";
                        }
                    }

                    if (!DcvgSeries.IsDcvg)
                    {
                        shapeValues[ACVG] = curDcvgValue.ToString("F2");
                        shapeValues[ACVGCAT] = curDcvgSeverity.GetDisplayName();
                    }
                    IndicationShapeFileOutput.Add(shapeValues);
                }

                if (pcmSeverities.ContainsKey(curFoot) && !curRegion.ShouldSkip && !Double.IsNaN(curAmpValue))
                {
                    var shapeValues = new string[34];

                    shapeValues[CISCAT] = curCisSeverity.GetDisplayName();
                    shapeValues[ON] = curOn.ToString("F4");
                    shapeValues[OFF] = curOff.ToString("F4");

                    shapeValues[LABEL] = $"PCM: {curAmpValue:F0} mA";
                    shapeValues[STATION] = curFoot.ToString("F0");
                    shapeValues[DATE] = curDate.ToShortDateString();
                    shapeValues[ECDAREGION] = curRegion.ReportQName;
                    shapeValues[LAT] = curAmpgGps.Latitude.ToString("F8");
                    shapeValues[LON] = curAmpgGps.Longitude.ToString("F8");
                    shapeValues[ECDACAT] = "Priority " + curPrio.ToString();
                    shapeValues[PCMCAT] = curAmpSeverity.GetDisplayName();
                    shapeValues[PCM] = curAmpValue.ToString("F0");
                    AmpsShapeFileOutput.Add(shapeValues);
                }

                if (!(curRegion.Name == lastRegion.Name && curRegion.ShouldSkip))
                    if (curCisSeverity != lastCisSeverity || curCisReason != lastCisReason || curDcvgSeverity != lastDcvgSeverity || curDcvgReason != lastDcvgReason || curRegion.Name != lastRegion.Name || curAmpSeverity != lastAmpSeverity || curAmpReason != lastAmpReason)
                    {
                        var fullReason = lastCisReason;
                        if (string.IsNullOrWhiteSpace(fullReason))
                            fullReason = lastDcvgReason;
                        else
                            fullReason += ". " + lastDcvgReason;
                        fullReason = fullReason.Trim();

                        if (string.IsNullOrWhiteSpace(fullReason))
                            fullReason = lastAmpReason;
                        else
                            fullReason += ". " + lastAmpReason;

                        if (lastRegion.ShouldSkip)
                            fullReason = "SKIP.";
                        output.Add((lastStartFoot, curFoot, lastCisSeverity, lastDcvgSeverity, lastAmpSeverity, lastRegion, lastPrio, fullReason.Trim()));
                        lastStartFoot = curFoot;
                        lastCisReason = curCisReason;
                        lastCisSeverity = curCisSeverity;
                        lastDcvgReason = curDcvgReason;
                        lastDcvgSeverity = curDcvgSeverity;
                        lastAmpSeverity = curAmpSeverity;
                        lastAmpReason = curAmpReason;
                        lastRegion = curRegion;
                        lastPrio = GetPriority(lastCisSeverity, lastDcvgSeverity, lastAmpSeverity);
                    }
                lastEndFoot = curFoot;
            }

            var finalPrio = GetPriority(lastCisSeverity, lastDcvgSeverity, lastAmpSeverity);
            var finalReason = lastCisReason;
            if (string.IsNullOrWhiteSpace(finalReason))
                finalReason = lastDcvgReason;
            else
                finalReason += ". " + lastDcvgReason;
            if (lastRegion.ShouldSkip)
                finalReason = "SKIP.";
            output.Add((lastStartFoot, lastEndFoot, lastCisSeverity, lastDcvgSeverity, lastAmpSeverity, lastRegion, finalPrio, finalReason));

            return output;
        }

        public override List<(double Start, double End, Color Color)> GetColorBounds(PageInformation page)
        {
            var cisSeverities = new Dictionary<int, PGESeverity>();
            if (CisSeries != null)
            {
                if (CisSeries.Hca != null)
                {
                    foreach (var dataPoint in CisSeries.DataUpdated)
                    {
                        if (dataPoint.Footage < page.StartFootage)
                            continue;
                        if (dataPoint.Footage > page.EndFootage)
                            break;
                        cisSeverities.Add((int)dataPoint.Footage, dataPoint.Severity);
                    }
                }
                else
                {
                    foreach (var dataPoint in CisSeries.Data)
                    {
                        if (dataPoint.Footage < page.StartFootage)
                            continue;
                        if (dataPoint.Footage > page.EndFootage)
                            break;
                        cisSeverities.Add((int)dataPoint.Footage, dataPoint.Severity);
                    }
                }
            }
            var dcvgSeverities = new Dictionary<int, (PGESeverity, int)>();
            if (DcvgSeries != null)
            {
                foreach (var (foot, actualFoot, _, severity, _, _) in DcvgSeries.Data)
                {
                    var footInt = (int)actualFoot;
                    if (footInt < page.StartFootage)
                        continue;
                    if (footInt > page.EndFootage)
                        break;
                    if (!dcvgSeverities.ContainsKey((int)foot))
                        dcvgSeverities.Add((int)foot, (severity, (int)actualFoot));
                }
            }

            var pcmSeverities = new Dictionary<int, PGESeverity>();
            if (AmpReads != null)
            {
                foreach (var (footage, _, _, percent, _, _) in AmpReads)
                {
                    var footInt = (int)footage;
                    if (footInt < page.StartFootage)
                        continue;
                    if (footInt > page.EndFootage)
                        break;
                    var (severity, _) = GetAmpSeverity(percent);
                    pcmSeverities.Add(footInt, severity);
                }
            }


            var colors = new List<(double Start, double End, Color Color)>();
            (double Start, double end, int Prio)? prevData = null;
            (double Footage, int Prio)? lastData = null;
            for (var curFoot = (int)page.StartFootage; curFoot <= page.EndFootage; ++curFoot)
            {
                var cis = cisSeverities.TryGetValue(curFoot, out PGESeverity curSeverity) ? curSeverity : PGESeverity.NRI;

                (PGESeverity, int) curDcvgSeverity;
                PGESeverity curPcmSeverity;
                var hasDcvg = dcvgSeverities.TryGetValue(curFoot, out curDcvgSeverity);
                var hasToolThree = pcmSeverities.TryGetValue(curFoot, out curPcmSeverity);
                var toolTwo = hasDcvg ? curDcvgSeverity.Item1 : PGESeverity.NRI;
                var toolThree = hasToolThree ? curPcmSeverity : PGESeverity.NRI;
                var prio = GetPriority(cis, toolTwo, toolThree);
                if (hasDcvg)
                {
                    var actualDcvg = dcvgSeverities[curDcvgSeverity.Item2].Item1;
                    var actualCis = cisSeverities.GetValueOrDefault(curDcvgSeverity.Item2, PGESeverity.NRI);
                    var actualDcvgPrio = GetPriority(actualCis, actualDcvg, toolThree);
                    if (prio != actualDcvgPrio)
                    {
                        prio = actualDcvgPrio;
                    }
                }


                if (!prevData.HasValue)
                {
                    prevData = (curFoot, curFoot, prio);
                    continue;
                }

                var (prevStart, prevEnd, prevPrio) = prevData.Value;
                if (prio == prevPrio)
                {
                    prevData = (prevStart, curFoot, prevPrio);
                }
                else
                {
                    var middleFoot = (prevEnd + curFoot) / 2;
                    var prevColor = GetColor(prevPrio);
                    if (prevColor.HasValue)
                        colors.Add((prevStart, middleFoot, prevColor.Value));
                    prevData = (middleFoot, curFoot, prio);
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

        private Color? GetColor(int prio)
        {
            switch (prio)
            {
                case 1:
                    return OneColor;
                case 2:
                    return TwoColor;
                case 3:
                    return ThreeColor;
                default:
                    return null;
            }
        }

        private int GetPriority(PGESeverity cis, PGESeverity toolTwo, PGESeverity toolThree)
        {
            if (IsPge)
            {
                var worstOther = toolTwo.GetWorseOf(toolThree);
                if (cis == PGESeverity.Moderate && worstOther == PGESeverity.Severe)
                    return 1;
                if (cis == PGESeverity.Severe && (worstOther == PGESeverity.Severe || worstOther == PGESeverity.Moderate))
                    return 1;

                if (cis == PGESeverity.NRI && worstOther == PGESeverity.Severe)
                    return 2;
                if (cis == PGESeverity.Minor && worstOther == PGESeverity.Severe)
                    return 2;
                if (cis == PGESeverity.Moderate && (worstOther == PGESeverity.Moderate || worstOther == PGESeverity.Minor))
                    return 2;
                if (cis == PGESeverity.Severe && (worstOther == PGESeverity.Minor || worstOther == PGESeverity.NRI))
                    return 2;

                if (cis == PGESeverity.NRI && worstOther == PGESeverity.Moderate)
                    return 3;
                if (cis == PGESeverity.Minor)
                    return 3;
                if (cis == PGESeverity.Moderate && worstOther == PGESeverity.NRI)
                    return 3;

                return 4;
            }
            else
            {
                if (toolTwo == PGESeverity.NRI)
                {
                    switch (cis)
                    {
                        case PGESeverity.NRI: return 4;
                        case PGESeverity.Minor: return 3;
                        case PGESeverity.Moderate: return 3;
                        case PGESeverity.Severe: return 2;
                    }
                    return 2;
                }
                switch (cis)
                {
                    case PGESeverity.NRI: return 3;
                    case PGESeverity.Minor: return 2;
                    case PGESeverity.Moderate: return 1;
                    case PGESeverity.Severe: return 1;
                }
                return 1;
            }
        }

        protected override List<(string, Color)> LegendInfo()
        {
            return new List<(string, Color)>()
            {
                ("Priority III", ThreeColor),
                ("Priority II", TwoColor),
                ("Priority I", OneColor)
            };
        }
    }
}
