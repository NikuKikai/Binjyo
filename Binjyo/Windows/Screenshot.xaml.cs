using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Runtime.InteropServices;
using Screen = System.Windows.Forms.Screen;


namespace Binjyo
{
    public enum DpiType
    {
        Effective = 0,
        Angular = 1,
        Raw = 2,
    }
    public static class ScreenExtensions
    {
        public static System.Drawing.Point GetDpi(this Screen screen, DpiType dpiType = DpiType.Effective)
        {
            uint x, y;
            var pnt = new System.Drawing.Point(screen.Bounds.Left + 1, screen.Bounds.Top + 1);
            var mon = MonitorFromPoint(pnt, 2/*MONITOR_DEFAULTTONEAREST*/);
            GetDpiForMonitor(mon, dpiType, out x, out y);
            return new System.Drawing.Point((int)x, (int)y);
        }

        //https://msdn.microsoft.com/en-us/library/windows/desktop/dd145062(v=vs.85).aspx
        [DllImport("User32.dll")]
        private static extern IntPtr MonitorFromPoint([In] System.Drawing.Point pt, [In] uint dwFlags);

        //https://msdn.microsoft.com/en-us/library/windows/desktop/dn280510(v=vs.85).aspx
        [DllImport("Shcore.dll")]
        private static extern IntPtr GetDpiForMonitor([In] IntPtr hmonitor, [In] DpiType dpiType, [Out] out uint dpiX, [Out] out uint dpiY);
    }

    /// <summary>
    /// Interaction logic for Screenshot.xaml
    /// </summary>
    public partial class Screenshot : Window
    {
        private double dpiFactor = 1;

        private bool isshot = false;
        private bool isdrag = false;

        private int w, h, l, t;  // Physical Pixel
        private int startx, starty;  // Physical Pixel
        private int selectedLeft, selectedTop, selectedWidth, selectedHeight;  // Physical Pixel

        private Line linew, lineh;
        private System.Windows.Shapes.Rectangle rectBitmap;
        private System.Windows.Shapes.Rectangle maskTop, maskLeft, maskRight, maskBottom;

        private Bitmap bitmap;
        private BitmapSource screenshotSource;
        private ImageBrush screenshotBrush;

        public Screenshot()
        {
            InitializeComponent();
            _CreateObjects();
        }

