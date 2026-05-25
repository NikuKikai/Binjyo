using System;
using System.Linq;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using System.Drawing;
using System.Drawing.Imaging;
using System.Reflection;

using Rect = System.Drawing.Rectangle;


namespace Binjyo
{

    /// <summary>
    /// Memo.xaml の相互作用ロジック
    /// </summary>
    public partial class Memo : Window, ISceneItemView
    {
        private static long focusSequence = 0;
        private static bool isFeaturePointModeEnabled { get => Scene.IsStitchMode; set => Scene.IsStitchMode = value; }
        private static readonly FieldInfo menuDropAlignmentField = typeof(SystemParameters).GetField("_menuDropAlignment", BindingFlags.NonPublic | BindingFlags.Static);
        private DispatcherTimer timer = null;

        private double dpiFactor { get => sceneItem.DpiFactor; set => sceneItem.DpiFactor = value; }

        private BitmapSource bitmapsource;
        public SceneItem sceneItem { get; private set; }

        private Bitmap bitmap { get => sceneItem.Bitmap; set => sceneItem.Bitmap = value; }
        private Bitmap bitmapTransformed { get => sceneItem.BitmapTransformed; set => sceneItem.BitmapTransformed = value; }
        private int originalBitmapWidth { get => sceneItem.OriginalBitmapWidth; set => sceneItem.OriginalBitmapWidth = value; }
        private int originalBitmapHeight { get => sceneItem.OriginalBitmapHeight; set => sceneItem.OriginalBitmapHeight = value; }

        // effect
        private double scale { get => sceneItem.Scale; set => sceneItem.Scale = value; }
        private bool isEffectGray { get => sceneItem.IsEffectGray; set => sceneItem.IsEffectGray = value; }
        private bool isEffectBinarize { get => sceneItem.IsEffectBinarize; set => sceneItem.IsEffectBinarize = value; }
        private int pEffectBinarize { get => sceneItem.PEffectBinarize; set => sceneItem.PEffectBinarize = value; }
        private bool isEffectQuantize { get => sceneItem.IsEffectQuantize; set => sceneItem.IsEffectQuantize = value; }  // exclusive to isEffectBinarize
        private int pEffectQuantize { get => sceneItem.PEffectQuantize; set => sceneItem.PEffectQuantize = value; }
        private bool isEffectTransparent { get => sceneItem.IsEffectTransparent; set => sceneItem.IsEffectTransparent = value; }
        private int pEffectTransparent { get => sceneItem.PEffectTransparent; set => sceneItem.PEffectTransparent = value; }
        private bool isEffectHuemap { get => sceneItem.IsEffectHuemap; set => sceneItem.IsEffectHuemap = value; }
        private static bool isHSVWheelPinnedGlobally = false;
        private List<char> geometryTransformHistory => sceneItem.GeometryTransformHistory;
        private DrawingDocumentData drawingDocument { get => sceneItem.DrawingDocument; set => sceneItem.DrawingDocument = value; }
        private DrawingStrokeData activeDrawingStroke = null;
        private EditModePanel editModePanel = null;
        private bool isEditMode = false;
        private bool isDrawingStroke = false;
        private EditTool currentEditTool = EditTool.Brush;
        private double drawingBrushSize = 5;
        private Stack<DrawingDocumentData> drawingUndoStack => sceneItem.DrawingUndoStack;
        private DrawingDocumentData pendingDrawingOperationSnapshot = null;
        private bool pendingDrawingOperationChanged = false;
        private const double MinimumDrawingBrushSize = 1;
        private const double MaximumDrawingBrushSize = 64;

        private bool isResizeMode = false;
        private bool isResizing = false;
        private bool isSaving = false;
        private bool isClosing = false;
        private bool flashOnNextActivation = false;
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
        private ResizeHandle activeResizeHandle = ResizeHandle.None;
        private double anchorLeft { get => sceneItem.Left; set => sceneItem.Left = value; }
        private double anchorTop { get => sceneItem.Top; set => sceneItem.Top = value; }
        private bool hasAnchorPosition { get => sceneItem.HasAnchorPosition; set => sceneItem.HasAnchorPosition = value; }


        public Memo(SceneItem item)    // Physical coordinates
        {
            this.sceneItem = item;

            InitializeComponent();
            ApplyConfiguredBitmapScalingMode();
            InitializeTimer();

            int w = item.OriginalBitmapWidth;  // Physical pixel
            int h = item.OriginalBitmapHeight;  // Physical pixel

            var centerx = (int)item.Left + w / 2;
            var centery = (int)item.Top + h / 2;

            //var scr = System.Windows.Forms.Screen.FromPoint(System.Windows.Forms.Control.MousePosition);
            var scr = System.Windows.Forms.Screen.FromPoint(new System.Drawing.Point(centerx, centery));
            dpiFactor = scr.GetDpiFactor();
            Console.WriteLine(dpiFactor);

            Left = item.Left / dpiFactor; Top = item.Top / dpiFactor;

            this.showFeaturePoints = isFeaturePointModeEnabled;
            this.ShowBitmap(item.Bitmap);

            LocationChanged += MemoLocationChanged;
            SizeChanged += MemoSizeChanged;
            InitializeContextMenu();
            UpdateResizeModeVisuals();

            NotifiedDisplayMode();
            if (showFeaturePoints)
                RefreshAllMemoFeatureOverlays();

            // Register
            item.RegisterView(this);
        }

