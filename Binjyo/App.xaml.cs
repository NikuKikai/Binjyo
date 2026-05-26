using System;
using System.Data;
using System.Linq;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
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
        private ContextMenu _trayContextMenu;
        private Window _trayMenuHostWindow;

        // single-instance
        static Mutex mutex = new Mutex(true, "{8F6F0AC4-B9A1-45fd-A8CF-72F04E6BDE8F}");
        private MainWindow mainWindow;
        private Settings settings;
        private ShortcutHelp shortcutHelp;
        private HistoryWindow historyWindow;
        private CanvasWindow canvasWindow;

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

            _notifyIcon = new System.Windows.Forms.NotifyIcon
            {
                //_notifyIcon.DoubleClick += (s, args) => ShowMainWindow();
                Icon = Binjyo.Properties.Resources.icon,
                Visible = true
            };

            canvasWindow = new CanvasWindow();
            canvasWindow.Hide();

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
                modifierDisplayMode = ModifierKeys.Control | ModifierKeys.Alt;
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
            Scene.CycleDisplayMode();
        }

        private void CreateContextMenu()
        {
            _trayContextMenu = BuildTrayContextMenu();
            _notifyIcon.MouseUp += NotifyIcon_MouseUp;
        }

        private ContextMenu BuildTrayContextMenu()
        {
            ContextMenu menu = new ContextMenu
            {
                FlowDirection = FlowDirection.LeftToRight
            };
            menu.Closed += TrayContextMenu_Closed;

            MenuItem viewModeItem = new MenuItem { Header = "View mode" };
            MenuItem expandedItem = CreateCheckableMenuItem("Expanded", FormatDisplayModeGestureText(), (s, e) => SetViewMode(EDisplayMode.Expanded));
            MenuItem autoHideItem = CreateCheckableMenuItem("Auto Hide", null, (s, e) => SetViewMode(EDisplayMode.AutoHide));
            MenuItem minimizedItem = CreateCheckableMenuItem("Minimized", null, (s, e) => SetViewMode(EDisplayMode.Minimized));
            viewModeItem.SubmenuOpened += (s, e) => UpdateViewModeMenuChecks(expandedItem, autoHideItem, minimizedItem);
            viewModeItem.Items.Add(expandedItem);
            viewModeItem.Items.Add(autoHideItem);
            viewModeItem.Items.Add(minimizedItem);

            menu.Items.Add(viewModeItem);
            menu.Items.Add(CreateMenuItem("Canvas Window", null, (s, e) => OpenCanvasWindow()));
            menu.Items.Add(CreateMenuItem("Close All", null, (s, e) => CloseAll()));
            menu.Items.Add(CreateMenuItem("History...", null, (s, e) => OpenHistory()));
            menu.Items.Add(CreateMenuItem("Shortcut Help", null, (s, e) => OpenShortcutHelp()));
            menu.Items.Add(CreateMenuItem("Settings...", null, (s, e) => OpenSettings()));
            menu.Items.Add(new Separator());
            menu.Items.Add(CreateMenuItem("Exit", null, (s, e) => ExitApplication()));
            return menu;
        }

        private void NotifyIcon_MouseUp(object sender, System.Windows.Forms.MouseEventArgs e)
        {
            if (e.Button != System.Windows.Forms.MouseButtons.Right || _trayContextMenu == null)
                return;

            Dispatcher.BeginInvoke(new Action(OpenTrayContextMenu));
        }

        private void OpenTrayContextMenu()
        {
            if (_trayContextMenu == null)
                return;

            EnsureTrayMenuHostWindow();
            var mousePosition = System.Windows.Forms.Control.MousePosition;
            _trayMenuHostWindow.Left = mousePosition.X;
            _trayMenuHostWindow.Top = mousePosition.Y;
            if (!_trayMenuHostWindow.IsVisible)
                _trayMenuHostWindow.Show();
            _trayMenuHostWindow.Activate();

            _trayContextMenu.Placement = PlacementMode.RelativePoint;
            _trayContextMenu.PlacementTarget = _trayMenuHostWindow;
            _trayContextMenu.HorizontalOffset = 0;
            _trayContextMenu.VerticalOffset = 0;
            _trayContextMenu.IsOpen = false;
            _trayContextMenu.IsOpen = true;
        }

        private void EnsureTrayMenuHostWindow()
        {
            if (_trayMenuHostWindow != null)
                return;

            _trayMenuHostWindow = new Window
            {
                Width = 1,
                Height = 1,
                WindowStyle = WindowStyle.None,
                ResizeMode = ResizeMode.NoResize,
                ShowInTaskbar = false,
                ShowActivated = true,
                AllowsTransparency = true,
                Background = System.Windows.Media.Brushes.Transparent,
                Opacity = 0.01,
                Topmost = true
            };
            _trayMenuHostWindow.Deactivated += TrayMenuHostWindow_Deactivated;
            _trayMenuHostWindow.Closed += (s, e) => _trayMenuHostWindow = null;
        }

        private void TrayMenuHostWindow_Deactivated(object sender, EventArgs e)
        {
            if (_trayContextMenu != null)
                _trayContextMenu.IsOpen = false;
        }

        private void TrayContextMenu_Closed(object sender, RoutedEventArgs e)
        {
            if (_trayMenuHostWindow != null && _trayMenuHostWindow.IsVisible)
                _trayMenuHostWindow.Hide();
        }

        private static MenuItem CreateMenuItem(string header, string inputGestureText, RoutedEventHandler onClick)
        {
            MenuItem item = new MenuItem
            {
                Header = header,
                InputGestureText = inputGestureText,
                FlowDirection = FlowDirection.LeftToRight
            };
            item.Click += onClick;
            return item;
        }

        private static MenuItem CreateCheckableMenuItem(string header, string inputGestureText, RoutedEventHandler onClick)
        {
            MenuItem item = new MenuItem
            {
                Header = header,
                InputGestureText = inputGestureText,
                IsCheckable = true,
                StaysOpenOnClick = false,
                FlowDirection = FlowDirection.LeftToRight
            };
            item.Click += onClick;
            return item;
        }

        private static string FormatDisplayModeGestureText()
        {
            ModifierKeys modifiers = (ModifierKeys)Binjyo.Properties.Settings.Default.ModifierDisplayMode;
            string result = string.Empty;
            if ((modifiers & ModifierKeys.Control) == ModifierKeys.Control)
                result += "Ctrl+";
            if ((modifiers & ModifierKeys.Alt) == ModifierKeys.Alt)
                result += "Alt+";
            if ((modifiers & ModifierKeys.Shift) == ModifierKeys.Shift)
                result += "Shift+";
            if ((modifiers & ModifierKeys.Windows) == ModifierKeys.Windows)
                result += "Win+";
            return $"{result}X";
        }

        public void SetViewMode(EDisplayMode mode)
        {
            Scene.SetDisplayMode(mode);
        }

        private void UpdateViewModeMenuChecks(
            MenuItem expandedItem,
            MenuItem autoHideItem,
            MenuItem minimizedItem)
        {
            EDisplayMode currentMode = Scene.DisplayMode;
            expandedItem.IsChecked = currentMode == EDisplayMode.Expanded;
            autoHideItem.IsChecked = currentMode == EDisplayMode.AutoHide;
            minimizedItem.IsChecked = currentMode == EDisplayMode.Minimized;
        }
        public void OpenHistory()
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

        public void OpenCanvasWindow()
        {
            if (!canvasWindow.IsVisible)
                canvasWindow.Show();
            canvasWindow.Activate();
            canvasWindow.Focus();
        }
        public void OpenShortcutHelp()
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
        public void OpenSettings()
        {
            if (settings == null)
            {
                settings = new Settings(OnSettingsScreenshotKeySet, OnSettingsDisplayModeModifierSet);
                settings.Closed += (s, e) => settings = null;
            }
            settings.Show();
        }
        public void CloseAll()
        {
            Scene.ClearItems();
        }

        public void ExitApplication()
        {
            _isExit = true;
            Scene.ClearItems();

            foreach (Window item in Application.Current.Windows.Cast<Window>().ToList())
            {
                if (item.Title == "") continue;
                else if (item.Title != "MainWindow")
                    item.Close();
            }
            MainWindow.Close();
            if (_trayContextMenu != null)
                _trayContextMenu.IsOpen = false;
            _trayMenuHostWindow?.Close();
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
