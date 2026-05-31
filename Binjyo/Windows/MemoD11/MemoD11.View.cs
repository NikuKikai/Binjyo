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
            RenderSceneItem();
        }

        /// <summary>
        /// Update the layered host layout before rendering so geometry and content stay in sync.
        /// </summary>
        public void NotifiedTransform(bool moveOnly)
        {
            UpdateRenderHostLayout();

            if (moveOnly)
            {
                RenderSceneItem();
                return;
            }
            if (renderWidth != currentHostBounds.Width || renderHeight != currentHostBounds.Height)
            {
                if (isGraphicsReady)
                    ResizeSwapChain(Math.Max(1, currentHostBounds.Width), Math.Max(1, currentHostBounds.Height));
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
