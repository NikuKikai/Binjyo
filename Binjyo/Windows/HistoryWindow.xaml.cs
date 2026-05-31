using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using Forms = System.Windows.Forms;

namespace Binjyo
{
    public sealed class HistoryEntryViewModel
    {
        public HistoryEntry Entry { get; set; }
        public BitmapImage Thumbnail { get; set; }
        public string CreatedAtText { get; set; }
        public string PositionText { get; set; }
        public string TransformText { get; set; }
        public string DrawingText { get; set; }
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
                        PositionText = $"Position: ({Math.Round(entry.Left):0}, {Math.Round(entry.Top):0})",
                        TransformText = $"Scale: {entry.Scale:0.###}, Rotation: {entry.Rotation:0.#}°, Flip: {FormatFlip(entry)}",
                        DrawingText = entry.HasDrawingData ? "Drawing: Yes" : "Drawing: No"
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
            GetAdjustedBounds(
                item.Left,
                item.Top,
                item.Width,
                item.Height,
                out double restoredLeft,
                out double restoredTop);

            item.SetPos(restoredLeft, restoredTop);
            var memo = new MemoD11(item);
            CanvasWindow.CreateItem(item);

            HistoryStore.DeleteEntry(viewModel.Entry);
            ReloadEntries();
        }

        private static string FormatFlip(HistoryEntry entry)
        {
            if (entry.IsFlipX && entry.IsFlipY)
                return "XY";
            if (entry.IsFlipX)
                return "X";
            if (entry.IsFlipY)
                return "Y";
            return "None";
        }

        private static void GetAdjustedBounds(double left, double top, double width, double height, out double adjustedLeft, out double adjustedTop)
        {
            var targetRect = new Rect(left, top, Math.Max(1, width), Math.Max(1, height));
            foreach (var screen in Forms.Screen.AllScreens)
            {
                var screenRect = new Rect(
                    screen.Bounds.Left,
                    screen.Bounds.Top,
                    screen.Bounds.Width,
                    screen.Bounds.Height);

                if (screenRect.IntersectsWith(targetRect))
                {
                    adjustedLeft = ClampToRange(left, screenRect.Left, screenRect.Right - width);
                    adjustedTop = ClampToRange(top, screenRect.Top, screenRect.Bottom - height);
                    return;
                }
            }

            Forms.Screen nearestScreen = Forms.Screen.AllScreens
                .OrderBy(screen => GetDistanceSquaredToScreen(targetRect, screen))
                .FirstOrDefault();

            if (nearestScreen == null)
            {
                adjustedLeft = left;
                adjustedTop = top;
                return;
            }

            adjustedLeft = ClampToRange(left, nearestScreen.Bounds.Left, nearestScreen.Bounds.Right - width);
            adjustedTop = ClampToRange(top, nearestScreen.Bounds.Top, nearestScreen.Bounds.Bottom - height);
        }

        private static double ClampToRange(double value, double minimum, double maximum)
        {
            if (maximum < minimum)
                return minimum;
            return Math.Max(minimum, Math.Min(maximum, value));
        }

        private static double GetDistanceSquaredToScreen(Rect rect, Forms.Screen screen)
        {
            double dx = 0;
            if (rect.Right < screen.Bounds.Left)
                dx = screen.Bounds.Left - rect.Right;
            else if (rect.Left > screen.Bounds.Right)
                dx = rect.Left - screen.Bounds.Right;

            double dy = 0;
            if (rect.Bottom < screen.Bounds.Top)
                dy = screen.Bounds.Top - rect.Bottom;
            else if (rect.Top > screen.Bounds.Bottom)
                dy = rect.Top - screen.Bounds.Bottom;

            return dx * dx + dy * dy;
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
    }
}
