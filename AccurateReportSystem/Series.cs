using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI;
using AccurateFileSystem;

namespace AccurateReportSystem
{
    public abstract class Series
    {
        public string Name { get; set; }
        public abstract bool IsDrawnInLegend { get; set; }
        public abstract Color LegendNameColor { get; set; }
    }
}
