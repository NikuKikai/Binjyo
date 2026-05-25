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
                right = Math.Max(right, item.Left + item.GetWidth());
                bottom = Math.Max(bottom, item.Top + item.GetHeight());
            }

            if (double.IsInfinity(left) || double.IsInfinity(top))
                return Rect.Empty;

            return new Rect(left, top, right - left, bottom - top);
        }

        public static List<Guid> GetIdsAtMouse()
        {
            double mouseX = System.Windows.Forms.Control.MousePosition.X;
            double mouseY = System.Windows.Forms.Control.MousePosition.Y;

            return Items.Keys.ToList()
                .Where(id => {
                    if (!Items.ContainsKey(id))
                        return false;
                    var item = Items[id];
                    double localX = mouseX / item.DpiFactor - item.Left;
                    double localY = mouseY / item.DpiFactor - item.Top;
                    return localX >= 0 && localX < item.GetWidth() && localY >= 0 && localY < item.GetHeight();
                })
                .ToList();
        }

        public static double? GetNextSnapPositionX(List<Guid> movingIds, Rect boundingBox, bool forward)
        {
            List<double> candidates = new List<double>();
            double width = boundingBox.Width;
            double top = boundingBox.Top;
            double bottom = boundingBox.Bottom;
            var movingSet = new HashSet<SceneItem>(movingIds.Select(id => Items[id]).Where(item => item != null));

            foreach (var screen in System.Windows.Forms.Screen.AllScreens)
            {
                var dpiFactor = screen.GetDpi(DpiType.Effective).X / 96.0;
                double screenLeft = screen.Bounds.Left / dpiFactor;
                double screenRight = screen.Bounds.Right / dpiFactor;
                candidates.Add(screenLeft);
                candidates.Add(screenRight - width);
            }

            foreach (var item in Items.Values)
            {
                if (movingSet.Contains(item))
                    continue;

                if (!Geo.DoSegmentsOverlap(top, bottom, item.Top, item.Top + item.GetHeight()))
                    continue;

                candidates.Add(item.Left);
                candidates.Add(item.Left + item.GetWidth());
                candidates.Add(item.Left - width);
                candidates.Add(item.Left + item.GetWidth() - width);
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
                var dpiFactor = screen.GetDpi(DpiType.Effective).X / 96.0;
                double screenTop = screen.Bounds.Top / dpiFactor;
                double screenBottom = screen.Bounds.Bottom / dpiFactor;
                candidates.Add(screenTop);
                candidates.Add(screenBottom - height);
            }

            foreach (var item in Items.Values)
            {
                if (movingSet.Contains(item))
                    continue;

                if (!Geo.DoSegmentsOverlap(left, right, item.Left, item.Left + item.GetWidth()))
                    continue;

                candidates.Add(item.Top);
                candidates.Add(item.Top + item.GetHeight());
                candidates.Add(item.Top - height);
                candidates.Add(item.Top + item.GetHeight() - height);
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

    }

}
