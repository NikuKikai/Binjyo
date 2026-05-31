using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Xml.Serialization;

namespace Binjyo
{
    public enum DrawingObjectKind
    {
        Stroke = 0,
        Text = 1,
    }

    public enum DrawingOperationKind
    {
        AddObjects = 0,
        RemoveObjects = 1,
        UpdateObject = 2,
    }

    [Serializable]
    public sealed class DrawingColorData
    {
        public byte A { get; set; } = 255;
        public byte R { get; set; } = 255;
        public byte G { get; set; } = 0;
        public byte B { get; set; } = 0;

        public Color ToMediaColor()
        {
            return Color.FromArgb(A, R, G, B);
        }

        public static DrawingColorData FromMediaColor(Color color)
        {
            return new DrawingColorData
            {
                A = color.A,
                R = color.R,
                G = color.G,
                B = color.B
            };
        }

        public DrawingColorData Clone()
        {
            return new DrawingColorData
            {
                A = A,
                R = R,
                G = G,
                B = B
            };
        }
    }

    [Serializable]
    public sealed class DrawingPointData
    {
        public double X { get; set; }
        public double Y { get; set; }

        public DrawingPointData Clone()
        {
            return new DrawingPointData
            {
                X = X,
                Y = Y
            };
        }
    }

    [Serializable]
    [XmlInclude(typeof(DrawingStrokeData))]
    public abstract class DrawingObjectData
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public DrawingObjectKind Kind { get; set; }
        public bool IsDeleted { get; set; }

