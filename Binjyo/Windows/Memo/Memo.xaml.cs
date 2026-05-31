using System;
using System.Linq;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Interop;


namespace Binjyo
{

    /// <summary>
    /// Memo.xaml の相互作用ロジック
    /// </summary>
    public partial class Memo : Window, ISceneItemView
    {
        private static bool isFeaturePointModeEnabled { get => Scene.IsStitchMode; set => Scene.IsStitchMode = value; }
        private double dpiFactor { get => Item.DpiFactor; set => Item.DpiFactor = value; }

        public SceneItem Item { get; private set; }


        private List<char> geometryTransformHistory = new List<char>();

        private bool isSaving = false;
        private const double MouseEvadeRange = 200;
        private const double MouseEvadeBaseStrength = 300;
        private const double MouseEvadeSpringStrength = 0.4;
        private const double MouseEvadeBlend = 0.35;
        private const double MouseEvadeSettledDistance = 0.5; private ResizeHandle activeResizeHandle = ResizeHandle.None;


        public Memo(SceneItem item)    // Physical coordinates
        {
            this.Item = item;
            InitializeComponent();

            ApplyConfiguredBitmapScalingMode();

            this.image.Source = item.Bitmap;
            Left = item.Left;
            Top = item.Top;
            Width = item.Width;
            Height = item.Height;

            Console.WriteLine($"Memo created for item {Id} at ({Left}, {Top}) with size ({item.Width}x{item.Height})");

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

        private List<Memo> GetVisibleMemos()
        {
            return GetAllMemos().Where(item => item.IsVisible).ToList();
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
            return isFeaturePointModeEnabled && isDragging ? 0.5 : 1.0;
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
            FlashHighlight();
        }

        public void NotifiedOpacity()
        {
            Opacity = Item.IsOpacity ? Item.Opacity : 1;
        }

        public void NotifiedTransform(bool moveOnly)
        {
            var w = Item.GetBaseWidth();
            var h = Item.GetBaseHeight();
            var tr = new TransformGroup();
            tr.Children.Add(new ScaleTransform(
                Item.IsFlipX ? -1 : 1,
                Item.IsFlipY ? -1 : 1,
                w / 2,
                h / 2));
            tr.Children.Add(new ScaleTransform(Item.Scale, Item.Scale));
            tr.Children.Add(new RotateTransform(Item.Rotation, 0, 0));
            var rect = tr.TransformBounds(new System.Windows.Rect(0, 0, w, h));
            tr.Children.Add(new TranslateTransform(-rect.X, -rect.Y));

            // Width = rect.Width;
            // Height = rect.Height;
            // ApplyMove(Item.Left, Item.Top);
            RefreshAllMemoFeatureOverlays();

            DisableAnimations();

            // ApplyWindowRect(Item.Left, Item.Top, rect.Width, rect.Height);
            MoveSmoothly((int)Math.Round(Item.Left), (int)Math.Round(Item.Top), (int)Math.Round(rect.Width), (int)Math.Round(rect.Height));

            image.LayoutTransform = tr;
            // image.RenderTransform = tr;

            // InvalidateFeatureAlignmentCachesFor(this);
            // UpdateFeatureOverlayTransform();
            // RenderDrawingOverlay();
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
        }

        #endregion


        #region ======== Transform Ops ========

        private void ApplyMove(double left, double top)
        {
            if (Math.Abs(Left - left) < 0.001 && Math.Abs(Top - top) < 0.001)
                return;

            Left = left;
            Top = top;
        }

        private void RotateMemo90()
        {
            // RotateDrawing90();
            // this.bitmapTransformed.RotateFlip(RotateFlipType.Rotate90FlipNone);
            // geometryTransformHistory.Add('R');
            // UpdateBitmap();
        }

        private void FlipMemoHorizontally()
        {
            // FlipDrawingHorizontally();
            // this.bitmapTransformed.RotateFlip(RotateFlipType.RotateNoneFlipX);
            // geometryTransformHistory.Add('H');
            // UpdateBitmap();
        }

        private void FlipMemoVertically()
        {
            // FlipDrawingVertically();
            // this.bitmapTransformed.RotateFlip(RotateFlipType.RotateNoneFlipY);
            // geometryTransformHistory.Add('V');
            // UpdateBitmap();
        }

        #endregion


        #region ======== Effect Ops ========

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
            Item.SetOpacity(enabled, p);
            ShowCenterInfoFading("Opacity", ThrToPercentInfo(enabled, (int)(Item.Opacity * 255)));
        }
        #endregion


