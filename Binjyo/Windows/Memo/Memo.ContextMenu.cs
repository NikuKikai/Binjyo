using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Drawing;
using System.Drawing.Imaging;
using System.Reflection;
using System.Runtime.InteropServices;

using Rect = System.Drawing.Rectangle;

namespace Binjyo
{
    public partial class Memo
    {
        private void InitializeContextMenu()
        {
            memoContextMenu = new ContextMenu();
            memoContextMenu.FlowDirection = FlowDirection.LeftToRight;
            memoContextMenu.Opened += MemoContextMenu_Opened;
            memoContextMenu.Closed += MemoContextMenu_Closed;

            memoContextMenu.Items.Add(CreateMenuItem("Copy", "C / Ctrl+C", (s, e) => CopyMemoToClipboard(true)));
            memoContextMenu.Items.Add(CreateMenuItem("Copy Original", "Shift+C", (s, e) => CopyMemoToClipboard(false)));
            memoContextMenu.Items.Add(CreateMenuItem("Cut", "X / Ctrl+X", (s, e) =>
            {
                CopyMemoToClipboard(true);
                _Close();
            }));
            memoContextMenu.Items.Add(CreateMenuItem("Cut Original", "Shift+X", (s, e) =>
            {
                CopyMemoToClipboard(false);
                _Close();
            }));
            memoContextMenu.Items.Add(CreateMenuItem("Save...", "S", (s, e) => Save(true)));
            memoContextMenu.Items.Add(CreateMenuItem("Save Original...", "Shift+S", (s, e) => Save(false)));
            memoContextMenu.Items.Add(new Separator());
            memoContextMenu.Items.Add(CreateMenuItem("Reset Size", "`", (s, e) => ResetSize()));

            resizeModeMenuItem = CreateCheckableMenuItem("Resize Mode", "T", (s, e) => SetResizeMode(!isResizeMode));
            memoContextMenu.Items.Add(resizeModeMenuItem);
            editModeMenuItem = CreateMenuItem("Edit Mode", "E", (s, e) => EnterEditMode());
            memoContextMenu.Items.Add(editModeMenuItem);
            memoContextMenu.Items.Add(new Separator());

            MenuItem transformMenu = new MenuItem { Header = "Transform" };
            transformMenu.Items.Add(CreateMenuItem("Rotate 90°", "R", (s, e) => RotateMemo90()));
            transformMenu.Items.Add(CreateMenuItem("Flip Horizontally", "F", (s, e) => FlipMemoHorizontally()));
            transformMenu.Items.Add(CreateMenuItem("Flip Vertically", "Shift+F", (s, e) => FlipMemoVertically()));
            memoContextMenu.Items.Add(transformMenu);

            MenuItem effectsMenu = new MenuItem { Header = "Effects" };
            grayscaleMenuItem = CreateCheckableMenuItem("Grayscale", "G", (s, e) => ToggleGrayscale());
            hueMapMenuItem = CreateCheckableMenuItem("Hue Map", "H", (s, e) => ToggleHueMap());
            effectsMenu.Items.Add(grayscaleMenuItem);
            effectsMenu.Items.Add(hueMapMenuItem);
            effectsMenu.Items.Add(CreateBinarizeMenu());
            effectsMenu.Items.Add(CreateQuantizeMenu());
            effectsMenu.Items.Add(CreateTransparencyMenu());
            memoContextMenu.Items.Add(effectsMenu);

            memoContextMenu.Items.Add(new Separator());
            memoContextMenu.Items.Add(CreateMenuItem("Close", "Esc", (s, e) => _Close()));
            memoContextMenu.Items.Add(new Separator());
            memoContextMenu.Items.Add(CreateTrayMirrorMenu());
            ContextMenu = memoContextMenu;
        }

