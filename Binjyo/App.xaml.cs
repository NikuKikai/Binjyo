using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.ComponentModel;
using System.Windows.Input;
using System.Threading;

namespace Binjyo
{
    /// <summary>
    /// App.xaml の相互作用ロジック
    /// </summary>
    public partial class App : Application
    {
        private System.Windows.Forms.NotifyIcon _notifyIcon;
        private bool _isExit;
        private HotKey _hotKey;

        // single-instance
        static Mutex mutex = new Mutex(true, "{8F6F0AC4-B9A1-45fd-A8CF-72F04E6BDE8F}");
        private MainWindow mainWindow;
        
        protected override void OnStartup(StartupEventArgs e)
        {
            
            /*if (mutex.WaitOne(TimeSpan.Zero, true))
            {
                mutex.ReleaseMutex();
            }
            else
            {
                MainWindow.Close();
            }*/

            base.OnStartup(e);

            mainWindow = new MainWindow();
            mainWindow.Closing += MainWindow_Closing;

            // bind global hotkey
            _hotKey = new HotKey(Key.A, KeyModifier.Ctrl | KeyModifier.Alt, OnHotKeyHandler);
            _hotKey = new HotKey(Key.A, KeyModifier.Ctrl | KeyModifier.Win, OnHotKeyHandler);


            _notifyIcon = new System.Windows.Forms.NotifyIcon();
            //_notifyIcon.DoubleClick += (s, args) => ShowMainWindow();
            _notifyIcon.Icon = Binjyo.Properties.Resources.icon;
            _notifyIcon.Visible = true;

            CreateContextMenu();
            
        }

        private void OnHotKeyHandler(HotKey hotKey)
        {
            //ShowMainWindow();
            mainWindow.Shot();
            
        }

        private void CreateContextMenu()
        {
            _notifyIcon.ContextMenuStrip =
              new System.Windows.Forms.ContextMenuStrip();
            //_notifyIcon.ContextMenuStrip.Items.Add("MainWindow...").Click += (s, e) => ShowMainWindow();
            _notifyIcon.ContextMenuStrip.Items.Add("Minimize All").Click += (s, e) => MinimizeAll();
            _notifyIcon.ContextMenuStrip.Items.Add("Expand/Unlock All").Click += (s, e) => ExpandAll();
            _notifyIcon.ContextMenuStrip.Items.Add("Close All").Click += (s, e) => CloseAll();
            _notifyIcon.ContextMenuStrip.Items.Add("Exit").Click += (s, e) => ExitApplication();
        }
        private void MinimizeAll()
        {
            foreach (Window item in Application.Current.Windows)
            {
                if (item.Title == "Memo")
                {
                    ((Memo)item).minimize();
                }
            }
        }
        private void ExpandAll()
        {
            foreach (Window item in Application.Current.Windows)
            {
                if (item.Title == "Memo")
                {
                    ((Memo)item).expand();
                }
            }
        }
        private void CloseAll()
        {
            foreach (Window item in Application.Current.Windows)
            {
                if (item.Title == "Memo") item.Close();
            }
        }

        private void ExitApplication()
        {
            _isExit = true;
            foreach (Window item in Application.Current.Windows)
            {
                if (item.Title == "") continue;
                if (item.Title != "MainWindow") item.Close();
            }
            MainWindow.Close();
            _notifyIcon.Dispose();
            _notifyIcon = null;
        }

        private void MainWindow_Closing(object sender, CancelEventArgs e)
        {
            if (!_isExit)
            {
                e.Cancel = true;
                MainWindow.Hide(); // A hidden window can be shown again, a closed one not
            }
        }
    }
}
