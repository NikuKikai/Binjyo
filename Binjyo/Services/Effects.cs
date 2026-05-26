using System;
using System.Collections.Generic;
using System.Linq;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Drawing.Drawing2D;
using BitmapScalingMode = System.Windows.Media.BitmapScalingMode;
using Rect = System.Drawing.Rectangle;


namespace Binjyo
{
    public static class Effects
    {
        public static void Gray(Bitmap src)
        {
            // Bitmap newBitmap = new Bitmap(src.Width, src.Height);
            using (Graphics g = Graphics.FromImage(src))
            {
                //create the grayscale ColorMatrix
                ColorMatrix colorMatrix = new ColorMatrix(
                    new float[][]
                    {
                        new float[] {.3f, .3f, .3f, 0, 0},
                        new float[] {.59f, .59f, .59f, 0, 0},
                        new float[] {.11f, .11f, .11f, 0, 0},
                        new float[] {0, 0, 0, 1, 0},
                        new float[] {0, 0, 0, 0, 1}
                    });

                //create some image attributes
                using (ImageAttributes attributes = new ImageAttributes())
                {
                    attributes.SetColorMatrix(colorMatrix);
                    g.DrawImage(src, new Rect(0, 0, src.Width, src.Height),
                                0, 0, src.Width, src.Height, GraphicsUnit.Pixel, attributes);
                }
            }
            // return newBitmap;
        }

        public static void Binarize(Bitmap src, int threshold)
        {
            if (src.PixelFormat != PixelFormat.Format32bppArgb)
                // src = src.Clone(new Rect(0, 0, src.Width, src.Height), PixelFormat.Format32bppArgb);
                return;

            // Lock the bitmap's bits.
            Rect rect = new Rect(0, 0, src.Width, src.Height);
            BitmapData bmpData = src.LockBits(rect, ImageLockMode.ReadWrite, src.PixelFormat);

            try
            {
                // Get the address of the first line.
                IntPtr ptr = bmpData.Scan0;

                // Declare an array to hold the bytes of the bitmap.
                int bytes = Math.Abs(bmpData.Stride) * src.Height;
                byte[] rgbValues = new byte[bytes];

                // Copy the RGB values into the array.
                Marshal.Copy(ptr, rgbValues, 0, bytes);

                // Set every alpha value. The order is B, G, R, A
                for (int i = 0; i < rgbValues.Length; i += 1)
                    rgbValues[i] = rgbValues[i] > threshold ? (byte)255 : (byte)0;

                // Copy the RGB values back to the bitmap
                Marshal.Copy(rgbValues, 0, ptr, bytes);
            }
            finally
            {
                src.UnlockBits(bmpData);
            }
        }

        public static void Quantize(Bitmap src, int q)
        {
            if (src.PixelFormat != PixelFormat.Format32bppArgb)
                // src = src.Clone(new Rect(0, 0, src.Width, src.Height), PixelFormat.Format32bppArgb);
                return;

            // Lock the bitmap's bits.
            Rect rect = new Rect(0, 0, src.Width, src.Height);
            BitmapData bmpData = src.LockBits(rect, ImageLockMode.ReadWrite, src.PixelFormat);

            try
            {
                // Get the address of the first line.
                IntPtr ptr = bmpData.Scan0;
                // Declare an array to hold the bytes of the bitmap.
                int bytes = Math.Abs(bmpData.Stride) * src.Height;
                byte[] rgbValues = new byte[bytes];
                // Copy the RGB values into the array.
                Marshal.Copy(ptr, rgbValues, 0, bytes);

                // Set every alpha value. The order is B, G, R, A
                for (int i = 0; i < rgbValues.Length; i += 1)
                {
                    float quant = 255f / (q - 1) / 2;
                    for (int iq = 0; iq < q; iq++)
                    {
                        var low = (2 * iq - 1) * quant;
                        var high = (2 * iq + 1) * quant;
                        if (rgbValues[i] > low && rgbValues[i] <= high)
                        {
                            rgbValues[i] = (byte)(2 * iq * quant);
                            break;
                        }
                    }
                }
                // Copy the RGB values back to the bitmap
                Marshal.Copy(rgbValues, 0, ptr, bytes);
            }
            finally
            {
                src.UnlockBits(bmpData);
            }
        }

