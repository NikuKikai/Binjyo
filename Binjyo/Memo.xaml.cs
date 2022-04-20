using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Drawing;
using System.Windows.Interop;
//using System.Threading;
using System.Timers;
using System.Runtime.InteropServices;
using System.Drawing.Imaging;


namespace Binjyo
{
    /// <summary>
    /// Memo.xaml の相互作用ロジック
    /// </summary>
    public partial class Memo : Window
    {
        private Timer timer = null;

        private double dpiFactor = 1;

        private BitmapSource bitmpasource;
        private Bitmap bitmap;
        private Bitmap bitmapGreyscale = null;

        private bool isShowingOriginal = true;
        private bool isShowingGreyscale = false;

        // effect
        private double scale = 1;
        private bool isEffectGrey = false;
        private int isEffectThreshold = 0;

        private double lastx, lasty;
        private bool isdrag = false;
        private bool isOverButton = false;

        private int lockmode = 0;


        public Memo(Bitmap bmp, int left, int top)    // Physical coordinates
        {
            InitializeComponent();
            InitializeTimer();

            int w = bmp.Width;
            int h = bmp.Height;  // Physical pixel

            var centerx = left + w / 2;
            var centery = top + h / 2;

            //var scr = System.Windows.Forms.Screen.FromPoint(System.Windows.Forms.Control.MousePosition);
            var scr = System.Windows.Forms.Screen.FromPoint(new System.Drawing.Point(centerx, centery));

            var dpi = scr.GetDpi(DpiType.Effective);
            dpiFactor = (double)dpi.X / 96.0;
            Console.WriteLine(dpiFactor);


            //this.dpiFactor = VisualTreeHelper.GetDpi(this as Visual).DpiScaleX;
            //Console.WriteLine("Memo init  " + dpiFactor.ToString());
            Left = left / dpiFactor; Top = top / dpiFactor;
            Width = bmp.Width / dpiFactor; Height = bmp.Height / dpiFactor;

            this.bitmap = bmp;
            this.ShowBitmap(bmp);
        }

        protected void _Close()
        {
            if (this.bitmap != null) this.bitmap.Dispose();
            if (this.timer != null) this.timer.Stop();
            this.Close();
            GC.Collect();
        }

        protected void ShowBitmap(Bitmap bmp)
        {
            IntPtr hbitmap = bmp.GetHbitmap();
            bitmpasource = Imaging.CreateBitmapSourceFromHBitmap(hbitmap, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            DeleteObject(hbitmap);

            this.image.Source = bitmpasource;
            Show();
        }

        private void InitializeTimer()
        {
            this.timer = new Timer(interval: 0.1);
            this.timer.Elapsed += new ElapsedEventHandler(TimerElapsed);
            this.timer.Enabled = true;
        }
        private delegate void TimerDelegate();
        private void TimerElapsed(object sender, ElapsedEventArgs e)
        {
            this.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Normal, new TimerDelegate(TimerHandler));
        }
        private void TimerHandler()
        {
            switch (lockmode)
            {
                case 1:
                    double x = System.Windows.Forms.Control.MousePosition.X;
                    double y = System.Windows.Forms.Control.MousePosition.Y;
                    if (Left < x && x < Left + Width &&
                        Top < y && y < Top + Height)
                    {
                        image.Opacity = 0;
                        button.Opacity = 1;
                    }
                    else
                    {
                        image.Opacity = 1;
                        button.Opacity = 0.005;
                    }
                    break;

                case 0:
                    if (isdrag)
                    {
                        if (Mouse.LeftButton != MouseButtonState.Pressed)
                            isdrag = false;
                        double xx = System.Windows.Forms.Control.MousePosition.X;
                        double yy = System.Windows.Forms.Control.MousePosition.Y;
                        //var dx = (xx - lastx) / dpiFactor;
                        Left += (xx - lastx) / dpiFactor;
                        Top += (yy - lasty) / dpiFactor;
                        lastx = xx;
                        lasty = yy;
                        //Console.WriteLine(xx.ToString()+" "+dx.ToString()+ " " + Left.ToString());
                    }
                    break;

                default:
                    break;
            }
        }


        // ========== Operations ==========

        public void Minimize()
        {
            isdrag = false; lockmode = 2;
            image.Opacity = 0;

            button.Opacity = 1;
            button.Content = FindResource("lockmin");
        }
        public void Expand()
        {
            isdrag = false; lockmode = 0;
            image.Opacity = 1;

            button.Opacity = 0.7;
            button.Content = FindResource("lockoff");
        }
        public void Resize(double s)
        {
            if (!isdrag)
            {
                scale = s;
                Width = this.bitmap.Width * s;
                Height = this.bitmap.Height * s;
            }
        }
        public void ResizeDelta(double ds)
        {
            scale += ds;
            if (scale <= 0 || scale >= 10 ||
                this.bitmap.Width * scale < 25 || this.bitmap.Height * scale < 25 ||
                this.bitmap.Width * scale > SystemParameters.VirtualScreenWidth || this.bitmap.Height * scale > SystemParameters.VirtualScreenHeight
            )
                scale -= ds;
            else
                Resize(scale);
        }
        public void ResetSize()
        {
            Resize(1);
        }

