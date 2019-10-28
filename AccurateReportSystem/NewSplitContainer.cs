using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AccurateReportSystem
{
    //TODO: Rename file to just split container
    public class SplitContainer
    {
        public SplitContainerOrientation Orientation { get; set; }
        // Was probably gonna have 3 different lists. Items that have their own size, items that are a specific size, everything else that should split the remainder. 

        public SplitContainer(SplitContainerOrientation orientation)
        {
            Orientation = orientation;
        }
    }

    public enum SplitContainerOrientation
    {
        Horizontal, Vertical
    }
}