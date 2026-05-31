using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Binjyo
{
    public partial class HSVWheelWindow : Window
    {
        public HSVWheelWindow()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Update the HSV wheel visuals and labels from a single sampled bitmap pixel.
        /// </summary>
        public void UpdateContent(BitmapSource bitmap, int bitmapX, int bitmapY, double screenLeft, double screenTop)
        {
            if (bitmap == null
                || bitmapX < 0 || bitmapX >= bitmap.PixelWidth
                || bitmapY < 0 || bitmapY >= bitmap.PixelHeight)
            {
                Hide();
                return;
            }

            var pixel = GetPixel(bitmap, bitmapX, bitmapY);
            float hue = pixel.GetH();
            HSV_SV.Hue = hue;

            double radius = HSVWheel.Width / 2 - HSVWheel.StrokeThickness / 2;
            double angle = (hue + 210) / 180 * Math.PI;
            double hueX = HSVWheel.Width / 2 + Math.Cos(angle) * radius;
            double hueY = HSVWheel.Height / 2 + Math.Sin(angle) * radius;
            HueMark.Margin = new Thickness(hueX - HueMark.Width / 2, hueY - HueMark.Height / 2, 0, 0);

            double v = Math.Max(Math.Max(pixel.R, pixel.G), pixel.B) / 255.0;
            double s = Math.Min(Math.Min(pixel.R, pixel.G), pixel.B) / 255.0;
            s = v == 0 ? 1 : (v - s) / v;

            SVMark.Stroke = new SolidColorBrush(v < 0.5 ? Colors.White : Colors.Black);
            SVMark.Margin = new Thickness(
                HSVWheel.Width / 2 - HSVRect.Width / 2 + s * HSVRect.Width - SVMark.Width / 2,
                HSVWheel.Height / 2 + HSVRect.Height / 2 - v * HSVRect.Height - SVMark.Height / 2,
                0,
                0);

            HSVText.Text = string.Format("H{0: 000}°   S{1: 000}    L{2: 000}", (int)pixel.GetH(), (int)(pixel.GetS() * 100), (int)(pixel.GetV() * 100));
            RGBText.Text = string.Format("R{0: 000}    G{1: 000}    B{2: 000}", pixel.R, pixel.G, pixel.B);
            CoordText.Text = string.Format("X{0: 0000}    Y{1: 0000}", bitmapX, bitmapY);

            Left = screenLeft;
            Top = screenTop;

            if (!IsVisible)
                Show();
        }

        /// <summary>
        /// Sample a single pixel from a bitmap source in BGRA order.
        /// </summary>
        private static Color GetPixel(BitmapSource bitmap, int x, int y)
        {
            byte[] pixel = new byte[4];
            bitmap.CopyPixels(new Int32Rect(x, y, 1, 1), pixel, 4, 0);

            return Color.FromArgb(pixel[3], pixel[2], pixel[1], pixel[0]);
        }
    }
}
