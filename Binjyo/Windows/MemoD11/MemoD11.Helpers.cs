using System;
using System.Windows.Forms;
using Rectangle = System.Drawing.Rectangle;
using Point = System.Drawing.Point;
using WpfPoint = System.Windows.Point;

namespace Binjyo
{
    public partial class MemoD11
    {
        #region ======== Helpers ========

        /// <summary>
        /// Measure the current mouse pointer angle around the fixed drag-rotation center.
        /// </summary>
        private double GetPointerAngleToFixedCenter()
        {
            Point cursor = Cursor.Position;
            double dx = cursor.X - rotateCenterScreenX;
            double dy = cursor.Y - rotateCenterScreenY;
            return Math.Atan2(dy, dx) * 180.0 / Math.PI;
        }

        /// <summary>
        /// Fold an angle difference into the shortest signed range so drag rotation stays stable across +/-180 degrees.
        /// </summary>
        private static double NormalizeAngleDelta(double delta)
        {
            while (delta <= -180.0) delta += 360.0;
            while (delta > 180.0) delta -= 360.0;
            return delta;
        }

        private bool IsMouseInside()
        {
            var mouse = MousePosition;
            double x = mouse.X;
            double y = mouse.Y;
            return currentHostBounds.Left <= x && x <= currentHostBounds.Right
                && currentHostBounds.Top <= y && y <= currentHostBounds.Bottom;
        }

        /// <summary>
        /// Map a host-local pixel position back into the source bitmap using the same inverse transform as rendering.
        /// </summary>
        private WpfPoint MapHostPositionToBitmap(double hostX, double hostY)
        {
            var inverse = Item.TransformInv.Value;
            double itemLocalX = hostX - renderContentOffsetX;
            double itemLocalY = hostY - renderContentOffsetY;
            return new WpfPoint(
                itemLocalX * inverse.M11 + itemLocalY * inverse.M21 + inverse.OffsetX,
                itemLocalX * inverse.M12 + itemLocalY * inverse.M22 + inverse.OffsetY);
        }

        /// <summary>
        /// Place an auxiliary window flush against the host window, preferring the right edge and falling back to the left edge.
        /// </summary>
        private Rectangle GetSidecarBounds(int hostLeft, int hostTop, int hostWidth, int sidecarWidth, int sidecarHeight, double dpiFactor)
        {
            int anchorX = (int)Math.Round((hostLeft + hostWidth) * dpiFactor);
            int anchorY = (int)Math.Round(hostTop * dpiFactor);
            var screen = Screen.FromPoint(new Point(anchorX, anchorY));

            double screenRight = screen.Bounds.Right / dpiFactor;
            int sidecarLeft = hostLeft + hostWidth;
            if (sidecarLeft + sidecarWidth > screenRight)
                sidecarLeft = hostLeft - sidecarWidth;

            return new Rectangle(sidecarLeft, hostTop, sidecarWidth, sidecarHeight);
        }

        #endregion
    }
}
