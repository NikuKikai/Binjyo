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
using System.Runtime.InteropServices;
using System.Drawing.Imaging;
using System.Windows.Threading;
// using OpenCvSharp;
// using OpenCvSharp.Extensions;

using Rect = System.Drawing.Rectangle;


namespace Binjyo
{
    public enum ResizeHandle
    {
        None,
        TopLeft,
        TopRight,
        BottomLeft,
        BottomRight
    }

    public static class BitmapExt
    {
        // https://stackoverflow.com/a/30729291
        public static BitmapSource ToBitmapSource(this Bitmap bitmap, System.Windows.Media.PixelFormat pixelFormat)
        {
            var bitmapData = bitmap.LockBits(
                new Rect(0, 0, bitmap.Width, bitmap.Height),
                System.Drawing.Imaging.ImageLockMode.ReadOnly, bitmap.PixelFormat);

            var bitmapSource = BitmapSource.Create(
                bitmapData.Width, bitmapData.Height,
                bitmap.HorizontalResolution, bitmap.VerticalResolution,
                pixelFormat, null,
                bitmapData.Scan0, bitmapData.Stride * bitmapData.Height, bitmapData.Stride);

            bitmap.UnlockBits(bitmapData);

            return bitmapSource;
        }
    }

    /// <summary>
    /// Memo.xaml の相互作用ロジック
    /// </summary>
    public partial class Memo : System.Windows.Window
    {
        private DispatcherTimer timer = null;

        private double dpiFactor = 1;

        private BitmapSource bitmpasource;
        private Bitmap bitmap;
        private Bitmap bitmapTransformed = null;

        // effect
        private double scale = 1;
        private bool isEffectGray = false;
        private bool isEffectBinarize = false;
        private int pEffectBinarize = 128;
        private bool isEffectQuantize = false;  // exclusive to isEffectBinarize
        private int pEffectQuantize = 3;
        private bool isEffectTransparent = false;
        private int pEffectTransparent = 128;
        private bool isEffectHuemap = false;

        private double dragStartMouseX, dragStartMouseY;
        private double dragStartLeft, dragStartTop;
        private bool isdrag = false;
        private bool isResizeMode = false;
        private bool isResizing = false;
        private bool isOverButton = false;
        private bool isSaving = false;
        private const double SnapDistance = 12;
        private const double ResizeHandleSize = 14;
        private const double ResizeHandleInset = 2;
        private int leftArrowRepeatCount = 0;
        private int rightArrowRepeatCount = 0;
        private int upArrowRepeatCount = 0;
        private int downArrowRepeatCount = 0;
        private ResizeHandle activeResizeHandle = ResizeHandle.None;
        private double resizeStartScale = 1;
        private double resizeStartLeft = 0;
        private double resizeStartTop = 0;
        private double resizeStartRight = 0;
        private double resizeStartBottom = 0;

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

            this.bitmap = bmp;
            this.bitmapTransformed = (Bitmap)this.bitmap.Clone();
            this._ShowBitmap(bmp);
            UpdateResizeModeVisuals();
        }

        protected void _Close()
        {
            SaveToHistory();
            if (this.bitmap != null) this.bitmap.Dispose();
            if (this.bitmapTransformed != null) this.bitmapTransformed.Dispose();
            this.image.Source = null;
            this.bitmpasource = null;
            if (this.timer != null) this.timer.Stop();
            if (Mouse.Captured == this) Mouse.Capture(null);
            this.Close();
            GC.Collect();
        }

        private void SaveToHistory()
        {
            if (bitmpasource == null)
                return;

            HistoryStore.Save(bitmpasource, Left, Top, Width, Height);
        }

        private void _ShowBitmap(Bitmap bmp, bool disposeBitmapAfterRender = false)
        {
            try
            {
                // NOTES: correct transparent rendering, and quicker
                this.bitmpasource = bmp.ToBitmapSource(PixelFormats.Bgra32);
                this.bitmpasource.Freeze();

                Resize(scale);

                this.image.Source = this.bitmpasource;
                Show();
            }
            finally
            {
                if (disposeBitmapAfterRender && bmp != null)
                    bmp.Dispose();
            }
        }

        private Bitmap _GetBitmapAfterEffect()
        {
            Bitmap bmp = this.bitmapTransformed;
            bmp = bmp.Clone(new Rect(0, 0, bmp.Width, bmp.Height), System.Drawing.Imaging.PixelFormat.Format32bppArgb);

            if (isEffectGray)
                EffectGray(bmp);

            if (isEffectBinarize && pEffectBinarize > 0)
                EffectBinarize(bmp, pEffectBinarize);

            else if (isEffectQuantize && pEffectQuantize > 2)
                EffectQuantize(bmp, pEffectQuantize);

            if (isEffectHuemap)
                EffectHuemap(bmp);

            if (isEffectTransparent && pEffectTransparent > 0)
                EffectTransparent(bmp, pEffectTransparent);

            return bmp;
        }

        protected void UpdateBitmap()
        {
            var res = _GetBitmapAfterEffect();
            _ShowBitmap(res, true);
        }


        #region ======== Timer ========
        private void InitializeTimer()
        {
            this.timer = new DispatcherTimer(DispatcherPriority.Render);
            this.timer.Interval = TimeSpan.FromMilliseconds(16);
            this.timer.Tick += TimerTick;
            this.timer.Start();
        }

