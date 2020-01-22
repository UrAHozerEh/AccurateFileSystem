using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI;

namespace AccurateReportSystem
{
    public class PgeDcvgIndicationChartSeries : ExceptionsChartSeries
    {
        public List<(double Footage, double ActualFoot, double Percent, PGESeverity Severity, string Reason)> Data { get; set; }
        public Color MinorColor { get; set; } = Colors.Blue;
        public Color ModerateColor { get; set; } = Colors.Green;
        public Color SevereColor { get; set; } = Colors.Red;
        public double MinimumFeet { get; set; } = 3;
        public bool IsDcvg { get; set; }

        public PgeDcvgIndicationChartSeries(List<(double, double)> data, Chart chart, bool isDcvg) : this(data, chart.LegendInfo, chart.YAxesInfo, isDcvg)
        {

        }

        public PgeDcvgIndicationChartSeries(List<(double, double)> data, LegendInfo masterLegendInfo, YAxesInfo masterYAxesInfo, bool isDcvg) : base(masterLegendInfo, masterYAxesInfo)
        {
            Data = new List<(double Footage, double ActualFoot, double Percent, PGESeverity Severity, string)>();
            IsDcvg = isDcvg;
            foreach (var (foot, percent) in data)
            {
                if (!isDcvg)
                {
                    //ACVG
                    var severity = PGESeverity.NRI;
                    var reason = "Individual normalized ACVG indications are less than 25";
                    if (percent >= 75)
                    {
                        severity = PGESeverity.Severe;
                        reason = "Individual normalized ACVG indications are greater than or equal to 75";
                    }
                    else if (percent >= 50)
                    {
                        severity = PGESeverity.Moderate;
                        reason = "Individual normalized ACVG indications are greater than or equal to 50 and less than 75";
                    }
                    else if (percent >= 25)
                    {
                        severity = PGESeverity.Minor;
                        reason = "Individual normalized ACVG indications are greater than or equal to 25 and less than 50";
                    }
                    for (double curFoot = foot - MinimumFeet; curFoot <= foot + MinimumFeet; ++curFoot)
                    {
                        Data.Add((curFoot, foot, percent, severity, reason));
                    }
                }
                else
                {
                    //DCVG
                    var severity = PGESeverity.NRI;
                    var reason = "DCVG % IR is greater than 0 and less than or equal to 15";
                    if (percent > 60)
                    {
                        severity = PGESeverity.Severe;
                        reason = "DCVG % IR is greater than 60";
                    }
                    else if (percent > 35)
                    {
                        severity = PGESeverity.Moderate;
                        reason = "DCVG % IR is greater than 35 and less than or equal to 60";
                    }
                    else if (percent > 15)
                    {
                        severity = PGESeverity.Minor;
                        reason = "DCVG % IR is greater than 15 and less than or equal to 35";
                    }
                    for (double curFoot = foot - MinimumFeet; curFoot <= foot + MinimumFeet; ++curFoot)
                    {
                        Data.Add((curFoot, foot, percent, severity, reason));
                    }
                }
            }
        }

        public override int NumberOfValues => 3;

        public override List<(double Start, double End, Color Color)> GetColorBounds(PageInformation page)
        {
            var colors = new List<(double Start, double End, Color Color)>();
            (double Start, double end, PGESeverity Severity)? prevData = null;
            for (int i = 0; i < Data.Count; ++i)
            {
                var (curFoot, _, _, severity, _) = Data[i];
                if (curFoot < page.StartFootage)
                {
                    continue;
                }
                if (curFoot > page.EndFootage)
                {
                    break;
                }
                if (!prevData.HasValue)
                {
                    prevData = (curFoot, curFoot, severity);
                    continue;
                }

                var (prevStart, prevEnd, prevSeverity) = prevData.Value;
                if (curFoot - prevEnd > MinimumFeet)
                {
                    var prevColor = GetColor(prevSeverity);
                    if (prevColor.HasValue)
                        colors.Add((prevStart, prevEnd, prevColor.Value));
                    prevData = (curFoot, curFoot, severity);
                    continue;
                }
                if (severity == prevSeverity)
                {
                    prevData = (prevStart, curFoot, prevSeverity);
                }
                else
                {
                    var middleFoot = (prevEnd + curFoot) / 2;
                    var prevColor = GetColor(prevSeverity);
                    if (prevColor.HasValue)
                        colors.Add((prevStart, middleFoot, prevColor.Value));
                    prevData = (middleFoot, curFoot, severity);
                }
            }
            if (prevData.HasValue)
            {
                var (prevStart, prevEnd, prevSeverity) = prevData.Value;
                var prevColor = GetColor(prevSeverity);
                if (prevColor.HasValue)
                    colors.Add((prevStart, prevEnd, prevColor.Value));
            }

            return colors;
        }

        public Color? GetColor(PGESeverity severity)
        {
            switch (severity)
            {
                case PGESeverity.Minor:
                    return MinorColor;
                case PGESeverity.Moderate:
                    return ModerateColor;
                case PGESeverity.Severe:
                    return SevereColor;
                default:
                    return null;
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
}
