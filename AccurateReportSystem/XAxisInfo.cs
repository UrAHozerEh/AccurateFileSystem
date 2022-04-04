using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Geometry;
using Microsoft.Graphics.Canvas.Text;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.UI;
using Windows.UI.Text;

namespace AccurateReportSystem
{
    public class XAxisInfo
    {
        // Label info
        public double LabelHeightInches
        {
            get
            {
                return labelHeightInches ?? MasterInfo.LabelHeightInches;
            }
            set
            {
                labelHeightInches = value;
            }
        }
        private double? labelHeightInches = null;
        public double LabelHeight => IsEnabled ? (Math.Round(LabelHeightInches * GraphicalReport.DEFAULT_DIP, GraphicalReport.DIGITS_TO_ROUND) + LabelTickLength) : 0;
        public double LabelTickLength
        {
            get
            {
                return labelTickLength ?? MasterInfo.LabelTickLength;
            }
            set
            {
                labelTickLength = value;
            }
        }
        private double? labelTickLength = null;
        public bool IsTickDrawn
        {
            get
            {
                return isTickDrawn ?? MasterInfo.IsTickDrawn;
            }
            set
            {
                isTickDrawn = value;
            }
        }
        private bool? isTickDrawn = null;
        public float LabelFontSize
        {
            get
            {
                return labelFontSize ?? MasterInfo.LabelFontSize;
            }
            set
            {
                labelFontSize = value;
            }
        }
        private float? labelFontSize = null;
        public string LabelFormat
        {
            get
            {
                return labelFormat ?? MasterInfo.LabelFormat;
            }
            set
            {
                labelFormat = value;
            }
        }
        private string labelFormat = null;

        // Title info
        public string Title
        {
            get
            {
                return title ?? MasterInfo.Title;
            }
            set
            {
                title = value;
            }
        }
        private string title = null;
        public float TitleFontSize
        {
            get
            {
                return titleFontSize ?? MasterInfo.TitleFontSize;
            }
            set
            {
                if (float.IsNaN(value))
                    titleFontSize = null;
                else
                    titleFontSize = value;
            }
        }
        private float? titleFontSize = null;
        public double TotalHeight => LabelHeight + TitleTotalHeight;
        public float TitleTotalHeight => IsEnabled ? (TitleFontSize + ExtraTitleHeight) : 0;
        public float ExtraTitleHeight
        {
            get
            {
                return extraTitleHeight ?? MasterInfo.ExtraTitleHeight;
            }
            set
            {
                if (double.IsNaN(value))
                    extraTitleHeight = null;
                else
                    extraTitleHeight = value;
            }
        }
        private float? extraTitleHeight = null;

        // Minor gridlines
        public GridlineInfo MinorGridline
        {
            get
            {
                return minorGridline ?? MasterInfo.MinorGridline;
            }
            set
            {
                minorGridline = value;
            }
        }
        private GridlineInfo minorGridline = null;

        // Major gridlines
        public GridlineInfo MajorGridline
        {
            get
            {
                return majorGridline ?? MasterInfo.MajorGridline;
            }
            set
            {
                majorGridline = value;
            }
        }
        private GridlineInfo majorGridline = null;

        public bool IsFlippedVertical
        {
            get
            {
                return isFlippedVertical ?? MasterInfo.IsFlippedVertical;
            }
            set
            {
                isFlippedVertical = value;
            }
        }
        private bool? isFlippedVertical = null;

        public Color OverlapColor
        {
            get => overlapColor ?? MasterInfo.OverlapColor;
            set => overlapColor = value;
        }
        private Color? overlapColor = null;

        public float OverlapOpacity
        {
            get => overlapOpacity ?? MasterInfo.OverlapOpacity;
            set => overlapOpacity = value;
        }
        private float? overlapOpacity = null;

        public bool IsEnabled
        {
            get => isEnabled ?? MasterInfo.IsEnabled;
            set => isEnabled = value;
        }
        private bool? isEnabled = null;

        public DateTime StartDate
        {
            get => startDate ?? MasterInfo.StartDate;
            set => startDate = value;
        }
        private DateTime? startDate = null;
        public XAxisInfo MasterInfo { get; set; }

