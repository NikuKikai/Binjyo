using System;
using System.Windows.Forms;
using Point = System.Drawing.Point;

namespace Binjyo
{
    public partial class MemoD11
    {
        #region ======== Interaction State ========

        private bool isEditedDuringKeyB;
        private bool isEditedDuringKeyQ;
        private bool isEditedDuringKeyO;
        private bool isRotateKeyDown;
        private bool isRotateDragging;
        private bool isRotateEditedDuringKeyR;
        private double rotateStartPointerAngle;
        private double rotateStartItemRotation;
        private double rotateCenterScreenX;
        private double rotateCenterScreenY;
        private Timer keyRotateTimer;
        private double keyRotatePendingDegrees;

        #endregion

        #region ======== Interaction ========

        /// <summary>
        /// Start drag-move interactions and set scene focus on mouse down.
        /// </summary>
        private void MemoD11_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
                return;

            Scene.Focus(Id);
            Activate();

            if (e.Button != MouseButtons.Left || Scene.DisplayMode != EDisplayMode.Expanded)
                return;

            if (isRotateKeyDown)
            {
                isRotateDragging = true;
                isRotateEditedDuringKeyR = true;

                var center = Item.GetCenter();
                rotateCenterScreenX = center.X;
                rotateCenterScreenY = center.Y;
                rotateStartPointerAngle = GetPointerAngleToFixedCenter();
                rotateStartItemRotation = Item.Rotation;
            }
            else
            {
                Scene.DragMoveStart(Id);
            }

            Capture = true;
        }

        /// <summary>
        /// Continue scene drag-move updates or drag-rotation updates while the left button is held.
        /// </summary>
        private void MemoD11_MouseMove(object sender, MouseEventArgs e)
        {
            if (!Capture || e.Button != MouseButtons.Left)
                return;

            if (isRotateDragging)
            {
                double currentAngle = GetPointerAngleToFixedCenter();
                double deltaFromStart = NormalizeAngleDelta(currentAngle - rotateStartPointerAngle);
                double targetRotation = rotateStartItemRotation + deltaFromStart;
                Item.SetRotationAroundCenter(targetRotation);
                return;
            }

            Scene.DragMoveUpdate();
        }

        /// <summary>
        /// End drag-move or drag-rotation interactions when the mouse button is released.
        /// </summary>
        private void MemoD11_MouseUp(object sender, MouseEventArgs e)
        {
            if (!Capture)
                return;

            if (isRotateDragging)
            {
                isRotateDragging = false;
            }
            else
            {
                Scene.DragMoveEnd();
            }

            Capture = false;
        }

        /// <summary>
        /// Apply transform and effect wheel interactions compatible with the existing memo shortcuts.
        /// </summary>
        private void MemoD11_MouseWheel(object sender, MouseEventArgs e)
        {
            if (Scene.DisplayMode != EDisplayMode.Expanded)
                return;

            if ((ModifierKeys & Keys.Control) == Keys.Control)
            {
                Item.SetScale(Item.Scale * (e.Delta > 0 ? 1.1 : 0.9));
                return;
            }

            if ((ModifierKeys & Keys.B) == Keys.B)
            {
                isEditedDuringKeyB = true;
                Item.SetEffectQuantize(false);
                Item.SetEffectBinarize(true, Item.PEffectBinarize + 14 * Math.Sign(e.Delta));
                return;
            }

            if ((ModifierKeys & Keys.Q) == Keys.Q)
            {
                isEditedDuringKeyQ = true;
                Item.SetEffectBinarize(false);
                Item.SetEffectQuantize(true, Item.PEffectQuantize + Math.Sign(e.Delta));
                return;
            }

            if ((ModifierKeys & Keys.O) == Keys.O)
            {
                isEditedDuringKeyO = true;
                Item.SetEffectTransparent(true, Item.PEffectTransparent + 15 * Math.Sign(e.Delta));
            }
        }

        /// <summary>
        /// Handle keyboard shortcuts for transform, focus, and effect toggles.
        /// </summary>
        private void MemoD11_KeyDown(object sender, KeyEventArgs e)
        {
            switch (e.KeyCode)
            {
                case Keys.Escape:
                    Scene.CloseItem(Id);
                    e.Handled = true;
                    break;
                case Keys.R:
                    if (!isRotateKeyDown)
                    {
                        isRotateKeyDown = true;
                        isRotateEditedDuringKeyR = false;
                    }
                    e.Handled = true;
                    break;
                case Keys.G:
                    Item.SetEffectGray(!Item.IsEffectGray);
                    e.Handled = true;
                    break;
                case Keys.H:
                    Item.SetEffectHuemap(!Item.IsEffectHuemap);
                    e.Handled = true;
                    break;
                case Keys.Oemtilde:
                    Item.SetScale(1);
                    Item.SetRotation(0);
                    Item.SetFlip(false, false);
                    e.Handled = true;
                    break;
                case Keys.Tab:
                    Scene.FocusNext();
                    e.Handled = true;
                    break;
                case Keys.B:
                    isEditedDuringKeyB = false;
                    break;
                case Keys.Q:
                    isEditedDuringKeyQ = false;
                    break;
                case Keys.O:
                    isEditedDuringKeyO = false;
                    break;
            }
        }

        /// <summary>
        /// Toggle discrete effect modes when their shortcut keys are released without wheel editing.
        /// </summary>
        private void MemoD11_KeyUp(object sender, KeyEventArgs e)
        {
            switch (e.KeyCode)
            {
                case Keys.R:
                    isRotateKeyDown = false;
                    if (isRotateDragging)
                    {
                        isRotateDragging = false;
                        if (Capture)
                            Capture = false;
                    }
                    else if (!isRotateEditedDuringKeyR)
                    {
                        keyRotatePendingDegrees += 30;
                        if (!keyRotateTimer.Enabled)
                            keyRotateTimer.Start();
                    }
                    break;
                case Keys.B:
                    if (!isEditedDuringKeyB)
                    {
                        Item.SetEffectBinarize(!Item.IsEffectBinarize);
                        if (Item.IsEffectBinarize)
                            Item.SetEffectQuantize(false);
                    }
                    break;
                case Keys.Q:
                    if (!isEditedDuringKeyQ)
                    {
                        Item.SetEffectQuantize(!Item.IsEffectQuantize);
                        if (Item.IsEffectQuantize)
                            Item.SetEffectBinarize(false);
                    }
                    break;
                case Keys.O:
                    if (!isEditedDuringKeyO)
                    {
                        Item.SetEffectTransparent(!Item.IsEffectTransparent);
                    }
                    break;
            }
        }

        /// <summary>
        /// Apply keyboard rotation in small increments so top-level window bounds do not jump by a large angle in one frame.
        /// </summary>
        private void KeyRotateTimer_Tick(object sender, EventArgs e)
        {
            if (Math.Abs(keyRotatePendingDegrees) < 0.001)
            {
                keyRotatePendingDegrees = 0;
                keyRotateTimer.Stop();
                return;
            }

            double step = Math.Sign(keyRotatePendingDegrees) * Math.Min(6.0, Math.Abs(keyRotatePendingDegrees));
            keyRotatePendingDegrees -= step;
            Item.SetRotationAroundCenter(Item.Rotation + step);
        }

        #endregion
    }
}
