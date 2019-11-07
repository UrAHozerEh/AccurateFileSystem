﻿using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Brushes;
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
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Shapes;

namespace AccurateReportSystem
{
    public class Graph : Container
    {
        public bool IsInverted { get; set; } = false;
        public List<GraphSeries> Series { get; set; } = new List<GraphSeries>();
        private List<GeometryInfo> Geometries;
        public CommentSeries CommentSeries { get; set; }
        public GridlineInfo[] Gridlines { get; set; }


        public XAxisInfo XAxisInfo { get; set; }
        public LegendInfo LegendInfo { get; set; }
        public YAxesInfo YAxesInfo { get; set; }
        private double TotalXValueShift => LegendInfo.WidthDIP + YAxesInfo.Y1TotalHeight;
        private double TotalWidthShift => TotalXValueShift + YAxesInfo.Y2TotalHeight;
        private double TotalYValueShift => 0;



        public Graph(GraphicalReport report)
        {
            XAxisInfo = new XAxisInfo(report.XAxisInfo);
            YAxesInfo = new YAxesInfo(report.YAxesInfo, "Volts", "Depth");
            LegendInfo = new LegendInfo(report.LegendInfo, "CIS Data");

            Gridlines = new GridlineInfo[4];
            Gridlines[(int)GridlineName.MinorHorizontal] = new GridlineInfo(0.1, Colors.LightGray);
            Gridlines[(int)GridlineName.MajorHorizontal] = new GridlineInfo(0.5, Colors.Gray)
            {
                Thickness = 2
            };
            Gridlines[(int)GridlineName.MinorVertical] = new GridlineInfo(100, Colors.LightGray);
            Gridlines[(int)GridlineName.MajorVertical] = new GridlineInfo(100, Colors.Gray)
            {
                Thickness = 2,
                Color = Colors.LightGray
            };
        }

        public override void Draw(PageInformation page, CanvasDrawingSession session, Rect DrawArea)
        {
            var graphBodyDrawArea = new Rect(TotalXValueShift + DrawArea.X, TotalYValueShift + DrawArea.Y, DrawArea.Width - TotalWidthShift, DrawArea.Height - XAxisInfo.TotalHeight);

            session.DrawRectangle(graphBodyDrawArea, Colors.Black);

            // Drawing entire graph area border.
            //TODO: Remove
            using (var graphBorder = CanvasGeometry.CreateRectangle(session, DrawArea))
            {
                var style = new CanvasStrokeStyle
                {
                    TransformBehavior = CanvasStrokeTransformBehavior.Hairline
                };
                //session.DrawGeometry(graphBorder, Colors.Orange, 1, style);
            }

            //Drawing graph body border.
            //TODO: Remove
            using (var graphBodyBorder = CanvasGeometry.CreateRectangle(session, graphBodyDrawArea))
            {
                var style = new CanvasStrokeStyle
                {
                    TransformBehavior = CanvasStrokeTransformBehavior.Hairline
                };
                //session.DrawGeometry(graphBodyBorder, Colors.Green, 1, style);
            }

            //Draw everything in the graph body layer. Should prevent bleed over.
            using (var layer = session.CreateLayer(1f, graphBodyDrawArea))
            {
                //TODO: Simplify getting geometries from the graph series. Should only be done once before getting all the pages.
                //TODO: Maybe it should actually be drawn per page.
                Geometries = new List<GeometryInfo>();
                Rect y1GraphArea = new Rect(page.StartFootage, YAxesInfo.Y1MinimumValue, page.Width, YAxesInfo.Y1ValuesHeight);
                Rect y2GraphArea = new Rect(page.StartFootage, YAxesInfo.Y2MinimumValue, page.Width, YAxesInfo.Y2ValuesHeight);
                TransformInformation y1Transform = new TransformInformation(graphBodyDrawArea, y1GraphArea, YAxesInfo.Y1IsInverted);
                TransformInformation y2Transform = new TransformInformation(graphBodyDrawArea, y2GraphArea, YAxesInfo.Y2IsInverted);

                var clipRect = new Rect(page.StartFootage, (float)YAxesInfo.Y1MinimumValue, page.Width, (float)YAxesInfo.Y1ValuesHeight);
                var widthScalar = graphBodyDrawArea.Width / page.Width;
                var heightScalar = graphBodyDrawArea.Height / YAxesInfo.Y1ValuesHeight;
                var xTranslate = graphBodyDrawArea.X - clipRect.X;
                var yTranslate = graphBodyDrawArea.Y - clipRect.Y;
                var transform = new CompositeTransform();
                transform.TranslateX = xTranslate;
                transform.TranslateY = yTranslate;
                transform.ScaleX = widthScalar;
                transform.ScaleY = heightScalar;
                var translateMatrix = Matrix3x2.CreateTranslation(new Vector2((float)xTranslate, (float)yTranslate));
                var scaleMatrix = Matrix3x2.CreateScale((float)widthScalar, (float)heightScalar, new Vector2((float)graphBodyDrawArea.X, (float)graphBodyDrawArea.Y));

                DrawGridLines(page, graphBodyDrawArea, session, translateMatrix, scaleMatrix);

                if (XAxisInfo.IsEnabled)
                {
                    var xAxisDrawArea = new Rect(graphBodyDrawArea.X, graphBodyDrawArea.Bottom, graphBodyDrawArea.Width, XAxisInfo.TotalHeight);
                    XAxisInfo.DrawLabels(session, page, y1Transform, y1GraphArea);
                }

                foreach (var series in Series)
                {
                    var curTransform = series.IsY1Axis ? y1Transform : y2Transform;
                    series.Draw(session, page, curTransform);
                }
                if (CommentSeries != null)
                {
                    var (commentGeoInfo, lineGeoInfo) = CommentSeries.GetGeometry(page, graphBodyDrawArea, session);
                    var style = new CanvasStrokeStyle
                    {
                        TransformBehavior = CanvasStrokeTransformBehavior.Hairline
                    };
                    if (lineGeoInfo != null)
                        session.DrawGeometry(lineGeoInfo.Geometry, lineGeoInfo.Color, 1, style);
                    if (commentGeoInfo != null)
                        session.FillGeometry(commentGeoInfo.Geometry, commentGeoInfo.Color);
                    //TODO: Canvas Stroke Style should be in Geo Info. Also should have different styles for text and the indicators.
                }
            }

            DrawAxisLabels(page, session, graphBodyDrawArea);
            DrawAxisTitles(session, graphBodyDrawArea);
            DrawOverlapShadow(page, session, graphBodyDrawArea);
            var legendDrawArea = new Rect(DrawArea.X, DrawArea.Y, LegendInfo.WidthDIP, graphBodyDrawArea.Height);
            DrawLegend(legendDrawArea, session);
        }

