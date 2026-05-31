using System;
using System.Drawing;
using System.Windows.Forms;

namespace Binjyo
{
    public partial class MemoD11
    {
        private const double MouseEvadeRange = 200;
        private const double MouseEvadeBaseStrength = 300;
        private const double MouseEvadeSpringStrength = 0.4;
        private const double MouseEvadeBlend = 0.35;
        private const double MouseEvadeSettledDistance = 0.5;

        private double evadeOffsetX;
        private double evadeOffsetY;

        private bool UpdateEvadeState()
        {
            if (!ShouldApplyEvadeMouse())
                return ResetEvadeOffset();

            Point mouse = Cursor.Position;
            Rectangle displayRect = currentHostBounds;
            double signedDistance = GetRectSignedDistance(displayRect, mouse.X, mouse.Y, out double normalX, out double normalY);
            if (signedDistance >= MouseEvadeRange)
            {
                if (Math.Abs(evadeOffsetX) < MouseEvadeSettledDistance && Math.Abs(evadeOffsetY) < MouseEvadeSettledDistance)
                    return false;
            }

            double normalized = Clamp(1 - signedDistance / MouseEvadeRange, 0, 1);
            double forceMagnitude = MouseEvadeBaseStrength * normalized * normalized;
            double forceX = normalX * forceMagnitude;
            double forceY = normalY * forceMagnitude;
            double springCap = 400 * Math.Max(1, signedDistance / MouseEvadeRange);
            double springX = Clamp(-evadeOffsetX, -springCap, springCap) * MouseEvadeSpringStrength;
            double springY = Clamp(-evadeOffsetY, -springCap, springCap) * MouseEvadeSpringStrength;

            double nextOffsetX = evadeOffsetX + (forceX + springX) * MouseEvadeBlend;
            double nextOffsetY = evadeOffsetY + (forceY + springY) * MouseEvadeBlend;
            if (Math.Abs(nextOffsetX - evadeOffsetX) < 0.001 && Math.Abs(nextOffsetY - evadeOffsetY) < 0.001)
                return false;

            evadeOffsetX = nextOffsetX;
            evadeOffsetY = nextOffsetY;
            return true;
        }

        private bool ResetEvadeOffset()
        {
            if (Math.Abs(evadeOffsetX) < 0.001 && Math.Abs(evadeOffsetY) < 0.001)
                return false;

            evadeOffsetX = 0;
            evadeOffsetY = 0;
            return true;
        }

        private bool ShouldApplyEvadeMouse()
        {
            return Visible &&
                Scene.DisplayMode == EDisplayMode.AutoHide &&
                (EAutoHideBehavior)Properties.Settings.Default.AutoHideBehavior == EAutoHideBehavior.EvadeMouse &&
                !Scene.IsCanvasActive &&
                !Scene.IsDragMoving &&
                !isRotateDragging &&
                !isDrawMode;
        }

        private static double GetRectSignedDistance(Rectangle rect, double x, double y, out double normalX, out double normalY)
        {
            double leftDistance = x - rect.Left;
            double rightDistance = rect.Right - x;
            double topDistance = y - rect.Top;
            double bottomDistance = rect.Bottom - y;
            bool isInside = leftDistance >= 0 && rightDistance >= 0 && topDistance >= 0 && bottomDistance >= 0;

            if (isInside)
            {
                double minDistance = leftDistance;
                normalX = 1;
                normalY = 0;

                if (rightDistance < minDistance)
                {
                    minDistance = rightDistance;
                    normalX = -1;
                    normalY = 0;
                }

                if (topDistance < minDistance)
                {
                    minDistance = topDistance;
                    normalX = 0;
                    normalY = 1;
                }

                if (bottomDistance < minDistance)
                {
                    minDistance = bottomDistance;
                    normalX = 0;
                    normalY = -1;
                }

                return -minDistance;
            }

            double nearestX = Clamp(x, rect.Left, rect.Right);
            double nearestY = Clamp(y, rect.Top, rect.Bottom);
            double deltaX = nearestX - x;
            double deltaY = nearestY - y;
            double distance = Math.Sqrt(deltaX * deltaX + deltaY * deltaY);

            if (distance < 0.0001)
            {
                normalX = 1;
                normalY = 0;
                return 0;
            }

            normalX = deltaX / distance;
            normalY = deltaY / distance;
            return distance;
        }

        private static double Clamp(double value, double minimum, double maximum)
        {
            if (maximum < minimum)
                return minimum;

            return Math.Max(minimum, Math.Min(maximum, value));
        }
    }
}
