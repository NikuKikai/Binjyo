using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using Forms = System.Windows.Forms;

namespace Binjyo
{
    public partial class MemoD11
    {
        private static readonly System.Reflection.FieldInfo MenuDropAlignmentField = typeof(SystemParameters).GetField("_menuDropAlignment", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        private static readonly int[] BinarizePercentOptions = new[] { 10, 20, 30, 40, 50, 60, 70, 80, 90 };
        private static readonly int[] QuantizeLevelOptions = new[] { 3, 4, 5, 6, 8, 12, 16 };
        private static readonly int[] OpacityPercentOptions = new[] { 10, 20, 30, 40, 50, 60, 70, 80, 90 };

        private ContextMenu memoContextMenu;
        private Window memoMenuHostWindow;
        private bool? originalMenuDropAlignment;
        private double contextMenuX;
        private double contextMenuY;
        private bool isCombinePreviewOn;

        private void ShowContextMenuAtCursor()
        {
            if (isDrawMode)
                return;

            if (memoContextMenu != null)
                memoContextMenu.IsOpen = false;

            Scene.Focus(Id);
            EnsureMemoMenuHostWindow();

            System.Drawing.Point mousePosition = Forms.Control.MousePosition;
            contextMenuX = mousePosition.X;
            contextMenuY = mousePosition.Y;

            memoContextMenu = BuildContextMenu();
            memoContextMenu.Opened += MemoContextMenu_Opened;
            memoContextMenu.Closed += MemoContextMenu_Closed;

            memoMenuHostWindow.Left = mousePosition.X;
            memoMenuHostWindow.Top = mousePosition.Y;
            if (!memoMenuHostWindow.IsVisible)
                memoMenuHostWindow.Show();
            memoMenuHostWindow.Activate();

            memoContextMenu.Placement = PlacementMode.RelativePoint;
            memoContextMenu.PlacementTarget = memoMenuHostWindow;
            memoContextMenu.HorizontalOffset = 0;
            memoContextMenu.VerticalOffset = 0;
            memoContextMenu.IsOpen = true;
        }

        private void EnsureMemoMenuHostWindow()
        {
            if (memoMenuHostWindow != null)
                return;

            memoMenuHostWindow = new Window
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
            memoMenuHostWindow.Deactivated += MemoMenuHostWindow_Deactivated;
            memoMenuHostWindow.Closed += (s, e) => memoMenuHostWindow = null;
        }

        private ContextMenu BuildContextMenu()
        {
            ContextMenu menu = new ContextMenu
            {
                FlowDirection = FlowDirection.LeftToRight
            };

            menu.Items.Add(AppContextMenuFactory.CreateMenuItem("Copy", "C / Ctrl+C", (s, e) => Item.CopyToClipboard(true)));
            menu.Items.Add(AppContextMenuFactory.CreateMenuItem("Copy Original", "Shift+C", (s, e) => Item.CopyToClipboard(false)));
            menu.Items.Add(AppContextMenuFactory.CreateMenuItem("Cut", "X / Ctrl+X", (s, e) =>
            {
                Item.CopyToClipboard(true);
                Scene.CloseItem(Item.Id);
            }));
            menu.Items.Add(AppContextMenuFactory.CreateMenuItem("Cut Original", "Shift+X", (s, e) =>
            {
                Item.CopyToClipboard(false);
                Scene.CloseItem(Item.Id);
            }));
            menu.Items.Add(AppContextMenuFactory.CreateMenuItem("Save...", "S", (s, e) => Item.Save(true)));
            menu.Items.Add(AppContextMenuFactory.CreateMenuItem("Save Original...", "Shift+S", (s, e) => Item.Save(false)));
            menu.Items.Add(new Separator());
            menu.Items.Add(AppContextMenuFactory.CreateMenuItem("Reset Size", "`", (s, e) => Item.SetScale(1)));

            MenuItem featurePointsMenuItem = AppContextMenuFactory.CreateCheckableMenuItem("Feature Points", "P", (s, e) => ToggleFeaturePoints());
            featurePointsMenuItem.IsEnabled = Scene.DisplayMode != EDisplayMode.Minimized;
            featurePointsMenuItem.IsChecked = Scene.IsStitchMode;
            menu.Items.Add(featurePointsMenuItem);
            MenuItem combineMenuItem = AppContextMenuFactory.CreateMenuItem("Combine", null, (s, e) => CombineMemosAtPos(contextMenuX, contextMenuY));
            combineMenuItem.IsEnabled = Scene.GetIdsAtPos(contextMenuX, contextMenuY).Count >= 2;
            combineMenuItem.MouseEnter += CombineMenuItem_MouseEnter;
            combineMenuItem.MouseLeave += CombineMenuItem_MouseLeave;
            menu.Items.Add(combineMenuItem);
            menu.Items.Add(new Separator());

            MenuItem transformMenu = new MenuItem { Header = "Transform", FlowDirection = FlowDirection.LeftToRight };
            transformMenu.Items.Add(AppContextMenuFactory.CreateMenuItem("Rotate 90°", "R", (s, e) => Item.SetRotationCentered(Item.Rotation + 90)));
            transformMenu.Items.Add(AppContextMenuFactory.CreateMenuItem("Flip Horizontally", "F", (s, e) => Item.SetFlip(!Item.IsFlipX, Item.IsFlipY)));
            transformMenu.Items.Add(AppContextMenuFactory.CreateMenuItem("Flip Vertically", "Shift+F", (s, e) => Item.SetFlip(Item.IsFlipX, !Item.IsFlipY)));
            menu.Items.Add(transformMenu);

            menu.Items.Add(CreateEffectsMenu());
            menu.Items.Add(new Separator());
            menu.Items.Add(AppContextMenuFactory.CreateMenuItem("Close", "Esc", (s, e) => Scene.CloseItem(Item.Id)));
            menu.Items.Add(new Separator());
            menu.Items.Add(AppContextMenuFactory.CreateTrayMenuRoot((App)Application.Current));
            return menu;
        }

        private MenuItem CreateEffectsMenu()
        {
            MenuItem effectsMenu = new MenuItem
            {
                Header = "Effects",
                FlowDirection = FlowDirection.LeftToRight
            };

            MenuItem grayscaleMenuItem = AppContextMenuFactory.CreateCheckableMenuItem("Grayscale", "G", (s, e) => Item.SetEffectGray(!Item.IsEffectGray));
            grayscaleMenuItem.IsChecked = Item.IsEffectGray;
            effectsMenu.Items.Add(grayscaleMenuItem);

            MenuItem hueMapMenuItem = AppContextMenuFactory.CreateCheckableMenuItem("Hue Map", "H", (s, e) => Item.SetEffectHuemap(!Item.IsEffectHuemap));
            hueMapMenuItem.IsChecked = Item.IsEffectHuemap;
            effectsMenu.Items.Add(hueMapMenuItem);
            effectsMenu.Items.Add(CreateBinarizeMenu());
            effectsMenu.Items.Add(CreateQuantizeMenu());
            effectsMenu.Items.Add(CreateOpacityMenu());
            return effectsMenu;
        }

        private MenuItem CreateBinarizeMenu()
        {
            MenuItem menu = new MenuItem
            {
                Header = "Binarization",
                InputGestureText = "B + Wheel",
                FlowDirection = FlowDirection.LeftToRight
            };

            MenuItem offItem = AppContextMenuFactory.CreateCheckableMenuItem("Off", null, (s, e) => Item.SetEffectBinarize(false));
            offItem.IsChecked = !Item.IsEffectBinarize;
            menu.Items.Add(offItem);
            menu.Items.Add(new Separator());

            int currentPercent = GetClosestOption(ThresholdToPercent(Item.PEffectBinarize), BinarizePercentOptions);
            foreach (int percent in BinarizePercentOptions)
            {
                int localPercent = percent;
                MenuItem item = AppContextMenuFactory.CreateCheckableMenuItem($"{localPercent}%", null, (s, e) =>
                {
                    Item.SetEffectQuantize(false);
                    Item.SetEffectBinarize(true, (int)Math.Round(255 * localPercent / 100.0));
                });
                item.IsChecked = Item.IsEffectBinarize && localPercent == currentPercent;
                menu.Items.Add(item);
            }

            return menu;
        }

        private MenuItem CreateQuantizeMenu()
        {
            MenuItem menu = new MenuItem
            {
                Header = "Quantization",
                InputGestureText = "Q + Wheel",
                FlowDirection = FlowDirection.LeftToRight
            };

            MenuItem offItem = AppContextMenuFactory.CreateCheckableMenuItem("Off", null, (s, e) => Item.SetEffectQuantize(false));
            offItem.IsChecked = !Item.IsEffectQuantize;
            menu.Items.Add(offItem);
            menu.Items.Add(new Separator());

            foreach (int level in QuantizeLevelOptions)
            {
                int localLevel = level;
                MenuItem item = AppContextMenuFactory.CreateCheckableMenuItem($"{localLevel} levels", null, (s, e) =>
                {
                    Item.SetEffectBinarize(false);
                    Item.SetEffectQuantize(true, localLevel);
                });
                item.IsChecked = Item.IsEffectQuantize && localLevel == Item.PEffectQuantize;
                menu.Items.Add(item);
            }

            return menu;
        }

        private MenuItem CreateOpacityMenu()
        {
            MenuItem menu = new MenuItem
            {
                Header = "Opacity",
                InputGestureText = "O + Wheel",
                FlowDirection = FlowDirection.LeftToRight
            };

            MenuItem offItem = AppContextMenuFactory.CreateCheckableMenuItem("Off", null, (s, e) => Item.SetOpacity(false));
            offItem.IsChecked = !Item.IsOpacity;
            menu.Items.Add(offItem);
            menu.Items.Add(new Separator());

            int currentPercent = GetClosestOption(ThresholdToPercent((int)Math.Round(Item.Opacity * 255)), OpacityPercentOptions);
            foreach (int percent in OpacityPercentOptions)
            {
                int localPercent = percent;
                MenuItem item = AppContextMenuFactory.CreateCheckableMenuItem($"{localPercent}%", null, (s, e) =>
                    Item.SetOpacity(true, localPercent / 100.0));
                item.IsChecked = Item.IsOpacity && localPercent == currentPercent;
                menu.Items.Add(item);
            }

            return menu;
        }

        private void MemoMenuHostWindow_Deactivated(object sender, EventArgs e)
        {
            if (memoContextMenu != null)
                memoContextMenu.IsOpen = false;
        }

        private void MemoContextMenu_Opened(object sender, RoutedEventArgs e)
        {
            if (MenuDropAlignmentField == null)
                return;

            if (!originalMenuDropAlignment.HasValue)
                originalMenuDropAlignment = SystemParameters.MenuDropAlignment;

            MenuDropAlignmentField.SetValue(null, false);
        }

        private void MemoContextMenu_Closed(object sender, RoutedEventArgs e)
        {
            CombinePreview();

            if (memoMenuHostWindow != null && memoMenuHostWindow.IsVisible)
                memoMenuHostWindow.Hide();

            if (memoContextMenu != null)
            {
                memoContextMenu.Opened -= MemoContextMenu_Opened;
                memoContextMenu.Closed -= MemoContextMenu_Closed;
                memoContextMenu = null;
            }

            if (MenuDropAlignmentField != null && originalMenuDropAlignment.HasValue)
            {
                MenuDropAlignmentField.SetValue(null, originalMenuDropAlignment.Value);
                originalMenuDropAlignment = null;
            }
        }

        private void CombineMenuItem_MouseEnter(object sender, MouseEventArgs e)
        {
            CombinePreview(GetMemosAtContextMenuPoint());
        }

        private void CombineMenuItem_MouseLeave(object sender, MouseEventArgs e)
        {
            CombinePreview();
        }

        private List<MemoD11> GetMemosAtContextMenuPoint()
        {
            return Forms.Application.OpenForms
                .OfType<MemoD11>()
                .Where(memo => memo.Visible && memo.ContainsScreenPoint(contextMenuX, contextMenuY))
                .OrderBy(memo => memo.Item.FocusOrder)
                .ToList();
        }

        private void CombinePreview(IEnumerable<MemoD11> memos = null)
        {
            HashSet<MemoD11> targetSet = new HashSet<MemoD11>(memos ?? Enumerable.Empty<MemoD11>());
            foreach (MemoD11 memo in Forms.Application.OpenForms.OfType<MemoD11>())
            {
                bool isTarget = targetSet.Contains(memo);
                if (memo.isCombinePreviewOn != isTarget)
                    memo.SetHighlight(isTarget);
                memo.isCombinePreviewOn = isTarget;
            }
        }

        private bool ContainsScreenPoint(double screenX, double screenY)
        {
            return currentHostBounds.Left <= screenX &&
                screenX < currentHostBounds.Right &&
                currentHostBounds.Top <= screenY &&
                screenY < currentHostBounds.Bottom;
        }

        private static int ThresholdToPercent(int threshold)
        {
            return (int)Math.Round(threshold * 100.0 / 255.0 / 10.0) * 10;
        }

        private static int GetClosestOption(int value, IEnumerable<int> options)
        {
            return options
                .OrderBy(option => Math.Abs(option - value))
                .First();
        }
    }
}
