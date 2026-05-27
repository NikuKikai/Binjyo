using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Drawing;


namespace Binjyo
{
    public partial class Memo
    {
        private bool isDrawMode = false;
        private DrawPanel drawPanel = null;
        private DrawingDocumentData drawingDocument { get => Item.DrawingDocument; set => Item.DrawingDocument = value; }
        private DrawingStrokeData activeDrawingStroke = null;
        private bool isDrawingStroke = false;
        private Stack<DrawingDocumentData> drawingUndoStack => Item.DrawingUndoStack;
        private DrawingDocumentData pendingDrawingOperationSnapshot = null;
        private bool pendingDrawingOperationChanged = false;


        private void EnterDrawMode()
        {
            if (isDrawMode || !CanInteract || isResizeMode || Scene.IsStitchMode)
                return;
            isDrawMode = true;

            isDrawingStroke = false;
            activeDrawingStroke = null;

            drawPanel?.Close();
            drawPanel = new DrawPanel();
            drawPanel.UpdatePlacement(Left, Top, Width, Item.DpiFactor);
            drawPanel.Show();

            HideHSVWheel();
        }

        private void ExitDrawMode()
        {
            if (!isDrawMode) return;

            if (isDrawingStroke)
            {
                isDrawingStroke = false;
                activeDrawingStroke = null;
                if (Mouse.Captured == this)
                    Mouse.Capture(null);
                CommitDrawingOperation();
            }
            else
            {
                CancelPendingDrawingOperation();
            }

            isDrawMode = false;
            drawPanel?.Close();
            drawPanel = null;

            Cursor = Cursors.Arrow;
        }


        private void BeginDrawingStroke(System.Windows.Point localPosition)
        {
            if (!TryGetDrawingPoint(localPosition, out DrawingPointData point))
                return;

            BeginDrawingOperation();

            if (drawPanel.Tool == DrawTool.Eraser)
            {
                EraseStrokeAtPoint(point);
            }
            else
            {
                activeDrawingStroke = new DrawingStrokeData
                {
                    Size = drawPanel.BrushSize
                };
                activeDrawingStroke.Points.Add(point);
                drawingDocument.Strokes.Add(activeDrawingStroke);
                MarkDrawingOperationChanged();
            }

            isDrawingStroke = true;
            Mouse.Capture(this);
            RenderDrawingOverlay();
        }

        private void ExtendDrawingStroke(System.Windows.Point localPosition)
        {
            if (!isDrawingStroke)
                return;

            if (!TryGetDrawingPoint(localPosition, out DrawingPointData point))
                return;

            if (drawPanel.Tool == DrawTool.Eraser)
            {
                EraseStrokeAtPoint(point);
                return;
            }

            if (activeDrawingStroke == null)
                return;

            DrawingPointData lastPoint = activeDrawingStroke.Points.LastOrDefault();
            if (lastPoint != null &&
                Math.Abs(lastPoint.X - point.X) < 0.25 &&
                Math.Abs(lastPoint.Y - point.Y) < 0.25)
            {
                return;
            }

            activeDrawingStroke.Points.Add(point);
            RenderDrawingOverlay();
        }

        private void EndDrawingStroke()
        {
            isDrawingStroke = false;
            activeDrawingStroke = null;
            if (Mouse.Captured == this)
                Mouse.Capture(null);
            CommitDrawingOperation();
            RenderDrawingOverlay();
        }


        // ======== Drawing Operation Management ========

        private void BeginDrawingOperation()
        {
            pendingDrawingOperationSnapshot = drawingDocument.Clone();
            pendingDrawingOperationChanged = false;
        }

        private void MarkDrawingOperationChanged()
        {
            pendingDrawingOperationChanged = true;
        }

        private void CommitDrawingOperation()
        {
            if (pendingDrawingOperationSnapshot != null && pendingDrawingOperationChanged)
                drawingUndoStack.Push(pendingDrawingOperationSnapshot);

            pendingDrawingOperationSnapshot = null;
            pendingDrawingOperationChanged = false;
        }

        private void CancelPendingDrawingOperation()
        {
            pendingDrawingOperationSnapshot = null;
            pendingDrawingOperationChanged = false;
        }

        private void ClearDrawingUndoHistory()
        {
            drawingUndoStack.Clear();
            CancelPendingDrawingOperation();
        }

