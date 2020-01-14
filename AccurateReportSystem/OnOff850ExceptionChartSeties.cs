using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI;

namespace AccurateReportSystem
{
    public class OnOff850ExceptionChartSeries : ExceptionsChartSeries
    {
        public Color OffBelow850Color { get; set; } = Colors.Yellow;
        public Color OnBelow850Color { get; set; } = Colors.Red;
        public Color BothAbove850Color { get; set; } = Colors.Green;
        public Color NoDataColor { get; set; } = Colors.Gray;
        public double MaxDistance { get; set; } = 10.0;
        public override int NumberOfValues => 4;
        public List<(double Footage, double On, double Off)> Data { get; set; }

        public OnOff850ExceptionChartSeries(List<(double Footage, double On, double Off)> data, LegendInfo masterLegendInfo, YAxesInfo masterYAxesInfo) : base(masterLegendInfo, masterYAxesInfo)
        {
            Data = data;
        }

        protected override List<(string, Color)> LegendInfo()
        {
            var info = new List<(string, Color)>
            {
                ("Off < 850", OffBelow850Color),
                ("On < 850", OnBelow850Color),
                ("Both > 850", BothAbove850Color),
                ("No Data", NoDataColor)
            };
            return info;
        }

        public override List<(double Start, double End, Color Color)> GetColorBounds(PageInformation page)
        {
            var colors = new List<(double Start, double End, Color Color)>();
            (double Start, double end, Color color)? prevData = null;
            (double Footage, Color Color)? firstData = null;
            (double Footage, Color Color)? lastData = null;
            for (int i = 0; i < Data.Count; ++i)
            {
                var (curFoot, on, off) = Data[i];
                var curColor = CheckValues(on, off);
                if (curFoot < page.StartFootage)
                {
                    firstData = (curFoot, curColor);
                    continue;
                }
                if (curFoot > page.EndFootage)
                {
                    lastData = (curFoot, curColor);
                    break;
                }
                if (!prevData.HasValue)
                {
                    prevData = (curFoot, curFoot, curColor);
                    if (curFoot != page.StartFootage && firstData.HasValue)
                    {
                        var (firstFoot, firstColor) = firstData.Value;
                        if (curFoot - firstFoot > MaxDistance)
                        {
                            colors.Add((firstFoot, curFoot, NoDataColor));
                        }
                        else if (firstColor == curColor)
                        {
                            prevData = (firstFoot, curFoot, curColor);
                        }
                        else
                        {
                            colors.Add((firstFoot, curFoot, firstColor));
                        }
                    }
                    continue;
                }

                var (prevStart, prevEnd, prevColor) = prevData.Value;


                if (curFoot - prevEnd > MaxDistance)
                {
                    if (prevStart != prevEnd)
                        colors.Add((prevStart, prevEnd, prevColor));
                    else
                        colors.Add((prevStart, prevStart + (MaxDistance / 2), prevColor));
                    colors.Add((prevEnd, curFoot, NoDataColor));
                    prevData = (curFoot, curFoot, curColor);
                }
                else if (curColor.Equals(prevColor))
                {
                    prevData = (prevStart, curFoot, prevColor);
                }
                else
                {
                    var middleFoot = (prevEnd + curFoot) / 2;
                    colors.Add((prevStart, middleFoot, prevColor));
                    prevData = (middleFoot, curFoot, curColor);
                }
            }
            if (prevData.HasValue)
            {
                var (prevStart, prevEnd, prevColor) = prevData.Value;
                if (lastData.HasValue)
                {
                    var (lastFoot, lastColor) = lastData.Value;
                    if (lastFoot - prevEnd > MaxDistance)
                    {
                        colors.Add((prevEnd, lastFoot, NoDataColor));
                    }
                    else if (prevColor == lastColor)
                    {
                        prevEnd = lastFoot;
                    }
                    else
                    {
                        colors.Add((prevEnd, lastFoot, lastColor));
                    }
                }
                if (prevStart != prevEnd)
                    colors.Add((prevStart, prevEnd, prevColor));
                else
                    colors.Add((prevStart, prevStart + (MaxDistance / 2), prevColor));
            }

            return colors;
        }

        private Color CheckValues(double on, double off)
        {
            if (on > -0.85)
                return OnBelow850Color;
            if (off > -0.85)
                return OffBelow850Color;
            return BothAbove850Color;
        }
    }
}
