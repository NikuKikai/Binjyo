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
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Text.RegularExpressions;

namespace Binjyo
{
    /// <summary>
    /// Interaction logic for Settings.xaml
    /// </summary>
    public partial class Settings : Window
    {
        private Key keyScreenshot;
        private ModifierKeys modifierScreenshot;
        private ModifierKeys modifierDisplayMode;
        private bool isSettingScreenshot = false;

        private Action<Key, ModifierKeys> callbackScreenshotKeySet;
        private Action<ModifierKeys> callbackDisplayModeModifierSet;

        public Settings(Action<Key, ModifierKeys> callbackScreenshotKeySet, Action<ModifierKeys> callbackDisplayModeModifierSet)
        {
            InitializeComponent();

            this.callbackScreenshotKeySet = callbackScreenshotKeySet;
            this.callbackDisplayModeModifierSet = callbackDisplayModeModifierSet;

            keyScreenshot = (Key)Properties.Settings.Default.KeyScreenshot;
            modifierScreenshot = (ModifierKeys)Properties.Settings.Default.ModifierScreenshot;
            modifierDisplayMode = (ModifierKeys)Properties.Settings.Default.ModifierDisplayMode;
            KeyBoxSreenshot.Text = keyScreenshot.ToString();
            CheckSnapMemo.IsChecked = Properties.Settings.Default.SnapMemo;
            CheckExportApplyTransform.IsChecked = Properties.Settings.Default.ExportApplyTransform;
            CheckExportApplyEffects.IsChecked = Properties.Settings.Default.ExportApplyEffects;
            HistoryEntryLimitBox.Text = Properties.Settings.Default.HistoryEntryLimit.ToString();
            SelectBitmapScalingMode((MemoBitmapScalingMode)Properties.Settings.Default.BitmapScalingMode);
            switch (modifierScreenshot)
            {
                case ModifierKeys.Control | ModifierKeys.Alt:
                    RadioScreenshot0.IsChecked = true;
                    break;
                case ModifierKeys.Control | ModifierKeys.Shift:
                    RadioScreenshot1.IsChecked = true;
                    break;
                case ModifierKeys.Control | ModifierKeys.Windows:
                    RadioScreenshot2.IsChecked = true;
                    break;
            }
            switch (modifierDisplayMode)
            {
                case ModifierKeys.Control | ModifierKeys.Alt:
                    RadioDisplayMode0.IsChecked = true;
                    break;
                case ModifierKeys.Control | ModifierKeys.Windows:
                    RadioDisplayMode2.IsChecked = true;
                    break;
                default:
                    RadioDisplayMode1.IsChecked = true;
                    break;
            }

            switch ((MemoAutoHideBehavior)Properties.Settings.Default.AutoHideBehavior)
            {
                case MemoAutoHideBehavior.EvadeMouse:
                    RadioAutoHideEvade.IsChecked = true;
                    break;
                default:
                    RadioAutoHideHover.IsChecked = true;
                    break;
            }
        }

        private void SelectBitmapScalingMode(MemoBitmapScalingMode mode)
        {
            if (BitmapScalingModeBox == null)
                return;

            foreach (ComboBoxItem item in BitmapScalingModeBox.Items)
            {
                if (item.Tag?.ToString() == ((int)mode).ToString())
                {
                    BitmapScalingModeBox.SelectedItem = item;
                    return;
                }
            }

            BitmapScalingModeBox.SelectedIndex = 0;
        }

        private void UpdateScreenshotKey()
        {
            if (Properties.Settings.Default.KeyScreenshot == (int)keyScreenshot
                && Properties.Settings.Default.ModifierScreenshot == (int)modifierScreenshot)
                return;
            Properties.Settings.Default.KeyScreenshot = (int)keyScreenshot;
            Properties.Settings.Default.ModifierScreenshot = (int)modifierScreenshot;
            Properties.Settings.Default.Save();

            if (callbackScreenshotKeySet != null)
                callbackScreenshotKeySet.Invoke(keyScreenshot, modifierScreenshot);
        }

        private void UpdateDisplayModeModifier()
        {
            if (Properties.Settings.Default.ModifierDisplayMode == (int)modifierDisplayMode)
                return;

            Properties.Settings.Default.ModifierDisplayMode = (int)modifierDisplayMode;
            Properties.Settings.Default.Save();

            if (callbackDisplayModeModifierSet != null)
                callbackDisplayModeModifierSet.Invoke(modifierDisplayMode);
        }

        private void KeyBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            var key = e.Key;
            var mod = Keyboard.Modifiers;

            if (isSettingScreenshot)
            {
                if (mod == ModifierKeys.None)
                    ((TextBox)sender).Text = $"{key}";
                else
                    ((TextBox)sender).Text = $"{mod}+{key}";
                e.Handled = true;
            }
        }
        private void KeyBoxSreenshot_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            keyScreenshot = e.Key;

