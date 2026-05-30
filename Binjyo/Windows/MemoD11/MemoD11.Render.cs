using SharpDX;
using SharpDX.D3DCompiler;
using SharpDX.Direct3D;
using SharpDX.Direct3D11;
using SharpDX.DirectComposition;
using SharpDX.DXGI;
using System;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Runtime.InteropServices;
using Buffer = SharpDX.Direct3D11.Buffer;
using Device = SharpDX.Direct3D11.Device;
using DCompVisual = SharpDX.DirectComposition.Visual;

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

        #region ======== Render State ========

        private Device d3dDevice;
        private SharpDX.DXGI.Device dxgiDevice;
        private Factory2 dxgiFactory;
        private SwapChain1 swapChain;
        private DeviceContext deviceContext;
        private Texture2D sourceTexture;
        private ShaderResourceView sourceTextureView;
        private RenderTargetView renderTargetView;
        private VertexShader vertexShader;
        private PixelShader pixelShader;
        private SamplerState samplerState;
        private Buffer constantBuffer;
        private SharpDX.DirectComposition.Device dcompDevice;
        private Target dcompTarget;
        private DCompVisual dcompVisual;
        private ShaderConstants shaderConstants;
        private int renderWidth;
        private int renderHeight;
        private bool isGraphicsReady;

        #endregion

        #region ======== Graphics ========

        /// <summary>
        /// Build the D3D11, DXGI, and DirectComposition pipeline for this memo window.
        /// </summary>
        private void InitializeGraphics()
        {
            if (isGraphicsReady || !IsHandleCreated)
                return;

            try
            {
                d3dDevice = new Device(DriverType.Hardware, DeviceCreationFlags.BgraSupport);
            }
            catch
            {
                d3dDevice = new Device(DriverType.Warp, DeviceCreationFlags.BgraSupport);
            }

            deviceContext = d3dDevice.ImmediateContext;
            dxgiDevice = d3dDevice.QueryInterface<SharpDX.DXGI.Device>();
            dxgiFactory = new Factory2();
            dcompDevice = new SharpDX.DirectComposition.Device(dxgiDevice);

            CreateSwapChainAndCompositionRoot(Math.Max(1, Width), Math.Max(1, Height));
            CreateShadersAndState();
            isGraphicsReady = true;
        }

        /// <summary>
        /// Create the composition swap chain and bind it to the top-level window.
        /// </summary>
        private void CreateSwapChainAndCompositionRoot(int width, int height)
        {
            renderWidth = width;
            renderHeight = height;

            var description = new SwapChainDescription1
            {
                Width = width,
                Height = height,
                Format = Format.B8G8R8A8_UNorm,
                Stereo = false,
                SampleDescription = new SampleDescription(1, 0),
                Usage = Usage.RenderTargetOutput,
                BufferCount = 2,
                Scaling = Scaling.Stretch,
                SwapEffect = SwapEffect.FlipSequential,
                AlphaMode = AlphaMode.Premultiplied,
                Flags = SwapChainFlags.None
            };

            swapChain = new SwapChain1(dxgiFactory, d3dDevice, ref description, null);
            dcompTarget = Target.FromHwnd(dcompDevice, Handle, true);
            dcompVisual = new DCompVisual(dcompDevice)
            {
                Content = swapChain,
                BitmapInterpolationMode = BitmapInterpolationMode.NearestNeighbor,
                CompositeMode = CompositeMode.SourceOver,
            };

            dcompTarget.Root = dcompVisual;
            RecreateRenderTargetView();
            dcompDevice.Commit();
        }

        /// <summary>
        /// Resize the existing swap chain buffers to match the current client size.
        /// </summary>
        private void ResizeSwapChain(int width, int height)
        {
            if (swapChain == null)
                return;

            renderWidth = width;
            renderHeight = height;

            deviceContext.OutputMerger.SetRenderTargets((RenderTargetView)null);
            renderTargetView?.Dispose();
            renderTargetView = null;

            swapChain.ResizeBuffers(2, width, height, Format.B8G8R8A8_UNorm, SwapChainFlags.None);
            RecreateRenderTargetView();
        }

        /// <summary>
        /// Recreate the render target view from the current swap chain back buffer.
        /// </summary>
        private void RecreateRenderTargetView()
        {
            renderTargetView?.Dispose();
            using (var backBuffer = swapChain.GetBackBuffer<Texture2D>(0))
            {
                renderTargetView = new RenderTargetView(d3dDevice, backBuffer);
            }
        }

        /// <summary>
        /// Create the memo shaders and fixed D3D11 state objects from precompiled shader files.
        /// </summary>
        private void CreateShadersAndState()
        {
            using (var vertexShaderBytecode = DX11.LoadPS("/Resources/Effect.D11.vs"))
            using (var pixelShaderBytecode = DX11.LoadPS("/Resources/Effect.D11.ps"))
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

        /// <summary>
        /// Render the current scene item state into the composition swap chain.
        /// </summary>
        private void RenderSceneItem()
        {
            if (!isGraphicsReady || renderTargetView == null || sourceTextureView == null)
                return;

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
            deviceContext.PixelShader.SetShaderResource(0, sourceTextureView);
            deviceContext.Draw(4, 0);
            swapChain.Present(0, PresentFlags.None);
            dcompDevice.Commit();
        }

        /// <summary>
        /// Update shader parameters from the current scene item transform and effect state.
        /// </summary>
        private void UpdateShaderConstants()
        {
            double baseWidth = Item.GetBaseWidth();
            double baseHeight = Item.GetBaseHeight();

            var inverse = Item.TransformInv.Value;

            shaderConstants.Sizes = new System.Numerics.Vector4(
                (float)baseWidth,
                (float)baseHeight,
                Math.Max(1, renderWidth),
                Math.Max(1, renderHeight));
            shaderConstants.RenderAndFlags = new System.Numerics.Vector4(
                Scene.FocusedId == Id ? 1f : 0f,
                0f,
                0f,
                0f);
            shaderConstants.EffectParamsA = new System.Numerics.Vector4(
                Item.IsEffectGray ? 1f : 0f,
                Item.IsEffectBinarize ? 1f : 0f,
                Item.PEffectBinarize,
                Item.IsEffectQuantize ? 1f : 0f);
            shaderConstants.EffectParamsB = new System.Numerics.Vector4(
                Item.PEffectQuantize,
                Item.IsEffectHuemap ? 1f : 0f,
                0f, // border thickness
                0f);
            shaderConstants.InverseRow0 = new System.Numerics.Vector4((float)inverse.M11, (float)inverse.M21, (float)inverse.OffsetX, 0f);
            shaderConstants.InverseRow1 = new System.Numerics.Vector4((float)inverse.M12, (float)inverse.M22, (float)inverse.OffsetY, 0f);

            deviceContext.UpdateSubresource(ref shaderConstants, constantBuffer);
        }

        /// <summary>
        /// Dispose all Direct3D and DirectComposition resources owned by this memo.
        /// </summary>
        private void DisposeGraphics()
        {
            isGraphicsReady = false;

            renderTargetView?.Dispose();
            renderTargetView = null;
            sourceTextureView?.Dispose();
            sourceTextureView = null;
            sourceTexture?.Dispose();
            sourceTexture = null;
            samplerState?.Dispose();
            samplerState = null;
            constantBuffer?.Dispose();
            constantBuffer = null;
            vertexShader?.Dispose();
            vertexShader = null;
            pixelShader?.Dispose();
            pixelShader = null;
            swapChain?.Dispose();
            swapChain = null;
            dcompVisual?.Dispose();
            dcompVisual = null;
            dcompTarget?.Dispose();
            dcompTarget = null;
            dcompDevice?.Dispose();
            dcompDevice = null;
            dxgiFactory?.Dispose();
            dxgiFactory = null;
            dxgiDevice?.Dispose();
            dxgiDevice = null;
            deviceContext?.Dispose();
            deviceContext = null;
            d3dDevice?.Dispose();
            d3dDevice = null;
        }

        #endregion
    }
}
