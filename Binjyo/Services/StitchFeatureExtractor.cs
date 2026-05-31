using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media.Imaging;
using Bitmap = System.Drawing.Bitmap;
using WpfPoint = System.Windows.Point;

namespace Binjyo
{
    public sealed class StitchAlignmentResult
    {
        public double TargetLeftOffset { get; set; }
        public double TargetTopOffset { get; set; }
        public double OverlapScore { get; set; }
        public HashSet<int> SourceMatchedIndices { get; } = new HashSet<int>();
        public HashSet<int> TargetMatchedIndices { get; } = new HashSet<int>();
        public bool IsValid { get; set; }
    }

    public static class StitchFeatureExtractor
    {
        private const int FastCornerThreshold = 50;
        private const int FeatureEdgeBandPixels = 150;
        private const int FeatureSuppressionRadius = 20;
        private const double FeatureMatchTolerance = 4.0;
        private const int FeatureIntensityTolerance = 24;
        private const int MinimumAlignmentMatchCount = 3;
        private const double MinimumFeatureOverlapScore = 0.18;
        private const int SignatureNeighborCount = 6;
        private const double SignatureNeighborDistance = 120.0;
        private const double SignatureVectorTolerance = 10.0;
        private const int MinimumSignatureMatchCount = 3;

        private sealed class PointSignature
        {
            public int PointIndex { get; set; }
            public List<Vector> NeighborVectors { get; } = new List<Vector>();
        }

        private sealed class AlignmentCandidate
        {
            public int SourceIndex { get; set; }
            public int TargetIndex { get; set; }
            public double OffsetX { get; set; }
            public double OffsetY { get; set; }
            public int SignatureMatchCount { get; set; }
        }

        public static List<FastCornerPoint> DetectFeaturePoints(WriteableBitmap bitmap)
        {
            if (bitmap == null)
                return new List<FastCornerPoint>();

            using (Bitmap sourceBitmap = Effects.ConvertWBitmapToGdi(bitmap))
            {
                return FastCornerDetector.DetectCorners(
                    sourceBitmap,
                    FastCornerThreshold,
                    9,
                    FeatureEdgeBandPixels,
                    FeatureSuppressionRadius);
            }
        }

        public static StitchAlignmentResult ComputeAlignment(
            SceneItem sourceItem,
            IReadOnlyList<FastCornerPoint> sourcePoints,
            SceneItem targetItem,
            IReadOnlyList<FastCornerPoint> targetPoints)
        {
            var result = new StitchAlignmentResult();
            if (sourceItem == null || targetItem == null || sourcePoints == null || targetPoints == null)
                return result;

            List<WpfPoint> sourcePositions = GetDisplayPositions(sourceItem, sourcePoints);
            List<WpfPoint> targetPositions = GetDisplayPositions(targetItem, targetPoints);
            List<PointSignature> sourceSignatures = BuildPointSignatures(sourcePositions);
            List<PointSignature> targetSignatures = BuildPointSignatures(targetPositions);
            List<AlignmentCandidate> candidates = BuildAlignmentCandidates(
                sourcePoints,
                sourceSignatures,
                sourcePositions,
                targetPoints,
                targetSignatures,
                targetPositions);

            if (candidates.Count == 0)
                return result;

            List<AlignmentCandidate> bestBucket = candidates
                .GroupBy(candidate => Tuple.Create(
                    (int)Math.Round(candidate.OffsetX / FeatureMatchTolerance),
                    (int)Math.Round(candidate.OffsetY / FeatureMatchTolerance)))
                .OrderByDescending(group => group.Sum(candidate => candidate.SignatureMatchCount))
                .ThenByDescending(group => group.Count())
                .First()
                .ToList();

            double averageOffsetX = 0;
            double averageOffsetY = 0;
            double totalWeight = 0;
            foreach (AlignmentCandidate candidate in bestBucket)
            {
                averageOffsetX += candidate.OffsetX * candidate.SignatureMatchCount;
                averageOffsetY += candidate.OffsetY * candidate.SignatureMatchCount;
                totalWeight += candidate.SignatureMatchCount;
            }

            if (totalWeight <= 0)
                return result;

            averageOffsetX /= totalWeight;
            averageOffsetY /= totalWeight;

            var usedSourceIndices = new HashSet<int>();
            var usedTargetIndices = new HashSet<int>();
            foreach (AlignmentCandidate candidate in bestBucket.OrderByDescending(item => item.SignatureMatchCount))
            {
                double deltaX = candidate.OffsetX - averageOffsetX;
                double deltaY = candidate.OffsetY - averageOffsetY;
                if (deltaX * deltaX + deltaY * deltaY > FeatureMatchTolerance * FeatureMatchTolerance)
                    continue;

                if (usedSourceIndices.Contains(candidate.SourceIndex) || usedTargetIndices.Contains(candidate.TargetIndex))
                    continue;

                result.SourceMatchedIndices.Add(candidate.SourceIndex);
                result.TargetMatchedIndices.Add(candidate.TargetIndex);
                usedSourceIndices.Add(candidate.SourceIndex);
                usedTargetIndices.Add(candidate.TargetIndex);
            }

            result.TargetLeftOffset = averageOffsetX;
            result.TargetTopOffset = averageOffsetY;
            result.OverlapScore = ComputeOverlapScore(
                sourcePositions,
                sourceItem.GetBaseWidth(),
                sourceItem.GetBaseHeight(),
                targetPositions,
                targetItem.GetBaseWidth(),
                targetItem.GetBaseHeight(),
                averageOffsetX,
                averageOffsetY,
                result.SourceMatchedIndices.Count);
            result.IsValid =
                result.SourceMatchedIndices.Count >= MinimumAlignmentMatchCount &&
                result.OverlapScore >= MinimumFeatureOverlapScore;

            if (!result.IsValid)
            {
                result.SourceMatchedIndices.Clear();
                result.TargetMatchedIndices.Clear();
            }

            return result;
        }

