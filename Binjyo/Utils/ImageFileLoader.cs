using System;
using System.IO;
using System.Linq;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Binjyo
{
    internal static class ImageFileLoader
    {
        private static readonly string[] SupportedExtensions =
        {
            ".png",
            ".jpg",
            ".jpeg",
            ".bmp",
            ".tif",
            ".tiff",
        };

        public static string OpenFileDialogFilter =>
            "Image Files|*.png;*.jpg;*.jpeg;*.bmp;*.tif;*.tiff";

        public static bool IsSupportedPath(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                return false;

            string extension = Path.GetExtension(filePath);
            return SupportedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase);
        }

        public static bool TryLoadWriteableBitmap(string filePath, out WriteableBitmap bitmap, out string errorMessage)
        {
            bitmap = null;
            errorMessage = null;

            if (!File.Exists(filePath))
            {
                errorMessage = "The selected file does not exist.";
                return false;
            }

            if (!IsSupportedPath(filePath))
            {
                errorMessage = "The selected file type is not supported.";
                return false;
            }

            try
            {
                using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    BitmapDecoder decoder = BitmapDecoder.Create(
                        stream,
                        BitmapCreateOptions.PreservePixelFormat,
                        BitmapCacheOption.OnLoad);

                    BitmapSource frame = decoder.Frames[0];
                    FormatConvertedBitmap convertedBitmap = new FormatConvertedBitmap();
                    convertedBitmap.BeginInit();
                    convertedBitmap.Source = frame;
                    convertedBitmap.DestinationFormat = PixelFormats.Bgra32;
                    convertedBitmap.EndInit();
                    convertedBitmap.Freeze();

                    bitmap = new WriteableBitmap(convertedBitmap);
                    bitmap.Freeze();
                    return true;
                }
            }
            catch (Exception ex)
            {
                errorMessage = ex.Message;
                return false;
            }
        }
    }
}
