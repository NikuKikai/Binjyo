using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using System.Windows;

namespace Binjyo
{
    public sealed class StitchOverlayData
    {
        public static readonly StitchOverlayData Hidden = new StitchOverlayData
        {
            FeaturePoints = Array.Empty<FastCornerPoint>(),
            MatchedPointIndices = new HashSet<int>(),
            ShowAllPoints = false
        };

        public IReadOnlyList<FastCornerPoint> FeaturePoints { get; set; }
        public IReadOnlyCollection<int> MatchedPointIndices { get; set; }
        public bool ShowAllPoints { get; set; }
    }

    public static class StitchSessionService
    {
        private sealed class FeatureCacheEntry
        {
            public List<FastCornerPoint> FeaturePoints { get; set; }
        }

        private sealed class AlignmentCacheKey : IEquatable<AlignmentCacheKey>
        {
            public AlignmentCacheKey(Guid sourceId, Guid targetId)
            {
                SourceId = sourceId;
                TargetId = targetId;
            }

            public Guid SourceId { get; }
            public Guid TargetId { get; }

            public bool Equals(AlignmentCacheKey other)
            {
                return other != null && SourceId.Equals(other.SourceId) && TargetId.Equals(other.TargetId);
            }

            public override bool Equals(object obj)
            {
                return Equals(obj as AlignmentCacheKey);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return (SourceId.GetHashCode() * 397) ^ TargetId.GetHashCode();
                }
            }
        }

        private static readonly Dictionary<Guid, FeatureCacheEntry> featureCache = new Dictionary<Guid, FeatureCacheEntry>();
        private static readonly Dictionary<AlignmentCacheKey, StitchAlignmentResult> alignmentCache = new Dictionary<AlignmentCacheKey, StitchAlignmentResult>();
        private const double SupportedScale = 1.0;
        private const double SupportedRotation = 0.001;
        private const double FeatureScaleTolerance = 0.001;
        private const double StitchSnapDistance = 28.0;

        public static bool ToggleMode()
        {
            Scene.IsStitchMode = !Scene.IsStitchMode;
            RefreshVisuals();
            return Scene.IsStitchMode;
        }

        public static void RefreshVisuals()
        {
            foreach (MemoD11 memo in System.Windows.Forms.Application.OpenForms.OfType<MemoD11>())
                memo.RefreshStitchVisuals();
        }

        public static void Invalidate(SceneItem item)
        {
            if (item == null)
                return;

            alignmentCache.Keys
                .Where(key => key.SourceId == item.Id || key.TargetId == item.Id)
                .ToList()
                .ForEach(key => alignmentCache.Remove(key));
        }

        public static void Remove(SceneItem item)
        {
            if (item == null)
                return;

            featureCache.Remove(item.Id);
            Invalidate(item);
        }

        public static StitchOverlayData GetOverlayData(SceneItem item)
        {
            if (!Scene.IsStitchMode || item == null || !CanParticipate(item))
                return StitchOverlayData.Hidden;

            if (!Scene.Items.TryGetValue(Scene.FocusedId, out SceneItem focusedItem) || !CanParticipate(focusedItem))
                return StitchOverlayData.Hidden;

            List<FastCornerPoint> featurePoints = GetFeaturePoints(item);
            if (featurePoints.Count == 0)
                return StitchOverlayData.Hidden;

            if (item.Id == focusedItem.Id)
            {
                var matchedIndices = new HashSet<int>();
                foreach (SceneItem otherItem in Scene.Items.Values)
                {
                    if (otherItem.Id == item.Id || !CanParticipate(otherItem) || !DoDisplayedBoundsOverlap(item, otherItem))
                        continue;

                    StitchAlignmentResult alignment = GetAlignment(item, otherItem);
                    if (!alignment.IsValid)
                        continue;

                    matchedIndices.UnionWith(alignment.SourceMatchedIndices);
                }

                return new StitchOverlayData
                {
                    FeaturePoints = featurePoints,
                    MatchedPointIndices = matchedIndices,
                    ShowAllPoints = true
                };
            }

            if (!DoDisplayedBoundsOverlap(focusedItem, item))
                return StitchOverlayData.Hidden;

            StitchAlignmentResult focusedAlignment = GetAlignment(focusedItem, item);
            if (!focusedAlignment.IsValid || focusedAlignment.TargetMatchedIndices.Count == 0)
                return StitchOverlayData.Hidden;

            return new StitchOverlayData
            {
                FeaturePoints = featurePoints,
                MatchedPointIndices = new HashSet<int>(focusedAlignment.TargetMatchedIndices),
                ShowAllPoints = false
            };
        }