        private static List<WpfPoint> GetDisplayPositions(SceneItem item, IReadOnlyList<FastCornerPoint> points)
        {
            double dpiFactor = item.DpiFactor <= 0 ? 1.0 : item.DpiFactor;
            return points
                .Select(point => new WpfPoint(point.X / dpiFactor, point.Y / dpiFactor))
                .ToList();
        }

        private static List<PointSignature> BuildPointSignatures(IReadOnlyList<WpfPoint> points)
        {
            var result = new List<PointSignature>(points.Count);
            for (int i = 0; i < points.Count; i++)
            {
                var signature = new PointSignature
                {
                    PointIndex = i
                };

                List<Tuple<double, Vector>> neighbors = new List<Tuple<double, Vector>>();
                for (int j = 0; j < points.Count; j++)
                {
                    if (i == j)
                        continue;

                    double dx = points[j].X - points[i].X;
                    double dy = points[j].Y - points[i].Y;
                    double distanceSquared = dx * dx + dy * dy;
                    if (distanceSquared > SignatureNeighborDistance * SignatureNeighborDistance)
                        continue;

                    neighbors.Add(Tuple.Create(distanceSquared, new Vector(dx, dy)));
                }

                foreach (Tuple<double, Vector> neighbor in neighbors.OrderBy(item => item.Item1).Take(SignatureNeighborCount))
                    signature.NeighborVectors.Add(neighbor.Item2);

                result.Add(signature);
            }

            return result;
        }

        private static List<AlignmentCandidate> BuildAlignmentCandidates(
            IReadOnlyList<FastCornerPoint> sourcePoints,
            IReadOnlyList<PointSignature> sourceSignatures,
            IReadOnlyList<WpfPoint> sourcePositions,
            IReadOnlyList<FastCornerPoint> targetPoints,
            IReadOnlyList<PointSignature> targetSignatures,
            IReadOnlyList<WpfPoint> targetPositions)
        {
            var candidates = new List<AlignmentCandidate>();
            List<int>[] targetBuckets = BuildIntensityBuckets(targetPoints);
            foreach (PointSignature sourceSignature in sourceSignatures)
            {
                if (sourceSignature.NeighborVectors.Count < MinimumSignatureMatchCount)
                    continue;

                FastCornerPoint sourcePointData = sourcePoints[sourceSignature.PointIndex];
                foreach (int targetIndex in GetCandidateTargetIndices(targetBuckets, sourcePointData.Intensity))
                {
                    PointSignature targetSignature = targetSignatures[targetIndex];
                    if (targetSignature.NeighborVectors.Count < MinimumSignatureMatchCount)
                        continue;

                    int matchCount = CountSignatureMatches(sourceSignature.NeighborVectors, targetSignature.NeighborVectors);
                    if (matchCount < MinimumSignatureMatchCount)
                        continue;

                    WpfPoint sourcePoint = sourcePositions[sourceSignature.PointIndex];
                    WpfPoint targetPoint = targetPositions[targetSignature.PointIndex];
                    candidates.Add(new AlignmentCandidate
                    {
                        SourceIndex = sourceSignature.PointIndex,
                        TargetIndex = targetSignature.PointIndex,
                        OffsetX = sourcePoint.X - targetPoint.X,
                        OffsetY = sourcePoint.Y - targetPoint.Y,
                        SignatureMatchCount = matchCount
                    });
                }
            }

            return candidates;
        }