        public void Shot()
        {
            WindowState = WindowState.Normal;

            // Scaled screen size
            // BUG: If the app start with dpi ratio X, and then changed to Y < X, following vars does not change.
            // var wv = (int)SystemParameters.VirtualScreenWidth;
            // var hv = (int)SystemParameters.VirtualScreenHeight;
            // var lv = (int)SystemParameters.VirtualScreenLeft;
            // var tv = (int)SystemParameters.VirtualScreenTop;

            // Get physical resolutions (https://stackoverflow.com/a/1317252)
            var rect = new System.Drawing.Rectangle(int.MaxValue, int.MaxValue, int.MinValue, int.MinValue);
            foreach (Screen screen in Screen.AllScreens)
                rect = System.Drawing.Rectangle.Union(rect, screen.Bounds);
            w = rect.Width;
            h = rect.Height;
            l = rect.Left;
            t = rect.Top;

            // Get DPI
            var scr = Screen.FromPoint(new System.Drawing.Point(l + 1, t + 1));
            var dpi = scr.GetDpi(DpiType.Effective);
            dpiFactor = dpi.X / 96.0;

            // Get DPI another method
            // var curr_dpiFactor = VisualTreeHelper.GetDpi(this).DpiScaleX; // Only works under per-monitor DPI mode > https://github.com/microsoft/WPF-Samples/tree/master/
            // Console.WriteLine("dpi scale = " + dpiFactor.ToString() + " curr " + curr_dpiFactor);

            // Resize window to cover all screen
            Width = w / dpiFactor;
            Height = h / dpiFactor;
            Left = l / dpiFactor;
            Top = t / dpiFactor;
            Console.WriteLine("Left " + Left + " Top " + Top + " W " + Width + " H " + Height);

            ReleaseScreenshotResources();

            // Get Screen bitmap
            this.bitmap = new Bitmap(w, h, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
            using (var g = Graphics.FromImage(this.bitmap))
            {
                g.CopyFromScreen(l, t, 0, 0, this.bitmap.Size);
            }

            screenshotSource = this.bitmap.ToBitmapSource(PixelFormats.Bgr24);
            screenshotSource.Freeze();
            screenshotBrush = new ImageBrush(screenshotSource)
            {
                Stretch = Stretch.Fill,
                AlignmentX = AlignmentX.Left,
                AlignmentY = AlignmentY.Top
            };

            //canvas.Background = new ImageBrush(bs);
            this.rectBitmap.Fill = screenshotBrush;
            this.rectBitmap.Width = Width;
            this.rectBitmap.Height = Height;
            Canvas.SetLeft(this.rectBitmap, 0);
            Canvas.SetTop(this.rectBitmap, 0);
            HideSelectionRect();

            _Show();
        }


        private void _CreateObjects()
        {
            // Set up canvas mask with four rectangles to avoid large opacity-mask surfaces.
            this.canvasMask.Background = System.Windows.Media.Brushes.Transparent;
            RenderOptions.SetEdgeMode(this.canvasMask, EdgeMode.Aliased);


            // rect to render image
            this.rectBitmap = new System.Windows.Shapes.Rectangle();
            RenderOptions.SetEdgeMode(this.rectBitmap, EdgeMode.Aliased);
            this.rectBitmap.SnapsToDevicePixels = true;
            this.canvas.Children.Add(this.rectBitmap);

            var overlayFill = new SolidColorBrush(System.Windows.Media.Color.FromArgb(180, 0, 0, 0));
            maskTop = CreateMaskRectangle(overlayFill);
            maskLeft = CreateMaskRectangle(overlayFill);
            maskRight = CreateMaskRectangle(overlayFill);
            maskBottom = CreateMaskRectangle(overlayFill);

            this.canvasMask.Children.Add(maskTop);
            this.canvasMask.Children.Add(maskLeft);
            this.canvasMask.Children.Add(maskRight);
            this.canvasMask.Children.Add(maskBottom);


            // lines
            linew = new Line
            {
                Stroke = System.Windows.Media.Brushes.White,
                StrokeThickness = 1,
                Y1 = 0
            };
            this.canvas.Children.Add(linew);
            lineh = new Line
            {
                Stroke = System.Windows.Media.Brushes.White,
                StrokeThickness = 1,
                X1 = 0
            };
            this.canvas.Children.Add(lineh);

        }

        private System.Windows.Shapes.Rectangle CreateMaskRectangle(System.Windows.Media.Brush fill)
        {
            return new System.Windows.Shapes.Rectangle
            {
                Fill = fill
            };
        }


        private void _Show()
        {
            if (!IsVisible)
                Show();
            Opacity = 1;
            Thread.Sleep(10);
            //canvas.Opacity = 1;

            UpdateCross();
            Activate();
            isshot = true;
        }

        private void CloseThis()
        {
            if (isshot)
            {
                Opacity = 0;
                HideCross();
                HideSelectionRect();
                HideSelectionPopup();
                Width = 10; Height = 10;

                ReleaseScreenshotResources();
                HideSelectionRect();

                isshot = false;
                isdrag = false;
                isshot = false;

                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
                Close();
            }
        }

        private void ReleaseScreenshotResources()
        {
            this.rectBitmap.Fill = null;

            if (screenshotBrush != null)
            {
                screenshotBrush.ImageSource = null;
                screenshotBrush = null;
            }

            screenshotSource = null;
            this.bitmap?.Dispose();
            this.bitmap = null;
        }

        private void UpdateCross()
        {
            double x = System.Windows.Forms.Control.MousePosition.X - l;
            double y = System.Windows.Forms.Control.MousePosition.Y - t;
            x /= dpiFactor; y /= dpiFactor;

            linew.X1 = x; linew.X2 = x; linew.Y1 = 0; linew.Y2 = Height; linew.Opacity = 0.7;
            lineh.Y1 = y; lineh.Y2 = y; lineh.X1 = 0; lineh.X2 = Width; lineh.Opacity = 0.7;
        }
        private void HideCross()
        {
            linew.Opacity = 0; lineh.Opacity = 0;
        }


        private void UpdateSelectionRect()
        {
            double x = System.Windows.Forms.Control.MousePosition.X - l;
            double y = System.Windows.Forms.Control.MousePosition.Y - t;
            x /= dpiFactor; y /= dpiFactor;
            int xint = (int)(x + 0.499); int yint = (int)(y + 0.499);

            this.selectedWidth = xint > startx ? xint - startx + 2 : startx - xint + 2;
            this.selectedHeight = yint > starty ? yint - starty + 2 : starty - yint + 2;
            this.selectedLeft = xint > startx ? startx - 1 : xint - 1;
            this.selectedTop = yint > starty ? starty - 1 : yint - 1;

            double selectionLeft = selectedLeft / dpiFactor;
            double selectionTop = selectedTop / dpiFactor;
            double selectionWidth = selectedWidth / dpiFactor;
            double selectionHeight = selectedHeight / dpiFactor;

            maskTop.Width = Width;
            maskTop.Height = Math.Max(0, selectionTop);
            Canvas.SetLeft(maskTop, 0);
            Canvas.SetTop(maskTop, 0);

            maskLeft.Width = Math.Max(0, selectionLeft);
            maskLeft.Height = Math.Max(0, selectionHeight);
            Canvas.SetLeft(maskLeft, 0);
            Canvas.SetTop(maskLeft, selectionTop);

            maskRight.Width = Math.Max(0, Width - (selectionLeft + selectionWidth));
            maskRight.Height = Math.Max(0, selectionHeight);
            Canvas.SetLeft(maskRight, selectionLeft + selectionWidth);
            Canvas.SetTop(maskRight, selectionTop);

            maskBottom.Width = Width;
            maskBottom.Height = Math.Max(0, Height - (selectionTop + selectionHeight));
            Canvas.SetLeft(maskBottom, 0);
            Canvas.SetTop(maskBottom, selectionTop + selectionHeight);
        }

        private void HideSelectionRect()
        {
            maskTop.Width = Width;
            maskTop.Height = Height;
            Canvas.SetLeft(maskTop, 0);
            Canvas.SetTop(maskTop, 0);

            maskLeft.Width = 0;
            maskLeft.Height = 0;
            maskRight.Width = 0;
            maskRight.Height = 0;
            maskBottom.Width = 0;
            maskBottom.Height = 0;
        }


        private void UpdateSelectionPopup()
        {
            double x = System.Windows.Forms.Control.MousePosition.X - l;
            double y = System.Windows.Forms.Control.MousePosition.Y - t;

            popup.HorizontalOffset = (x + 40) / dpiFactor;
            popup.VerticalOffset = (y + 11) / dpiFactor;
            poptext.Text = String.Format("{0}x{1}", this.selectedWidth, this.selectedHeight);
            popup.IsOpen = true;
        }

        private void HideSelectionPopup()
        {
            popup.IsOpen = false;
        }


        private void CreateMemo()
        {
            if (this.selectedWidth > 20 && this.selectedHeight > 20)
            {
                // Crop bitmap with rect
                var croppedImage = new Bitmap(this.selectedWidth, this.selectedHeight);
                using (var graphics = Graphics.FromImage(croppedImage))
                {
                    var srcrect = new System.Drawing.Rectangle(
                        this.selectedLeft + 1, this.selectedTop + 1,
                        croppedImage.Width, croppedImage.Height);
                    graphics.DrawImage(this.bitmap, 0, 0, srcrect, GraphicsUnit.Pixel);
                }

                // Create Memo from cropped bitmap
                var item = Scene.CreateItem(croppedImage, selectedLeft + 1 + l, selectedTop + 1 + t);
                Memo memo = new Memo(item);
                Scene.Focus(item.Id);
            }
        }


        #region ======== Event Callbacks ========

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            isdrag = true;
            startx = System.Windows.Forms.Control.MousePosition.X - l;
            starty = System.Windows.Forms.Control.MousePosition.Y - t;
        }