        #region ======== Display ========

        private void DisplayExpanded()
        {
            if (!IsVisible) Show();
            image.Opacity = GetCurrentImageOpacity();
            ApplyMove(Item.Left, Item.Top);
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

            ApplyMove(Item.Left, Item.Top);
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

        private void UpdateEvadeDisplayPosition()
        {
            if (isDragging || isResizing)
            {
                ApplyMove(Item.Left, Item.Top);
                return;
            }

            var mouse = System.Windows.Forms.Control.MousePosition;
            double mouseX = mouse.X / dpiFactor;
            double mouseY = mouse.Y / dpiFactor;
            System.Windows.Rect displayRect = new System.Windows.Rect(Left, Top, Width, Height);
            double signedDistance = GetRectSignedDistance(displayRect, mouseX, mouseY, out double normalX, out double normalY);
            if (signedDistance >= MouseEvadeRange)
            {
                if (Math.Abs(Left - Item.Left) < MouseEvadeSettledDistance && Math.Abs(Top - Item.Top) < MouseEvadeSettledDistance)
                    return;
            }

            double normalized = Clamp(1 - signedDistance / MouseEvadeRange, 0, 1);
            double forceMagnitude = MouseEvadeBaseStrength * normalized * normalized;
            double forceX = normalX * forceMagnitude;
            double forceY = normalY * forceMagnitude;
            double springCap = 400 * Math.Max(1, signedDistance / MouseEvadeRange);
            double springX = Clamp(Item.Left - Left, -springCap, springCap) * MouseEvadeSpringStrength;
            double springY = Clamp(Item.Top - Top, -springCap, springCap) * MouseEvadeSpringStrength;
            double vX = forceX + springX;
            double vY = forceY + springY;

            double targetLeft = Left + vX * MouseEvadeBlend;
            double targetTop = Top + vY * MouseEvadeBlend;
            ApplyMove(targetLeft, targetTop);
        }

        #endregion


        #region ======== IO Ops ========

        // physical coordinates
        private void CombineMemosAtPos(double x, double y)
        {
            var ids = Scene.GetIdsAtPos(x, y);
            if (ids.Count < 2) return;

            var sceneItems = ids.Select(id => Scene.Items[id]).Where(item => item != null).ToList();

            CombinePreview(); // clear preview highlights

            var renderedItems = sceneItems
                .Select(item => new
                {
                    // Memo = memo,
                    Bitmap = Scene.RenderOffscreen(item),
                    Left = (int)Math.Round(item.Left * item.DpiFactor),
                    Top = (int)Math.Round(item.Top * item.DpiFactor),
                    Right = (int)Math.Round((item.Left + item.Width) * item.DpiFactor),
                    Bottom = (int)Math.Round((item.Top + item.Height) * item.DpiFactor)
                })
                .ToList();

            // Calculate union bounds
            int unionLeft = renderedItems.Min(item => item.Left);
            int unionTop = renderedItems.Min(item => item.Top);
            int unionRight = renderedItems.Max(item => item.Right);
            int unionBottom = renderedItems.Max(item => item.Bottom);
            int unionWidth = Math.Max(1, unionRight - unionLeft);
            int unionHeight = Math.Max(1, unionBottom - unionTop);

            // Render combined bitmap
            var visual = new DrawingVisual();
            using (var dc = visual.RenderOpen())
            {
                foreach (var item in renderedItems)
                {
                    dc.DrawImage(item.Bitmap, new System.Windows.Rect(
                        item.Left - unionLeft,
                        item.Top - unionTop,
                        item.Right - item.Left,
                        item.Bottom - item.Top
                    ));
                }
            }
            var rtb = new RenderTargetBitmap(unionWidth, unionHeight, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(visual);
            var wb = new WriteableBitmap(rtb);

            // Create new SceneItem and Memo
            var sceneItem = Scene.CreateItem(wb, unionLeft, unionTop);
            Memo combinedMemo = new Memo(sceneItem);
            CanvasWindow.CreateItem(sceneItem);
            Scene.Focus(sceneItem.Id);

            // Close original memos
            foreach (var id in ids)
                Scene.CloseItem(id);

        }

        public void Save(bool edited = true, bool dialog = true)
        {
            if (isSaving) return;
            isSaving = true;

            try
            {
                if (dialog)
                {
                    Microsoft.Win32.SaveFileDialog dlg = new Microsoft.Win32.SaveFileDialog();
                    string name = DateTime.Now.ToString("yyyy-MM-dd-hh-mm-ss");
                    dlg.FileName = name;
                    dlg.Filter = "Png Image|*.png"; //|Bitmap Image|*.bmp|Gif Image|*.gif";

                    if (dlg.ShowDialog() == true)
                    {
                        var wbmp = edited ? Scene.RenderOffscreen(Item) : Item.Bitmap;
                        var encoder = new PngBitmapEncoder();
                        encoder.Frames.Add(BitmapFrame.Create(wbmp));

                        using (var stream = dlg.OpenFile())
                            encoder.Save(stream);
                    }
                }
                else
                {
                    var desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                    string name = DateTime.Now.ToString("yyyy-MM-dd-hh-mm-ss");
                    string filePath = System.IO.Path.Combine(desktopPath, $"{name}.png");

                    var wbmp = edited ? Scene.RenderOffscreen(Item) : Item.Bitmap;
                    var encoder = new PngBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(wbmp));

                    using (var stream = System.IO.File.Create(filePath))
                        encoder.Save(stream);
                }
            }
            finally
            {
                isSaving = false;
            }
        }

        private void CopyToClipboard(bool edited)
        {
            var wbmp = edited ? Scene.RenderOffscreen(Item) : Item.Bitmap;
            wbmp.Freeze();
            Clipboard.SetImage(wbmp);
        }

        protected void UpdateBitmap()
        {
        }

        #endregion

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowPos(
            IntPtr hWnd,
            IntPtr hWndInsertAfter,
            int X,
            int Y,
            int cx,
            int cy,
            uint uFlags);

        private const uint SWP_NOZORDER = 0x0004;
        private const uint SWP_NOACTIVATE = 0x0010;
        private const uint SWP_NOOWNERZORDER = 0x0200;
        private const uint SWP_NOCOPYBITS = 0x0100;

        private void ApplyWindowRect(double left, double top, double width, double height)
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            if (hwnd == IntPtr.Zero)
                return;

            SetWindowPos(
                hwnd,
                IntPtr.Zero,
                (int)Math.Round(left),
                (int)Math.Round(top),
                Math.Max(1, (int)Math.Round(width)),
                Math.Max(1, (int)Math.Round(height)),
                SWP_NOZORDER);
            //SWP_NOZORDER | SWP_NOACTIVATE | SWP_NOOWNERZORDER | SWP_NOCOPYBITS
        }
        [DllImport("dwmapi.dll")]
        static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        const int DWMWA_TRANSITIONS_FORCEDISABLED = 3;

        public void DisableAnimations()
        {
            IntPtr hwnd = new WindowInteropHelper(this).Handle;
            int True = 1;
            DwmSetWindowAttribute(hwnd, DWMWA_TRANSITIONS_FORCEDISABLED, ref True, sizeof(int));
        }
        [DllImport("user32.dll", SetLastError = true)]
        static extern IntPtr BeginDeferWindowPos(int nNumWindows);

        [DllImport("user32.dll", SetLastError = true)]
        static extern IntPtr DeferWindowPos(IntPtr hWinPosInfo, IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll", SetLastError = true)]
        static extern bool EndDeferWindowPos(IntPtr hWinPosInfo);

        public void MoveSmoothly(int x, int y, int width, int height)
        {
            IntPtr hwnd = new WindowInteropHelper(this).Handle;
            IntPtr hWinPosInfo = BeginDeferWindowPos(1);

            if (hWinPosInfo != IntPtr.Zero)
            {
                // SWP_NOZORDER = 0x0004 | SWP_NOACTIVATE = 0x0010
                hWinPosInfo = DeferWindowPos(hWinPosInfo, hwnd, IntPtr.Zero, x, y, width, height, 0x0014);
                EndDeferWindowPos(hWinPosInfo);
            }
        }

    }
}