        public abstract DrawingObjectData Clone();
    }

    [Serializable]
    public sealed class DrawingStrokeData : DrawingObjectData
    {
        public DrawingStrokeData()
        {
            Kind = DrawingObjectKind.Stroke;
        }

        public double SizePx { get; set; } = 5;
        public DrawingColorData Color { get; set; } = new DrawingColorData();
        public List<DrawingPointData> Points { get; set; } = new List<DrawingPointData>();

        public override DrawingObjectData Clone()
        {
            return new DrawingStrokeData
            {
                Id = Id,
                Kind = Kind,
                IsDeleted = IsDeleted,
                SizePx = SizePx,
                Color = Color?.Clone() ?? new DrawingColorData(),
                Points = Points.Select(point => point.Clone()).ToList()
            };
        }
    }

    [Serializable]
    [XmlInclude(typeof(AddDrawingObjectsOperationData))]
    [XmlInclude(typeof(RemoveDrawingObjectsOperationData))]
    [XmlInclude(typeof(UpdateDrawingObjectOperationData))]
    public abstract class DrawingOperationData
    {
        public DrawingOperationKind Kind { get; set; }
        public abstract DrawingOperationData Clone();
    }

    [Serializable]
    public sealed class AddDrawingObjectsOperationData : DrawingOperationData
    {
        public AddDrawingObjectsOperationData()
        {
            Kind = DrawingOperationKind.AddObjects;
        }

        public List<Guid> ObjectIds { get; set; } = new List<Guid>();

        public override DrawingOperationData Clone()
        {
            return new AddDrawingObjectsOperationData
            {
                ObjectIds = new List<Guid>(ObjectIds)
            };
        }
    }

    [Serializable]
    public sealed class RemoveDrawingObjectsOperationData : DrawingOperationData
    {
        public RemoveDrawingObjectsOperationData()
        {
            Kind = DrawingOperationKind.RemoveObjects;
        }

        public List<Guid> ObjectIds { get; set; } = new List<Guid>();

        public override DrawingOperationData Clone()
        {
            return new RemoveDrawingObjectsOperationData
            {
                ObjectIds = new List<Guid>(ObjectIds)
            };
        }
    }

    [Serializable]
    public sealed class UpdateDrawingObjectOperationData : DrawingOperationData
    {
        public UpdateDrawingObjectOperationData()
        {
            Kind = DrawingOperationKind.UpdateObject;
        }

        public Guid ObjectId { get; set; }
        public DrawingObjectData Before { get; set; }
        public DrawingObjectData After { get; set; }

        public override DrawingOperationData Clone()
        {
            return new UpdateDrawingObjectOperationData
            {
                ObjectId = ObjectId,
                Before = Before?.Clone(),
                After = After?.Clone()
            };
        }
    }

    [Serializable]
    public sealed class DrawingDocumentData
    {
        public double SourcePixelWidth { get; set; } = 1;
        public double SourcePixelHeight { get; set; } = 1;

        [XmlArrayItem(typeof(DrawingStrokeData))]
        public List<DrawingObjectData> Objects { get; set; } = new List<DrawingObjectData>();

        [XmlArrayItem(typeof(AddDrawingObjectsOperationData))]
        [XmlArrayItem(typeof(RemoveDrawingObjectsOperationData))]
        [XmlArrayItem(typeof(UpdateDrawingObjectOperationData))]
        public List<DrawingOperationData> Operations { get; set; } = new List<DrawingOperationData>();

        public int AppliedOperationCount { get; set; }

        public void ConfigureSourceSize(int pixelWidth, int pixelHeight)
        {
            SourcePixelWidth = Math.Max(1, pixelWidth);
            SourcePixelHeight = Math.Max(1, pixelHeight);
        }

        public DrawingDocumentData Clone()
        {
            return new DrawingDocumentData
            {
                SourcePixelWidth = SourcePixelWidth,
                SourcePixelHeight = SourcePixelHeight,
                Objects = Objects.Select(obj => obj.Clone()).ToList(),
                Operations = Operations.Select(operation => operation.Clone()).ToList(),
                AppliedOperationCount = AppliedOperationCount
            };
        }

        public DrawingPointData CreateNormalizedPoint(double pixelX, double pixelY)
        {
            return new DrawingPointData
            {
                X = NormalizeX(pixelX),
                Y = NormalizeY(pixelY)
            };
        }

        public double NormalizeX(double pixelX)
        {
            return SourcePixelWidth <= 0 ? 0 : pixelX / SourcePixelWidth;
        }

        public double NormalizeY(double pixelY)
        {
            return SourcePixelHeight <= 0 ? 0 : pixelY / SourcePixelHeight;
        }

        public Point DenormalizePoint(DrawingPointData point)
        {
            return new Point(point.X * SourcePixelWidth, point.Y * SourcePixelHeight);
        }

        public IEnumerable<DrawingObjectData> GetVisibleObjects()
        {
            return Objects.Where(obj => !obj.IsDeleted);
        }

        public IEnumerable<DrawingStrokeData> GetVisibleStrokes()
        {
            return GetVisibleObjects().OfType<DrawingStrokeData>();
        }

        public bool HasVisibleObjects()
        {
            return Objects.Any(obj => !obj.IsDeleted);
        }

        public bool CanUndo()
        {
            return AppliedOperationCount > 0;
        }

        public bool CanRedo()
        {
            return AppliedOperationCount < Operations.Count;
        }

        public void AddObject(DrawingObjectData drawingObject)
        {
            if (drawingObject == null)
                return;

            DrawingObjectData storedObject = drawingObject.Clone();
            storedObject.IsDeleted = false;
            ReplaceObject(storedObject);

            ApplyNewOperation(new AddDrawingObjectsOperationData
            {
                ObjectIds = new List<Guid> { storedObject.Id }
            });
        }

        public void RemoveObjects(IEnumerable<Guid> objectIds)
        {
            List<Guid> ids = objectIds?
                .Distinct()
                .Where(id =>
                {
                    DrawingObjectData drawingObject = FindObject(id);
                    return drawingObject != null && !drawingObject.IsDeleted;
                })
                .ToList() ?? new List<Guid>();

            if (ids.Count == 0)
                return;

            ApplyNewOperation(new RemoveDrawingObjectsOperationData
            {
                ObjectIds = ids
            });
        }

        public void UpdateObject(DrawingObjectData before, DrawingObjectData after)
        {
            if (before == null || after == null || before.Id != after.Id)
                return;

            ApplyNewOperation(new UpdateDrawingObjectOperationData
            {
                ObjectId = after.Id,
                Before = before.Clone(),
                After = after.Clone()
            });
        }

        public bool Undo()
        {
            if (!CanUndo())
                return false;

            AppliedOperationCount--;
            RevertOperation(Operations[AppliedOperationCount]);
            return true;
        }

        public bool Redo()
        {
            if (!CanRedo())
                return false;

            ApplyOperation(Operations[AppliedOperationCount]);
            AppliedOperationCount++;
            return true;
        }

        private void ApplyNewOperation(DrawingOperationData operation)
        {
            if (operation == null)
                return;

            if (AppliedOperationCount < Operations.Count)
                Operations.RemoveRange(AppliedOperationCount, Operations.Count - AppliedOperationCount);

            ApplyOperation(operation);
            Operations.Add(operation);
            AppliedOperationCount = Operations.Count;
        }

        private void ApplyOperation(DrawingOperationData operation)
        {
            switch (operation)
            {
                case AddDrawingObjectsOperationData addOperation:
                    foreach (Guid objectId in addOperation.ObjectIds)
                    {
                        DrawingObjectData drawingObject = FindObject(objectId);
                        if (drawingObject != null)
                            drawingObject.IsDeleted = false;
                    }
                    break;

                case RemoveDrawingObjectsOperationData removeOperation:
                    foreach (Guid objectId in removeOperation.ObjectIds)
                    {
                        DrawingObjectData drawingObject = FindObject(objectId);
                        if (drawingObject != null)
                            drawingObject.IsDeleted = true;
                    }
                    break;

                case UpdateDrawingObjectOperationData updateOperation:
                    if (updateOperation.After != null)
                        ReplaceObject(updateOperation.After.Clone());
                    break;
            }
        }

        private void RevertOperation(DrawingOperationData operation)
        {
            switch (operation)
            {
                case AddDrawingObjectsOperationData addOperation:
                    foreach (Guid objectId in addOperation.ObjectIds)
                    {
                        DrawingObjectData drawingObject = FindObject(objectId);
                        if (drawingObject != null)
                            drawingObject.IsDeleted = true;
                    }
                    break;

                case RemoveDrawingObjectsOperationData removeOperation:
                    foreach (Guid objectId in removeOperation.ObjectIds)
                    {
                        DrawingObjectData drawingObject = FindObject(objectId);
                        if (drawingObject != null)
                            drawingObject.IsDeleted = false;
                    }
                    break;

                case UpdateDrawingObjectOperationData updateOperation:
                    if (updateOperation.Before != null)
                        ReplaceObject(updateOperation.Before.Clone());
                    break;
            }
        }

        private DrawingObjectData FindObject(Guid objectId)
        {
            return Objects.FirstOrDefault(obj => obj.Id == objectId);
        }

        private void ReplaceObject(DrawingObjectData drawingObject)
        {
            int index = Objects.FindIndex(obj => obj.Id == drawingObject.Id);
            if (index >= 0)
                Objects[index] = drawingObject;
            else
                Objects.Add(drawingObject);
        }
    }

    public static class DrawingData
    {
        public static WriteableBitmap RenderOverlay(DrawingDocumentData document)
        {
            int pixelWidth = Math.Max(1, (int)Math.Round(document?.SourcePixelWidth ?? 1));
            int pixelHeight = Math.Max(1, (int)Math.Round(document?.SourcePixelHeight ?? 1));
            return RenderOverlay(document, pixelWidth, pixelHeight);
        }

        public static WriteableBitmap RenderOverlay(DrawingDocumentData document, int pixelWidth, int pixelHeight)
        {
            pixelWidth = Math.Max(1, pixelWidth);
            pixelHeight = Math.Max(1, pixelHeight);

            DrawingVisual visual = new DrawingVisual();
            using (DrawingContext dc = visual.RenderOpen())
            {
                if (document != null)
                    DrawVisibleObjects(dc, document);
            }

            RenderTargetBitmap renderTarget = new RenderTargetBitmap(pixelWidth, pixelHeight, 96, 96, PixelFormats.Pbgra32);
            renderTarget.Render(visual);
            return new WriteableBitmap(renderTarget);
        }

        public static WriteableBitmap Composite(BitmapSource source, DrawingDocumentData document)
        {
            if (source == null)
                return null;

            int pixelWidth = source.PixelWidth;
            int pixelHeight = source.PixelHeight;
            DrawingVisual visual = new DrawingVisual();

            using (DrawingContext dc = visual.RenderOpen())
            {
                dc.DrawImage(source, new Rect(0, 0, pixelWidth, pixelHeight));
                if (document != null)
                    DrawVisibleObjects(dc, document);
            }

            RenderTargetBitmap renderTarget = new RenderTargetBitmap(pixelWidth, pixelHeight, 96, 96, PixelFormats.Pbgra32);
            renderTarget.Render(visual);
            return new WriteableBitmap(renderTarget);
        }

        public static WriteableBitmap Composite(BitmapSource source, BitmapSource overlay)
        {
            if (source == null)
                return null;

            int pixelWidth = source.PixelWidth;
            int pixelHeight = source.PixelHeight;
            DrawingVisual visual = new DrawingVisual();

            using (DrawingContext dc = visual.RenderOpen())
            {
                dc.DrawImage(source, new Rect(0, 0, pixelWidth, pixelHeight));
                if (overlay != null)
                    dc.DrawImage(overlay, new Rect(0, 0, pixelWidth, pixelHeight));
            }

            RenderTargetBitmap renderTarget = new RenderTargetBitmap(pixelWidth, pixelHeight, 96, 96, PixelFormats.Pbgra32);
            renderTarget.Render(visual);
            return new WriteableBitmap(renderTarget);
        }

        public static List<Guid> HitTestObjectIds(DrawingDocumentData document, Point pixelPoint, double eraserRadiusPx)
        {
            if (document == null)
                return new List<Guid>();

            List<Guid> hitIds = new List<Guid>();
            foreach (DrawingObjectData drawingObject in document.GetVisibleObjects())
            {
                switch (drawingObject)
                {
                    case DrawingStrokeData stroke when DoesStrokeIntersectPoint(document, stroke, pixelPoint, eraserRadiusPx):
                        hitIds.Add(stroke.Id);
                        break;
                }
            }

            return hitIds;
        }

        public static bool DoesStrokeIntersectPoint(DrawingDocumentData document, DrawingStrokeData stroke, Point pixelPoint, double eraserRadiusPx)
        {
            if (document == null || stroke == null || stroke.Points.Count == 0)
                return false;

            double threshold = eraserRadiusPx + stroke.SizePx / 2.0;
            double thresholdSquared = threshold * threshold;

            if (stroke.Points.Count == 1)
                return GetDistanceSquared(document.DenormalizePoint(stroke.Points[0]), pixelPoint) <= thresholdSquared;

            for (int i = 1; i < stroke.Points.Count; i++)
            {
                Point start = document.DenormalizePoint(stroke.Points[i - 1]);
                Point end = document.DenormalizePoint(stroke.Points[i]);
                if (GetDistanceSquaredToSegment(start, end, pixelPoint) <= thresholdSquared)
                    return true;
            }

            return false;
        }

        private static void DrawVisibleObjects(DrawingContext dc, DrawingDocumentData document)
        {
            foreach (DrawingObjectData drawingObject in document.GetVisibleObjects())
            {
                switch (drawingObject)
                {
                    case DrawingStrokeData stroke:
                        DrawStroke(dc, document, stroke);
                        break;
                }
            }
        }

        private static void DrawStroke(DrawingContext dc, DrawingDocumentData document, DrawingStrokeData stroke)
        {
            if (stroke.Points.Count == 0)
                return;

            Brush brush = new SolidColorBrush(stroke.Color?.ToMediaColor() ?? Colors.Red);
            if (brush.CanFreeze)
                brush.Freeze();

            if (stroke.Points.Count == 1)
            {
                Point point = document.DenormalizePoint(stroke.Points[0]);
                double radius = stroke.SizePx / 2.0;
                dc.DrawEllipse(brush, null, point, radius, radius);
                return;
            }

            StreamGeometry geometry = new StreamGeometry();
            using (StreamGeometryContext context = geometry.Open())
            {
                context.BeginFigure(document.DenormalizePoint(stroke.Points[0]), false, false);
                context.PolyLineTo(stroke.Points.Skip(1).Select(document.DenormalizePoint).ToList(), true, true);
            }
            geometry.Freeze();

            Pen pen = new Pen(brush, stroke.SizePx)
            {
                StartLineCap = PenLineCap.Round,
                EndLineCap = PenLineCap.Round,
                LineJoin = PenLineJoin.Round
            };
            if (pen.CanFreeze)
                pen.Freeze();

            dc.DrawGeometry(null, pen, geometry);
        }

        private static double GetDistanceSquared(Point a, Point b)
        {
            double dx = a.X - b.X;
            double dy = a.Y - b.Y;
            return dx * dx + dy * dy;
        }

        private static double GetDistanceSquaredToSegment(Point segmentStart, Point segmentEnd, Point point)
        {
            double segmentDeltaX = segmentEnd.X - segmentStart.X;
            double segmentDeltaY = segmentEnd.Y - segmentStart.Y;
            double segmentLengthSquared = segmentDeltaX * segmentDeltaX + segmentDeltaY * segmentDeltaY;

            if (segmentLengthSquared <= 0.0001)
                return GetDistanceSquared(segmentStart, point);

            double projection = ((point.X - segmentStart.X) * segmentDeltaX + (point.Y - segmentStart.Y) * segmentDeltaY) / segmentLengthSquared;
            projection = Math.Max(0, Math.Min(1, projection));

            double projectedX = segmentStart.X + projection * segmentDeltaX;
            double projectedY = segmentStart.Y + projection * segmentDeltaY;
            double distanceX = point.X - projectedX;
            double distanceY = point.Y - projectedY;
            return distanceX * distanceX + distanceY * distanceY;
        }
    }

    public static class DrawingDataSerializer
    {
        public static void Save(string path, DrawingDocumentData data)
        {
            XmlSerializer serializer = new XmlSerializer(typeof(DrawingDocumentData));
            using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write))
            {
                serializer.Serialize(stream, data ?? new DrawingDocumentData());
            }
        }

        public static DrawingDocumentData Load(string path)
        {
            if (!File.Exists(path))
                return new DrawingDocumentData();

            XmlSerializer serializer = new XmlSerializer(typeof(DrawingDocumentData));
            using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                return serializer.Deserialize(stream) as DrawingDocumentData ?? new DrawingDocumentData();
            }
        }
    }
}
