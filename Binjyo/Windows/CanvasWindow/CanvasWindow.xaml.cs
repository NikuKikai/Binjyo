using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using Bitmap = System.Drawing.Bitmap;
using Screen = System.Windows.Forms.Screen;

namespace Binjyo
{
    public sealed class CanvasMemoInteractionContext
    {
        public Memo Memo { get; set; }
        public Rect Bounds { get; set; }
        public BitmapSource BitmapSource { get; set; }
    }

    public partial class CanvasWindow : Window
    {
        private const double MinimumZoom = 0.1;
        private const double MaximumZoom = 8.0;
        private const double ZoomStep = 1.1;
        private const double RefreshIntervalMilliseconds = 120;

        private readonly Dictionary<Memo, CanvasMemoVisual> memoVisuals = new Dictionary<Memo, CanvasMemoVisual>();
        private readonly DispatcherTimer refreshTimer = new DispatcherTimer();

        private Matrix sceneMatrix = Matrix.Identity;
        private bool isPanning = false;
        private System.Windows.Point lastPanPoint;
        private Rect desktopBounds;
        private double currentZoom = 1;
        private Memo selectedMemo = null;

        public CanvasWindow()
        {
            InitializeComponent();
            Loaded += CanvasWindow_Loaded;
            Closed += CanvasWindow_Closed;

            refreshTimer.Interval = TimeSpan.FromMilliseconds(RefreshIntervalMilliseconds);
            refreshTimer.Tick += RefreshTimer_Tick;
        }

        public Bitmap GetFocusedMemoImage()
        {
            Memo memo = selectedMemo ?? Memo.GetFocusedMemo();
            return memo?.CreateDisplayBitmap();
        }

        public Bitmap GetMemoImage(Memo memo)
        {
            if (memo == null)
                return null;

            return memo.CreateDisplayBitmap();
        }

        public Memo GetSelectedMemo()
        {
            return selectedMemo;
        }

        private void CanvasWindow_Loaded(object sender, RoutedEventArgs e)
        {
            ConfigureBounds();
            RenderScreens();
            RefreshMemos();
            FitSceneToViewport();
            refreshTimer.Start();
            Focus();
            Keyboard.Focus(this);
        }

        private void CanvasWindow_Closed(object sender, EventArgs e)
        {
            refreshTimer.Stop();
        }

        private void RefreshTimer_Tick(object sender, EventArgs e)
        {
            RefreshMemos();
        }

        private void ConfigureBounds()
        {
            desktopBounds = new Rect(
                SystemParameters.VirtualScreenLeft,
                SystemParameters.VirtualScreenTop,
                SystemParameters.VirtualScreenWidth,
                SystemParameters.VirtualScreenHeight);

            Left = desktopBounds.Left;
            Top = desktopBounds.Top;
            Width = desktopBounds.Width;
            Height = desktopBounds.Height;
        }

        private void RenderScreens()
        {
            foreach (Screen screen in Screen.AllScreens)
            {
                var dpi = screen.GetDpi(DpiType.Effective);
                double dpiFactor = Math.Max(1.0, dpi.X / 96.0);
                Rect bounds = new Rect(
                screen.Bounds.Left / dpiFactor,
                screen.Bounds.Top / dpiFactor,
                screen.Bounds.Width / dpiFactor,
                screen.Bounds.Height / dpiFactor);

                var outline = new Rectangle
                {
                    Width = bounds.Width,
                    Height = bounds.Height,
                    Stroke = Brushes.White,
                    StrokeThickness = 2,
                    Fill = Brushes.Transparent,
                    IsHitTestVisible = false
                };

                sceneCanvas.Children.Add(outline);
                Canvas.SetLeft(outline, bounds.Left);
                Canvas.SetTop(outline, bounds.Top);
            }
        }