        private bool TryGetDrawingPoint(System.Windows.Point localPosition, out DrawingPointData point)
        {
            point = null;

            // if (localPosition.X < 0 || localPosition.X >= Width || localPosition.Y < 0 || localPosition.Y >= Height)
            //     return false;

            // double imageX = Math.Max(0, Math.Min(bitmapTransformed.Width - 1, localPosition.X / scale));
            // double imageY = Math.Max(0, Math.Min(bitmapTransformed.Height - 1, localPosition.Y / scale));
            // point = new DrawingPointData
            // {
            //     X = imageX,
            //     Y = imageY
            // };
            return true;
        }

        private void UndoLastDrawingStroke()
        {
            if (isDrawingStroke)
            {
                isDrawingStroke = false;
                activeDrawingStroke = null;
                if (Mouse.Captured == this)
                    Mouse.Capture(null);
                if (pendingDrawingOperationSnapshot != null)
                {
                    drawingDocument = pendingDrawingOperationSnapshot.Clone();
                    CancelPendingDrawingOperation();
                    RenderDrawingOverlay();
                    return;
                }
                CancelPendingDrawingOperation();
            }

            if (drawingUndoStack.Count == 0)
                return;

            drawingDocument = drawingUndoStack.Pop();
            RenderDrawingOverlay();
        }

        private void EraseStrokeAtPoint(DrawingPointData point)
        {
            if (drawingDocument.Strokes.Count == 0)
                return;

            double eraserRadius = Math.Max(1, drawPanel.BrushSize / 2.0);
            int removedCount = drawingDocument.Strokes.RemoveAll(stroke => DoesStrokeIntersectPoint(stroke, point, eraserRadius));
            if (removedCount > 0)
            {
                MarkDrawingOperationChanged();
                RenderDrawingOverlay();
            }
        }

        private static bool DoesStrokeIntersectPoint(DrawingStrokeData stroke, DrawingPointData point, double eraserRadius)
        {
            if (stroke == null || stroke.Points.Count == 0)
                return false;

            double threshold = eraserRadius + stroke.Size / 2.0;
            double thresholdSquared = threshold * threshold;

            if (stroke.Points.Count == 1)
            {
                return GetDistanceSquared(stroke.Points[0], point) <= thresholdSquared;
            }

            for (int i = 1; i < stroke.Points.Count; i++)
            {
                if (GetDistanceSquaredToSegment(stroke.Points[i - 1], stroke.Points[i], point) <= thresholdSquared)
                    return true;
            }

            return false;
        }

        private static double GetDistanceSquared(DrawingPointData a, DrawingPointData b)
        {
            double dx = a.X - b.X;
            double dy = a.Y - b.Y;
            return dx * dx + dy * dy;
        }

        private static double GetDistanceSquaredToSegment(DrawingPointData segmentStart, DrawingPointData segmentEnd, DrawingPointData point)
        {
            double segmentDeltaX = segmentEnd.X - segmentStart.X;
            double segmentDeltaY = segmentEnd.Y - segmentStart.Y;
            double segmentLengthSquared = segmentDeltaX * segmentDeltaX + segmentDeltaY * segmentDeltaY;

            if (segmentLengthSquared <= 0.0001)
                return GetDistanceSquared(segmentStart, point);

            double projection = ((point.X - segmentStart.X) * segmentDeltaX + (point.Y - segmentStart.Y) * segmentDeltaY) / segmentLengthSquared;
            projection = Clamp(projection, 0, 1);

            double projectedX = segmentStart.X + projection * segmentDeltaX;
            double projectedY = segmentStart.Y + projection * segmentDeltaY;
            double distanceX = point.X - projectedX;
            double distanceY = point.Y - projectedY;
            return distanceX * distanceX + distanceY * distanceY;
        }

