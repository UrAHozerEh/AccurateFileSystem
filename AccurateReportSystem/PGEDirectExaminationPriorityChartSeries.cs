﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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

        public PGEDirectExaminationPriorityChartSeries(Chart chart, CisSeries cisSeries = null, DcvgSeries dcvgSeries = null) : base(chart.LegendInfo, chart.YAxesInfo)
        {
            CisSeries = cisSeries;
            DcvgSeries = dcvgSeries;
        }

        public List<(double Start, double End, PGESeverity CisSeverity, PGESeverity DcvgSeverity, int Overall, string Comments)> GetReportQ()
        {
            var output = new List<(double, double, PGESeverity, PGESeverity, int, string)>();
            var lastFoot = 0.0;

            var cisSeverities = new Dictionary<int, (PGESeverity, string)>();
            if (CisSeries != null)
            {
                foreach (var (foot, _, _, _, _, severity, reason) in CisSeries.Data)
                {
                    if (foot > lastFoot)
                        lastFoot = foot;
                    cisSeverities.Add((int)foot, (severity, reason));
                }
            }
            var dcvgSeverities = new Dictionary<int, (PGESeverity, string)>();
            if (DcvgSeries != null)
            {
                foreach (var (_, foot, _, severity, reason) in DcvgSeries.Data)
                {
                    if (foot > lastFoot)
                        lastFoot = foot;
                    if (!dcvgSeverities.ContainsKey((int)foot))
                        dcvgSeverities.Add((int)foot, (severity, reason));
                }
            }
            var lastStartFoot = 0;
            var lastEndFoot = 0;
            var lastDcvgSeverity = PGESeverity.NRI;
            var lastDcvgReason = "";
            if (dcvgSeverities.ContainsKey(0))
                (lastDcvgSeverity, lastDcvgReason) = dcvgSeverities[0];

            var lastCisSeverity = PGESeverity.NRI;
            var lastCisReason = "";
            if (cisSeverities.ContainsKey(0))
                (lastCisSeverity, lastCisReason) = cisSeverities[0];

            for (var curFoot = 1; curFoot <= lastFoot; ++curFoot)
            {
                var (curDcvgSeverity, curDcvgReason) = (PGESeverity.NRI, "");
                if (dcvgSeverities.ContainsKey(curFoot))
                    (curDcvgSeverity, curDcvgReason) = dcvgSeverities[curFoot];

                var (curCisSeverity, curCisReason) = (PGESeverity.NRI, "");
                if (cisSeverities.ContainsKey(curFoot))
                    (curCisSeverity, curCisReason) = cisSeverities[curFoot];

                if (curCisSeverity != lastCisSeverity || curCisReason != lastCisReason || curDcvgSeverity != lastDcvgSeverity || curDcvgReason != lastDcvgReason)
                {
                    var lastPrio = GetPriority(lastCisSeverity, lastDcvgSeverity, PGESeverity.NRI);
                    var fullReason = lastCisReason;
                    if (string.IsNullOrWhiteSpace(fullReason))
                        fullReason = lastDcvgReason;
                    else
                        fullReason += ". " + lastDcvgReason;
                    output.Add((lastStartFoot, lastEndFoot, lastCisSeverity, lastDcvgSeverity, lastPrio, fullReason.Trim()));
                    lastStartFoot = curFoot;
                    lastCisReason = curCisReason;
                    lastCisSeverity = curCisSeverity;
                    lastDcvgReason = curDcvgReason;
                    lastDcvgSeverity = curDcvgSeverity;
                }
                lastEndFoot = curFoot;
            }

            var finalPrio = GetPriority(lastCisSeverity, lastDcvgSeverity, PGESeverity.NRI);
            var finalReason = lastCisReason;
            if (string.IsNullOrWhiteSpace(finalReason))
                finalReason = lastDcvgReason;
            else
                finalReason += ". " + lastDcvgReason;
            output.Add((lastStartFoot, lastEndFoot, lastCisSeverity, lastDcvgSeverity, finalPrio, finalReason));

            return output;
        }

        public override List<(double Start, double End, Color Color)> GetColorBounds(PageInformation page)
        {
            var cisSeverities = new Dictionary<int, PGESeverity>();
            if (CisSeries != null)
            {
                foreach (var (foot, _, _, _, _, severity, _) in CisSeries.Data)
                {
                    if (foot < page.StartFootage)
                        continue;
                    if (foot > page.EndFootage)
                        break;
                    cisSeverities.Add((int)foot, severity);
                }
            }
            var dcvgSeverities = new Dictionary<int, PGESeverity>();
            if (DcvgSeries != null)
            {
                foreach (var (foot, _, _, severity, _) in DcvgSeries.Data)
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
                //TODO: Add acvg

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