        private void TimerTick(object sender, EventArgs e)
        {
            TimerHandler();
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

                default:
                    break;
            }
        }

        #endregion


        #region  ========== Operations ==========

        public void Minimize()
        {
            isdrag = false;
            StopResize();
            SetResizeMode(false);
            lockmode = 2;
            image.Opacity = 0;

            button.Opacity = 1;
            button.Content = FindResource("lockmin");
        }
        public void Expand()
        {
            isdrag = false;
            StopResize();
            lockmode = 0;
            image.Opacity = 1;

            button.Opacity = 0.7;
            button.Content = FindResource("lockoff");
            UpdateResizeModeVisuals();
        }
        public void Resize(double s)
        {
            if (!isdrag && !isResizing)
            {
                scale = ClampScale(s);
                Width = this.bitmapTransformed.Width / dpiFactor * scale;
                Height = this.bitmapTransformed.Height / dpiFactor * scale;
            }
        }
        public void ResizeDelta(double ds)
        {
            Resize(scale + ds);
        }
        public void ResetSize()
        {
            Resize(1);
        }

        public new void RestoreBounds(double left, double top, double width, double height)
        {
            double baseWidth = GetBaseWidth();
            if (baseWidth <= 0.0001)
                return;

            Left = left;
            Top = top;
            scale = ClampScale(width / baseWidth);
            Width = GetBaseWidth() * scale;
            Height = GetBaseHeight() * scale;
        }

        private double GetMinimumScale()
        {
            return Math.Max(25.0 / this.bitmapTransformed.Width, 25.0 / this.bitmapTransformed.Height);
        }

        private double GetMaximumScale()
        {
            return Math.Min(
                10,
                Math.Min(
                    SystemParameters.VirtualScreenWidth / this.bitmapTransformed.Width,
                    SystemParameters.VirtualScreenHeight / this.bitmapTransformed.Height));
        }

        private double ClampScale(double requestedScale)
        {
            return Math.Max(GetMinimumScale(), Math.Min(GetMaximumScale(), requestedScale));
        }

        private double GetBaseWidth()
        {
            return this.bitmapTransformed.Width / dpiFactor;
        }

        private double GetBaseHeight()
        {
            return this.bitmapTransformed.Height / dpiFactor;
        }

        private void SetResizeMode(bool enabled)
        {
            isResizeMode = enabled && lockmode == 0;
            if (!isResizeMode)
                StopResize();
            UpdateResizeModeVisuals();
        }

        private void UpdateResizeModeVisuals()
        {
            if (resizeOverlay == null || button == null)
                return;

            resizeOverlay.Visibility = isResizeMode && lockmode == 0 ? Visibility.Visible : Visibility.Collapsed;

            if (isResizeMode && lockmode == 0)
            {
                button.Opacity = 0;
                button.IsHitTestVisible = false;
            }
            else
            {
                button.IsHitTestVisible = true;
                if (lockmode == 0 && !isOverButton)
                    button.Opacity = 0.005;
            }
        }

        private bool IsResizeHandle(ResizeHandle handle)
        {
            return handle != ResizeHandle.None;
        }

        private ResizeHandle GetResizeHandleAtMousePosition()
        {
            double localX = System.Windows.Forms.Control.MousePosition.X / dpiFactor - Left;
            double localY = System.Windows.Forms.Control.MousePosition.Y / dpiFactor - Top;

            if (localX >= ResizeHandleInset && localX <= ResizeHandleInset + ResizeHandleSize &&
                localY >= ResizeHandleInset && localY <= ResizeHandleInset + ResizeHandleSize)
                return ResizeHandle.TopLeft;

            if (localX >= Width - ResizeHandleInset - ResizeHandleSize && localX <= Width - ResizeHandleInset &&
                localY >= ResizeHandleInset && localY <= ResizeHandleInset + ResizeHandleSize)
                return ResizeHandle.TopRight;

            if (localX >= ResizeHandleInset && localX <= ResizeHandleInset + ResizeHandleSize &&
                localY >= Height - ResizeHandleInset - ResizeHandleSize && localY <= Height - ResizeHandleInset)
                return ResizeHandle.BottomLeft;

            if (localX >= Width - ResizeHandleInset - ResizeHandleSize && localX <= Width - ResizeHandleInset &&
                localY >= Height - ResizeHandleInset - ResizeHandleSize && localY <= Height - ResizeHandleInset)
                return ResizeHandle.BottomRight;

            return ResizeHandle.None;
        }

        private Cursor GetCursorForResizeHandle(ResizeHandle handle)
        {
            switch (handle)
            {
                case ResizeHandle.TopLeft:
                case ResizeHandle.BottomRight:
                    return Cursors.SizeNWSE;
                case ResizeHandle.TopRight:
                case ResizeHandle.BottomLeft:
                    return Cursors.SizeNESW;
                default:
                    return Cursors.Arrow;
            }
        }

        private void BeginResize(ResizeHandle handle)
        {
            activeResizeHandle = handle;
            isResizing = true;
            isdrag = false;
            resizeStartScale = scale;
            resizeStartLeft = Left;
            resizeStartTop = Top;
            resizeStartRight = Left + Width;
            resizeStartBottom = Top + Height;
            dragStartMouseX = System.Windows.Forms.Control.MousePosition.X;
            dragStartMouseY = System.Windows.Forms.Control.MousePosition.Y;
            Mouse.Capture(this);
            UpdateResizeInfoOverlay(scale, Width, Height);
        }

        private void StopResize()
        {
            isResizing = false;
            activeResizeHandle = ResizeHandle.None;
            if (Mouse.Captured == this)
                Mouse.Capture(null);
            if (resizeInfoOverlay != null)
                resizeInfoOverlay.Visibility = Visibility.Collapsed;
        }

        private void UpdateResizeFromMouse()
        {
            double mouseX = System.Windows.Forms.Control.MousePosition.X;
            double mouseY = System.Windows.Forms.Control.MousePosition.Y;
            double deltaX = (mouseX - dragStartMouseX) / dpiFactor;
            double deltaY = (mouseY - dragStartMouseY) / dpiFactor;
            double rawScale = GetResizeScaleFromMouseDelta(deltaX, deltaY);
            rawScale = ClampScale(rawScale);
            double snappedScale = ApplyResizeSnap(rawScale);
            ApplyScaleToBounds(snappedScale, out double nextLeft, out double nextTop, out double nextWidth, out double nextHeight);
            scale = snappedScale;
            Left = nextLeft;
            Top = nextTop;
            Width = nextWidth;
            Height = nextHeight;
            UpdateResizeInfoOverlay(snappedScale, nextWidth, nextHeight);
        }

        private void UpdateResizeInfoOverlay(double currentScale, double currentWidth, double currentHeight)
        {
            if (resizeInfoOverlay == null)
                return;

            string scaleText = $"{Math.Round(currentScale * 100):0}%";
            string sizeText = $"{Math.Round(currentWidth):0} x {Math.Round(currentHeight):0}";

            resizeScaleText.Text = scaleText;
            resizeScaleTextStrokeLeft.Text = scaleText;
            resizeScaleTextStrokeRight.Text = scaleText;
            resizeScaleTextStrokeTop.Text = scaleText;
            resizeScaleTextStrokeBottom.Text = scaleText;
            resizeSizeText.Text = sizeText;
            resizeSizeTextStrokeLeft.Text = sizeText;
            resizeSizeTextStrokeRight.Text = sizeText;
            resizeSizeTextStrokeTop.Text = sizeText;
            resizeSizeTextStrokeBottom.Text = sizeText;
            resizeInfoOverlay.Visibility = Visibility.Visible;
        }

        private double GetResizeScaleFromMouseDelta(double deltaX, double deltaY)
        {
            double handleVectorX = 0;
            double handleVectorY = 0;

            switch (activeResizeHandle)
            {
                case ResizeHandle.TopLeft:
                    handleVectorX = -GetBaseWidth();
                    handleVectorY = -GetBaseHeight();
                    break;
                case ResizeHandle.TopRight:
                    handleVectorX = GetBaseWidth();
                    handleVectorY = -GetBaseHeight();
                    break;
                case ResizeHandle.BottomLeft:
                    handleVectorX = -GetBaseWidth();
                    handleVectorY = GetBaseHeight();
                    break;
                case ResizeHandle.BottomRight:
                    handleVectorX = GetBaseWidth();
                    handleVectorY = GetBaseHeight();
                    break;
            }

            double denominator = handleVectorX * handleVectorX + handleVectorY * handleVectorY;
            if (denominator <= 0.0001)
                return resizeStartScale;

            double projectedScaleDelta = (deltaX * handleVectorX + deltaY * handleVectorY) / denominator;
            return resizeStartScale + projectedScaleDelta;
        }

        private void ApplyScaleToBounds(double targetScale, out double nextLeft, out double nextTop, out double nextWidth, out double nextHeight)
        {
            nextWidth = GetBaseWidth() * targetScale;
            nextHeight = GetBaseHeight() * targetScale;

            switch (activeResizeHandle)
            {
                case ResizeHandle.TopLeft:
                    nextLeft = resizeStartRight - nextWidth;
                    nextTop = resizeStartBottom - nextHeight;
                    break;
                case ResizeHandle.TopRight:
                    nextLeft = resizeStartLeft;
                    nextTop = resizeStartBottom - nextHeight;
                    break;
                case ResizeHandle.BottomLeft:
                    nextLeft = resizeStartRight - nextWidth;
                    nextTop = resizeStartTop;
                    break;
                case ResizeHandle.BottomRight:
                default:
                    nextLeft = resizeStartLeft;
                    nextTop = resizeStartTop;
                    break;
            }
        }

        private double ApplyResizeSnap(double rawScale)
        {
            if (!IsSnapEnabled() || !IsResizeHandle(activeResizeHandle))
                return rawScale;

            ApplyScaleToBounds(rawScale, out double rawLeft, out double rawTop, out double rawWidth, out double rawHeight);
            double rawRight = rawLeft + rawWidth;
            double rawBottom = rawTop + rawHeight;
            double bestScale = rawScale;
            double bestDistance = SnapDistance + 1;

            foreach (var screen in System.Windows.Forms.Screen.AllScreens)
            {
                TryResizeSnapCandidate(rawScale, screen.Bounds.Left / dpiFactor, true, ref bestScale, ref bestDistance);
                TryResizeSnapCandidate(rawScale, screen.Bounds.Right / dpiFactor, true, ref bestScale, ref bestDistance);
                TryResizeSnapCandidate(rawScale, screen.Bounds.Top / dpiFactor, false, ref bestScale, ref bestDistance);
                TryResizeSnapCandidate(rawScale, screen.Bounds.Bottom / dpiFactor, false, ref bestScale, ref bestDistance);
            }

            foreach (Window item in Application.Current.Windows)
            {
                if (item == this || item.Title != "Memo" || !item.IsVisible)
                    continue;

                if (MovesLeftOrRightEdge() &&
                    IntervalsOverlapOrTouch(rawTop, rawBottom, item.Top, item.Top + item.Height))
                {
                    TryResizeSnapCandidate(rawScale, item.Left, true, ref bestScale, ref bestDistance);
                    TryResizeSnapCandidate(rawScale, item.Left + item.Width, true, ref bestScale, ref bestDistance);
                }

                if (MovesTopOrBottomEdge() &&
                    IntervalsOverlapOrTouch(rawLeft, rawRight, item.Left, item.Left + item.Width))
                {
                    TryResizeSnapCandidate(rawScale, item.Top, false, ref bestScale, ref bestDistance);
                    TryResizeSnapCandidate(rawScale, item.Top + item.Height, false, ref bestScale, ref bestDistance);
                }
            }

            return bestScale;
        }

        private void TryResizeSnapCandidate(double rawScale, double targetEdge, bool horizontalEdge, ref double bestScale, ref double bestDistance)
        {
            if (!TryGetResizeScaleForTarget(targetEdge, horizontalEdge, out double candidateScale))
                return;

            candidateScale = ClampScale(candidateScale);
            ApplyScaleToBounds(candidateScale, out double candidateLeft, out double candidateTop, out double candidateWidth, out double candidateHeight);

            double rawMovingEdge = GetMovingEdge(rawScale, horizontalEdge);
            double candidateMovingEdge = horizontalEdge
                ? (MovesLeftEdge() ? candidateLeft : candidateLeft + candidateWidth)
                : (MovesTopEdge() ? candidateTop : candidateTop + candidateHeight);

            double distance = Math.Abs(rawMovingEdge - targetEdge);
            if (distance <= SnapDistance && distance < bestDistance && Math.Abs(candidateMovingEdge - targetEdge) < 0.001)
            {
                bestScale = candidateScale;
                bestDistance = distance;
            }
        }

        private double GetMovingEdge(double candidateScale, bool horizontalEdge)
        {
            ApplyScaleToBounds(candidateScale, out double candidateLeft, out double candidateTop, out double candidateWidth, out double candidateHeight);
            if (horizontalEdge)
                return MovesLeftEdge() ? candidateLeft : candidateLeft + candidateWidth;
            return MovesTopEdge() ? candidateTop : candidateTop + candidateHeight;
        }

        private bool TryGetResizeScaleForTarget(double targetEdge, bool horizontalEdge, out double candidateScale)
        {
            candidateScale = scale;
            if (horizontalEdge)
            {
                if (MovesLeftEdge())
                {
                    candidateScale = (resizeStartRight - targetEdge) / GetBaseWidth();
                    return true;
                }
                if (MovesRightEdge())
                {
                    candidateScale = (targetEdge - resizeStartLeft) / GetBaseWidth();
                    return true;
                }
                return false;
            }

            if (MovesTopEdge())
            {
                candidateScale = (resizeStartBottom - targetEdge) / GetBaseHeight();
                return true;
            }
            if (MovesBottomEdge())
            {
                candidateScale = (targetEdge - resizeStartTop) / GetBaseHeight();
                return true;
            }
            return false;
        }

        private bool MovesLeftEdge()
        {
            return activeResizeHandle == ResizeHandle.TopLeft || activeResizeHandle == ResizeHandle.BottomLeft;
        }

        private bool MovesRightEdge()
        {
            return activeResizeHandle == ResizeHandle.TopRight || activeResizeHandle == ResizeHandle.BottomRight;
        }

        private bool MovesTopEdge()
        {
            return activeResizeHandle == ResizeHandle.TopLeft || activeResizeHandle == ResizeHandle.TopRight;
        }

        private bool MovesBottomEdge()
        {
            return activeResizeHandle == ResizeHandle.BottomLeft || activeResizeHandle == ResizeHandle.BottomRight;
        }

        private bool MovesLeftOrRightEdge()
        {
            return MovesLeftEdge() || MovesRightEdge();
        }

        private bool MovesTopOrBottomEdge()
        {
            return MovesTopEdge() || MovesBottomEdge();
        }

        public void Save()
        {
            if (isSaving)
                return;

            isSaving = true;
            try
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
            finally
            {
                isSaving = false;
            }
        }

        #endregion


        private void _UpdateHSVWheel()
        {
            double x = System.Windows.Forms.Control.MousePosition.X - Left;
            double y = System.Windows.Forms.Control.MousePosition.Y - Top;

            if (x < 0 || x >= Width || y < 0 || y >= Height)
                return;
            var px = bitmapTransformed.GetPixel((int)(x / scale), (int)(y / scale));

            popup.IsOpen = true;
            popup.HorizontalOffset = x / dpiFactor + 20;
            popup.VerticalOffset = y / dpiFactor + 10;

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
            HSVText.Text = String.Format("H{0: 000}°   S{1: 000}    L{2: 000}", (int)px.GetHue(), (int)(px.GetSaturation() * 100), (int)(px.GetBrightness() * 100));
            RGBText.Text = String.Format("R{0: 000}    G{1: 000}    B{2: 000}", px.R, px.G, px.B);
        }

        private void _HideHSVWheel()
        {
            popup.IsOpen = false;
        }

        private void ApplySnap(ref double nextLeft, ref double nextTop)
        {
            if (!IsSnapEnabled())
                return;

            double snappedLeft = nextLeft;
            double snappedTop = nextTop;
            double bestDistanceX = SnapDistance + 1;
            double bestDistanceY = SnapDistance + 1;
            double width = Width;
            double height = Height;

            foreach (var screen in System.Windows.Forms.Screen.AllScreens)
            {
                double screenLeft = screen.Bounds.Left / dpiFactor;
                double screenTop = screen.Bounds.Top / dpiFactor;
                double screenRight = screen.Bounds.Right / dpiFactor;
                double screenBottom = screen.Bounds.Bottom / dpiFactor;

                TrySnapValue(nextLeft, screenLeft, ref snappedLeft, ref bestDistanceX);
                TrySnapValue(nextLeft, screenRight - width, ref snappedLeft, ref bestDistanceX);
                TrySnapValue(nextTop, screenTop, ref snappedTop, ref bestDistanceY);
                TrySnapValue(nextTop, screenBottom - height, ref snappedTop, ref bestDistanceY);
            }

            foreach (Window item in Application.Current.Windows)
            {
                if (item == this || item.Title != "Memo" || !item.IsVisible)
                    continue;

                double otherLeft = item.Left;
                double otherTop = item.Top;
                double otherRight = item.Left + item.Width;
                double otherBottom = item.Top + item.Height;
                double nextRight = nextLeft + width;
                double nextBottom = nextTop + height;

                bool canSnapX = IntervalsOverlapOrTouch(nextTop, nextBottom, otherTop, otherBottom);
                bool canSnapY = IntervalsOverlapOrTouch(nextLeft, nextRight, otherLeft, otherRight);

                if (canSnapX)
                {
                    TrySnapValue(nextLeft, otherLeft, ref snappedLeft, ref bestDistanceX);
                    TrySnapValue(nextLeft, otherRight, ref snappedLeft, ref bestDistanceX);
                    TrySnapValue(nextLeft, otherLeft - width, ref snappedLeft, ref bestDistanceX);
                    TrySnapValue(nextLeft, otherRight - width, ref snappedLeft, ref bestDistanceX);
                }

                if (canSnapY)
                {
                    TrySnapValue(nextTop, otherTop, ref snappedTop, ref bestDistanceY);
                    TrySnapValue(nextTop, otherBottom, ref snappedTop, ref bestDistanceY);
                    TrySnapValue(nextTop, otherTop - height, ref snappedTop, ref bestDistanceY);
                    TrySnapValue(nextTop, otherBottom - height, ref snappedTop, ref bestDistanceY);
                }
            }

            nextLeft = snappedLeft;
            nextTop = snappedTop;
        }

        private bool IsSnapEnabled()
        {
            bool isDefaultEnabled = Properties.Settings.Default.SnapMemo;
            bool isAltDown = Keyboard.IsKeyDown(Key.LeftAlt) || Keyboard.IsKeyDown(Key.RightAlt);
            return isAltDown ? !isDefaultEnabled : isDefaultEnabled;
        }

        private static void TrySnapValue(double nextValue, double targetValue, ref double snappedValue, ref double bestDistance)
        {
            double distance = Math.Abs(nextValue - targetValue);
            if (distance <= SnapDistance && distance < bestDistance)
            {
                snappedValue = targetValue;
                bestDistance = distance;
            }
        }

        private static bool IntervalsOverlapOrTouch(double startA, double endA, double startB, double endB)
        {
            return endA >= startB && endB >= startA;
        }


        protected override void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi)
        {
            dpiFactor = newDpi.DpiScaleX;
            Console.WriteLine("DPI Changed");
            Console.WriteLine(newDpi.DpiScaleX);
            Console.WriteLine(Left);
        }


        #region ========== Key events ==========
        private bool isEditedDuringKeyB = false;
        private bool isEditedDuringKeyQ = false;
        private bool isEditedDuringKeyO = false;
        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            switch(e.Key)
            {
                case Key.Escape:
                    this._Close();
                    e.Handled = true;
                    break;
                case Key.S:
                    if (!e.IsRepeat)
                    {
                        Save();
                    }
                    e.Handled = true;
                    break;
                case Key.C:
                    if(Keyboard.IsKeyDown(Key.LeftCtrl))
                        Clipboard.SetImage(bitmpasource);
                    else
                    {
                        isEffectHuemap = !isEffectHuemap;
                        UpdateBitmap();
                    }
                    break;
                case Key.X:
                    if (Keyboard.IsKeyDown(Key.LeftCtrl))
                    {
                        Clipboard.SetImage(bitmpasource);
                        this._Close();
                    }
                    break;
                case Key.OemTilde:
                    ResetSize();
                    break;
                case Key.T:
                    if (!e.IsRepeat)
                        SetResizeMode(!isResizeMode);
                    e.Handled = true;
                    break;
                case Key.D:
                    ResizeDelta(-0.2);
                    break;
                case Key.F:
                    ResizeDelta(0.2);
                    break;
                case Key.G:
                    this.isEffectGray = !this.isEffectGray;
                    UpdateBitmap();
                    break;
                case Key.LeftShift:
                    this._UpdateHSVWheel();
                    break;
                case Key.H:
                    this.bitmapTransformed.RotateFlip(RotateFlipType.RotateNoneFlipX);
                    UpdateBitmap();
                    break;
                case Key.V:
                    this.bitmapTransformed.RotateFlip(RotateFlipType.RotateNoneFlipY);
                    UpdateBitmap();
                    break;
                case Key.R:
                    this.bitmapTransformed.RotateFlip(RotateFlipType.Rotate90FlipNone);
                    UpdateBitmap();
                    break;
                case Key.Left:
                case Key.Right:
                case Key.Up:
                case Key.Down:
                    MoveByKeyboard(e.Key, e.IsRepeat);
                    e.Handled = true;
                    break;
                case Key.B:
                    if (!e.IsRepeat)
                        isEditedDuringKeyB = false;
                    break;
                case Key.Q:
                    if (!e.IsRepeat)
                        isEditedDuringKeyQ = false;
                    break;
                case Key.O:
                    if (!e.IsRepeat)
                        isEditedDuringKeyO = false;
                    break;
                default:
                    break;
            }
        }
        private void Window_KeyUp(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.B:
                    if (!isEditedDuringKeyB)
                    {
                        isEffectBinarize = !isEffectBinarize;
                        if (isEffectBinarize) isEffectQuantize = false;
                    }
                    UpdateBitmap();
                    break;
                case Key.Q:
                    if (!isEditedDuringKeyQ)
                    {
                        isEffectQuantize = !isEffectQuantize;
                        if (isEffectQuantize) isEffectBinarize = false;
                    }
                    UpdateBitmap();
                    break;
                case Key.O:
                    if (!isEditedDuringKeyO)
                    {
                        isEffectTransparent = !isEffectTransparent;
                    }
                    UpdateBitmap();
                    break;
                case Key.Left:
                    leftArrowRepeatCount = 0;
                    break;
                case Key.Right:
                    rightArrowRepeatCount = 0;
                    break;
                case Key.Up:
                    upArrowRepeatCount = 0;
                    break;
                case Key.Down:
                    downArrowRepeatCount = 0;
                    break;
                default:
                    break;
            }
        }

        private void MoveByKeyboard(Key key, bool isRepeat)
        {
            if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))
            {
                MoveToNextSnap(key);
                return;
            }

            int repeatCount = UpdateArrowRepeatCount(key, isRepeat);
            double multiplier = (Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift)) ? 10 : 1;
            double acceleratedStep = multiplier * (1 + repeatCount / 4);

            switch (key)
            {
                case Key.Left:
                    Left -= acceleratedStep;
                    break;
                case Key.Right:
                    Left += acceleratedStep;
                    break;
                case Key.Up:
                    Top -= acceleratedStep;
                    break;
                case Key.Down:
                    Top += acceleratedStep;
                    break;
                default:
                    break;
            }
        }

        private int UpdateArrowRepeatCount(Key key, bool isRepeat)
        {
            int repeatCount = isRepeat ? 1 : 0;
            switch (key)
            {
                case Key.Left:
                    leftArrowRepeatCount = isRepeat ? leftArrowRepeatCount + 1 : 0;
                    repeatCount = leftArrowRepeatCount;
                    break;
                case Key.Right:
                    rightArrowRepeatCount = isRepeat ? rightArrowRepeatCount + 1 : 0;
                    repeatCount = rightArrowRepeatCount;
                    break;
                case Key.Up:
                    upArrowRepeatCount = isRepeat ? upArrowRepeatCount + 1 : 0;
                    repeatCount = upArrowRepeatCount;
                    break;
                case Key.Down:
                    downArrowRepeatCount = isRepeat ? downArrowRepeatCount + 1 : 0;
                    repeatCount = downArrowRepeatCount;
                    break;
            }
            return repeatCount;
        }

        private void MoveToNextSnap(Key key)
        {
            double? target = null;
            switch (key)
            {
                case Key.Left:
                    target = GetNextSnapPositionX(false);
                    if (target.HasValue)
                        Left = target.Value;
                    break;
                case Key.Right:
                    target = GetNextSnapPositionX(true);
                    if (target.HasValue)
                        Left = target.Value;
                    break;
                case Key.Up:
                    target = GetNextSnapPositionY(false);
                    if (target.HasValue)
                        Top = target.Value;
                    break;
                case Key.Down:
                    target = GetNextSnapPositionY(true);
                    if (target.HasValue)
                        Top = target.Value;
                    break;
            }
        }

        private double? GetNextSnapPositionX(bool forward)
        {
            List<double> candidates = new List<double>();
            double width = Width;
            double top = Top;
            double bottom = Top + Height;

            foreach (var screen in System.Windows.Forms.Screen.AllScreens)
            {
                double screenLeft = screen.Bounds.Left / dpiFactor;
                double screenRight = screen.Bounds.Right / dpiFactor;
                candidates.Add(screenLeft);
                candidates.Add(screenRight - width);
            }

            foreach (Window item in Application.Current.Windows)
            {
                if (item == this || item.Title != "Memo" || !item.IsVisible)
                    continue;

                if (!IntervalsOverlapOrTouch(top, bottom, item.Top, item.Top + item.Height))
                    continue;

                candidates.Add(item.Left);
                candidates.Add(item.Left + item.Width);
                candidates.Add(item.Left - width);
                candidates.Add(item.Left + item.Width - width);
            }

            return FindNextCandidate(Left, candidates, forward);
        }

        private double? GetNextSnapPositionY(bool forward)
        {
            List<double> candidates = new List<double>();
            double height = Height;
            double left = Left;
            double right = Left + Width;

            foreach (var screen in System.Windows.Forms.Screen.AllScreens)
            {
                double screenTop = screen.Bounds.Top / dpiFactor;
                double screenBottom = screen.Bounds.Bottom / dpiFactor;
                candidates.Add(screenTop);
                candidates.Add(screenBottom - height);
            }

            foreach (Window item in Application.Current.Windows)
            {
                if (item == this || item.Title != "Memo" || !item.IsVisible)
                    continue;

                if (!IntervalsOverlapOrTouch(left, right, item.Left, item.Left + item.Width))
                    continue;

                candidates.Add(item.Top);
                candidates.Add(item.Top + item.Height);
                candidates.Add(item.Top - height);
                candidates.Add(item.Top + item.Height - height);
            }

            return FindNextCandidate(Top, candidates, forward);
        }

        private static double? FindNextCandidate(double currentValue, IEnumerable<double> candidates, bool forward)
        {
            double? bestCandidate = null;
            foreach (double candidate in candidates)
            {
                if (forward)
                {
                    if (candidate <= currentValue + SnapDistance)
                        continue;
                    if (!bestCandidate.HasValue || candidate < bestCandidate.Value)
                        bestCandidate = candidate;
                }
                else
                {
                    if (candidate >= currentValue - SnapDistance)
                        continue;
                    if (!bestCandidate.HasValue || candidate > bestCandidate.Value)
                        bestCandidate = candidate;
                }
            }
            return bestCandidate;
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (isResizeMode && lockmode == 0)
            {
                ResizeHandle handle = GetResizeHandleAtMousePosition();
                if (IsResizeHandle(handle))
                {
                    BeginResize(handle);
                    return;
                }
            }

            isdrag = true;
            dragStartMouseX = System.Windows.Forms.Control.MousePosition.X;
            dragStartMouseY = System.Windows.Forms.Control.MousePosition.Y;
            dragStartLeft = Left;
            dragStartTop = Top;
            Mouse.Capture(this);
        }

        private void Window_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
        }

        private void Window_MouseMove(object sender, MouseEventArgs e)
        {
            if (isResizing)
            {
                if (Mouse.LeftButton == MouseButtonState.Pressed)
                    UpdateResizeFromMouse();
                else
                    StopResize();
            }
            else if (isdrag)
            {
                if (Mouse.LeftButton == MouseButtonState.Pressed)
                    UpdateDragFromMouse();
                else
                    isdrag = false;
            }

            if (isResizeMode && lockmode == 0)
            {
                if (isResizing)
                {
                    Cursor = GetCursorForResizeHandle(activeResizeHandle);
                }
                else
                {
                    Cursor = GetCursorForResizeHandle(GetResizeHandleAtMousePosition());
                }
            }
            else
            {
                Cursor = Cursors.Arrow;
            }

            // Show HSV
            if (!isdrag && !isResizing && Keyboard.IsKeyDown(Key.LeftShift) && !isOverButton)
                this._UpdateHSVWheel();
            else
                this._HideHSVWheel();
        }

        private void UpdateDragFromMouse()
        {
            double xx = System.Windows.Forms.Control.MousePosition.X;
            double yy = System.Windows.Forms.Control.MousePosition.Y;
            double rawLeft = dragStartLeft + (xx - dragStartMouseX) / dpiFactor;
            double rawTop = dragStartTop + (yy - dragStartMouseY) / dpiFactor;
            double nextLeft = rawLeft;
            double nextTop = rawTop;
            ApplySnap(ref nextLeft, ref nextTop);
            Left = nextLeft;
            Top = nextTop;
        }

        private void Window_MouseUp(object sender, MouseButtonEventArgs e)
        {
            isdrag = false;
            StopResize();
            if (Mouse.Captured == this)
                Mouse.Capture(null);
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
            else if (Keyboard.IsKeyDown(Key.B))
            {
                isEditedDuringKeyB = true;
                isEffectBinarize = true; isEffectQuantize = false;
                pEffectBinarize = Math.Max(Math.Min(pEffectBinarize + 15 * Math.Sign(e.Delta), 250), 5);
                UpdateBitmap();
            }
            else if (Keyboard.IsKeyDown(Key.Q))
            {
                isEditedDuringKeyQ = true;
                isEffectQuantize = true; isEffectBinarize = false;
                pEffectQuantize = Math.Max(Math.Min(pEffectQuantize + 1 * Math.Sign(e.Delta), 16), 3);
                UpdateBitmap();
            }
            else if (Keyboard.IsKeyDown(Key.O))
            {
                isEditedDuringKeyO = true;
                isEffectTransparent = true;
                pEffectTransparent = Math.Max(Math.Min(pEffectTransparent + 15 * Math.Sign(e.Delta), 245), 10);
                UpdateBitmap();
            }
        }

        private void Window_Deactivated(object sender, EventArgs e)
        {
            isdrag = false;
            StopResize();
            if (Mouse.Captured == this)
                Mouse.Capture(null);
            popup.IsOpen = false;
        }

        private void Window_PreviewMouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
        }
        #endregion


        #region ========== BUTTON ==========

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            switch (lockmode)
            {
                case 2:
                    Expand();
                    break;
                case 0:
                    SetResizeMode(false);
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
            if (lockmode == 0 && !isResizeMode)
            {
                button.Opacity = 1;
            }
        }

        private void Button_MouseLeave(object sender, MouseEventArgs e)
        {
            isOverButton = false;
            if (lockmode == 0 && !isResizeMode)
            {
                button.Opacity = 0.005;
            }
        }

        #endregion


        #region ======== UTIL ========

        public static void EffectGray(Bitmap src)
        {
            // https://stackoverflow.com/questions/2265910/convert-an-image-to-grayscale
            //create a blank bitmap the same size as original
            // Bitmap newBitmap = new Bitmap(src.Width, src.Height);

            //get a graphics object from the new image
            using (Graphics g = Graphics.FromImage(src))
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
                    g.DrawImage(src, new Rect(0, 0, src.Width, src.Height),
                                0, 0, src.Width, src.Height, GraphicsUnit.Pixel, attributes);
                }
            }
            // return newBitmap;
        }

        public static void EffectBinarize(Bitmap src, int threshold)
        {
            if (src.PixelFormat != System.Drawing.Imaging.PixelFormat.Format32bppArgb)
                src = src.Clone(new Rect(0, 0, src.Width, src.Height), System.Drawing.Imaging.PixelFormat.Format32bppArgb);

            // Lock the bitmap's bits.  
            Rect rect = new Rect(0, 0, src.Width, src.Height);
            System.Drawing.Imaging.BitmapData bmpData =
                src.LockBits(rect, System.Drawing.Imaging.ImageLockMode.ReadWrite,
                src.PixelFormat);

            // Get the address of the first line.
            IntPtr ptr = bmpData.Scan0;

            // Declare an array to hold the bytes of the bitmap.
            int bytes  = Math.Abs(bmpData.Stride) * src.Height;
            byte[] rgbValues = new byte[bytes];

            // Copy the RGB values into the array.
            System.Runtime.InteropServices.Marshal.Copy(ptr, rgbValues, 0, bytes);

            // Set every alpha value. The order is B, G, R, A
            for (int i = 0; i < rgbValues.Length; i += 1)
                rgbValues[i] = rgbValues[i] > threshold? (byte)255 : (byte)0;

            // Copy the RGB values back to the bitmap
            System.Runtime.InteropServices.Marshal.Copy(rgbValues, 0, ptr, bytes);

            // Unlock the bits.
            src.UnlockBits(bmpData);;
        }
        
        public static void EffectQuantize(Bitmap src, int q)
        {
            if (src.PixelFormat != System.Drawing.Imaging.PixelFormat.Format32bppArgb)
                src = src.Clone(new Rect(0, 0, src.Width, src.Height), System.Drawing.Imaging.PixelFormat.Format32bppArgb);

            // Lock the bitmap's bits.  
            Rect rect = new Rect(0, 0, src.Width, src.Height);
            System.Drawing.Imaging.BitmapData bmpData =
                src.LockBits(rect, System.Drawing.Imaging.ImageLockMode.ReadWrite,
                src.PixelFormat);

            // Get the address of the first line.
            IntPtr ptr = bmpData.Scan0;

            // Declare an array to hold the bytes of the bitmap.
            int bytes  = Math.Abs(bmpData.Stride) * src.Height;
            byte[] rgbValues = new byte[bytes];

            // Copy the RGB values into the array.
            System.Runtime.InteropServices.Marshal.Copy(ptr, rgbValues, 0, bytes);

            // Set every alpha value. The order is B, G, R, A
            for (int i = 0; i < rgbValues.Length; i += 1)
            {
                float quant = 255f / (q-1) / 2;
                for (int iq = 0; iq < q; iq++)
                {
                    var low  = (2 * iq - 1) * quant;
                    var high = (2 * iq + 1) * quant;
                    if (rgbValues[i] > low && rgbValues[i] <= high)
                    {
                        rgbValues[i] = (byte)(2 * iq * quant);
                        break;
                    }
                }
            }

            // Copy the RGB values back to the bitmap
            System.Runtime.InteropServices.Marshal.Copy(rgbValues, 0, ptr, bytes);

            // Unlock the bits.
            src.UnlockBits(bmpData);;
        }

        public static void EffectTransparent(Bitmap src, int transparency)
        {
            // Processing bytes using LockBits is faster than SetPixel/GetPixel
            // https://docs.microsoft.com/ja-jp/dotnet/api/system.drawing.bitmap.lockbits?view=dotnet-plat-ext-6.0
            
            if (src.PixelFormat != System.Drawing.Imaging.PixelFormat.Format32bppArgb)
                src = src.Clone(new Rect(0, 0, src.Width, src.Height), System.Drawing.Imaging.PixelFormat.Format32bppArgb);

            // Lock the bitmap's bits.  
            Rect rect = new Rect(0, 0, src.Width, src.Height);
            System.Drawing.Imaging.BitmapData bmpData =
                src.LockBits(rect, System.Drawing.Imaging.ImageLockMode.ReadWrite,
                src.PixelFormat);

            // Get the address of the first line.
            IntPtr ptr = bmpData.Scan0;

            // Declare an array to hold the bytes of the bitmap.
            int bytes  = Math.Abs(bmpData.Stride) * src.Height;
            byte[] rgbValues = new byte[bytes];

            // Copy the RGB values into the array.
            System.Runtime.InteropServices.Marshal.Copy(ptr, rgbValues, 0, bytes);

            // Set every alpha value. The order is B, G, R, A
            for (int i = 3; i < rgbValues.Length; i += 4)
                rgbValues[i] = (byte)((int)rgbValues[i] * (255-transparency) / 255);

            // Copy the RGB values back to the bitmap
            System.Runtime.InteropServices.Marshal.Copy(rgbValues, 0, ptr, bytes);

            // Unlock the bits.
            src.UnlockBits(bmpData);;
        }

        public static void EffectHuemap(Bitmap src)
        {
            if (src.PixelFormat != System.Drawing.Imaging.PixelFormat.Format32bppArgb)
                src = src.Clone(new Rect(0, 0, src.Width, src.Height), System.Drawing.Imaging.PixelFormat.Format32bppArgb);

            // Lock the bitmap's bits.  
            Rect rect = new Rect(0, 0, src.Width, src.Height);
            System.Drawing.Imaging.BitmapData bmpData =
                src.LockBits(rect, System.Drawing.Imaging.ImageLockMode.ReadWrite,
                src.PixelFormat);

            // Get the address of the first line.
            IntPtr ptr = bmpData.Scan0;

            // Declare an array to hold the bytes of the bitmap.
            int bytes  = Math.Abs(bmpData.Stride) * src.Height;
            byte[] rgbValues = new byte[bytes];

            // Copy the RGB values into the array.
            System.Runtime.InteropServices.Marshal.Copy(ptr, rgbValues, 0, bytes);

            // Set every alpha value. The order is B, G, R, A
            const int quant = 10;
            for (int i = 0; i < rgbValues.Length; i += 4)
            {
                byte b = rgbValues[i];
                byte g = rgbValues[i+1];
                byte r = rgbValues[i+2];
                // byte a = rgbValues[i+3];

                if (Math.Max(b, Math.Max(g, r)) - Math.Min(b, Math.Min(g, r)) < quant)
                {
                    rgbValues[i] = 128;
                    rgbValues[i+1] = 128;
                    rgbValues[i+2] = 128;
                    continue;
                }

                // Get Hue
                float h = 0;
                if (r >= b && r >= g)
                    h = 60f * (g-b) / (r - Math.Min(b, g));
                else if (g > r && g > b)
                    h = 60f * (b-r) / (g - Math.Min(b, r)) + 120;
                else
                    h = 60f * (r-g) / (b - Math.Min(g, r)) + 240;
                h = (float)Math.Round(h / quant) * quant;

                // Get RGB. s = 255, v = 255, thus MAX = 255, MIN = 0
                if (h >= 0 && h < 60)
                {
                    r = 255; g = (byte)(h/60*255); b = 0;
                }
                else if (h >= 60 && h < 120)
                {
                    r = (byte)((120-h)/60 * 255); g = 255; b = 0;
                }
                else if (h >= 120 && h < 180)
                {
                    r = 0; g = 255; b = (byte)((h-120)/60 * 255);
                }
                else if (h >= 180 && h < 240)
                {
                    r = 0; g = (byte)((240-h)/60 * 255); b = 255;
                }
                else if (h >= 240 && h < 300)
                {
                    r = (byte)((h-240)/60 * 255); g = 0; b = 255;
                }
                else
                {
                    r = 255; g = 0; b = (byte)((360-h)/60 * 255);
                }

                rgbValues[i] = b;
                rgbValues[i+1] = g;
                rgbValues[i+2] = r;
                // rgbValues[i+3] = 255;
            }

            // Copy the RGB values back to the bitmap
            System.Runtime.InteropServices.Marshal.Copy(rgbValues, 0, ptr, bytes);

            // Unlock the bits.
            src.UnlockBits(bmpData);;
        }
        
        /* // Cv2's dll is too large
        public static Bitmap CvGray(Bitmap src)
        {
            var mat = src.ToMat();
            var mat_gray = mat.CvtColor(ColorConversionCodes.BGR2GRAY);
            mat_gray = mat_gray.CvtColor(ColorConversionCodes.GRAY2BGRA);
            return mat_gray.ToBitmap();
        }

        public static Bitmap CvBinarize(Bitmap src, int threshold)
        {
            var mat = src.ToMat();
            var mat_gray = mat.CvtColor(ColorConversionCodes.BGR2GRAY);
            var mat_thr = mat_gray.Threshold(threshold, 255, ThresholdTypes.Binary);
            mat_thr = mat_thr.CvtColor(ColorConversionCodes.GRAY2BGRA);
            return mat_thr.ToBitmap();
        }

        public static Bitmap CvTransparent(Bitmap src, int transparent)
        {
            var mat = src.ToMat();
            var channels = mat.Split();
            var alpha = channels[3];
            alpha.ConvertTo(alpha, MatType.CV_16UC1);
            alpha = alpha * (255-transparent) / 255;
            alpha.ConvertTo(alpha, MatType.CV_8UC1);
            channels[3] = alpha;
            Cv2.Merge(channels, mat);
            return mat.ToBitmap();
        }
        */        

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
