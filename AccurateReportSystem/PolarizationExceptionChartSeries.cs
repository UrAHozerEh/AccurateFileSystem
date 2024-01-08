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
        public double PolarizationRequirement { get; set; }
        public override int NumberOfValues => 3;

        public PolarizationExceptionChartSeries(IEnumerable<(double Footage, double Pol)> data,
            LegendInfo masterLegendInfo, YAxesInfo masterYAxesInfo, double rquirement = -0.100f) : base(data, masterLegendInfo, masterYAxesInfo)
        {
            PolarizationRequirement = rquirement;
        }

        protected override List<(string, Color)> LegendInfo()
        {
            var mv = PolarizationRequirement * 1000;
            var info = new List<(string, Color)>
            {
                ($"Pol > {Math.Abs(mv):F0}mV", PolarizationAbove),
                ($"Pol < {Math.Abs(mv):F0}mV", PolarizationBelow),
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
