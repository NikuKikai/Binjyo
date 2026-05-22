using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

using Rect = System.Drawing.Rectangle;

namespace Binjyo
{
    public struct FastCornerPoint
    {
        public FastCornerPoint(int x, int y, int score, byte intensity)
        {
            X = x;
            Y = y;
            Score = score;
            Intensity = intensity;
        }

        public int X { get; }
        public int Y { get; }
        public int Score { get; }
        public byte Intensity { get; }
    }

    public static class FastCornerDetector
    {
        private static readonly int[] circleOffsetX = new[] { 0, 1, 2, 3, 3, 3, 2, 1, 0, -1, -2, -3, -3, -3, -2, -1 };
        private static readonly int[] circleOffsetY = new[] { -3, -3, -2, -1, 0, 1, 2, 3, 3, 3, 2, 1, 0, -1, -2, -3 };

        public static List<FastCornerPoint> DetectCorners(
            Bitmap sourceBitmap,
            int threshold = 30,
            int contiguousArcLength = 9,
            int edgeBandPixels = int.MaxValue,
            int suppressionRadius = 4)
        {
            if (sourceBitmap == null)
                throw new ArgumentNullException(nameof(sourceBitmap));

            if (sourceBitmap.Width < 7 || sourceBitmap.Height < 7)
                return new List<FastCornerPoint>();

            Bitmap workingBitmap = sourceBitmap;
            bool disposeWorkingBitmap = false;
            if (sourceBitmap.PixelFormat != PixelFormat.Format32bppArgb)
            {
                workingBitmap = sourceBitmap.Clone(
                    new Rect(0, 0, sourceBitmap.Width, sourceBitmap.Height),
                    PixelFormat.Format32bppArgb);
                disposeWorkingBitmap = true;
            }

            try
            {
                int width = workingBitmap.Width;
                int height = workingBitmap.Height;
                byte[] grayscale = ExtractGrayscale(workingBitmap);
                int[] scoreMap = new int[width * height];

                for (int y = 3; y < height - 3; y++)
                {
                    int[] differences = new int[16];
                    for (int x = 3; x < width - 3; x++)
                    {
                        if (!IsInsideEdgeBand(width, height, x, y, edgeBandPixels))
                            continue;

                        int index = y * width + x;
                        int center = grayscale[index];
                        int highThreshold = center + threshold;
                        int lowThreshold = center - threshold;

                        int brighterCardinals = 0;
                        int darkerCardinals = 0;
                        TestCardinal(grayscale[(y - 3) * width + x], highThreshold, lowThreshold, ref brighterCardinals, ref darkerCardinals);
                        TestCardinal(grayscale[y * width + x + 3], highThreshold, lowThreshold, ref brighterCardinals, ref darkerCardinals);
                        TestCardinal(grayscale[(y + 3) * width + x], highThreshold, lowThreshold, ref brighterCardinals, ref darkerCardinals);
                        TestCardinal(grayscale[y * width + x - 3], highThreshold, lowThreshold, ref brighterCardinals, ref darkerCardinals);

                        if (brighterCardinals < 3 && darkerCardinals < 3)
                            continue;

                        for (int i = 0; i < 16; i++)
                        {
                            int sample = grayscale[(y + circleOffsetY[i]) * width + (x + circleOffsetX[i])];
                            differences[i] = sample - center;
                        }

                        int score = GetCornerScore(differences, threshold, contiguousArcLength);
                        if (score > 0)
                            scoreMap[index] = score;
                    }
                }

                return ApplyNonMaxSuppression(scoreMap, grayscale, width, height, suppressionRadius);
            }
            finally
            {
                if (disposeWorkingBitmap)
                    workingBitmap.Dispose();
            }
        }

        private static byte[] ExtractGrayscale(Bitmap bitmap)
        {
            Rect rect = new Rect(0, 0, bitmap.Width, bitmap.Height);
            BitmapData bitmapData = bitmap.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);

