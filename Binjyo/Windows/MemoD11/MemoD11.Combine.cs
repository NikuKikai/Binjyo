using System;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Binjyo
{
    public partial class MemoD11
    {
        private void CombineMemosAtPos(double x, double y)
        {
            var ids = Scene.GetIdsAtPos(x, y);
            if (ids.Count < 2)
                return;

            var sceneItems = ids
                .Select(id => Scene.Items[id])
                .Where(item => item != null)
                .OrderBy(item => item.FocusOrder)
                .ToList();
            if (sceneItems.Count < 2)
                return;

            var renderedItems = sceneItems
                .Select(item => new
                {
                    Bitmap = Scene.RenderOffscreen(item),
                    Left = (int)Math.Round(item.Left * item.DpiFactor),
                    Top = (int)Math.Round(item.Top * item.DpiFactor),
                    Right = (int)Math.Round((item.Left + item.Width) * item.DpiFactor),
                    Bottom = (int)Math.Round((item.Top + item.Height) * item.DpiFactor)
                })
                .ToList();

            int unionLeft = renderedItems.Min(item => item.Left);
            int unionTop = renderedItems.Min(item => item.Top);
            int unionRight = renderedItems.Max(item => item.Right);
            int unionBottom = renderedItems.Max(item => item.Bottom);
            int unionWidth = Math.Max(1, unionRight - unionLeft);
            int unionHeight = Math.Max(1, unionBottom - unionTop);

            DrawingVisual visual = new DrawingVisual();
            using (DrawingContext dc = visual.RenderOpen())
            {
                foreach (var item in renderedItems)
                {
                    dc.DrawImage(item.Bitmap, new Rect(
                        item.Left - unionLeft,
                        item.Top - unionTop,
                        item.Right - item.Left,
                        item.Bottom - item.Top));
                }
            }

            RenderTargetBitmap renderTarget = new RenderTargetBitmap(unionWidth, unionHeight, 96, 96, PixelFormats.Pbgra32);
            renderTarget.Render(visual);
            var combinedBitmap = new WriteableBitmap(renderTarget);

            SceneItem sceneItem = Scene.CreateItem(combinedBitmap, unionLeft, unionTop);
            _ = new MemoD11(sceneItem);
            CanvasWindow.CreateItem(sceneItem);
            Scene.Focus(sceneItem.Id);

            foreach (Guid id in ids)
                Scene.CloseItem(id);
        }
    }
}
