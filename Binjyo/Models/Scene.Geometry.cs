using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;


namespace Binjyo
{
    public partial class Scene
    {
        public const double SnapDistance = 12;

        public static Rect GetBounds(IEnumerable<Guid> ids)
        {
            double left = double.PositiveInfinity;
            double top = double.PositiveInfinity;
            double right = double.NegativeInfinity;
            double bottom = double.NegativeInfinity;

            var items = ids.Select(id => Items[id]).Where(item => item != null);

            foreach (var item in items)
            {
                left = Math.Min(left, item.Left);
                top = Math.Min(top, item.Top);
                right = Math.Max(right, item.Left + item.GetDisplayWidth());
                bottom = Math.Max(bottom, item.Top + item.GetDisplayHeight());
            }

            if (double.IsInfinity(left) || double.IsInfinity(top))
                return Rect.Empty;

            return new Rect(left, top, right - left, bottom - top);
        }
        public static Rect GetTargetBounds(IDictionary<Guid, Point> targetPts)
        {
            double left = double.PositiveInfinity;
            double top = double.PositiveInfinity;
            double right = double.NegativeInfinity;
            double bottom = double.NegativeInfinity;


            foreach (var kvp in targetPts)
            {
                var item = Items[kvp.Key];
                left = Math.Min(left, kvp.Value.X);
                top = Math.Min(top, kvp.Value.Y);
                right = Math.Max(right, kvp.Value.X + item.GetDisplayWidth());
                bottom = Math.Max(bottom, kvp.Value.Y + item.GetDisplayHeight());
            }

            if (double.IsInfinity(left) || double.IsInfinity(top))
                return Rect.Empty;

            return new Rect(left, top, right - left, bottom - top);
        }

        public static List<Guid> GetIdsAtMouse()
        {
            double mouseX = System.Windows.Forms.Control.MousePosition.X;
            double mouseY = System.Windows.Forms.Control.MousePosition.Y;
            return GetIdsAtPos(mouseX, mouseY);
        }
        /// <summary>
        /// pos is of physical pixels
        /// </summary>
        public static List<Guid> GetIdsAtPos(double x, double y)
        {
            return Items.Keys.ToList()
                .Where(id =>
                {
                    if (!Items.ContainsKey(id))
                        return false;
                    var item = Items[id];
                    double localX = x / item.DpiFactor - item.Left;
                    double localY = y / item.DpiFactor - item.Top;
                    return localX >= 0 && localX < item.GetDisplayWidth() && localY >= 0 && localY < item.GetDisplayHeight();
                })
                .ToList();
        }

        public static List<Guid> GetConnectedIds(Guid id)
        {
            if (DisplayMode != EDisplayMode.Expanded)
                return new List<Guid>();

            var result = new List<Guid>();
            var queue = new Queue<Guid>();
            var visited = new HashSet<Guid>();
            queue.Enqueue(id);
            visited.Add(id);

            while (queue.Count > 0)
            {
                Guid current = queue.Dequeue();
                result.Add(current);
                Rect currentBounds = Items[current].GetBounds();

                foreach (Guid candidate in Items.Keys)
                {
                    if (visited.Contains(candidate))
                        continue;

                    if (Geo.DoRectsOverlap(currentBounds, Items[candidate].GetBounds()))
                    {
                        visited.Add(candidate);
                        queue.Enqueue(candidate);
                    }
                }
            }

            return result;
        }


        #region ======== Snap ========
        public static double? GetNextSnapPositionX(List<Guid> movingIds, Rect boundingBox, bool forward)
        {
            List<double> candidates = new List<double>();
            double width = boundingBox.Width;
            double top = boundingBox.Top;
            double bottom = boundingBox.Bottom;
            var movingSet = new HashSet<SceneItem>(movingIds.Select(id => Items[id]).Where(item => item != null));

            foreach (var screen in System.Windows.Forms.Screen.AllScreens)
            {
                var dpiFactor = screen.GetDpiFactor();
                double screenLeft = screen.Bounds.Left / dpiFactor;
                double screenRight = screen.Bounds.Right / dpiFactor;
                candidates.Add(screenLeft);
                candidates.Add(screenRight - width);
            }

            foreach (var item in Items.Values)
            {
                if (movingSet.Contains(item))
                    continue;

                if (!Geo.DoSegmentsOverlap(top, bottom, item.Top, item.Top + item.GetDisplayHeight()))
                    continue;

                candidates.Add(item.Left);
                candidates.Add(item.Left + item.GetDisplayWidth());
                candidates.Add(item.Left - width);
                candidates.Add(item.Left + item.GetDisplayWidth() - width);
            }

            return FindNextSnapCandidate(boundingBox.Left, candidates, forward);
        }

