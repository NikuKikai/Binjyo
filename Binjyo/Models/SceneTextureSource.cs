using SharpDX.Direct3D11;
using System;
using System.Runtime.Serialization;
using System.Windows.Media.Imaging;

namespace Binjyo
{
    [DataContract]
    public enum HistorySourceKind
    {
        [EnumMember]
        StaticImage = 0,

        [EnumMember]
        WindowCapture = 1
    }

    [DataContract]
    public sealed class SceneSourceHistoryDescriptor
    {
        [DataMember(Order = 1)]
        public HistorySourceKind Kind { get; set; } = HistorySourceKind.StaticImage;

        [DataMember(Order = 2)]
        public long WindowHandleValue { get; set; }

        [DataMember(Order = 3)]
        public int OffsetX { get; set; }

        [DataMember(Order = 4)]
        public int OffsetY { get; set; }

        [DataMember(Order = 5)]
        public int PixelWidth { get; set; }

        [DataMember(Order = 6)]
        public int PixelHeight { get; set; }
    }

    public interface ISceneTextureSource : IDisposable
    {
        int PixelWidth { get; }
        int PixelHeight { get; }
        event EventHandler SourceUpdated;

        bool TryAcquireShaderResourceView(out ShaderResourceView shaderResourceView);
        WriteableBitmap CreateBitmapSnapshot();
    }

    public interface IActivatableSceneTextureSource
    {
        bool TryActivateSourceWindow();
    }

    public interface IHistorySceneTextureSource
    {
        SceneSourceHistoryDescriptor CreateHistoryDescriptor();
    }
}