        public static void Transparent(Bitmap src, int transparency)
        {
            // Processing bytes using LockBits is faster than SetPixel/GetPixel
            // https://docs.microsoft.com/ja-jp/dotnet/api/system.drawing.bitmap.lockbits?view=dotnet-plat-ext-6.0

            if (src.PixelFormat != PixelFormat.Format32bppArgb)
                // src = src.Clone(new Rect(0, 0, src.Width, src.Height), PixelFormat.Format32bppArgb);
                return;

            // Lock the bitmap's bits.
            Rect rect = new Rect(0, 0, src.Width, src.Height);
            BitmapData bmpData = src.LockBits(rect, ImageLockMode.ReadWrite, src.PixelFormat);

            try
            {
                // Get the address of the first line.
                IntPtr ptr = bmpData.Scan0;
                // Declare an array to hold the bytes of the bitmap.
                int bytes = Math.Abs(bmpData.Stride) * src.Height;
                byte[] rgbValues = new byte[bytes];
                // Copy the RGB values into the array.
                Marshal.Copy(ptr, rgbValues, 0, bytes);

                // Set every alpha value. The order is B, G, R, A
                for (int i = 3; i < rgbValues.Length; i += 4)
                    rgbValues[i] = (byte)((int)rgbValues[i] * (255 - transparency) / 255);

                // Copy the RGB values back to the bitmap
                Marshal.Copy(rgbValues, 0, ptr, bytes);
            }
            finally
            {
                src.UnlockBits(bmpData);
            }
        }

        public static void Huemap(Bitmap src)
        {
            if (src.PixelFormat != PixelFormat.Format32bppArgb)
                // src = src.Clone(new Rect(0, 0, src.Width, src.Height), PixelFormat.Format32bppArgb);
                return;

            // Lock the bitmap's bits.
            Rect rect = new Rect(0, 0, src.Width, src.Height);
            BitmapData bmpData =
                src.LockBits(rect, ImageLockMode.ReadWrite,
                src.PixelFormat);

            try
            {
                // Get the address of the first line.
                IntPtr ptr = bmpData.Scan0;
                // Declare an array to hold the bytes of the bitmap.
                int bytes = Math.Abs(bmpData.Stride) * src.Height;
                byte[] rgbValues = new byte[bytes];
                // Copy the RGB values into the array.
                Marshal.Copy(ptr, rgbValues, 0, bytes);

                // Set every alpha value. The order is B, G, R, A
                const int quant = 10;
                for (int i = 0; i < rgbValues.Length; i += 4)
                {
                    byte b = rgbValues[i];
                    byte g = rgbValues[i + 1];
                    byte r = rgbValues[i + 2];
                    // byte a = rgbValues[i+3];

                    if (Math.Max(b, Math.Max(g, r)) - Math.Min(b, Math.Min(g, r)) < quant)
                    {
                        rgbValues[i] = 128;
                        rgbValues[i + 1] = 128;
                        rgbValues[i + 2] = 128;
                        continue;
                    }

                    // Get Hue
                    float h = 0;
                    if (r >= b && r >= g)
                        h = 60f * (g - b) / (r - Math.Min(b, g));
                    else if (g > r && g > b)
                        h = 60f * (b - r) / (g - Math.Min(b, r)) + 120;
                    else
                        h = 60f * (r - g) / (b - Math.Min(g, r)) + 240;
                    h = (float)Math.Round(h / quant) * quant;

                    // Get RGB. s = 255, v = 255, thus MAX = 255, MIN = 0
                    if (h >= 0 && h < 60)
                    {
                        r = 255; g = (byte)(h / 60 * 255); b = 0;
                    }
                    else if (h >= 60 && h < 120)
                    {
                        r = (byte)((120 - h) / 60 * 255); g = 255; b = 0;
                    }
                    else if (h >= 120 && h < 180)
                    {
                        r = 0; g = 255; b = (byte)((h - 120) / 60 * 255);
                    }
                    else if (h >= 180 && h < 240)
                    {
                        r = 0; g = (byte)((240 - h) / 60 * 255); b = 255;
                    }
                    else if (h >= 240 && h < 300)
                    {
                        r = (byte)((h - 240) / 60 * 255); g = 0; b = 255;
                    }
                    else
                    {
                        r = 255; g = 0; b = (byte)((360 - h) / 60 * 255);
                    }

                    rgbValues[i] = b;
                    rgbValues[i + 1] = g;
                    rgbValues[i + 2] = r;
                    // rgbValues[i+3] = 255;
                }

                // Copy the RGB values back to the bitmap
                Marshal.Copy(rgbValues, 0, ptr, bytes);
            }
            finally
            {
                src.UnlockBits(bmpData); ;
            }
        }

        public static InterpolationMode GetConfiguredInterpolationMode()
        {
            switch ((EBitmapScalingMode)Properties.Settings.Default.BitmapScalingMode)
            {
                case EBitmapScalingMode.NearestNeighbor:
                    return InterpolationMode.NearestNeighbor;
                case EBitmapScalingMode.Linear:
                    return InterpolationMode.HighQualityBilinear;
                default:
                    return InterpolationMode.HighQualityBicubic;
            }
        }

        public static BitmapScalingMode GetConfiguredBitmapScalingMode()
        {
            switch ((EBitmapScalingMode)Properties.Settings.Default.BitmapScalingMode)
            {
                case EBitmapScalingMode.NearestNeighbor:
                    return BitmapScalingMode.NearestNeighbor;
                case EBitmapScalingMode.Linear:
                    return BitmapScalingMode.Linear;
                default:
                    return BitmapScalingMode.Fant;
            }
        }
    }
}
