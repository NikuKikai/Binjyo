using SharpDX.Direct3D9;
using System;
using System.Drawing;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;


namespace Binjyo
{
    public static partial class Scene
    {
        /// <summary>
        /// Render the edited scene item into an offscreen bitmap.
        /// Effects are evaluated with D3D9/ps_3_0 so WPF offscreen shader limits are avoided.
        /// Geometry transforms and opacity are still applied through the existing WPF path.
        /// </summary>
        public static WriteableBitmap RenderOffscreen(SceneItem item)
        {
            if (item == null) throw new ArgumentNullException(nameof(item));
            if (item.Bitmap == null) throw new ArgumentNullException(nameof(item.Bitmap));

            WriteableBitmap transformedSource = RenderTransform(item, item.Bitmap);
            transformedSource = RenderEffectsDX9(item, transformedSource) ?? RenderEffectsOnCpu(item, transformedSource);

            WriteableBitmap transformedOverlay = RenderDrawingOverlay(item);
            WriteableBitmap composited = transformedOverlay == null
                ? transformedSource
                : DrawingData.Composite(transformedSource, transformedOverlay);

            return RenderOpacity(item, composited);
        }

        private static WriteableBitmap RenderDrawingOverlay(SceneItem item)
        {
            if (item?.DrawingDocument == null || !item.DrawingDocument.HasVisibleObjects())
                return null;

            item.DrawingDocument.ConfigureSourceSize(item.Bitmap.PixelWidth, item.Bitmap.PixelHeight);
            WriteableBitmap overlay = DrawingData.RenderOverlay(item.DrawingDocument, item.Bitmap.PixelWidth, item.Bitmap.PixelHeight);
            return RenderTransform(item, overlay);
        }

        private static WriteableBitmap RenderEffectsDX9(SceneItem item, WriteableBitmap source = null)
        {
            source = source ?? item.Bitmap;

            try
            {
                int width = Math.Max(1, source.PixelWidth);
                int height = Math.Max(1, source.PixelHeight);
                using (var direct3D = new Direct3D())
                using (var device = DX9.CreateDevice(direct3D, width, height))
                using (var sourceTex = DX9.CreateSourceTexture(device, source))
                using (var renderTex = new Texture(device, width, height, 1, Usage.RenderTarget, Format.A8R8G8B8, Pool.Default))
                using (var renderSurf = renderTex.GetSurfaceLevel(0))
                using (var resultSurf = Surface.CreateOffscreenPlain(device, width, height, Format.A8R8G8B8, Pool.SystemMemory))
                using (var shaderBytecode = DX9.LoadPS("/Resources/Effect.ps"))
                using (var pixelShader = new PixelShader(device, shaderBytecode))
                {
                    device.SetPixelShaderConstant(0, new[] { item.IsEffectGray ? 1f : 0f, 0f, 0f, 0f });
                    device.SetPixelShaderConstant(1, new[] { item.IsEffectBinarize ? 1f : 0f, 0f, 0f, 0f });
                    device.SetPixelShaderConstant(2, new[] { item.PEffectBinarize, 0f, 0f, 0f });
                    device.SetPixelShaderConstant(3, new[] { item.IsEffectQuantize ? 1f : 0f, 0f, 0f, 0f });
                    device.SetPixelShaderConstant(4, new[] { item.PEffectQuantize, 0f, 0f, 0f });
                    device.SetPixelShaderConstant(5, new[] { item.IsEffectHuemap ? 1f : 0f, 0f, 0f, 0f });

                    DX9.RenderTextureWithShader(device, sourceTex, renderSurf, pixelShader, width, height);
                    device.GetRenderTargetData(renderSurf, resultSurf);
                    return DX9.GetWBitmapFromSurface(resultSurf, width, height);
                }
            }
            catch
            {
                return null;
            }
        }


        private static WriteableBitmap RenderEffectsOnCpu(SceneItem item, WriteableBitmap source = null)
        {
            source = source ?? item.Bitmap;
            Bitmap gdiBitmap = Effects.ConvertWBitmapToGdi(source);
            try
            {
                if (item.IsEffectGray)
                    Effects.Gray(gdiBitmap);

                if (item.IsEffectBinarize)
                    Effects.Binarize(gdiBitmap, item.PEffectBinarize);
                else if (item.IsEffectQuantize)
                    Effects.Quantize(gdiBitmap, item.PEffectQuantize);

                if (item.IsEffectHuemap)
                    Effects.Huemap(gdiBitmap);

                return Effects.ConvertGdiToWBitmap(gdiBitmap);
            }
            finally
            {
                gdiBitmap.Dispose();
            }
        }

        private static WriteableBitmap RenderTransform(SceneItem item, BitmapSource source)
        {
            TransformGroup transformGroup = new TransformGroup();
            transformGroup.Children.Add(new ScaleTransform(
                item.IsFlipX ? -item.Scale : item.Scale,
                item.IsFlipY ? -item.Scale : item.Scale));
            transformGroup.Children.Add(new RotateTransform(item.Rotation));

            Rect originalRect = new Rect(0, 0, source.PixelWidth, source.PixelHeight);
            Rect transformedRect = transformGroup.TransformBounds(originalRect);
            int targetWidth = (int)Math.Max(1, Math.Round(transformedRect.Width));
            int targetHeight = (int)Math.Max(1, Math.Round(transformedRect.Height));

            DrawingVisual drawingVisual = new DrawingVisual();
            using (DrawingContext dc = drawingVisual.RenderOpen())
            {
                dc.PushTransform(new TranslateTransform(-transformedRect.X, -transformedRect.Y));
                dc.PushTransform(transformGroup);
                dc.DrawImage(source, originalRect);
                dc.Pop();
                dc.Pop();
            }

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


        private static WriteableBitmap RenderOpacity(SceneItem item, BitmapSource source)
        {
            Rect rect = new Rect(0, 0, source.PixelWidth, source.PixelHeight);

            DrawingVisual drawingVisual = new DrawingVisual();
            using (DrawingContext dc = drawingVisual.RenderOpen())
            {
                dc.PushOpacity(item.IsOpacity ? item.Opacity : 1.0);
                dc.DrawImage(source, rect);
                dc.Pop();
            }

            RenderTargetBitmap renderTarget = new RenderTargetBitmap(
                source.PixelWidth,
                source.PixelHeight,
                96,
                96,
                PixelFormats.Pbgra32);
            renderTarget.Render(drawingVisual);
            renderTarget.Freeze();
            return new WriteableBitmap(renderTarget);
        }

    }
}
