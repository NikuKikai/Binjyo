
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Resources;

using SharpDX;
using SharpDX.Direct3D9;


namespace Binjyo
{
    internal static class DX9
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct ScreenVertex
        {
            public float X;
            public float Y;
            public float Z;
            public float Rhw;
            public float U;
            public float V;

            public ScreenVertex(float x, float y, float u, float v)
            {
                X = x;
                Y = y;
                Z = 0f;
                Rhw = 1f;
                U = u;
                V = v;
            }
        }

        public static Device CreateDevice(Direct3D direct3D, int width, int height)
        {
            PresentParameters presentParameters = new PresentParameters
            {
                Windowed = true,
                SwapEffect = SwapEffect.Discard,
                BackBufferWidth = width,
                BackBufferHeight = height,
                BackBufferFormat = Format.A8R8G8B8,
                DeviceWindowHandle = GetDesktopWindow(),
                PresentationInterval = PresentInterval.Immediate
            };

            try
            {
                return new Device(
                    direct3D,
                    0,
                    DeviceType.Hardware,
                    presentParameters.DeviceWindowHandle,
                    CreateFlags.HardwareVertexProcessing | CreateFlags.Multithreaded | CreateFlags.FpuPreserve,
                    presentParameters);
            }
            catch
            {
                return new Device(
                    direct3D,
                    0,
                    DeviceType.Hardware,
                    presentParameters.DeviceWindowHandle,
                    CreateFlags.SoftwareVertexProcessing | CreateFlags.Multithreaded | CreateFlags.FpuPreserve,
                    presentParameters);
            }
        }

        public static Texture CreateSourceTexture(Device device, BitmapSource source)
        {
            int width = source.PixelWidth;
            int height = source.PixelHeight;
            int sourceStride = width * 4;
            byte[] sourcePixels = Effects.CopyPixels(source);

            Texture texture = new Texture(device, width, height, 1, Usage.None, Format.A8R8G8B8, Pool.Managed);
            DataRectangle dataRectangle = texture.LockRectangle(0, LockFlags.None);
            try
            {
                for (int y = 0; y < height; y++)
                {
                    IntPtr destination = IntPtr.Add(dataRectangle.DataPointer, y * dataRectangle.Pitch);
                    Marshal.Copy(sourcePixels, y * sourceStride, destination, sourceStride);
                }
            }
            finally
            {
                texture.UnlockRectangle(0);
            }

            return texture;
        }

        public static void RenderTextureWithShader(Device device, Texture sourceTexture, Surface renderSurface, PixelShader pixelShader, int width, int height)
        {
            device.SetRenderTarget(0, renderSurface);
            device.Viewport = new SharpDX.Mathematics.Interop.RawViewport
            {
                X = 0,
                Y = 0,
                Width = width,
                Height = height,
                MinDepth = 0f,
                MaxDepth = 1f
            };
            device.Clear(ClearFlags.Target, new SharpDX.Mathematics.Interop.RawColorBGRA(0, 0, 0, 0), 1f, 0);

            device.SetTexture(0, sourceTexture);
            device.PixelShader = pixelShader;
            device.VertexShader = null;
            device.SetSamplerState(0, SamplerState.MinFilter, (int)TextureFilter.Linear);
            device.SetSamplerState(0, SamplerState.MagFilter, (int)TextureFilter.Linear);
            device.SetSamplerState(0, SamplerState.MipFilter, (int)TextureFilter.None);
            device.SetSamplerState(0, SamplerState.AddressU, (int)TextureAddress.Clamp);
            device.SetSamplerState(0, SamplerState.AddressV, (int)TextureAddress.Clamp);
            device.SetRenderState(RenderState.CullMode, (int)Cull.None);
            device.SetRenderState(RenderState.ZEnable, false);
            device.SetRenderState(RenderState.AlphaBlendEnable, false);
            device.VertexFormat = VertexFormat.PositionRhw | VertexFormat.Texture1;

            // D3D9 uses a half-pixel convention for screen-space quads.
            ScreenVertex[] vertices =
            {
                new ScreenVertex(-0.5f, -0.5f, 0f, 0f),
                new ScreenVertex(width - 0.5f, -0.5f, 1f, 0f),
                new ScreenVertex(-0.5f, height - 0.5f, 0f, 1f),
                new ScreenVertex(width - 0.5f, height - 0.5f, 1f, 1f)
            };

            device.BeginScene();
            try
            {
                device.DrawUserPrimitives(PrimitiveType.TriangleStrip, 2, vertices);
            }
            finally
            {
                device.SetPixelShaderConstant(0, new float[8]);
                device.EndScene();
                device.PixelShader = null;
                device.SetTexture(0, null);
            }
        }

        public static WriteableBitmap GetWBitmapFromSurface(Surface surface, int width, int height)
        {
            DataRectangle dataRectangle = surface.LockRectangle(LockFlags.ReadOnly);
            try
            {
                int targetStride = width * 4;
                byte[] pixels = new byte[targetStride * height];
                for (int y = 0; y < height; y++)
                {
                    IntPtr source = IntPtr.Add(dataRectangle.DataPointer, y * dataRectangle.Pitch);
                    Marshal.Copy(source, pixels, y * targetStride, targetStride);
                }

                WriteableBitmap bitmap = new WriteableBitmap(width, height, 96, 96, PixelFormats.Bgra32, null);
                bitmap.WritePixels(new Int32Rect(0, 0, width, height), pixels, targetStride, 0);
                bitmap.Freeze();
                return bitmap;
            }
            finally
            {
                surface.UnlockRectangle();
            }
        }


        public static ShaderBytecode LoadPS(string path)
        {
            StreamResourceInfo resourceInfo = Application.GetResourceStream(new Uri(path, UriKind.Relative));

            if (resourceInfo == null)
                throw new FileNotFoundException("Effect.ps was not found.");

            using (Stream stream = resourceInfo.Stream)
            using (MemoryStream memoryStream = new MemoryStream())
            {
                stream.CopyTo(memoryStream);
                return new ShaderBytecode(memoryStream.ToArray());
            }
        }

        [DllImport("user32.dll")]
        private static extern IntPtr GetDesktopWindow();
    }
}
