using System;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Interop;


namespace Binjyo
{
    public partial class Memo
    {
        public static void RefreshAllMemoScalingModes()
        {
            foreach (Memo memo in Application.Current.Windows.OfType<Window>().OfType<Memo>())
            {
                memo.ApplyConfiguredBitmapScalingMode();
            }
        }

        private void ApplyConfiguredBitmapScalingMode()
        {
            RenderOptions.SetBitmapScalingMode(image, Effects.GetConfiguredBitmapScalingMode());
        }


        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            var hwnd = new WindowInteropHelper(this).Handle;
            //WinService.SetWindowExTransparent(hwnd);
            WindowInteropHelper helper = new WindowInteropHelper(this);

            HwndSource source = HwndSource.FromHwnd(hwnd);
            source.AddHook(WndProc);
        }

        const int WM_SYSCOMMAND = 0x0112;
        const int SC_MINIMIZE = 0xF020;
        const int SC_MAXIMIZE = 0xF030;
        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            long command = wParam.ToInt64() & 0xfff0;
            switch (msg)
            {
                case WM_SYSCOMMAND:
                    if (command == SC_MINIMIZE || command == SC_MAXIMIZE)
                        handled = true;
                    break;
                default:
                    break;
            }
            return IntPtr.Zero;
        }

    }
}
