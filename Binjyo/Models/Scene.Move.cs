using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Input;


namespace Binjyo
{
    public partial class Scene
    {
        private static double moveSpeed = 1; // for key repeat acceleration
        public static bool IsDragMoving { get; private set; } = false;
        private static bool isDragMovingGroup = false;
        private static double dragStartMouseX, dragStartMouseY;
        private static Dictionary<Guid, Point> dragMoveStartPts = new Dictionary<Guid, Point>();

        public static void MoveByKey(Guid id, Key key, bool isRepeat)
        {
            bool isGroup = Keyboard.IsKeyDown(Key.LeftAlt) || Keyboard.IsKeyDown(Key.RightAlt);
            List<Guid> targetIds = isGroup ? GetConnectedIds(id) : new List<Guid> { id };

            if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))
            {
                MoveToNextSnap(targetIds, key);
                return;
            }

            double multiplier = (Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift)) ? 10 : 1;
            moveSpeed = isRepeat ? moveSpeed + 1 / 4 : 1;
            double delta = multiplier * moveSpeed;
            double deltaX = key == Key.Left ? -delta : key == Key.Right ? delta : 0;
            double deltaY = key == Key.Up ? -delta : key == Key.Down ? delta : 0;

            MoveBy(targetIds, deltaX, deltaY);
        }

        private static void MoveToNextSnap(List<Guid> ids, Key key)
        {
            if (ids.Count == 0) return;
            if (DisplayMode != EDisplayMode.Expanded) return;

            var boundingBox = GetBounds(ids);
            double? target = null;
            switch (key)
            {
                case Key.Left:
                    target = GetNextSnapPositionX(ids, boundingBox, false);
                    if (target.HasValue)
                        MoveBy(ids, target.Value - boundingBox.Left, 0);
                    break;
                case Key.Right:
                    target = GetNextSnapPositionX(ids, boundingBox, true);
                    if (target.HasValue)
                        MoveBy(ids, target.Value - boundingBox.Left, 0);
                    break;
                case Key.Up:
                    target = GetNextSnapPositionY(ids, boundingBox, false);
                    if (target.HasValue)
                        MoveBy(ids, 0, target.Value - boundingBox.Top);
                    break;
                case Key.Down:
                    target = GetNextSnapPositionY(ids, boundingBox, true);
                    if (target.HasValue)
                        MoveBy(ids, 0, target.Value - boundingBox.Top);
                    break;
            }
        }

        private static void MoveBy(List<Guid> movingIds, double deltaX, double deltaY, bool snap = false)
        {
            var movingItems = movingIds.Select(id => Items[id]).Where(item => item != null).ToList();
            if (movingItems.Count == 0)
                return;

            var targetPositions = new Dictionary<SceneItem, Point>();
            foreach (var item in movingItems)
            {
                targetPositions[item] = new Point(item.Left + deltaX, item.Top + deltaY);
            }

            foreach (var item in movingItems)
            {
                var position = targetPositions[item];
                item.SetPos(position.X, position.Y);
            }
        }

        public static void DragMoveStart(Guid id)
        {
            IsDragMoving = true;
            isDragMovingGroup = Keyboard.IsKeyDown(Key.LeftAlt) || Keyboard.IsKeyDown(Key.RightAlt);
            List<Guid> targetIds = isDragMovingGroup ? GetConnectedIds(id) : new List<Guid> { id };

            dragStartMouseX = Control.MousePosition.X;
            dragStartMouseY = Control.MousePosition.Y;
            foreach (var tid in targetIds)
            {
                var item = Items[tid];
                dragMoveStartPts[tid] = new Point(item.Left, item.Top);
            }
        }

        public static void DragMoveUpdate()
        {
            var mousePt = Control.MousePosition;
            var dpiFactor = Screen.FromPoint(mousePt).GetDpiFactor();
            double x = Control.MousePosition.X;
            double y = Control.MousePosition.Y;
            double deltaX = (x - dragStartMouseX) / dpiFactor;
            double deltaY = (y - dragStartMouseY) / dpiFactor;
            Console.WriteLine($"DragMoveUpdate: mouse=({x},{y}), delta=({deltaX},{deltaY})");

            var movingIds = dragMoveStartPts.Keys.ToList();
            var targetPts = new Dictionary<Guid, Point>();

            // Calculate target positions w/o snap
            foreach (var kv in dragMoveStartPts)
                targetPts[kv.Key] = new Point(kv.Value.X + deltaX, kv.Value.Y + deltaY);

            // Predict bounds of the group after moved
            Rect bounds = GetTargetBounds(targetPts);

            // Snap
            if (Keyboard.IsKeyDown(Key.Space) != Properties.Settings.Default.SnapMemo)
            {
                SnapTargetPts(movingIds, targetPts, bounds);
            }

            // Move items to target positions
            foreach (var kv in targetPts)
                Items[kv.Key].SetPos(kv.Value.X, kv.Value.Y);
        }

        public static void DragMoveEnd()
        {
            IsDragMoving = false;
            isDragMovingGroup = false;
            dragMoveStartPts.Clear();
        }

        private static void SnapTargetPts(
            ICollection<Guid> ids,
            IDictionary<Guid, Point> targetPts,
            Rect bounds
        )
        {
            double snappedLeft = bounds.Left;
            double snappedTop = bounds.Top;
            double bestDistanceX = SnapDistance + 1;
            double bestDistanceY = SnapDistance + 1;
            var otherIdSet = GetIdsExcept(ids);

            // Snap to screen edges
            foreach (var screen in Screen.AllScreens)
            {
                var dpiFactor = screen.GetDpiFactor();
                double screenLeft = screen.Bounds.Left / dpiFactor;
                double screenTop = screen.Bounds.Top / dpiFactor;
                double screenRight = screen.Bounds.Right / dpiFactor;
                double screenBottom = screen.Bounds.Bottom / dpiFactor;

                Geo.SnapValue(bounds.Left, screenLeft, SnapDistance, ref snappedLeft, ref bestDistanceX);
                Geo.SnapValue(bounds.Left, screenRight - bounds.Width, SnapDistance, ref snappedLeft, ref bestDistanceX);
                Geo.SnapValue(bounds.Top, screenTop, SnapDistance, ref snappedTop, ref bestDistanceY);
                Geo.SnapValue(bounds.Top, screenBottom - bounds.Height, SnapDistance, ref snappedTop, ref bestDistanceY);
            }
            Console.WriteLine($"snappedTop={snappedTop}, bounds.Top={bounds.Top}, bestDistanceY={bestDistanceY}");

            // Snap to other items
            if (!IsStitchMode)
            {
                GetLeftSnapToOthers(
                    otherIdSet, bounds.Left, bounds.Top, bounds.Width, bounds.Height, ref snappedLeft, ref bestDistanceX
                );
                GetTopSnapToOthers(
                    otherIdSet, snappedLeft, bounds.Top, bounds.Width, bounds.Height, ref snappedTop, ref bestDistanceY
                );
                GetLeftSnapToOthers(
                    otherIdSet, bounds.Left, snappedTop, bounds.Width, bounds.Height, ref snappedLeft, ref bestDistanceX
                );
            }

            var offsetX = snappedLeft - bounds.Left;
            var offsetY = snappedTop - bounds.Top;

            // if (targetPts != null &&
            //     TryGetFeatureAlignmentSnapOffset(targetPts, movingIdSet, out double alignmentOffsetX, out double alignmentOffsetY))
            // {
            //     offsetX = alignmentOffsetX;
            //     offsetY = alignmentOffsetY;
            // }

            foreach (var id in ids)
                targetPts[id] = new Point(targetPts[id].X + offsetX, targetPts[id].Y + offsetY);
        }
    }
}
