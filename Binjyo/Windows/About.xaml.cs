using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Navigation;

namespace Binjyo
{
    /// <summary>
    /// Interaction logic for About.xaml
    /// </summary>
    public partial class About : Window
    {
        private const int GwlExStyle = -20;
        private const int WsExToolwindow = 0x00000080;
        private bool isClosing;

        public About()
        {
            InitializeComponent();
            SourceInitialized += About_SourceInitialized;
            Deactivated += About_Deactivated;
            PreviewKeyDown += About_PreviewKeyDown;
        }

        private void About_SourceInitialized(object sender, EventArgs e)
        {
            IntPtr hwnd = new WindowInteropHelper(this).Handle;
            IntPtr exStyle = GetWindowLongPtr(hwnd, GwlExStyle);
            SetWindowLongPtr(hwnd, GwlExStyle, new IntPtr(exStyle.ToInt64() | WsExToolwindow));
        }

        private void About_Deactivated(object sender, EventArgs e)
        {
            CloseSafely();
        }

        private void About_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Escape)
                return;

            e.Handled = true;
            CloseSafely();
        }

        private void GithubLink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = e.Uri.AbsoluteUri,
                UseShellExecute = true
            });
            e.Handled = true;
        }

        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr", SetLastError = true)]
        private static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", EntryPoint = "GetWindowLong", SetLastError = true)]
        private static extern IntPtr GetWindowLongPtr32(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr", SetLastError = true)]
        private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        [DllImport("user32.dll", EntryPoint = "SetWindowLong", SetLastError = true)]
        private static extern IntPtr SetWindowLongPtr32(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        private static IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex)
        {
            return IntPtr.Size == 8
                ? GetWindowLongPtr64(hWnd, nIndex)
                : GetWindowLongPtr32(hWnd, nIndex);
        }

        private static IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong)
        {
            return IntPtr.Size == 8
                ? SetWindowLongPtr64(hWnd, nIndex, dwNewLong)
                : SetWindowLongPtr32(hWnd, nIndex, dwNewLong);
        }

        private void CloseSafely()
        {
            if (isClosing)
                return;

            isClosing = true;
            Close();
        }
    }
}
