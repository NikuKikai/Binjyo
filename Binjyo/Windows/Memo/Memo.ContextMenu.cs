using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Drawing;


namespace Binjyo
{
    public partial class Memo
    {
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
        private bool isCombinePreviewHighlighted = false;
        private double lastContextMenuScreenX = 0;
        private double lastContextMenuScreenY = 0;
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
                Scene.CloseItem(sceneItem.Id);
            }));
            memoContextMenu.Items.Add(CreateMenuItem("Cut Original", "Shift+X", (s, e) =>
            {
                CopyMemoToClipboard(false);
                Scene.CloseItem(sceneItem.Id);
            }));
            memoContextMenu.Items.Add(CreateMenuItem("Save...", "S", (s, e) => Save(true)));
            memoContextMenu.Items.Add(CreateMenuItem("Save Original...", "Shift+S", (s, e) => Save(false)));
            memoContextMenu.Items.Add(new Separator());
            memoContextMenu.Items.Add(CreateMenuItem("Reset Size", "`", (s, e) => ResetSize()));

            resizeModeMenuItem = CreateCheckableMenuItem("Resize Mode", "T", (s, e) => SetResizeMode(!isResizeMode));
            memoContextMenu.Items.Add(resizeModeMenuItem);
            editModeMenuItem = CreateMenuItem("Edit Mode", "E", (s, e) => EnterEditMode());
            memoContextMenu.Items.Add(editModeMenuItem);
            featurePointsMenuItem = CreateCheckableMenuItem("Feature Points", "P", (s, e) => ToggleFeaturePoints());
            memoContextMenu.Items.Add(featurePointsMenuItem);
            combineMenuItem = CreateMenuItem("Combine", null, (s, e) => CombineMemosAtLastContextMenuPoint());
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
            memoContextMenu.Items.Add(CreateMenuItem("Close", "Esc", (s, e) => Scene.CloseItem(sceneItem.Id)));
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
            MenuItem expandedItem = CreateCheckableMenuItem("Expanded", FormatDisplayModeGestureText(), (s, e) => app.SetViewMode(EDisplayMode.Expanded));
            MenuItem autoHideItem = CreateCheckableMenuItem("Auto Hide", null, (s, e) => app.SetViewMode(EDisplayMode.AutoHide));
            MenuItem minimizedItem = CreateCheckableMenuItem("Minimized", null, (s, e) => app.SetViewMode(EDisplayMode.Minimized));
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
            ClearCombinePreviewHighlights();
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

        private void CombineMenuItem_MouseEnter(object sender, MouseEventArgs e)
        {
            HighlightCombinePreviewTargets(GetMemosAtContextMenuPoint());
        }

        private void CombineMenuItem_MouseLeave(object sender, MouseEventArgs e)
        {
            ClearCombinePreviewHighlights();
        }

        private List<Memo> GetMemosAtContextMenuPoint()
        {
            return GetMemosAtScreenPoint(lastContextMenuScreenX, lastContextMenuScreenY);
        }

        private List<Memo> GetMemosAtScreenPoint(double screenX, double screenY)
        {
            return GetVisibleMemos()
                .Where(memo => memo.ContainsScreenPoint(screenX, screenY))
                .OrderBy(memo => memo.lastFocusOrder)
                .ToList();
        }

        private void HighlightCombinePreviewTargets(IEnumerable<Memo> memos)
        {
            HashSet<Memo> targetSet = new HashSet<Memo>(memos ?? Enumerable.Empty<Memo>());
            foreach (Memo memo in GetVisibleMemos())
            {
                memo.SetCombinePreviewHighlight(targetSet.Contains(memo));
            }
        }

        private void ClearCombinePreviewHighlights()
        {
            HighlightCombinePreviewTargets(Enumerable.Empty<Memo>());
        }

        private void SetCombinePreviewHighlight(bool enabled)
        {
            if (focusFlashOverlay == null || isCombinePreviewHighlighted == enabled)
                return;

            isCombinePreviewHighlighted = enabled;
            focusFlashOverlay.BeginAnimation(UIElement.OpacityProperty, null);
            focusFlashOverlay.Opacity = enabled ? 0.35 : 0;
        }

        private void CombineMemosAtLastContextMenuPoint()
        {
            List<Memo> memosToCombine = GetMemosAtContextMenuPoint();
            if (memosToCombine.Count < 2)
                return;

            ClearCombinePreviewHighlights();

            var renderedItems = memosToCombine
                .Select(memo => new
                {
                    Memo = memo,
                    Bitmap = memo.CreateOutputBitmap(true),
                    Left = (int)Math.Round(memo.Left * memo.dpiFactor),
                    Top = (int)Math.Round(memo.Top * memo.dpiFactor)
                })
                .ToList();

            try
            {
                int unionLeft = renderedItems.Min(item => item.Left);
                int unionTop = renderedItems.Min(item => item.Top);
                int unionRight = renderedItems.Max(item => item.Left + item.Bitmap.Width);
                int unionBottom = renderedItems.Max(item => item.Top + item.Bitmap.Height);
                int combinedWidth = Math.Max(1, unionRight - unionLeft);
                int combinedHeight = Math.Max(1, unionBottom - unionTop);

                Bitmap combinedBitmap = new Bitmap(combinedWidth, combinedHeight, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                using (Graphics graphics = Graphics.FromImage(combinedBitmap))
                {
                    graphics.Clear(System.Drawing.Color.Transparent);
                    foreach (var item in renderedItems)
                    {
                        graphics.DrawImage(item.Bitmap, item.Left - unionLeft, item.Top - unionTop, item.Bitmap.Width, item.Bitmap.Height);
                    }
                }

                var sceneItem = Scene.CreateItem(combinedBitmap, unionLeft, unionTop);
                Memo combinedMemo = new Memo(sceneItem);
                combinedMemo.BringToMemoFocus();

                foreach (Memo memo in memosToCombine)
                {
                    Scene.CloseItem(memo.sceneItem.Id);
                }
            }
            finally
            {
                foreach (var item in renderedItems)
                {
                    item.Bitmap.Dispose();
                }
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
