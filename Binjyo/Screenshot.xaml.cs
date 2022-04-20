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
        public static System.Drawing.Point GetDpi(this System.Windows.Forms.Screen screen, DpiType dpiType=DpiType.Effective)
        {
            uint x, y;
            var pnt = new System.Drawing.Point(screen.Bounds.Left + 1, screen.Bounds.Top + 1);
            var mon = MonitorFromPoint(pnt, 2/*MONITOR_DEFAULTTONEAREST*/);
            GetDpiForMonitor(mon, dpiType, out x, out y);
            return new System.Drawing.Point((int)x, (int)y);
        }

        //https://msdn.microsoft.com/en-us/library/windows/desktop/dd145062(v=vs.85).aspx
        [DllImport("User32.dll")]
        private static extern IntPtr MonitorFromPoint([In]System.Drawing.Point pt, [In]uint dwFlags);

        //https://msdn.microsoft.com/en-us/library/windows/desktop/dn280510(v=vs.85).aspx
        [DllImport("Shcore.dll")]
        private static extern IntPtr GetDpiForMonitor([In]IntPtr hmonitor, [In]DpiType dpiType, [Out]out uint dpiX, [Out]out uint dpiY);
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
        private System.Windows.Shapes.Rectangle rectBitmap, rectMask;

        private Bitmap bitmap;

        public Screenshot()
        {
            InitializeComponent();
            Show();
            _CreateObjects();
        }

        public void Shot()
        {
            w = (int)SystemParameters.VirtualScreenWidth;
            h = (int)SystemParameters.VirtualScreenHeight;
            l = (int)SystemParameters.VirtualScreenLeft;
            t = (int)SystemParameters.VirtualScreenTop;

            WindowState = WindowState.Normal;

            var scr = System.Windows.Forms.Screen.FromPoint(new System.Drawing.Point((int)l + 1, (int)t + 1));

            var dpi = scr.GetDpi(DpiType.Effective);
            dpiFactor = (double)dpi.X / 96.0;

            var curr_dpiFactor = VisualTreeHelper.GetDpi(this).DpiScaleX; // Only works under per-monitor DPI mode > https://github.com/microsoft/WPF-Samples/tree/master/
            Console.WriteLine("dpi scale = " + dpiFactor.ToString() + " curr " + curr_dpiFactor);

            Width = w / dpiFactor;
            Height = h / dpiFactor;
            Left = l / dpiFactor;
            Top = t / dpiFactor;
            Console.WriteLine("Left " + Left + " Top " + Top);
            //Console.WriteLine(SystemParameters.VirtualScreenLeft.ToString() + " " + SystemParameters.VirtualScreenTop.ToString());

            this.bitmap = new Bitmap(w, h, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
            using (var g = Graphics.FromImage(this.bitmap))
            {
                g.CopyFromScreen(l, t, 0, 0, this.bitmap.Size);
            }

            IntPtr hbitmap = this.bitmap.GetHbitmap();
            BitmapSource bs = Imaging.CreateBitmapSourceFromHBitmap(hbitmap, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            DeleteObject(hbitmap);

            //canvas.Background = new ImageBrush(bs);
            this.rectBitmap.Fill = new ImageBrush(bs);
            this.rectBitmap.Width = Width;
            this.rectBitmap.Height = Height;
            Canvas.SetLeft(this.rectBitmap, 0);
            Canvas.SetTop(this.rectBitmap, 0);

            _Show();
        }


        private void _CreateObjects()
        {
            // Set up canvas Mask
            this.canvasMask.Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(180, 0, 0, 0));
            RenderOptions.SetEdgeMode(this.canvasMask, EdgeMode.Aliased);

            rectMask = new System.Windows.Shapes.Rectangle
            {
                Fill = new SolidColorBrush(System.Windows.Media.Color.FromArgb(255, 0, 0, 0))
            };
            this.canvasMask.Children.Add(rectMask);

            var maskBrush = new VisualBrush();
            maskBrush.Visual = this.canvasMask;


            // rect to render image
            this.rectBitmap = new System.Windows.Shapes.Rectangle
            {
                OpacityMask = maskBrush
            };
            RenderOptions.SetEdgeMode(this.rectBitmap, EdgeMode.Aliased);
            this.rectBitmap.SnapsToDevicePixels = true;
            this.canvas.Children.Add(this.rectBitmap);


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


        private void _Show()
        {
            //Show();
            Opacity = 1;
            Thread.Sleep(10);
            //canvas.Opacity = 1;

            _UpdateCross();
            Activate();
            isshot = true;
        }

        private void _Hide()
        {
            if (isshot)
            {
                //Hide();
                Opacity = 0;
                _HideCross();
                _HideSelectionRect();
                _HideSelectionPopup();
                Width = 10; Height = 10;

                this.rectBitmap.Fill = null;
                this.bitmap.Dispose();
                this.bitmap = null;

                isshot = false;
                isdrag = false;
                isshot = false;

                GC.Collect();
            }
        }

        private void _UpdateCross()
        {
            double x = System.Windows.Forms.Control.MousePosition.X - l;
            double y = System.Windows.Forms.Control.MousePosition.Y - t;
            x /= dpiFactor; y /= dpiFactor;

            linew.X1 = x; linew.X2 = x; linew.Y1 = y - 200; linew.Y2 = y + 200; linew.Opacity = 0.7;
            lineh.Y1 = y; lineh.Y2 = y; lineh.X1 = x - 200; lineh.X2 = x + 200; lineh.Opacity = 0.7;
        }
        private void _HideCross()
        {
            linew.Opacity = 0; lineh.Opacity = 0;
        }

        
        private void _UpdateSelectionRect()
        {
            int x = System.Windows.Forms.Control.MousePosition.X - l;
            int y = System.Windows.Forms.Control.MousePosition.Y - t;

            this.selectedWidth = x > startx ? x - startx + 2 : startx - x + 2;
            this.selectedHeight = y > starty ? y - starty + 2 : starty - y + 2;
            this.selectedLeft = x > startx ? startx - 1 : x - 1;
            this.selectedTop = y > starty ? starty - 1 : y - 1;
            
            this.rectMask.Width = (int)(selectedWidth / dpiFactor);
            this.rectMask.Height = (int)(selectedHeight / dpiFactor);
            Canvas.SetLeft(this.rectMask, selectedLeft / dpiFactor);
            Canvas.SetTop(this.rectMask, selectedTop / dpiFactor);

            this.rectMask.Opacity = 1;
        }

        private void _HideSelectionRect()
        {
            this.rectMask.Opacity = 0;
        }


        private void _UpdateSelectionPopup()
        {
            int x = System.Windows.Forms.Control.MousePosition.X - l;
            int y = System.Windows.Forms.Control.MousePosition.Y - t;

            popup.HorizontalOffset = (x + 40) / dpiFactor;
            popup.VerticalOffset = (y + 11) / dpiFactor;
            poptext.Text = String.Format("{0}x{1}", (int)(this.rectMask.Width * dpiFactor), (int)(this.rectMask.Height * dpiFactor));
            popup.IsOpen = true;
        }

        private void _HideSelectionPopup()
        {
            popup.IsOpen = false;
        }


        private void _CreateMemo()
        {
            if (this.rectMask.Width > 20 && this.rectMask.Height > 20)
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
                new Memo(croppedImage, this.selectedLeft + 1 + l, this.selectedTop + 1 + t);    // Physical coordinates
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
                _HideCross();
                _UpdateSelectionRect();
                _UpdateSelectionPopup();
            }
            else
            {
                _HideSelectionRect();
                _HideSelectionPopup();
                _UpdateCross();
            }
        }

        private void Window_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            _HideSelectionRect();
            _HideSelectionPopup();
            _HideCross();

            _CreateMemo();

            _Hide();
        }

        private void Window_Deactivated(object sender, EventArgs e)
        {
            _Hide();
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
                _Hide();
                e.Handled = true;
            }
        }

        private void Window_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            _Hide();
            e.Handled = true;
        }

        #endregion


        [System.Runtime.InteropServices.DllImport("gdi32.dll")]
        public static extern bool DeleteObject(IntPtr hObject);
    }
}
