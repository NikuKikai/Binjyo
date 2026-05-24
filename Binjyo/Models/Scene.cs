using System;
using System.Collections.Generic;
using System.Drawing;

namespace Binjyo
{

    public class Scene
    {
        public static Dictionary<Guid, SceneItem> Items { get; } = new Dictionary<Guid, SceneItem>();

        public static List<ISceneItemView> views = new List<ISceneItemView>();

        public static void RegisterView(ISceneItemView view)
        {
            if (!views.Contains(view))
                views.Add(view);
        }
        public static void UnregisterView(ISceneItemView view)
        {
            if (views.Contains(view))
                views.Remove(view);
        }

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


        public static EDisplayMode DisplayMode { get; private set; } = EDisplayMode.Expanded;

        public static void SetDisplayMode(EDisplayMode mode)
        {
            DisplayMode = mode;
            foreach (ISceneItemView view in views)
                view.NotifiedDisplayMode();
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