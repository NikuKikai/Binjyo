using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Binjyo
{
    public partial class MemoD11
    {
        #region ======== Interaction State ========

        // Binarize
        private bool isEditedDuringKeyB;
        private bool isKeyDownB;
        // Quantize
        private bool isEditedDuringKeyQ;
        private bool isKeyDownQ;
        // Opacity
        private bool isEditedDuringKeyO;
        private bool isKeyDownO;
        // Rotate
        private bool isKeyDownR;
        private bool isRotateDragging;
        private bool isEditedDuringKeyR;
        private double rotateStartPointerAngle;
        private double rotateStartItemRotation;
        private double rotateCenterScreenX;
        private double rotateCenterScreenY;
        private Timer keyRotateTimer;
        private double keyRotatePendingDegrees;
        // Save
        private bool isKeyDownS;

        #endregion

        #region ======== Interaction ========

        /// <summary>
        /// Double-click to clip
        /// </summary>
        private void MemoD11_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            if (Scene.IsDragMoving) return;
            var isShiftDown = (ModifierKeys & Keys.Control) == Keys.Control;
            Item.CopyToClipboard(!isShiftDown);
            Scene.CloseItem(Id);
        }

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

            if (isKeyDownR)
            {
                isRotateDragging = true;
                isEditedDuringKeyR = true;

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

            if (IsKeyDown(Keys.B))
            {
                isEditedDuringKeyB = true;
                Item.SetEffectQuantize(false);
                Item.SetEffectBinarize(true, Item.PEffectBinarize + 14 * Math.Sign(e.Delta));
                return;
            }

            if (IsKeyDown(Keys.Q))
            {
                isEditedDuringKeyQ = true;
                Item.SetEffectBinarize(false);
                Item.SetEffectQuantize(true, Item.PEffectQuantize + Math.Sign(e.Delta));
                return;
            }

            if (IsKeyDown(Keys.O))
            {
                isEditedDuringKeyO = true;
                Item.SetEffectTransparent(true, Item.PEffectTransparent + 15 * Math.Sign(e.Delta));
                return;
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
                    if (!isKeyDownR)
                        isEditedDuringKeyR = false;
                    isKeyDownR = true;
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
                    if (!isKeyDownB)
                        isEditedDuringKeyB = false;
                    isKeyDownB = true;
                    break;
                case Keys.Q:
                    if (!isKeyDownQ)
                        isEditedDuringKeyQ = false;
                    isKeyDownQ = true;
                    break;
                case Keys.O:
                    if (!isKeyDownO)
                        isEditedDuringKeyO = false;
                    isKeyDownO = true;
                    break;
                case Keys.C:
                    Item.CopyToClipboard(!e.Shift);
                    break;
                case Keys.X:
                    Item.CopyToClipboard(!e.Shift);
                    Scene.CloseItem(Id);
                    break;
                case Keys.S:
                    if (isKeyDownS) break;
                    isKeyDownS = true;
                    Item.Save(!e.Shift);
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
                    isKeyDownR = false;
                    if (isRotateDragging)
                    {
                        isRotateDragging = false;
                        if (Capture)
                            Capture = false;
                    }
                    else if (!isEditedDuringKeyR)
                    {
                        keyRotatePendingDegrees = Math.Ceiling(Item.Rotation / 90 + 0.001) * 90 - Item.Rotation;
                        if (!keyRotateTimer.Enabled)
                            keyRotateTimer.Start();
                    }
                    break;
                case Keys.B:
                    isKeyDownB = false;
                    if (!isEditedDuringKeyB)
                        Item.SetEffectBinarize(!Item.IsEffectBinarize);
                    break;
                case Keys.Q:
                    isKeyDownQ = false;
                    if (!isEditedDuringKeyQ)
                        Item.SetEffectQuantize(!Item.IsEffectQuantize);
                    break;
                case Keys.O:
                    isKeyDownO = false;
                    if (!isEditedDuringKeyO)
                        Item.SetEffectTransparent(!Item.IsEffectTransparent);
                    break;
                case Keys.S:
                    isKeyDownS = false;
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

        [DllImport("user32.dll")]
        private static extern short GetKeyState(int nVirtKey);
        private static bool IsKeyDown(Keys key)
        {
            return (GetKeyState((int)key) & 0x8000) != 0;
        }
    }
}