        private MenuItem CreateBinarizeMenu()
        {
            MenuItem menu = new MenuItem { Header = "Binarization", InputGestureText = "B + Wheel", FlowDirection = FlowDirection.LeftToRight };
            binarizeMenuItems = new Dictionary<int, MenuItem>();
            binarizeOffMenuItem = CreateCheckableMenuItem("Off", null, (s, e) => SetBinarizationEnabled(false));
            menu.Items.Add(binarizeOffMenuItem);
            menu.Items.Add(new Separator());

            foreach (int percent in binarizePercentOptions)
            {
                int localPercent = percent;
                MenuItem item = CreateCheckableMenuItem($"{localPercent}%", null, (s, e) => SetBinarizationPercent(localPercent));
                binarizeMenuItems[localPercent] = item;
                menu.Items.Add(item);
            }

            return menu;
        }

        private MenuItem CreateQuantizeMenu()
        {
            MenuItem menu = new MenuItem { Header = "Quantization", InputGestureText = "Q + Wheel", FlowDirection = FlowDirection.LeftToRight };
            quantizeMenuItems = new Dictionary<int, MenuItem>();
            quantizeOffMenuItem = CreateCheckableMenuItem("Off", null, (s, e) => SetQuantizationEnabled(false));
            menu.Items.Add(quantizeOffMenuItem);
            menu.Items.Add(new Separator());

            foreach (int level in quantizeLevelOptions)
            {
                int localLevel = level;
                MenuItem item = CreateCheckableMenuItem($"{localLevel} levels", null, (s, e) => SetQuantizationLevel(localLevel));
                quantizeMenuItems[localLevel] = item;
                menu.Items.Add(item);
            }

            return menu;
        }

        private MenuItem CreateTransparencyMenu()
        {
            MenuItem menu = new MenuItem { Header = "Transparency", InputGestureText = "O + Wheel", FlowDirection = FlowDirection.LeftToRight };
            transparencyMenuItems = new Dictionary<int, MenuItem>();
            transparencyOffMenuItem = CreateCheckableMenuItem("Off", null, (s, e) => SetTransparencyEnabled(false));
            menu.Items.Add(transparencyOffMenuItem);
            menu.Items.Add(new Separator());

            foreach (int percent in transparencyPercentOptions)
            {
                int localPercent = percent;
                MenuItem item = CreateCheckableMenuItem($"{localPercent}%", null, (s, e) => SetTransparencyPercent(localPercent));
                transparencyMenuItems[localPercent] = item;
                menu.Items.Add(item);
            }

            return menu;
        }

