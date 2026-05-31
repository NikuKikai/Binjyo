using System;
using System.Drawing;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Diagnostics;


namespace Binjyo
{
    /// <summary>
    /// Interaction logic for Screenshot.xaml
    /// </summary>
    public partial class Screenshot : Window
    {
        private double dpiFactor = 1;

        private bool isshot = false;
        private bool isdrag = false;

        private int w, h, l, t;  // Screen bounds in Physical Pixel
        private int startx, starty;  // Mouse start position in Physical Pixel
        private int selectedLeft, selectedTop, selectedWidth, selectedHeight;  // Physical Pixel

        private Bitmap bitmap;
        private BitmapSource screenshotSource;
        private ImageBrush screenshotBrush;

        public Screenshot()
        {
            InitializeComponent();
        }

        // Use WriteableBitmap for better performance
        public void Shot()
        {
            WindowState = WindowState.Normal;

            // Scaled screen size
            // BUG: If the app start with dpi ratio X, and then changed to Y < X, following vars does not change.
            // var wv = (int)SystemParameters.VirtualScreenWidth;
            // var hv = (int)SystemParameters.VirtualScreenHeight;
            // var lv = (int)SystemParameters.VirtualScreenLeft;
            // var tv = (int)SystemParameters.VirtualScreenTop;

            // Get physical bounds
            var rect = Geo.GetAllScreenBoundsPhysical2();
            w = rect.Width;
            h = rect.Height;
            l = rect.X;
            t = rect.Y;

            // Get DPI
            dpiFactor = Geo.GetDpiFactorAt(l + 1, t + 1);

            // Resize window to cover all screens
            Width = w / dpiFactor;
            Height = h / dpiFactor;
            Left = l / dpiFactor;
            Top = t / dpiFactor;

            ReleaseScreenshotResources();

            var wb = CaptureScreen.Run();

            this.image.Source = wb;

            HideSelectionRect();

            ShowThis();
        }


        private void ShowThis()
        {
            if (!IsVisible)
                Show();
            Opacity = 1;
            Thread.Sleep(10);

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
                HideSelectionRect();
                Width = 10; Height = 10;

                ReleaseScreenshotResources();

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
            this.image.Source = null;

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

            lineY.X1 = x; lineY.X2 = x; lineY.Y1 = 0; lineY.Y2 = Height; lineY.Opacity = 0.7;
            lineX.Y1 = y; lineX.Y2 = y; lineX.X1 = 0; lineX.X2 = Width; lineX.Opacity = 0.7;
        }
        private void HideCross()
        {
            lineY.Opacity = 0; lineX.Opacity = 0;
        }


        private void UpdateSelectionRect()
        {
            double x = System.Windows.Forms.Control.MousePosition.X - l;
            double y = System.Windows.Forms.Control.MousePosition.Y - t;
            x /= dpiFactor; y /= dpiFactor;
            int xint = (int)(x + 0.499); int yint = (int)(y + 0.499);

            this.selectedWidth = xint > startx ? xint - startx + 1 : startx - xint + 1;
            this.selectedHeight = yint > starty ? yint - starty + 1 : starty - yint + 1;
            this.selectedLeft = xint > startx ? startx : xint;
            this.selectedTop = yint > starty ? starty : yint;

            double mx = (selectedLeft - 1) / dpiFactor;
            double my = (selectedTop - 1) / dpiFactor;
            double mw = (selectedWidth + 2) / dpiFactor;
            double mh = (selectedHeight + 2) / dpiFactor;

            maskTop.Height = Math.Max(0, my);

            maskBottom.Height = Math.Max(0, Height - (my + mh));

            maskLeft.Width = Math.Max(0, mx);
            // maskLeft.Height = Math.Max(0, selectionHeight);

            maskRight.Width = Math.Max(0, Width - (mx + mw));
            // maskRight.Height = Math.Max(0, selectionHeight);
        }

        private void HideSelectionRect()
        {
            maskTop.Height = Height;
            maskBottom.Height = 0;
            maskLeft.Width = 0;
            maskRight.Width = 0;
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
            if (selectedWidth < 20 || selectedHeight < 20) return;

            var source = (WriteableBitmap)this.image.Source;

            // Crop bitmap with rect
            var croppedImage = new WriteableBitmap(selectedWidth, selectedHeight, 96, 96, PixelFormats.Bgra32, null);
            int srcStride = source.BackBufferStride;
            int dstStride = croppedImage.BackBufferStride;

            source.Lock();
            croppedImage.Lock();
            try
            {
                IntPtr srcPtr = source.BackBuffer;
                IntPtr dstPtr = croppedImage.BackBuffer;

                // クロップの開始位置までポインタを進める (1ピクセル = 4バイト)
                IntPtr srcStartPtr = srcPtr + (selectedTop * srcStride) + (selectedLeft * 4);
                int bytesToCopyPerRow = selectedWidth * 4;

                // 2. 行（Row）ごとにメモリを一括コピーする
                for (int row = 0; row < selectedHeight; row++)
                {
                    IntPtr currentSrcRow = srcStartPtr + (row * srcStride);
                    IntPtr currentDstRow = dstPtr + (row * dstStride);

                    // Win32のCopyMemory（RtlMoveMemory）を使って1行分を丸ごと高速コピー
                    CaptureScreen.CopyMemory(currentDstRow, currentSrcRow, (uint)bytesToCopyPerRow);
                }

                // 変更を通知
                croppedImage.AddDirtyRect(new Int32Rect(0, 0, selectedWidth, selectedHeight));
            }
            finally
            {
                croppedImage.Unlock();
                source.Unlock();
            }

            // Get center DPI
            double left = selectedLeft + l;
            double top = selectedTop + t;
            double dpiFactor = Geo.GetDpiFactorAt(
                left + croppedImage.Width / 2,
                top + croppedImage.Height / 2
            );

            // Create Memo from cropped bitmap
            var item = Scene.CreateItem(croppedImage, left / dpiFactor, top / dpiFactor);
            MemoD11 memo = new MemoD11(item);
            CanvasWindow.CreateItem(item);
            Scene.Focus(item.Id);
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
