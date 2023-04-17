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

namespace Binjyo
{
    /// <summary>
    /// Interaction logic for Settings.xaml
    /// </summary>
    public partial class Settings : Window
    {
        private Key keyScreenshot;
        private ModifierKeys modifierScreenshot;
        private bool isSetting = false;

        private Action<Key, ModifierKeys> callbackScreenshotKeySet;

        public Settings(Action<Key, ModifierKeys> callbackScreenshotKeySet)
        {
            InitializeComponent();

            this.callbackScreenshotKeySet = callbackScreenshotKeySet;

            keyScreenshot = (Key)Properties.Settings.Default.KeyScreenshot;
            modifierScreenshot = (ModifierKeys)Properties.Settings.Default.ModifierScreenshot;
            KeyBoxSreenshot.Text = keyScreenshot.ToString();
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

        private void KeyBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            var key = e.Key;
            var mod = Keyboard.Modifiers;

            if (isSetting)
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

            if (isSetting)
            {
                ((TextBox)sender).Text = $"{keyScreenshot}";
                e.Handled = true;
            }
        }

        private void KeyBoxSreenshot_PreviewKeyUp(object sender, KeyEventArgs e)
        {
            isSetting = false;
            ((TextBox)sender).IsReadOnly = true;
            UpdateScreenshotKey();
        }

        private void KeyBoxSreenshot_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            isSetting = true;
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
    }
}
