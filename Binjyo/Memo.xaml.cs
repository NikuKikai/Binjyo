﻿using System;
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

namespace Binjyo
{
    /// <summary>
    /// Memo.xaml の相互作用ロジック
    /// </summary>
    public partial class Memo : Window
    {
        private bool isdrag = false;
        private double dpiFactor = 1;

        private double lastx, lasty;

        private Bitmap bitmap;
        private BitmapSource bitmpasource;
        private int lockmode = 0;

        private double scale = 1;

        private bool isOverButton = false;

        public Memo(double dpi=1)
        {
            InitializeComponent();
            dpiFactor = dpi;
        }

        public void Set_Bitmap(Bitmap bmp, double x, double y)
        {
            Left = x; Top = y;
            Width = bmp.Width/dpiFactor; Height = bmp.Height/dpiFactor;
            bitmap = bmp;

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
            switch(lockmode)
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
                        Left += (xx - lastx)/dpiFactor;
                        Top += (yy - lasty)/dpiFactor;
                        lastx = xx;
                        lasty = yy;
                    }
                    break;

                default:
                    break;
            }
        }

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
            if (!isdrag)// && !isresize)
            {
                scale = s;
                double right = Left + Width;
                //Opacity = 0.001;
                Width = bitmpasource.Width * s; Height = bitmpasource.Height * s;
                //Left = right - Width;
                //Opacity = 1;
            }
        }
        public void Sizeup()
        {
            scale += 0.2;
            if (scale <= 0 || scale >= 3 || 
                bitmpasource.Width * scale < 30 || bitmpasource.Height * scale < 30)
                scale -= 0.2;
            else
                Resize(scale);
        }
        public void Sizedown()
        {
            scale -= 0.2;
            if (scale <= 0 || scale >= 3 ||
                bitmpasource.Width * scale < 30 || bitmpasource.Height * scale < 30)
                scale += 0.2;
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

        [DllImport("gdi32.dll")]
        public static extern bool DeleteObject(IntPtr hObject);


        #region Event
        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            switch(e.Key)
            {
                case Key.Escape:
                    Close();
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
                        Close();
                    }
                    break;
                case Key.R:
                    ResetSize();
                    break;
                case Key.D:
                    Sizedown();
                    break;
                case Key.F:
                    Sizeup();
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
            var x = System.Windows.Forms.Control.MousePosition.X;
            var y = System.Windows.Forms.Control.MousePosition.Y;
            if (!isdrag && Keyboard.IsKeyDown(Key.LeftShift) && !isOverButton)
            {
                popup.IsOpen = true;
                if (x - (int)Left < 0 || x - (int)Left >= bitmap.Width || y - (int)Top < 0 || y - (int)Top >= bitmap.Height)
                    return;
                var px = bitmap.GetPixel(x - (int)Left, y - (int)Top);

                popup.HorizontalOffset = x - (int)Left + 180;
                popup.VerticalOffset = y - (int)Top + 30;

                float hue = px.GetHue();
                HSV_SV.Hue = hue;
                var radius = HSVWheel.Width / 2 - HSVWheel.StrokeThickness / 2;
                var angle = (hue + 210) / 180 * Math.PI;
                var xc = HSVWheel.Width / 2 + Math.Cos(angle) * radius;
                var yc = HSVWheel.Height / 2 + Math.Sin(angle) * radius;
                HueMark.Margin = new Thickness(xc-HueMark.Width/2, yc-HueMark.Height/2, 0, 0);

                var v = (double)Math.Max(Math.Max(px.R, px.G), px.B)/255;
                var s = (double)Math.Min(Math.Min(px.R, px.G), px.B) / 255;
                s = (v - s) / v; // S of HSV is different from px.GetSaturation(), which is S of HSL(?)
                SVMark.Margin = new Thickness(
                    HSVWheel.Width/2-HSVRect.Width/2 + s * HSVRect.Width - SVMark.Width/2, 
                    HSVWheel.Height/2+HSVRect.Height/2 - v * HSVRect.Height - SVMark.Height/2, 0, 0);

                //poptext.Text = String.Format("H{0: 000}° S{1: 000} L{2: 000}", (int)(px.GetHue()), (int)(px.GetSaturation()*100), (int)(px.GetBrightness()*100));
            }
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
            Close();
        }

        private void Window_MouseEnter(object sender, MouseEventArgs e)
        {
        }

        private void Window_MouseLeave(object sender, MouseEventArgs e)
        {
            //isdrag = false;
            popup.IsOpen = false;
        }

        private void button_Click(object sender, RoutedEventArgs e)
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

        private void Window_Deactivated(object sender, EventArgs e)
        {
            isdrag = false;
            popup.IsOpen = false;
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

        private void Window_PreviewMouseDoubleClick(object sender, MouseButtonEventArgs e)
        {

        }

        private void button_PreviewMouseDoubleClick(object sender, MouseButtonEventArgs e)
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

    }
}
