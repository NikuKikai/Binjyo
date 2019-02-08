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
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Drawing;

namespace Binjyo
{
    /// <summary>
    /// MainWindow.xaml の相互作用ロジック
    /// </summary>
    public partial class MainWindow : Window
    {
        private Bitmap bitmap;
        ImageBrush ib;

        private bool isshot = false;
        private bool isdrag = false;
        private double startx, starty;

        private int w, h;

        private int offset = 0; // 7
        private Line linew, lineh;
        private System.Windows.Shapes.Rectangle rect;

        public MainWindow()
        {
            InitializeComponent();
            //this.SourceInitialized += new EventHandler(OnSourceInitialized);
            
            w = (int)SystemParameters.VirtualScreenWidth;
            h = (int)SystemParameters.VirtualScreenHeight;

            bitmap = new Bitmap(w, h);

            canvas.Background = new SolidColorBrush(Colors.Transparent);

            Create_Objects();
        }

        public void Shot()
        {
            if (isshot == false)
            {
                isshot = true;

                Graphics g = Graphics.FromImage(bitmap);
                g.CopyFromScreen(0, 0, 0, 0, bitmap.Size);
                g.Dispose();

                IntPtr hbitmap = bitmap.GetHbitmap();
                BitmapSource bs = Imaging.CreateBitmapSourceFromHBitmap(hbitmap, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                DeleteObject(hbitmap);
            
                canvas.Background = new ImageBrush(bs);

                if (IsVisible)
                {
                    if (WindowState != WindowState.Maximized)
                    {
                        WindowState = WindowState.Maximized;
                    }
                }
                else
                {
                    Show();
                }
                Activate();
            }
            
        }

        private void Window_Deactivated(object sender, EventArgs e)
        {
            Hide();
            isshot = false;
            isdrag = false;
        }

        private void Window_StateChanged(object sender, EventArgs e)
        {
            if (WindowState != WindowState.Maximized)
            {
                WindowState = WindowState.Maximized;
                Topmost = true;
            }
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            Console.WriteLine(e.Key);
            if (e.Key == Key.Escape || e.Key == Key.System || e.Key == Key.LeftAlt || 
                e.Key == Key.RightAlt || e.Key == Key.LWin || e.Key == Key.RWin)
            {
                Hide();
                isshot = false;
                isdrag = false;
            }
        }

        /*private void OnSourceInitialized(object sender, EventArgs e)
        {
            HwndSource source = (HwndSource)PresentationSource.FromVisual(this);
            source.AddHook(new HwndSourceHook(HandleMessages));
        }
        private IntPtr HandleMessages(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            // 0x0112 == WM_SYSCOMMAND, 'Window' command message.
            // 0xF020 == SC_MINIMIZE, command to minimize the window.
            if (msg == 0x0112 && ((int)wParam & 0xFFF0) == 0xF020)
            {
                // Cancel the minimize.
                handled = true;

                WindowState = WindowState.Maximized;
                Topmost = true;
            }

            return IntPtr.Zero;
        }*/


        [System.Runtime.InteropServices.DllImport("gdi32.dll")]
        public static extern bool DeleteObject(IntPtr hObject);

        private void Create_Objects()
        {
            ib = new ImageBrush();
            ib.Stretch = Stretch.None;

            linew = new Line();
            linew.Stroke = new SolidColorBrush(System.Windows.Media.Color.FromArgb(0x80, 0x00, 0x00, 0x00));
            linew.StrokeThickness = 1;
            linew.X1 = 0; linew.X2 = 0; linew.Y1 = offset; linew.Y2 = h + offset;
            linew.Opacity = 0.0;
            canvas.Children.Add(linew);
            lineh = new Line();
            lineh.Stroke = new SolidColorBrush(System.Windows.Media.Color.FromArgb(0x80, 0x00, 0x00, 0x00));
            lineh.StrokeThickness = 1;
            lineh.X1 = offset; lineh.X2 = w + offset; lineh.Y1 = 0; lineh.Y2 = 0;
            lineh.Opacity = 0.0;
            canvas.Children.Add(lineh);
            rect = new System.Windows.Shapes.Rectangle();
            rect.Stroke = new SolidColorBrush(Colors.White);
            rect.Opacity = 0.0;
            canvas.Children.Add(rect);
        }

        /*
        private void Draw(Bitmap bmp)
        {
            IntPtr hbitmap = bmp.GetHbitmap();
            image.Source = Imaging.CreateBitmapSourceFromHBitmap(hbitmap, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            DeleteObject(hbitmap);
        }

        private void Draw_cross(double x, double y)
        {
            Bitmap bmp = (Bitmap)bitmap.Clone();
            Graphics g = Graphics.FromImage(bmp);
            System.Drawing.Pen p = new System.Drawing.Pen(System.Drawing.Color.White, 1);
            g.DrawLine(p, (float)x, 0, (float)x, h);
            g.DrawLine(p, 0, (float)y, w, (float)y);
            g.Dispose();
            p.Dispose();
            Draw(bmp);
            bmp.Dispose();
        }*/

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            isdrag = true;
            startx = System.Windows.Forms.Control.MousePosition.X;
            starty = System.Windows.Forms.Control.MousePosition.Y;
        }

        private void Window_MouseMove(object sender, MouseEventArgs e)
        {
            double x = System.Windows.Forms.Control.MousePosition.X;
            double y = System.Windows.Forms.Control.MousePosition.Y;

            if (isdrag)
            {
                linew.Opacity = 0.0; lineh.Opacity = 0.0;
                rect.Width = x > startx ? x - startx : startx - x;
                rect.Height = y > starty ? y - starty : starty - y;
                Canvas.SetLeft(rect, x > startx ? startx + offset : x + offset);
                Canvas.SetTop(rect, y > starty ? starty + offset : y + offset);
                rect.Opacity = 0.8;
            }
            else
            {
                // draw cross
                rect.Opacity = 0.0;
                linew.X1 = x + offset; linew.X2 = x + offset; linew.Opacity = 1.0;
                lineh.Y1 = y + offset; lineh.Y2 = y + offset; lineh.Opacity = 1.0;
            }
        }

        private void Window_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            Hide();
            isshot = false;
            isdrag = false;

            rect.Opacity = 0.0;
            if (rect.Width > 10 && rect.Height > 10)
            {
                var croppedImage = new Bitmap((int)rect.Width, (int)rect.Height);
                using (var graphics = Graphics.FromImage(croppedImage))
                {
                    
                    var srcrect = new System.Drawing.Rectangle(
                        (int)Canvas.GetLeft(rect) - offset, 
                        (int)Canvas.GetTop(rect) - offset, 
                        (int)rect.Width, 
                        (int)rect.Height);
                    graphics.DrawImage(bitmap, 0, 0, srcrect, GraphicsUnit.Pixel);
                }

                Memo memo = new Memo();
                memo.Set_Bitmap(croppedImage, (int)Canvas.GetLeft(rect) - offset, (int)Canvas.GetTop(rect) - offset);
                memo = null;
            }
        }

        private void Window_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            Hide();
            isshot = false;
            isdrag = false;
        }


    }
}
