using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI;

namespace AccurateReportSystem
{
    public abstract class ColorBoxExceptionSeries : ExceptionsChartSeries
    {
        public Color NoDataColor { get; set; } = Colors.Gray;
        public double MaxDistance { get; set; } = 10.0;
        public List<(double Footage, List<double> values)> Data { get; set; }
        

        protected ColorBoxExceptionSeries(IEnumerable<(double Footage, double Value)> data, LegendInfo masterLegendInfo, YAxesInfo masterYAxesInfo) : base(masterLegendInfo, masterYAxesInfo)
        {
            Data = data.Select(d => (d.Footage, new List<double> { d.Value })).ToList();
        }

        protected ColorBoxExceptionSeries(IEnumerable<(double Footage, double On, double Off)> data, LegendInfo masterLegendInfo, YAxesInfo masterYAxesInfo) : base(masterLegendInfo, masterYAxesInfo)
        {
            Data = data.Select(d => (d.Footage, new List<double> { d.On, d.Off })).ToList();
        }

        protected ColorBoxExceptionSeries(IEnumerable<(double Footage, List<double>)> data, LegendInfo masterLegendInfo, YAxesInfo masterYAxesInfo) : base(masterLegendInfo, masterYAxesInfo)
        {
            Data = data.ToList();
        }

        protected abstract Color CheckValues(List<double> values);

        public override List<(double Start, double End, Color Color)> GetColorBounds(PageInformation page)
        {
            var colors = new List<(double Start, double End, Color Color)>();
            (double Start, double end, Color color)? prevData = null;
            (double Footage, Color Color)? firstData = null;
            (double Footage, Color Color)? lastData = null;
            for (var i = 0; i < Data.Count; ++i)
            {
                var (curFoot, values) = Data[i];
                var curColor = CheckValues(values);
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

            if (colors.Count == 0)
            {
                colors.Add((page.StartFootage, page.EndFootage, NoDataColor));
            }

            return colors;
        }

        public List<(double Start, double End, Color Color)> GetAllColorBounds()
        {
            var start = Data.First().Footage;
            var end = Data.Last().Footage;
            var allPage = new PageInformation()
            {
                StartFootage = start,
                EndFootage = end,
                Overlap = 0,
                PageNumber = 1,
                TotalPages = 1
            };
            return GetColorBounds(allPage);
        }
    }
}
