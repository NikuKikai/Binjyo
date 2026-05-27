using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;


namespace Binjyo
{
    public static partial class Scene
    {
        /// <summary>
        /// Offscreen rendering
        /// </summary>
        public static WriteableBitmap RenderOffscreen(SceneItem item)
        {
            WriteableBitmap source = item.Bitmap;
            if (source == null) throw new ArgumentNullException(nameof(source));

            // Setup Effect
            var effect = new ImageEffect
            {
                IsGray = item.IsEffectGray ? 1 : 0,
                IsHuemap = item.IsEffectHuemap ? 1 : 0,
                IsBinarize = item.IsEffectBinarize ? 1 : 0,
                BinarizeThreshold = item.PEffectBinarize,
                IsQuantize = item.IsEffectQuantize ? 1 : 0,
                QuantizeLevels = item.PEffectQuantize,
            };

            // 创建一个内存中的隐形句柄，强行激活当前线程的 DirectX 硬件加速上下文
            HwndSourceParameters parameters = new HwndSourceParameters("GpuActivator")
            {
                WindowStyle = 0, // 无样式
                Width = 1,      // 极小尺寸，不占用实际屏幕资源
                Height = 1,
                PositionX = -20000, // 扔到屏幕视野外
                PositionY = -20000
            };

            using (HwndSource hwndSource = new HwndSource(parameters))
            {
                // Get Bounds after transform (including rotation)
                TransformGroup transformGroup = new TransformGroup();
                transformGroup.Children.Add(new ScaleTransform(
                    item.IsFlipX ? -item.Scale : item.Scale,
                    item.IsFlipY ? -item.Scale : item.Scale)
                );
                transformGroup.Children.Add(new RotateTransform(item.Rotation));

                Rect originalRect = new Rect(0, 0, source.Width, source.Height);
                Rect transformedRect = transformGroup.TransformBounds(originalRect);

                int targetWidth = (int)Math.Max(1, Math.Round(transformedRect.Width));
                int targetHeight = (int)Math.Max(1, Math.Round(transformedRect.Height));

                // Create Render pipline
                DrawingVisual drawingVisual = new DrawingVisual
                {
                    Effect = effect
                };
                using (DrawingContext dc = drawingVisual.RenderOpen())
                {
                    dc.PushOpacity(item.IsEffectTransparent ? item.PEffectTransparent / 255.0 : 1);
                    dc.PushTransform(new TranslateTransform(-transformedRect.X, -transformedRect.Y));
                    dc.PushTransform(transformGroup);
                    dc.DrawImage(source, originalRect);
                    dc.Pop();
                    dc.Pop();
                }

                // Render
                RenderTargetBitmap renderTarget = new RenderTargetBitmap(
                    targetWidth,
                    targetHeight,
                    96,
                    96,
                    PixelFormats.Pbgra32);
                renderTarget.Render(drawingVisual);
                renderTarget.Freeze();

                return new WriteableBitmap(renderTarget);
            }
        }
    }
}