        public void Save()
        {
            Microsoft.Win32.SaveFileDialog dlg = new Microsoft.Win32.SaveFileDialog();
            var time = DateTime.Now;
            string formattedTime = time.ToString("yyyy-MM-dd-hh-mm-ss");
            dlg.FileName = formattedTime;
            dlg.Filter = "Png Image|*.png"; //|Bitmap Image|*.bmp|Gif Image|*.gif";
            if (dlg.ShowDialog() == true)
            {
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(bitmpasource));
                using (var stream = dlg.OpenFile())
                {
                    encoder.Save(stream);
                }
            }
        }

        public void SwitchGreyscale()
        {
            if (!this.isShowingGreyscale)
            {
                this.bitmapGreyscale = MakeGrayscale3(this.bitmap);
                this.ShowBitmap(this.bitmapGreyscale);
                this.isShowingOriginal = false;
                this.isShowingGreyscale = true;
            }
            else
            {
                this.ShowBitmap(this.bitmap);
                this.bitmapGreyscale.Dispose(); // TODO
                this.bitmapGreyscale = null;
                this.isShowingOriginal = true;
                this.isShowingGreyscale = false;
            }
        }

        public void UpdateHSVWheel()
        {
            popup.IsOpen = true;

            double x = System.Windows.Forms.Control.MousePosition.X - Left;
            double y = System.Windows.Forms.Control.MousePosition.Y - Top;
            if (x < 0 || x >= Width || y < 0 || y >= Height)
                return;
            var px = bitmap.GetPixel((int)(x / scale), (int)(y / scale));

            popup.HorizontalOffset = x + 180;
            popup.VerticalOffset = y + 30;

            //  Update Hue marker
            float hue = px.GetHue();
            HSV_SV.Hue = hue;
            var radius = HSVWheel.Width / 2 - HSVWheel.StrokeThickness / 2;
            var angle = (hue + 210) / 180 * Math.PI;
            var xc = HSVWheel.Width / 2 + Math.Cos(angle) * radius;
            var yc = HSVWheel.Height / 2 + Math.Sin(angle) * radius;
            HueMark.Margin = new Thickness(xc - HueMark.Width / 2, yc - HueMark.Height / 2, 0, 0);

            //  Update SV marker
            var v = (double)Math.Max(Math.Max(px.R, px.G), px.B) / 255;
            var s = (double)Math.Min(Math.Min(px.R, px.G), px.B) / 255;
            if (v == 0) s = 1;
            else s = (v - s) / v; // S of HSV is different from px.GetSaturation(), which is S of HSL(?)

            if (v < 0.5) SVMark.Stroke = new SolidColorBrush(Colors.White);
            else SVMark.Stroke = new SolidColorBrush(Colors.Black);

            SVMark.Margin = new Thickness(
                HSVWheel.Width / 2 - HSVRect.Width / 2 + s * HSVRect.Width - SVMark.Width / 2,
                HSVWheel.Height / 2 + HSVRect.Height / 2 - v * HSVRect.Height - SVMark.Height / 2, 0, 0);

            // Show text
            HSVText.Text = String.Format("H{0: 000}° S{1: 000} L{2: 000}", (int)px.GetHue(), (int)(px.GetSaturation() * 100), (int)(px.GetBrightness() * 100));
        }


        #region ========== EVENT ========== 

