using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI;

namespace AccurateReportSystem
{
    public class OnOff850ExceptionChartSeries : ColorBoxExceptionSeries
    {
        public Color OffBelow850Color { get; set; } = Colors.Yellow;
        public Color OnBelow850Color { get; set; } = Colors.Red;
        public Color BothAbove850Color { get; set; } = Colors.Green;
        public override int NumberOfValues => 4;

        public OnOff850ExceptionChartSeries(List<(double Footage, double On, double Off)> data, LegendInfo masterLegendInfo, YAxesInfo masterYAxesInfo) : base(data, masterLegendInfo, masterYAxesInfo)
        {

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

        protected override Color CheckValues(List<double> values)
        {
            if (values[0] > -0.85)
                return OnBelow850Color;
            if (values[1] > -0.85)
                return OffBelow850Color;
            return BothAbove850Color;
        }
    }
}
