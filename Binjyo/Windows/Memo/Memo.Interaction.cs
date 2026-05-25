using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Drawing;


namespace Binjyo
{
    public partial class Memo
    {
        private bool isdrag { get => Scene.IsDragMoving; }
        private double dragStartMouseX, dragStartMouseY;
        private bool dragMovesConnectedGroup = false;
        private Dictionary<Memo, System.Windows.Point> dragStartPositions = new Dictionary<Memo, System.Windows.Point>();

        private bool IsResizeSnapEnabled()
        {
            bool isDefaultEnabled = Properties.Settings.Default.SnapMemo;
            return IsSnapToggleModifierDown() ? !isDefaultEnabled : isDefaultEnabled;
        }

        private bool IsMoveSnapEnabled()
        {
            bool isDefaultEnabled = Properties.Settings.Default.SnapMemo;
            return IsSnapToggleModifierDown() ? !isDefaultEnabled : isDefaultEnabled;
        }

        private bool IsSnapToggleModifierDown()
        {
            return Keyboard.IsKeyDown(Key.Space);
        }

        private List<Memo> GetVisibleMemos()
        {
            return GetAllMemos()
                .Where(item => item.IsVisible)
                .ToList();
        }


        private void GetMoveSnapAdjustment(
            ICollection<Memo> movingMemos,
            IDictionary<Memo, System.Windows.Point> targetPositions,
            double nextLeft,
            double nextTop,
            double width,
            double height,
            out double offsetX,
            out double offsetY)
        {
            offsetX = 0;
            offsetY = 0;

            if (!IsMoveSnapEnabled())
                return;

            double snappedLeft = nextLeft;
            double snappedTop = nextTop;
            double bestDistanceX = SnapDistance + 1;
            double bestDistanceY = SnapDistance + 1;
            var movingSet = new HashSet<Memo>(movingMemos);
            bool disableMemoEdgeSnap = isFeaturePointModeEnabled && isdrag;

            foreach (var screen in System.Windows.Forms.Screen.AllScreens)
            {
                double screenLeft = screen.Bounds.Left / dpiFactor;
                double screenTop = screen.Bounds.Top / dpiFactor;
                double screenRight = screen.Bounds.Right / dpiFactor;
                double screenBottom = screen.Bounds.Bottom / dpiFactor;

                TrySnapValue(nextLeft, screenLeft, ref snappedLeft, ref bestDistanceX);
                TrySnapValue(nextLeft, screenRight - width, ref snappedLeft, ref bestDistanceX);
                TrySnapValue(nextTop, screenTop, ref snappedTop, ref bestDistanceY);
                TrySnapValue(nextTop, screenBottom - height, ref snappedTop, ref bestDistanceY);
            }

            if (!disableMemoEdgeSnap)
            {
                TrySnapAgainstMemosForX(nextLeft, nextTop, width, height, movingSet, ref snappedLeft, ref bestDistanceX);
                TrySnapAgainstMemosForY(snappedLeft, nextTop, width, height, movingSet, ref snappedTop, ref bestDistanceY);
                TrySnapAgainstMemosForX(nextLeft, snappedTop, width, height, movingSet, ref snappedLeft, ref bestDistanceX);
            }

            offsetX = snappedLeft - nextLeft;
            offsetY = snappedTop - nextTop;

            if (targetPositions != null &&
                TryGetFeatureAlignmentSnapOffset(targetPositions, movingSet, out double alignmentOffsetX, out double alignmentOffsetY))
            {
                offsetX = alignmentOffsetX;
                offsetY = alignmentOffsetY;
            }
        }

        private void TrySnapAgainstMemosForX(double nextLeft, double currentTop, double width, double height, HashSet<Memo> movingSet, ref double snappedLeft, ref double bestDistanceX)
        {
            double currentBottom = currentTop + height;
            foreach (Memo item in GetVisibleMemos())
            {
                if (movingSet.Contains(item))
                    continue;

                double otherLeft = item.anchorLeft;
                double otherTop = item.anchorTop;
                double otherRight = item.anchorLeft + item.Width;
                double otherBottom = item.anchorTop + item.Height;

                if (!Geo.DoSegmentsOverlap(currentTop, currentBottom, otherTop, otherBottom))
                    continue;

                TrySnapValue(nextLeft, otherLeft, ref snappedLeft, ref bestDistanceX);
                TrySnapValue(nextLeft, otherRight, ref snappedLeft, ref bestDistanceX);
                TrySnapValue(nextLeft, otherLeft - width, ref snappedLeft, ref bestDistanceX);
                TrySnapValue(nextLeft, otherRight - width, ref snappedLeft, ref bestDistanceX);
            }
        }

