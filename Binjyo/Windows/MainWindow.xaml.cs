using System;
using System.Windows;


namespace Binjyo
{
    /// <summary>
    /// MainWindow.xaml の相互作用ロジック
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            //this.SourceInitialized += new EventHandler(OnSourceInitialized);

            Show();
            Hide();
        }

        public void Shot()
        {
            Screenshot ss = new Screenshot { Owner = this };
            ss.Shot();
        }

        public void CaptureWindowRegion()
        {
            Screenshot ss = new Screenshot { Owner = this };
            ss.Shot(ScreenshotMode.WindowCapture);
        }


        /*private void OnSourceInitialized(object sender, EventArgs e)
        {
            HwndSource source = (HwndSource)PresentationSource.FromVisual(this);
            source.AddHook(new HwndSourceHook(HandleMessages));
        }
        private IntPtr HandleMessages(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            // 0x0112 == WM_SYSCOMMAND, 'Window' command message.
            // 0xF020 == SC_MINIMIZE, command to minimize the window.
            if (msg == 0x0112 && ((int)wParam & 0xFFF0) == 0xF020)
            {
                // Cancel the minimize.
                handled = true;

                WindowState = WindowState.Maximized;
                Topmost = true;
            }

            return IntPtr.Zero;
        }*/


        [System.Runtime.InteropServices.DllImport("gdi32.dll")]
        public static extern bool DeleteObject(IntPtr hObject);

    }
}