        private static bool CanInteract => Scene.DisplayMode == EDisplayMode.Expanded;

        public static IReadOnlyList<Memo> GetAllMemos()
        {
            return Application.Current.Windows.OfType<Window>().OfType<Memo>().ToList();
        }


        #region ======= ISceneItemView Implementation ========
        public Guid Id => sceneItem.Id;

        public void NotifiedDisplayMode()
        {
            if (isClosing)
                return;

            if (!hasAnchorPosition)
                return;

            switch (Scene.DisplayMode)
            {
                case EDisplayMode.Minimized:
                    DisplayMinimized();
                    break;
                case EDisplayMode.AutoHide:
                    DisplayAutoHide();
                    break;
                default:
                    DisplayExpanded();
                    break;
            }

            UpdateResizeModeVisuals();
        }

        public void NotifiedClose()
        {
            if (isClosing)
                return;
            isClosing = true;

            SaveToHistory();
            ExitEditMode();

            this.image.Source = null;
            bitmapsource = null;
            timer?.Stop();
            if (Mouse.Captured == this) Mouse.Capture(null);
            Close();

            GC.Collect();
        }

        public void NotifiedFocus()
        {
            if (!IsActive)
            {
                flashOnNextActivation = true;
                Activate();
                Focus();
                return;
            }

            flashOnNextActivation = false;
            Focus();
            FlashFocusCue();
        }

        public void NotifiedMove()
        {
            if (isClosing)
                return;

            // if (Scene.DisplayMode != EDisplayMode.Expanded)
            //     return;

            //  if (!hasAnchorPosition)
            //     return;

            if (isSuspendingDisplayPosition)
                return;

            ApplyDisplayPosition(sceneItem.Left, sceneItem.Top);
        }
        #endregion


        private void SaveToHistory()
        {
            if (bitmapsource == null)
                return;

            HistoryStore.Save(bitmapsource, Left, Top, Width, Height, drawingDocument.Clone());
        }

        private void ShowBitmap(Bitmap bmp, bool disposeBitmapAfterRender = false)
        {
            try
            {
                // NOTES: correct transparent rendering, and quicker
                this.bitmapsource = bmp.ToBitmapSource(PixelFormats.Bgra32);
                this.bitmapsource.Freeze();

                Resize(scale);

                this.image.Source = this.bitmapsource;
                RenderDrawingOverlay();
                HandleDisplayedBitmapUpdated(bmp);
                Show();
            }
            finally
            {
                if (disposeBitmapAfterRender && bmp != null)
                    bmp.Dispose();
            }
        }

        private Bitmap GetBitmapAfterEffect()
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
            var res = GetBitmapAfterEffect();
            ShowBitmap(res, true);
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
            switch ((EBitmapScalingMode)Properties.Settings.Default.BitmapScalingMode)
            {
                case EBitmapScalingMode.NearestNeighbor:
                    return System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
                case EBitmapScalingMode.Linear:
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
            NotifiedDisplayMode();
        }

        #endregion


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


        private void DisplayExpanded()
        {
            if (!IsVisible) Show();
            image.Opacity = GetCurrentImageOpacity();
            ApplyDisplayPosition(anchorLeft, anchorTop);
        }

        private void DisplayAutoHide()
        {
            if (!IsVisible) Show();

            if ((EAutoHideBehavior)Properties.Settings.Default.AutoHideBehavior == EAutoHideBehavior.EvadeMouse)
            {
                image.Opacity = GetCurrentImageOpacity();
                UpdateEvadeDisplayPosition();
                return;
            }

            ApplyDisplayPosition(anchorLeft, anchorTop);
            image.Opacity = IsMouseInsideMemoBounds() ? 0 : GetCurrentImageOpacity();
        }

        private void DisplayMinimized()
        {
            Scene.DragMoveEnd();

            dragStartPositions.Clear();
            StopResize();
            if (isResizeMode)
                isResizeMode = false;
            HideHSVWheel();
            if (IsVisible) Hide();
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
            ApplyDisplayPosition(left, top);
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

        private double GetCurrentImageOpacity()
        {
            return isFeaturePointModeEnabled && isdrag ? 0.5 : 1.0;
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

        public void Save(bool includeDrawing = true)
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
