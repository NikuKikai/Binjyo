using System;
using System.Windows;

namespace Binjyo
{
    public sealed class WindowCaptureSelection
    {
        public IntPtr WindowHandle { get; set; }
        public Int32Rect WindowBounds { get; set; }
        public Int32Rect SelectionBounds { get; set; }
        public int OffsetX { get; set; }
        public int OffsetY { get; set; }
        public int PixelWidth { get; set; }
        public int PixelHeight { get; set; }
    }
}