            if (isSettingScreenshot)
            {
                ((TextBox)sender).Text = $"{keyScreenshot}";
                e.Handled = true;
            }
        }

        private void KeyBoxSreenshot_PreviewKeyUp(object sender, KeyEventArgs e)
        {
            isSettingScreenshot = false;
            ((TextBox)sender).IsReadOnly = true;
            UpdateScreenshotKey();
        }

        private void KeyBoxSreenshot_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            isSettingScreenshot = true;
            ((TextBox)sender).IsReadOnly = true;
            ((TextBox)sender).Text = "...";
        }

        private void RadioScreenshot_Checked(object sender, RoutedEventArgs e)
        {
            RadioButton rb = sender as RadioButton;
            if (rb.IsChecked == true)
            {
                switch (rb.Content.ToString())
                {
                    case "Ctrl+Alt":
                        modifierScreenshot = ModifierKeys.Control | ModifierKeys.Alt;
                        break;
                    case "Ctrl+Shift":
                        modifierScreenshot = ModifierKeys.Control | ModifierKeys.Shift;
                        break;
                    case "Ctrl+Win":
                        modifierScreenshot = ModifierKeys.Control | ModifierKeys.Windows;
                        break;
                }
            }
            UpdateScreenshotKey();
        }

        private void RadioDisplayMode_Checked(object sender, RoutedEventArgs e)
        {
            RadioButton rb = sender as RadioButton;
            if (rb.IsChecked == true)
            {
                switch (rb.Content.ToString())
                {
                    case "Ctrl+Alt":
                        modifierDisplayMode = ModifierKeys.Control | ModifierKeys.Alt;
                        break;
                    case "Ctrl+Shift":
                        modifierDisplayMode = ModifierKeys.Control | ModifierKeys.Shift;
                        break;
                    case "Ctrl+Win":
                        modifierDisplayMode = ModifierKeys.Control | ModifierKeys.Windows;
                        break;
                }
            }
            UpdateDisplayModeModifier();
        }

        private void CheckSnapMemo_Changed(object sender, RoutedEventArgs e)
        {
            if (CheckSnapMemo == null || CheckSnapMemo.IsChecked == null)
                return;

            if (Properties.Settings.Default.SnapMemo == CheckSnapMemo.IsChecked.Value)
                return;

            Properties.Settings.Default.SnapMemo = CheckSnapMemo.IsChecked.Value;
            Properties.Settings.Default.Save();
        }

        private void RadioAutoHide_Checked(object sender, RoutedEventArgs e)
        {
            if (RadioAutoHideHover == null || RadioAutoHideEvade == null)
                return;

            MemoAutoHideBehavior behavior = RadioAutoHideEvade.IsChecked == true
                ? MemoAutoHideBehavior.EvadeMouse
                : MemoAutoHideBehavior.HideOnHover;

            if (Properties.Settings.Default.AutoHideBehavior == (int)behavior)
                return;

            Properties.Settings.Default.AutoHideBehavior = (int)behavior;
            Properties.Settings.Default.Save();
        }

        private void HistoryEntryLimitBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = Regex.IsMatch(e.Text, "[^0-9]");
        }

        private void HistoryEntryLimitBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (HistoryEntryLimitBox == null)
                return;

            if (!int.TryParse(HistoryEntryLimitBox.Text, out int value))
                return;

            value = Math.Max(1, value);
            if (Properties.Settings.Default.HistoryEntryLimit == value)
                return;

            Properties.Settings.Default.HistoryEntryLimit = value;
            Properties.Settings.Default.Save();
        }

        private void CheckExportApplyTransform_Changed(object sender, RoutedEventArgs e)
        {
            if (CheckExportApplyTransform?.IsChecked == null)
                return;

            if (Properties.Settings.Default.ExportApplyTransform == CheckExportApplyTransform.IsChecked.Value)
                return;

            Properties.Settings.Default.ExportApplyTransform = CheckExportApplyTransform.IsChecked.Value;
            Properties.Settings.Default.Save();
        }

        private void CheckExportApplyEffects_Changed(object sender, RoutedEventArgs e)
        {
            if (CheckExportApplyEffects?.IsChecked == null)
                return;

            if (Properties.Settings.Default.ExportApplyEffects == CheckExportApplyEffects.IsChecked.Value)
                return;

            Properties.Settings.Default.ExportApplyEffects = CheckExportApplyEffects.IsChecked.Value;
            Properties.Settings.Default.Save();
        }

        private void BitmapScalingModeBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!(BitmapScalingModeBox?.SelectedItem is ComboBoxItem item) || item.Tag == null)
                return;

            if (!int.TryParse(item.Tag.ToString(), out int value))
                return;

            if (Properties.Settings.Default.BitmapScalingMode == value)
                return;

            Properties.Settings.Default.BitmapScalingMode = value;
            Properties.Settings.Default.Save();
            Memo.RefreshAllMemoScalingModes();
        }
    }
}