            try
            {
                int stride = bitmapData.Stride;
                int totalBytes = Math.Abs(stride) * bitmap.Height;
                byte[] pixels = new byte[totalBytes];
                byte[] grayscale = new byte[bitmap.Width * bitmap.Height];
                Marshal.Copy(bitmapData.Scan0, pixels, 0, totalBytes);

                for (int y = 0; y < bitmap.Height; y++)
                {
                    int sourceRow = y * stride;
                    int targetRow = y * bitmap.Width;
                    for (int x = 0; x < bitmap.Width; x++)
                    {
                        int pixelIndex = sourceRow + x * 4;
                        int blue = pixels[pixelIndex];
                        int green = pixels[pixelIndex + 1];
                        int red = pixels[pixelIndex + 2];
                        grayscale[targetRow + x] = (byte)((red * 77 + green * 150 + blue * 29) >> 8);
                    }
                }

                return grayscale;
            }
            finally
            {
                bitmap.UnlockBits(bitmapData);
            }
        }

        private static void TestCardinal(int sample, int highThreshold, int lowThreshold, ref int brighterCardinals, ref int darkerCardinals)
        {
            if (sample > highThreshold)
                brighterCardinals++;
            else if (sample < lowThreshold)
                darkerCardinals++;
        }

        private static int GetCornerScore(int[] differences, int threshold, int contiguousArcLength)
        {
            int bestScore = 0;
            for (int start = 0; start < 16; start++)
            {
                int minBrighter = int.MaxValue;
                bool brighterArc = true;
                for (int offset = 0; offset < contiguousArcLength; offset++)
                {
                    int value = differences[(start + offset) & 15];
                    if (value <= threshold)
                    {
                        brighterArc = false;
                        break;
                    }

                    if (value < minBrighter)
                        minBrighter = value;
                }

                if (brighterArc && minBrighter > bestScore)
                    bestScore = minBrighter;

                int minDarker = int.MaxValue;
                bool darkerArc = true;
                for (int offset = 0; offset < contiguousArcLength; offset++)
                {
                    int value = -differences[(start + offset) & 15];
                    if (value <= threshold)
                    {
                        darkerArc = false;
                        break;
                    }

                    if (value < minDarker)
                        minDarker = value;
                }

                if (darkerArc && minDarker > bestScore)
                    bestScore = minDarker;
            }

            return bestScore;
        }

        private static bool IsInsideEdgeBand(int width, int height, int x, int y, int edgeBandPixels)
        {
            if (edgeBandPixels == int.MaxValue)
                return true;

            int distanceToLeft = x;
            int distanceToTop = y;
            int distanceToRight = width - 1 - x;
            int distanceToBottom = height - 1 - y;
            int distanceToEdge = Math.Min(Math.Min(distanceToLeft, distanceToRight), Math.Min(distanceToTop, distanceToBottom));
            return distanceToEdge <= edgeBandPixels;
        }

        private static List<FastCornerPoint> ApplyNonMaxSuppression(int[] scoreMap, byte[] grayscale, int width, int height, int suppressionRadius)
        {
            var candidates = new List<FastCornerPoint>();
            for (int y = 3; y < height - 3; y++)
            {
                for (int x = 3; x < width - 3; x++)
                {
                    int index = y * width + x;
                    int score = scoreMap[index];
                    if (score <= 0)
                        continue;

                    bool isLocalMaximum = true;
                    for (int offsetY = -1; offsetY <= 1 && isLocalMaximum; offsetY++)
                    {
                        for (int offsetX = -1; offsetX <= 1; offsetX++)
                        {
                            if (offsetX == 0 && offsetY == 0)
                                continue;

                            int neighborScore = scoreMap[(y + offsetY) * width + (x + offsetX)];
                            if (neighborScore > score)
                            {
                                isLocalMaximum = false;
                                break;
                            }
                        }
                    }

                    if (isLocalMaximum)
                        candidates.Add(new FastCornerPoint(x, y, score, grayscale[index]));
                }
            }

            candidates.Sort((a, b) => b.Score.CompareTo(a.Score));

            var removed = new bool[candidates.Count];
            var result = new List<FastCornerPoint>();
            int suppressionRadiusSquared = suppressionRadius * suppressionRadius;

            for (int i = 0; i < candidates.Count; i++)
            {
                if (removed[i])
                    continue;

                FastCornerPoint current = candidates[i];
                result.Add(current);

                for (int j = i + 1; j < candidates.Count; j++)
                {
                    if (removed[j])
                        continue;

                    FastCornerPoint candidate = candidates[j];
                    int deltaX = current.X - candidate.X;
                    int deltaY = current.Y - candidate.Y;
                    if (deltaX * deltaX + deltaY * deltaY <= suppressionRadiusSquared)
                        removed[j] = true;
                }
            }

            return result;
        }
    }
}
