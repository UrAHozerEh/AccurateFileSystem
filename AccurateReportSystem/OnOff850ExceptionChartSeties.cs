using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI;

namespace AccurateReportSystem
{
    public class OnOff850ExceptionChartSeries : OnOffPassingRangeExceptionChartSeries
    {
        public OnOff850ExceptionChartSeries(List<(double Footage, double On, double Off)> data, LegendInfo masterLegendInfo, YAxesInfo masterYAxesInfo) : base(data, masterLegendInfo, masterYAxesInfo, maxValue: -0.850)
        {

        }
    }
}