        private void DrawOverlapShadow(PageInformation page, CanvasDrawingSession session, Rect graphBodyDrawArea)
        {
            var color = Colors.Black;
            var opacity = 0.25f;
            var pixelToFootRatio = graphBodyDrawArea.Width / page.Width;
            var shadowWidth = (float)Math.Round(pixelToFootRatio * page.Overlap, GraphicalReport.DIGITS_TO_ROUND);
            var startRect = new Rect(graphBodyDrawArea.X, graphBodyDrawArea.Y, shadowWidth, graphBodyDrawArea.Height);
            var endRect = new Rect(graphBodyDrawArea.Right - shadowWidth, graphBodyDrawArea.Y, shadowWidth, graphBodyDrawArea.Height);
            using (var layer = session.CreateLayer(opacity))
            {
                session.FillRectangle(startRect, color);
                session.FillRectangle(endRect, color);
            }
        }

        private void DrawGridLines(PageInformation page, Rect graphBodyDrawArea, CanvasDrawingSession session, Matrix3x2 translateMatrix, Matrix3x2 scaleMatrix)
        {
            for (int i = 0; i < 4; ++i)
            {
                var gridline = Gridlines[i];
                var isVert = (i == 1 || i == 3);
                using (var geoInfo = isVert ? gridline.GetGeometryInfo(page, graphBodyDrawArea) : gridline.GetGeometryInfo(YAxesInfo.Y1MinimumValue, YAxesInfo.Y1MaximumValue, YAxesInfo.Y1IsInverted, graphBodyDrawArea))
                {
                    var style = new CanvasStrokeStyle
                    {
                        TransformBehavior = CanvasStrokeTransformBehavior.Fixed
                    };
                    session.DrawGeometry(geoInfo.Geometry, geoInfo.Color, gridline.Thickness, style);
                }
            }
        }

