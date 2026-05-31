using System;
using System.Windows.Forms;
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
            {
                HideHSVWheel();
                Hide();
            }
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
                HideHSVWheel();
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
            HideHSVWheel();
            if (Visible)
                Hide();
            Close();
        }

        /// <summary>
        /// Activate the form when needed and flash the focus highlight on the focused memo.
        /// </summary>
        public void NotifiedFocus()
        {
            if (Scene.FocusedId == Id && !ContainsFocus)
            {
                Activate();
                FlashHighlight();
            }

            RefreshHSVWheelVisibility();
        }

        public void NotifiedOpacity()
        {
            RenderRequest();
            RefreshHSVWheelVisibility();
        }

        /// <summary>
        /// Update the layered host layout before rendering so geometry and content stay in sync.
        /// </summary>
        public void NotifiedTransform(bool moveOnly)
        {
            UpdateRenderHostLayout();
            bool shouldRenderImmediately = isRotateDragging || Scene.IsDragMoving;

            if (moveOnly)
            {
                RenderRequest(shouldRenderImmediately);
                RefreshHSVWheelVisibility();
                return;
            }
            if (renderWidth != currentHostBounds.Width || renderHeight != currentHostBounds.Height)
            {
                ResetRenderTargets(Math.Max(1, currentHostBounds.Width), Math.Max(1, currentHostBounds.Height));
            }
            RenderRequest(shouldRenderImmediately);
            RefreshHSVWheelVisibility();
        }

        /// <summary>
        /// Re-render the memo when any effect parameter changes.
        /// </summary>
        public void NotifiedEffect()
        {
            RenderRequest();
            RefreshHSVWheelVisibility();
        }

        #endregion
    }
}
