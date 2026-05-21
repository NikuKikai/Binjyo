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
        private HotKey _screenshotHotKey;
        private HotKey _displayModeHotKey;

        // single-instance
        static Mutex mutex = new Mutex(true, "{8F6F0AC4-B9A1-45fd-A8CF-72F04E6BDE8F}");
        private MainWindow mainWindow;
        private Settings settings;
        private ShortcutHelp shortcutHelp;
        private HistoryWindow historyWindow;

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

            InitShotcut();

            mainWindow = new MainWindow();
            mainWindow.Closing += MainWindow_Closing;

            _notifyIcon = new System.Windows.Forms.NotifyIcon();
            //_notifyIcon.DoubleClick += (s, args) => ShowMainWindow();
            _notifyIcon.Icon = Binjyo.Properties.Resources.icon;
            _notifyIcon.Visible = true;

            CreateContextMenu();
        }

        private void InitShotcut()
        {
            var keyScreenshot = (Key)Binjyo.Properties.Settings.Default.KeyScreenshot;
            var modifierScreenshot = (ModifierKeys)Binjyo.Properties.Settings.Default.ModifierScreenshot;
            var modifierDisplayMode = (ModifierKeys)Binjyo.Properties.Settings.Default.ModifierDisplayMode;
            if (keyScreenshot == Key.None)
            {
                keyScreenshot = Key.A;
                modifierScreenshot = ModifierKeys.Control | ModifierKeys.Alt;
                Binjyo.Properties.Settings.Default.KeyScreenshot = (int)keyScreenshot;
                Binjyo.Properties.Settings.Default.ModifierScreenshot = (int)modifierScreenshot;
                Binjyo.Properties.Settings.Default.Save();
            }
            if (modifierDisplayMode == ModifierKeys.None)
            {
                modifierDisplayMode = ModifierKeys.Control | ModifierKeys.Shift;
                Binjyo.Properties.Settings.Default.ModifierDisplayMode = (int)modifierDisplayMode;
                Binjyo.Properties.Settings.Default.Save();
            }
            OnSettingsScreenshotKeySet(keyScreenshot, modifierScreenshot);
            OnSettingsDisplayModeModifierSet(modifierDisplayMode);
        }

        private void OnSettingsScreenshotKeySet(Key key, ModifierKeys modifier)
        {
            if (_screenshotHotKey != null) _screenshotHotKey.Unregister();
            _screenshotHotKey = new HotKey(key, (Binjyo.KeyModifier)modifier, OnScreenshotHotKeyHandler);

            if (shortcutHelp != null)
                shortcutHelp.UpdateGlobalShortcut(key, modifier);
        }

        private void OnSettingsDisplayModeModifierSet(ModifierKeys modifier)
        {
            if (_displayModeHotKey != null) _displayModeHotKey.Unregister();
            _displayModeHotKey = new HotKey(Key.X, (Binjyo.KeyModifier)modifier, OnDisplayModeHotKeyHandler);

            if (shortcutHelp != null)
                shortcutHelp.UpdateDisplayModeShortcut(modifier);
        }

        private void OnScreenshotHotKeyHandler(HotKey hotKey)
        {
            mainWindow.Shot();
        }

        private void OnDisplayModeHotKeyHandler(HotKey hotKey)
        {
            Memo.CycleGlobalDisplayMode();
        }

        private void CreateContextMenu()
        {
            var menu = new System.Windows.Forms.ContextMenuStrip();
            //_notifyIcon.ContextMenuStrip.Items.Add("MainWindow...").Click += (s, e) => ShowMainWindow();

            var viewModeItem = new System.Windows.Forms.ToolStripMenuItem("View mode");
            var expandedItem = new System.Windows.Forms.ToolStripMenuItem("Expanded");
            var autoHideItem = new System.Windows.Forms.ToolStripMenuItem("Auto Hide");
            var minimizedItem = new System.Windows.Forms.ToolStripMenuItem("Minimized");

            expandedItem.Click += (s, e) => SetViewMode(MemoDisplayMode.Expanded);
            autoHideItem.Click += (s, e) => SetViewMode(MemoDisplayMode.AutoHide);
            minimizedItem.Click += (s, e) => SetViewMode(MemoDisplayMode.Minimized);
            viewModeItem.DropDownOpening += (s, e) => UpdateViewModeMenuChecks(expandedItem, autoHideItem, minimizedItem);
            viewModeItem.DropDownItems.Add(expandedItem);
            viewModeItem.DropDownItems.Add(autoHideItem);
            viewModeItem.DropDownItems.Add(minimizedItem);

            menu.Items.Add(viewModeItem);
            menu.Items.Add("Close All").Click += (s, e) => CloseAll();
            menu.Items.Add("History...").Click += (s, e) => OpenHistory();
            menu.Items.Add("Shortcut Help").Click += (s, e) => OpenShortcutHelp();
            menu.Items.Add("Settings...").Click += (s, e) => OpenSettings();
            menu.Items.Add("Exit").Click += (s, e) => ExitApplication();
            _notifyIcon.ContextMenuStrip = menu;
        }

        private void SetViewMode(MemoDisplayMode mode)
        {
            Memo.SetGlobalDisplayMode(mode);
        }

        private void UpdateViewModeMenuChecks(
            System.Windows.Forms.ToolStripMenuItem expandedItem,
            System.Windows.Forms.ToolStripMenuItem autoHideItem,
            System.Windows.Forms.ToolStripMenuItem minimizedItem)
        {
            MemoDisplayMode currentMode = Memo.GetGlobalDisplayMode();
            expandedItem.Checked = currentMode == MemoDisplayMode.Expanded;
            autoHideItem.Checked = currentMode == MemoDisplayMode.AutoHide;
            minimizedItem.Checked = currentMode == MemoDisplayMode.Minimized;
        }
        private void OpenHistory()
        {
            if (historyWindow == null)
            {
                historyWindow = new HistoryWindow();
                historyWindow.Closed += (s, e) => historyWindow = null;
            }
            historyWindow.ReloadEntries();
            historyWindow.Show();
            historyWindow.Activate();
        }
        private void OpenShortcutHelp()
        {
            if (shortcutHelp == null)
            {
                shortcutHelp = new ShortcutHelp();
                shortcutHelp.Closed += (s, e) => shortcutHelp = null;
            }
            shortcutHelp.UpdateDisplayModeShortcut((ModifierKeys)Binjyo.Properties.Settings.Default.ModifierDisplayMode);
            shortcutHelp.Show();
            shortcutHelp.Activate();
        }
        private void OpenSettings()
        {
            if (settings == null)
            {
                settings = new Settings(OnSettingsScreenshotKeySet, OnSettingsDisplayModeModifierSet);
                settings.Closed += (s, e) => settings = null;
            }
            settings.Show();
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
