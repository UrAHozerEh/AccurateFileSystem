﻿using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Geometry;
using Microsoft.Graphics.Canvas.Text;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Windows.UI;
using Windows.UI.Text;

namespace AccurateReportSystem
{
    public class PointWithLabelGraphSeries : GraphSeries
    {
        public List<(double Footage, string Label)> Labels { get; set; }
        public float FontSize { get; set; } = 8f;
        public float BackdropOpacity { get; set; } = 0.75f;
        public Color BackdropColor { get; set; } = Colors.White;
        public float BackdropIncrease { get; set; } = 0.25f;

        public PointWithLabelGraphSeries(string name, List<(double Footage, double Value, string Label)> labels) : base(name, labels.Select(label => (label.Footage, label.Value)).ToList())
        {
            Init(labels.Select(label => (label.Footage, label.Label)).ToList());
        }

        public PointWithLabelGraphSeries(string name, double value, List<(double Footage, string Label)> labels) : base(name, labels.Select(label => (label.Footage, value)).ToList())
        {
            Init(labels);
        }

        private void Init(List<(double Footage, string Label)> labels)
        {
            GraphType = Type.Point;
            Labels = labels;
            Labels.Sort((value1, value2) => value1.Footage.CompareTo(value2.Footage));
            PointShape = Shape.Triangle;
        }

        public override void Draw(CanvasDrawingSession session, PageInformation page, TransformInformation2d transform)
        {
            base.Draw(session, page, transform);

            DrawLabels(session, page, transform);
        }

        private List<(float X, float Y, CanvasTextLayout TextLayout)> GetLayouts(CanvasDrawingSession session, PageInformation page, TransformInformation2d transform)
        {
            var output = new List<(float X, float Y, CanvasTextLayout TextLayout)>();
            for (var i = 0; i < Labels.Count; ++i)
            {
                var (foot, label) = Labels[i];
                if (string.IsNullOrWhiteSpace(label))
                    continue;
                var (_, value) = Values[i];
                if (foot < page.StartFootage)
                    continue;
                if (foot > page.EndFootage)
                    break;
                var (x, y) = transform.ToDrawArea(foot, value);

                var drawArea = transform.DrawArea;
                using (var format = new CanvasTextFormat())
                {
                    format.HorizontalAlignment = CanvasHorizontalAlignment.Left;
                    format.WordWrapping = CanvasWordWrapping.WholeWord;
                    format.FontSize = FontSize;
                    format.FontFamily = "Arial";
                    format.FontWeight = FontWeights.Normal;
                    format.FontStyle = FontStyle.Normal;
                    var endLocation = x;
                    var layout = new CanvasTextLayout(session, label, format, 0, 0);
                    var halfLayoutWidth = (float)Math.Round(layout.LayoutBounds.Width / 2, GraphicalReport.DIGITS_TO_ROUND);
                    var finalLocation = x - halfLayoutWidth;
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
                    y -= ((float)layout.LayoutBounds.Height) + 2f + ShapeRadius;
                    output.Add((finalLocation, y, layout));
                }
            }
            return output;
        }

        protected void DrawLabels(CanvasDrawingSession session, PageInformation page, TransformInformation2d transform)
        {
            //TODO: Add so you can show above and below. Maybe an anti collision check.
            var layouts = GetLayouts(session, page, transform);
            for (var i = 0; i < layouts.Count; ++i)
            {
                var (x, y, layout) = layouts[i];
                //var layoutHeight = (float)(layout.LayoutBounds.Height * 1.1);
                bool hasMoved;
                do
                {
                    hasMoved = false;
                    for (var j = 0; j < i; ++j)
                    {
                        var (prevX, prevY, prevLayout) = layouts[j];
                        var prevLayoutWidth = prevLayout.LayoutBounds.Width * 1.1;
                        if (prevX + prevLayoutWidth > x && prevY == y)
                        {
                            y -= FontSize + 2;
                            hasMoved = true;
                            layouts[i] = (x, y, layout);
                        }
                    }
                }
                while (hasMoved);
                var translate = Matrix3x2.CreateTranslation(x, y);

                using (var geo = CanvasGeometry.CreateText(layout))
                {
                    using (var translatedGeo = geo.Transform(translate))
                    {
                        using (var _ = session.CreateLayer(BackdropOpacity))
                        {
                            var translatedBounds = translatedGeo.ComputeBounds();
                            var yShift = translatedBounds.Height * (BackdropIncrease / 2);
                            translatedBounds.Height *= 1 + BackdropIncrease;
                            translatedBounds.Y -= yShift;
                            session.FillRectangle(translatedBounds, BackdropColor);
                        }
                        session.FillGeometry(translatedGeo, Colors.Black);
                    }
                }
            }
        }
    }
}