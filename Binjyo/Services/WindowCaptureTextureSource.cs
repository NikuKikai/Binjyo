using SharpDX;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Windows.Graphics;
using Windows.Graphics.Capture;
using Windows.Graphics.DirectX;
using Windows.Graphics.DirectX.Direct3D11;

namespace Binjyo
{
    public sealed class WindowCaptureTextureSource : ISceneTextureSource, IActivatableSceneTextureSource, IHistorySceneTextureSource
    {
        private readonly object syncRoot = new object();
        private readonly WindowCaptureSelection selection;
        private readonly IDirect3DDevice winrtDevice;
        private readonly GraphicsCaptureItem captureItem;
        private Direct3D11CaptureFramePool framePool;
        private GraphicsCaptureSession captureSession;
        private Texture2D croppedTexture;
        private ShaderResourceView croppedTextureView;
        private RenderTargetView croppedRenderTargetView;
        private Texture2D snapshotStagingTexture;
        private SizeInt32 lastFrameSize;
        private bool isDisposed;
        private bool hasReportedFrameFailure;

        public event EventHandler SourceUpdated;

        public int PixelWidth => selection.PixelWidth;
        public int PixelHeight => selection.PixelHeight;

        public WindowCaptureTextureSource(WindowCaptureSelection selection)
        {
            this.selection = selection ?? throw new ArgumentNullException(nameof(selection));

            if (!GraphicsCaptureSession.IsSupported())
                throw new NotSupportedException("Windows.Graphics.Capture is not supported on this system.");

            try
            {
                captureItem = WindowCaptureInterop.CreateItemForWindow(selection.WindowHandle)
                    ?? throw new InvalidOperationException("Failed to create a GraphicsCaptureItem for the selected window.");
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Failed while creating the GraphicsCaptureItem.", ex);
            }

            try
            {
                winrtDevice = WindowCaptureInterop.CreateDirect3DDevice(DX11.SharedDevice);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Failed while bridging the shared D3D11 device into a WinRT IDirect3DDevice.", ex);
            }

            try
            {
                lastFrameSize = captureItem.Size;

                framePool = Direct3D11CaptureFramePool.CreateFreeThreaded(
                    winrtDevice,
                    DirectXPixelFormat.B8G8R8A8UIntNormalized,
                    2,
                    lastFrameSize);
                framePool.FrameArrived += FramePool_FrameArrived;

                captureSession = framePool.CreateCaptureSession(captureItem);
                captureSession.IsCursorCaptureEnabled = false;
                captureSession.StartCapture();
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Failed while starting the capture session.", ex);
            }
        }

        public bool TryAcquireShaderResourceView(out ShaderResourceView shaderResourceView)
        {
            lock (syncRoot)
            {
                shaderResourceView = croppedTextureView;
                return shaderResourceView != null;
            }
        }

        public WriteableBitmap CreateBitmapSnapshot()
        {
            lock (syncRoot)
            {
                if (croppedTexture == null)
                    return null;

                EnsureSnapshotStagingTexture();
                lock (DX11.SharedDeviceSyncRoot)
                {
                    DX11.SharedImmediateContext.CopyResource(croppedTexture, snapshotStagingTexture);
                    DataBox dataBox = DX11.SharedImmediateContext.MapSubresource(snapshotStagingTexture, 0, MapMode.Read, SharpDX.Direct3D11.MapFlags.None);
                    try
                    {
                        return CreateBitmapFromMappedData(dataBox);
                    }
                    finally
                    {
                        DX11.SharedImmediateContext.UnmapSubresource(snapshotStagingTexture, 0);
                    }
                }
            }
        }

        public bool TryActivateSourceWindow()
        {
            return WinService.TryActivateWindow(selection.WindowHandle);
        }

        public SceneSourceHistoryDescriptor CreateHistoryDescriptor()
        {
            return new SceneSourceHistoryDescriptor
            {
                Kind = HistorySourceKind.WindowCapture,
                WindowHandleValue = selection.WindowHandle.ToInt64(),
                OffsetX = selection.OffsetX,
                OffsetY = selection.OffsetY,
                PixelWidth = selection.PixelWidth,
                PixelHeight = selection.PixelHeight
            };
        }

        public void Dispose()
        {
            lock (syncRoot)
            {
                if (isDisposed)
                    return;

                isDisposed = true;

                if (framePool != null)
                    framePool.FrameArrived -= FramePool_FrameArrived;

                captureSession?.Dispose();
                captureSession = null;
                framePool?.Dispose();
                framePool = null;
                croppedRenderTargetView?.Dispose();
                croppedRenderTargetView = null;
                croppedTextureView?.Dispose();
                croppedTextureView = null;
                croppedTexture?.Dispose();
                croppedTexture = null;
                snapshotStagingTexture?.Dispose();
                snapshotStagingTexture = null;
            }
        }

