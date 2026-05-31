using System;
using System.Windows.Forms;
using Point = System.Drawing.Point;

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

        #endregion
    }
}
