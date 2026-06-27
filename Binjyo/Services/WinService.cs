using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Binjyo
{
    class WinService
    {
        const int WS_EX_TRANSPARENT = 0x00000020;
        const int GWL_EXSTYLE = (-20);
        static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        const int CWP_SKIPINVISIBLE = 0x0001;
        const int CWP_SKIPDISABLED = 0x0002;
        const int CWP_SKIPTRANSPARENT = 0x0004;
        const int SW_RESTORE = 9;
        const uint SWP_NOMOVE = 0x0002;
        const uint SWP_NOSIZE = 0x0001;
        const uint SWP_NOACTIVATE = 0x0010;
        const uint SWP_SHOWWINDOW = 0x0040;
        const uint WM_MOUSEMOVE = 0x0200;
        const uint WM_LBUTTONDOWN = 0x0201;
        const uint WM_LBUTTONUP = 0x0202;
        const uint WM_MOUSEWHEEL = 0x020A;
        const uint MK_LBUTTON = 0x0001;

        [StructLayout(LayoutKind.Sequential)]
        private struct NativePoint
        {
            public int X;
            public int Y;
        }

        [DllImport("user32.dll")]
        static extern int GetWindowLong(IntPtr hwnd, int index);

        [DllImport("user32.dll")]
        static extern int SetWindowLong(IntPtr hwnd, int index, int newStyle);

        [DllImport("user32.dll")]
        static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll")]
        static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        static extern bool IsWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        static extern bool IsIconic(IntPtr hWnd);

        [DllImport("user32.dll")]
        static extern short GetAsyncKeyState(int vKey);

        [DllImport("user32.dll", SetLastError = true)]
        static extern bool SetCursorPos(int X, int Y);

        [DllImport("user32.dll", SetLastError = true)]
        static extern bool ScreenToClient(IntPtr hWnd, ref NativePoint lpPoint);

        [DllImport("user32.dll", SetLastError = true)]
        static extern IntPtr ChildWindowFromPointEx(IntPtr hwndParent, NativePoint pt, uint uFlags);

        [DllImport("user32.dll", SetLastError = true)]
        static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        public static void SetWindowExTransparent(IntPtr hwnd)
        {
            var extendedStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
            SetWindowLong(hwnd, GWL_EXSTYLE, extendedStyle | WS_EX_TRANSPARENT);
        }

        public static void BringWindowToTopmost(IntPtr hwnd)
        {
            SetWindowPos(hwnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_SHOWWINDOW);
        }

        public static bool TryActivateWindow(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero || !IsWindow(hwnd))
                return false;

            if (IsIconic(hwnd))
                ShowWindow(hwnd, SW_RESTORE);

            return SetForegroundWindow(hwnd);
        }

        public static bool IsValidWindow(IntPtr hwnd)
        {
            return hwnd != IntPtr.Zero && IsWindow(hwnd);
        }

        public static bool IsKeyDown(Keys key)
        {
            return (GetAsyncKeyState((int)key) & 0x8000) != 0;
        }

        public static bool TrySetCursorPosition(int screenX, int screenY)
        {
            return SetCursorPos(screenX, screenY);
        }

        public static bool TryPostLeftMouseEvent(IntPtr rootHwnd, int screenX, int screenY, RelayMouseEventKind eventKind, bool isLeftButtonDown)
        {
            if (!IsValidWindow(rootHwnd))
                return false;

            if (!TryResolveDeepestChildWindow(rootHwnd, screenX, screenY, out IntPtr targetHwnd, out NativePoint clientPoint))
                return false;

            uint message;
            switch (eventKind)
            {
                case RelayMouseEventKind.LeftButtonDown:
                    message = WM_LBUTTONDOWN;
                    isLeftButtonDown = true;
                    break;
                case RelayMouseEventKind.LeftButtonUp:
                    message = WM_LBUTTONUP;
                    isLeftButtonDown = false;
                    break;
                default:
                    message = WM_MOUSEMOVE;
                    break;
            }

            IntPtr wParam = isLeftButtonDown ? new IntPtr(MK_LBUTTON) : IntPtr.Zero;
            IntPtr lParam = MakeLParam(clientPoint.X, clientPoint.Y);
            return PostMessage(targetHwnd, message, wParam, lParam);
        }

        public static bool TryPostMouseWheel(IntPtr rootHwnd, int screenX, int screenY, int delta)
        {
            if (!IsValidWindow(rootHwnd))
                return false;

            if (!TryResolveDeepestChildWindow(rootHwnd, screenX, screenY, out IntPtr targetHwnd, out _))
                return false;

            IntPtr wParam = new IntPtr((delta & 0xFFFF) << 16);
            IntPtr lParam = MakeLParam(screenX, screenY);
            return PostMessage(targetHwnd, WM_MOUSEWHEEL, wParam, lParam);
        }

        private static bool TryResolveDeepestChildWindow(IntPtr rootHwnd, int screenX, int screenY, out IntPtr targetHwnd, out NativePoint clientPoint)
        {
            targetHwnd = IntPtr.Zero;
            clientPoint = default;

            NativePoint screenPoint = new NativePoint { X = screenX, Y = screenY };
            IntPtr currentHwnd = rootHwnd;

            if (!TryConvertScreenToClient(currentHwnd, screenPoint, out clientPoint))
                return false;

            while (true)
            {
                IntPtr childHwnd = ChildWindowFromPointEx(
                    currentHwnd,
                    clientPoint,
                    CWP_SKIPINVISIBLE | CWP_SKIPDISABLED | CWP_SKIPTRANSPARENT);

                if (childHwnd == IntPtr.Zero || childHwnd == currentHwnd)
                    break;

                currentHwnd = childHwnd;
                if (!TryConvertScreenToClient(currentHwnd, screenPoint, out clientPoint))
                    return false;
            }

            targetHwnd = currentHwnd;
            return true;
        }

        private static bool TryConvertScreenToClient(IntPtr hwnd, NativePoint screenPoint, out NativePoint clientPoint)
        {
            clientPoint = screenPoint;
            return ScreenToClient(hwnd, ref clientPoint);
        }

        private static IntPtr MakeLParam(int low, int high)
        {
            int packed = (low & 0xFFFF) | (high << 16);
            return new IntPtr(packed);
        }
    }
}
