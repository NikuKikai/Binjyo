using SharpDX;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using System;
using System.IO;
using System.Windows.Forms;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Binjyo
{
    public sealed class ExplorerStaticTextureSource : ISceneTextureSource, IHistorySceneTextureSource, IOverlayBadgeSceneTextureSource, IFileDropSceneTextureSource
    {
        private readonly WriteableBitmap snapshotBitmap;
        private readonly string explorerDirectoryPath;
        private Texture2D texture;
        private ShaderResourceView textureView;
        private bool isDisposed;

        public ExplorerStaticTextureSource(WriteableBitmap snapshotBitmap, string explorerDirectoryPath)
        {
            this.snapshotBitmap = snapshotBitmap ?? throw new ArgumentNullException(nameof(snapshotBitmap));
            this.explorerDirectoryPath = explorerDirectoryPath ?? throw new ArgumentNullException(nameof(explorerDirectoryPath));
            CreateTexture();
        }

        public int PixelWidth => snapshotBitmap.PixelWidth;
        public int PixelHeight => snapshotBitmap.PixelHeight;
        public bool IsDynamic => false;
        public string OverlayBadgeText => "DIR";
        public event EventHandler SourceUpdated
        {
            add { }
            remove { }
        }

        public bool TryAcquireShaderResourceView(out ShaderResourceView shaderResourceView)
        {
            shaderResourceView = textureView;
            return shaderResourceView != null;
        }

        public WriteableBitmap CreateBitmapSnapshot()
        {
            return new WriteableBitmap(snapshotBitmap);
        }

        public SceneSourceHistoryDescriptor CreateHistoryDescriptor()
        {
            return new SceneSourceHistoryDescriptor
            {
                Kind = HistorySourceKind.ExplorerCaptureStatic,
                PixelWidth = PixelWidth,
                PixelHeight = PixelHeight,
                ExplorerDirectoryPath = explorerDirectoryPath
            };
        }

        public DragDropEffects GetPreferredDropEffect(string[] filePaths, int keyState)
        {
            return ExplorerFileDropService.GetPreferredEffect(explorerDirectoryPath, filePaths, keyState);
        }

        public bool TryHandleFileDrop(string[] filePaths, DragDropEffects effect, out string errorMessage)
        {
            return ExplorerFileDropService.TryApplyDrop(explorerDirectoryPath, filePaths, effect, out errorMessage);
        }

        public void Dispose()
        {
            if (isDisposed)
                return;

            isDisposed = true;
            textureView?.Dispose();
            textureView = null;
            texture?.Dispose();
            texture = null;
        }

        private void CreateTexture()
        {
            BitmapSource source = snapshotBitmap;
            if (source.Format != PixelFormats.Bgra32)
            {
                source = new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
                source.Freeze();
            }

            int width = source.PixelWidth;
            int height = source.PixelHeight;
            int stride = width * 4;
            byte[] pixels = Effects.CopyPixels(source);

            Texture2DDescription textureDescription = new Texture2DDescription
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
                using (DataStream stream = new DataStream(pixels.Length, true, true))
                {
                    stream.WriteRange(pixels);
                    stream.Position = 0;
                    texture = new Texture2D(
                        DX11.SharedDevice,
                        textureDescription,
                        new[] { new DataRectangle(stream.DataPointer, stride) });
                }

                textureView = new ShaderResourceView(DX11.SharedDevice, texture);
            }
        }
    }
}
