using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;


namespace Binjyo
{
    public partial class Memo
    {
        private static readonly System.Reflection.FieldInfo menuDropAlignmentField = typeof(SystemParameters).GetField("_menuDropAlignment", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        private ContextMenu memoContextMenu = null;
        private MenuItem resizeModeMenuItem = null;
        private MenuItem editModeMenuItem = null;
        private MenuItem featurePointsMenuItem = null;
        private MenuItem combineMenuItem = null;
        private MenuItem grayscaleMenuItem = null;
        private MenuItem hueMapMenuItem = null;
        private MenuItem binarizeOffMenuItem = null;
        private Dictionary<int, MenuItem> binarizeMenuItems = null;
        private MenuItem quantizeOffMenuItem = null;
        private Dictionary<int, MenuItem> quantizeMenuItems = null;
        private MenuItem transparencyOffMenuItem = null;
        private Dictionary<int, MenuItem> transparencyMenuItems = null;
        private bool? originalMenuDropAlignment = null;
        private bool isCombinePreviewOn = false;
        private double contextMenuX = 0;
        private double contextMenuY = 0;
        private readonly int[] binarizePercentOptions = new[] { 10, 20, 30, 40, 50, 60, 70, 80, 90 };
        private readonly int[] quantizeLevelOptions = new[] { 3, 4, 5, 6, 8, 12, 16 };
        private readonly int[] transparencyPercentOptions = new[] { 10, 20, 30, 40, 50, 60, 70, 80, 90 };


        private void InitializeContextMenu()
        {
            memoContextMenu = new ContextMenu
            {
                FlowDirection = FlowDirection.LeftToRight
            };
            memoContextMenu.Opened += MemoContextMenu_Opened;
            memoContextMenu.Closed += MemoContextMenu_Closed;

            memoContextMenu.Items.Add(CreateMenuItem("Copy", "C / Ctrl+C", (s, e) => CopyToClipboard(true)));
            memoContextMenu.Items.Add(CreateMenuItem("Copy Original", "Shift+C", (s, e) => CopyToClipboard(false)));
            memoContextMenu.Items.Add(CreateMenuItem("Cut", "X / Ctrl+X", (s, e) =>
            {
                CopyToClipboard(true);
                Scene.CloseItem(Item.Id);
            }));
            memoContextMenu.Items.Add(CreateMenuItem("Cut Original", "Shift+X", (s, e) =>
            {
                CopyToClipboard(false);
                Scene.CloseItem(Item.Id);
            }));
            memoContextMenu.Items.Add(CreateMenuItem("Save...", "S", (s, e) => Save(true)));
            memoContextMenu.Items.Add(CreateMenuItem("Save Original...", "Shift+S", (s, e) => Save(false)));
            memoContextMenu.Items.Add(new Separator());
            memoContextMenu.Items.Add(CreateMenuItem("Reset Size", "`", (s, e) => Item.SetScale(1)));

            resizeModeMenuItem = CreateCheckableMenuItem("Resize Mode", "T", (s, e) => SetResizeMode(!isResizeMode));
            memoContextMenu.Items.Add(resizeModeMenuItem);
            editModeMenuItem = CreateMenuItem("Edit Mode", "E", (s, e) => EnterDrawMode());
            memoContextMenu.Items.Add(editModeMenuItem);
            featurePointsMenuItem = CreateCheckableMenuItem("Feature Points", "P", (s, e) => ToggleFeaturePoints());
            memoContextMenu.Items.Add(featurePointsMenuItem);
            combineMenuItem = CreateMenuItem("Combine", null, (s, e) => CombineMemosAtPos(contextMenuX, contextMenuY));
            combineMenuItem.MouseEnter += CombineMenuItem_MouseEnter;
            combineMenuItem.MouseLeave += CombineMenuItem_MouseLeave;
            memoContextMenu.Items.Add(combineMenuItem);
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
            memoContextMenu.Items.Add(CreateMenuItem("Close", "Esc", (s, e) => Scene.CloseItem(Item.Id)));
            memoContextMenu.Items.Add(new Separator());
            memoContextMenu.Items.Add(CreateTrayMirrorMenu());
            ContextMenu = memoContextMenu;
        }

        private MenuItem CreateBinarizeMenu()
        {
            MenuItem menu = new MenuItem { Header = "Binarization", InputGestureText = "B + Wheel", FlowDirection = FlowDirection.LeftToRight };
            binarizeMenuItems = new Dictionary<int, MenuItem>();
            binarizeOffMenuItem = CreateCheckableMenuItem("Off", null, (s, e) => SetEffectBinarize(false));
            menu.Items.Add(binarizeOffMenuItem);
            menu.Items.Add(new Separator());

            foreach (int percent in binarizePercentOptions)
            {
                int localPercent = percent;
                MenuItem item = CreateCheckableMenuItem($"{localPercent}%", null, (s, e) => SetEffectBinarize(true, localPercent));
                binarizeMenuItems[localPercent] = item;
                menu.Items.Add(item);
            }

            return menu;
        }

        private MenuItem CreateQuantizeMenu()
        {
            MenuItem menu = new MenuItem { Header = "Quantization", InputGestureText = "Q + Wheel", FlowDirection = FlowDirection.LeftToRight };
            quantizeMenuItems = new Dictionary<int, MenuItem>();
            quantizeOffMenuItem = CreateCheckableMenuItem("Off", null, (s, e) => SetEffectQuantize(false));
            menu.Items.Add(quantizeOffMenuItem);
            menu.Items.Add(new Separator());

            foreach (int level in quantizeLevelOptions)
            {
                int localLevel = level;
                MenuItem item = CreateCheckableMenuItem($"{localLevel} levels", null, (s, e) => SetEffectQuantize(true, localLevel));
                quantizeMenuItems[localLevel] = item;
                menu.Items.Add(item);
            }

            return menu;
        }

        private MenuItem CreateTransparencyMenu()
        {
            MenuItem menu = new MenuItem { Header = "Opacity", InputGestureText = "O + Wheel", FlowDirection = FlowDirection.LeftToRight };
            transparencyMenuItems = new Dictionary<int, MenuItem>();
            transparencyOffMenuItem = CreateCheckableMenuItem("Off", null, (s, e) => SetEffectTransparent(false));
            menu.Items.Add(transparencyOffMenuItem);
            menu.Items.Add(new Separator());

            foreach (int percent in transparencyPercentOptions)
            {
                int localPercent = percent;
                MenuItem item = CreateCheckableMenuItem($"{localPercent}%", null, (s, e) => SetEffectTransparent(true, localPercent));
                transparencyMenuItems[localPercent] = item;
                menu.Items.Add(item);
            }

            return menu;
        }

        private MenuItem CreateTrayMirrorMenu()
        {
            MenuItem trayMenu = new MenuItem { Header = "Tray Menu", FlowDirection = FlowDirection.LeftToRight };
            App app = (App)Application.Current;

            var viewModeGestureText = $"{FormatModifiers((ModifierKeys)Properties.Settings.Default.ModifierDisplayMode)}X";
            MenuItem viewModeMenu = new MenuItem { Header = "View mode", FlowDirection = FlowDirection.LeftToRight };
            MenuItem expandedItem = CreateCheckableMenuItem("Expanded", viewModeGestureText, (s, e) => app.SetViewMode(EDisplayMode.Expanded));
            MenuItem autoHideItem = CreateCheckableMenuItem("Auto Hide", viewModeGestureText, (s, e) => app.SetViewMode(EDisplayMode.AutoHide));
            MenuItem minimizedItem = CreateCheckableMenuItem("Minimized", viewModeGestureText, (s, e) => app.SetViewMode(EDisplayMode.Minimized));
            viewModeMenu.SubmenuOpened += (s, e) =>
            {
                EDisplayMode mode = Scene.DisplayMode;
                expandedItem.IsChecked = mode == EDisplayMode.Expanded;
                autoHideItem.IsChecked = mode == EDisplayMode.AutoHide;
                minimizedItem.IsChecked = mode == EDisplayMode.Minimized;
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

        private void UpdateContextMenu()
        {
            contextMenuX = System.Windows.Forms.Control.MousePosition.X;
            contextMenuY = System.Windows.Forms.Control.MousePosition.Y;

            if (memoContextMenu == null)
                return;

            bool isInteractive = !isDrawMode;
            if (resizeModeMenuItem != null)
            {
                resizeModeMenuItem.IsChecked = isResizeMode;
                resizeModeMenuItem.IsEnabled = isInteractive;
            }
            if (editModeMenuItem != null)
                editModeMenuItem.IsEnabled = isInteractive && Scene.DisplayMode != EDisplayMode.Minimized;
            if (featurePointsMenuItem != null)
            {
                featurePointsMenuItem.IsChecked = isFeaturePointModeEnabled;
                featurePointsMenuItem.IsEnabled = Scene.DisplayMode != EDisplayMode.Minimized;
            }
            if (combineMenuItem != null)
            {
                combineMenuItem.IsEnabled = GetMemosAtContextMenuPoint().Count >= 2;
            }
            if (grayscaleMenuItem != null)
            {
                grayscaleMenuItem.IsChecked = Item.IsEffectGray;
                grayscaleMenuItem.IsEnabled = isInteractive;
            }
            if (hueMapMenuItem != null)
            {
                hueMapMenuItem.IsChecked = Item.IsEffectHuemap;
                hueMapMenuItem.IsEnabled = isInteractive;
            }

            if (binarizeOffMenuItem != null)
                binarizeOffMenuItem.IsChecked = !Item.IsEffectBinarize;
            if (binarizeMenuItems != null)
            {
                int currentPercent = GetClosestOption(ThresholdToPercent(Item.PEffectBinarize), binarizePercentOptions);
                foreach (var pair in binarizeMenuItems)
                    pair.Value.IsChecked = Item.IsEffectBinarize && pair.Key == currentPercent;
            }

            if (quantizeOffMenuItem != null)
                quantizeOffMenuItem.IsChecked = !Item.IsEffectQuantize;
            if (quantizeMenuItems != null)
            {
                foreach (var pair in quantizeMenuItems)
                    pair.Value.IsChecked = Item.IsEffectQuantize && pair.Key == Item.PEffectQuantize;
            }

            if (transparencyOffMenuItem != null)
                transparencyOffMenuItem.IsChecked = !Item.IsEffectTransparent;
            if (transparencyMenuItems != null)
            {
                int currentPercent = GetClosestOption(ThresholdToPercent(Item.PEffectTransparent), transparencyPercentOptions);
                foreach (var pair in transparencyMenuItems)
                    pair.Value.IsChecked = Item.IsEffectTransparent && pair.Key == currentPercent;
            }
        }

        private void MemoContextMenu_Opened(object sender, RoutedEventArgs e)
        {
            // Force Menu Drop Alignment Right
            if (menuDropAlignmentField == null) return;

            if (!originalMenuDropAlignment.HasValue)
                originalMenuDropAlignment = SystemParameters.MenuDropAlignment;

            menuDropAlignmentField.SetValue(null, false);
        }

        private void MemoContextMenu_Closed(object sender, RoutedEventArgs e)
        {
            CombinePreview(); // clear

            // Restore Menu Drop Alignment
            if (menuDropAlignmentField == null || !originalMenuDropAlignment.HasValue)
                return;

            menuDropAlignmentField.SetValue(null, originalMenuDropAlignment.Value);
            originalMenuDropAlignment = null;
        }


        private void CombineMenuItem_MouseEnter(object sender, MouseEventArgs e)
        {
            CombinePreview(GetMemosAtContextMenuPoint());
        }

        private void CombineMenuItem_MouseLeave(object sender, MouseEventArgs e)
        {
            CombinePreview(); // clear
        }

        private List<Memo> GetMemosAtContextMenuPoint()
        {
            return GetVisibleMemos()
                .Where(memo => memo.ContainsScreenPoint(contextMenuX, contextMenuY))
                .OrderBy(memo => memo.Item.FocusOrder)
                .ToList();
        }

        private void CombinePreview(IEnumerable<Memo> memos = null)
        {
            HashSet<Memo> targetSet = new HashSet<Memo>(memos ?? Enumerable.Empty<Memo>());
            foreach (Memo memo in GetVisibleMemos())
            {
                var isTarget = targetSet.Contains(memo);
                if (memo.isCombinePreviewOn != isTarget)
                    memo.SetHighlight(isTarget);
                memo.isCombinePreviewOn = isTarget;
            }
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