        private MenuItem CreateTrayMirrorMenu()
        {
            MenuItem trayMenu = new MenuItem { Header = "Tray Menu", FlowDirection = FlowDirection.LeftToRight };
            App app = (App)Application.Current;

            MenuItem viewModeMenu = new MenuItem { Header = "View mode", FlowDirection = FlowDirection.LeftToRight };
            MenuItem expandedItem = CreateCheckableMenuItem("Expanded", FormatDisplayModeGestureText(), (s, e) => app.SetViewMode(MemoDisplayMode.Expanded));
            MenuItem autoHideItem = CreateCheckableMenuItem("Auto Hide", null, (s, e) => app.SetViewMode(MemoDisplayMode.AutoHide));
            MenuItem minimizedItem = CreateCheckableMenuItem("Minimized", null, (s, e) => app.SetViewMode(MemoDisplayMode.Minimized));
            viewModeMenu.SubmenuOpened += (s, e) =>
            {
                MemoDisplayMode mode = GetGlobalDisplayMode();
                expandedItem.IsChecked = mode == MemoDisplayMode.Expanded;
                autoHideItem.IsChecked = mode == MemoDisplayMode.AutoHide;
                minimizedItem.IsChecked = mode == MemoDisplayMode.Minimized;
            };
            viewModeMenu.Items.Add(expandedItem);
            viewModeMenu.Items.Add(autoHideItem);
            viewModeMenu.Items.Add(minimizedItem);

            trayMenu.Items.Add(viewModeMenu);
            trayMenu.Items.Add(CreateMenuItem("Close All", null, (s, e) => app.CloseAll()));
            trayMenu.Items.Add(CreateMenuItem("History...", null, (s, e) => app.OpenHistory()));
            trayMenu.Items.Add(CreateMenuItem("Shortcut Help", null, (s, e) => app.OpenShortcutHelp()));
            trayMenu.Items.Add(CreateMenuItem("Settings...", null, (s, e) => app.OpenSettings()));
            trayMenu.Items.Add(CreateMenuItem("Exit", null, (s, e) => app.ExitApplication()));
            return trayMenu;
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

        private void UpdateContextMenuState()
        {
            if (memoContextMenu == null)
                return;

            bool isInteractive = !isEditMode;
            if (resizeModeMenuItem != null)
            {
                resizeModeMenuItem.IsChecked = isResizeMode;
                resizeModeMenuItem.IsEnabled = isInteractive;
            }
            if (editModeMenuItem != null)
                editModeMenuItem.IsEnabled = isInteractive && globalDisplayMode != MemoDisplayMode.Minimized;
            if (grayscaleMenuItem != null)
            {
                grayscaleMenuItem.IsChecked = isEffectGray;
                grayscaleMenuItem.IsEnabled = isInteractive;
            }
            if (hueMapMenuItem != null)
            {
                hueMapMenuItem.IsChecked = isEffectHuemap;
                hueMapMenuItem.IsEnabled = isInteractive;
            }

            if (binarizeOffMenuItem != null)
                binarizeOffMenuItem.IsChecked = !isEffectBinarize;
            if (binarizeMenuItems != null)
            {
                int currentPercent = GetClosestOption(ThresholdToPercent(pEffectBinarize), binarizePercentOptions);
                foreach (var pair in binarizeMenuItems)
                    pair.Value.IsChecked = isEffectBinarize && pair.Key == currentPercent;
            }

            if (quantizeOffMenuItem != null)
                quantizeOffMenuItem.IsChecked = !isEffectQuantize;
            if (quantizeMenuItems != null)
            {
                foreach (var pair in quantizeMenuItems)
                    pair.Value.IsChecked = isEffectQuantize && pair.Key == pEffectQuantize;
            }

            if (transparencyOffMenuItem != null)
                transparencyOffMenuItem.IsChecked = !isEffectTransparent;
            if (transparencyMenuItems != null)
            {
                int currentPercent = GetClosestOption(ThresholdToPercent(pEffectTransparent), transparencyPercentOptions);
                foreach (var pair in transparencyMenuItems)
                    pair.Value.IsChecked = isEffectTransparent && pair.Key == currentPercent;
            }
        }

        private void MemoContextMenu_Opened(object sender, RoutedEventArgs e)
        {
            ForceMenuDropAlignmentRight();
        }

        private void MemoContextMenu_Closed(object sender, RoutedEventArgs e)
        {
            RestoreMenuDropAlignment();
        }

        private void ForceMenuDropAlignmentRight()
        {
            if (menuDropAlignmentField == null)
                return;

            if (!originalMenuDropAlignment.HasValue)
                originalMenuDropAlignment = SystemParameters.MenuDropAlignment;

            menuDropAlignmentField.SetValue(null, false);
        }

        private void RestoreMenuDropAlignment()
        {
            if (menuDropAlignmentField == null || !originalMenuDropAlignment.HasValue)
                return;

            menuDropAlignmentField.SetValue(null, originalMenuDropAlignment.Value);
            originalMenuDropAlignment = null;
        }

        private static string FormatDisplayModeGestureText()
        {
            ModifierKeys modifiers = (ModifierKeys)Properties.Settings.Default.ModifierDisplayMode;
            return $"{FormatModifiers(modifiers)}X";
        }

        private static string FormatModifiers(ModifierKeys modifiers)
        {
            string result = string.Empty;
            if ((modifiers & ModifierKeys.Control) == ModifierKeys.Control)
                result += "Ctrl+";
            if ((modifiers & ModifierKeys.Alt) == ModifierKeys.Alt)
                result += "Alt+";
            if ((modifiers & ModifierKeys.Shift) == ModifierKeys.Shift)
                result += "Shift+";
            if ((modifiers & ModifierKeys.Windows) == ModifierKeys.Windows)
                result += "Win+";
            return result;
        }


    }
}
