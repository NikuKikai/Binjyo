using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Binjyo
{
    public partial class Memo
    {
        private const int FastCornerThreshold = 50;
        private const int FeatureEdgeBandPixels = 150;
        private const int FeatureSuppressionRadius = 20;
        private const double FeaturePointRadius = 2.5;
        private const double FeatureMatchTolerance = 4.0;
        private const double FeatureAlignmentSnapDistance = 28.0;
        private const int FeatureIntensityTolerance = 24;
        private const int MinimumAlignmentMatchCount = 3;
        private const double MinimumFeatureOverlapScore = 0.18;
        private const int MaximumFeatureMatchLines = 6;
        private const int SignatureNeighborCount = 6;
        private const double SignatureNeighborDistance = 120.0;
        private const double SignatureVectorTolerance = 10.0;
        private const int MinimumSignatureMatchCount = 3;

        private static readonly Dictionary<Tuple<Memo, Memo>, MemoAlignmentCacheEntry> featureAlignmentCache =
            new Dictionary<Tuple<Memo, Memo>, MemoAlignmentCacheEntry>();

        private static FeatureMatchOverlayWindow featureMatchOverlayWindow = null;

        private readonly List<FastCornerPoint> featurePoints = new List<FastCornerPoint>();
        private bool showFeaturePoints = false;
        private bool areFeaturePointsDirty = true;
        private List<System.Windows.Point> cachedFeatureLocalDisplayPositions = null;
        private List<PointSignature> cachedFeatureSignatures = null;
        private double cachedFeatureGeometryScale = double.NaN;
        private double cachedFeatureGeometryDpi = double.NaN;

        private sealed class MemoAlignmentCacheEntry
        {
            public double TargetLeftOffset { get; set; }
            public double TargetTopOffset { get; set; }
            public double OverlapScore { get; set; }
            public HashSet<int> SourceMatchedIndices { get; set; } = new HashSet<int>();
            public HashSet<int> TargetMatchedIndices { get; set; } = new HashSet<int>();
            public List<MatchedFeaturePair> MatchedPairs { get; set; } = new List<MatchedFeaturePair>();
            public List<MatchedFeaturePair> RepresentativeMatchedPairs { get; set; } = new List<MatchedFeaturePair>();
            public bool IsValid { get; set; }
        }

        private sealed class MatchedFeaturePair
        {
            public int SourceIndex { get; set; }
            public int TargetIndex { get; set; }
        }

        private sealed class PointSignature
        {
            public int PointIndex { get; set; }
            public List<System.Windows.Vector> NeighborVectors { get; set; } = new List<System.Windows.Vector>();
        }

        private sealed class AlignmentCandidate
        {
            public int SourceIndex { get; set; }
            public int TargetIndex { get; set; }
            public double OffsetX { get; set; }
            public double OffsetY { get; set; }
            public int SignatureMatchCount { get; set; }
        }

        private void ToggleFeaturePoints()
        {
            isFeaturePointModeEnabled = !isFeaturePointModeEnabled;
            foreach (Memo memo in GetAllMemos())
            {
                memo.showFeaturePoints = isFeaturePointModeEnabled;
                if (memo.showFeaturePoints)
                    memo.EnsureFeaturePoints();
            }

            RefreshAllMemoFeatureOverlays();
            ShowCenterInfoFading("Feature Points", isFeaturePointModeEnabled ? "On" : "Off");
        }

        private void MarkFeaturePointsDirty()
        {
            areFeaturePointsDirty = true;
            InvalidateFeatureGeometryCache();
        }

        private void HandleDisplayedBitmapUpdated(Bitmap displayedBitmap)
        {
            if (displayedBitmap == null || bitmap == null)
            {
                featurePoints.Clear();
                areFeaturePointsDirty = false;
                RefreshAllMemoFeatureOverlays();
                return;
            }

            EnsureFeaturePoints();
            RefreshAllMemoFeatureOverlays();
        }

        private void EnsureFeaturePoints()
        {
            if (!showFeaturePoints || !areFeaturePointsDirty)
                return;

            RecomputeFeaturePoints(bitmap);
        }

        private void RecomputeFeaturePoints(Bitmap sourceBitmap)
        {
            var stopwatch = Stopwatch.StartNew();
            featurePoints.Clear();
            featurePoints.AddRange(
                FastCornerDetector.DetectCorners(sourceBitmap, FastCornerThreshold, 9, FeatureEdgeBandPixels, FeatureSuppressionRadius));
            stopwatch.Stop();
            Console.WriteLine($"FAST detected {featurePoints.Count} corners in {stopwatch.Elapsed.TotalMilliseconds:F1} ms");
            areFeaturePointsDirty = false;
            InvalidateFeatureGeometryCache();
        }

        private void RenderFeatureOverlay()
        {
            if (featureOverlay == null)
                return;

            featureOverlay.Children.Clear();
            UpdateFeatureOverlayTransform();

            if (!ShouldShowFeaturePointsOnThisMemo() || bitmap == null)
                return;

            EnsureFeaturePoints();
            if (featurePoints.Count == 0)
                return;

            Memo focusedMemo = GetFocusedMemo();
            bool isFocusedMemo = ReferenceEquals(this, focusedMemo);
            HashSet<int> matchedPointIndices = GetMatchedFeaturePointIndices(focusedMemo);
            if (!isFocusedMemo && matchedPointIndices.Count == 0)
                return;

            var unmatchedGeometry = new GeometryGroup();
            var matchedGeometry = new GeometryGroup();
            double radius = FeaturePointRadius / dpiFactor;

            for (int i = 0; i < featurePoints.Count; i++)
            {
                FastCornerPoint point = featurePoints[i];
                System.Windows.Point displayedPoint = MapOriginalPointToDisplayed(point.X, point.Y);
                double x = displayedPoint.X / dpiFactor;
                double y = displayedPoint.Y / dpiFactor;
                var ellipse = new EllipseGeometry(new System.Windows.Point(x, y), radius, radius);

                if (matchedPointIndices.Contains(i))
                {
                    matchedGeometry.Children.Add(ellipse);
                }
                else if (isFocusedMemo)
                {
                    unmatchedGeometry.Children.Add(ellipse);
                }
            }

            if (unmatchedGeometry.Children.Count > 0)
            {
                featureOverlay.Children.Add(new Path
                {
                    Data = unmatchedGeometry,
                    Fill = new SolidColorBrush(System.Windows.Media.Color.FromArgb(255, 0, 255, 160))
                });
            }

            if (matchedGeometry.Children.Count > 0)
            {
                featureOverlay.Children.Add(new Path
                {
                    Data = matchedGeometry,
                    Fill = new SolidColorBrush(System.Windows.Media.Color.FromArgb(255, 255, 64, 64))
                });
            }
        }

        private void UpdateFeatureOverlayTransform()
        {
            if (featureOverlay == null || isClosing)
                return;

            double safeScale = Math.Max(scale, 0.0001);
            featureOverlay.Width = Width / safeScale;
            featureOverlay.Height = Height / safeScale;
            featureOverlay.RenderTransformOrigin = new System.Windows.Point(0, 0);
            featureOverlay.RenderTransform = new ScaleTransform(scale, scale);
        }

        private HashSet<int> GetMatchedFeaturePointIndices(Memo focusedMemo)
        {
            var matched = new HashSet<int>();
            if (!CanParticipateInFeatureMatching() || focusedMemo == null)
                return matched;

            if (ReferenceEquals(this, focusedMemo))
            {
                foreach (Memo other in GetAllMemos())
                {
                    if (ReferenceEquals(other, this) || !other.CanParticipateInFeatureMatching())
                        continue;
                    if (!DoDisplayedBoundsOverlap(other))
                        continue;

                    MemoAlignmentCacheEntry cacheEntry = GetFeatureAlignmentCache(this, other);
                    if (cacheEntry == null || !cacheEntry.IsValid)
                        continue;

                    foreach (int index in cacheEntry.SourceMatchedIndices)
                        matched.Add(index);
                }

                return matched;
            }

            MemoAlignmentCacheEntry focusedCache = GetFeatureAlignmentCache(this, focusedMemo);
            if (focusedCache == null || !focusedCache.IsValid)
                return matched;

            foreach (int index in focusedCache.SourceMatchedIndices)
                matched.Add(index);

            return matched;
        }

        private bool CanParticipateInFeatureMatching()
        {
            return showFeaturePoints &&
                   bitmap != null &&
                   bitmapTransformed != null &&
                   geometryTransformHistory.Count == 0 &&
                   Scene.DisplayMode != EDisplayMode.Minimized;
        }

        private bool ShouldShowFeaturePointsOnThisMemo()
        {
            if (!showFeaturePoints || !isFeaturePointModeEnabled || bitmapTransformed == null)
                return false;

            Memo focusedMemo = GetFocusedMemo();
            if (focusedMemo == null)
                return false;

            if (ReferenceEquals(this, focusedMemo))
                return true;

            if (!focusedMemo.DoDisplayedBoundsOverlap(this))
                return false;

            MemoAlignmentCacheEntry cacheEntry = GetFeatureAlignmentCache(focusedMemo, this);
            return cacheEntry != null && cacheEntry.IsValid;
        }

        private static MemoAlignmentCacheEntry GetFeatureAlignmentCache(Memo source, Memo target)
        {
            featureAlignmentCache.TryGetValue(Tuple.Create(source, target), out MemoAlignmentCacheEntry cacheEntry);
            return cacheEntry;
        }

        private bool DoDisplayedBoundsOverlap(Memo other)
        {
            double overlapLeft = Math.Max(Left, other.Left);
            double overlapTop = Math.Max(Top, other.Top);
            double overlapRight = Math.Min(Left + Width, other.Left + other.Width);
            double overlapBottom = Math.Min(Top + Height, other.Top + other.Height);
            return overlapRight > overlapLeft && overlapBottom > overlapTop;
        }

        private static void InvalidateFeatureAlignmentCachesFor(Memo memo)
        {
            var keysToRemove = featureAlignmentCache.Keys
                .Where(key => ReferenceEquals(key.Item1, memo) || ReferenceEquals(key.Item2, memo))
                .ToList();
            foreach (var key in keysToRemove)
                featureAlignmentCache.Remove(key);
        }

        private static void RefreshAllMemoFeatureOverlays()
        {
            EnsureFeatureAlignmentCachesForVisibleMemos();
            foreach (Memo memo in GetAllMemos())
            {
                if (memo.isClosing)
                    continue;

                memo.RenderFeatureOverlay();
            }

            RefreshFeatureMatchOverlayWindow();
        }

        private static void EnsureFeatureAlignmentCachesForVisibleMemos()
        {
            Memo focusedMemo = GetFocusedMemo();
            if (focusedMemo == null || !focusedMemo.CanParticipateInFeatureMatching())
                return;

            foreach (Memo other in GetAllMemos())
            {
                if (ReferenceEquals(other, focusedMemo) || !other.CanParticipateInFeatureMatching())
                    continue;

                if (!focusedMemo.DoDisplayedBoundsOverlap(other))
                    continue;

                if (GetFeatureAlignmentCache(focusedMemo, other) != null)
                    continue;

                focusedMemo.ComputeAndCacheFeatureAlignment(other);
            }
        }

        private void ComputeAndCacheFeatureAlignment(Memo other)
        {
            EnsureFeaturePoints();
            other.EnsureFeaturePoints();

            var stopwatch = Stopwatch.StartNew();
            List<System.Windows.Point> sourcePositions = GetFeatureLocalDisplayPositions();
            List<System.Windows.Point> targetPositions = other.GetFeatureLocalDisplayPositions();
            List<PointSignature> sourceSignatures = GetFeaturePointSignatures();
            List<PointSignature> targetSignatures = other.GetFeaturePointSignatures();
            List<AlignmentCandidate> candidates = BuildAlignmentCandidates(
                featurePoints,
                sourceSignatures,
                sourcePositions,
                other.featurePoints,
                targetSignatures,
                targetPositions);

            MemoAlignmentCacheEntry forwardEntry = new MemoAlignmentCacheEntry();
            MemoAlignmentCacheEntry reverseEntry = new MemoAlignmentCacheEntry();

            if (candidates.Count > 0)
            {
                var bucketGroups = candidates
                    .GroupBy(candidate => Tuple.Create(
                        (int)Math.Round(candidate.OffsetX / FeatureMatchTolerance),
                        (int)Math.Round(candidate.OffsetY / FeatureMatchTolerance)))
                    .OrderByDescending(group => group.Sum(candidate => candidate.SignatureMatchCount))
                    .ThenByDescending(group => group.Count())
                    .ToList();

                List<AlignmentCandidate> bestBucket = bucketGroups.First().ToList();
                double averageOffsetX = 0;
                double averageOffsetY = 0;
                double totalWeight = 0;
                foreach (AlignmentCandidate candidate in bestBucket)
                {
                    averageOffsetX += candidate.OffsetX * candidate.SignatureMatchCount;
                    averageOffsetY += candidate.OffsetY * candidate.SignatureMatchCount;
                    totalWeight += candidate.SignatureMatchCount;
                }

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

                    forwardEntry.SourceMatchedIndices.Add(candidate.SourceIndex);
                    forwardEntry.TargetMatchedIndices.Add(candidate.TargetIndex);
                    forwardEntry.MatchedPairs.Add(new MatchedFeaturePair
                    {
                        SourceIndex = candidate.SourceIndex,
                        TargetIndex = candidate.TargetIndex
                    });
                    usedSourceIndices.Add(candidate.SourceIndex);
                    usedTargetIndices.Add(candidate.TargetIndex);
                }

                forwardEntry.TargetLeftOffset = averageOffsetX;
                forwardEntry.TargetTopOffset = averageOffsetY;
                forwardEntry.OverlapScore = ComputeOverlapScore(sourcePositions, Width, Height, targetPositions, other.Width, other.Height, averageOffsetX, averageOffsetY, forwardEntry.MatchedPairs.Count);
                forwardEntry.IsValid =
                    forwardEntry.MatchedPairs.Count >= MinimumAlignmentMatchCount &&
                    forwardEntry.OverlapScore >= MinimumFeatureOverlapScore;

                if (!forwardEntry.IsValid)
                {
                    forwardEntry.SourceMatchedIndices.Clear();
                    forwardEntry.TargetMatchedIndices.Clear();
                    forwardEntry.MatchedPairs.Clear();
                }
                else
                {
                    forwardEntry.RepresentativeMatchedPairs = SelectRepresentativeMatchedPairs(
                        featurePoints,
                        sourcePositions,
                        forwardEntry.MatchedPairs);
                }
            }

            reverseEntry.TargetLeftOffset = -forwardEntry.TargetLeftOffset;
            reverseEntry.TargetTopOffset = -forwardEntry.TargetTopOffset;
            reverseEntry.OverlapScore = forwardEntry.OverlapScore;
            reverseEntry.SourceMatchedIndices = new HashSet<int>(forwardEntry.TargetMatchedIndices);
            reverseEntry.TargetMatchedIndices = new HashSet<int>(forwardEntry.SourceMatchedIndices);
            reverseEntry.MatchedPairs = forwardEntry.MatchedPairs
                .Select(pair => new MatchedFeaturePair
                {
                    SourceIndex = pair.TargetIndex,
                    TargetIndex = pair.SourceIndex
                })
                .ToList();
            reverseEntry.RepresentativeMatchedPairs = forwardEntry.RepresentativeMatchedPairs
                .Select(pair => new MatchedFeaturePair
                {
                    SourceIndex = pair.TargetIndex,
                    TargetIndex = pair.SourceIndex
                })
                .ToList();
            reverseEntry.IsValid = forwardEntry.IsValid;

            featureAlignmentCache[Tuple.Create(this, other)] = forwardEntry;
            featureAlignmentCache[Tuple.Create(other, this)] = reverseEntry;

            stopwatch.Stop();
            Console.WriteLine(
                $"Feature alignment {GetHashCode()} -> {other.GetHashCode()} in {stopwatch.Elapsed.TotalMilliseconds:F1} ms, matched {forwardEntry.MatchedPairs.Count} points, overlap {forwardEntry.OverlapScore:F3}, success {forwardEntry.IsValid}");
        }

        private static double ComputeOverlapScore(
            IReadOnlyList<System.Windows.Point> sourcePositions,
            double sourceWidth,
            double sourceHeight,
            IReadOnlyList<System.Windows.Point> targetPositions,
            double targetWidth,
            double targetHeight,
            double targetOffsetX,
            double targetOffsetY,
            int matchedPairCount)
        {
            var sourceRect = new System.Windows.Rect(0, 0, sourceWidth, sourceHeight);
            var targetRect = new System.Windows.Rect(targetOffsetX, targetOffsetY, targetWidth, targetHeight);
            System.Windows.Rect overlapRect = System.Windows.Rect.Intersect(sourceRect, targetRect);
            if (overlapRect.IsEmpty || overlapRect.Width <= 0 || overlapRect.Height <= 0)
                return 0;

            int sourceOverlapCount = sourcePositions.Count(point => overlapRect.Contains(point));
            int targetOverlapCount = targetPositions.Count(point => overlapRect.Contains(new System.Windows.Point(point.X + targetOffsetX, point.Y + targetOffsetY)));
            int unionCount = sourceOverlapCount + targetOverlapCount - matchedPairCount;
            if (unionCount <= 0)
                return 0;

            return (double)matchedPairCount / unionCount;
        }

        private static List<PointSignature> BuildPointSignatures(IReadOnlyList<System.Windows.Point> points)
        {
            var result = new List<PointSignature>(points.Count);
            for (int i = 0; i < points.Count; i++)
            {
                var signature = new PointSignature
                {
                    PointIndex = i
                };

                List<Tuple<double, System.Windows.Vector>> neighbors = new List<Tuple<double, System.Windows.Vector>>();
                for (int j = 0; j < points.Count; j++)
                {
                    if (i == j)
                        continue;

                    double dx = points[j].X - points[i].X;
                    double dy = points[j].Y - points[i].Y;
                    double distanceSquared = dx * dx + dy * dy;
                    if (distanceSquared > SignatureNeighborDistance * SignatureNeighborDistance)
                        continue;

                    neighbors.Add(Tuple.Create(distanceSquared, new System.Windows.Vector(dx, dy)));
                }

                foreach (var neighbor in neighbors.OrderBy(item => item.Item1).Take(SignatureNeighborCount))
                    signature.NeighborVectors.Add(neighbor.Item2);

                result.Add(signature);
            }

            return result;
        }

        private void InvalidateFeatureGeometryCache()
        {
            cachedFeatureLocalDisplayPositions = null;
            cachedFeatureSignatures = null;
            cachedFeatureGeometryScale = double.NaN;
            cachedFeatureGeometryDpi = double.NaN;
        }

        private List<System.Windows.Point> GetFeatureLocalDisplayPositions()
        {
            EnsureFeaturePoints();
            if (cachedFeatureLocalDisplayPositions != null &&
                Math.Abs(cachedFeatureGeometryScale - scale) < 0.0001 &&
                Math.Abs(cachedFeatureGeometryDpi - dpiFactor) < 0.0001)
            {
                return cachedFeatureLocalDisplayPositions;
            }

            cachedFeatureLocalDisplayPositions = featurePoints
                .Select(GetFeaturePointLocalDisplayPosition)
                .ToList();
            cachedFeatureSignatures = null;
            cachedFeatureGeometryScale = scale;
            cachedFeatureGeometryDpi = dpiFactor;
            return cachedFeatureLocalDisplayPositions;
        }

        private List<PointSignature> GetFeaturePointSignatures()
        {
            List<System.Windows.Point> positions = GetFeatureLocalDisplayPositions();
            if (cachedFeatureSignatures == null)
                cachedFeatureSignatures = BuildPointSignatures(positions);
            return cachedFeatureSignatures;
        }

        private static List<AlignmentCandidate> BuildAlignmentCandidates(
            IReadOnlyList<FastCornerPoint> sourcePoints,
            IReadOnlyList<PointSignature> sourceSignatures,
            IReadOnlyList<System.Windows.Point> sourcePositions,
            IReadOnlyList<FastCornerPoint> targetPoints,
            IReadOnlyList<PointSignature> targetSignatures,
            IReadOnlyList<System.Windows.Point> targetPositions)
        {
            var candidates = new List<AlignmentCandidate>();
            var targetBuckets = BuildIntensityBuckets(targetPoints);
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

                    System.Windows.Point sourcePoint = sourcePositions[sourceSignature.PointIndex];
                    System.Windows.Point targetPoint = targetPositions[targetSignature.PointIndex];
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
            IReadOnlyList<System.Windows.Vector> sourceVectors,
            IReadOnlyList<System.Windows.Vector> targetVectors)
        {
            int matchCount = 0;
            var usedTargetIndices = new HashSet<int>();
            double toleranceSquared = SignatureVectorTolerance * SignatureVectorTolerance;

            for (int i = 0; i < sourceVectors.Count; i++)
            {
                System.Windows.Vector sourceVector = sourceVectors[i];
                double bestDistanceSquared = double.MaxValue;
                int bestTargetIndex = -1;

                for (int j = 0; j < targetVectors.Count; j++)
                {
                    if (usedTargetIndices.Contains(j))
                        continue;

                    System.Windows.Vector targetVector = targetVectors[j];
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

        private System.Windows.Point GetFeaturePointScreenPosition(FastCornerPoint point)
        {
            System.Windows.Point displayedPoint = MapOriginalPointToDisplayed(point.X, point.Y);
            return new System.Windows.Point(
                Left + displayedPoint.X / dpiFactor * scale,
                Top + displayedPoint.Y / dpiFactor * scale);
        }

        private System.Windows.Point GetFeaturePointLocalDisplayPosition(FastCornerPoint point)
        {
            System.Windows.Point displayedPoint = MapOriginalPointToDisplayed(point.X, point.Y);
            return new System.Windows.Point(
                displayedPoint.X / dpiFactor * scale,
                displayedPoint.Y / dpiFactor * scale);
        }

        private static bool TryGetFeatureAlignmentSnapOffset(
            IDictionary<Memo, System.Windows.Point> targetPositions,
            HashSet<Memo> movingSet,
            out double offsetX,
            out double offsetY)
        {
            offsetX = 0;
            offsetY = 0;
            double bestDistanceSquared = double.MaxValue;
            bool found = false;

            foreach (var movingItem in targetPositions)
            {
                Memo movingMemo = movingItem.Key;
                System.Windows.Point proposedPosition = movingItem.Value;

                foreach (Memo stationaryMemo in GetAllMemos())
                {
                    if (movingSet.Contains(stationaryMemo) || ReferenceEquals(movingMemo, stationaryMemo))
                        continue;

                    MemoAlignmentCacheEntry cacheEntry = GetFeatureAlignmentCache(stationaryMemo, movingMemo);
                    if (cacheEntry == null || !cacheEntry.IsValid)
                        continue;

                    double targetLeft = stationaryMemo.anchorLeft + cacheEntry.TargetLeftOffset;
                    double targetTop = stationaryMemo.anchorTop + cacheEntry.TargetTopOffset;
                    double candidateOffsetX = targetLeft - proposedPosition.X;
                    double candidateOffsetY = targetTop - proposedPosition.Y;
                    if (Math.Abs(candidateOffsetX) > FeatureAlignmentSnapDistance || Math.Abs(candidateOffsetY) > FeatureAlignmentSnapDistance)
                        continue;

                    double distanceSquared = candidateOffsetX * candidateOffsetX + candidateOffsetY * candidateOffsetY;
                    if (distanceSquared < bestDistanceSquared)
                    {
                        bestDistanceSquared = distanceSquared;
                        offsetX = candidateOffsetX;
                        offsetY = candidateOffsetY;
                        found = true;
                    }
                }
            }

            return found;
        }

        public static Memo GetFocusedMemo()
        {
            return GetAllMemos()
                .OrderByDescending(memo => memo.lastFocusOrder)
                .FirstOrDefault();
        }

        private static void RefreshFeatureMatchOverlayWindow()
        {
            if (!isFeaturePointModeEnabled || !Properties.Settings.Default.ShowFeatureMatchLines)
            {
                HideFeatureMatchOverlayWindow();
                return;
            }

            Memo focusedMemo = GetFocusedMemo();
            if (focusedMemo == null || !focusedMemo.CanParticipateInFeatureMatching())
            {
                HideFeatureMatchOverlayWindow();
                return;
            }

            var lines = new List<Tuple<System.Windows.Point, System.Windows.Point>>();
            foreach (Memo other in GetAllMemos())
            {
                if (ReferenceEquals(other, focusedMemo) || !other.CanParticipateInFeatureMatching())
                    continue;
                if (!focusedMemo.DoDisplayedBoundsOverlap(other))
                    continue;

                MemoAlignmentCacheEntry cacheEntry = GetFeatureAlignmentCache(focusedMemo, other);
                if (cacheEntry == null || !cacheEntry.IsValid)
                    continue;

                foreach (MatchedFeaturePair pair in cacheEntry.RepresentativeMatchedPairs)
                {
                    if (pair.SourceIndex < 0 || pair.SourceIndex >= focusedMemo.featurePoints.Count ||
                        pair.TargetIndex < 0 || pair.TargetIndex >= other.featurePoints.Count)
                    {
                        continue;
                    }

                    lines.Add(Tuple.Create(
                        focusedMemo.GetFeaturePointScreenPosition(focusedMemo.featurePoints[pair.SourceIndex]),
                        other.GetFeaturePointScreenPosition(other.featurePoints[pair.TargetIndex])));
                }
            }

            if (lines.Count == 0)
            {
                HideFeatureMatchOverlayWindow();
                return;
            }

            if (featureMatchOverlayWindow == null)
                featureMatchOverlayWindow = new FeatureMatchOverlayWindow();

            featureMatchOverlayWindow.EnsureShown();
            featureMatchOverlayWindow.UpdateLines(lines);
            featureMatchOverlayWindow.SetOverlayOpacity(IsAnyMemoDragging() ? 1 : 0);
        }

        private static List<MatchedFeaturePair> SelectRepresentativeMatchedPairs(
            IReadOnlyList<FastCornerPoint> sourceFeaturePoints,
            IReadOnlyList<System.Windows.Point> sourcePositions,
            IReadOnlyList<MatchedFeaturePair> matchedPairs)
        {
            if (matchedPairs == null || matchedPairs.Count <= MaximumFeatureMatchLines)
                return matchedPairs?.ToList() ?? new List<MatchedFeaturePair>();

            List<MatchedFeaturePair> orderedPairs = matchedPairs
                .OrderByDescending(pair => sourceFeaturePoints[pair.SourceIndex].Score)
                .ToList();
            var selectedPairs = new List<MatchedFeaturePair>();
            var selectedPositions = new List<System.Windows.Point>();

            MatchedFeaturePair firstPair = orderedPairs[0];
            selectedPairs.Add(firstPair);
            selectedPositions.Add(sourcePositions[firstPair.SourceIndex]);

            while (selectedPairs.Count < MaximumFeatureMatchLines && selectedPairs.Count < orderedPairs.Count)
            {
                MatchedFeaturePair bestPair = null;
                double bestDistance = double.NegativeInfinity;

                foreach (MatchedFeaturePair candidate in orderedPairs)
                {
                    if (selectedPairs.Contains(candidate))
                        continue;

                    System.Windows.Point candidatePosition = sourcePositions[candidate.SourceIndex];
                    double nearestDistanceSquared = selectedPositions
                        .Select(position =>
                        {
                            double dx = candidatePosition.X - position.X;
                            double dy = candidatePosition.Y - position.Y;
                            return dx * dx + dy * dy;
                        })
                        .Min();

                    if (nearestDistanceSquared > bestDistance)
                    {
                        bestDistance = nearestDistanceSquared;
                        bestPair = candidate;
                    }
                }

                if (bestPair == null)
                    break;

                selectedPairs.Add(bestPair);
                selectedPositions.Add(sourcePositions[bestPair.SourceIndex]);
            }

            return selectedPairs;
        }

        private static void HideFeatureMatchOverlayWindow()
        {
            if (featureMatchOverlayWindow == null)
                return;

            featureMatchOverlayWindow.HideOverlay();
        }

        public static void RefreshAllFeatureVisuals()
        {
            RefreshAllMemoFeatureOverlays();
        }

        private static bool IsAnyMemoDragging()
        {
            return GetAllMemos().Any(memo => memo.isdrag);
        }

        private System.Windows.Point MapOriginalPointToDisplayed(double originalX, double originalY)
        {
            double x = originalX;
            double y = originalY;
            int currentWidth = originalBitmapWidth;
            int currentHeight = originalBitmapHeight;

            foreach (char transform in geometryTransformHistory)
            {
                switch (transform)
                {
                    case 'H':
                        x = currentWidth - 1 - x;
                        break;
                    case 'V':
                        y = currentHeight - 1 - y;
                        break;
                    case 'R':
                        double rotatedX = currentHeight - 1 - y;
                        double rotatedY = x;
                        x = rotatedX;
                        y = rotatedY;
                        int previousWidth = currentWidth;
                        currentWidth = currentHeight;
                        currentHeight = previousWidth;
                        break;
                }
            }

            return new System.Windows.Point(x, y);
        }
    }
}
