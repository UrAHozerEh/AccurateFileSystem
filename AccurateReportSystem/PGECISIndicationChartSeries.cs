using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI;

namespace AccurateReportSystem
{
    public class PGECISIndicationChartSeries : ExceptionsChartSeries
    {
        public override int NumberOfValues => 3;
        public Color MinorColor { get; set; } = Colors.Blue;
        public Color ModerateColor { get; set; } = Colors.Green;
        public Color SevereColor { get; set; } = Colors.Red;
        public List<(double Footage, double On, double Off, bool IsExtrapolated, double Baseline, PGESeverity Severity)> Data { get; set; }
        public double MinimumFeet { get; set; } = 5;

        public PGECISIndicationChartSeries(List<(double, double)> onData, List<(double, double)> offData, Chart chart) : base(chart.LegendInfo, chart.YAxesInfo)
        {
            if (onData.Count != offData.Count)
                throw new ArgumentException();
            var combinedData = new List<(double Footage, double On, double Off)>();
            for (int i = 0; i < onData.Count; ++i)
            {
                var (footOn, on) = onData[i];
                var (footOff, off) = offData[i];
                if (footOn != footOff)
                    throw new ArgumentException();
                combinedData.Add((footOn, on, off));
            }
            Data = ExtrapolateData(combinedData);
        }

        public PGECISIndicationChartSeries(List<(double Footage, double On, double Off)> data, LegendInfo masterLegendInfo, YAxesInfo masterYAxesInfo) : base(masterLegendInfo, masterYAxesInfo)
        {
            Data = ExtrapolateData(data);
        }

        private List<(double Footage, double On, double Off, bool IsExtrapolated, double Baseline, PGESeverity Severity)> ExtrapolateData(List<(double Footage, double On, double Off)> data)
        {

            var extrapolatedData = new List<(double Footage, double On, double Off, bool IsExtrapolated)>();

            for (int i = 0; i < data.Count - 1; ++i)
            {
                var (startFoot, startOn, startOff) = data[i];
                var (endFoot, endOn, endOff) = data[i + 1];
                extrapolatedData.Add((startFoot, startOn, startOff, false));
                var dist = endFoot - startFoot;
                var onDiff = endOn - startOn;
                var offDiff = endOff - startOff;
                var onPerFoot = onDiff / dist;
                var offPerFoot = offDiff / dist;
                for (int offset = 1; offset < dist; ++offset)
                {
                    var newOn = onPerFoot * offset + startOn;
                    var newOff = offPerFoot * offset + startOff;
                    var newFoot = startFoot + offset;
                    extrapolatedData.Add((newFoot, newOn, newOff, true));
                }
            }
            var (foot, on, off) = data.Last();
            extrapolatedData.Add((foot, on, off, false));
            bool extrapolated;
            var output = new List<(double, double, double, bool, double, PGESeverity)>(extrapolatedData.Count);
            if (foot <= 200)
            {
                var average = extrapolatedData.Average(value => value.Off);
                for (int i = 0; i < extrapolatedData.Count; ++i)
                {
                    (foot, on, off, extrapolated) = extrapolatedData[i];
                    var changeInBaseline = Math.Abs(off - average);
                    PGESeverity severity = GetSeverity(off, average);
                    output.Add((foot, on, off, extrapolated, average, severity));
                }
                return output;
            }
            var curAverages = new List<double>(extrapolatedData.Count);
            for (int i = 0; i < extrapolatedData.Count; ++i)
            {
                (foot, on, off, extrapolated) = extrapolatedData[i];
                var within100 = extrapolatedData.Where(value => Within100(foot, value.Footage));
                var average = within100.Average(value => value.Off);
                curAverages.Add(average);
            }
            var curBaselines = Enumerable.Repeat(double.NaN, extrapolatedData.Count).ToList();
            for (int center = 0; center < extrapolatedData.Count; ++center)
            {
                var start = Math.Max(center - 105, 0);
                var end = Math.Min(center + 105, extrapolatedData.Count - 1);
                var (centerFoot, centerOn, centerOff, centerExtrapolated) = extrapolatedData[center];
                var centerAverage = curAverages[center];
                for (int i = start; i <= end; ++i)
                {
                    var (curFoot, _, _, _) = extrapolatedData[i];
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
                PGESeverity severity = GetSeverity(centerOff, baseline);
                output.Add((centerFoot, centerOn, centerOff, centerExtrapolated, baseline, severity));
            }
            return output;
        }

        private PGESeverity GetSeverity(double off, double baseline)
        {
            var changeInBaseline = Math.Abs(off - baseline);
            if (off > -0.5 || (off > -0.7 && changeInBaseline >= 0.2))
                return PGESeverity.Severe;
            if (off > -0.7 || (off > -0.85 && changeInBaseline >= 0.2))
                return PGESeverity.Moderate;
            if (off > -0.85 || changeInBaseline >= 0.2)
                return PGESeverity.Minor;
            return PGESeverity.NRI;
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
                var (curFoot, _, _, _, _, severity) = Data[i];
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
