using SharpDX;
using SharpDX.Direct3D;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Buffer = SharpDX.Direct3D11.Buffer;
using Device = SharpDX.Direct3D11.Device;

namespace Binjyo
{
    public partial class MemoD11
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct ShaderConstants
        {
            public System.Numerics.Vector4 Sizes;
            public System.Numerics.Vector4 RenderAndFlags;
            public System.Numerics.Vector4 EffectParamsA;
            public System.Numerics.Vector4 EffectParamsB;
            public System.Numerics.Vector4 InverseRow0;
            public System.Numerics.Vector4 InverseRow1;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct PointInt
        {
            public int X;
            public int Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct SizeInt
        {
            public int Width;
            public int Height;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct BlendFunction
        {
            public byte BlendOp;
            public byte BlendFlags;
            public byte SourceConstantAlpha;
            public byte AlphaFormat;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct BitmapInfo
        {
            public BitmapInfoHeader bmiHeader;
            public uint bmiColors;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct BitmapInfoHeader
        {
            public uint biSize;
            public int biWidth;
            public int biHeight;
            public ushort biPlanes;
            public ushort biBitCount;
            public uint biCompression;
            public uint biSizeImage;
            public int biXPelsPerMeter;
            public int biYPelsPerMeter;
            public uint biClrUsed;
            public uint biClrImportant;
        }

        private const int AC_SRC_OVER = 0x00;
        private const int AC_SRC_ALPHA = 0x01;
        private const int ULW_ALPHA = 0x00000002;
        private const uint BI_RGB = 0;
        private const uint DIB_RGB_COLORS = 0;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr GetDC(IntPtr hWnd);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDc);

        [DllImport("gdi32.dll", SetLastError = true)]
        private static extern IntPtr CreateCompatibleDC(IntPtr hDc);

        [DllImport("gdi32.dll", SetLastError = true)]
        private static extern bool DeleteDC(IntPtr hDc);

        [DllImport("gdi32.dll", SetLastError = true)]
        private static extern IntPtr SelectObject(IntPtr hDc, IntPtr hObject);

        [DllImport("gdi32.dll", SetLastError = true)]
        private static extern bool DeleteObject(IntPtr hObject);

        [DllImport("gdi32.dll", SetLastError = true)]
        private static extern IntPtr CreateDIBSection(
            IntPtr hDc,
            ref BitmapInfo pbmi,
            uint iUsage,
            out IntPtr ppvBits,
            IntPtr hSection,
            uint dwOffset);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UpdateLayeredWindow(
            IntPtr hWnd,
            IntPtr hdcDst,
            ref PointInt pptDst,
            ref SizeInt psize,
            IntPtr hdcSrc,
            ref PointInt pptSrc,
            int crKey,
            ref BlendFunction pblend,
            int dwFlags);

        #region ======== Render State ========

        private Device d3dDevice;
        private DeviceContext deviceContext;
        private Texture2D sourceTexture;
        private ShaderResourceView sourceTextureView;
        private Texture2D overlayTexture;
        private ShaderResourceView overlayTextureView;
        private Texture2D renderTargetTexture;
        private RenderTargetView renderTargetView;
        private Texture2D stagingTexture;
        private VertexShader vertexShader;
        private PixelShader pixelShader;
        private SamplerState samplerState;
        private Buffer constantBuffer;
        private ShaderConstants shaderConstants;
        private byte[] layeredPixelBuffer;
        private Rectangle currentHostBounds;
        private double renderContentOffsetX;
        private double renderContentOffsetY;
        private double focusHighlightOpacity;
        private double persistentHighlightOpacity;
        private double flashHighlightOpacity;
        private bool isFlashHighlightAnimating;
        private double flashHighlightElapsedSeconds;
        private HSVWheelWindow hsvWheelWindow;
        private int renderWidth;
        private int renderHeight;
        private bool isGraphicsReady;
        private bool isRendering;
        private bool isRenderRequested;
        private bool isDrawingOverlayDirty = true;

        #endregion

        #region ======== Graphics ========

        /// <summary>
        /// Update the layered host bounds and content offsets from the current scene item bounds.
        /// </summary>
        private void UpdateRenderHostLayout()
        {
            double displayLeft = Item.Left + evadeOffsetX;
            double displayTop = Item.Top + evadeOffsetY;
            currentHostBounds = new Rectangle(
                (int)Math.Round(displayLeft),
                (int)Math.Round(displayTop),
                Math.Max(1, (int)Math.Ceiling(Item.Width)),
                Math.Max(1, (int)Math.Ceiling(Item.Height)));

            renderContentOffsetX = displayLeft - currentHostBounds.Left;
            renderContentOffsetY = displayTop - currentHostBounds.Top;
        }

        /// <summary>
        /// Build the D3D11 pipeline used to render the memo into an offscreen surface.
        /// </summary>
        private void InitializeGraphics()
        {
            if (isGraphicsReady || !IsHandleCreated)
                return;

            d3dDevice = DX11.SharedDevice;
            deviceContext = DX11.SharedImmediateContext;
            int initialWidth = Math.Max(1, currentHostBounds.Width > 0 ? currentHostBounds.Width : Width);
            int initialHeight = Math.Max(1, currentHostBounds.Height > 0 ? currentHostBounds.Height : Height);
            ResetRenderTargets(initialWidth, initialHeight);
            CreateShadersAndState();
            isGraphicsReady = true;

            UploadSourceBitmap();
            UploadDrawingOverlayBitmap();
        }

        /// <summary>
        /// Create or recreate the offscreen render target and CPU-readable staging texture.
        /// </summary>
        private void ResetRenderTargets(int width, int height)
        {
            renderWidth = width;
            renderHeight = height;
            layeredPixelBuffer = new byte[width * height * 4];

            renderTargetView?.Dispose();
            renderTargetView = null;
            renderTargetTexture?.Dispose();
            renderTargetTexture = null;
            stagingTexture?.Dispose();
            stagingTexture = null;

            if (d3dDevice == null) return;

            renderTargetTexture = new Texture2D(d3dDevice, new Texture2DDescription
            {
                Width = width,
                Height = height,
                ArraySize = 1,
                MipLevels = 1,
                Format = Format.B8G8R8A8_UNorm,
                SampleDescription = new SampleDescription(1, 0),
                Usage = ResourceUsage.Default,
                BindFlags = BindFlags.RenderTarget,
                CpuAccessFlags = CpuAccessFlags.None,
                OptionFlags = ResourceOptionFlags.None
            });

            renderTargetView = new RenderTargetView(d3dDevice, renderTargetTexture);

            stagingTexture = new Texture2D(d3dDevice, new Texture2DDescription
            {
                Width = width,
                Height = height,
                ArraySize = 1,
                MipLevels = 1,
                Format = Format.B8G8R8A8_UNorm,
                SampleDescription = new SampleDescription(1, 0),
                Usage = ResourceUsage.Staging,
                BindFlags = BindFlags.None,
                CpuAccessFlags = CpuAccessFlags.Read,
                OptionFlags = ResourceOptionFlags.None
            });
        }

        /// <summary>
        /// Create the memo shaders and fixed D3D11 state objects from precompiled shader files.
        /// </summary>
        private void CreateShadersAndState()
        {
            using (var vertexShaderBytecode = DX11.LoadPS("/Resources/Effect.D11.vs"))
            using (var pixelShaderBytecode = DX11.CompileHlslResource("/Shaders/Effect.D11.hlsl", "main", "ps_4_0"))
            {
                vertexShader = new VertexShader(d3dDevice, vertexShaderBytecode);
                pixelShader = new PixelShader(d3dDevice, pixelShaderBytecode);
            }

            samplerState = new SamplerState(d3dDevice, new SamplerStateDescription
            {
                Filter = Filter.MinMagMipLinear,
                AddressU = TextureAddressMode.Clamp,
                AddressV = TextureAddressMode.Clamp,
                AddressW = TextureAddressMode.Clamp,
                ComparisonFunction = Comparison.Never,
                MinimumLod = 0,
                MaximumLod = float.MaxValue
            });

            shaderConstants = new ShaderConstants();
            constantBuffer = Buffer.Create(
                d3dDevice,
                BindFlags.ConstantBuffer,
                ref shaderConstants,
                Utilities.SizeOf<ShaderConstants>(),
                ResourceUsage.Default,
                CpuAccessFlags.None,
                ResourceOptionFlags.None,
                0);
        }

        /// <summary>
        /// Upload the scene item's bitmap into a GPU texture for sampling.
        /// </summary>
        private void UploadSourceBitmap()
        {
            if (Item.HasDynamicTextureSource)
            {
                sourceTextureView?.Dispose();
                sourceTextureView = null;
                sourceTexture?.Dispose();
                sourceTexture = null;
                return;
            }

            sourceTextureView?.Dispose();
            sourceTexture?.Dispose();

            BitmapSource source = Item.Bitmap;
            if (source.Format != PixelFormats.Bgra32)
            {
                source = new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
                source.Freeze();
            }

            int width = source.PixelWidth;
            int height = source.PixelHeight;
            int stride = width * 4;
            byte[] pixels = Effects.CopyPixels(source);

            var textureDescription = new Texture2DDescription
            {
                Width = width,
                Height = height,
                ArraySize = 1,
                MipLevels = 1,
                Format = Format.B8G8R8A8_UNorm,
                SampleDescription = new SampleDescription(1, 0),
                Usage = ResourceUsage.Immutable,
                BindFlags = BindFlags.ShaderResource,
                CpuAccessFlags = CpuAccessFlags.None,
                OptionFlags = ResourceOptionFlags.None
            };

            lock (DX11.SharedDeviceSyncRoot)
            {
                using (var stream = new DataStream(pixels.Length, true, true))
                {
                    stream.WriteRange(pixels);
                    stream.Position = 0;
                    sourceTexture = new Texture2D(
                        d3dDevice,
                        textureDescription,
                        new[] { new DataRectangle(stream.DataPointer, stride) });
                }

                sourceTextureView = new ShaderResourceView(d3dDevice, sourceTexture);
            }
        }

        /// <summary>
        /// Rasterize the current drawing document into a texture sampled alongside the source bitmap.
        /// </summary>
        private void UploadDrawingOverlayBitmap()
        {
            overlayTextureView?.Dispose();
            overlayTextureView = null;
            overlayTexture?.Dispose();
            overlayTexture = null;

            if (d3dDevice == null || Item.Bitmap == null)
                return;

            Item.DrawingDocument.ConfigureSourceSize(Item.Bitmap.PixelWidth, Item.Bitmap.PixelHeight);
            WriteableBitmap overlayBitmap = null;
            if (Item.DrawingDocument.HasVisibleObjects() || activeDrawingStroke != null)
            {
                overlayBitmap = DrawingData.RenderOverlay(
                    Item.DrawingDocument,
                    Item.Bitmap.PixelWidth,
                    Item.Bitmap.PixelHeight,
                    activeDrawingStroke);
            }

            WriteableBitmap featureOverlayBitmap = RenderFeatureOverlayBitmap(Item.Bitmap.PixelWidth, Item.Bitmap.PixelHeight);
            if (overlayBitmap == null)
                overlayBitmap = featureOverlayBitmap;
            else if (featureOverlayBitmap != null)
                overlayBitmap = DrawingData.Composite(overlayBitmap, featureOverlayBitmap);

            if (overlayBitmap == null)
            {
                isDrawingOverlayDirty = false;
                return;
            }

            BitmapSource uploadBitmap = overlayBitmap;
            if (uploadBitmap.Format != PixelFormats.Bgra32)
            {
                uploadBitmap = new FormatConvertedBitmap(uploadBitmap, PixelFormats.Bgra32, null, 0);
                uploadBitmap.Freeze();
            }

            int width = uploadBitmap.PixelWidth;
            int height = uploadBitmap.PixelHeight;
            int stride = width * 4;
            byte[] pixels = Effects.CopyPixels(uploadBitmap);

            var textureDescription = new Texture2DDescription
            {
                Width = width,
                Height = height,
                ArraySize = 1,
                MipLevels = 1,
                Format = Format.B8G8R8A8_UNorm,
                SampleDescription = new SampleDescription(1, 0),
                Usage = ResourceUsage.Immutable,
                BindFlags = BindFlags.ShaderResource,
                CpuAccessFlags = CpuAccessFlags.None,
                OptionFlags = ResourceOptionFlags.None
            };

            lock (DX11.SharedDeviceSyncRoot)
            {
                using (var stream = new DataStream(pixels.Length, true, true))
                {
                    stream.WriteRange(pixels);
                    stream.Position = 0;
                    overlayTexture = new Texture2D(
                        d3dDevice,
                        textureDescription,
                        new[] { new DataRectangle(stream.DataPointer, stride) });
                }

                overlayTextureView = new ShaderResourceView(d3dDevice, overlayTexture);
            }
            isDrawingOverlayDirty = false;
        }

        /// <summary>
        /// Mark the drawing overlay texture dirty after any document change.
        /// </summary>
        private void InvalidateDrawingOverlay()
        {
            isDrawingOverlayDirty = true;
        }

        /// <summary>
        /// Render the current scene item state, read it back to CPU memory, and push it into the layered window.
        /// </summary>
        private void RenderSceneItem()
        {
            if (!isGraphicsReady || renderTargetView == null || stagingTexture == null)
                return;
            if (isDrawingOverlayDirty)
                UploadDrawingOverlayBitmap();
            ShaderResourceView currentSourceView = GetCurrentSourceTextureView();
            if (currentSourceView == null)
                return;

            lock (DX11.SharedDeviceSyncRoot)
            {
                UpdateShaderConstants();

                deviceContext.OutputMerger.SetRenderTargets(renderTargetView);
                deviceContext.Rasterizer.SetViewport(0, 0, Math.Max(1, renderWidth), Math.Max(1, renderHeight));
                deviceContext.ClearRenderTargetView(renderTargetView, new SharpDX.Mathematics.Interop.RawColor4(0, 0, 0, 0));
                deviceContext.InputAssembler.InputLayout = null;
                deviceContext.InputAssembler.PrimitiveTopology = PrimitiveTopology.TriangleStrip;
                deviceContext.VertexShader.Set(vertexShader);
                deviceContext.PixelShader.Set(pixelShader);
                deviceContext.VertexShader.SetConstantBuffer(0, constantBuffer);
                deviceContext.PixelShader.SetConstantBuffer(0, constantBuffer);
                deviceContext.PixelShader.SetSampler(0, samplerState);
                deviceContext.PixelShader.SetShaderResource(0, currentSourceView);
                deviceContext.PixelShader.SetShaderResource(1, overlayTextureView);
                deviceContext.Draw(4, 0);
                deviceContext.PixelShader.SetShaderResource(0, null);
                deviceContext.PixelShader.SetShaderResource(1, null);
                deviceContext.Flush();

                deviceContext.CopyResource(renderTargetTexture, stagingTexture);
            }
            PushLayeredFrame();
        }

        private ShaderResourceView GetCurrentSourceTextureView()
        {
            if (Item.TextureSource == null)
                return sourceTextureView;

            return Item.TextureSource.TryAcquireShaderResourceView(out ShaderResourceView dynamicSourceView)
                ? dynamicSourceView
                : null;
        }

        /// <summary>
        /// Read back the rendered texture and update the native layered window.
        /// </summary>
        private void PushLayeredFrame()
        {
            lock (DX11.SharedDeviceSyncRoot)
            {
                DataBox dataBox = deviceContext.MapSubresource(stagingTexture, 0, MapMode.Read, SharpDX.Direct3D11.MapFlags.None);
                try
                {
                    int rowSize = renderWidth * 4;

                    for (int y = 0; y < renderHeight; y++)
                    {
                        IntPtr rowPointer = IntPtr.Add(dataBox.DataPointer, y * dataBox.RowPitch);
                        int rowOffset = y * rowSize;
                        Marshal.Copy(rowPointer, layeredPixelBuffer, rowOffset, rowSize);
                    }
                }
                finally
                {
                    deviceContext.UnmapSubresource(stagingTexture, 0);
                }
            }

            UpdateLayeredBitmap();
        }

        /// <summary>
        /// Upload the CPU bitmap to the layered window so Windows uses per-pixel alpha for both presentation and hit testing.
        /// </summary>
        private void UpdateLayeredBitmap()
        {
            if (!IsHandleCreated)
                return;

            IntPtr screenDc = GetDC(IntPtr.Zero);
            IntPtr memoryDc = IntPtr.Zero;
            IntPtr dib = IntPtr.Zero;
            IntPtr dibBits = IntPtr.Zero;
            IntPtr oldBitmap = IntPtr.Zero;

            try
            {
                memoryDc = CreateCompatibleDC(screenDc);
                if (memoryDc == IntPtr.Zero)
                    return;

                BitmapInfo bitmapInfo = new BitmapInfo
                {
                    bmiHeader = new BitmapInfoHeader
                    {
                        biSize = (uint)Marshal.SizeOf<BitmapInfoHeader>(),
                        biWidth = renderWidth,
                        biHeight = -renderHeight,
                        biPlanes = 1,
                        biBitCount = 32,
                        biCompression = BI_RGB
                    }
                };

                dib = CreateDIBSection(screenDc, ref bitmapInfo, DIB_RGB_COLORS, out dibBits, IntPtr.Zero, 0);
                if (dib == IntPtr.Zero || dibBits == IntPtr.Zero)
                    return;

                Marshal.Copy(layeredPixelBuffer, 0, dibBits, layeredPixelBuffer.Length);
                oldBitmap = SelectObject(memoryDc, dib);

                PointInt dstPoint = new PointInt { X = currentHostBounds.Left, Y = currentHostBounds.Top };
                SizeInt size = new SizeInt { Width = renderWidth, Height = renderHeight };
                PointInt srcPoint = new PointInt();
                BlendFunction blend = new BlendFunction
                {
                    BlendOp = AC_SRC_OVER,
                    BlendFlags = 0,
                    SourceConstantAlpha = 255,
                    AlphaFormat = AC_SRC_ALPHA
                };

                UpdateLayeredWindow(Handle, screenDc, ref dstPoint, ref size, memoryDc, ref srcPoint, 0, ref blend, ULW_ALPHA);
            }
            finally
            {
                if (oldBitmap != IntPtr.Zero)
                    SelectObject(memoryDc, oldBitmap);
                if (dib != IntPtr.Zero)
                    DeleteObject(dib);
                if (memoryDc != IntPtr.Zero)
                    DeleteDC(memoryDc);
                if (screenDc != IntPtr.Zero)
                    ReleaseDC(IntPtr.Zero, screenDc);
            }
        }

        /// <summary>
        /// Update shader parameters from the current scene item transform and effect state.
        /// </summary>
        private void UpdateShaderConstants()
        {
            double baseWidth = Item.GetBaseWidth();
            double baseHeight = Item.GetBaseHeight();
            var inverse = Item.TransformInv.Value;
            double adjustedOffsetX = inverse.OffsetX - renderContentOffsetX * inverse.M11 - renderContentOffsetY * inverse.M21;
            double adjustedOffsetY = inverse.OffsetY - renderContentOffsetX * inverse.M12 - renderContentOffsetY * inverse.M22;

            shaderConstants.Sizes = new System.Numerics.Vector4(
                (float)baseWidth,
                (float)baseHeight,
                Math.Max(1, renderWidth),
                Math.Max(1, renderHeight));
            shaderConstants.RenderAndFlags = new System.Numerics.Vector4(
                Scene.FocusedId == Id ? 1f : 0f,
                (float)Math.Max(0.0, Math.Min(1.0, FinalOpacity)),
                (float)Math.Max(0.0, Math.Min(1.0, focusHighlightOpacity)),
                0f);
            shaderConstants.EffectParamsA = new System.Numerics.Vector4(
                Item.IsEffectGray ? 1f : 0f,
                Item.IsEffectBinarize ? 1f : 0f,
                Item.PEffectBinarize,
                Item.IsEffectQuantize ? 1f : 0f);
            shaderConstants.EffectParamsB = new System.Numerics.Vector4(
                Item.PEffectQuantize,
                Item.IsEffectHuemap ? 1f : 0f,
                0f,
                0f);
            shaderConstants.InverseRow0 = new System.Numerics.Vector4((float)inverse.M11, (float)inverse.M21, (float)adjustedOffsetX, 0f);
            shaderConstants.InverseRow1 = new System.Numerics.Vector4((float)inverse.M12, (float)inverse.M22, (float)adjustedOffsetY, 0f);

            deviceContext.UpdateSubresource(ref shaderConstants, constantBuffer);
        }

        /// <summary>
        /// Dispose all Direct3D resources owned by this memo.
        /// </summary>
        private void DisposeGraphics()
        {
            isGraphicsReady = false;
            layeredPixelBuffer = null;

            renderTargetView?.Dispose();
            renderTargetView = null;
            renderTargetTexture?.Dispose();
            renderTargetTexture = null;
            stagingTexture?.Dispose();
            stagingTexture = null;
            sourceTextureView?.Dispose();
            sourceTextureView = null;
            sourceTexture?.Dispose();
            sourceTexture = null;
            overlayTextureView?.Dispose();
            overlayTextureView = null;
            overlayTexture?.Dispose();
            overlayTexture = null;
            samplerState?.Dispose();
            samplerState = null;
            constantBuffer?.Dispose();
            constantBuffer = null;
            vertexShader?.Dispose();
            vertexShader = null;
            pixelShader?.Dispose();
            pixelShader = null;
            deviceContext = null;
            d3dDevice = null;
        }

        #endregion


        #region ======== Highlight ========

        private const double PersistentHighlightTargetOpacity = 0.35;
        private const double FlashHighlightPeakOpacity = 0.5;
        private const double FlashHighlightFadeInSeconds = 0.08;
        private const double FlashHighlightFadeOutSeconds = 0.18;

        /// <summary>
        /// Keep an explicit on/off entry point for future menu hover highlighting.
        /// </summary>
        private void SetHighlight(bool on)
        {
            double nextPersistentOpacity = on ? PersistentHighlightTargetOpacity : 0.0;
            if (Math.Abs(nextPersistentOpacity - persistentHighlightOpacity) < 0.0001)
                return;

            persistentHighlightOpacity = nextPersistentOpacity;
            UpdateComposedHighlightOpacity();
        }

        /// <summary>
        /// Flash the focus highlight with a short fade-in followed by a longer fade-out.
        /// </summary>
        private void FlashHighlight()
        {
            isFlashHighlightAnimating = true;
            flashHighlightElapsedSeconds = 0.0;
            flashHighlightOpacity = 0.0;
            UpdateComposedHighlightOpacity();
        }

        /// <summary>
        /// Advance the local highlight animation inside the shared frame loop.
        /// </summary>
        private void UpdateHighlightAnimation(double deltaSeconds)
        {
            if (!isFlashHighlightAnimating || deltaSeconds <= 0)
                return;

            flashHighlightElapsedSeconds += deltaSeconds;
            double nextFlashOpacity;

            if (flashHighlightElapsedSeconds <= FlashHighlightFadeInSeconds)
            {
                nextFlashOpacity = FlashHighlightPeakOpacity
                    * (flashHighlightElapsedSeconds / FlashHighlightFadeInSeconds);
            }
            else
            {
                double fadeOutElapsed = flashHighlightElapsedSeconds - FlashHighlightFadeInSeconds;
                if (fadeOutElapsed >= FlashHighlightFadeOutSeconds)
                {
                    nextFlashOpacity = 0.0;
                    isFlashHighlightAnimating = false;
                }
                else
                {
                    nextFlashOpacity = FlashHighlightPeakOpacity
                        * (1.0 - fadeOutElapsed / FlashHighlightFadeOutSeconds);
                }
            }

            if (Math.Abs(nextFlashOpacity - flashHighlightOpacity) < 0.0001)
                return;

            flashHighlightOpacity = nextFlashOpacity;
            UpdateComposedHighlightOpacity();
        }

        /// <summary>
        /// Merge persistent and transient highlight contributions and request a redraw only when the visible value changes.
        /// </summary>
        private void UpdateComposedHighlightOpacity()
        {
            double nextOpacity = Math.Max(persistentHighlightOpacity, flashHighlightOpacity);
            if (Math.Abs(nextOpacity - focusHighlightOpacity) < 0.0001)
                return;

            focusHighlightOpacity = nextOpacity;
            RenderRequest();
        }

        #endregion


        #region ======== Render Request ========
        /// <summary>
        /// Mark the memo as dirty so the shared frame loop can submit one new layered frame.
        /// </summary>
        private void RenderRequest(bool immediate = false)
        {
            if (immediate)
            {
                RenderNowOrQueue();
                return;
            }

            isRenderRequested = true;
        }

        /// <summary>
        /// Render immediately when possible and collapse overlapping requests into one trailing frame.
        /// </summary>
        private void RenderNowOrQueue()
        {
            if (isRendering)
            {
                isRenderRequested = true;
                return;
            }

            isRendering = true;
            try
            {
                do
                {
                    isRenderRequested = false;
                    RenderSceneItem();
                }
                while (isRenderRequested);
            }
            finally
            {
                isRendering = false;
            }
        }
        #endregion
    }
}