        public XAxisInfo()
        {
            MasterInfo = null;
            LabelHeightInches = 0.15;
            LabelTickLength = 2;
            IsTickDrawn = false;
            LabelFontSize = 10f;
            LabelFormat = "F0";
            Title = "Footage";
            TitleFontSize = 14f;
            IsEnabled = true;
            ExtraTitleHeight = 2f;
            MinorGridline = new GridlineInfo(100, Colors.Gray)
            {
                IsEnabled = false
            };
            MajorGridline = new GridlineInfo(100, Colors.LightGray);
            IsFlippedVertical = false;
            OverlapColor = Colors.Black;
            OverlapOpacity = 0.25f;
        }

        public XAxisInfo(XAxisInfo master)
        {
            MasterInfo = master;
            MinorGridline = new GridlineInfo(MasterInfo.MinorGridline);
            MajorGridline = new GridlineInfo(MasterInfo.MajorGridline);
        }

        public void ResetIsEnabled()
        {
            isEnabled = null;
        }

        public void DrawGridlines(CanvasDrawingSession session, PageInformation page, Rect graphBodyDrawArea, TransformInformation1d transform)
        {
            if (MinorGridline.IsEnabled)
            {
                var values = GetGridlineValues(page, transform, MinorGridline.Offset);
                foreach (var (x, _) in values)
                {
                    session.DrawLine(x, (float)graphBodyDrawArea.Top, x, (float)graphBodyDrawArea.Bottom, MinorGridline.Color);
                }
            }
            if (MajorGridline.IsEnabled)
            {
                var values = GetGridlineValues(page, transform, MajorGridline.Offset);
                foreach (var (x, _) in values)
                {
                    session.DrawLine(x, (float)graphBodyDrawArea.Top, x, (float)graphBodyDrawArea.Bottom, MinorGridline.Color);
                }
            }
        }

        private List<(float X, double Value)> GetGridlineValues(PageInformation page, TransformInformation1d transform, double offset)
        {
            var output = new List<(float X, double Value)>();

            var numOffsetInStart = (int)(page.StartFootage / offset);
            var start = numOffsetInStart * offset;
            var curVal = start;
            while (curVal <= page.EndFootage)
            {
                if (curVal >= page.StartFootage)
                {
                    var x = transform.ToDrawArea(curVal);
                    output.Add((x, curVal));
                }
                curVal += offset;
            }

            return output;
        }

        public void DrawInfo(CanvasDrawingSession session, PageInformation page, TransformInformation1d transform, Rect drawArea)
        {
            if (!IsEnabled)
                return;
            if (!IsFlippedVertical)
            {
                Rect labelDrawArea = new Rect(drawArea.Left, drawArea.Top, drawArea.Width, LabelHeight);
                DrawLabels(session, page, transform, labelDrawArea);
                Rect titleDrawArea = new Rect(drawArea.Left, drawArea.Top + LabelHeight, drawArea.Width, TitleTotalHeight);
                DrawTitle(session, titleDrawArea);
            }
            else
            {
                Rect labelDrawArea = new Rect(drawArea.Left, drawArea.Top + TitleTotalHeight, drawArea.Width, LabelHeight);
                DrawLabels(session, page, transform, labelDrawArea);
                Rect titleDrawArea = new Rect(drawArea.Left, drawArea.Top, drawArea.Width, TitleTotalHeight);
                DrawTitle(session, titleDrawArea);
            }
        }

        public void DrawTitle(CanvasDrawingSession session, Rect drawArea)
        {
            //session.DrawRectangle(drawArea, Colors.Purple);
            using (var format = new CanvasTextFormat())
            {
                format.HorizontalAlignment = CanvasHorizontalAlignment.Center;
                format.WordWrapping = CanvasWordWrapping.WholeWord;
                format.FontSize = TitleFontSize;
                format.FontFamily = "Arial";
                format.FontWeight = FontWeights.Thin;
                format.FontStyle = FontStyle.Normal;
                using (var layout = new CanvasTextLayout(session, Title, format, (float)drawArea.Width, (float)drawArea.Height))
                {
                    using (var geometry = CanvasGeometry.CreateText(layout))
                    {
                        var bounds = layout.DrawBounds;
                        var translateMatrix = bounds.CreateTranslateMiddleTo(drawArea);
                        var rotationMatrix = drawArea.CreateRotationAroundMiddle(90);
                        using (var rotatedGeo = geometry.Transform(translateMatrix))//.Transform(rotationMatrix))
                        {
                            session.FillGeometry(rotatedGeo, Colors.Black);
                        }
                    }
                }
            }
        }

