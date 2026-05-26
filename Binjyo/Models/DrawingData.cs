using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Xml.Serialization;

namespace Binjyo
{
    public static class DrawingData
    {
        public static void ApplyDrawing(Bitmap targetBitmap, DrawingDocumentData document)
        {
            if (document == null || document.Strokes.Count == 0)
                return;

            using (Graphics graphics = Graphics.FromImage(targetBitmap))
            {
                graphics.SmoothingMode = SmoothingMode.AntiAlias;

                foreach (DrawingStrokeData stroke in document.Strokes)
                {
                    if (stroke.Points.Count == 0)
                        continue;

                    using (var pen = new Pen(Color.Red, (float)stroke.Size))
                    using (var brush = new SolidBrush(Color.Red))
                    {
                        pen.StartCap = LineCap.Round;
                        pen.EndCap = LineCap.Round;
                        pen.LineJoin = LineJoin.Round;

                        if (stroke.Points.Count == 1)
                        {
                            DrawingPointData point = stroke.Points[0];
                            float radius = (float)stroke.Size / 2f;
                            graphics.FillEllipse(brush, (float)point.X - radius, (float)point.Y - radius, radius * 2f, radius * 2f);
                            continue;
                        }

                        PointF[] points = stroke.Points
                            .Select(point => new PointF((float)point.X, (float)point.Y))
                            .ToArray();
                        graphics.DrawLines(pen, points);
                    }
                }
            }
        }

        // public static DrawingDocumentData MapDrawingDocumentToOriginal(SceneItem item)
        // {
        //     var mapped = new DrawingDocumentData();
        //     foreach (DrawingStrokeData stroke in item.DrawingDocument.Strokes)
        //     {
        //         var mappedStroke = new DrawingStrokeData
        //         {
        //             Size = stroke.Size
        //         };

        //         foreach (DrawingPointData point in stroke.Points)
        //         {
        //             System.Windows.Point originalPoint = MapTransformedPointToOriginal(item, point.X, point.Y);
        //             mappedStroke.Points.Add(new DrawingPointData
        //             {
        //                 X = Clamp(originalPoint.X, 0, item.Bitmap.Width - 1),
        //                 Y = Clamp(originalPoint.Y, 0, item.Bitmap.Height - 1)
        //             });
        //         }

        //         mapped.Strokes.Add(mappedStroke);
        //     }

        //     return mapped;
        // }
    }

    [Serializable]
    public sealed class DrawingPointData
    {
        public double X { get; set; }
        public double Y { get; set; }
    }

    [Serializable]
    public sealed class DrawingStrokeData
    {
        public double Size { get; set; }
        public List<DrawingPointData> Points { get; set; } = new List<DrawingPointData>();
    }

    [Serializable]
    public sealed class DrawingDocumentData
    {
        public List<DrawingStrokeData> Strokes { get; set; } = new List<DrawingStrokeData>();

        public DrawingDocumentData Clone()
        {
            var clone = new DrawingDocumentData();
            foreach (DrawingStrokeData stroke in Strokes)
            {
                var clonedStroke = new DrawingStrokeData
                {
                    Size = stroke.Size
                };

                foreach (DrawingPointData point in stroke.Points)
                {
                    clonedStroke.Points.Add(new DrawingPointData
                    {
                        X = point.X,
                        Y = point.Y
                    });
                }

                clone.Strokes.Add(clonedStroke);
            }

            return clone;
        }
    }

    public static class DrawingDataSerializer
    {
        public static void Save(string path, DrawingDocumentData data)
        {
            var serializer = new XmlSerializer(typeof(DrawingDocumentData));
            using (var stream = new FileStream(path, FileMode.Create, FileAccess.Write))
            {
                serializer.Serialize(stream, data ?? new DrawingDocumentData());
            }
        }

        public static DrawingDocumentData Load(string path)
        {
            if (!File.Exists(path))
                return new DrawingDocumentData();

            var serializer = new XmlSerializer(typeof(DrawingDocumentData));
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                return serializer.Deserialize(stream) as DrawingDocumentData ?? new DrawingDocumentData();
            }
        }
    }
}
