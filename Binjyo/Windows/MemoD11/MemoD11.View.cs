using System;
using Rectangle = System.Drawing.Rectangle;

namespace Binjyo
{
    public partial class MemoD11
    {
        #region ======== ISceneItemView ========

        /// <summary>
        /// Expose the scene item identifier to the scene model.
        /// </summary>
        public Guid Id => Item.Id;

        /// <summary>
        /// This window is a consumer of scene state, not a scene renderer authority.
        /// </summary>
        public bool IsRenderer => IsRendererField;

        /// <summary>
        /// Hide or show this memo when the canvas is active.
        /// </summary>
        public void NotifiedCanvasActive()
        {
            if (Scene.IsCanvasActive)
                Hide();
            else
                NotifiedDisplayMode();
        }

        /// <summary>
        /// Respond to scene display mode changes with simple show/hide behavior.
        /// </summary>
        public void NotifiedDisplayMode()
        {
            if (Scene.IsCanvasActive || Scene.DisplayMode == EDisplayMode.Minimized)
            {
                Hide();
                return;
            }

            if (!Visible)
                Show();

            NotifiedTransform(false);
        }

        /// <summary>
        /// Close the memo when the owning scene item is removed.
        /// </summary>
        public void NotifiedClose()
        {
            if (Visible)
                Hide();
            Close();
        }

        /// <summary>
        /// Refresh the focus border and activate the window when it becomes focused.
        /// </summary>
        public void NotifiedFocus()
        {
            if (Scene.FocusedId == Id && !Focused)
                Activate();

            RenderSceneItem();
        }

        public void NotifiedOpacity()
        {
            Opacity = FinalOpacity;
        }

        /// <summary>
        /// Resize the window to the transformed bounding box and redraw the item.
        /// </summary>
        public void NotifiedTransform(bool moveOnly)
        {
            Rectangle bounds = new Rectangle(
                (int)Math.Round(Item.Left),
                (int)Math.Round(Item.Top),
                Math.Max(1, (int)Math.Ceiling(Item.Width)),
                Math.Max(1, (int)Math.Ceiling(Item.Height)));
            if (moveOnly)
            {
                if (Bounds != bounds)
                    Bounds = bounds;
                return;
            }
            if (Bounds != bounds)
            {
                if (isGraphicsReady && (bounds.Width != Bounds.Width || bounds.Height != Bounds.Height))
                    ResizeSwapChain(Math.Max(1, bounds.Width), Math.Max(1, bounds.Height));
                Bounds = bounds;
            }
            RenderSceneItem();
        }

        /// <summary>
        /// Re-render the memo when any effect parameter changes.
        /// </summary>
        public void NotifiedEffect()
        {
            RenderSceneItem();
        }

        #endregion
    }
}
