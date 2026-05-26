using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows;
using System.Windows.Media.Imaging;


namespace Binjyo
{

    public partial class Scene
    {
        public static bool IsCanvasActive { get; internal set; } = false;
        public static bool IsStitchMode { get; internal set; } = false;


        public static HashSet<Guid> GetIdsExcept(IEnumerable<Guid> ids)
        {
            return new HashSet<Guid>(Items.Keys.Where(id => !ids.Contains(id)));
        }


        #region ======== Create / CLose ========
        public static Dictionary<Guid, SceneItem> Items { get; } = new Dictionary<Guid, SceneItem>();
        public static SceneItem CreateItem(WriteableBitmap bmp, int left, int top)
        {
            var item = new SceneItem(bmp, left, top);
            Items.Add(item.Id, item);
            return item;
        }

        public static void CloseItem(Guid id)
        {
            if (FocusedId == id)
                FocusedId = Guid.Empty;

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


        #region ======== Display ========

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

        public static void SetCanvasActive(bool active)
        {
            IsCanvasActive = active;
            foreach (var item in Items.Values)
            {
                item.views.ForEach(view => view.NotifiedCanvasActive());
            }
        }


        #endregion

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