        private static List<int>[] BuildIntensityBuckets(IReadOnlyList<FastCornerPoint> points)
        {
            var buckets = new List<int>[256];
            for (int i = 0; i < points.Count; i++)
            {
                int intensity = points[i].Intensity;
                if (buckets[intensity] == null)
                    buckets[intensity] = new List<int>();
                buckets[intensity].Add(i);
            }

            return buckets;
        }

        private static IEnumerable<int> GetCandidateTargetIndices(List<int>[] targetBuckets, byte intensity)
        {
            int min = Math.Max(0, intensity - FeatureIntensityTolerance);
            int max = Math.Min(255, intensity + FeatureIntensityTolerance);
            for (int value = min; value <= max; value++)
            {
                List<int> bucket = targetBuckets[value];
                if (bucket == null)
                    continue;

                for (int i = 0; i < bucket.Count; i++)
                    yield return bucket[i];
            }
        }

        private static int CountSignatureMatches(
            IReadOnlyList<Vector> sourceVectors,
            IReadOnlyList<Vector> targetVectors)
        {
            int matchCount = 0;
            var usedTargetIndices = new HashSet<int>();
            double toleranceSquared = SignatureVectorTolerance * SignatureVectorTolerance;

            for (int i = 0; i < sourceVectors.Count; i++)
            {
                Vector sourceVector = sourceVectors[i];
                double bestDistanceSquared = double.MaxValue;
                int bestTargetIndex = -1;

                for (int j = 0; j < targetVectors.Count; j++)
                {
                    if (usedTargetIndices.Contains(j))
                        continue;

                    Vector targetVector = targetVectors[j];
                    double deltaX = sourceVector.X - targetVector.X;
                    double deltaY = sourceVector.Y - targetVector.Y;
                    double distanceSquared = deltaX * deltaX + deltaY * deltaY;
                    if (distanceSquared > toleranceSquared || distanceSquared >= bestDistanceSquared)
                        continue;

                    bestDistanceSquared = distanceSquared;
                    bestTargetIndex = j;
                }

                if (bestTargetIndex >= 0)
                {
                    usedTargetIndices.Add(bestTargetIndex);
                    matchCount++;
                }
            }

            return matchCount;
        }

        private static double ComputeOverlapScore(
            IReadOnlyList<WpfPoint> sourcePositions,
            double sourceWidth,
            double sourceHeight,
            IReadOnlyList<WpfPoint> targetPositions,
            double targetWidth,
            double targetHeight,
            double targetOffsetX,
            double targetOffsetY,
            int matchedPairCount)
        {
            var sourceRect = new Rect(0, 0, sourceWidth, sourceHeight);
            var targetRect = new Rect(targetOffsetX, targetOffsetY, targetWidth, targetHeight);
            Rect overlapRect = Rect.Intersect(sourceRect, targetRect);
            if (overlapRect.IsEmpty || overlapRect.Width <= 0 || overlapRect.Height <= 0)
                return 0;

            int sourceOverlapCount = sourcePositions.Count(point => overlapRect.Contains(point));
            int targetOverlapCount = targetPositions.Count(point => overlapRect.Contains(new WpfPoint(point.X + targetOffsetX, point.Y + targetOffsetY)));
            int unionCount = sourceOverlapCount + targetOverlapCount - matchedPairCount;
            if (unionCount <= 0)
                return 0;

            return (double)matchedPairCount / unionCount;
        }
    }
}
