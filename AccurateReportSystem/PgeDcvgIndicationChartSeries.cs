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
        public List<(double Footage, double Percent, PGESeverity Severity)> Data { get; set; }
        public Color MinorColor { get; set; } = Colors.Blue;
        public Color ModerateColor { get; set; } = Colors.Green;
        public Color SevereColor { get; set; } = Colors.Red;
        public double MinimumFeet { get; set; } = 3;

        public PgeDcvgIndicationChartSeries(List<(double, double)> data, Chart chart) : this(data, chart.LegendInfo, chart.YAxesInfo)
        {

        }

        public PgeDcvgIndicationChartSeries(List<(double, double)> data, LegendInfo masterLegendInfo, YAxesInfo masterYAxesInfo) : base(masterLegendInfo, masterYAxesInfo)
        {
            Data = new List<(double Footage, double Percent, PGESeverity Severity)>();
            foreach (var (foot, percent) in data)
            {
                var severity = PGESeverity.NRI;
                if (percent >= 61)
                    severity = PGESeverity.Severe;
                else if (percent >= 36)
                    severity = PGESeverity.Moderate;
                else if (percent >= 16)
                    severity = PGESeverity.Minor;
                for(double curFoot = foot - MinimumFeet; curFoot <= foot + MinimumFeet; ++curFoot)
                {
                    Data.Add((curFoot, percent, severity));
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
                var (curFoot, _, severity) = Data[i];
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
                if(curFoot - prevEnd > MinimumFeet)
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
