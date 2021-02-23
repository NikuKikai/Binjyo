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
        private int startx, starty;
        private double dpiFactor = 1;


        private int w, h, l, t;

        private Line linew, lineh;
        private System.Windows.Shapes.Rectangle rectBitmap, rectMask;

        private Bitmap bitmap;

        public Screenshot()
        {
            InitializeComponent();
            Show();
            Create_Objects();
        }

        public void Shot()
        {
            Width = (int)SystemParameters.VirtualScreenWidth;
            Height = (int)SystemParameters.VirtualScreenHeight;
            Left = (int)SystemParameters.VirtualScreenLeft;
            Top = (int)SystemParameters.VirtualScreenTop;
            dpiFactor = System.Windows.PresentationSource.FromVisual(this).CompositionTarget.TransformToDevice.M11;
            WindowState = WindowState.Normal;
            w = (int)(dpiFactor * Width);
            h = (int)(dpiFactor * Height);
            l = (int)(dpiFactor * Left);
            t = (int)(dpiFactor * Top);

            //Console.WriteLine(GC.GetTotalMemory(true));

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
            Canvas.SetLeft(this.rectBitmap, Left);
            Canvas.SetTop(this.rectBitmap, Top);

            _Show();
        }

        private void _Show()
        {
            //Show();
            Opacity = 1;
            Thread.Sleep(10);
            //canvas.Opacity = 1;

            double x = System.Windows.Forms.Control.MousePosition.X - l;
            double y = System.Windows.Forms.Control.MousePosition.Y - t;
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
                this.rectMask.Opacity = 0;
                popup.IsOpen = false;
                linew.Opacity = 0; lineh.Opacity = 0;
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

        private void Create_Objects()
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
            this.rectBitmap = new System.Windows.Shapes.Rectangle{
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
            canvas.Children.Add(linew);
            lineh = new Line
            {
                Stroke = System.Windows.Media.Brushes.White,
                StrokeThickness = 1,
                X1 = 0
            };
            canvas.Children.Add(lineh);

        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            isdrag = true;
            startx = System.Windows.Forms.Control.MousePosition.X - l;
            starty = System.Windows.Forms.Control.MousePosition.Y - t;
        }

        private void Window_MouseMove(object sender, MouseEventArgs e)
        {
            int x = System.Windows.Forms.Control.MousePosition.X - l;
            int y = System.Windows.Forms.Control.MousePosition.Y - t;
            
            if (isdrag)
            {
                linew.Opacity = 0; lineh.Opacity = 0;
                _SetRect(x > startx ? x - startx + 2 : startx - x + 2,
                        y > starty ? y - starty + 2 : starty - y + 2,
                        x > startx ? startx - 1 : x - 1,
                        y > starty ? starty - 1 : y - 1);
                this.rectMask.Opacity = 1;

                popup.HorizontalOffset = (x + 40)/dpiFactor;
                popup.VerticalOffset = (y + 11)/dpiFactor;
                poptext.Text = String.Format("{0}x{1}", (int)(this.rectMask.Width*dpiFactor), (int)(this.rectMask.Height*dpiFactor));
                popup.IsOpen = true;
            }
            else
            {
                // draw cross
                this.rectMask.Opacity = 0; popup.IsOpen = false;
                linew.X1 = x / dpiFactor; linew.X2 = x / dpiFactor; linew.Opacity = 0.7;
                lineh.Y1 = y / dpiFactor; lineh.Y2 = y / dpiFactor; lineh.Opacity = 0.7;
            }
        }
        private void _SetRect(int w, int h, int l, int t)
        {
            this.rectMask.Width = (int)(w / dpiFactor);
            this.rectMask.Height = (int)(h / dpiFactor);
            Canvas.SetLeft(this.rectMask, l / dpiFactor);
            Canvas.SetTop(this.rectMask, t / dpiFactor);
        }

        private void Window_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            this.rectMask.Opacity = 0; popup.IsOpen = false; linew.Opacity = 0; lineh.Opacity = 0;
            if (this.rectMask.Width > 20 && this.rectMask.Height > 20)
            {
                // Crop bitmap with rect
                var croppedImage = new Bitmap((int)(this.rectMask.Width*dpiFactor),
                                            (int)(this.rectMask.Height*dpiFactor));
                using (var graphics = Graphics.FromImage(croppedImage))
                {
                    var srcrect = new System.Drawing.Rectangle(
                        (int)((Canvas.GetLeft(this.rectMask) + 1) * dpiFactor),
                        (int)((Canvas.GetTop(this.rectMask) + 1) * dpiFactor),
                        croppedImage.Width, croppedImage.Height );
                    graphics.DrawImage(this.bitmap, 0, 0, srcrect, GraphicsUnit.Pixel);
                }

                // Create Memo from cropped bitmap
                new Memo(
                    dpiFactor,
                    croppedImage,
                    (int)Canvas.GetLeft(this.rectMask) + 1 + Left,
                    (int)Canvas.GetTop(this.rectMask) + 1 + Top
                );
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

        [System.Runtime.InteropServices.DllImport("gdi32.dll")]
        public static extern bool DeleteObject(IntPtr hObject);
    }
}
