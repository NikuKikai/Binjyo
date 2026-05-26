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
    public enum EditTool
    {
        Brush,
        Eraser
    }

    public partial class Memo
    {
        private void UpdateHSVWheel()
        {
            double x = System.Windows.Forms.Control.MousePosition.X - Left;
            double y = System.Windows.Forms.Control.MousePosition.Y - Top;

            if (x < 0 || x >= Width || y < 0 || y >= Height)
                return;
            int displayX = ClampToPixelIndex((int)(x / scale), bitmapTransformed.Width);
            int displayY = ClampToPixelIndex((int)(y / scale), bitmapTransformed.Height);
            var px = bitmapTransformed.GetPixel(displayX, displayY);
            var originalPoint = MapDisplayedPixelToOriginalPixel(displayX, displayY);

            popup.IsOpen = true;
            popup.HorizontalOffset = x / dpiFactor + 20;
            popup.VerticalOffset = y / dpiFactor + 10;

            //  Update Hue marker
            float hue = px.GetHue();
            HSV_SV.Hue = hue;
            var radius = HSVWheel.Width / 2 - HSVWheel.StrokeThickness / 2;
            var angle = (hue + 210) / 180 * Math.PI;
            var xc = HSVWheel.Width / 2 + Math.Cos(angle) * radius;
            var yc = HSVWheel.Height / 2 + Math.Sin(angle) * radius;
            HueMark.Margin = new Thickness(xc - HueMark.Width / 2, yc - HueMark.Height / 2, 0, 0);

            //  Update SV marker
            var v = (double)Math.Max(Math.Max(px.R, px.G), px.B) / 255;
            var s = (double)Math.Min(Math.Min(px.R, px.G), px.B) / 255;
            if (v == 0) s = 1;
            else s = (v - s) / v; // S of HSV is different from px.GetSaturation(), which is S of HSL(?)

            if (v < 0.5) SVMark.Stroke = new SolidColorBrush(Colors.White);
            else SVMark.Stroke = new SolidColorBrush(Colors.Black);

            SVMark.Margin = new Thickness(
                HSVWheel.Width / 2 - HSVRect.Width / 2 + s * HSVRect.Width - SVMark.Width / 2,
                HSVWheel.Height / 2 + HSVRect.Height / 2 - v * HSVRect.Height - SVMark.Height / 2, 0, 0);

            // Show text
            HSVText.Text = String.Format("H{0: 000}°   S{1: 000}    L{2: 000}", (int)px.GetHue(), (int)(px.GetSaturation() * 100), (int)(px.GetBrightness() * 100));
            RGBText.Text = String.Format("R{0: 000}    G{1: 000}    B{2: 000}", px.R, px.G, px.B);
            CoordText.Text = String.Format("X{0: 0000}    Y{1: 0000}", originalPoint.X, originalPoint.Y);
        }

        private void HideHSVWheel()
        {
            popup.IsOpen = false;
        }

        private bool ShouldShowHSVWheel()
        {
            return isHSVWheelPinnedGlobally && !isEditMode && !isdrag && !isResizing && Scene.DisplayMode != EDisplayMode.Minimized;
        }

        private void RefreshHSVWheelVisibility()
        {
            if (ShouldShowHSVWheel())
                UpdateHSVWheel();
            else
                HideHSVWheel();
        }

        private static void RefreshAllMemoHSVWheelVisibility()
        {
            foreach (Memo memo in Application.Current.Windows.OfType<Window>().OfType<Memo>())
            {
                memo.RefreshHSVWheelVisibility();
            }
        }

        public static void RefreshAllMemoScalingModes()
        {
            foreach (Memo memo in Application.Current.Windows.OfType<Window>().OfType<Memo>())
            {
                memo.ApplyConfiguredBitmapScalingMode();
            }
        }

        private int ClampToPixelIndex(int value, int length)
        {
            if (length <= 0)
                return 0;
            return Math.Max(0, Math.Min(length - 1, value));
        }

        private System.Drawing.Point MapDisplayedPixelToOriginalPixel(int displayX, int displayY)
        {
            int currentWidth = bitmapTransformed.Width;
            int currentHeight = bitmapTransformed.Height;
            int x = displayX;
            int y = displayY;

            for (int i = geometryTransformHistory.Count - 1; i >= 0; i--)
            {
                switch (geometryTransformHistory[i])
                {
                    case 'H':
                        x = currentWidth - 1 - x;
                        break;
                    case 'V':
                        y = currentHeight - 1 - y;
                        break;
                    case 'R':
                        int previousWidth = currentHeight;
                        int previousHeight = currentWidth;
                        int rotatedX = y;
                        int rotatedY = currentWidth - 1 - x;
                        x = rotatedX;
                        y = rotatedY;
                        currentWidth = previousWidth;
                        currentHeight = previousHeight;
                        break;
                }
            }

            return new System.Drawing.Point(
                ClampToPixelIndex(x, bitmap.Width),
                ClampToPixelIndex(y, bitmap.Height));
        }

        private void ApplyConfiguredBitmapScalingMode()
        {
            if (image == null)
                return;

            RenderOptions.SetBitmapScalingMode(image, Effects.GetConfiguredBitmapScalingMode());
        }


        private void MemoLocationChanged(object sender, EventArgs e)
        {
            if (!isSuspendingDisplayPosition)
            {
                anchorLeft = Left;
                anchorTop = Top;
                hasAnchorPosition = true;
            }

            UpdateEditPanelPlacement();
            RefreshAllMemoFeatureOverlays();
        }

        private void MemoSizeChanged(object sender, EventArgs e)
        {
            if (!isSuspendingDisplayPosition)
            {
                anchorLeft = Left;
                anchorTop = Top;
                hasAnchorPosition = true;
            }

            UpdateEditPanelPlacement();
            InvalidateFeatureAlignmentCachesFor(this);
            UpdateFeatureOverlayTransform();
            RenderDrawingOverlay();
            RefreshAllMemoFeatureOverlays();
        }

        private void EnterEditMode()
        {
            if (isEditMode || !CanInteract)
                return;

            SetResizeMode(false);
            StopResize();
            isEditMode = true;
            isDrawingStroke = false;
            activeDrawingStroke = null;
            EnsureEditModePanel();
            SetEditTool(EditTool.Brush);
            UpdateResizeModeVisuals();
            HideHSVWheel();
        }

        private void ExitEditMode()
        {
            if (!isEditMode)
                return;

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

            isEditMode = false;
            CloseEditModePanel();
            UpdateResizeModeVisuals();
            Cursor = Cursors.Arrow;
        }

        private void EnsureEditModePanel()
        {
            if (editModePanel == null)
            {
                editModePanel = new EditModePanel();
            }

            UpdateEditPanelState();
            if (!editModePanel.IsVisible)
                editModePanel.Show();
        }

        private void CloseEditModePanel()
        {
            if (editModePanel == null)
                return;

            editModePanel.Close();
            editModePanel = null;
        }

        private void UpdateEditPanelState()
        {
            if (editModePanel == null)
                return;

            editModePanel.UpdateToolName(currentEditTool == EditTool.Brush ? "Brush" : "Eraser");
            editModePanel.UpdateBrushSize(drawingBrushSize);
            UpdateEditPanelPlacement();
        }

        private void UpdateEditPanelPlacement()
        {
            if (editModePanel == null || !isEditMode)
                return;

            editModePanel.UpdatePlacement(Left, Top, Width, dpiFactor);
        }

        private void AdjustDrawingBrushSize(double delta)
        {
            drawingBrushSize = Math.Max(MinimumDrawingBrushSize, Math.Min(MaximumDrawingBrushSize, drawingBrushSize + delta));
            UpdateEditPanelState();
        }

        private void SetEditTool(EditTool tool)
        {
            currentEditTool = tool;
            Cursor = tool == EditTool.Brush ? Cursors.Pen : Cursors.Cross;
            UpdateEditPanelState();
        }

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

            if (bitmapTransformed == null || localPosition.X < 0 || localPosition.X >= Width || localPosition.Y < 0 || localPosition.Y >= Height)
                return false;

            double imageX = Math.Max(0, Math.Min(bitmapTransformed.Width - 1, localPosition.X / scale));
            double imageY = Math.Max(0, Math.Min(bitmapTransformed.Height - 1, localPosition.Y / scale));
            point = new DrawingPointData
            {
                X = imageX,
                Y = imageY
            };
            return true;
        }

        private void BeginDrawingStroke(System.Windows.Point localPosition)
        {
            if (!TryGetDrawingPoint(localPosition, out DrawingPointData point))
                return;

            BeginDrawingOperation();

            if (currentEditTool == EditTool.Eraser)
            {
                EraseStrokeAtPoint(point);
            }
            else
            {
                activeDrawingStroke = new DrawingStrokeData
                {
                    Size = drawingBrushSize
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

            if (currentEditTool == EditTool.Eraser)
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

            double eraserRadius = Math.Max(1, drawingBrushSize / 2.0);
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

                double displayThickness = Math.Max(1, stroke.Size / dpiFactor * scale);
                if (stroke.Points.Count == 1)
                {
                    var point = stroke.Points[0];
                    drawingOverlay.Children.Add(new Ellipse
                    {
                        Width = displayThickness,
                        Height = displayThickness,
                        Fill = new SolidColorBrush(Colors.Red),
                        Margin = new Thickness(point.X / dpiFactor * scale - displayThickness / 2, point.Y / dpiFactor * scale - displayThickness / 2, 0, 0)
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
                    polyline.Points.Add(new System.Windows.Point(point.X / dpiFactor * scale, point.Y / dpiFactor * scale));
                }

                drawingOverlay.Children.Add(polyline);
            }

            // TODO
            // sceneItem.
        }

        private void ApplyDrawingToBitmap(Bitmap targetBitmap)
        {
            ApplyDrawingToBitmap(targetBitmap, drawingDocument);
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
                        X = Clamp(originalPoint.X, 0, bitmap.Width - 1),
                        Y = Clamp(originalPoint.Y, 0, bitmap.Height - 1)
                    });
                }

                mapped.Strokes.Add(mappedStroke);
            }

            return mapped;
        }

        private System.Windows.Point MapTransformedPointToOriginal(double transformedX, double transformedY)
        {
            int currentWidth = bitmapTransformed.Width;
            int currentHeight = bitmapTransformed.Height;
            double x = transformedX;
            double y = transformedY;

            for (int i = geometryTransformHistory.Count - 1; i >= 0; i--)
            {
                switch (geometryTransformHistory[i])
                {
                    case 'H':
                        x = currentWidth - 1 - x;
                        break;
                    case 'V':
                        y = currentHeight - 1 - y;
                        break;
                    case 'R':
                        int previousWidth = currentHeight;
                        int previousHeight = currentWidth;
                        double rotatedX = y;
                        double rotatedY = currentWidth - 1 - x;
                        x = rotatedX;
                        y = rotatedY;
                        currentWidth = previousWidth;
                        currentHeight = previousHeight;
                        break;
                }
            }

            return new System.Windows.Point(x, y);
        }

        public void RestoreDrawingData(DrawingDocumentData data)
        {
            drawingDocument = data?.Clone() ?? new DrawingDocumentData();
            ClearDrawingUndoHistory();
            RenderDrawingOverlay();
        }

        private void FlipDrawingHorizontally()
        {
            ClearDrawingUndoHistory();
            foreach (DrawingStrokeData stroke in drawingDocument.Strokes)
            {
                foreach (DrawingPointData point in stroke.Points)
                {
                    point.X = bitmapTransformed.Width - 1 - point.X;
                }
            }
        }

        private void FlipDrawingVertically()
        {
            ClearDrawingUndoHistory();
            foreach (DrawingStrokeData stroke in drawingDocument.Strokes)
            {
                foreach (DrawingPointData point in stroke.Points)
                {
                    point.Y = bitmapTransformed.Height - 1 - point.Y;
                }
            }
        }

        private void RotateDrawing90()
        {
            ClearDrawingUndoHistory();
            foreach (DrawingStrokeData stroke in drawingDocument.Strokes)
            {
                foreach (DrawingPointData point in stroke.Points)
                {
                    double rotatedX = bitmapTransformed.Height - 1 - point.Y;
                    double rotatedY = point.X;
                    point.X = rotatedX;
                    point.Y = rotatedY;
                }
            }
        }

    }
}
