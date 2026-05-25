using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;


namespace Binjyo
{
    public partial class Scene
    {
        private static double moveSpeed = 1; // for key repeat acceleration

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

        public static void MoveToNextSnap(List<Guid> ids, Key key)
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

            var targetPositions = new Dictionary<SceneItem, System.Windows.Point>();
            foreach (var item in movingItems)
            {
                targetPositions[item] = new System.Windows.Point(item.Left + deltaX, item.Top + deltaY);
            }

            // Rect boundingBox = GetBounds(targetPositions);
            // if (snap)
            // {
            //     GetMoveSnapAdjustment(movingItems, targetPositions, boundingBox.Left, boundingBox.Top, boundingBox.Width, boundingBox.Height, out double snapOffsetX, out double snapOffsetY);
            //     if (snapOffsetX != 0 || snapOffsetY != 0)
            //     {
            //         foreach (var item in movingItems)
            //         {
            //             var position = targetPositions[item];
            //             targetPositions[item] = new System.Windows.Point(position.X + snapOffsetX, position.Y + snapOffsetY);
            //         }
            //         boundingBox = GetBounds(targetPositions);
            //     }
            // }

            // GetPerMemoReachabilityOffset(targetPositions, out double constraintOffsetX, out double constraintOffsetY);

            foreach (var item in movingItems)
            {
                var position = targetPositions[item];
                item.MoveTo(position.X, position.Y);
            }
        }
    }
}
