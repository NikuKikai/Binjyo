using SharpDX.Direct3D11;
using SharpDX.DXGI;
using System;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Windows;
using Windows.Graphics.Capture;
using Windows.Graphics.DirectX.Direct3D11;
using Device = SharpDX.Direct3D11.Device;

namespace Binjyo
{
    internal static class WindowCaptureInterop
    {
        private const uint DWMWA_EXTENDED_FRAME_BOUNDS = 9;
        private const uint GA_ROOT = 2;
        private static readonly Guid GraphicsCaptureItemIid = new Guid("79C3F95B-31F7-4EC2-A464-632EF5D30760");

        [StructLayout(LayoutKind.Sequential)]
        private struct NativePoint
        {
            public int X;
            public int Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct NativeRect
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [ComImport]
        [Guid("3628E81B-3CAC-4C60-B7F4-23CE0E0C3356")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IGraphicsCaptureItemInterop
        {
            [PreserveSig]
            int CreateForWindow(IntPtr window, [In] ref Guid iid, out IntPtr item);

            [PreserveSig]
            int CreateForMonitor(IntPtr monitor, [In] ref Guid iid, out IntPtr item);
        }

        [ComImport]
        [Guid("A9B3D012-3DF2-4EE3-B8D1-8695F457D3C1")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IDirect3DDxgiInterfaceAccess
        {
            IntPtr GetInterface([In] ref Guid iid);
        }

        [DllImport("user32.dll")]
        private static extern IntPtr WindowFromPoint(NativePoint point);

        [DllImport("user32.dll")]
        private static extern IntPtr GetAncestor(IntPtr hwnd, uint flags);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsWindow(IntPtr hwnd);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsWindowVisible(IntPtr hwnd);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int GetWindowText(IntPtr hwnd, System.Text.StringBuilder text, int maxCount);

        [DllImport("dwmapi.dll")]
        private static extern int DwmGetWindowAttribute(IntPtr hwnd, uint attribute, out NativeRect rect, int size);

        [DllImport("d3d11.dll", ExactSpelling = true, PreserveSig = false)]
        private static extern void CreateDirect3D11DeviceFromDXGIDevice(IntPtr dxgiDevice, out IntPtr graphicsDevice);

        [DllImport("combase.dll", ExactSpelling = true, PreserveSig = false, CharSet = CharSet.Unicode)]
        private static extern void RoGetActivationFactory(
            [MarshalAs(UnmanagedType.HString)] string activatableClassId,
            [In] ref Guid iid,
            out IntPtr factory);

        public static bool TryResolveWindowCaptureSelection(Int32Rect selectionBounds, out WindowCaptureSelection selection)
        {
            selection = null;

            if (selectionBounds.Width <= 0 || selectionBounds.Height <= 0)
                return false;

            NativePoint centerPoint = new NativePoint
            {
                X = selectionBounds.X + selectionBounds.Width / 2,
                Y = selectionBounds.Y + selectionBounds.Height / 2
            };

            IntPtr hwnd = WindowFromPoint(centerPoint);
            hwnd = GetAncestor(hwnd, GA_ROOT);
            if (!IsCaptureCandidateWindow(hwnd))
                return false;

            if (!TryGetExtendedFrameBounds(hwnd, out NativeRect windowRect))
                return false;

            Int32Rect windowBounds = new Int32Rect(
                windowRect.Left,
                windowRect.Top,
                Math.Max(0, windowRect.Right - windowRect.Left),
                Math.Max(0, windowRect.Bottom - windowRect.Top));

            Int32Rect intersectedSelection = Intersect(selectionBounds, windowBounds);
            if (intersectedSelection.Width <= 0 || intersectedSelection.Height <= 0)
                return false;

            selection = new WindowCaptureSelection
            {
                WindowHandle = hwnd,
                WindowBounds = windowBounds,
                SelectionBounds = intersectedSelection,
                OffsetX = intersectedSelection.X - windowBounds.X,
                OffsetY = intersectedSelection.Y - windowBounds.Y,
                PixelWidth = intersectedSelection.Width,
                PixelHeight = intersectedSelection.Height
            };
            return true;
        }

        public static GraphicsCaptureItem CreateItemForWindow(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero)
                throw new ArgumentException("Window handle must not be zero.", nameof(hwnd));

            Guid interopGuid = typeof(IGraphicsCaptureItemInterop).GUID;
            RoGetActivationFactory("Windows.Graphics.Capture.GraphicsCaptureItem", ref interopGuid, out IntPtr factoryPointer);
            try
            {
                IGraphicsCaptureItemInterop interop =
                    (IGraphicsCaptureItemInterop)Marshal.GetTypedObjectForIUnknown(factoryPointer, typeof(IGraphicsCaptureItemInterop));

                Guid itemGuid = GraphicsCaptureItemIid;
                int hr = interop.CreateForWindow(hwnd, ref itemGuid, out IntPtr itemPointer);
                if (hr < 0)
                    Marshal.ThrowExceptionForHR(hr);

                try
                {
                    return Marshal.GetObjectForIUnknown(itemPointer) as GraphicsCaptureItem;
                }
                finally
                {
                    if (itemPointer != IntPtr.Zero)
                        Marshal.Release(itemPointer);
                }
            }
            finally
            {
                if (factoryPointer != IntPtr.Zero)
                    Marshal.Release(factoryPointer);
            }
        }

        public static IDirect3DDevice CreateDirect3DDevice(Device device)
        {
            using (var dxgiDevice = device.QueryInterface<SharpDX.DXGI.Device>())
            {
                CreateDirect3D11DeviceFromDXGIDevice(dxgiDevice.NativePointer, out IntPtr graphicsDevicePointer);
                try
                {
                    return Marshal.GetObjectForIUnknown(graphicsDevicePointer) as IDirect3DDevice;
                }
                finally
                {
                    if (graphicsDevicePointer != IntPtr.Zero)
                        Marshal.Release(graphicsDevicePointer);
                }
            }
        }

        public static Texture2D CreateSharpDXTexture2D(IDirect3DSurface surface)
        {
            if (surface == null)
                return null;

            IntPtr surfaceUnknown = Marshal.GetIUnknownForObject(surface);
            IntPtr accessPointer = IntPtr.Zero;
            try
            {
                Guid accessGuid = typeof(IDirect3DDxgiInterfaceAccess).GUID;
                Marshal.QueryInterface(surfaceUnknown, ref accessGuid, out accessPointer);
                if (accessPointer == IntPtr.Zero)
                    return null;

                IDirect3DDxgiInterfaceAccess access =
                    (IDirect3DDxgiInterfaceAccess)Marshal.GetTypedObjectForIUnknown(accessPointer, typeof(IDirect3DDxgiInterfaceAccess));

                Guid textureGuid = typeof(Texture2D).GUID;
                IntPtr texturePointer = access.GetInterface(ref textureGuid);
                return texturePointer == IntPtr.Zero ? null : new Texture2D(texturePointer);
            }
            finally
            {
                if (accessPointer != IntPtr.Zero)
                    Marshal.Release(accessPointer);
                if (surfaceUnknown != IntPtr.Zero)
                    Marshal.Release(surfaceUnknown);
            }
        }

        private static bool IsCaptureCandidateWindow(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero || !IsWindow(hwnd) || !IsWindowVisible(hwnd))
                return false;

            if (hwnd == System.Diagnostics.Process.GetCurrentProcess().MainWindowHandle)
                return false;

            return GetWindowTitle(hwnd).Length > 0;
        }

        private static string GetWindowTitle(IntPtr hwnd)
        {
            var builder = new System.Text.StringBuilder(256);
            return GetWindowText(hwnd, builder, builder.Capacity) > 0
                ? builder.ToString()
                : string.Empty;
        }

        private static bool TryGetExtendedFrameBounds(IntPtr hwnd, out NativeRect rect)
        {
            int hr = DwmGetWindowAttribute(hwnd, DWMWA_EXTENDED_FRAME_BOUNDS, out rect, Marshal.SizeOf<NativeRect>());
            return hr >= 0;
        }

        private static Int32Rect Intersect(Int32Rect a, Int32Rect b)
        {
            int left = Math.Max(a.X, b.X);
            int top = Math.Max(a.Y, b.Y);
            int right = Math.Min(a.X + a.Width, b.X + b.Width);
            int bottom = Math.Min(a.Y + a.Height, b.Y + b.Height);
            if (right <= left || bottom <= top)
                return new Int32Rect();

            return new Int32Rect(left, top, right - left, bottom - top);
        }
    }
}
