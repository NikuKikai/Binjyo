using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Drawing;
using System.Windows.Media.Imaging;

namespace Binjyo
{
    public sealed class HistoryEntry
    {
        public string DirectoryPath { get; set; }
        public string ImagePath { get; set; }
        public string GroupLabel { get; set; }
        public DateTime CreatedAt { get; set; }
        public double Left { get; set; }
        public double Top { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
    }

    public static class HistoryStore
    {
        private const string HistoryFolderName = "BinjyoHistory";
        private const string ImageFileName = "image.png";
        private const string MetaFileName = "meta.txt";
        private const string DrawingFileName = "drawing.xml";

        public static string GetHistoryRoot()
        {
            return Path.Combine(Path.GetTempPath(), HistoryFolderName);
        }


        public static void Save(BitmapSource source, double left, double top, double width, double height, DrawingDocumentData drawingData)
        {
            if (source == null)
                return;

            string root = GetHistoryRoot();
            Directory.CreateDirectory(root);

            string entryDirectory = Path.Combine(root, DateTime.Now.ToString("yyyyMMdd-HHmmssfff", CultureInfo.InvariantCulture));
            Directory.CreateDirectory(entryDirectory);

            string imagePath = Path.Combine(entryDirectory, ImageFileName);
            string metaPath = Path.Combine(entryDirectory, MetaFileName);

            SaveBitmapSourceToPng(source, imagePath);
            File.WriteAllLines(metaPath, new[]
            {
                left.ToString(CultureInfo.InvariantCulture),
                top.ToString(CultureInfo.InvariantCulture),
                width.ToString(CultureInfo.InvariantCulture),
                height.ToString(CultureInfo.InvariantCulture),
            });

            if (drawingData != null && drawingData.Strokes.Count > 0)
            {
                string drawingPath = Path.Combine(entryDirectory, DrawingFileName);
                DrawingDataSerializer.Save(drawingPath, drawingData);
            }

            EnforceEntryLimit();
        }

        public static List<HistoryEntry> LoadEntries()
        {
            string root = GetHistoryRoot();
            if (!Directory.Exists(root))
                return new List<HistoryEntry>();

            List<HistoryEntry> entries = new List<HistoryEntry>();
            foreach (string directory in Directory.GetDirectories(root))
            {
                string imagePath = Path.Combine(directory, ImageFileName);
                string metaPath = Path.Combine(directory, MetaFileName);
                if (!File.Exists(imagePath) || !File.Exists(metaPath))
                    continue;

                if (!TryReadMetadata(metaPath, out double left, out double top, out double width, out double height))
                    continue;

                DateTime createdAt = File.GetCreationTime(imagePath);
                entries.Add(new HistoryEntry
                {
                    DirectoryPath = directory,
                    ImagePath = imagePath,
                    CreatedAt = createdAt,
                    GroupLabel = createdAt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    Left = left,
                    Top = top,
                    Width = width,
                    Height = height
                });
            }

            return entries
                .OrderByDescending(entry => entry.CreatedAt)
                .ToList();
        }

        public static Bitmap LoadBitmap(HistoryEntry entry)
        {
            using (var stream = new FileStream(entry.ImagePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var bitmap = new Bitmap(stream))
            {
                return new Bitmap(bitmap);
            }
        }
        public static WriteableBitmap LoadWriteableBitmap(HistoryEntry entry)
        {
            // 1. ファイルストリームを開く（読み込み完了後に即破棄されるように using を使用）
            using (var stream = new FileStream(entry.ImagePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                // 2. デコーダーを使い、ストリームから画像をデコード（メモリ上に直接展開）
                // BitmapCreateOptions.PreservePixelFormat | BitmapCreateOptions.IgnoreColorProfile で高速化
                var decoder = BitmapDecoder.Create(
                    stream,
                    BitmapCreateOptions.PreservePixelFormat,
                    BitmapCacheOption.OnLoad);

                BitmapFrame frame = decoder.Frames[0];

                // Formatting
                FormatConvertedBitmap convertedBitmap = new FormatConvertedBitmap();
                convertedBitmap.BeginInit();
                convertedBitmap.Source = frame;
                convertedBitmap.DestinationFormat = System.Windows.Media.PixelFormats.Bgra32;
                convertedBitmap.EndInit();

                // 4. Create WriteableBitmap
                WriteableBitmap wbitmap = new WriteableBitmap(convertedBitmap);

                // （オプション）もしこの関数を「表示専用（編集しない）」として使うなら、
                // ここで wbitmap.Freeze(); を呼ぶとさらにパフォーマンスが向上します。

                return wbitmap;
            }
        }

        public static void DeleteEntry(HistoryEntry entry)
        {
            if (entry == null || string.IsNullOrWhiteSpace(entry.DirectoryPath))
                return;

            if (Directory.Exists(entry.DirectoryPath))
                Directory.Delete(entry.DirectoryPath, true);
        }

        public static void ClearAll()
        {
            string root = GetHistoryRoot();
            if (Directory.Exists(root))
                Directory.Delete(root, true);
        }

        public static void ClearOlderThan(DateTime cutoff)
        {
            foreach (HistoryEntry entry in LoadEntries().Where(item => item.CreatedAt < cutoff).ToList())
            {
                DeleteEntry(entry);
            }
        }

        public static DrawingDocumentData LoadDrawingData(HistoryEntry entry)
        {
            if (entry == null || string.IsNullOrWhiteSpace(entry.DirectoryPath))
                return new DrawingDocumentData();

            string drawingPath = Path.Combine(entry.DirectoryPath, DrawingFileName);
            return DrawingDataSerializer.Load(drawingPath);
        }

        private static bool TryReadMetadata(string metaPath, out double left, out double top, out double width, out double height)
        {
            left = 0;
            top = 0;
            width = 0;
            height = 0;

            string[] lines = File.ReadAllLines(metaPath);
            if (lines.Length < 4)
                return false;

            return
                double.TryParse(lines[0], NumberStyles.Float, CultureInfo.InvariantCulture, out left) &&
                double.TryParse(lines[1], NumberStyles.Float, CultureInfo.InvariantCulture, out top) &&
                double.TryParse(lines[2], NumberStyles.Float, CultureInfo.InvariantCulture, out width) &&
                double.TryParse(lines[3], NumberStyles.Float, CultureInfo.InvariantCulture, out height);
        }

        private static void SaveBitmapSourceToPng(BitmapSource source, string path)
        {
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(source));
            using (var stream = new FileStream(path, FileMode.Create, FileAccess.Write))
            {
                encoder.Save(stream);
            }
        }

        private static void EnforceEntryLimit()
        {
            int limit = Math.Max(1, Properties.Settings.Default.HistoryEntryLimit);
            List<HistoryEntry> entries = LoadEntries();
            if (entries.Count <= limit)
                return;

            foreach (HistoryEntry entry in entries
                .OrderByDescending(item => item.CreatedAt)
                .Skip(limit)
                .ToList())
            {
                DeleteEntry(entry);
            }
        }
    }
}
