using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI;

namespace AccurateReportSystem
{
    public class XAxisInfo
    {
        public double LabelHeight
        {
            get
            {
                return labelHeight ?? MasterInfo.LabelHeight;
            }
            set
            {
                labelHeight = value;
            }
        }
        private double? labelHeight = null;
        public double LabelHeightDIP => IsEnabled ? Math.Round(LabelHeight * GraphicalReport.DEFAULT_DIP, GraphicalReport.DIGITS_TO_ROUND) : 0.0;
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
                return IsEnabled ? titleFontSize ?? MasterInfo.TitleFontSize : 0f;
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
        public double TotalHeight => LabelHeightDIP + TitleTotalHeight;
        public double TitleTotalHeight => TitleFontSize + ExtraTitleHeight;
        public double ExtraTitleHeight
        {
            get
            {
                return IsEnabled ? extraTitleHeight ?? MasterInfo.ExtraTitleHeight : 0;
            }
            set
            {
                if (double.IsNaN(value))
                    extraTitleHeight = null;
                else
                    extraTitleHeight = value;
            }
        }
        private double? extraTitleHeight = null;
        public bool IsEnabled
        {
            get
            {
                return isEnabled ?? MasterInfo.IsEnabled;
            }
            set
            {
                isEnabled = value;
            }
        }
        private bool? isEnabled = null;
        public XAxisInfo MasterInfo { get; set; }
        public GridlineInfo MinorGridlineInfo
        {
            get
            {
                return minorGridlineInfo ?? MasterInfo.MinorGridlineInfo;
            }
            set
            {
                minorGridlineInfo = value;
            }
        }
        private GridlineInfo minorGridlineInfo = null;
        public GridlineInfo MajorGridlineInfo
        {
            get
            {
                return majorGridlineInfo ?? MasterInfo.MajorGridlineInfo;
            }
            set
            {
                majorGridlineInfo = value;
            }
        }
        private GridlineInfo majorGridlineInfo = null;

        public XAxisInfo()
        {
            MasterInfo = null;
            LabelHeight = 0.15;
            LabelTickLength = 5;
            LabelFontSize = 8f;
            LabelFormat = "F0";
            Title = "Footage";
            TitleFontSize = 10f;
            IsEnabled = true;
            ExtraTitleHeight = 2.0;
            MinorGridlineInfo = new GridlineInfo(100, Colors.Gray)
            {
                IsEnabled = false
            };
            MajorGridlineInfo = new GridlineInfo(100, Colors.LightGray);
        }

        public XAxisInfo(XAxisInfo master)
        {
            MasterInfo = master;
        }

        public void ResetIsEnabled()
        {
            isEnabled = null;
        }

        public void Draw()
        {

            var xAxisMajor = Gridlines[(int)GridlineName.MajorVertical].GetValues(page, graphBodyDrawArea);

            if (XAxisInfo.IsEnabled && XAxisInfo.TotalHeight != 0)
            {
                drawArea = new Rect(graphBodyDrawArea.X, graphBodyDrawArea.Bottom, graphBodyDrawArea.Width, XAxisInfo.TotalHeight);
                color = Gridlines[(int)GridlineName.MajorVertical].Color;
                thickness = Gridlines[(int)GridlineName.MajorVertical].Thickness;
                //session.DrawRectangle(drawArea, Colors.Green);
                using (var format = new CanvasTextFormat())
                {
                    format.HorizontalAlignment = CanvasHorizontalAlignment.Left;
                    format.WordWrapping = CanvasWordWrapping.WholeWord;
                    format.FontSize = XAxisInfo.LabelFontSize;
                    format.FontFamily = "Arial";
                    format.FontWeight = FontWeights.Thin;
                    format.FontStyle = FontStyle.Normal;
                    foreach (var (value, location) in xAxisMajor)
                    {
                        var endLocation = location;
                        var label = value.ToString(XAxisInfo.LabelFormat);
                        using (var layout = new CanvasTextLayout(session, label, format, 0, 0))
                        {
                            var layoutWidth = (float)Math.Round(layout.LayoutBounds.Width / 2, GraphicalReport.DIGITS_TO_ROUND);
                            var finalLocation = location - layoutWidth;
                            if (finalLocation < drawArea.Left)
                            {
                                endLocation = (float)drawArea.Left + layoutWidth;
                                finalLocation = (float)drawArea.Left;
                            }
                            else if (finalLocation + (2 * layoutWidth) > drawArea.Right)
                            {
                                endLocation = (float)drawArea.Right - layoutWidth;
                                finalLocation = (float)Math.Round(drawArea.Right - (2 * layoutWidth), GraphicalReport.DIGITS_TO_ROUND);
                            }
                            var translate = Matrix3x2.CreateTranslation(finalLocation, (float)(drawArea.Top + XAxisInfo.LabelTickLength));
                            using (var geo = CanvasGeometry.CreateText(layout))
                            {
                                using (var translatedGeo = geo.Transform(translate))
                                {
                                    session.FillGeometry(translatedGeo, Colors.Black);
                                }
                            }
                        }

                        using (var pathBuilder = new CanvasPathBuilder(session))
                        {
                            pathBuilder.BeginFigure(location, (float)drawArea.Top);
                            pathBuilder.AddLine(endLocation, (float)(drawArea.Top + XAxisInfo.LabelTickLength));
                            pathBuilder.EndFigure(CanvasFigureLoop.Open);
                            using (var geo = CanvasGeometry.CreatePath(pathBuilder))
                            {
                                var style = new CanvasStrokeStyle
                                {
                                    TransformBehavior = CanvasStrokeTransformBehavior.Fixed
                                };
                                session.DrawGeometry(geo, color, thickness, style);
                            }
                        }
                    }
                }
            }
        }
    }
}
