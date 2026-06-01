
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Resources;

using SharpDX;
using SharpDX.D3DCompiler;
using SharpDX.Direct3D;
using SharpDX.Direct3D11;
using SharpDX.DirectComposition;
using SharpDX.DXGI;
using Buffer = SharpDX.Direct3D11.Buffer;
using Device = SharpDX.Direct3D11.Device;
using DCompVisual = SharpDX.DirectComposition.Visual;


namespace Binjyo
{
    internal static class DX11
    {
        private static readonly object SharedDeviceLock = new object();
        private static Device sharedDevice;
        private static DeviceContext sharedImmediateContext;
        public static object SharedDeviceSyncRoot { get; } = new object();

        public static Device SharedDevice
        {
            get
            {
                EnsureSharedDevice();
                return sharedDevice;
            }
        }

        public static DeviceContext SharedImmediateContext
        {
            get
            {
                EnsureSharedDevice();
                return sharedImmediateContext;
            }
        }

        private static void EnsureSharedDevice()
        {
            if (sharedDevice != null && sharedImmediateContext != null)
                return;

            lock (SharedDeviceLock)
            {
                if (sharedDevice != null && sharedImmediateContext != null)
                    return;

                try
                {
                    sharedDevice = new Device(DriverType.Hardware, DeviceCreationFlags.BgraSupport);
                }
                catch
                {
                    sharedDevice = new Device(DriverType.Warp, DeviceCreationFlags.BgraSupport);
                }

                sharedImmediateContext = sharedDevice.ImmediateContext;
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

        public static ShaderBytecode CompileHlslResource(string path, string entryPoint, string profile)
        {
            StreamResourceInfo resourceInfo = Application.GetResourceStream(new Uri(path, UriKind.Relative));
            if (resourceInfo == null)
                throw new FileNotFoundException($"{path} was not found.");

            using (Stream stream = resourceInfo.Stream)
            using (StreamReader reader = new StreamReader(stream, Encoding.UTF8, true))
            {
                string source = reader.ReadToEnd();
                using (CompilationResult result = ShaderBytecode.Compile(
                    source,
                    entryPoint,
                    profile,
                    ShaderFlags.OptimizationLevel3,
                    EffectFlags.None))
                {
                    return result;
                }
            }
        }
    }
}
