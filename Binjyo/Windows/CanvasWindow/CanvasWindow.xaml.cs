using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace Binjyo
{
    /// <summary>
    /// CanvasWindow is the centralized scene renderer.
    /// It tracks SceneItem state, renders item output once on the canvas side,
    /// and then lets memo windows consume the rendered result.
    /// </summary>
    public partial class CanvasWindow : Window
    {
        private static CanvasWindow instance;

        private const double MinimumZoom = 0.1;
        private const double MaximumZoom = 8.0;
        private const double ZoomStep = 1.1;
        private const double SceneSyncIntervalMilliseconds = 16.67; // ~60 FPS

        private readonly Dictionary<Guid, CanvasItem> canvasItems = new Dictionary<Guid, CanvasItem>();
        private readonly HashSet<Guid> dirtyRenderItemIds = new HashSet<Guid>();

        private Matrix sceneMatrix = Matrix.Identity;
        private bool isPanning = false;
        private Point lastPanPoint;
        private Rect desktopBounds;
        private double currentZoom = 1;
        private Guid? selectedItemId = null;

        public CanvasWindow()
        {
            CanvasWindow.instance = this;

            InitializeComponent();

            Left = SystemParameters.VirtualScreenLeft;
            Top = SystemParameters.VirtualScreenTop;
            Width = SystemParameters.VirtualScreenWidth;
            Height = SystemParameters.VirtualScreenHeight;

            Loaded += CanvasWindow_Loaded;
            Closed += CanvasWindow_Closed;
        }

        public static void CreateItem(SceneItem item)
        {
            var c = new CanvasItem(instance, item);
            instance.sceneCanvas.Children.Add(c.Container);
            instance.canvasItems.Add(item.Id, c);
        }

        public static void RefreshAllCanvasItemScalingModes()
        {
            if (instance == null)
                return;

            foreach (CanvasItem item in instance.canvasItems.Values)
                item.RefreshBitmapScalingMode();
        }

        internal void RemoveItem(Guid itemId)
        {
            if (!canvasItems.TryGetValue(itemId, out CanvasItem visual))
                return;

            sceneCanvas.Children.Remove(visual.Container);
            canvasItems.Remove(itemId);
            dirtyRenderItemIds.Remove(itemId);
        }

        private void CanvasWindow_Loaded(object sender, RoutedEventArgs e)
        {
            RenderScreens();
            FitSceneToViewport();
        }

        private void CanvasWindow_Closed(object sender, EventArgs e)
        {
            canvasItems.Clear();
            dirtyRenderItemIds.Clear();
        }


    }
}
