using System;
using System.Linq;
using System.Windows;
using System.Runtime.InteropServices;
using Screen = System.Windows.Forms.Screen;


namespace Binjyo
{
    public static class Geo
    {
        public enum DockSide
        {
            Left = 0,
            Right = 1,
            Top = 2,
            Bottom = 3,
        }

        public static bool DoRectsOverlap(Rect a, Rect b)
        {
            return a.Right >= b.Left && b.Right >= a.Left &&
                   a.Bottom >= b.Top && b.Bottom >= a.Top;
        }

        public static bool DoSegmentsOverlap(double a1, double a2, double b1, double b2)
        {
            return a2 >= b1 && b2 >= a1;
        }


        public static void SnapValue(double value, double targetValue, double range, ref double snappedValue, ref double bestDistance)
        {
            double distance = Math.Abs(value - targetValue);
            if (distance <= range && distance < bestDistance)
            {
                snappedValue = targetValue;
                bestDistance = distance;
            }
        }

        public static DockSide GetDockSide(Rect targetRect, Point dropPoint)
        {
            double centerX = targetRect.Left + targetRect.Width / 2.0;
            double centerY = targetRect.Top + targetRect.Height / 2.0;
            double dx = dropPoint.X - centerX;
            double dy = dropPoint.Y - centerY;

            if (Math.Abs(dx) >= Math.Abs(dy))
                return dx < 0 ? DockSide.Left : DockSide.Right;

            return dy < 0 ? DockSide.Top : DockSide.Bottom;
        }

        public static Point GetDockedPosition(Rect targetRect, Size newSize, DockSide dockSide)
        {
            switch (dockSide)
            {
                case DockSide.Left:
                    return new Point(
                        targetRect.Left - newSize.Width,
                        targetRect.Top + (targetRect.Height - newSize.Height) / 2.0);
                case DockSide.Right:
                    return new Point(
                        targetRect.Right,
                        targetRect.Top + (targetRect.Height - newSize.Height) / 2.0);
                case DockSide.Top:
                    return new Point(
                        targetRect.Left + (targetRect.Width - newSize.Width) / 2.0,
                        targetRect.Top - newSize.Height);
                default:
                    return new Point(
                        targetRect.Left + (targetRect.Width - newSize.Width) / 2.0,
                        targetRect.Bottom);
            }
        }

        public static Point GetDockedPositionConstrainedToTargetScreen(Rect targetRect, Size newSize, DockSide dockSide)
        {
            Point dockedPosition = GetDockedPosition(targetRect, newSize, dockSide);
            Screen targetScreen = Screen.FromPoint(new System.Drawing.Point(
                (int)Math.Round(targetRect.Left + targetRect.Width / 2.0),
                (int)Math.Round(targetRect.Top + targetRect.Height / 2.0)));

            if (targetScreen == null)
                return dockedPosition;

            Rect screenRect = new Rect(
                targetScreen.Bounds.Left,
                targetScreen.Bounds.Top,
                targetScreen.Bounds.Width,
                targetScreen.Bounds.Height);

            switch (dockSide)
            {
                case DockSide.Left:
                case DockSide.Right:
                    return new Point(
                        dockedPosition.X,
                        ClampToRange(dockedPosition.Y, screenRect.Top, screenRect.Bottom - newSize.Height));
                case DockSide.Top:
                case DockSide.Bottom:
                default:
                    return new Point(
                        ClampToRange(dockedPosition.X, screenRect.Left, screenRect.Right - newSize.Width),
                        dockedPosition.Y);
            }
        }

        public static void GetAdjustedBounds(double left, double top, double width, double height, out double adjustedLeft, out double adjustedTop)
        {
            var targetRect = new Rect(left, top, Math.Max(1, width), Math.Max(1, height));
            foreach (var screen in Screen.AllScreens)
            {
                var screenRect = new Rect(
                    screen.Bounds.Left,
                    screen.Bounds.Top,
                    screen.Bounds.Width,
                    screen.Bounds.Height);

                if (screenRect.IntersectsWith(targetRect))
                {
                    adjustedLeft = ClampToRange(left, screenRect.Left, screenRect.Right - width);
                    adjustedTop = ClampToRange(top, screenRect.Top, screenRect.Bottom - height);
                    return;
                }
            }

            Screen nearestScreen = Screen.AllScreens
                .OrderBy(screen => GetDistanceSquaredToScreen(targetRect, screen))
                .FirstOrDefault();

            if (nearestScreen == null)
            {
                adjustedLeft = left;
                adjustedTop = top;
                return;
            }

            adjustedLeft = ClampToRange(left, nearestScreen.Bounds.Left, nearestScreen.Bounds.Right - width);
            adjustedTop = ClampToRange(top, nearestScreen.Bounds.Top, nearestScreen.Bounds.Bottom - height);
        }