        public static bool TryGetSnapOffset(
            Guid primaryMovingId,
            ICollection<Guid> movingIds,
            IDictionary<Guid, Point> targetPts,
            out double offsetX,
            out double offsetY)
        {
            offsetX = 0;
            offsetY = 0;

            if (!Scene.IsStitchMode ||
                movingIds == null ||
                targetPts == null ||
                !Scene.Items.TryGetValue(primaryMovingId, out SceneItem movingItem) ||
                !targetPts.TryGetValue(primaryMovingId, out Point proposedPosition) ||
                !CanParticipate(movingItem))
            {
                return false;
            }

            var movingIdSet = new HashSet<Guid>(movingIds);
            double bestDistanceSquared = double.MaxValue;
            bool found = false;

            foreach (SceneItem stationaryItem in Scene.Items.Values)
            {
                if (movingIdSet.Contains(stationaryItem.Id) || !CanParticipate(stationaryItem))
                    continue;

                StitchAlignmentResult alignment = GetAlignment(stationaryItem, movingItem);
                if (!alignment.IsValid)
                    continue;

                double candidateOffsetX = stationaryItem.Left + alignment.TargetLeftOffset - proposedPosition.X;
                double candidateOffsetY = stationaryItem.Top + alignment.TargetTopOffset - proposedPosition.Y;
                if (Math.Abs(candidateOffsetX) > StitchSnapDistance || Math.Abs(candidateOffsetY) > StitchSnapDistance)
                    continue;

                if (!WouldOverlapAfterSnap(movingItem, proposedPosition, candidateOffsetX, candidateOffsetY, stationaryItem))
                    continue;

                double distanceSquared = candidateOffsetX * candidateOffsetX + candidateOffsetY * candidateOffsetY;
                if (distanceSquared >= bestDistanceSquared)
                    continue;

                bestDistanceSquared = distanceSquared;
                offsetX = candidateOffsetX;
                offsetY = candidateOffsetY;
                found = true;
            }

            return found;
        }

        private static List<FastCornerPoint> GetFeaturePoints(SceneItem item)
        {
            if (!featureCache.TryGetValue(item.Id, out FeatureCacheEntry cacheEntry))
            {
                cacheEntry = new FeatureCacheEntry
                {
                    FeaturePoints = StitchFeatureExtractor.DetectFeaturePoints(item.Bitmap)
                };
                featureCache[item.Id] = cacheEntry;
            }

            return cacheEntry.FeaturePoints;
        }

        private static StitchAlignmentResult GetAlignment(SceneItem sourceItem, SceneItem targetItem)
        {
            var cacheKey = new AlignmentCacheKey(sourceItem.Id, targetItem.Id);
            if (alignmentCache.TryGetValue(cacheKey, out StitchAlignmentResult cachedAlignment))
                return cachedAlignment;

            StitchAlignmentResult alignment = StitchFeatureExtractor.ComputeAlignment(
                sourceItem,
                GetFeaturePoints(sourceItem),
                targetItem,
                GetFeaturePoints(targetItem));
            alignmentCache[cacheKey] = alignment;
            return alignment;
        }

        private static bool CanParticipate(SceneItem item)
        {
            if (item == null || item.Bitmap == null || Scene.DisplayMode == EDisplayMode.Minimized)
                return false;

            if (Math.Abs(item.Scale - SupportedScale) > FeatureScaleTolerance)
                return false;

            if (Math.Abs(item.Rotation) > SupportedRotation)
                return false;

            return !item.IsFlipX && !item.IsFlipY;
        }

        private static bool DoDisplayedBoundsOverlap(SceneItem sourceItem, SceneItem targetItem)
        {
            double overlapLeft = Math.Max(sourceItem.Left, targetItem.Left);
            double overlapTop = Math.Max(sourceItem.Top, targetItem.Top);
            double overlapRight = Math.Min(sourceItem.Right, targetItem.Right);
            double overlapBottom = Math.Min(sourceItem.Bottom, targetItem.Bottom);
            return overlapRight > overlapLeft && overlapBottom > overlapTop;
        }

        private static bool WouldOverlapAfterSnap(
            SceneItem movingItem,
            Point proposedPosition,
            double offsetX,
            double offsetY,
            SceneItem stationaryItem)
        {
            double movedLeft = proposedPosition.X + offsetX;
            double movedTop = proposedPosition.Y + offsetY;
            double movedRight = movedLeft + movingItem.Width;
            double movedBottom = movedTop + movingItem.Height;

            double overlapLeft = Math.Max(movedLeft, stationaryItem.Left);
            double overlapTop = Math.Max(movedTop, stationaryItem.Top);
            double overlapRight = Math.Min(movedRight, stationaryItem.Right);
            double overlapBottom = Math.Min(movedBottom, stationaryItem.Bottom);
            return overlapRight > overlapLeft && overlapBottom > overlapTop;
        }
    }
}
