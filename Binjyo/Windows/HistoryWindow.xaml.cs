using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.IO;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace Binjyo
{
    public sealed class HistoryEntryViewModel
    {
        public HistoryEntry Entry { get; set; }
        public BitmapImage Thumbnail { get; set; }
        public string CreatedAtText { get; set; }
        public bool IsCaptureSource { get; set; }
    }

    public sealed class HistoryGroupViewModel
    {
        public string Key { get; set; }
        public ObservableCollection<HistoryEntryViewModel> Items { get; set; }
    }

    /// <summary>
    /// Interaction logic for HistoryWindow.xaml
    /// </summary>
    public partial class HistoryWindow : Window
    {
        private readonly ObservableCollection<HistoryGroupViewModel> groups = new ObservableCollection<HistoryGroupViewModel>();

        public HistoryWindow()
        {
            InitializeComponent();
            GroupsControl.ItemsSource = groups;
            ReloadEntries();
            Activated += (sender, e) => ReloadEntries();
        }

        public void ReloadEntries()
        {
            groups.Clear();
            var groupedEntries = HistoryStore.LoadEntries()
                .GroupBy(entry => entry.GroupLabel)
                .OrderByDescending(group => group.Max(item => item.CreatedAt));

            foreach (var group in groupedEntries)
            {
                var items = new ObservableCollection<HistoryEntryViewModel>(
                    group.Select(entry => new HistoryEntryViewModel
                    {
                        Entry = entry,
                        Thumbnail = CreateThumbnail(entry.ImagePath),
                        CreatedAtText = entry.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                        IsCaptureSource = entry.IsCaptureSource
                    }));

                groups.Add(new HistoryGroupViewModel
                {
                    Key = group.Key,
                    Items = items
                });
            }
        }

        private BitmapImage CreateThumbnail(string imagePath)
        {
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.UriSource = new Uri(imagePath);
            image.DecodePixelWidth = 164;
            image.EndInit();
            image.Freeze();
            return image;
        }

        private void EntryBorder_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount != 2)
                return;

            if (!(sender is Border border) || !(border.DataContext is HistoryEntryViewModel viewModel))
                return;

            RestoreEntry(viewModel);
        }

        private void RestoreEntry(HistoryEntryViewModel viewModel)
        {
            var item = HistoryStore.RestoreSceneItem(viewModel.Entry);
            Geo.GetAdjustedBounds(
                item.Left,
                item.Top,
                item.Width,
                item.Height,
                out double restoredLeft,
                out double restoredTop);

            item.SetPos(restoredLeft, restoredTop);
            var memo = new MemoD11(item);
            CanvasWindow.CreateItem(item);
            Scene.Focus(item.Id);

            HistoryStore.DeleteEntry(viewModel.Entry);
            ReloadEntries();

            Dispatcher.BeginInvoke(new Action(() =>
            {
                memo.Activate();
            }), System.Windows.Threading.DispatcherPriority.ApplicationIdle);
        }

        private void ClearButton_Click(object sender, RoutedEventArgs e)
        {
            HistoryStore.ClearAll();
            ReloadEntries();
        }

        private void ClearOldButton_Click(object sender, RoutedEventArgs e)
        {
            HistoryStore.ClearOlderThan(DateTime.Now.AddDays(-7));
            ReloadEntries();
        }

        private void OpenFolderButton_Click(object sender, RoutedEventArgs e)
        {
            string historyRoot = HistoryStore.GetHistoryRoot();
            Directory.CreateDirectory(historyRoot);

            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = historyRoot,
                UseShellExecute = true
            });
        }
    }
}