        public static double? GetNextSnapPositionY(List<Guid> movingIds, Rect boundingBox, bool forward)
        {
            List<double> candidates = new List<double>();
            double height = boundingBox.Height;
            double left = boundingBox.Left;
            double right = boundingBox.Right;
            var movingSet = new HashSet<SceneItem>(movingIds.Select(id => Items[id]).Where(item => item != null));

            foreach (var screen in System.Windows.Forms.Screen.AllScreens)
            {
                var dpiFactor = screen.GetDpiFactor();
                double screenTop = screen.Bounds.Top / dpiFactor;
                double screenBottom = screen.Bounds.Bottom / dpiFactor;
                candidates.Add(screenTop);
                candidates.Add(screenBottom - height);
            }

            foreach (var item in Items.Values)
            {
                if (movingSet.Contains(item))
                    continue;

                if (!Geo.DoSegmentsOverlap(left, right, item.Left, item.Left + item.GetDisplayWidth()))
                    continue;

                candidates.Add(item.Top);
                candidates.Add(item.Top + item.GetDisplayHeight());
                candidates.Add(item.Top - height);
                candidates.Add(item.Top + item.GetDisplayHeight() - height);
            }

            return FindNextSnapCandidate(boundingBox.Top, candidates, forward);
        }

        private static double? FindNextSnapCandidate(double currentValue, IEnumerable<double> candidates, bool forward)
        {
            double? bestCandidate = null;
            foreach (double candidate in candidates)
            {
                if (forward)
                {
                    if (candidate <= currentValue + SnapDistance)
                        continue;
                    if (!bestCandidate.HasValue || candidate < bestCandidate.Value)
                        bestCandidate = candidate;
                }
                else
                {
                    if (candidate >= currentValue - SnapDistance)
                        continue;
                    if (!bestCandidate.HasValue || candidate > bestCandidate.Value)
                        bestCandidate = candidate;
                }
            }
            return bestCandidate;
        }

        private static void GetLeftSnapToOthers(
            HashSet<Guid> otherIdSet,
            double left,
            double top,
            double width,
            double height,
            ref double snappedLeft,
            ref double bestDistanceX
            )
        {
            foreach (var other in otherIdSet)
            {
                var item = Items[other];

                double otherLeft = item.Left;
                double otherTop = item.Top;
                double otherRight = item.Left + item.GetDisplayWidth();
                double otherBottom = item.Top + item.GetDisplayHeight();

                if (!Geo.DoSegmentsOverlap(top, top + height, otherTop, otherBottom))
                    continue;

                Geo.SnapValue(left, otherLeft, SnapDistance, ref snappedLeft, ref bestDistanceX);
                Geo.SnapValue(left, otherRight, SnapDistance, ref snappedLeft, ref bestDistanceX);
                Geo.SnapValue(left, otherLeft - width, SnapDistance, ref snappedLeft, ref bestDistanceX);
                Geo.SnapValue(left, otherRight - width, SnapDistance, ref snappedLeft, ref bestDistanceX);
            }
        }
        private static void GetTopSnapToOthers(
            HashSet<Guid> otherIdSet,
            double left,
            double top,
            double width,
            double height,
            ref double snappedTop,
            ref double bestDistanceY
            )
        {
            foreach (var other in otherIdSet)
            {
                var item = Items[other];
                double otherLeft = item.Left;
                double otherTop = item.Top;
                double otherRight = item.Left + item.GetDisplayWidth();
                double otherBottom = item.Top + item.GetDisplayHeight();

                if (!Geo.DoSegmentsOverlap(left, left + width, otherLeft, otherRight))
                    continue;

                Geo.SnapValue(top, otherTop, SnapDistance, ref snappedTop, ref bestDistanceY);
                Geo.SnapValue(top, otherBottom, SnapDistance, ref snappedTop, ref bestDistanceY);
                Geo.SnapValue(top, otherTop - height, SnapDistance, ref snappedTop, ref bestDistanceY);
                Geo.SnapValue(top, otherBottom - height, SnapDistance, ref snappedTop, ref bestDistanceY);
            }
        }
        #endregion
    }

}
