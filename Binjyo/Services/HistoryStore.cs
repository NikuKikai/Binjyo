using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
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
        public double Scale { get; set; }
        public double Rotation { get; set; }
        public bool IsFlipX { get; set; }
        public bool IsFlipY { get; set; }
        public bool HasDrawingData { get; set; }
    }

    [DataContract]
    internal sealed class SceneItemHistorySnapshot
    {
        [DataMember(Order = 1)]
        public double Left { get; set; }

        [DataMember(Order = 2)]
        public double Top { get; set; }

        [DataMember(Order = 3)]
        public double Scale { get; set; }

        [DataMember(Order = 4)]
        public double Rotation { get; set; }

        [DataMember(Order = 5)]
        public bool IsFlipX { get; set; }

        [DataMember(Order = 6)]
        public bool IsFlipY { get; set; }

        [DataMember(Order = 7)]
        public bool HasDrawingData { get; set; }
    }

    [DataContract]
    internal sealed class DrawingDocumentSnapshot
    {
        [DataMember(Order = 1)]
        public double SourcePixelWidth { get; set; }

        [DataMember(Order = 2)]
        public double SourcePixelHeight { get; set; }

        [DataMember(Order = 3)]
        public List<DrawingStrokeSnapshot> Strokes { get; set; } = new List<DrawingStrokeSnapshot>();
    }

    [DataContract]
    internal sealed class DrawingStrokeSnapshot
    {
        [DataMember(Order = 1)]
        public Guid Id { get; set; }

        [DataMember(Order = 2)]
        public double SizePx { get; set; }

        [DataMember(Order = 3)]
        public DrawingColorSnapshot Color { get; set; }

        [DataMember(Order = 4)]
        public List<DrawingPointSnapshot> Points { get; set; } = new List<DrawingPointSnapshot>();
    }

    [DataContract]
    internal sealed class DrawingColorSnapshot
    {
        [DataMember(Order = 1)]
        public byte A { get; set; }

        [DataMember(Order = 2)]
        public byte R { get; set; }

        [DataMember(Order = 3)]
        public byte G { get; set; }

        [DataMember(Order = 4)]
        public byte B { get; set; }
    }

    [DataContract]
    internal sealed class DrawingPointSnapshot
    {
        [DataMember(Order = 1)]
        public double X { get; set; }

        [DataMember(Order = 2)]
        public double Y { get; set; }
    }

    public static class HistoryStore
    {
        private const string HistoryFolderName = "BinjyoHistory";
        private const string ImageFileName = "image.png";
        private const string MetaFileName = "meta.json";
        private const string DrawingFileName = "drawing.json";

        public static string GetHistoryRoot()
        {
            return Path.Combine(Path.GetTempPath(), HistoryFolderName);
        }

        public static void SaveSceneItemSnapshot(SceneItem item)
        {
            if (item?.Bitmap == null)
                return;

            string root = GetHistoryRoot();
            Directory.CreateDirectory(root);

            string entryDirectory = Path.Combine(root, DateTime.Now.ToString("yyyyMMdd-HHmmssfff", CultureInfo.InvariantCulture));
            Directory.CreateDirectory(entryDirectory);

            string imagePath = Path.Combine(entryDirectory, ImageFileName);
            string metaPath = Path.Combine(entryDirectory, MetaFileName);
            string drawingPath = Path.Combine(entryDirectory, DrawingFileName);

            SaveBitmapSourceToPng(item.Bitmap, imagePath);

            DrawingDocumentSnapshot drawingSnapshot = CreateDrawingSnapshot(item);
            bool hasDrawingData = drawingSnapshot != null && drawingSnapshot.Strokes.Count > 0;
            if (hasDrawingData)
                WriteJson(drawingPath, drawingSnapshot);

            WriteJson(metaPath, new SceneItemHistorySnapshot
            {
                Left = item.Left,
                Top = item.Top,
                Scale = item.Scale,
                Rotation = item.Rotation,
                IsFlipX = item.IsFlipX,
                IsFlipY = item.IsFlipY,
                HasDrawingData = hasDrawingData
            });

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

                SceneItemHistorySnapshot snapshot = ReadJson<SceneItemHistorySnapshot>(metaPath);
                if (snapshot == null)
                    continue;

                DateTime createdAt = File.GetCreationTime(imagePath);
                entries.Add(new HistoryEntry
                {
                    DirectoryPath = directory,
                    ImagePath = imagePath,
                    CreatedAt = createdAt,
                    GroupLabel = createdAt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    Left = snapshot.Left,
                    Top = snapshot.Top,
                    Scale = snapshot.Scale,
                    Rotation = snapshot.Rotation,
                    IsFlipX = snapshot.IsFlipX,
                    IsFlipY = snapshot.IsFlipY,
                    HasDrawingData = snapshot.HasDrawingData
                });
            }

            return entries
                .OrderByDescending(entry => entry.CreatedAt)
                .ToList();
        }

        public static SceneItem RestoreSceneItem(HistoryEntry entry)
        {
            if (entry == null)
                return null;

            WriteableBitmap bitmap = LoadWriteableBitmap(entry);
            SceneItem item = Scene.CreateItem(bitmap, 0, 0);
            item.SetScale(entry.Scale);
            item.SetFlip(entry.IsFlipX, entry.IsFlipY);
            item.SetRotationCentered(entry.Rotation);
            item.SetPos(entry.Left, entry.Top);
            item.DrawingDocument = LoadDrawingData(entry);
            item.DrawingDocument.ConfigureSourceSize(bitmap.PixelWidth, bitmap.PixelHeight);
            return item;
        }

        public static WriteableBitmap LoadWriteableBitmap(HistoryEntry entry)
        {
            using (var stream = new FileStream(entry.ImagePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                var decoder = BitmapDecoder.Create(
                    stream,
                    BitmapCreateOptions.PreservePixelFormat,
                    BitmapCacheOption.OnLoad);

                BitmapFrame frame = decoder.Frames[0];
                FormatConvertedBitmap convertedBitmap = new FormatConvertedBitmap();
                convertedBitmap.BeginInit();
                convertedBitmap.Source = frame;
                convertedBitmap.DestinationFormat = System.Windows.Media.PixelFormats.Bgra32;
                convertedBitmap.EndInit();
                return new WriteableBitmap(convertedBitmap);
            }
        }

        public static DrawingDocumentData LoadDrawingData(HistoryEntry entry)
        {
            if (entry == null || string.IsNullOrWhiteSpace(entry.DirectoryPath))
                return new DrawingDocumentData();

            string drawingPath = Path.Combine(entry.DirectoryPath, DrawingFileName);
            DrawingDocumentSnapshot snapshot = ReadJson<DrawingDocumentSnapshot>(drawingPath);
            return snapshot == null ? new DrawingDocumentData() : CreateDrawingDocument(snapshot);
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
                DeleteEntry(entry);
        }

        private static DrawingDocumentSnapshot CreateDrawingSnapshot(SceneItem item)
        {
            DrawingDocumentData document = item.DrawingDocument;
            if (document == null)
                return null;

            document.ConfigureSourceSize(item.Bitmap.PixelWidth, item.Bitmap.PixelHeight);
            List<DrawingStrokeSnapshot> strokes = document.GetVisibleStrokes()
                .Select(stroke => new DrawingStrokeSnapshot
                {
                    Id = stroke.Id,
                    SizePx = stroke.SizePx,
                    Color = stroke.Color == null ? null : new DrawingColorSnapshot
                    {
                        A = stroke.Color.A,
                        R = stroke.Color.R,
                        G = stroke.Color.G,
                        B = stroke.Color.B
                    },
                    Points = stroke.Points.Select(point => new DrawingPointSnapshot
                    {
                        X = point.X,
                        Y = point.Y
                    }).ToList()
                })
                .ToList();

            if (strokes.Count == 0)
                return null;

            return new DrawingDocumentSnapshot
            {
                SourcePixelWidth = document.SourcePixelWidth,
                SourcePixelHeight = document.SourcePixelHeight,
                Strokes = strokes
            };
        }

        private static DrawingDocumentData CreateDrawingDocument(DrawingDocumentSnapshot snapshot)
        {
            var document = new DrawingDocumentData
            {
                SourcePixelWidth = Math.Max(1, snapshot.SourcePixelWidth),
                SourcePixelHeight = Math.Max(1, snapshot.SourcePixelHeight),
                AppliedOperationCount = 0
            };

            document.Objects = snapshot.Strokes
                .Select(stroke => (DrawingObjectData)new DrawingStrokeData
                {
                    Id = stroke.Id,
                    IsDeleted = false,
                    SizePx = stroke.SizePx,
                    Color = stroke.Color == null ? new DrawingColorData() : new DrawingColorData
                    {
                        A = stroke.Color.A,
                        R = stroke.Color.R,
                        G = stroke.Color.G,
                        B = stroke.Color.B
                    },
                    Points = stroke.Points.Select(point => new DrawingPointData
                    {
                        X = point.X,
                        Y = point.Y
                    }).ToList()
                })
                .ToList();

            document.Operations = new List<DrawingOperationData>();
            return document;
        }

        private static void WriteJson<T>(string path, T value)
        {
            var serializer = new DataContractJsonSerializer(typeof(T));
            using (var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                serializer.WriteObject(stream, value);
            }
        }

        private static T ReadJson<T>(string path) where T : class
        {
            if (!File.Exists(path))
                return null;

            try
            {
                var serializer = new DataContractJsonSerializer(typeof(T));
                using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    return serializer.ReadObject(stream) as T;
                }
            }
            catch
            {
                return null;
            }
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
