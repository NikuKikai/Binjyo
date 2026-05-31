using System;
using System.Linq;
using System.Windows.Forms;
using Rectangle = System.Drawing.Rectangle;

namespace Binjyo
{
    public partial class MemoD11
    {
        private static bool isHSVWheelEnabled;

        /// <summary>
        /// Refresh HSV wheel visibility for all active MemoD11 windows after the shared toggle changes.
        /// </summary>
        private static void RefreshAllMemoD11HSVWheelVisibility()
        {
            foreach (var memo in Application.OpenForms.OfType<MemoD11>())
                memo.RefreshHSVWheelVisibility();
        }

        /// <summary>
        /// Show or hide the HSV wheel according to the global toggle and current interaction state.
        /// </summary>
        private void RefreshHSVWheelVisibility()
        {
            if (isHSVWheelEnabled
                && Visible
                && Scene.DisplayMode == EDisplayMode.Expanded
                && !isDrawMode
                && !Scene.IsDragMoving
                && !isRotateDragging
                && IsMouseInside())
            {
                UpdateHSVWheel();
            }
            else
            {
                HideHSVWheel();
            }
        }

        /// <summary>
        /// Hide the HSV wheel helper window.
        /// </summary>
        private void HideHSVWheel()
        {
            hsvWheelWindow?.Hide();
        }

        /// <summary>
        /// Sample the hovered bitmap pixel and update the HSV wheel helper window.
        /// </summary>
        private void UpdateHSVWheel()
        {
            var bitmap = Item.Bitmap;
            if (bitmap == null)
            {
                HideHSVWheel();
                return;
            }

            var mouse = MousePosition;
            double hostX = mouse.X - currentHostBounds.Left;
            double hostY = mouse.Y - currentHostBounds.Top;
            var bitmapPoint = MapHostPositionToBitmap(hostX, hostY);
            int bitmapX = (int)Math.Floor(bitmapPoint.X);
            int bitmapY = (int)Math.Floor(bitmapPoint.Y);

            if (bitmapX < 0 || bitmapX >= bitmap.PixelWidth || bitmapY < 0 || bitmapY >= bitmap.PixelHeight)
            {
                HideHSVWheel();
                return;
            }

            if (hsvWheelWindow == null)
                hsvWheelWindow = new HSVWheelWindow();

            double dpiFactor = Item.DpiFactor <= 0 ? 1.0 : Item.DpiFactor;
            Rectangle sidecarBounds = GetSidecarBounds(
                currentHostBounds.Left,
                currentHostBounds.Top,
                currentHostBounds.Width,
                (int)Math.Round(hsvWheelWindow.Width),
                (int)Math.Round(hsvWheelWindow.Height),
                dpiFactor);

            hsvWheelWindow.UpdateContent(
                bitmap,
                bitmapX,
                bitmapY,
                sidecarBounds.Left,
                sidecarBounds.Top);
        }
    }
}
