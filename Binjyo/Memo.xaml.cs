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

namespace Binjyo
{
    /// <summary>
    /// Memo.xaml の相互作用ロジック
    /// </summary>
    public partial class Memo : Window
    {
        private bool isdrag = false;
        private double lastx, lasty;
        private BitmapSource bitmpasource;

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
            //InitializeTimer();
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
            //InitializeTimer();
        }

        private void InitializeTimer()
        {
            System.Windows.Threading.DispatcherTimer dispatcherTimer = new System.Windows.Threading.DispatcherTimer();
            dispatcherTimer.Tick += dispatcherTimer_Tick;
            dispatcherTimer.Interval = new TimeSpan(0, 0, 0, 0, 5);
            dispatcherTimer.Start();
        }
        private void dispatcherTimer_Tick(object sender, EventArgs e)
        {
            double x = System.Windows.Forms.Control.MousePosition.X;
            double y = System.Windows.Forms.Control.MousePosition.Y;
            if (Left < x && x < Left + Width &&
                Top < y && y < Top + Height)
            {
                Opacity = 0.1;
            }
            else
            {
                Opacity = 1.0;
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

        private void Window_MouseMove(object sender, MouseEventArgs e)
        {
            if (isdrag)
            {
                double x = System.Windows.Forms.Control.MousePosition.X;
                double y = System.Windows.Forms.Control.MousePosition.Y;
                Left += x - lastx;
                Top += y - lasty;
                lastx = x;
                lasty = y;
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
            //Opacity = 0.1;
        }

        private void Window_MouseLeave(object sender, MouseEventArgs e)
        {
            //Opacity = 1.0;
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            //var hwnd = new WindowInteropHelper(this).Handle;
            //WinService.SetWindowExTransparent(hwnd);
        }
        #endregion

    }
}