        public void DrawLabels(CanvasDrawingSession session, PageInformation page, TransformInformation1d transform, Rect drawArea)
        {
            //session.DrawRectangle(drawArea, Colors.Orange);
            var tickColor = MajorGridline.Color;
            var tickThickness = MajorGridline.Thickness;
            using (var format = new CanvasTextFormat())
            {
                format.HorizontalAlignment = CanvasHorizontalAlignment.Left;
                format.WordWrapping = CanvasWordWrapping.NoWrap;
                format.FontSize = LabelFontSize;
                format.FontFamily = "Arial";
                format.FontWeight = FontWeights.Thin;
                format.FontStyle = FontStyle.Normal;
                var values = GetGridlineValues(page, transform, MajorGridline.Offset);
                foreach (var (location, value) in values)
                {
                    var endLocation = location;
                    var label = value.ToString();
                    if(LabelFormat == "Empty")
                    {
                        return;
                    }
                    else if (LabelFormat == "Hours")
                    {
                        var time = new DateTime().Date.AddHours(value);
                        label = time.ToShortTimeString();
                    }
                    else if(LabelFormat == "DateOnly")
                    {
                        var time = StartDate.AddHours(value);
                        label = time.ToShortDateString();
                    }
                    else if (LabelFormat.StartsWith("Date"))
                    {
                        label = ParseDateFormat(value);
                    }
                    else if(LabelFormat == "StartDateOtherHour")
                    {
                        var time = StartDate.AddHours(value);
                        if(time.Hour == 0)
                        {
                            label = time.ToShortDateString();
                        }
                        else
                        {
                            label = time.ToShortTimeString();
                        }
                    }
                    else
                    {
                        label = value.ToString(LabelFormat);
                    }

                    using (var layout = new CanvasTextLayout(session, label, format, 0, 0))
                    {
                        var halfLayoutWidth = (float)Math.Round(layout.LayoutBounds.Width / 2, GraphicalReport.DIGITS_TO_ROUND);
                        var finalLocation = location - halfLayoutWidth;
                        if (finalLocation < drawArea.Left)
                        {
                            endLocation = (float)drawArea.Left + halfLayoutWidth;
                            finalLocation = (float)drawArea.Left;
                        }
                        else if (finalLocation + (2 * halfLayoutWidth) > drawArea.Right)
                        {
                            endLocation = (float)drawArea.Right - halfLayoutWidth;
                            finalLocation = (float)Math.Round(drawArea.Right - (2 * halfLayoutWidth), GraphicalReport.DIGITS_TO_ROUND);
                        }
                        var height = layout.LayoutBounds.Height;
                        var y = !IsFlippedVertical ? (float)(drawArea.Top + LabelTickLength) : (float)(drawArea.Bottom - LabelTickLength - height);
                        var translate = Matrix3x2.CreateTranslation(finalLocation, y);
                        using (var geo = CanvasGeometry.CreateText(layout))
                        {
                            using (var translatedGeo = geo.Transform(translate))
                            {
                                session.FillGeometry(translatedGeo, Colors.Black);
                            }
                        }
                    }

                    if (IsTickDrawn)
                    {
                        using (var pathBuilder = new CanvasPathBuilder(session))
                        {
                            pathBuilder.BeginFigure(location, (float)drawArea.Top);
                            pathBuilder.AddLine(endLocation, (float)(drawArea.Top + LabelTickLength));
                            pathBuilder.EndFigure(CanvasFigureLoop.Open);
                            using (var geo = CanvasGeometry.CreatePath(pathBuilder))
                            {
                                var style = new CanvasStrokeStyle
                                {
                                    TransformBehavior = CanvasStrokeTransformBehavior.Fixed
                                };
                                session.DrawGeometry(geo, tickColor, tickThickness, style);
                            }
                        }
                    }
                }
            }
        }

        private string ParseDateFormat(double value)
        {
            if (StartDate == null)
            {
                throw new ArgumentNullException("StartDate with Date format is null for XAxisInfo.");
            }
            var type = LabelFormat.Replace("Date", "").Trim();
            var labelDate = StartDate;
            switch (type)
            {
                case "Hour":
                    labelDate = labelDate.AddHours(value);
                    break;
                case "Day":
                    labelDate = labelDate.AddDays(value);
                    break;
                case "Minute":
                    labelDate = labelDate.AddMinutes(value);
                    break;
                case "Second":
                    labelDate = labelDate.AddSeconds(value);
                    break;
                default:
                    break;
            }
            return labelDate.ToString("g");
        }
    }
}