        private void TrySnapAgainstMemosForY(double currentLeft, double nextTop, double width, double height, HashSet<Memo> movingSet, ref double snappedTop, ref double bestDistanceY)
        {
            double currentRight = currentLeft + width;
            foreach (Memo item in GetVisibleMemos())
            {
                if (movingSet.Contains(item))
                    continue;

                double otherLeft = item.anchorLeft;
                double otherTop = item.anchorTop;
                double otherRight = item.anchorLeft + item.Width;
                double otherBottom = item.anchorTop + item.Height;

                if (!Geo.DoSegmentsOverlap(currentLeft, currentRight, otherLeft, otherRight))
                    continue;

                TrySnapValue(nextTop, otherTop, ref snappedTop, ref bestDistanceY);
                TrySnapValue(nextTop, otherBottom, ref snappedTop, ref bestDistanceY);
                TrySnapValue(nextTop, otherTop - height, ref snappedTop, ref bestDistanceY);
                TrySnapValue(nextTop, otherBottom - height, ref snappedTop, ref bestDistanceY);
            }
        }


        private static double Clamp(double value, double minimum, double maximum)
        {
            if (maximum < minimum)
                return minimum;
            return Math.Max(minimum, Math.Min(maximum, value));
        }

        private static int PercentToThreshold(int percent)
        {
            return (int)Math.Round(255 * percent / 100.0);
        }

        private static int ThresholdToPercent(int threshold)
        {
            return (int)Math.Round(threshold * 100.0 / 255.0 / 10.0) * 10;
        }

        private static int GetClosestOption(int value, IEnumerable<int> options)
        {
            return options
                .OrderBy(option => Math.Abs(option - value))
                .First();
        }

        private static void TrySnapValue(double nextValue, double targetValue, ref double snappedValue, ref double bestDistance)
        {
            double distance = Math.Abs(nextValue - targetValue);
            if (distance <= SnapDistance && distance < bestDistance)
            {
                snappedValue = targetValue;
                bestDistance = distance;
            }
        }


        protected override void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi)
        {
            dpiFactor = newDpi.DpiScaleX;
            Console.WriteLine("DPI Changed");
            Console.WriteLine(newDpi.DpiScaleX);
            Console.WriteLine(Left);
        }


        private bool isEditedDuringKeyB = false;
        private bool isEditedDuringKeyQ = false;
        private bool isEditedDuringKeyO = false;
        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            Key actualKey = GetActualKey(e);

            if (isEditMode)
            {
                switch (actualKey)
                {
                    case Key.Escape:
                    case Key.Enter:
                        if (!e.IsRepeat)
                            ExitEditMode();
                        break;
                    case Key.E:
                        if (!e.IsRepeat)
                            SetEditTool(EditTool.Eraser);
                        break;
                    case Key.Q:
                        if (!e.IsRepeat)
                            SetEditTool(EditTool.Brush);
                        break;
                    case Key.Z:
                        if (!e.IsRepeat)
                            UndoLastDrawingStroke();
                        break;
                    case Key.Oem4:
                        if (!e.IsRepeat)
                            AdjustDrawingBrushSize(-1);
                        break;
                    case Key.Oem6:
                        if (!e.IsRepeat)
                            AdjustDrawingBrushSize(1);
                        break;
                    default:
                        break;
                }

                e.Handled = true;
                return;
            }

