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
using System.Reflection;
using System.Windows.Media.Animation;
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

    public enum EditTool
    {
        Brush,
        Eraser
    }

    public enum MemoDisplayMode
    {
        Expanded,
        AutoHide,
        Minimized
    }

    public enum MemoAutoHideBehavior
    {
        HideOnHover = 0,
        EvadeMouse = 1
    }

    public enum MemoBitmapScalingMode
    {
        NearestNeighbor = 0,
        Linear = 1,
        Fant = 2
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
        private static long focusSequence = 0;
        private static MemoDisplayMode globalDisplayMode = MemoDisplayMode.Expanded;
        private static readonly FieldInfo menuDropAlignmentField = typeof(SystemParameters).GetField("_menuDropAlignment", BindingFlags.NonPublic | BindingFlags.Static);
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
        private static bool isHSVWheelPinnedGlobally = false;
        private readonly List<char> geometryTransformHistory = new List<char>();
        private DrawingDocumentData drawingDocument = new DrawingDocumentData();
        private DrawingStrokeData activeDrawingStroke = null;
        private EditModePanel editModePanel = null;
        private bool isEditMode = false;
        private bool isDrawingStroke = false;
        private EditTool currentEditTool = EditTool.Brush;
        private double drawingBrushSize = 5;
        private readonly Stack<DrawingDocumentData> drawingUndoStack = new Stack<DrawingDocumentData>();
        private DrawingDocumentData pendingDrawingOperationSnapshot = null;
        private bool pendingDrawingOperationChanged = false;
        private const double MinimumDrawingBrushSize = 1;
        private const double MaximumDrawingBrushSize = 64;

        private double dragStartMouseX, dragStartMouseY;
        private double dragStartLeft, dragStartTop;
        private bool isdrag = false;
        private bool isResizeMode = false;
        private bool isResizing = false;
        private bool isSaving = false;
        private bool isClosing = false;
        private bool isSuspendingDisplayPosition = false;
        private EventHandler centerInfoFadeCompletedHandler = null;
        private const double SnapDistance = 12;
        private const double MinVisiblePixels = 2;
        private const double ResizeHandleSize = 14;
        private const double ResizeHandleInset = 2;
        private const double MouseEvadeRange = 200;
        private const double MouseEvadeBaseStrength = 300;
        private const double MouseEvadeSpringStrength = 0.4;
        private const double MouseEvadeBlend = 0.35;
        private const double MouseEvadeSettledDistance = 0.5;
        private readonly int[] binarizePercentOptions = new[] { 10, 20, 30, 40, 50, 60, 70, 80, 90 };
        private readonly int[] quantizeLevelOptions = new[] { 3, 4, 5, 6, 8, 12, 16 };
        private readonly int[] transparencyPercentOptions = new[] { 10, 20, 30, 40, 50, 60, 70, 80, 90 };
        private int leftArrowRepeatCount = 0;
        private int rightArrowRepeatCount = 0;
        private int upArrowRepeatCount = 0;
        private int downArrowRepeatCount = 0;
        private bool dragMovesConnectedGroup = false;
        private Dictionary<Memo, System.Windows.Point> dragStartPositions = new Dictionary<Memo, System.Windows.Point>();
        private ResizeHandle activeResizeHandle = ResizeHandle.None;
        private double resizeStartScale = 1;
        private double resizeStartLeft = 0;
        private double resizeStartTop = 0;
        private double resizeStartRight = 0;
        private double resizeStartBottom = 0;
        private long lastFocusOrder = 0;
        private double anchorLeft = 0;
        private double anchorTop = 0;
        private bool hasAnchorPosition = false;
        private ContextMenu memoContextMenu = null;
        private MenuItem resizeModeMenuItem = null;
        private MenuItem editModeMenuItem = null;
        private MenuItem grayscaleMenuItem = null;
        private MenuItem hueMapMenuItem = null;
        private MenuItem binarizeOffMenuItem = null;
        private Dictionary<int, MenuItem> binarizeMenuItems = null;
        private MenuItem quantizeOffMenuItem = null;
        private Dictionary<int, MenuItem> quantizeMenuItems = null;
        private MenuItem transparencyOffMenuItem = null;
        private Dictionary<int, MenuItem> transparencyMenuItems = null;
        private bool? originalMenuDropAlignment = null;


        public Memo(Bitmap bmp, int left, int top)    // Physical coordinates
        {
            InitializeComponent();
            ApplyConfiguredBitmapScalingMode();
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
            anchorLeft = Left;
            anchorTop = Top;
            hasAnchorPosition = true;

            this.bitmap = bmp;
            this.bitmapTransformed = (Bitmap)this.bitmap.Clone();
            this._ShowBitmap(bmp);
            InitializeContextMenu();
            LocationChanged += MemoBoundsChanged;
            SizeChanged += MemoBoundsChanged;
            UpdateResizeModeVisuals();
            ApplyCurrentDisplayMode();
        }

        public static MemoDisplayMode GetGlobalDisplayMode()
        {
            return globalDisplayMode;
        }

        public static void SetGlobalDisplayMode(MemoDisplayMode mode)
        {
            globalDisplayMode = mode;
            foreach (Memo memo in GetVisibleAndHiddenMemos())
                memo.ApplyCurrentDisplayMode();
        }

        public static void CycleGlobalDisplayMode()
        {
            MemoDisplayMode nextMode;
            switch (globalDisplayMode)
            {
                case MemoDisplayMode.Expanded:
                    nextMode = MemoDisplayMode.AutoHide;
                    break;
                case MemoDisplayMode.AutoHide:
                    nextMode = MemoDisplayMode.Minimized;
                    break;
                default:
                    nextMode = MemoDisplayMode.Expanded;
                    break;
            }

            SetGlobalDisplayMode(nextMode);
        }

        protected void _Close()
        {
            if (isClosing)
                return;

            isClosing = true;
            SaveToHistory();
            ExitEditMode();
            if (this.bitmap != null) this.bitmap.Dispose();
            if (this.bitmapTransformed != null) this.bitmapTransformed.Dispose();
            this.image.Source = null;
            this.bitmpasource = null;
            if (this.timer != null) this.timer.Stop();
            if (Mouse.Captured == this) Mouse.Capture(null);
            this.Close();
            GC.Collect();
        }

        public void CloseMemo()
        {
            _Close();
        }

        private void SaveToHistory()
        {
            if (bitmpasource == null)
                return;

            HistoryStore.Save(bitmpasource, Left, Top, Width, Height, drawingDocument.Clone());
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
                RenderDrawingOverlay();
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

        private Bitmap CreateOutputBitmap(bool exportCurrentView)
        {
            if (!exportCurrentView)
                return bitmap.Clone(new Rect(0, 0, bitmap.Width, bitmap.Height), System.Drawing.Imaging.PixelFormat.Format32bppArgb);

            bool applyTransform = Properties.Settings.Default.ExportApplyTransform;
            bool applyEffects = Properties.Settings.Default.ExportApplyEffects;
            Bitmap baseBitmap = applyTransform ? bitmapTransformed : bitmap;
            Bitmap outputBitmap = baseBitmap.Clone(new Rect(0, 0, baseBitmap.Width, baseBitmap.Height), System.Drawing.Imaging.PixelFormat.Format32bppArgb);

            if (applyEffects)
                ApplyConfiguredEffects(outputBitmap);

            DrawingDocumentData documentToRender = applyTransform
                ? drawingDocument
                : MapDrawingDocumentToOriginal();
            ApplyDrawingToBitmap(outputBitmap, documentToRender);

            int targetWidth = Math.Max(1, (int)Math.Round(Width * dpiFactor));
            int targetHeight = Math.Max(1, (int)Math.Round(Height * dpiFactor));
            if (outputBitmap.Width == targetWidth && outputBitmap.Height == targetHeight)
                return outputBitmap;

            Bitmap resizedBitmap = ResizeBitmapForExport(outputBitmap, targetWidth, targetHeight);
            outputBitmap.Dispose();
            return resizedBitmap;
        }

        private BitmapSource CreateOutputBitmapSource(bool exportCurrentView)
        {
            using (Bitmap outputBitmap = CreateOutputBitmap(exportCurrentView))
            {
                BitmapSource bitmapSource = outputBitmap.ToBitmapSource(PixelFormats.Bgra32);
                bitmapSource.Freeze();
                return bitmapSource;
            }
        }

        private void CopyMemoToClipboard(bool exportCurrentView)
        {
            Clipboard.SetImage(CreateOutputBitmapSource(exportCurrentView));
        }

        protected void UpdateBitmap()
        {
            var res = _GetBitmapAfterEffect();
            _ShowBitmap(res, true);
        }

        private void ApplyConfiguredEffects(Bitmap bitmapToUpdate)
        {
            if (isEffectGray)
                EffectGray(bitmapToUpdate);

            if (isEffectBinarize && pEffectBinarize > 0)
                EffectBinarize(bitmapToUpdate, pEffectBinarize);
            else if (isEffectQuantize && pEffectQuantize > 2)
                EffectQuantize(bitmapToUpdate, pEffectQuantize);

            if (isEffectHuemap)
                EffectHuemap(bitmapToUpdate);

            if (isEffectTransparent && pEffectTransparent > 0)
                EffectTransparent(bitmapToUpdate, pEffectTransparent);
        }

        private Bitmap ResizeBitmapForExport(Bitmap sourceBitmap, int width, int height)
        {
            var resizedBitmap = new Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            using (Graphics graphics = Graphics.FromImage(resizedBitmap))
            {
                graphics.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceCopy;
                graphics.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;
                graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;
                graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                graphics.InterpolationMode = GetConfiguredInterpolationMode();
                graphics.DrawImage(sourceBitmap, new Rect(0, 0, width, height), 0, 0, sourceBitmap.Width, sourceBitmap.Height, GraphicsUnit.Pixel);
            }

            return resizedBitmap;
        }

        private static System.Drawing.Drawing2D.InterpolationMode GetConfiguredInterpolationMode()
        {
            switch ((MemoBitmapScalingMode)Properties.Settings.Default.BitmapScalingMode)
            {
                case MemoBitmapScalingMode.NearestNeighbor:
                    return System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
                case MemoBitmapScalingMode.Linear:
                    return System.Drawing.Drawing2D.InterpolationMode.HighQualityBilinear;
                default:
                    return System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
            }
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
            ApplyCurrentDisplayMode();
        }

        #endregion

        public void Minimize()
        {
            SetGlobalDisplayMode(MemoDisplayMode.Minimized);
        }
        public void Expand()
        {
            SetGlobalDisplayMode(MemoDisplayMode.Expanded);
        }

        public void SetAutoHide()
        {
            SetGlobalDisplayMode(MemoDisplayMode.AutoHide);
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

        private void RotateMemo90()
        {
            RotateDrawing90();
            this.bitmapTransformed.RotateFlip(RotateFlipType.Rotate90FlipNone);
            geometryTransformHistory.Add('R');
            UpdateBitmap();
        }

        private void FlipMemoHorizontally()
        {
            FlipDrawingHorizontally();
            this.bitmapTransformed.RotateFlip(RotateFlipType.RotateNoneFlipX);
            geometryTransformHistory.Add('H');
            UpdateBitmap();
        }

        private void FlipMemoVertically()
        {
            FlipDrawingVertically();
            this.bitmapTransformed.RotateFlip(RotateFlipType.RotateNoneFlipY);
            geometryTransformHistory.Add('V');
            UpdateBitmap();
        }

        private void ToggleGrayscale()
        {
            isEffectGray = !isEffectGray;
            UpdateBitmap();
            ShowCenterInfoFading("Grayscale", isEffectGray ? "On" : "Off");
        }

        private void ToggleHueMap()
        {
            isEffectHuemap = !isEffectHuemap;
            UpdateBitmap();
            ShowCenterInfoFading("Hue Map", isEffectHuemap ? "On" : "Off");
        }

        private void ToggleBinarization()
        {
            isEffectBinarize = !isEffectBinarize;
            if (isEffectBinarize)
                isEffectQuantize = false;
            UpdateBitmap();
            ShowCenterInfoFading("Binarization", isEffectBinarize ? $"{ThresholdToPercent(pEffectBinarize)}%" : "Off");
        }

        private void ToggleQuantization()
        {
            isEffectQuantize = !isEffectQuantize;
            if (isEffectQuantize)
                isEffectBinarize = false;
            UpdateBitmap();
            ShowCenterInfoFading("Quantization", isEffectQuantize ? $"{pEffectQuantize} levels" : "Off");
        }

        private void ToggleTransparency()
        {
            isEffectTransparent = !isEffectTransparent;
            UpdateBitmap();
            ShowCenterInfoFading("Transparency", isEffectTransparent ? $"{ThresholdToPercent(pEffectTransparent)}%" : "Off");
        }

        private void SetBinarizationEnabled(bool enabled)
        {
            isEffectBinarize = enabled;
            if (enabled)
                isEffectQuantize = false;
            UpdateBitmap();
            ShowCenterInfoFading("Binarization", enabled ? $"{ThresholdToPercent(pEffectBinarize)}%" : "Off");
        }

        private void SetBinarizationPercent(int percent)
        {
            pEffectBinarize = PercentToThreshold(percent);
            isEffectBinarize = true;
            isEffectQuantize = false;
            UpdateBitmap();
            ShowCenterInfoFading("Binarization", $"{percent}%");
        }

        private void SetQuantizationEnabled(bool enabled)
        {
            isEffectQuantize = enabled;
            if (enabled)
                isEffectBinarize = false;
            UpdateBitmap();
            ShowCenterInfoFading("Quantization", enabled ? $"{pEffectQuantize} levels" : "Off");
        }

        private void SetQuantizationLevel(int level)
        {
            pEffectQuantize = level;
            isEffectQuantize = true;
            isEffectBinarize = false;
            UpdateBitmap();
            ShowCenterInfoFading("Quantization", $"{level} levels");
        }

        private void SetTransparencyEnabled(bool enabled)
        {
            isEffectTransparent = enabled;
            UpdateBitmap();
            ShowCenterInfoFading("Transparency", enabled ? $"{ThresholdToPercent(pEffectTransparent)}%" : "Off");
        }

        private void SetTransparencyPercent(int percent)
        {
            pEffectTransparent = PercentToThreshold(percent);
            isEffectTransparent = true;
            UpdateBitmap();
            ShowCenterInfoFading("Transparency", $"{percent}%");
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
            if (isEditMode)
                return;

            isResizeMode = enabled && CanInteractNormally();
            if (!isResizeMode)
                StopResize();
            UpdateResizeModeVisuals();
        }

        private void UpdateResizeModeVisuals()
        {
            if (resizeOverlay == null)
                return;

            resizeOverlay.Visibility = isResizeMode && CanInteractNormally() && !isEditMode
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        private bool CanInteractNormally()
        {
            return globalDisplayMode == MemoDisplayMode.Expanded;
        }

        private void ApplyCurrentDisplayMode()
        {
            if (isClosing)
                return;

            if (!hasAnchorPosition)
                return;

            switch (globalDisplayMode)
            {
                case MemoDisplayMode.Minimized:
                    ApplyMinimizedDisplayMode();
                    break;
                case MemoDisplayMode.AutoHide:
                    ApplyAutoHideDisplayMode();
                    break;
                default:
                    ApplyExpandedDisplayMode();
                    break;
            }

            UpdateResizeModeVisuals();
        }

        private void ApplyExpandedDisplayMode()
        {
            EnsureMemoVisible();
            image.Opacity = 1;
            ApplyDisplayPosition(anchorLeft, anchorTop);
        }

        private void ApplyAutoHideDisplayMode()
        {
            EnsureMemoVisible();

            if ((MemoAutoHideBehavior)Properties.Settings.Default.AutoHideBehavior == MemoAutoHideBehavior.EvadeMouse)
            {
                image.Opacity = 1;
                UpdateEvadeDisplayPosition();
                return;
            }

            ApplyDisplayPosition(anchorLeft, anchorTop);
            image.Opacity = IsMouseInsideMemoBounds() ? 0 : 1;
        }

        private void ApplyMinimizedDisplayMode()
        {
            isdrag = false;
            dragStartPositions.Clear();
            StopResize();
            if (isResizeMode)
                isResizeMode = false;
            _HideHSVWheel();
            if (IsVisible)
                Hide();
        }

        private void EnsureMemoVisible()
        {
            if (isClosing)
                return;

            if (!IsVisible)
                Show();
        }

        private void ApplyDisplayPosition(double left, double top)
        {
            if (Math.Abs(Left - left) < 0.001 && Math.Abs(Top - top) < 0.001)
                return;

            isSuspendingDisplayPosition = true;
            Left = left;
            Top = top;
            isSuspendingDisplayPosition = false;
        }

        private void SetAnchorPosition(double left, double top)
        {
            anchorLeft = left;
            anchorTop = top;
            hasAnchorPosition = true;
            ApplyCurrentDisplayMode();
        }

        private void SetCenterInfoText(string title, string detail)
        {
            resizeScaleText.Text = title;
            resizeScaleTextStrokeLeft.Text = title;
            resizeScaleTextStrokeRight.Text = title;
            resizeScaleTextStrokeTop.Text = title;
            resizeScaleTextStrokeBottom.Text = title;
            resizeSizeText.Text = detail;
            resizeSizeTextStrokeLeft.Text = detail;
            resizeSizeTextStrokeRight.Text = detail;
            resizeSizeTextStrokeTop.Text = detail;
            resizeSizeTextStrokeBottom.Text = detail;
        }

        private void ShowCenterInfoPersistent(string title, string detail)
        {
            if (resizeInfoOverlay == null)
                return;

            SetCenterInfoText(title, detail);
            resizeInfoOverlay.BeginAnimation(UIElement.OpacityProperty, null);
            resizeInfoOverlay.Visibility = Visibility.Visible;
            resizeInfoOverlay.Opacity = 1;
        }

        private void ShowCenterInfoFading(string title, string detail)
        {
            if (resizeInfoOverlay == null)
                return;

            SetCenterInfoText(title, detail);
            resizeInfoOverlay.BeginAnimation(UIElement.OpacityProperty, null);
            resizeInfoOverlay.Visibility = Visibility.Visible;
            resizeInfoOverlay.Opacity = 1;

            if (centerInfoFadeCompletedHandler != null)
                resizeInfoOverlay.BeginAnimation(UIElement.OpacityProperty, null);

            var animation = new DoubleAnimationUsingKeyFrames();
            animation.KeyFrames.Add(new DiscreteDoubleKeyFrame(1, KeyTime.FromTimeSpan(TimeSpan.Zero)));
            animation.KeyFrames.Add(new DiscreteDoubleKeyFrame(1, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(550))));
            animation.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(900))));
            centerInfoFadeCompletedHandler = (s, e) =>
            {
                resizeInfoOverlay.Visibility = Visibility.Collapsed;
                resizeInfoOverlay.Opacity = 1;
                centerInfoFadeCompletedHandler = null;
            };
            animation.Completed += centerInfoFadeCompletedHandler;
            resizeInfoOverlay.BeginAnimation(UIElement.OpacityProperty, animation);
        }

        private void HideCenterInfo()
        {
            if (resizeInfoOverlay == null)
                return;

            resizeInfoOverlay.BeginAnimation(UIElement.OpacityProperty, null);
            resizeInfoOverlay.Visibility = Visibility.Collapsed;
            resizeInfoOverlay.Opacity = 1;
            centerInfoFadeCompletedHandler = null;
        }

        private void UpdateEvadeDisplayPosition()
        {
            if (isdrag || isResizing || isEditMode)
            {
                ApplyDisplayPosition(anchorLeft, anchorTop);
                return;
            }

            var mouse = System.Windows.Forms.Control.MousePosition;
            double mouseX = mouse.X / dpiFactor;
            double mouseY = mouse.Y / dpiFactor;
            System.Windows.Rect displayRect = new System.Windows.Rect(Left, Top, Width, Height);
            double signedDistance = GetRectSignedDistance(displayRect, mouseX, mouseY, out double normalX, out double normalY);
            if (signedDistance >= MouseEvadeRange)
            {
                if (Math.Abs(Left - anchorLeft) < MouseEvadeSettledDistance && Math.Abs(Top - anchorTop) < MouseEvadeSettledDistance)
                    return;
            }

            double normalized = Clamp(1 - signedDistance / MouseEvadeRange, 0, 1);
            double forceMagnitude = MouseEvadeBaseStrength * normalized * normalized;
            double forceX = normalX * forceMagnitude;
            double forceY = normalY * forceMagnitude;
            double springCap = 400 * Math.Max(1, signedDistance / MouseEvadeRange);
            double springX = Clamp(anchorLeft - Left, -springCap, springCap) * MouseEvadeSpringStrength;
            double springY = Clamp(anchorTop - Top, -springCap, springCap) * MouseEvadeSpringStrength;
            double vX = forceX + springX;
            double vY = forceY + springY;

            double targetLeft = Left + vX * MouseEvadeBlend;
            double targetTop = Top + vY * MouseEvadeBlend;
            ApplyDisplayPosition(targetLeft, targetTop);
        }

        private bool IsMouseInsideMemoBounds()
        {
            var mouse = System.Windows.Forms.Control.MousePosition;
            double x = mouse.X / dpiFactor;
            double y = mouse.Y / dpiFactor;
            return Left <= x && x <= Left + Width && Top <= y && y <= Top + Height;
        }

        private static double Lerp(double current, double target, double amount)
        {
            return current + (target - current) * amount;
        }

        private static double GetRectSignedDistance(System.Windows.Rect rect, double x, double y, out double normalX, out double normalY)
        {
            double leftDistance = x - rect.Left;
            double rightDistance = rect.Right - x;
            double topDistance = y - rect.Top;
            double bottomDistance = rect.Bottom - y;
            bool isInside = leftDistance >= 0 && rightDistance >= 0 && topDistance >= 0 && bottomDistance >= 0;

            if (isInside)
            {
                double minDistance = leftDistance;
                normalX = 1;
                normalY = 0;

                if (rightDistance < minDistance)
                {
                    minDistance = rightDistance;
                    normalX = -1;
                    normalY = 0;
                }

                if (topDistance < minDistance)
                {
                    minDistance = topDistance;
                    normalX = 0;
                    normalY = 1;
                }

                if (bottomDistance < minDistance)
                {
                    minDistance = bottomDistance;
                    normalX = 0;
                    normalY = -1;
                }

                return -minDistance;
            }

            double nearestX = Clamp(x, rect.Left, rect.Right);
            double nearestY = Clamp(y, rect.Top, rect.Bottom);
            double deltaX = nearestX - x;
            double deltaY = nearestY - y;
            double distance = Math.Sqrt(deltaX * deltaX + deltaY * deltaY);

            if (distance < 0.0001)
            {
                normalX = 1;
                normalY = 0;
                return 0;
            }

            normalX = deltaX / distance;
            normalY = deltaY / distance;
            return distance;
        }


        public void Save()
        {
            Save(true);
        }

        public void Save(bool includeDrawing)
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
                    using (Bitmap outputBitmap = CreateOutputBitmap(includeDrawing))
                    using (var stream = dlg.OpenFile())
                    {
                        outputBitmap.Save(stream, ImageFormat.Png);
                    }
                }
            }
            finally
            {
                isSaving = false;
            }
        }

    }
}