        private void Window_MouseMove(object sender, MouseEventArgs e)
        {

            if (isdrag)
            {
                HideCross();
                UpdateSelectionRect();
                UpdateSelectionPopup();
            }
            else
            {
                HideSelectionRect();
                HideSelectionPopup();
                UpdateCross();
            }
        }

        private void Window_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            HideSelectionRect();
            HideSelectionPopup();
            HideCross();

            Opacity = 0;
            CreateMemo();

            CloseThis();
        }

        private void Window_Deactivated(object sender, EventArgs e)
        {
            CloseThis();
        }

        private void Window_StateChanged(object sender, EventArgs e)
        {
            /*if (WindowState != WindowState.Maximized)
            {
                WindowState = WindowState.Maximized;
                Topmost = true;
            }*/
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            // Console.WriteLine(e.Key);
            if (e.Key == Key.Escape || e.Key == Key.System || e.Key == Key.LeftAlt ||
                e.Key == Key.RightAlt || e.Key == Key.LWin || e.Key == Key.RWin)
            {
                CloseThis();
                e.Handled = true;
            }
        }

        private void Window_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            CloseThis();
            e.Handled = true;
        }

        #endregion


        [System.Runtime.InteropServices.DllImport("gdi32.dll")]
        public static extern bool DeleteObject(IntPtr hObject);
    }
}
