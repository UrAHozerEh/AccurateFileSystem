using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI;

namespace AccurateReportSystem
{
    public class PolarizationExceptionChartSeries : ColorBoxExceptionSeries
    {
        public Color PolarizationAbove { get; set; } = Colors.Green;
        public Color PolarizationBelow { get; set; } = Colors.Red;
        public double PolarizationRequirement = -0.100f;
        public override int NumberOfValues => 3;

        public PolarizationExceptionChartSeries(IEnumerable<(double Footage, double Pol)> data,
            LegendInfo masterLegendInfo, YAxesInfo masterYAxesInfo) : base(data, masterLegendInfo, masterYAxesInfo)
        {
        }

        protected override List<(string, Color)> LegendInfo()
        {
            var info = new List<(string, Color)>
            {
                ("Pol > 100mV", PolarizationAbove),
                ("Pol < 100mV", PolarizationBelow),
                ("No Data", NoDataColor)
            };
            return info;
        }

        protected override Color CheckValues(List<double> values)
        {
            return values[0] > PolarizationRequirement ? PolarizationBelow : PolarizationAbove;
        }
    }
}