        protected override void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi)
        {
            dpiFactor = newDpi.DpiScaleX;
            Console.WriteLine("DPI Changed");
            Console.WriteLine(newDpi.DpiScaleX);
            Console.WriteLine(Left);
        }


        // ========== BUTTON ==========
        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            switch(e.Key)
            {
                case Key.Escape:
                    this._Close();
                    e.Handled = true;
                    break;
                case Key.S:
                    Save();
                    break;
                case Key.C:
                    if(Keyboard.IsKeyDown(Key.LeftCtrl))
                        Clipboard.SetImage(bitmpasource);
                    break;
                case Key.X:
                    if (Keyboard.IsKeyDown(Key.LeftCtrl))
                    {
                        Clipboard.SetImage(bitmpasource);
                        this._Close();
                    }
                    break;
                case Key.R:
                    ResetSize();
                    break;
                case Key.D:
                    ResizeDelta(-0.2);
                    break;
                case Key.F:
                    ResizeDelta(0.2);
                    break;
                case Key.G:
                    SwitchGreyscale();
                    break;
                case Key.LeftShift:
                    this.UpdateHSVWheel();
                    break;
                default:
                    break;
            }
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            isdrag = true;
            lastx = System.Windows.Forms.Control.MousePosition.X;
            lasty = System.Windows.Forms.Control.MousePosition.Y;
        }

        private void Window_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
        }

        private void Window_MouseMove(object sender, MouseEventArgs e)
        {
            // Show HSV
            if (!isdrag && Keyboard.IsKeyDown(Key.LeftShift) && !isOverButton)
                this.UpdateHSVWheel();
            else
                popup.IsOpen = false;
        }

        private void Window_MouseUp(object sender, MouseButtonEventArgs e)
        {
            isdrag = false;
        }

        private void Window_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            Clipboard.SetImage(bitmpasource);
            this._Close();
        }

        private void Window_MouseEnter(object sender, MouseEventArgs e)
        {
        }

        private void Window_MouseLeave(object sender, MouseEventArgs e)
        {
            //isdrag = false;
            popup.IsOpen = false;
        }

        private void Window_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (Keyboard.IsKeyDown(Key.LeftCtrl))
            {
                if (e.Delta > 0) ResizeDelta(0.1);
                else ResizeDelta(-0.1);
            }
        }

        private void Window_Deactivated(object sender, EventArgs e)
        {
            isdrag = false;
            popup.IsOpen = false;
        }

        private void Window_PreviewMouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
        }


        // ========== BUTTON ==========

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            switch (lockmode)
            {
                case 2:
                    Expand();
                    break;
                case 0:
                    lockmode = 1;
                    //image.Opacity = 0;
                    button.Opacity = 1;
                    button.Content = FindResource("lockon");
                    break;
                case 1:
                    Minimize();
                    break;
            }
        }

        private void Button_MouseDown(object sender, MouseButtonEventArgs e)
        {
        }

        private void Button_PreviewMouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
        }

        private void Button_MouseEnter(object sender, MouseEventArgs e)
        {
            isOverButton = true;
            if (lockmode == 0)
            {
                button.Opacity = 1;
            }
        }

        private void Button_MouseLeave(object sender, MouseEventArgs e)
        {
            isOverButton = false;
            if (lockmode == 0)
            {
                button.Opacity = 0.005;
            }
        }

        #endregion


        #region UTIL
        // ==================================

        public static Bitmap MakeGrayscale3(Bitmap original)
        {
            // https://stackoverflow.com/questions/2265910/convert-an-image-to-grayscale
            //create a blank bitmap the same size as original
            Bitmap newBitmap = new Bitmap(original.Width, original.Height);

            //get a graphics object from the new image
            using (Graphics g = Graphics.FromImage(newBitmap))
            {

                //create the grayscale ColorMatrix
                ColorMatrix colorMatrix = new ColorMatrix(
                    new float[][]
                    {
                        new float[] {.3f, .3f, .3f, 0, 0},
                        new float[] {.59f, .59f, .59f, 0, 0},
                        new float[] {.11f, .11f, .11f, 0, 0},
                        new float[] {0, 0, 0, 1, 0},
                        new float[] {0, 0, 0, 0, 1}
                    });

                //create some image attributes
                using (ImageAttributes attributes = new ImageAttributes())
                {

                    //set the color matrix attribute
                    attributes.SetColorMatrix(colorMatrix);

                    //draw the original image on the new image
                    //using the grayscale color matrix
                    g.DrawImage(original, new System.Drawing.Rectangle(0, 0, original.Width, original.Height),
                                0, 0, original.Width, original.Height, GraphicsUnit.Pixel, attributes);
                }
            }
            return newBitmap;
        }


        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            var hwnd = new WindowInteropHelper(this).Handle;
            //WinService.SetWindowExTransparent(hwnd);
            WindowInteropHelper helper = new WindowInteropHelper(this);

            HwndSource source = HwndSource.FromHwnd(hwnd);
            source.AddHook(WndProc);
        }

        const int WM_SYSCOMMAND = 0x0112;
        const int SC_MINIMIZE = 0xF020;
        const int SC_MAXIMIZE = 0xF030;
        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            long command = wParam.ToInt64() & 0xfff0;
            switch (msg)
            {
                case WM_SYSCOMMAND:
                    if (command == SC_MINIMIZE || command == SC_MAXIMIZE)
                        handled = true;
                    break;
                default:
                    break;
            }
            return IntPtr.Zero;
        }


        [DllImport("gdi32.dll")]
        public static extern bool DeleteObject(IntPtr hObject);

        #endregion
    }
}
