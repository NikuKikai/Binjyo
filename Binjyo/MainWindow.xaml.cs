using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Drawing;
using System.Threading;

namespace Binjyo
{
    /// <summary>
    /// MainWindow.xaml の相互作用ロジック
    /// </summary>
    public partial class MainWindow : Window
    {
        readonly Screenshot ss;
        public MainWindow()
        {
            InitializeComponent();
            //this.SourceInitialized += new EventHandler(OnSourceInitialized);
            ss = new Screenshot();

            Show();
            ss.Owner = this;
            Hide();
        }

        public void Shot()
        {
            /*foreach(var scr in System.Windows.Forms.Screen.AllScreens)
            {
                Screenshot ss = new Screenshot();
                ss.Shot(scr);
            }*/

            
            ss.Shot();
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