        private void RefreshMemos()
        {
            IReadOnlyList<Memo> memos = Memo.GetAllMemos();
            var aliveSet = new HashSet<Memo>(memos);

            // Remove visuals for memos that no longer exist
            foreach (Memo memo in memoVisuals.Keys.Where(item => !aliveSet.Contains(item)).ToList())
            {
                RemoveMemoVisual(memo);
            }

            foreach (Memo memo in memos)
            {
                UpdateOrCreateMemoVisual(memo);
            }

            UpdateFocusedMemo();
            UpdateStatusText();
        }

        private void RemoveMemoVisual(Memo memo)
        {
            CanvasMemoVisual visual;
            if (!memoVisuals.TryGetValue(memo, out visual))
                return;

            sceneCanvas.Children.Remove(visual.Container);
            memoVisuals.Remove(memo);
        }

        private void UpdateOrCreateMemoVisual(Memo memo)
        {
            CanvasMemoVisual visual;
            if (!memoVisuals.TryGetValue(memo, out visual))
            {
                visual = CreateMemoVisual(memo);
                memoVisuals[memo] = visual;
                sceneCanvas.Children.Add(visual.Container);
            }

            Rect bounds = memo.GetCanvasBounds();
            visual.Bounds = bounds;
            visual.Image.Source = memo.CreateDisplayBitmapSource();
            visual.Border.Width = bounds.Width;
            visual.Border.Height = bounds.Height;

            Canvas.SetLeft(visual.Container, bounds.Left);
            Canvas.SetTop(visual.Container, bounds.Top);
        }

        private CanvasMemoVisual CreateMemoVisual(Memo memo)
        {
            var image = new Image
            {
                Stretch = Stretch.Fill,
                SnapsToDevicePixels = true
            };

            var border = new Border
            {
                BorderBrush = Brushes.Transparent,
                BorderThickness = new Thickness(2),
                Background = Brushes.Transparent,
                Child = image
            };

            var container = new Grid
            {
                Focusable = false,
                Background = Brushes.Transparent,
                Tag = memo,
            };
            container.Children.Add(border);

            container.MouseLeftButtonDown += MemoContainer_MouseLeftButtonDown;
            container.MouseMove += MemoContainer_MouseMove;

            return new CanvasMemoVisual
            {
                Memo = memo,
                Container = container,
                Border = border,
                Image = image
            };
        }

        private void UpdateFocusedMemo()
        {
            Memo focusedMemo = Memo.GetFocusedMemo();
            if (focusedMemo != null)
                selectedMemo = focusedMemo;

            foreach (CanvasMemoVisual visual in memoVisuals.Values)
            {
                bool isFocused = ReferenceEquals(visual.Memo, selectedMemo);
                visual.Border.BorderBrush = isFocused ? Brushes.Lime : Brushes.Transparent;
            }
        }

        private void UpdateStatusText()
        {
            int memoCount = memoVisuals.Count;
            string memoText = selectedMemo == null ? "none" : selectedMemo.GetHashCode().ToString();
            statusText.Text = $"Drag: Pan  Wheel: Zoom  Esc: Close  Zoom: {currentZoom:F2}x  Memos: {memoCount}  Focus: {memoText}";
        }

        private void FitSceneToViewport()
        {
            if (desktopBounds.Width <= 0 || desktopBounds.Height <= 0 || ActualWidth <= 0 || ActualHeight <= 0)
            {
                ApplySceneTransform();
                return;
            }

            double padding = 80;
            double scaleX = Math.Max(MinimumZoom, (ActualWidth - padding * 2) / desktopBounds.Width);
            double scaleY = Math.Max(MinimumZoom, (ActualHeight - padding * 2) / desktopBounds.Height);
            currentZoom = ClampZoom(Math.Min(scaleX, scaleY));

            sceneMatrix = Matrix.Identity;
            sceneMatrix.Translate(-desktopBounds.Left, -desktopBounds.Top);
            sceneMatrix.Scale(currentZoom, currentZoom);

            double offsetX = (ActualWidth - desktopBounds.Width * currentZoom) / 2;
            double offsetY = (ActualHeight - desktopBounds.Height * currentZoom) / 2;
            sceneMatrix.Translate(offsetX, offsetY);
            ApplySceneTransform();
        }

