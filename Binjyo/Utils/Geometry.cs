using System;
using System.Windows;
using System.Runtime.InteropServices;
using Screen = System.Windows.Forms.Screen;


namespace Binjyo
{
    public static class Geo
    {

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
        #endregion

    }

    public enum DpiType
    {
        Effective = 0,
        Angular = 1,
        Raw = 2,
    }
}