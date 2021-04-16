using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI;

namespace AccurateReportSystem
{
    public class PolarizationChartSeries : ExceptionsChartSeries
    {
        public Color PolarizationBelowColor { get; set; } = Colors.Red;
        public Color PolarizationAboveColor { get; set; } = Colors.Green;
        public Color NoDataColor { get; set; } = Colors.Gray;
        public double MaxDistance { get; set; } = 10.0;
        public override int NumberOfValues => 3;
        public List<(double Footage, double On, double Off)> OnOffData { get; set; }
        public List<(double Footage, double On, double Off)> DepolData { get; set; }
        public List<(double Footage, double Polarization)> PolarizationData { get; private set; }

        public PolarizationChartSeries(List<(double Footage, double On, double Off)> onOffData, List<(double Footage, double On, double Off)> depolData, LegendInfo masterLegendInfo, YAxesInfo masterYAxesInfo) : base(masterLegendInfo, masterYAxesInfo)
        {
            OnOffData = onOffData;
            DepolData = depolData;
            PolarizationData = GetPolarizationData();
        }

        protected override List<(string, Color)> LegendInfo()
        {
            var info = new List<(string, Color)>
            {
                ("Pol > 100", PolarizationAboveColor),
                ("Pol < 100", PolarizationBelowColor),
                ("No Data", NoDataColor)
            };
            return info;
        }

        public List<(double Footage, double Polarization)> GetPolarizationData()
        {
            var output = new List<(double Footage, double Polarization)>();

            foreach (var curData in OnOffData)
            {
                var off = curData.Off;
                var otherData = GetValue(curData.Footage, DepolData);
                if (otherData == null)
                    continue;
                var depol = otherData.Value.On;
                var pol = off - depol;
                output.Add((curData.Footage, pol));
            }
            foreach (var curData in DepolData)
            {
                var depol = curData.On;
                var otherData = GetValue(curData.Footage, OnOffData);
                if (otherData == null)
                    continue;
                var off = GetValue(curData.Footage, OnOffData).Value.Off;
                var pol = off - depol;
                output.Add((curData.Footage, pol));
            }
            output.Sort((val1, val2) => val1.Footage.CompareTo(val2.Footage));
            return output;
        }

        private (double On, double Off)? GetValue(double footage, List<(double Footage, double On, double Off)> data, double maxDistance = 30)
        {
            var min = 0;
            var max = data.Count - 1;
            var curIndex = -1;
            while (true)
            {
                curIndex = ((max - min) / 2) + min;
                var curFootage = data[curIndex].Footage;
                if (curFootage == footage || max - min == 1)
                    break;
                else if (curFootage > footage)
                    max = curIndex;
                else
                    min = curIndex;
            }

            if (curIndex == -1)
                throw new ArgumentException();
            var curValue = data[curIndex];
            if (curValue.Footage == footage)
                return (curValue.On, curValue.Off);
            else if (curIndex == data.Count - 1)
                return (curValue.On, curValue.Off);
            else
            {
                var nextValue = data[curIndex + 1];
                var footDist = nextValue.Footage - curValue.Footage;
                if (footDist > maxDistance)
                    return null;
                var onDiff = nextValue.On - curValue.On;
                var offDiff = nextValue.Off - curValue.Off;

                var onSlope = onDiff / footDist;
                var offSlope = offDiff / footDist;

                var footDiff = footage - curValue.Footage;
                var newOn = curValue.On + (onSlope * footDiff);
                var newOff = curValue.Off + (offSlope * footDiff);
                return (newOn, newOff);
            }
        }

        public override List<(double Start, double End, Color Color)> GetColorBounds(PageInformation page)
        {
            var colors = new List<(double Start, double End, Color Color)>();
            (double Start, double end, Color color)? prevData = null;
            (double Footage, Color Color)? firstData = null;
            (double Footage, Color Color)? lastData = null;
            for (int i = 0; i < PolarizationData.Count; ++i)
            {
                var (curFoot, pol) = PolarizationData[i];
                var curColor = CheckValues(pol);
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

        private Color CheckValues(double pol)
        {
            if (pol < -0.1)
                return PolarizationAboveColor;
            return PolarizationBelowColor;
        }
    }

}