        private void DrawLegend(Rect legendDrawArea, CanvasDrawingSession session)
        {
            //session.DrawRectangle(legendDrawArea, Colors.Orange);
            var height = LegendInfo.NameFontSize + LegendInfo.SeriesNameFontSize * Series.Count;
            var exampleRect = new Rect(legendDrawArea.X, legendDrawArea.Y, legendDrawArea.Width, height);
            //session.DrawRectangle(exampleRect, Colors.Orange);


            var nextHeight = 0f;
            if (LegendInfo.VerticalAlignment == CanvasVerticalAlignment.Center)
                nextHeight = (float)Math.Round(legendDrawArea.Height / 2 - height / 2, GraphicalReport.DIGITS_TO_ROUND);
            else if (LegendInfo.VerticalAlignment == CanvasVerticalAlignment.Bottom)
                nextHeight = (float)(legendDrawArea.Bottom - height);
            using (var format = new CanvasTextFormat())
            {
                format.HorizontalAlignment = LegendInfo.HorizontalAlignment;
                format.FontSize = LegendInfo.NameFontSize;
                format.FontFamily = "Arial";
                format.FontWeight = FontWeights.Bold;
                format.FontStyle = FontStyle.Normal;
                using (var layout = new CanvasTextLayout(session, LegendInfo.Name, format, (float)legendDrawArea.Width, 0))
                {
                    //var translateX = (float)Math.Round(legendDrawArea.X, GraphicalReport.DIGITS_TO_ROUND);
                    //var translateY = (float)Math.Round(legendDrawArea.Y, GraphicalReport.DIGITS_TO_ROUND);
                    //var translate = Matrix3x2.CreateTranslation(translateX, translateY);
                    using (var geo = CanvasGeometry.CreateText(layout))
                    {
                        session.FillGeometry(geo, (float)legendDrawArea.X, (float)legendDrawArea.Y + nextHeight, LegendInfo.NameColor);
                    }
                    nextHeight += LegendInfo.NameFontSize;
                }
            }
            using (var format = new CanvasTextFormat())
            {
                format.HorizontalAlignment = LegendInfo.HorizontalAlignment;
                format.FontSize = LegendInfo.SeriesNameFontSize;
                format.FontFamily = "Arial";
                format.FontWeight = FontWeights.Bold;
                format.FontStyle = FontStyle.Normal;
                foreach (var series in Series)
                {
                    using (var layout = new CanvasTextLayout(session, series.Name, format, (float)legendDrawArea.Width, 0))
                    {
                        //var translateX = (float)Math.Round(legendDrawArea.X, GraphicalReport.DIGITS_TO_ROUND);
                        //var translateY = (float)Math.Round(legendDrawArea.Y, GraphicalReport.DIGITS_TO_ROUND);
                        //var translate = Matrix3x2.CreateTranslation(translateX, translateY);
                        using (var geo = CanvasGeometry.CreateText(layout))
                        {
                            session.FillGeometry(geo, (float)legendDrawArea.X, (float)legendDrawArea.Y + nextHeight, series.LineColor);
                        }
                        nextHeight += LegendInfo.SeriesNameFontSize;
                    }
                }
            }
        }

