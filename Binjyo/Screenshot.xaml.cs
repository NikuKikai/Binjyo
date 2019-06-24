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

namespace Binjyo
{
    /// <summary>
    /// Interaction logic for Screenshot.xaml
    /// </summary>
    public partial class Screenshot : Window
    {
        private bool isshot = false;
        private bool isdrag = false;
        private double startx, starty;
        
        private int w, h, l, t;

        private Line linew, lineh;
        private System.Windows.Shapes.Rectangle rect;

        private Bitmap bitmap;

        public Screenshot()
        {
            InitializeComponent();
            Show();
            Create_Objects();
        }

        /*
        public void Shot(System.Windows.Forms.Screen scr)
        {
            Top = scr.WorkingArea.Top; Left = scr.WorkingArea.Left;
            WindowState = WindowState.Maximized;
            Console.Write(Top);
            Console.WriteLine(scr.WorkingArea.Top);

            w = scr.Bounds.Width; h = scr.Bounds.Height;
            bitmap = new Bitmap(w, h);
            Graphics g = Graphics.FromImage(bitmap);
            g.CopyFromScreen(scr.Bounds.X, scr.Bounds.Y, 0, 0, bitmap.Size);
            g.Dispose();
            
            IntPtr hbitmap = bitmap.GetHbitmap();
            BitmapSource bs = Imaging.CreateBitmapSourceFromHBitmap(hbitmap, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            DeleteObject(hbitmap);
            canvas.Background = new ImageBrush(bs);
            Create_Objects();

            double x = System.Windows.Forms.Control.MousePosition.X;
            double y = System.Windows.Forms.Control.MousePosition.Y;
            linew.X1 = x + offset; linew.X2 = x + offset; linew.Opacity = 1.0;
            lineh.Y1 = y + offset; lineh.Y2 = y + offset; lineh.Opacity = 1.0;
            Show();
            Activate();
        }*/

        public void Shot()
        {
            w = (int)SystemParameters.VirtualScreenWidth;
            h = (int)SystemParameters.VirtualScreenHeight;
            l = (int)SystemParameters.VirtualScreenLeft;
            t = (int)SystemParameters.VirtualScreenTop;
            WindowState = WindowState.Normal;
            Width = w; Height = h; Left = l; Top = t;

            bitmap = new Bitmap(w, h, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
            Graphics g = Graphics.FromImage(bitmap);
            g.CopyFromScreen(l, t, 0, 0, bitmap.Size);
            g.Dispose();

            IntPtr hbitmap = bitmap.GetHbitmap();
            BitmapSource bs = Imaging.CreateBitmapSourceFromHBitmap(hbitmap, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            DeleteObject(hbitmap);
            canvas.Background = new ImageBrush(bs);

            _Show();
        }

        private void _Show()
        {
            //Show();
            Opacity = 1;
            Thread.Sleep(10);
            //canvas.Opacity = 1;

            double x = System.Windows.Forms.Control.MousePosition.X;
            double y = System.Windows.Forms.Control.MousePosition.Y;
            linew.X1 = x; linew.X2 = x; linew.Y2 = h; linew.Opacity = 1.0;
            lineh.Y1 = y; lineh.Y2 = y; lineh.X2 = w; lineh.Opacity = 1.0;
            Activate();
            isshot = true;
        }
        private void _Hide()
        {
            if (isshot)
            {
                //Hide();
                Opacity = 0;
                isshot = false;
                isdrag = false;
                rect.Opacity = 0;
                popup.IsOpen = false;
                linew.Opacity = 0; lineh.Opacity = 0;
                bitmap.Dispose();
                isshot = false;
            }
        }

        private void Create_Objects()
        {
            linew = new Line
            {
                Stroke = System.Windows.Media.Brushes.Black,
                StrokeThickness = 1,
                Y1 = 0
            };
            canvas.Children.Add(linew);
            lineh = new Line
            {
                Stroke = System.Windows.Media.Brushes.Black,
                StrokeThickness = 1,
                X1 = 0
            };
            canvas.Children.Add(lineh);
            rect = new System.Windows.Shapes.Rectangle
            {
                Stroke = System.Windows.Media.Brushes.Black,
                Opacity = 0
            };
            canvas.Children.Add(rect);
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            isdrag = true;
            startx = System.Windows.Forms.Control.MousePosition.X - l;
            starty = System.Windows.Forms.Control.MousePosition.Y - t;
        }

        private void Window_MouseMove(object sender, MouseEventArgs e)
        {
            double x = System.Windows.Forms.Control.MousePosition.X - l;
            double y = System.Windows.Forms.Control.MousePosition.Y - t;
            
            if (isdrag)
            {
                linew.Opacity = 0; lineh.Opacity = 0;
                rect.Width = x > startx ? x - startx : startx - x;
                rect.Height = y > starty ? y - starty : starty - y;
                Canvas.SetLeft(rect, x > startx ? startx : x);
                Canvas.SetTop(rect, y > starty ? starty : y);
                rect.Opacity = 1;

                popup.HorizontalOffset = x + 40;
                popup.VerticalOffset = y + 10;
                poptext.Text = String.Format("{0}x{1}", (int)rect.Width, (int)rect.Height);
                popup.IsOpen = true;
            }
            else
            {
                // draw cross
                rect.Opacity = 0; popup.IsOpen = false;
                linew.X1 = x; linew.X2 = x; linew.Opacity = 1;
                lineh.Y1 = y; lineh.Y2 = y; lineh.Opacity = 1;
            }
        }

        private void Window_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            rect.Opacity = 0; popup.IsOpen = false; linew.Opacity = 0; lineh.Opacity = 0;
            if (rect.Width > 20 && rect.Height > 20)
            {
                var croppedImage = new Bitmap((int)rect.Width, (int)rect.Height);
                using (var graphics = Graphics.FromImage(croppedImage))
                {

                    var srcrect = new System.Drawing.Rectangle(
                        (int)Canvas.GetLeft(rect),
                        (int)Canvas.GetTop(rect),
                        (int)rect.Width,
                        (int)rect.Height);
                    graphics.DrawImage(bitmap, 0, 0, srcrect, GraphicsUnit.Pixel);
                }

                Memo memo = new Memo();
                memo.Set_Bitmap(croppedImage, (int)Canvas.GetLeft(rect) + l, (int)Canvas.GetTop(rect) + t);
                memo = null;
            }
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
            Console.WriteLine(e.Key);
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

        [System.Runtime.InteropServices.DllImport("gdi32.dll")]
        public static extern bool DeleteObject(IntPtr hObject);
    }
}
