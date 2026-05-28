using System;
using System.Windows.Forms;
using Point = System.Drawing.Point;
using Rectangle = System.Drawing.Rectangle;

namespace Binjyo
{
    public partial class MemoD11
    {
        #region ======== Helpers ========

        /// <summary>
        /// Compute the transformed scene item bounds in screen coordinates for this top-level window.
        /// </summary>
        private Rectangle GetTargetBounds()
        {
            var size = Item.GetDisplaySize();
            return new Rectangle(
                (int)Math.Round(Item.Left),
                (int)Math.Round(Item.Top),
                Math.Max(1, (int)Math.Ceiling(size.Width)),
                Math.Max(1, (int)Math.Ceiling(size.Height)));
        }

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

        #endregion
    }
}
