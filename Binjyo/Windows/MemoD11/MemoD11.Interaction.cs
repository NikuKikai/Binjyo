using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Binjyo
{
    public partial class MemoD11
    {
        #region ======== Interaction State ========

        // Move
        private bool isKeyDownLeft = false;
        private bool isKeyDownRight = false;
        private bool isKeyDownUp = false;
        private bool isKeyDownDown = false;
        private bool isKeyDownArrow => isKeyDownLeft || isKeyDownRight || isKeyDownUp || isKeyDownDown;
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
        private bool isRotateAnimating;
        private double rotateAnimationRemainingDelta;
        // Save
        private bool isKeyDownS;
        private int lastLeftClickTimestamp;
        private int lastLeftClickScreenX;
        private int lastLeftClickScreenY;

        #endregion

        #region ======== Interaction ========

        /// <summary>
        /// Double-click to clip
        /// </summary>
        private void MemoD11_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            if (Scene.IsDragMoving) return;
            IActivatableSceneTextureSource activatableSource = Item.TextureSource as IActivatableSceneTextureSource;

            if (Item.HasDynamicTextureSource)
                Item.CopyToClipboard(false);
            else
                Item.CopyToClipboard((ModifierKeys & Keys.Shift) != Keys.Shift);

            Scene.CloseItem(Id);
            activatableSource?.TryActivateSourceWindow();
        }

        /// <summary>
        /// Start drag-move interactions and set scene focus on mouse down.
        /// </summary>
        private void MemoD11_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                ShowContextMenuAtCursor();
                return;
            }

            if (TryHandleManualDoubleClick(e))
                return;

            if (isDrawMode)
            {
                Scene.Focus(Id);
                if (e.Button == MouseButtons.Left)
                    BeginDrawingStroke(e);
                return;
            }

            StopRotateAnimation();
            Scene.Focus(Id);

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

        private bool TryHandleManualDoubleClick(MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left || isDrawMode)
                return false;

            int now = Environment.TickCount;
            int deltaTime = unchecked(now - lastLeftClickTimestamp);
            int deltaX = Math.Abs(Control.MousePosition.X - lastLeftClickScreenX);
            int deltaY = Math.Abs(Control.MousePosition.Y - lastLeftClickScreenY);
            bool isDoubleClick =
                lastLeftClickTimestamp > 0 &&
                deltaTime >= 0 &&
                deltaTime <= SystemInformation.DoubleClickTime &&
                deltaX <= SystemInformation.DoubleClickSize.Width &&
                deltaY <= SystemInformation.DoubleClickSize.Height;

            lastLeftClickTimestamp = now;
            lastLeftClickScreenX = Control.MousePosition.X;
            lastLeftClickScreenY = Control.MousePosition.Y;

            if (!isDoubleClick)
                return false;

            MemoD11_MouseDoubleClick(this, e);
            return true;
        }

        /// <summary>
        /// Continue scene drag-move updates or drag-rotation updates while the left button is held.
        /// </summary>
        private void MemoD11_MouseMove(object sender, MouseEventArgs e)
        {
            if (isDrawMode)
            {
                if (isDrawingStroke && e.Button == MouseButtons.Left)
                    ExtendDrawingStroke(e);
                return;
            }

            if (!Capture || e.Button != MouseButtons.Left)
            {
                RefreshHSVWheelVisibility();
                return;
            }

            if (isRotateDragging)
            {
                double currentAngle = GetPointerAngleToFixedCenter();
                double deltaFromStart = NormalizeAngleDelta(currentAngle - rotateStartPointerAngle);
                double targetRotation = rotateStartItemRotation + deltaFromStart;
                Item.SetRotationCentered(targetRotation);
                RefreshHSVWheelVisibility();
                return;
            }

            Scene.DragMoveUpdate();
            RefreshHSVWheelVisibility();
        }

        /// <summary>
        /// End drag-move or drag-rotation interactions when the mouse button is released.
        /// </summary>
        private void MemoD11_MouseUp(object sender, MouseEventArgs e)
        {
            if (isDrawMode)
            {
                EndDrawingStroke();
                return;
            }

            if (!Capture)
                return;

            if (isRotateDragging)
            {
                isRotateDragging = false;
                NotifiedTransform(false);
            }
            else
            {
                Scene.DragMoveEnd();
            }

            Capture = false;
            RefreshHSVWheelVisibility();
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
                RefreshHSVWheelVisibility();
                return;
            }

            if (IsKeyDown(Keys.B))
            {
                isEditedDuringKeyB = true;
                Item.SetEffectQuantize(false);
                Item.SetEffectBinarize(true, Item.PEffectBinarize + 14 * Math.Sign(e.Delta));
                RefreshHSVWheelVisibility();
                return;
            }

            if (IsKeyDown(Keys.Q))
            {
                isEditedDuringKeyQ = true;
                Item.SetEffectBinarize(false);
                Item.SetEffectQuantize(true, Item.PEffectQuantize + Math.Sign(e.Delta));
                RefreshHSVWheelVisibility();
                return;
            }

            if (IsKeyDown(Keys.O))
            {
                isEditedDuringKeyO = true;
                Item.SetOpacity(true, Item.Opacity + 0.1 * Math.Sign(e.Delta));
                RefreshHSVWheelVisibility();
                return;
            }
        }

        /// <summary>
        /// Handle keyboard shortcuts for transform, focus, and effect toggles.
        /// </summary>
        private void MemoD11_KeyDown(object sender, KeyEventArgs e)
        {
            if (isDrawMode)
            {
                switch (e.KeyCode)
                {
                    case Keys.Escape:
                        ExitDrawMode();
                        break;
                    case Keys.Enter:
                        ExitDrawMode();
                        break;
                    case Keys.E:
                        drawPanel?.SetTool(DrawTool.Eraser);
                        break;
                    case Keys.Q:
                        drawPanel?.SetTool(DrawTool.Brush);
                        break;
                    case Keys.Z:
                        if ((ModifierKeys & Keys.Shift) == Keys.Shift)
                            RedoDrawingOperation();
                        else
                            UndoDrawingOperation();
                        break;
                    case Keys.Oem4:
                        drawPanel?.SetBrushSize(drawPanel.BrushSize - 1);
                        break;
                    case Keys.Oem6:
                        drawPanel?.SetBrushSize(drawPanel.BrushSize + 1);
                        break;
                }

                e.Handled = true;
                return;
            }

            switch (e.KeyCode)
            {
                case Keys.Escape:
                    Scene.CloseItem(Id);
                    e.Handled = true;
                    break;
                case Keys.E:
                    EnterDrawMode();
                    e.Handled = true;
                    break;
                case Keys.Q:
                    if (!isKeyDownQ)
                        isEditedDuringKeyQ = false;
                    isKeyDownQ = true;
                    break;
                case Keys.Z:
                    break;
                case Keys.Oem4:
                    break;
                case Keys.Oem6:
                    break;
                case Keys.Left:
                    Scene.MoveByKey(Id, -1, 0, isKeyDownArrow);
                    isKeyDownLeft = true;
                    e.Handled = true;
                    break;
                case Keys.Right:
                    Scene.MoveByKey(Id, 1, 0, isKeyDownArrow);
                    isKeyDownRight = true;
                    e.Handled = true;
                    break;
                case Keys.Up:
                    Scene.MoveByKey(Id, 0, -1, isKeyDownArrow);
                    isKeyDownUp = true;
                    e.Handled = true;
                    break;
                case Keys.Down:
                    Scene.MoveByKey(Id, 0, 1, isKeyDownArrow);
                    isKeyDownDown = true;
                    e.Handled = true;
                    break;
                case Keys.F:
                    if (e.Shift)
                        Item.SetFlip(Item.IsFlipX, !Item.IsFlipY);
                    else
                        Item.SetFlip(!Item.IsFlipX, Item.IsFlipY);
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
                case Keys.P:
                    ToggleFeaturePoints();
                    e.Handled = true;
                    break;
                case Keys.Oemtilde:
                    Item.ResetTransform();
                    e.Handled = true;
                    break;
                case Keys.Tab:
                    Scene.FocusNext();
                    e.Handled = true;
                    break;
                case Keys.CapsLock:
                    if (!e.SuppressKeyPress)
                    {
                        isHSVWheelEnabled = !isHSVWheelEnabled;
                        RefreshAllMemoD11HSVWheelVisibility();
                    }
                    e.Handled = true;
                    break;
                case Keys.B:
                    if (!isKeyDownB)
                        isEditedDuringKeyB = false;
                    isKeyDownB = true;
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
            if (isDrawMode)
            {
                e.Handled = true;
                return;
            }

            switch (e.KeyCode)
            {
                case Keys.Left:
                    isKeyDownLeft = false;
                    break;
                case Keys.Right:
                    isKeyDownRight = false;
                    break;
                case Keys.Up:
                    isKeyDownUp = false;
                    break;
                case Keys.Down:
                    isKeyDownDown = false;
                    break;
                case Keys.R:
                    isKeyDownR = false;
                    if (isRotateDragging)
                    {
                        isRotateDragging = false;
                        if (Capture)
                            Capture = false;
                        NotifiedTransform(false);
                    }
                    else if (!isEditedDuringKeyR)
                    {
                        var delta = Math.Ceiling(Item.Rotation / 90 + 0.001) * 90 - Item.Rotation;
                        if (Math.Abs(delta) < 0.001)
                            delta = 90;
                        StartRotateAnimation(delta);
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
                        Item.SetOpacity(!Item.IsOpacity);
                    break;
                case Keys.S:
                    isKeyDownS = false;
                    break;
            }
        }

        #endregion

        [DllImport("user32.dll")]
        private static extern short GetKeyState(int nVirtKey);
        private static bool IsKeyDown(Keys key)
        {
            return (GetKeyState((int)key) & 0x8000) != 0;
        }

        /// <summary>
        /// Start the discrete key rotation animation managed by the shared frame loop.
        /// </summary>
        private void StartRotateAnimation(double deltaDegrees)
        {
            if (Math.Abs(deltaDegrees) < 0.001)
                return;

            rotateAnimationRemainingDelta = deltaDegrees;
            isRotateAnimating = true;
            RenderRequest();
        }

        /// <summary>
        /// Stop the discrete key rotation animation immediately.
        /// </summary>
        private void StopRotateAnimation()
        {
            isRotateAnimating = false;
            rotateAnimationRemainingDelta = 0;
        }

        /// <summary>
        /// Advance the discrete key rotation animation and apply the transform in small steps.
        /// </summary>
        private void UpdateRotateAnimation(double deltaSeconds)
        {
            if (!isRotateAnimating || deltaSeconds <= 0)
                return;

            const double rotateSpeedDegreesPerSecond = 720.0;
            double maxStep = rotateSpeedDegreesPerSecond * deltaSeconds;
            double step = Math.Sign(rotateAnimationRemainingDelta)
                * Math.Min(Math.Abs(rotateAnimationRemainingDelta), maxStep);

            if (Math.Abs(step) < 0.001)
            {
                StopRotateAnimation();
                return;
            }

            rotateAnimationRemainingDelta -= step;
            Item.SetRotationCentered(Item.Rotation + step);

            if (Math.Abs(rotateAnimationRemainingDelta) < 0.001)
                StopRotateAnimation();
        }

    }
}
