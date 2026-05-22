using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Binjyo
{
    public sealed class FeatureMatchOverlayWindow : Window
    {
        private readonly Path overlayPath;
        private static readonly SolidColorBrush lineBrush = CreateLineBrush();
        private IntPtr hwnd = IntPtr.Zero;

        public FeatureMatchOverlayWindow()
        {
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;
            ShowInTaskbar = false;
            ShowActivated = false;
            Topmost = true;
            Focusable = false;
            IsHitTestVisible = false;
            Left = SystemParameters.VirtualScreenLeft;
            Top = SystemParameters.VirtualScreenTop;
            Width = SystemParameters.VirtualScreenWidth;
            Height = SystemParameters.VirtualScreenHeight;
            Opacity = 0;

            overlayPath = new Path
            {
                IsHitTestVisible = false,
                Stroke = lineBrush,
                StrokeThickness = 1.5,
                SnapsToDevicePixels = true
            };

            Content = overlayPath;
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            hwnd = new WindowInteropHelper(this).Handle;
            if (hwnd != IntPtr.Zero)
            {
                WinService.SetWindowExTransparent(hwnd);
                WinService.BringWindowToTopmost(hwnd);
            }
        }

        public void UpdateLines(IReadOnlyCollection<Tuple<Point, Point>> lines)
        {
            if (lines == null || lines.Count == 0)
            {
                overlayPath.Data = null;
                return;
            }

            var geometry = new StreamGeometry();
            using (StreamGeometryContext context = geometry.Open())
            {
                foreach (Tuple<Point, Point> line in lines)
                {
                    context.BeginFigure(
                        new Point(line.Item1.X - Left, line.Item1.Y - Top),
                        false,
                        false);
                    context.LineTo(
                        new Point(line.Item2.X - Left, line.Item2.Y - Top),
                        true,
                        false);
                }
            }
            geometry.Freeze();
            overlayPath.Data = geometry;

            if (!IsVisible)
                Show();

            BringToFront();
        }

        public void EnsureShown()
        {
            if (!IsVisible)
                Show();

            BringToFront();
        }

        public void SetOverlayOpacity(double opacity)
        {
            Opacity = opacity;
        }

        public void HideOverlay()
        {
            overlayPath.Data = null;
            Opacity = 0;
        }

        private void BringToFront()
        {
            if (hwnd != IntPtr.Zero)
                WinService.BringWindowToTopmost(hwnd);
        }

        private static SolidColorBrush CreateLineBrush()
        {
            var brush = new SolidColorBrush(Color.FromArgb(220, 255, 64, 64));
            brush.Freeze();
            return brush;
        }
    }
}
