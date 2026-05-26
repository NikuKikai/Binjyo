using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Windows.Media.Imaging;
using Bitmap = System.Drawing.Bitmap;
using Graphics = System.Drawing.Graphics;
using RectDrawing = System.Drawing.Rectangle;
using System.Windows.Controls;
using Rectangle = System.Windows.Shapes.Rectangle;
using Screen = System.Windows.Forms.Screen;
using Rect = System.Windows.Rect;
using Brushes = System.Windows.Media.Brushes;


namespace Binjyo
{
    public partial class CanvasWindow
    {
        private void RenderScreens()
        {
            foreach (Screen screen in Screen.AllScreens)
            {
                double dpiFactor = screen.GetDpiFactor();
                Rect bounds = new Rect(
                    screen.Bounds.Left / dpiFactor,
                    screen.Bounds.Top / dpiFactor,
                    screen.Bounds.Width / dpiFactor,
                    screen.Bounds.Height / dpiFactor
                );

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

        internal void UpdateStatusText()
        {
            int itemCount = canvasItems.Count;
            string focusedText = selectedItemId.HasValue
                ? selectedItemId.Value.ToString("N").Substring(0, 8)
                : "none";
            statusText.Text = $"Drag: Pan  Wheel: Zoom  Esc: Close  Zoom: {currentZoom:F2}x  Items: {itemCount}  Focus: {focusedText}";
        }
    }
}
