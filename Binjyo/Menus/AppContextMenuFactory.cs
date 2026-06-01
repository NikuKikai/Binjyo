using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Binjyo
{
    internal static class AppContextMenuFactory
    {
        public static ContextMenu CreateTrayContextMenu(App app, RoutedEventHandler closedHandler = null)
        {
            ContextMenu menu = new ContextMenu
            {
                FlowDirection = FlowDirection.LeftToRight
            };

            if (closedHandler != null)
                menu.Closed += closedHandler;

            menu.Items.Add(CreateTrayMenuRoot(app, includeExit: true, includeCanvasWindow: true));
            return FlattenSingleRootMenu(menu);
        }

        public static MenuItem CreateTrayMenuRoot(App app, bool includeExit = true, bool includeCanvasWindow = true)
        {
            if (app == null)
                throw new ArgumentNullException(nameof(app));

            MenuItem root = new MenuItem
            {
                Header = "Tray Menu",
                FlowDirection = FlowDirection.LeftToRight
            };

            MenuItem createItem = new MenuItem
            {
                Header = "Create",
                FlowDirection = FlowDirection.LeftToRight
            };
            createItem.Items.Add(CreateMenuItem("Screenshot", null, (s, e) => app.CreateScreenshotMemo()));
            createItem.Items.Add(CreateMenuItem("Capture", null, (s, e) => app.CreateWindowCaptureMemo()));

            MenuItem viewModeItem = new MenuItem
            {
                Header = "View mode",
                FlowDirection = FlowDirection.LeftToRight
            };

            string gestureText = FormatDisplayModeGestureText();
            MenuItem expandedItem = CreateCheckableMenuItem("Expanded", gestureText, (s, e) => app.SetViewMode(EDisplayMode.Expanded));
            MenuItem autoHideItem = CreateCheckableMenuItem("Auto Hide", null, (s, e) => app.SetViewMode(EDisplayMode.AutoHide));
            MenuItem minimizedItem = CreateCheckableMenuItem("Minimized", null, (s, e) => app.SetViewMode(EDisplayMode.Minimized));
            viewModeItem.SubmenuOpened += (s, e) =>
            {
                EDisplayMode currentMode = Scene.DisplayMode;
                expandedItem.IsChecked = currentMode == EDisplayMode.Expanded;
                autoHideItem.IsChecked = currentMode == EDisplayMode.AutoHide;
                minimizedItem.IsChecked = currentMode == EDisplayMode.Minimized;
            };
            viewModeItem.Items.Add(expandedItem);
            viewModeItem.Items.Add(autoHideItem);
            viewModeItem.Items.Add(minimizedItem);

            root.Items.Add(createItem);
            root.Items.Add(viewModeItem);
            if (includeCanvasWindow)
                root.Items.Add(CreateMenuItem("Canvas Window", null, (s, e) => app.OpenCanvasWindow()));
            root.Items.Add(CreateMenuItem("Close All", null, (s, e) => app.CloseAll()));
            root.Items.Add(CreateMenuItem("History...", null, (s, e) => app.OpenHistory()));
            root.Items.Add(CreateMenuItem("Shortcut Help", null, (s, e) => app.OpenShortcutHelp()));
            root.Items.Add(CreateMenuItem("Settings...", null, (s, e) => app.OpenSettings()));

            if (includeExit)
            {
                root.Items.Add(new Separator());
                root.Items.Add(CreateMenuItem("Exit", null, (s, e) => app.ExitApplication()));
            }

            return root;
        }

        public static MenuItem CreateMenuItem(string header, string inputGestureText, RoutedEventHandler onClick)
        {
            MenuItem item = new MenuItem
            {
                Header = header,
                InputGestureText = inputGestureText,
                FlowDirection = FlowDirection.LeftToRight
            };
            if (onClick != null)
                item.Click += onClick;
            return item;
        }

        public static MenuItem CreateCheckableMenuItem(string header, string inputGestureText, RoutedEventHandler onClick)
        {
            MenuItem item = new MenuItem
            {
                Header = header,
                InputGestureText = inputGestureText,
                IsCheckable = true,
                StaysOpenOnClick = false,
                FlowDirection = FlowDirection.LeftToRight
            };
            if (onClick != null)
                item.Click += onClick;
            return item;
        }

        public static string FormatDisplayModeGestureText()
        {
            ModifierKeys modifiers = (ModifierKeys)Properties.Settings.Default.ModifierDisplayMode;
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

        private static ContextMenu FlattenSingleRootMenu(ContextMenu menu)
        {
            if (menu.Items.Count != 1)
                return menu;

            MenuItem root = menu.Items[0] as MenuItem;
            if (root == null)
                return menu;

            menu.Items.Clear();
            while (root.Items.Count > 0)
            {
                object child = root.Items[0];
                root.Items.RemoveAt(0);
                menu.Items.Add(child);
            }
            return menu;
        }
    }
}
