using Microsoft.Graphics.Canvas;
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
            PointShape = Shape.Triangle;
        }

        public override void Draw(CanvasDrawingSession session, PageInformation page, TransformInformation2d transform)
        {
            base.Draw(session, page, transform);

            DrawLabels(session, page, transform);
        }

        protected void DrawLabels(CanvasDrawingSession session, PageInformation page, TransformInformation2d transform)
        {
            //TODO: Add so you can show above and below. Maybe an anti collision check.
            for (int i = 0; i < Labels.Count; ++i)
            {
                var (foot, label) = Labels[i];
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
                    using (var layout = new CanvasTextLayout(session, label, format, 0, 0))
                    {
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
                        var translate = Matrix3x2.CreateTranslation(finalLocation, y);

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
    }
}
