using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Forms;
using Rectangle = System.Drawing.Rectangle;

namespace Binjyo
{
    public partial class MemoD11
    {
        private bool isDrawMode;
        private bool isDrawingStroke;
        private DrawingStrokeData activeDrawingStroke;
        private DrawPanel drawPanel;

        private bool HasPendingDrawingStroke()
        {
            return isDrawingStroke && activeDrawingStroke != null;
        }

        /// <summary>
        /// Restore a drawing document loaded from history and refresh the overlay texture.
        /// </summary>
        public void RestoreDrawingData(DrawingDocumentData data)
        {
            Item.DrawingDocument = data?.Clone() ?? new DrawingDocumentData();
            Item.DrawingDocument.ConfigureSourceSize(Item.Bitmap.PixelWidth, Item.Bitmap.PixelHeight);
            InvalidateDrawingOverlay();
            RenderRequest();
        }

        /// <summary>
        /// Enter draw mode and show the shared tool panel beside the memo.
        /// </summary>
        private void EnterDrawMode()
        {
            if (isDrawMode || Scene.DisplayMode != EDisplayMode.Expanded)
                return;

            StopRotateAnimation();
            Scene.DragMoveEnd();
            isDrawMode = true;
            isDrawingStroke = false;
            activeDrawingStroke = null;

            drawPanel?.Close();
            drawPanel = new DrawPanel();
            UpdateDrawPanelPlacement();
            drawPanel.Show();

            HideHSVWheel();
        }

        /// <summary>
        /// Exit draw mode and close the tool panel.
        /// </summary>
        private void ExitDrawMode()
        {
            if (!isDrawMode)
                return;

            isDrawMode = false;
            isDrawingStroke = false;
            activeDrawingStroke = null;

            if (Capture)
                Capture = false;

            drawPanel?.Close();
            drawPanel = null;
        }

        /// <summary>
        /// Keep the drawing tool panel attached to the memo edge with the shared sidecar placement logic.
        /// </summary>
        private void UpdateDrawPanelPlacement()
        {
            if (drawPanel == null)
                return;

            Rectangle sidecarBounds = GetSidecarBounds(
                currentHostBounds.Left,
                currentHostBounds.Top,
                currentHostBounds.Width,
                (int)Math.Round(drawPanel.Width),
                (int)Math.Round(drawPanel.Height),
                Item.DpiFactor <= 0 ? 1.0 : Item.DpiFactor);

            drawPanel.Left = sidecarBounds.Left;
            drawPanel.Top = sidecarBounds.Top;
        }

        /// <summary>
        /// Translate a host-local cursor position into source-bitmap physical pixels.
        /// </summary>
        private bool TryMapHostPositionToBitmapPixels(double hostX, double hostY, out Point bitmapPixelPoint)
        {
            var bitmapPoint = MapHostPositionToBitmap(hostX, hostY);
            double pixelX = bitmapPoint.X * Item.DpiFactor;
            double pixelY = bitmapPoint.Y * Item.DpiFactor;

            if (pixelX < 0 || pixelY < 0 || pixelX >= Item.Bitmap.PixelWidth || pixelY >= Item.Bitmap.PixelHeight)
            {
                bitmapPixelPoint = new Point();
                return false;
            }

            bitmapPixelPoint = new Point(pixelX, pixelY);
            return true;
        }

        /// <summary>
        /// Begin a new brush stroke or erase operation at the current cursor position.
        /// </summary>
        private void BeginDrawingStroke(MouseEventArgs e)
        {
            if (drawPanel == null || !TryMapHostPositionToBitmapPixels(e.X, e.Y, out Point bitmapPixelPoint))
                return;

            Item.DrawingDocument.ConfigureSourceSize(Item.Bitmap.PixelWidth, Item.Bitmap.PixelHeight);

            if (drawPanel.Tool == DrawTool.Eraser)
            {
                ApplyEraseAt(bitmapPixelPoint);
            }
            else
            {
                activeDrawingStroke = new DrawingStrokeData
                {
                    SizePx = drawPanel.BrushSize
                };
                activeDrawingStroke.Points.Add(Item.DrawingDocument.CreateNormalizedPoint(bitmapPixelPoint.X, bitmapPixelPoint.Y));
                InvalidateDrawingOverlay();
                RenderRequest(true);
            }

            isDrawingStroke = true;
            Capture = true;
        }

        /// <summary>
        /// Extend the active brush stroke or continue erasing while dragging.
        /// </summary>
        private void ExtendDrawingStroke(MouseEventArgs e)
        {
            if (!isDrawingStroke || drawPanel == null || !TryMapHostPositionToBitmapPixels(e.X, e.Y, out Point bitmapPixelPoint))
                return;

            if (drawPanel.Tool == DrawTool.Eraser)
            {
                ApplyEraseAt(bitmapPixelPoint);
                return;
            }

            if (activeDrawingStroke == null)
                return;

            DrawingPointData lastPoint = activeDrawingStroke.Points.LastOrDefault();
            if (lastPoint != null)
            {
                Point lastPixelPoint = Item.DrawingDocument.DenormalizePoint(lastPoint);
                double dx = lastPixelPoint.X - bitmapPixelPoint.X;
                double dy = lastPixelPoint.Y - bitmapPixelPoint.Y;
                if (dx * dx + dy * dy < 0.25)
                    return;
            }

            activeDrawingStroke.Points.Add(Item.DrawingDocument.CreateNormalizedPoint(bitmapPixelPoint.X, bitmapPixelPoint.Y));
            InvalidateDrawingOverlay();
            RenderRequest(true);
        }

        /// <summary>
        /// Finish the current drawing gesture.
        /// </summary>
        private void EndDrawingStroke()
        {
            if (activeDrawingStroke != null && activeDrawingStroke.Points.Count > 0)
            {
                Item.DrawingDocument.AddObject(activeDrawingStroke);
                InvalidateDrawingOverlay();
                RenderRequest(true);
            }

            isDrawingStroke = false;
            activeDrawingStroke = null;
            InvalidateDrawingOverlay();

            if (Capture)
                Capture = false;
        }

        /// <summary>
        /// Delete every visible object hit by the eraser radius.
        /// </summary>
        private void ApplyEraseAt(Point bitmapPixelPoint)
        {
            double eraserRadiusPx = Math.Max(1, drawPanel?.BrushSize ?? 1) / 2.0;
            List<Guid> hitIds = DrawingData.HitTestObjectIds(Item.DrawingDocument, bitmapPixelPoint, eraserRadiusPx);
            if (hitIds.Count == 0)
                return;

            Item.DrawingDocument.RemoveObjects(hitIds);
            InvalidateDrawingOverlay();
            RenderRequest(true);
        }

        /// <summary>
        /// Undo the last drawing operation.
        /// </summary>
        private void UndoDrawingOperation()
        {
            if (HasPendingDrawingStroke())
                return;

            if (Item.DrawingDocument.Undo())
            {
                InvalidateDrawingOverlay();
                RenderRequest();
            }
        }

        /// <summary>
        /// Redo the last undone drawing operation.
        /// </summary>
        private void RedoDrawingOperation()
        {
            if (HasPendingDrawingStroke())
                return;

            if (Item.DrawingDocument.Redo())
            {
                InvalidateDrawingOverlay();
                RenderRequest();
            }
        }
    }
}
