using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI;

namespace AccurateReportSystem
{
    public class OnOffPassingRangeExceptionChartSeries : ColorBoxExceptionSeries
    {
        public Color OffBelowColor { get; set; } = Colors.Red;
        public Color OnBelowColor { get; set; } = Colors.Yellow;
        public Color PassingColor { get; set; } = Colors.Green;
        public Color OffAboveColor { get; set; } = Colors.Yellow;
        public Color OnAboveColor { get; set; } = Colors.Red;
        public double? MinimumValue { get; set; } = null;
        public double? MaximumValue { get; set; } = null;
        public string LegendNumberFormat { get; set; } = "F3";
        public override int NumberOfValues => 2 + (MinimumValue.HasValue ? 2 : 0) + (MaximumValue.HasValue ? 2 : 0);

        public OnOffPassingRangeExceptionChartSeries(List<(double Footage, double On, double Off)> data, LegendInfo masterLegendInfo, YAxesInfo masterYAxesInfo, double? minValue = null, double? maxValue = null) : base(data, masterLegendInfo, masterYAxesInfo)
        {
            MinimumValue = minValue;
            MaximumValue = maxValue;
        }

        protected override List<(string, Color)> LegendInfo()
        {
            var info = new List<(string, Color)>
            {
                ("Passing", PassingColor),
                ("No Data", NoDataColor)
            };
            if (MinimumValue.HasValue)
            {
                info.Add(($"Off > {MinimumValue.Value.ToString(LegendNumberFormat)}", OffBelowColor));
                info.Add(($"On > {MinimumValue.Value.ToString(LegendNumberFormat)}", OnBelowColor));
            }
            if (MaximumValue.HasValue)
            {
                info.Add(($"Off < {MaximumValue.Value.ToString(LegendNumberFormat)}", OffAboveColor));
                info.Add(($"On < {MaximumValue.Value.ToString(LegendNumberFormat)}", OnAboveColor));
            }
            return info;
        }

        protected override Color CheckValues(List<double> values)
        {
            if (MaximumValue.HasValue)
            {
                if (values[0] > MaximumValue.Value)
                    return OnAboveColor;
                if (values[1] > MaximumValue.Value)
                    return OffAboveColor;
            }
            if (MinimumValue.HasValue)
            {
                if (values[0] < MinimumValue.Value)
                    return OnBelowColor;
                if (values[1] < MinimumValue.Value)
                    return OffBelowColor;
            }
            return PassingColor;
        }
    }
}
