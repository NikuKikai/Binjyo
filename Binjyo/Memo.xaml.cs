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

namespace Binjyo
{
    /// <summary>
    /// Memo.xaml の相互作用ロジック
    /// </summary>
    public partial class Memo : Window
    {
        private bool isdrag = false;
        private bool isresize = false;
        private double lastx, lasty;
        private double startdx, startdy;
        private BitmapSource bitmpasource;
        private bool islock = false;

        public Memo()
        {
            InitializeComponent();
        }

        public void Set_Bitmap(BitmapSource bs, double x, double y)
        {
            Left = x; Top = y;
            Width = bs.Width; Height = bs.Height;

            bitmpasource = bs;
            image.Source = bs;

            Show();
            InitializeTimer();
        }
        public void Set_Bitmap(Bitmap bmp, double x, double y)
        {
            Left = x; Top = y;
            Width = bmp.Width; Height = bmp.Height;

            IntPtr hbitmap = bmp.GetHbitmap();
            bitmpasource = Imaging.CreateBitmapSourceFromHBitmap(hbitmap, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            DeleteObject(hbitmap);

            image.Source = bitmpasource;

            Show();
            InitializeTimer();
        }

        private void InitializeTimer()
        {
            /*System.Windows.Threading.DispatcherTimer dispatcherTimer = new System.Windows.Threading.DispatcherTimer();
            dispatcherTimer.Tick += dispatcherTimer_Tick;
            dispatcherTimer.Interval = new TimeSpan(0, 0, 0, 0, 50);
            dispatcherTimer.Start();*/
            
            var _tm = new Timer();
            _tm.Interval = 0.1;
            _tm.Elapsed += new ElapsedEventHandler(_tm_Elapsed);
            _tm.Enabled = true;
        }
        private delegate void TimerDelegate();
        private void _tm_Elapsed(object sender, ElapsedEventArgs e)
        {
            this.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Normal, new TimerDelegate(TimerHandler));
        }
        private void TimerHandler()
        {
            if (islock)
            {
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
                    button.Opacity = 0.1;
                }
            }
            else
            {
                if (isresize)
                {
                    if (System.Windows.Input.Mouse.LeftButton != MouseButtonState.Pressed)
                        isresize = false;
                    double w = System.Windows.Forms.Control.MousePosition.X + startdx - Left;
                    double h = System.Windows.Forms.Control.MousePosition.Y + startdy - Top;
                    if (w * bitmpasource.Height / bitmpasource.Width > h)
                        h = w * bitmpasource.Height / bitmpasource.Width;
                    else
                        w = h * bitmpasource.Width / bitmpasource.Height;
                    w = w < MinWidth ? MinWidth : w;
                    h = h < MinHeight ? MinHeight : h;
                    w = w > MaxWidth ? MaxWidth : w;
                    h = h > MaxHeight ? MaxHeight : h;
                    Width = w;
                    Height = h;
                }
                if (isdrag)
                {
                    if (System.Windows.Input.Mouse.LeftButton != MouseButtonState.Pressed)
                        isdrag = false;
                    double x = System.Windows.Forms.Control.MousePosition.X;
                    double y = System.Windows.Forms.Control.MousePosition.Y;
                    Left += x - lastx;
                    Top += y - lasty;
                    lastx = x;
                    lasty = y;
                }
            }
        }

        public void save()
        {
            Microsoft.Win32.SaveFileDialog dlg = new Microsoft.Win32.SaveFileDialog();
            var time = DateTime.Now;
            string formattedTime = time.ToString("yyyy-MM-dd-hh-mm-ss");
            dlg.FileName = formattedTime;
            dlg.Filter = "Png Image|*.png"; //|Bitmap Image|*.bmp|Gif Image|*.gif";
            if (dlg.ShowDialog() == true)
            {
                var encoder = new PngBitmapEncoder(); // Or PngBitmapEncoder, or whichever encoder you want
                encoder.Frames.Add(BitmapFrame.Create(bitmpasource));
                using (var stream = dlg.OpenFile())
                {
                    encoder.Save(stream);
                }
            }
        }

        [System.Runtime.InteropServices.DllImport("gdi32.dll")]
        public static extern bool DeleteObject(IntPtr hObject);

        #region Event
        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            switch(e.Key)
            {
                case Key.Escape:
                    Close();
                    break;
                case Key.S:
                    save();
                    break;
                case Key.C:
                    if(Keyboard.IsKeyDown(Key.LeftCtrl))
                        Clipboard.SetImage(bitmpasource);
                    break;
                case Key.X:
                    if (Keyboard.IsKeyDown(Key.LeftCtrl))
                    {
                        Clipboard.SetImage(bitmpasource);
                        Close();
                    }
                    break;
                default:
                    break;
            }
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            print("Window MouseDown");
            isdrag = true;
            lastx = System.Windows.Forms.Control.MousePosition.X;
            lasty = System.Windows.Forms.Control.MousePosition.Y;
        }

        private void Window_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
        }

        private void Window_MouseMove(object sender, MouseEventArgs e)
        {
            if (isdrag)
            {
                /*double x = System.Windows.Forms.Control.MousePosition.X;
                double y = System.Windows.Forms.Control.MousePosition.Y;
                Left += x - lastx;
                Top += y - lasty;
                lastx = x;
                lasty = y;*/
            }
        }

        private void Window_MouseUp(object sender, MouseButtonEventArgs e)
        {
            isdrag = false;
        }

        private void Window_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            Clipboard.SetImage(bitmpasource);
            Close();
        }

        private void Window_MouseEnter(object sender, MouseEventArgs e)
        {
            if (!islock)
            {
                button.Opacity = 0.7;
                resizer.Opacity = 0.5;
            }
        }

        private void Window_MouseLeave(object sender, MouseEventArgs e)
        {
            isdrag = false;
            if (!islock)
            {
                button.Opacity = 0.1;
                resizer.Opacity = 0.1;
            }
        }

        private void button_Click(object sender, RoutedEventArgs e)
        {
            if (islock)
            {
                islock = false;
                image.Opacity = 1;
                resizer.Opacity = 0.5;
                button.Opacity = 0.7;
                button.Content = FindResource("lockoff");
            }
            else
            {
                islock = true;
                //image.Opacity = 0;
                resizer.Opacity = 0;
                button.Opacity = 1;
                button.Content = FindResource("lockon");
            }
        }

        private void resizer_MouseDown(object sender, MouseButtonEventArgs e)
        {
            isresize = true;
            e.Handled = true;
            startdx = Left + Width - System.Windows.Forms.Control.MousePosition.X;
            startdy = Top + Height - System.Windows.Forms.Control.MousePosition.Y;
        }

        private void resizer_MouseUp(object sender, MouseButtonEventArgs e)
        {
            isresize = false;
        }

        private void Window_Deactivated(object sender, EventArgs e)
        {
            isdrag = false;
            isresize = false;
        }

        private void button_MouseDown(object sender, MouseButtonEventArgs e)
        {

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
            int command = wParam.ToInt32() & 0xfff0;
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
        #endregion

        void print(string s)
        {
            System.Diagnostics.Debug.WriteLine(s);
        }

    }
}
