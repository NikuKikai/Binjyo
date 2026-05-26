using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace Binjyo
{
    public partial class CanvasWindow
    {
        private void FitSceneToViewport()
        {
            if (desktopBounds.Width <= 0 || desktopBounds.Height <= 0 || ActualWidth <= 0 || ActualHeight <= 0)
            {
                ApplySceneTransform();
                return;
            }

            double padding = 80;
            double scaleX = System.Math.Max(MinimumZoom, (ActualWidth - padding * 2) / desktopBounds.Width);
            double scaleY = System.Math.Max(MinimumZoom, (ActualHeight - padding * 2) / desktopBounds.Height);
            currentZoom = System.Math.Max(MinimumZoom, System.Math.Min(MaximumZoom, System.Math.Min(scaleX, scaleY)));

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

        private void ZoomAt(Point pivot, double factor)
        {
            double nextZoom = System.Math.Max(MinimumZoom, System.Math.Min(MaximumZoom, currentZoom * factor));
            factor = nextZoom / currentZoom;
            currentZoom = nextZoom;

            sceneMatrix.Translate(-pivot.X, -pivot.Y);
            sceneMatrix.Scale(factor, factor);
            sceneMatrix.Translate(pivot.X, pivot.Y);
            ApplySceneTransform();
            UpdateStatusText();
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                Scene.SetCanvasActive(false);
                Hide();
                e.Handled = true;
                return;
            }
        }

        private void Window_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            Point pivot = e.GetPosition(rootGrid);
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

            Point current = e.GetPosition(rootGrid);
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
    }
}