        #region  ======== Screen Bounds ========
        public static Int32Rect GetAllScreenBoundsPhysical()
        {
            // Get physical resolutions (https://stackoverflow.com/a/1317252)
            var rect = new System.Drawing.Rectangle(int.MaxValue, int.MaxValue, int.MinValue, int.MinValue);
            foreach (Screen screen in Screen.AllScreens)
                rect = System.Drawing.Rectangle.Union(rect, screen.Bounds);
            return new Int32Rect(rect.Left, rect.Top, rect.Width, rect.Height);
        }

        public static Int32Rect GetAllScreenBoundsPhysical2()
        {
            int left = GetSystemMetrics(SM_XVIRTUALSCREEN);
            int top = GetSystemMetrics(SM_YVIRTUALSCREEN);
            int width = GetSystemMetrics(SM_CXVIRTUALSCREEN);
            int height = GetSystemMetrics(SM_CYVIRTUALSCREEN);
            return new Int32Rect(left, top, width, height);
        }
        internal const int SM_XVIRTUALSCREEN = 76;
        internal const int SM_YVIRTUALSCREEN = 77;
        internal const int SM_CXVIRTUALSCREEN = 78;
        internal const int SM_CYVIRTUALSCREEN = 79;

        [DllImport("user32.dll")]
        internal static extern int GetSystemMetrics(int nIndex);

        #endregion


        #region  ======== DPI Handling ========

        public static double GetDpiFactorAt(double x, double y, DpiType dpiType = DpiType.Effective)
        {
            var pnt = new System.Drawing.Point((int)x, (int)y);
            var mon = MonitorFromPoint(pnt, 2/*MONITOR_DEFAULTTONEAREST*/);
            GetDpiForMonitor(mon, dpiType, out uint dpiX, out uint dpiY);
            return dpiX / 96.0;
        }

        public static double GetDpiFactor(this Screen screen, DpiType dpiType = DpiType.Effective)
        {
            return GetDpiFactorAt(screen.Bounds.Left + 1, screen.Bounds.Top + 1, dpiType);
        }

        //https://msdn.microsoft.com/en-us/library/windows/desktop/dd145062(v=vs.85).aspx
        [DllImport("User32.dll")]
        private static extern IntPtr MonitorFromPoint([In] System.Drawing.Point pt, [In] uint dwFlags);

        //https://msdn.microsoft.com/en-us/library/windows/desktop/dn280510(v=vs.85).aspx
        [DllImport("Shcore.dll")]
        private static extern IntPtr GetDpiForMonitor([In] IntPtr hmonitor, [In] DpiType dpiType, [Out] out uint dpiX, [Out] out uint dpiY);

        private static double ClampToRange(double value, double minimum, double maximum)
        {
            if (maximum < minimum)
                return minimum;
            return Math.Max(minimum, Math.Min(maximum, value));
        }

        private static double GetDistanceSquaredToScreen(Rect rect, Screen screen)
        {
            double dx = 0;
            if (rect.Right < screen.Bounds.Left)
                dx = screen.Bounds.Left - rect.Right;
            else if (rect.Left > screen.Bounds.Right)
                dx = rect.Left - screen.Bounds.Right;

            double dy = 0;
            if (rect.Bottom < screen.Bounds.Top)
                dy = screen.Bounds.Top - rect.Bottom;
            else if (rect.Top > screen.Bounds.Bottom)
                dy = rect.Top - screen.Bounds.Bottom;

            return dx * dx + dy * dy;
        }
        #endregion

    }

    public enum DpiType
    {
        Effective = 0,
        Angular = 1,
        Raw = 2,
    }
}