        private void ApplySceneTransform()
        {
            sceneCanvas.RenderTransform = new MatrixTransform(sceneMatrix);
        }

        private void ZoomAt(System.Windows.Point pivot, double factor)
        {
            double nextZoom = ClampZoom(currentZoom * factor);
            factor = nextZoom / currentZoom;
            currentZoom = nextZoom;

            sceneMatrix.Translate(-pivot.X, -pivot.Y);
            sceneMatrix.Scale(factor, factor);
            sceneMatrix.Translate(pivot.X, pivot.Y);
            ApplySceneTransform();
            UpdateStatusText();
        }

        private double ClampZoom(double zoom)
        {
            return Math.Max(MinimumZoom, Math.Min(MaximumZoom, zoom));
        }


        #region Window Event Handlers
        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                Close();
                e.Handled = true;
                return;
            }

            CanvasMemoVisual visual;
            if (selectedMemo != null && memoVisuals.TryGetValue(selectedMemo, out visual))
            {
                HandleMemoImageKeyDown(CreateInteractionContext(visual), e);
            }
        }

        private void Window_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            System.Windows.Point pivot = e.GetPosition(rootGrid);
            double factor = e.Delta > 0 ? ZoomStep : 1.0 / ZoomStep;
            ZoomAt(pivot, factor);
            e.Handled = true;
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            isPanning = true;
            lastPanPoint = e.GetPosition(rootGrid);
            Mouse.Capture(rootGrid);
        }

        private void Window_MouseMove(object sender, MouseEventArgs e)
        {
            if (!isPanning || Mouse.LeftButton != MouseButtonState.Pressed)
                return;

            System.Windows.Point current = e.GetPosition(rootGrid);
            Vector delta = current - lastPanPoint;
            lastPanPoint = current;
            sceneMatrix.Translate(delta.X, delta.Y);
            ApplySceneTransform();
        }

        private void Window_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (!isPanning)
                return;

            isPanning = false;
            if (Mouse.Captured == rootGrid)
                Mouse.Capture(null);
        }
        #endregion


        #region Memo Event Handlers
        private void MemoContainer_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            Grid container = sender as Grid;
            if (container == null)
                return;

            Memo memo = container.Tag as Memo;
            if (memo == null)
                return;

            selectedMemo = memo;
            UpdateFocusedMemo();

            CanvasMemoVisual visual;
            if (memoVisuals.TryGetValue(memo, out visual))
            {
                HandleMemoImageMouseClick(CreateInteractionContext(visual), e);
            }

            e.Handled = true;
        }

        private void MemoContainer_MouseMove(object sender, MouseEventArgs e)
        {
            Grid container = sender as Grid;
            if (container == null)
                return;

            Memo memo = container.Tag as Memo;
            if (memo == null)
                return;

            CanvasMemoVisual visual;
            if (memoVisuals.TryGetValue(memo, out visual))
            {
                HandleMemoImageMouseMove(CreateInteractionContext(visual), e);
            }
        }
        private void HandleMemoImageMouseClick(CanvasMemoInteractionContext context, MouseButtonEventArgs e)
        {
        }

        private void HandleMemoImageMouseMove(CanvasMemoInteractionContext context, MouseEventArgs e)
        {
        }

        private void HandleMemoImageKeyDown(CanvasMemoInteractionContext context, KeyEventArgs e)
        {
        }
        #endregion


        private CanvasMemoInteractionContext CreateInteractionContext(CanvasMemoVisual visual)
        {
            return new CanvasMemoInteractionContext
            {
                Memo = visual.Memo,
                Bounds = visual.Bounds,
                BitmapSource = visual.Image.Source as BitmapSource
            };
        }


        private sealed class CanvasMemoVisual
        {
            public Memo Memo { get; set; }
            public Grid Container { get; set; }
            public Border Border { get; set; }
            public System.Windows.Controls.Image Image { get; set; }
            public Rect Bounds { get; set; }
        }
    }
}
