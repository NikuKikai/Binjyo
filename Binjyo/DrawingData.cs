using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Serialization;

namespace Binjyo
{
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
