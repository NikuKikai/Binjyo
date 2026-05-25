using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Input;
using System.Linq;
using System.Windows;


namespace Binjyo
{

    public partial class Scene
    {
        #region ======== Create / CLose ========
        public static Dictionary<Guid, SceneItem> Items { get; } = new Dictionary<Guid, SceneItem>();
        public static SceneItem CreateItem(Bitmap bmp, int left, int top)
        {
            var item = new SceneItem(bmp, left, top);
            Items.Add(item.Id, item);
            return item;
        }

        public static void CloseItem(Guid id)
        {
            if (Items.ContainsKey(id))
            {
                Items[id].Close();
                Items.Remove(id);
            }
        }

        public static void ClearItems()
        {
            foreach (var item in Items.Values)
                item.Close();
            Items.Clear();
        }
        #endregion


        #region ======== Move ========
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


        #endregion

        public static EDisplayMode DisplayMode { get; private set; } = EDisplayMode.Expanded;

        public static void SetDisplayMode(EDisplayMode mode)
        {
            DisplayMode = mode;
            foreach (var item in Items.Values)
            {
                item.views.ForEach(view => view.NotifiedDisplayMode());
            }
        }
        public static void CycleDisplayMode()
        {
            switch (DisplayMode)
            {
                case EDisplayMode.Expanded:
                    SetDisplayMode(EDisplayMode.AutoHide);
                    break;
                case EDisplayMode.AutoHide:
                    SetDisplayMode(EDisplayMode.Minimized);
                    break;
                case EDisplayMode.Minimized:
                    SetDisplayMode(EDisplayMode.Expanded);
                    break;
            }
        }



    }

    public enum EDisplayMode
    {
        Expanded,
        AutoHide,
        Minimized
    }

    public enum EAutoHideBehavior
    {
        HideOnHover = 0,
        EvadeMouse = 1
    }

    public enum EBitmapScalingMode
    {
        NearestNeighbor = 0,
        Linear = 1,
        Fant = 2
    }
}