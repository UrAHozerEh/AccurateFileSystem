using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI;

namespace AccurateReportSystem
{
    public class SingleValueGraphSeries : GraphSeries
    {
        public bool IsDashed { get; set; } = true;
        public double Value { get; set; }

        public SingleValueGraphSeries(string name, double value) : base(name, new List<(double footage, double value)>())
        {
            Value = value;
            LineColor = Colors.Red;
            LineThickness = 1;
        }

        public override void Draw(CanvasDrawingSession session, PageInformation page, TransformInformation2d transform)
        {
            using (var _ = session.CreateLayer(Opcaity))
            using (var strokeStyle = new CanvasStrokeStyle())
            {
                strokeStyle.DashStyle = IsDashed ? CanvasDashStyle.Dash : CanvasDashStyle.Solid;
                var (x1, y) = transform.ToDrawArea(page.StartFootage, Value);
                var (x2, _) = transform.ToDrawArea(page.EndFootage, Value);
                session.DrawLine(x1, y, x2, y, LineColor, LineThickness, strokeStyle);
            }
        }
    }
}