            switch (actualKey)
            {
                case Key.Escape:
                    if (isResizeMode)
                    {
                        SetResizeMode(false);
                    }
                    else
                    {
                        Scene.CloseItem(sceneItem.Id);
                    }
                    e.Handled = true;
                    break;
                case Key.S:
                    if (!e.IsRepeat)
                    {
                        if (Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift))
                            Save(false);
                        else
                            Save();
                    }
                    e.Handled = true;
                    break;
                case Key.C:
                    if (!e.IsRepeat)
                    {
                        bool includeDrawing = !(Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift));
                        CopyMemoToClipboard(includeDrawing);
                        e.Handled = true;
                    }
                    break;
                case Key.X:
                    if (!e.IsRepeat)
                    {
                        bool includeDrawing = !(Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift));
                        CopyMemoToClipboard(includeDrawing);
                        Scene.CloseItem(sceneItem.Id);
                        e.Handled = true;
                    }
                    break;
                case Key.OemTilde:
                    ResetSize();
                    break;
                case Key.T:
                    if (!e.IsRepeat)
                        SetResizeMode(!isResizeMode);
                    e.Handled = true;
                    break;
                case Key.E:
                    if (!e.IsRepeat)
                        EnterEditMode();
                    e.Handled = true;
                    break;
                case Key.LeftAlt:
                case Key.RightAlt:
                    e.Handled = true;
                    break;
                case Key.F:
                    if (!e.IsRepeat)
                    {
                        if (Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift))
                        {
                            FlipDrawingVertically();
                            this.bitmapTransformed.RotateFlip(RotateFlipType.RotateNoneFlipY);
                            geometryTransformHistory.Add('V');
                        }
                        else
                        {
                            FlipDrawingHorizontally();
                            this.bitmapTransformed.RotateFlip(RotateFlipType.RotateNoneFlipX);
                            geometryTransformHistory.Add('H');
                        }
                        UpdateBitmap();
                    }
                    break;
                case Key.G:
                    if (!e.IsRepeat)
                        ToggleGrayscale();
                    break;
                case Key.H:
                    if (!e.IsRepeat)
                        ToggleHueMap();
                    break;
                case Key.P:
                    if (!e.IsRepeat)
                        ToggleFeaturePoints();
                    e.Handled = true;
                    break;
                case Key.CapsLock:
                    if (!e.IsRepeat)
                    {
                        isHSVWheelPinnedGlobally = !isHSVWheelPinnedGlobally;
                        RefreshAllMemoHSVWheelVisibility();
                    }
                    break;
                case Key.R:
                    RotateDrawing90();
                    this.bitmapTransformed.RotateFlip(RotateFlipType.Rotate90FlipNone);
                    geometryTransformHistory.Add('R');
                    UpdateBitmap();
                    break;
                case Key.Left:
                case Key.Right:
                case Key.Up:
                case Key.Down:
                    HideHSVWheel();
                    Scene.MoveByKey(Id, actualKey, e.IsRepeat);
                    e.Handled = true;
                    break;
                case Key.Tab:
                    if (!e.IsRepeat)
                        Scene.FocusNext();
                    e.Handled = true;
                    break;
                case Key.B:
                    if (!e.IsRepeat)
                        isEditedDuringKeyB = false;
                    break;
                case Key.Q:
                    if (!e.IsRepeat)
                        isEditedDuringKeyQ = false;
                    break;
                case Key.O:
                    if (!e.IsRepeat)
                        isEditedDuringKeyO = false;
                    break;
                default:
                    break;
            }
        }
        private void Window_KeyUp(object sender, KeyEventArgs e)
        {
            Key actualKey = GetActualKey(e);
            if (isEditMode)
            {
                e.Handled = true;
                return;
            }

            switch (actualKey)
            {
                case Key.LeftAlt:
                case Key.RightAlt:
                    e.Handled = true;
                    break;
                case Key.B:
                    if (!isEditedDuringKeyB)
                        ToggleBinarization();
                    break;
                case Key.Q:
                    if (!isEditedDuringKeyQ)
                        ToggleQuantization();
                    break;
                case Key.O:
                    if (!isEditedDuringKeyO)
                        ToggleTransparency();
                    break;
                default:
                    break;
            }
        }

        private static Key GetActualKey(KeyEventArgs e)
        {
            return e.Key == Key.System ? e.SystemKey : e.Key;
        }

        private bool ContainsScreenPoint(double screenX, double screenY)
        {
            double localX = screenX / dpiFactor - Left;
            double localY = screenY / dpiFactor - Top;
            return localX >= 0 && localX < Width && localY >= 0 && localY < Height;
        }


        private void FlashFocusCue()
        {
            if (focusFlashOverlay == null)
                return;

            var animation = new DoubleAnimationUsingKeyFrames();
            animation.KeyFrames.Add(new DiscreteDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.Zero)));
            animation.KeyFrames.Add(new LinearDoubleKeyFrame(0.5, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(70))));
            animation.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(180))));
            focusFlashOverlay.BeginAnimation(UIElement.OpacityProperty, animation);
        }


        #region =================== Mouse Hanlder ===================

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Right)
                return;

            if (!IsActive)
                flashOnNextActivation = true;

            if (isEditMode)
            {
                if (e.ChangedButton == MouseButton.Left)
                    BeginDrawingStroke(e.GetPosition(this));
                e.Handled = true;
                return;
            }

            if (!CanInteract)
                return;

            if (isResizeMode)
            {
                ResizeHandle handle = GetResizeHandleAtMousePosition();
                if (IsResizeHandle(handle))
                {
                    BeginResize(handle);
                    return;
                }
            }

            Scene.DragMoveStart(Id);

            if (isFeaturePointModeEnabled)
                image.Opacity = 0.5;
            Mouse.Capture(this);
        }

        private void Window_ContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            lastContextMenuScreenX = System.Windows.Forms.Control.MousePosition.X;
            lastContextMenuScreenY = System.Windows.Forms.Control.MousePosition.Y;

            if (isResizeMode)
            {
                SetResizeMode(false);
                e.Handled = true;
                return;
            }

            if (isEditMode)
            {
                e.Handled = true;
                return;
            }

            UpdateContextMenuState();
        }


        private void Window_MouseMove(object sender, MouseEventArgs e)
        {
            if (isEditMode)
            {
                Cursor = currentEditTool == EditTool.Brush ? Cursors.Pen : Cursors.Cross;
                if (isDrawingStroke && Mouse.LeftButton == MouseButtonState.Pressed)
                    ExtendDrawingStroke(e.GetPosition(this));
                else if (isDrawingStroke)
                    EndDrawingStroke();

                HideHSVWheel();
                return;
            }

            if (isResizing)
            {
                if (Mouse.LeftButton == MouseButtonState.Pressed)
                    UpdateResizeFromMouse();
                else
                    StopResize();
            }
            else if (isdrag)
            {
                if (Mouse.LeftButton == MouseButtonState.Pressed)
                    Scene.DragMoveUpdate();
                else
                    Scene.DragMoveEnd();
            }

            if (isResizeMode)
            {
                if (isResizing)
                {
                    Cursor = GetCursorForResizeHandle(activeResizeHandle);
                }
                else
                {
                    Cursor = GetCursorForResizeHandle(GetResizeHandleAtMousePosition());
                }
            }
            else
            {
                Cursor = Cursors.Arrow;
            }

            RefreshHSVWheelVisibility();
        }

        private void Window_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (isEditMode)
            {
                EndDrawingStroke();
                e.Handled = true;
                return;
            }

            Scene.DragMoveEnd();

            if (isFeaturePointModeEnabled)
                image.Opacity = 1;
            StopResize();
            if (Mouse.Captured == this)
                Mouse.Capture(null);
        }

        private void Window_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (isEditMode)
                return;

            bool includeDrawing = !(Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift));
            CopyMemoToClipboard(includeDrawing);
            Scene.CloseItem(sceneItem.Id);
        }

        private void Window_MouseEnter(object sender, MouseEventArgs e)
        {
            RefreshHSVWheelVisibility();
        }

        private void Window_MouseLeave(object sender, MouseEventArgs e)
        {
            HideHSVWheel();
        }

        private void Window_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (isEditMode)
                return;

            if (Keyboard.IsKeyDown(Key.LeftCtrl))
            {
                if (e.Delta > 0) ResizeDelta(0.1);
                else ResizeDelta(-0.1);
            }
            else if (Keyboard.IsKeyDown(Key.B))
            {
                isEditedDuringKeyB = true;
                isEffectBinarize = true; isEffectQuantize = false;
                pEffectBinarize = Math.Max(Math.Min(pEffectBinarize + 15 * Math.Sign(e.Delta), 250), 5);
                UpdateBitmap();
                ShowCenterInfoFading("Binarization", $"{ThresholdToPercent(pEffectBinarize)}%");
            }
            else if (Keyboard.IsKeyDown(Key.Q))
            {
                isEditedDuringKeyQ = true;
                isEffectQuantize = true; isEffectBinarize = false;
                pEffectQuantize = Math.Max(Math.Min(pEffectQuantize + 1 * Math.Sign(e.Delta), 16), 3);
                UpdateBitmap();
                ShowCenterInfoFading("Quantization", $"{pEffectQuantize} levels");
            }
            else if (Keyboard.IsKeyDown(Key.O))
            {
                isEditedDuringKeyO = true;
                isEffectTransparent = true;
                pEffectTransparent = Math.Max(Math.Min(pEffectTransparent + 15 * Math.Sign(e.Delta), 245), 10);
                UpdateBitmap();
                ShowCenterInfoFading("Transparency", $"{ThresholdToPercent(pEffectTransparent)}%");
            }
        }

        #endregion

        private void Window_Deactivated(object sender, EventArgs e)
        {
            if (isEditMode)
                ExitEditMode();
            if (isResizeMode)
                SetResizeMode(false);

            Scene.DragMoveEnd();

            dragStartPositions.Clear();
            if (isFeaturePointModeEnabled)
                image.Opacity = 1;
            StopResize();
            if (Mouse.Captured == this)
                Mouse.Capture(null);
            RefreshHSVWheelVisibility();
        }

        private void Window_Activated(object sender, EventArgs e)
        {
            RefreshAllMemoFeatureOverlays();
            if (flashOnNextActivation)
            {
                flashOnNextActivation = false;
                FlashFocusCue();
            }
            RefreshHSVWheelVisibility();
        }

    }
}
