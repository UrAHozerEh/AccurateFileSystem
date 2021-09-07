using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AccurateReportSystem.AccurateDrawingDevices;
using Microsoft.Graphics.Canvas;
using Windows.Foundation;
using Windows.UI;

namespace AccurateReportSystem
{
    //TODO: Rename file to just split container
    public class SplitContainer : Container
    {
        private bool IsHorizontal => Orientation == SplitContainerOrientation.Horizontal;
        public SplitContainerOrientation Orientation { get; set; }
        // Was probably gonna have 3 different lists. Items that have their own size, items that are a specific size, everything else that should split the remainder.
        public List<SplitContainerMeasurement> ContainerMeasurements { get; set; } = new List<SplitContainerMeasurement>();

        public SplitContainer(SplitContainerOrientation orientation)
        {
            Orientation = orientation;
        }

        public override void Draw(PageInformation pageInformation, AccurateDrawingDevice drawingDevice, Rect drawArea)
        {
            //TODO: Containers should probably have a "calculate" that is done once per session so I dont need to do all this each page.
            //session.DrawRectangle(drawArea, Colors.Red);
            var size = IsHorizontal ? drawArea.Width : drawArea.Height;
            double requestedTotalPercent = 1;
            double countPercent = 0;
            double countRemainderPercent = 0;
            int remainderCount = 0;

            foreach (var measure in ContainerMeasurements)
            {
                if (measure.IsFixedSize)
                {
                    size -= measure.GetFixedSize(Orientation);
                }
                else
                {
                    if (measure.IsPercentSpecified)
                    {
                        requestedTotalPercent -= Math.Round(measure.RequestedPercent.Value, GraphicalReport.DIGITS_TO_ROUND);
                    }
                    else
                    {
                        ++countRemainderPercent;
                    }
                    ++countPercent;
                }
            }
            var remainderPercentPer = requestedTotalPercent / countRemainderPercent;
            var remainingSize = size;

            double offset = 0;
            foreach (var measure in ContainerMeasurements)
            {
                double curSize = 0;
                var rect = new Rect();
                if (measure.IsFixedSize)
                {
                    curSize = measure.GetFixedSize(Orientation);
                }
                else
                {
                    ++remainderCount;
                    if (remainderCount == countPercent)
                    {
                        curSize = remainingSize;
                    }
                    else
                    {
                        double percent = measure.IsPercentSpecified ? measure.RequestedPercent.Value : remainderPercentPer;
                        curSize = Math.Round(percent * size, GraphicalReport.DIGITS_TO_ROUND);
                        remainingSize -= curSize;
                    }
                }
                rect = GetRect(offset, curSize, drawArea);
                //session.DrawRectangle(rect, Colors.Blue);
                measure.Container.Draw(pageInformation, drawingDevice, rect);
                offset += curSize;
            }
        }

        private Rect GetRect(double offset, double size, Rect drawArea)
        {
            if (IsHorizontal)
            {
                return new Rect(drawArea.X + offset, drawArea.Y, size, drawArea.Height);
            }
            else
            {
                return new Rect(drawArea.X, drawArea.Y + offset, drawArea.Width, size);
            }
        }

        public void AddContainer(Container container)
        {
            ContainerMeasurements.Add(new SplitContainerMeasurement(container));
        }

        public void AddContainer(SplitContainerMeasurement measurement)
        {
            ContainerMeasurements.Add(measurement);
        }

        public void AddSelfSizedContainer(Container container)
        {
            var measure = new SplitContainerMeasurement(container)
            {
                IsSizeSelfAssigned = true
            };
            ContainerMeasurements.Add(measure);
        }

        public override double GetRequestedWidth()
        {
            return double.MaxValue;
        }

        public override double GetRequestedHeight()
        {
            return double.MaxValue;
        }
    }

    public enum SplitContainerOrientation
    {
        Horizontal, Vertical
    }

    public struct SplitContainerMeasurement
    {
        public double? FixedInchSize { get; set; }
        public double? FixedDipSize { get; set; }
        public bool IsSizeSelfAssigned { get; set; }
        public bool IsFixedSize => FixedInchSize.HasValue || FixedDipSize.HasValue || IsSizeSelfAssigned;
        public double? RequestedPercent { get; set; }
        public bool IsPercentSpecified => RequestedPercent.HasValue;
        public Container Container { get; set; }

        public SplitContainerMeasurement(Container container)
        {
            Container = container;
            FixedInchSize = null;
            FixedDipSize = null;
            IsSizeSelfAssigned = false;
            RequestedPercent = null;
        }

        public double GetFixedSize(SplitContainerOrientation orientation)
        {
            if (FixedInchSize.HasValue)
                return Math.Round(FixedInchSize.Value * GraphicalReport.DEFAULT_DIP, GraphicalReport.DIGITS_TO_ROUND);
            if (FixedDipSize.HasValue)
                return Math.Round(FixedDipSize.Value, GraphicalReport.DIGITS_TO_ROUND);
            if (IsSizeSelfAssigned)
            {
                if (orientation == SplitContainerOrientation.Horizontal)
                    return Math.Round(Container.GetRequestedWidth(), GraphicalReport.DIGITS_TO_ROUND);
                else
                    return Math.Round(Container.GetRequestedHeight(), GraphicalReport.DIGITS_TO_ROUND);
            }
            throw new Exception();
        }
    }
}