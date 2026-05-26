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
        private static bool isFeaturePointModeEnabled { get => Scene.IsStitchMode; set => Scene.IsStitchMode = value; }
        private static readonly FieldInfo menuDropAlignmentField = typeof(SystemParameters).GetField("_menuDropAlignment", BindingFlags.NonPublic | BindingFlags.Static);

        private double dpiFactor { get => Item.DpiFactor; set => Item.DpiFactor = value; }

        public SceneItem Item { get; private set; }

        private Bitmap bitmap = null;
        private Bitmap bitmapTransformed { get => Item.BitmapTransformed; set => Item.BitmapTransformed = value; }

        // effect
        private double scale { get => Item.Scale; }
        private List<char> geometryTransformHistory => Item.GeometryTransformHistory;
        private DrawingDocumentData drawingDocument { get => Item.DrawingDocument; set => Item.DrawingDocument = value; }
        private DrawingStrokeData activeDrawingStroke = null;
        private EditModePanel editModePanel = null;
        private bool isEditMode = false;
        private bool isDrawingStroke = false;
        private EditTool currentEditTool = EditTool.Brush;
        private double drawingBrushSize = 5;
        private Stack<DrawingDocumentData> drawingUndoStack => Item.DrawingUndoStack;
        private DrawingDocumentData pendingDrawingOperationSnapshot = null;
        private bool pendingDrawingOperationChanged = false;
        private const double MinimumDrawingBrushSize = 1;
        private const double MaximumDrawingBrushSize = 64;

        private bool isSaving = false;
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
        private double anchorLeft { get => Item.Left; set => Item.Left = value; }
        private double anchorTop { get => Item.Top; set => Item.Top = value; }


        public Memo(SceneItem item)    // Physical coordinates
        {
            this.Item = item;
            InitializeComponent();

            ApplyConfiguredBitmapScalingMode();

            this.image.Source = item.Bitmap;
            Left = item.Left;
            Top = item.Top;
            Width = item.GetWidth();
            Height = item.GetHeight();

            Console.WriteLine($"Memo created for item {Id} at ({Left}, {Top}) with size ({item.GetWidth()}x{item.GetHeight()})");

            LocationChanged += MemoLocationChanged;
            SizeChanged += MemoSizeChanged;
            InitializeContextMenu();
            UpdateResizeVisuals();

            if (showFeaturePoints)
                RefreshAllMemoFeatureOverlays();

            // Register
            item.RegisterView(this);

            NotifiedDisplayMode();
        }

        private static bool CanInteract => Scene.DisplayMode == EDisplayMode.Expanded;

        public static IReadOnlyList<Memo> GetAllMemos()
        {
            return Application.Current.Windows.OfType<Window>().OfType<Memo>().ToList();
        }


        #region ======= ISceneItemView Implementation ========
        public Guid Id => Item.Id;
        public bool IsRenderer => false;

        public void NotifiedCanvasActive()
        {
            if (Scene.IsCanvasActive)
                DisplayMinimized();
            else
            {
                NotifiedEffect();
                NotifiedDisplayMode();
            }
        }

        public void NotifiedDisplayMode()
        {
            Console.WriteLine(Scene.IsCanvasActive);
            if (Scene.IsCanvasActive)
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

            UpdateResizeVisuals();
        }

        public void NotifiedClose()
        {
            SaveToHistory();
            ExitEditMode();

            this.image.Source = null;
            if (Mouse.Captured == this) Mouse.Capture(null);
            Close();

            GC.Collect();
        }

        public void NotifiedFocus()
        {
            if (Scene.FocusedId != Id)
                return;

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
            if (isSuspendingDisplayPosition)
                return;

            ApplyDisplayPosition(Item.Left, Item.Top);
        }

        public void NotifiedTransform()
        {
            Width = Item.GetWidth();
            Height = Item.GetHeight();
        }

        public void NotifiedEffect()
        {
            var e = effect as ImageEffect;
            e.IsGray = Item.IsEffectGray ? 1 : 0;
            e.IsHuemap = Item.IsEffectHuemap ? 1 : 0;
            e.IsBinarize = Item.IsEffectBinarize ? 1 : 0;
            e.BinarizeThreshold = Item.PEffectBinarize;
            e.IsQuantize = Item.IsEffectQuantize ? 1 : 0;
            e.QuantizeLevels = Item.PEffectQuantize;
            Opacity = Item.IsEffectTransparent ? Item.PEffectTransparent / 255.0 : 1;
        }

        #endregion


        private void SaveToHistory()
        {
            // HistoryStore.Save(bitmapsource, Left, Top, Width, Height, drawingDocument.Clone());
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
        }

        private void ApplyConfiguredEffects(Bitmap bitmapToUpdate)
        {
            if (Item.IsEffectGray)
                Effects.Gray(bitmapToUpdate);

            if (Item.IsEffectBinarize && Item.PEffectBinarize > 0)
                Effects.Binarize(bitmapToUpdate, Item.PEffectBinarize);
            else if (Item.IsEffectQuantize && Item.PEffectQuantize > 2)
                Effects.Quantize(bitmapToUpdate, Item.PEffectQuantize);

            if (Item.IsEffectHuemap)
                Effects.Huemap(bitmapToUpdate);

            if (Item.IsEffectTransparent && Item.PEffectTransparent > 0)
                Effects.Transparent(bitmapToUpdate, Item.PEffectTransparent);
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
                graphics.InterpolationMode = Effects.GetConfiguredInterpolationMode();
                graphics.DrawImage(sourceBitmap, new Rect(0, 0, width, height), 0, 0, sourceBitmap.Width, sourceBitmap.Height, GraphicsUnit.Pixel);
            }

            return resizedBitmap;
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
            Item.SetEffectGray(!Item.IsEffectGray);
            ShowCenterInfoFading("Grayscale", Item.IsEffectGray ? "On" : "Off");
        }

        private void ToggleHueMap()
        {
            Item.SetEffectHuemap(!Item.IsEffectHuemap);
            ShowCenterInfoFading("Hue Map", Item.IsEffectHuemap ? "On" : "Off");
        }


        #region ======== Effect Ops ========

        private void SetEffectBinarize(bool enabled, int? percent = null)
        {
            var p = percent.HasValue ? (int?)Math.Round(255 * percent.Value / 100.0) : null;
            Item.SetEffectBinarize(enabled, p);
            if (enabled) Item.SetEffectQuantize(false);
            ShowCenterInfoFading("Binarization", ThrToPercentInfo(enabled, Item.PEffectBinarize));
        }

        private void SetEffectQuantize(bool enabled, int? level = null)
        {
            Item.SetEffectQuantize(enabled, level);
            if (enabled) Item.SetEffectBinarize(false);
            ShowCenterInfoFading("Quantization", $"{level} levels");
        }

        private void SetEffectTransparent(bool enabled, int? percent = null)
        {
            var p = percent.HasValue ? (int?)Math.Round(255 * percent.Value / 100.0) : null;
            Item.SetEffectTransparent(enabled, p);
            ShowCenterInfoFading("Opacity", ThrToPercentInfo(enabled, Item.PEffectTransparent));
        }
        #endregion


        #region ======== Display ========

        private void DisplayExpanded()
        {
            if (!IsVisible) Show();
            image.Opacity = GetCurrentImageOpacity();
            ApplyDisplayPosition(Item.Left, Item.Top);
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

            ApplyDisplayPosition(Item.Left, Item.Top);
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

        #endregion

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
