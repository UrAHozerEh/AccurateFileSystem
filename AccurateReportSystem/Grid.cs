using Microsoft.Graphics.Canvas;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Foundation;

namespace AccurateReportSystem
{
    class Grid : SplitContainer
    {

        public Grid(int[] counts) : base(SplitContainerOrientation.Horizontal)
        {
            for(int c = 0; c < counts.Length; ++c)
            {
                var column = new SplitContainer(SplitContainerOrientation.Vertical);
                for(int r = 0; r < counts[c]; ++r)
                {
                    column.AddContainer(null);
                }
                AddContainer(column);
            }
        }

        public SplitContainerMeasurement this[int column, int row]
        {
            get
            {
                var colValue = this[column];
                if (colValue.Container is SplitContainer colContainer)
                {
                    return colContainer[row];
                }
                throw new Exception();

            }
        }
    }
}