        private void RenderDrawingOverlay()
        {
            if (drawingOverlay == null)
                return;

            drawingOverlay.Children.Clear();

            foreach (DrawingStrokeData stroke in drawingDocument.Strokes)
            {
                if (stroke.Points.Count == 0)
                    continue;

                double displayThickness = Math.Max(1, stroke.Size / dpiFactor * Item.Scale);
                if (stroke.Points.Count == 1)
                {
                    var point = stroke.Points[0];
                    drawingOverlay.Children.Add(new Ellipse
                    {
                        Width = displayThickness,
                        Height = displayThickness,
                        Fill = new SolidColorBrush(Colors.Red),
                        Margin = new Thickness(point.X / dpiFactor * Item.Scale - displayThickness / 2, point.Y / dpiFactor * Item.Scale - displayThickness / 2, 0, 0)
                    });
                    continue;
                }

                var polyline = new Polyline
                {
                    Stroke = new SolidColorBrush(Colors.Red),
                    StrokeThickness = displayThickness,
                    StrokeStartLineCap = PenLineCap.Round,
                    StrokeEndLineCap = PenLineCap.Round,
                    StrokeLineJoin = PenLineJoin.Round
                };

                foreach (DrawingPointData point in stroke.Points)
                {
                    polyline.Points.Add(new System.Windows.Point(point.X / dpiFactor * Item.Scale, point.Y / dpiFactor * Item.Scale));
                }

                drawingOverlay.Children.Add(polyline);
            }

            // TODO
            // sceneItem.
        }


        private void ApplyDrawingToBitmap(Bitmap targetBitmap, DrawingDocumentData document)
        {
            if (document == null || document.Strokes.Count == 0)
                return;

            using (Graphics graphics = Graphics.FromImage(targetBitmap))
            {
                graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

                foreach (DrawingStrokeData stroke in document.Strokes)
                {
                    if (stroke.Points.Count == 0)
                        continue;

                    using (var pen = new System.Drawing.Pen(System.Drawing.Color.Red, (float)stroke.Size))
                    using (var brush = new SolidBrush(System.Drawing.Color.Red))
                    {
                        pen.StartCap = System.Drawing.Drawing2D.LineCap.Round;
                        pen.EndCap = System.Drawing.Drawing2D.LineCap.Round;
                        pen.LineJoin = System.Drawing.Drawing2D.LineJoin.Round;

                        if (stroke.Points.Count == 1)
                        {
                            DrawingPointData point = stroke.Points[0];
                            float radius = (float)stroke.Size / 2f;
                            graphics.FillEllipse(brush, (float)point.X - radius, (float)point.Y - radius, radius * 2f, radius * 2f);
                            continue;
                        }

                        var points = stroke.Points
                            .Select(point => new System.Drawing.PointF((float)point.X, (float)point.Y))
                            .ToArray();
                        graphics.DrawLines(pen, points);
                    }
                }
            }
        }

        private DrawingDocumentData MapDrawingDocumentToOriginal()
        {
            var mapped = new DrawingDocumentData();
            foreach (DrawingStrokeData stroke in drawingDocument.Strokes)
            {
                var mappedStroke = new DrawingStrokeData
                {
                    Size = stroke.Size
                };

                foreach (DrawingPointData point in stroke.Points)
                {
                    var originalPoint = MapTransformedPointToOriginal(point.X, point.Y);
                    mappedStroke.Points.Add(new DrawingPointData
                    {
                        X = Clamp(originalPoint.X, 0, Item.Bitmap.Width - 1),
                        Y = Clamp(originalPoint.Y, 0, Item.Bitmap.Height - 1)
                    });
                }

                mapped.Strokes.Add(mappedStroke);
            }

            return mapped;
        }

        private System.Windows.Point MapTransformedPointToOriginal(double transformedX, double transformedY)
        {
            return new System.Windows.Point(0, 0);
            // int currentWidth = bitmapTransformed.Width;
            // int currentHeight = bitmapTransformed.Height;
            // double x = transformedX;
            // double y = transformedY;

            // for (int i = geometryTransformHistory.Count - 1; i >= 0; i--)
            // {
            //     switch (geometryTransformHistory[i])
            //     {
            //         case 'H':
            //             x = currentWidth - 1 - x;
            //             break;
            //         case 'V':
            //             y = currentHeight - 1 - y;
            //             break;
            //         case 'R':
            //             int previousWidth = currentHeight;
            //             int previousHeight = currentWidth;
            //             double rotatedX = y;
            //             double rotatedY = currentWidth - 1 - x;
            //             x = rotatedX;
            //             y = rotatedY;
            //             currentWidth = previousWidth;
            //             currentHeight = previousHeight;
            //             break;
            //     }
            // }

            // return new System.Windows.Point(x, y);
        }

        public void RestoreDrawingData(DrawingDocumentData data)
        {
            drawingDocument = data?.Clone() ?? new DrawingDocumentData();
            ClearDrawingUndoHistory();
            RenderDrawingOverlay();
        }


    }
}