        private void DrawAxisLabels(PageInformation page, CanvasDrawingSession session, Rect graphBodyDrawArea)
        {
            var yAxisMajor = Gridlines[(int)GridlineName.MajorHorizontal].GetValues(YAxesInfo.Y1MinimumValue, YAxesInfo.Y1MaximumValue, YAxesInfo.Y1IsInverted, graphBodyDrawArea);
            var color = Gridlines[(int)GridlineName.MajorHorizontal].Color;
            var thickness = Gridlines[(int)GridlineName.MajorHorizontal].Thickness;
            var drawArea = new Rect(graphBodyDrawArea.X - YAxesInfo.Y1LabelHeight, graphBodyDrawArea.Y, YAxesInfo.Y1LabelHeight, graphBodyDrawArea.Height);
            //session.DrawRectangle(drawArea, Colors.Orange);
            using (var format = new CanvasTextFormat())
            {
                format.HorizontalAlignment = CanvasHorizontalAlignment.Right;
                format.WordWrapping = CanvasWordWrapping.WholeWord;
                format.FontSize = YAxesInfo.Y1LabelFontSize;
                format.FontFamily = "Arial";
                format.FontWeight = FontWeights.Thin;
                format.FontStyle = FontStyle.Normal;
                foreach (var (value, location) in yAxisMajor)
                {
                    var endLocation = location;
                    var width = (float)(YAxesInfo.Y1LabelHeight - YAxesInfo.Y1LabelTickLength);
                    var label = value.ToString(YAxesInfo.Y1LabelFormat);
                    using (var layout = new CanvasTextLayout(session, label, format, width, 0))
                    {
                        var layoutHeight = (float)Math.Round(layout.LayoutBounds.Height / 2, GraphicalReport.DIGITS_TO_ROUND);
                        var finalLocation = location - layoutHeight;
                        if (finalLocation < drawArea.Top)
                        {
                            endLocation = (float)drawArea.Top + layoutHeight;
                            finalLocation = (float)drawArea.Top;
                        }
                        else if (finalLocation + (2 * layoutHeight) > drawArea.Bottom)
                        {
                            endLocation = (float)drawArea.Bottom - layoutHeight;
                            finalLocation = (float)drawArea.Bottom - (2 * layoutHeight);
                        }
                        var translate = Matrix3x2.CreateTranslation((float)drawArea.X, finalLocation);
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
                        pathBuilder.BeginFigure((float)graphBodyDrawArea.X, location);
                        pathBuilder.AddLine((float)(graphBodyDrawArea.X - YAxesInfo.Y1LabelTickLength), endLocation);
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

        private void DrawAxisTitles(CanvasDrawingSession session, Rect graphBodyDrawArea)
        {
            if (XAxisInfo.IsEnabled && XAxisInfo.TitleFontSize != 0 && !string.IsNullOrWhiteSpace(XAxisInfo.Title))
            {
                var xAxisDrawArea = new Rect(graphBodyDrawArea.X, graphBodyDrawArea.Bottom + XAxisInfo.LabelHeightDIP, graphBodyDrawArea.Width, XAxisInfo.TitleTotalHeight);
                DrawAxisTitle(XAxisInfo.Title, session, xAxisDrawArea, XAxisInfo.TitleFontSize, 0);
                //session.DrawRectangle(xAxisDrawArea, Colors.Orange);
            }
            if (YAxesInfo.Y1TitleFontSize != 0 && !string.IsNullOrWhiteSpace(YAxesInfo.Y1Title))
            {
                var y1AxisDrawArea = new Rect(graphBodyDrawArea.X - YAxesInfo.Y1TotalHeight, graphBodyDrawArea.Y, YAxesInfo.Y1TitleHeight, graphBodyDrawArea.Height);
                DrawAxisTitle(YAxesInfo.Y1Title, session, y1AxisDrawArea, YAxesInfo.Y1TitleFontSize, 90);
                //session.DrawRectangle(y1AxisDrawArea, Colors.Orange);
            }
        }

        private void DrawAxisTitle(string title, CanvasDrawingSession session, Rect drawArea, float fontSize, float rotation)
        {
            float width, height;
            if (rotation == 90 || rotation == -90)
            {
                width = (float)drawArea.Height;
                height = (float)drawArea.Width;
            }
            else
            {
                width = (float)drawArea.Width;
                height = (float)drawArea.Height;
            }
            var middleX = (float)(drawArea.X + drawArea.Width / 2);
            var middleY = (float)(drawArea.Y + drawArea.Height / 2);

            using (var format = new CanvasTextFormat())
            {
                format.HorizontalAlignment = CanvasHorizontalAlignment.Center;
                format.WordWrapping = CanvasWordWrapping.WholeWord;
                format.FontSize = fontSize;
                format.FontFamily = "Arial";
                format.FontWeight = FontWeights.Thin;
                format.FontStyle = FontStyle.Normal;
                using (var layout = new CanvasTextLayout(session, title, format, width, height))
                {
                    using (var geometry = CanvasGeometry.CreateText(layout))
                    {
                        var bounds = layout.DrawBounds;
                        var boundsMiddleY = (float)(bounds.Y + bounds.Height / 2);
                        var boundsMiddleX = (float)(bounds.X + bounds.Width / 2);
                        var translateY = middleY - boundsMiddleY;
                        var translateX = middleX - boundsMiddleX;
                        var translateMatrix = Matrix3x2.CreateTranslation(translateX, translateY);
                        var rotationMatrix = Matrix3x2.CreateRotation((float)(rotation * Math.PI / 180), new Vector2(middleX, middleY));
                        using (var rotatedGeo = geometry.Transform(translateMatrix).Transform(rotationMatrix))
                        {
                            session.FillGeometry(rotatedGeo, Colors.Black);
                        }
                    }
                }
            }
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

    public enum GridlineName
    {
        MinorHorizontal = 0, MajorHorizontal = 2, MinorVertical = 1, MajorVertical = 3
    }
}
