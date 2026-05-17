using System;
using System.Windows;
using System.Windows.Input;

namespace Binjyo
{
    /// <summary>
    /// Interaction logic for ShortcutHelp.xaml
    /// </summary>
    public partial class ShortcutHelp : Window
    {
        public ShortcutHelp()
        {
            InitializeComponent();
            UpdateGlobalShortcut(
                (Key)Properties.Settings.Default.KeyScreenshot,
                (ModifierKeys)Properties.Settings.Default.ModifierScreenshot);
        }

        public void UpdateGlobalShortcut(Key key, ModifierKeys modifiers)
        {
            GlobalShortcutText.Text = $"{FormatModifiers(modifiers)}{key}";
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
