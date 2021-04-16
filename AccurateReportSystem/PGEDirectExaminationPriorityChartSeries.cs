using System;
using System.Collections.Generic;
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
        public override int NumberOfValues => 3;
        public Color OneColor { get; set; } = Colors.Red;
        public Color TwoColor { get; set; } = Colors.Green;
        public Color ThreeColor { get; set; } = Colors.Blue;
        public CisSeries CisSeries { get; set; } = null;
        public DcvgSeries DcvgSeries { get; set; } = null;
        public List<string[]> CISShapeFileOutput { get; set; }
        public List<string[]> IndicationShapeFileOutput { get; set; }

        private static int LABEL = 0;
        private static int STATION = 1;
        private static int DATE = 2;
        private static int PRIMARYDES = 3;
        private static int DCVGREMOTE = 6;
        private static int DEPTH = 7;
        private static int ECDAREGION = 8;
        private static int LAT = 12;
        private static int LON = 13;
        private static int ECDACAT = 14;
        private static int DCVGCAT = 17;
        private static int CISCAT = 18;
        private static int ON = 19;
        private static int OFF = 20;
        private static int ACVG = 32;
        private static int ACVGCAT = 33;

        public PGEDirectExaminationPriorityChartSeries(Chart chart, CisSeries cisSeries = null, DcvgSeries dcvgSeries = null) : base(chart.LegendInfo, chart.YAxesInfo)
        {
            CisSeries = cisSeries;
            DcvgSeries = dcvgSeries;
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
            CISShapeFileOutput = new List<string[]>();
            CISShapeFileOutput.Add(header);
            IndicationShapeFileOutput = new List<string[]>();
            IndicationShapeFileOutput.Add(header);
        }

        public List<(double Start, double End, PGESeverity CisSeverity, PGESeverity DcvgSeverity, string Region, int Overall, string Comments)> GetReportQ()
        {
            var output = new List<(double, double, PGESeverity, PGESeverity, string, int, string)>();
            var lastFoot = 0.0;
            string catString = "NRI";

            var cisSeverities = new Dictionary<int, (double, double, string, DateTime, double?, bool, PGESeverity, string, BasicGeoposition, string)>();
            GenerateShapeFileTemplate();

            if (CisSeries != null)
            {
                foreach (var dataPoint in CisSeries.Data)
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
            var lastStartFoot = 0;
            var lastEndFoot = 0;
            var lastDcvgSeverity = PGESeverity.NRI;
            var lastDcvgReason = "";
            var lastDcvgValue = 0.0;
            var lastDcvgGps = new BasicGeoposition();
            if (dcvgSeverities.ContainsKey(0))
                (lastDcvgValue, lastDcvgSeverity, lastDcvgReason, lastDcvgGps) = dcvgSeverities[0];

            var lastCisSeverity = PGESeverity.NRI;
            var lastCisReason = "";
            var lastRegion = "";
            var lastIsExtrapolated = true;
            var lastOn = 0.0;
            var lastOff = 0.0;
            var lastGps = new BasicGeoposition();
            var lastPrimaryDes = "";
            var lastDate = new DateTime();
            double? lastDepth = null;
            if (cisSeverities.ContainsKey(0))
                (lastOn, lastOff, lastPrimaryDes, lastDate, lastDepth, lastIsExtrapolated, lastCisSeverity, lastCisReason, lastGps, lastRegion) = cisSeverities[0];

            var lastPrio = GetPriority(lastCisSeverity, lastDcvgSeverity, PGESeverity.NRI);
            if (!lastIsExtrapolated)
            {
                var firstShapeValues = new string[34];
                firstShapeValues[LABEL] = $"On: {lastOn}, Off: {lastOff}";
                firstShapeValues[STATION] = "0";
                firstShapeValues[DATE] = lastDate.ToShortDateString();
                firstShapeValues[PRIMARYDES] = lastPrimaryDes;
                if (lastDepth.HasValue)
                    firstShapeValues[DEPTH] = lastDepth.Value.ToString("F0");
                firstShapeValues[ECDAREGION] = lastRegion;
                firstShapeValues[LAT] = lastGps.Latitude.ToString("F8");
                firstShapeValues[LON] = lastGps.Longitude.ToString("F8");
                firstShapeValues[ECDACAT] = "Priority " + lastPrio.ToString();
                switch (lastCisSeverity)
                {
                    case PGESeverity.NRI:
                        catString = "NRI";
                        break;
                    case PGESeverity.Minor:
                        catString = "Minor";
                        break;
                    case PGESeverity.Moderate:
                        catString = "Moderate";
                        break;
                    case PGESeverity.Severe:
                        catString = "Severe";
                        break;
                    default:
                        break;
                }
                firstShapeValues[CISCAT] = catString;
                firstShapeValues[ON] = lastOn.ToString("F4");
                firstShapeValues[OFF] = lastOff.ToString("F4");
                CISShapeFileOutput.Add(firstShapeValues);
                if (dcvgSeverities.ContainsKey(0) && DcvgSeries.IsDcvg)
                {
                    firstShapeValues = new string[34];
                    firstShapeValues[LABEL] = $"DCVG: {lastDcvgValue:F1}%";
                    firstShapeValues[STATION] = "0";
                    firstShapeValues[DATE] = lastDate.ToShortDateString();
                    firstShapeValues[DCVGREMOTE] = lastDcvgValue.ToString("F1");
                    firstShapeValues[ECDAREGION] = lastRegion;
                    firstShapeValues[LAT] = lastDcvgGps.Latitude.ToString("F8");
                    firstShapeValues[LON] = lastDcvgGps.Longitude.ToString("F8");
                    firstShapeValues[ECDACAT] = "Priority " + lastPrio.ToString();
                    switch (lastDcvgSeverity)
                    {
                        case PGESeverity.NRI:
                            catString = "NRI";
                            break;
                        case PGESeverity.Minor:
                            catString = "Minor";
                            break;
                        case PGESeverity.Moderate:
                            catString = "Moderate";
                            break;
                        case PGESeverity.Severe:
                            catString = "Severe";
                            break;
                        default:
                            break;
                    }
                    firstShapeValues[DCVGCAT] = catString;
                    IndicationShapeFileOutput.Add(firstShapeValues);
                }
                if (dcvgSeverities.ContainsKey(0) && !DcvgSeries.IsDcvg)
                {
                    firstShapeValues = new string[34];
                    firstShapeValues[LABEL] = $"ACVG: {lastDcvgValue:F2}";
                    firstShapeValues[STATION] = "0";
                    firstShapeValues[DATE] = lastDate.ToShortDateString();
                    firstShapeValues[ECDAREGION] = lastRegion;
                    firstShapeValues[LAT] = lastDcvgGps.Latitude.ToString("F8");
                    firstShapeValues[LON] = lastDcvgGps.Longitude.ToString("F8");
                    firstShapeValues[ECDACAT] = "Priority " + lastPrio.ToString();
                    firstShapeValues[ACVG] = lastDcvgValue.ToString("F2");
                    switch (lastDcvgSeverity)
                    {
                        case PGESeverity.NRI:
                            catString = "NRI";
                            break;
                        case PGESeverity.Minor:
                            catString = "Minor";
                            break;
                        case PGESeverity.Moderate:
                            catString = "Moderate";
                            break;
                        case PGESeverity.Severe:
                            catString = "Severe";
                            break;
                        default:
                            break;
                    }
                    firstShapeValues[ACVGCAT] = catString;
                    IndicationShapeFileOutput.Add(firstShapeValues);
                }

            }

            for (var curFoot = 1; curFoot <= lastFoot; ++curFoot)
            {
                var (curDcvgValue, curDcvgSeverity, curDcvgReason, curDcvgGps) = (0.0, PGESeverity.NRI, "", new BasicGeoposition());
                if (dcvgSeverities.ContainsKey(curFoot))
                    (curDcvgValue, curDcvgSeverity, curDcvgReason, curDcvgGps) = dcvgSeverities[curFoot];

                var (curOn, curOff, curPrimaryDes, curDate, curDepth, curIsExtrapolated, curCisSeverity, curCisReason, curGps, curRegion) = (0.0, 0.0, "", new DateTime(), (double?)null, true, PGESeverity.NRI, "", new BasicGeoposition(), "");
                if (cisSeverities.ContainsKey(curFoot))
                    (curOn, curOff, curPrimaryDes, curDate, curDepth, curIsExtrapolated, curCisSeverity, curCisReason, curGps, curRegion) = cisSeverities[curFoot];

                var curPrio = GetPriority(curCisSeverity, curDcvgSeverity, PGESeverity.NRI);
                if (!curIsExtrapolated)
                {
                    var shapeValues = new string[34];
                    shapeValues[LABEL] = $"On: {curOn}, Off: {curOff}";
                    shapeValues[STATION] = curFoot.ToString("F0");
                    shapeValues[DATE] = curDate.ToShortDateString();
                    shapeValues[PRIMARYDES] = curPrimaryDes;
                    if (curDepth.HasValue)
                        shapeValues[DEPTH] = curDepth.Value.ToString("F0");
                    shapeValues[ECDAREGION] = curRegion;
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
                    switch (curCisSeverity)
                    {
                        case PGESeverity.NRI:
                            catString = "NRI";
                            break;
                        case PGESeverity.Minor:
                            catString = "Minor";
                            break;
                        case PGESeverity.Moderate:
                            catString = "Moderate";
                            break;
                        case PGESeverity.Severe:
                            catString = "Severe";
                            break;
                        default:
                            break;
                    }
                    shapeValues[CISCAT] = catString;
                    shapeValues[ON] = curOn.ToString("F4");
                    shapeValues[OFF] = curOff.ToString("F4");
                    CISShapeFileOutput.Add(shapeValues);
                }
                if (dcvgSeverities.ContainsKey(curFoot))
                {
                    var shapeValues = new string[34];
                    shapeValues[LABEL] = DcvgSeries.IsDcvg ? $"DCVG: {curDcvgValue:F1}%" : $"ACVG: {curDcvgValue:F2}";
                    shapeValues[STATION] = curFoot.ToString("F0");
                    shapeValues[DATE] = curDate.ToShortDateString();                        
                    shapeValues[ECDAREGION] = curRegion;
                    shapeValues[LAT] = curDcvgGps.Latitude.ToString("F8");
                    shapeValues[LON] = curDcvgGps.Longitude.ToString("F8");
                    shapeValues[ECDACAT] = "Priority " + curPrio.ToString();
                    if (DcvgSeries.IsDcvg)
                    {
                        switch (curDcvgSeverity)
                        {
                            case PGESeverity.NRI:
                                catString = "NRI";
                                break;
                            case PGESeverity.Minor:
                                catString = "Minor";
                                break;
                            case PGESeverity.Moderate:
                                catString = "Moderate";
                                break;
                            case PGESeverity.Severe:
                                catString = "Severe";
                                break;
                            default:
                                break;
                        }
                        shapeValues[DCVGCAT] = catString;
                        shapeValues[DCVGREMOTE] = curDcvgValue.ToString("F1");
                    }

                    if (!DcvgSeries.IsDcvg)
                    {
                        switch (curDcvgSeverity)
                        {
                            case PGESeverity.NRI:
                                catString = "NRI";
                                break;
                            case PGESeverity.Minor:
                                catString = "Minor";
                                break;
                            case PGESeverity.Moderate:
                                catString = "Moderate";
                                break;
                            case PGESeverity.Severe:
                                catString = "Severe";
                                break;
                            default:
                                break;
                        }
                        shapeValues[ACVG] = curDcvgValue.ToString("F2");
                        shapeValues[ACVGCAT] = catString;
                    }
                    IndicationShapeFileOutput.Add(shapeValues);
                }
                if (curCisSeverity != lastCisSeverity || curCisReason != lastCisReason || curDcvgSeverity != lastDcvgSeverity || curDcvgReason != lastDcvgReason || curRegion != lastRegion)
                {
                    var fullReason = lastCisReason;
                    if (string.IsNullOrWhiteSpace(fullReason))
                        fullReason = lastDcvgReason;
                    else
                        fullReason += ". " + lastDcvgReason;
                    output.Add((lastStartFoot, curFoot, lastCisSeverity, lastDcvgSeverity, lastRegion, lastPrio, fullReason.Trim()));
                    lastStartFoot = curFoot;
                    lastCisReason = curCisReason;
                    lastCisSeverity = curCisSeverity;
                    lastDcvgReason = curDcvgReason;
                    lastDcvgSeverity = curDcvgSeverity;
                    lastRegion = curRegion;
                    lastPrio = GetPriority(lastCisSeverity, lastDcvgSeverity, PGESeverity.NRI);
                }
                lastEndFoot = curFoot;
            }

            var finalPrio = GetPriority(lastCisSeverity, lastDcvgSeverity, PGESeverity.NRI);
            var finalReason = lastCisReason;
            if (string.IsNullOrWhiteSpace(finalReason))
                finalReason = lastDcvgReason;
            else
                finalReason += ". " + lastDcvgReason;
            output.Add((lastStartFoot, lastEndFoot, lastCisSeverity, lastDcvgSeverity, lastRegion, finalPrio, finalReason));

            return output;
        }

        public override List<(double Start, double End, Color Color)> GetColorBounds(PageInformation page)
        {
            var cisSeverities = new Dictionary<int, PGESeverity>();
            if (CisSeries != null)
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
            var dcvgSeverities = new Dictionary<int, PGESeverity>();
            if (DcvgSeries != null)
            {
                foreach (var (foot, _, _, severity, _, _) in DcvgSeries.Data)
                {
                    if (foot < page.StartFootage)
                        continue;
                    if (foot > page.EndFootage)
                        break;
                    if (!dcvgSeverities.ContainsKey((int)foot))
                        dcvgSeverities.Add((int)foot, severity);
                }
            }


            var colors = new List<(double Start, double End, Color Color)>();
            (double Start, double end, int Prio)? prevData = null;
            (double Footage, int Prio)? lastData = null;
            for (var curFoot = (int)page.StartFootage; curFoot <= page.EndFootage; ++curFoot)
            {
                PGESeverity curSeverity;
                var cis = cisSeverities.TryGetValue(curFoot, out curSeverity) ? curSeverity : PGESeverity.NRI;
                var dcvg = dcvgSeverities.TryGetValue(curFoot, out curSeverity) ? curSeverity : PGESeverity.NRI;
                var acvg = PGESeverity.NRI;

                var prio = GetPriority(cis, dcvg, acvg);
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

        private int GetPriority(PGESeverity cis, PGESeverity dcvg, PGESeverity acvg)
        {
            if (cis == PGESeverity.Moderate && dcvg == PGESeverity.Severe)
                return 1;
            if (cis == PGESeverity.Severe && (dcvg == PGESeverity.Severe || dcvg == PGESeverity.Moderate))
                return 1;

            if (cis == PGESeverity.NRI && dcvg == PGESeverity.Severe)
                return 2;
            if (cis == PGESeverity.Minor && dcvg == PGESeverity.Severe)
                return 2;
            if (cis == PGESeverity.Moderate && (dcvg == PGESeverity.Moderate || dcvg == PGESeverity.Minor))
                return 2;
            if (cis == PGESeverity.Severe && (dcvg == PGESeverity.Minor || dcvg == PGESeverity.NRI))
                return 2;

            if (cis == PGESeverity.NRI && dcvg == PGESeverity.Moderate)
                return 3;
            if (cis == PGESeverity.Minor)
                return 3;
            if (cis == PGESeverity.Moderate && dcvg == PGESeverity.NRI)
                return 3;

            return 4;
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
