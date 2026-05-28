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

            NotifiedTransform();
            NotifiedEffect();
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

        /// <summary>
        /// Move the native window to the scene item's logical position.
        /// </summary>
        public void NotifiedMove()
        {
            Rectangle bounds = GetTargetBounds();
            if (Bounds != bounds)
                Bounds = bounds;
        }

        /// <summary>
        /// Resize the window to the transformed bounding box and redraw the item.
        /// </summary>
        public void NotifiedTransform()
        {
            Rectangle bounds = GetTargetBounds();
            if (Bounds != bounds)
            {
                if (isGraphicsReady)
                {
                    ResizeSwapChain(Math.Max(1, bounds.Width), Math.Max(1, bounds.Height));
                    RenderSceneItem();
                }

                Console.WriteLine("Notified x" + bounds.X + ", y" + bounds.Y);
                Bounds = bounds;
                return;
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