        private void FramePool_FrameArrived(Direct3D11CaptureFramePool sender, object args)
        {
            try
            {
                lock (syncRoot)
                {
                    if (isDisposed)
                        return;

                    using (Direct3D11CaptureFrame frame = sender.TryGetNextFrame())
                    {
                        if (frame == null)
                            return;

                        if (frame.ContentSize.Width != lastFrameSize.Width || frame.ContentSize.Height != lastFrameSize.Height)
                        {
                            lastFrameSize = frame.ContentSize;
                            framePool.Recreate(winrtDevice, DirectXPixelFormat.B8G8R8A8UIntNormalized, 2, lastFrameSize);
                        }

                        using (Texture2D frameTexture = WindowCaptureInterop.CreateSharpDXTexture2D(frame.Surface))
                        {
                            if (frameTexture == null)
                                return;

                            EnsureCroppedTexture();

                            ResourceRegion? copyRegion = GetCopyRegion(frameTexture.Description.Width, frameTexture.Description.Height);
                            lock (DX11.SharedDeviceSyncRoot)
                            {
                                if (croppedRenderTargetView != null)
                                    DX11.SharedImmediateContext.ClearRenderTargetView(croppedRenderTargetView, new SharpDX.Mathematics.Interop.RawColor4(0, 0, 0, 0));

                                if (copyRegion.HasValue)
                                {
                                    DX11.SharedImmediateContext.CopySubresourceRegion(
                                        frameTexture,
                                        0,
                                        copyRegion.Value,
                                        croppedTexture,
                                        0,
                                        0,
                                        0,
                                        0);
                                }
                            }
                        }
                    }
                }

                SourceUpdated?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                if (hasReportedFrameFailure)
                    return;

                hasReportedFrameFailure = true;
                Application.Current?.Dispatcher.BeginInvoke(new Action(() =>
                {
                    MessageBox.Show(
                        $"Capture frame processing failed.{Environment.NewLine}{Environment.NewLine}{ex}",
                        "Capture Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }));
            }
        }

        private void EnsureCroppedTexture()
        {
            if (croppedTexture != null)
                return;

            croppedTexture = new Texture2D(DX11.SharedDevice, new Texture2DDescription
            {
                Width = Math.Max(1, selection.PixelWidth),
                Height = Math.Max(1, selection.PixelHeight),
                ArraySize = 1,
                MipLevels = 1,
                Format = Format.B8G8R8A8_UNorm,
                SampleDescription = new SampleDescription(1, 0),
                Usage = ResourceUsage.Default,
                BindFlags = BindFlags.ShaderResource | BindFlags.RenderTarget,
                CpuAccessFlags = CpuAccessFlags.None,
                OptionFlags = ResourceOptionFlags.None
            });
            croppedTextureView = new ShaderResourceView(DX11.SharedDevice, croppedTexture);
            croppedRenderTargetView = new RenderTargetView(DX11.SharedDevice, croppedTexture);
        }

        private void EnsureSnapshotStagingTexture()
        {
            if (snapshotStagingTexture != null)
                return;

            snapshotStagingTexture = new Texture2D(DX11.SharedDevice, new Texture2DDescription
            {
                Width = Math.Max(1, selection.PixelWidth),
                Height = Math.Max(1, selection.PixelHeight),
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

        private ResourceRegion? GetCopyRegion(int sourceWidth, int sourceHeight)
        {
            int left = Math.Max(0, selection.OffsetX);
            int top = Math.Max(0, selection.OffsetY);
            int right = Math.Min(sourceWidth, left + selection.PixelWidth);
            int bottom = Math.Min(sourceHeight, top + selection.PixelHeight);

            if (right <= left || bottom <= top)
                return null;

            return new ResourceRegion(left, top, 0, right, bottom, 1);
        }

        private WriteableBitmap CreateBitmapFromMappedData(DataBox dataBox)
        {
            int width = Math.Max(1, selection.PixelWidth);
            int height = Math.Max(1, selection.PixelHeight);
            int rowSize = width * 4;
            byte[] pixels = new byte[rowSize * height];

            for (int y = 0; y < height; y++)
            {
                IntPtr rowPointer = IntPtr.Add(dataBox.DataPointer, y * dataBox.RowPitch);
                Marshal.Copy(rowPointer, pixels, y * rowSize, rowSize);
            }

            WriteableBitmap bitmap = new WriteableBitmap(width, height, 96, 96, PixelFormats.Bgra32, null);
            bitmap.WritePixels(new Int32Rect(0, 0, width, height), pixels, rowSize, 0);
            bitmap.Freeze();
            return new WriteableBitmap(bitmap);
        }
    }
}
