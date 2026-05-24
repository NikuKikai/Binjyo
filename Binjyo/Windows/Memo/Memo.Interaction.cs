using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Drawing;
using System.Drawing.Imaging;
using System.Reflection;
using System.Runtime.InteropServices;

using Rect = System.Drawing.Rectangle;

namespace Binjyo
{
    public partial class Memo
    {
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

        private bool IsMoveGroupModifierDown()
        {
            return Keyboard.IsKeyDown(Key.LeftAlt) || Keyboard.IsKeyDown(Key.RightAlt);
        }

        private static List<Memo> GetVisibleAndHiddenMemos()
        {
            return Application.Current.Windows
                .OfType<Window>()
                .Where(item => item.Title == "Memo")
                .Cast<Memo>()
                .ToList();
        }

        private List<Memo> GetVisibleMemos()
        {
            return GetVisibleAndHiddenMemos()
                .Where(item => item.IsVisible)
                .ToList();
        }

        private static bool AreRectsConnected(System.Windows.Rect a, System.Windows.Rect b)
        {
            return a.Right >= b.Left && b.Right >= a.Left &&
                   a.Bottom >= b.Top && b.Bottom >= a.Top;
        }

        private System.Windows.Rect GetMemoBounds(Memo memo)
        {
            return new System.Windows.Rect(memo.anchorLeft, memo.anchorTop, memo.Width, memo.Height);
        }

        private List<Memo> GetConnectedMemoGroup()
        {
            var allMemos = GetVisibleMemos();
            var result = new List<Memo>();
            var queue = new Queue<Memo>();
            var visited = new HashSet<Memo>();
            queue.Enqueue(this);
            visited.Add(this);

            while (queue.Count > 0)
            {
                Memo current = queue.Dequeue();
                result.Add(current);
                System.Windows.Rect currentBounds = GetMemoBounds(current);

                foreach (Memo candidate in allMemos)
                {
                    if (visited.Contains(candidate))
                        continue;

                    if (AreRectsConnected(currentBounds, GetMemoBounds(candidate)))
                    {
                        visited.Add(candidate);
                        queue.Enqueue(candidate);
                    }
                }
            }

            return result;
        }

        private System.Windows.Rect GetBoundingBox(IEnumerable<Memo> memos)
        {
            double left = double.PositiveInfinity;
            double top = double.PositiveInfinity;
            double right = double.NegativeInfinity;
            double bottom = double.NegativeInfinity;

            foreach (Memo memo in memos)
            {
                left = Math.Min(left, memo.anchorLeft);
                top = Math.Min(top, memo.anchorTop);
                right = Math.Max(right, memo.anchorLeft + memo.Width);
                bottom = Math.Max(bottom, memo.anchorTop + memo.Height);
            }

            if (double.IsInfinity(left) || double.IsInfinity(top))
                return new System.Windows.Rect(0, 0, 0, 0);

            return new System.Windows.Rect(left, top, right - left, bottom - top);
        }

        private System.Windows.Rect GetBoundingBox(IDictionary<Memo, System.Windows.Point> positions)
        {
            double left = double.PositiveInfinity;
            double top = double.PositiveInfinity;
            double right = double.NegativeInfinity;
            double bottom = double.NegativeInfinity;

            foreach (var item in positions)
            {
                left = Math.Min(left, item.Value.X);
                top = Math.Min(top, item.Value.Y);
                right = Math.Max(right, item.Value.X + item.Key.Width);
                bottom = Math.Max(bottom, item.Value.Y + item.Key.Height);
            }

            if (double.IsInfinity(left) || double.IsInfinity(top))
                return new System.Windows.Rect(0, 0, 0, 0);

            return new System.Windows.Rect(left, top, right - left, bottom - top);
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
                TrySnapAgainstMemosForY(nextLeft, snappedLeft, nextTop, width, height, movingSet, ref snappedTop, ref bestDistanceY);
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

                if (!IntervalsOverlapOrTouch(currentTop, currentBottom, otherTop, otherBottom))
                    continue;

                TrySnapValue(nextLeft, otherLeft, ref snappedLeft, ref bestDistanceX);
                TrySnapValue(nextLeft, otherRight, ref snappedLeft, ref bestDistanceX);
                TrySnapValue(nextLeft, otherLeft - width, ref snappedLeft, ref bestDistanceX);
                TrySnapValue(nextLeft, otherRight - width, ref snappedLeft, ref bestDistanceX);
            }
        }

        private void TrySnapAgainstMemosForY(double nextLeft, double currentLeft, double nextTop, double width, double height, HashSet<Memo> movingSet, ref double snappedTop, ref double bestDistanceY)
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

                if (!IntervalsOverlapOrTouch(currentLeft, currentRight, otherLeft, otherRight))
                    continue;

                TrySnapValue(nextTop, otherTop, ref snappedTop, ref bestDistanceY);
                TrySnapValue(nextTop, otherBottom, ref snappedTop, ref bestDistanceY);
                TrySnapValue(nextTop, otherTop - height, ref snappedTop, ref bestDistanceY);
                TrySnapValue(nextTop, otherBottom - height, ref snappedTop, ref bestDistanceY);
            }
        }

        private void EnsureRectStaysReachable(ref double left, ref double top, double width, double height)
        {
            var rect = new System.Windows.Rect(left, top, width, height);
            if (HasMinimumVisibleArea(rect))
                return;

            var nearestScreen = System.Windows.Forms.Screen.AllScreens
                .OrderBy(screen => GetDistanceSquaredToScreen(rect, screen))
                .FirstOrDefault();

            if (nearestScreen == null)
                return;

            double screenLeft = nearestScreen.Bounds.Left / dpiFactor;
            double screenTop = nearestScreen.Bounds.Top / dpiFactor;
            double screenRight = nearestScreen.Bounds.Right / dpiFactor;
            double screenBottom = nearestScreen.Bounds.Bottom / dpiFactor;

            left = Clamp(left, screenLeft - width + MinVisiblePixels, screenRight - MinVisiblePixels);
            top = Clamp(top, screenTop - height + MinVisiblePixels, screenBottom - MinVisiblePixels);
        }

        private bool HasMinimumVisibleArea(System.Windows.Rect rect)
        {
            foreach (var screen in System.Windows.Forms.Screen.AllScreens)
            {
                var screenRect = new System.Windows.Rect(
                    screen.Bounds.Left / dpiFactor,
                    screen.Bounds.Top / dpiFactor,
                    screen.Bounds.Width / dpiFactor,
                    screen.Bounds.Height / dpiFactor);
                var intersection = System.Windows.Rect.Intersect(rect, screenRect);
                if (!intersection.IsEmpty &&
                    ((intersection.Width >= MinVisiblePixels && intersection.Height > 0) ||
                     (intersection.Height >= MinVisiblePixels && intersection.Width > 0)))
                {
                    return true;
                }
            }

            return false;
        }

        private double GetDistanceSquaredToScreen(System.Windows.Rect rect, System.Windows.Forms.Screen screen)
        {
            double screenLeft = screen.Bounds.Left / dpiFactor;
            double screenTop = screen.Bounds.Top / dpiFactor;
            double screenRight = screen.Bounds.Right / dpiFactor;
            double screenBottom = screen.Bounds.Bottom / dpiFactor;

            double dx = 0;
            if (rect.Right < screenLeft)
                dx = screenLeft - rect.Right;
            else if (rect.Left > screenRight)
                dx = rect.Left - screenRight;

            double dy = 0;
            if (rect.Bottom < screenTop)
                dy = screenTop - rect.Bottom;
            else if (rect.Top > screenBottom)
                dy = rect.Top - screenBottom;

            return dx * dx + dy * dy;
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

        private static bool IntervalsOverlapOrTouch(double startA, double endA, double startB, double endB)
        {
            return endA >= startB && endB >= startA;
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
                        this.CloseMemo();
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
                        this.CloseMemo();
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
                    _HideHSVWheel();
                    MoveByKeyboard(actualKey, e.IsRepeat);
                    e.Handled = true;
                    break;
                case Key.Tab:
                    if (!e.IsRepeat)
                        FocusMemoFromMousePosition();
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
                case Key.Left:
                    leftArrowRepeatCount = 0;
                    break;
                case Key.Right:
                    rightArrowRepeatCount = 0;
                    break;
                case Key.Up:
                    upArrowRepeatCount = 0;
                    break;
                case Key.Down:
                    downArrowRepeatCount = 0;
                    break;
                default:
                    break;
            }
        }

        private static Key GetActualKey(KeyEventArgs e)
        {
            return e.Key == Key.System ? e.SystemKey : e.Key;
        }

        private void FocusMemoFromMousePosition()
        {
            List<Memo> allMemos = GetVisibleMemos();
            List<Memo> candidates = GetMemosUnderMouse(allMemos);
            if (candidates.Count == 0)
                candidates = allMemos;

            if (candidates.Count == 0)
                return;

            Memo target = GetNextFocusTarget(allMemos, candidates);
            if (target == null)
                return;

            target.BringIntoMemoFocus();
        }

        private Memo GetNextFocusTarget(List<Memo> allMemos, List<Memo> candidates)
        {
            if (allMemos == null || candidates == null || candidates.Count == 0)
                return null;

            HashSet<Memo> candidateSet = new HashSet<Memo>(candidates);
            List<Memo> orderedCandidates = allMemos
                .Where(memo => candidateSet.Contains(memo))
                .ToList();
            Memo activeMemo = orderedCandidates.FirstOrDefault(memo => memo.IsActive);
            if (activeMemo == null)
                return orderedCandidates[0];

            if (orderedCandidates.Count == 1)
                return activeMemo;

            int currentIndex = orderedCandidates.IndexOf(activeMemo);
            return orderedCandidates[(currentIndex + 1) % orderedCandidates.Count];
        }

        private List<Memo> GetMemosUnderMouse(List<Memo> allMemos)
        {
            double mouseX = System.Windows.Forms.Control.MousePosition.X;
            double mouseY = System.Windows.Forms.Control.MousePosition.Y;

            return allMemos
                .Where(memo => memo.ContainsScreenPoint(mouseX, mouseY))
                .ToList();
        }

        private bool ContainsScreenPoint(double screenX, double screenY)
        {
            double localX = screenX / dpiFactor - Left;
            double localY = screenY / dpiFactor - Top;
            return localX >= 0 && localX < Width && localY >= 0 && localY < Height;
        }

        public void BringToMemoFocus()
        {
            BringIntoMemoFocus();
        }

        private void BringIntoMemoFocus()
        {
            if (!IsActive)
            {
                flashOnNextActivation = true;
                Activate();
                Focus();
                return;
            }

            flashOnNextActivation = false;
            Focus();
            MarkAsFocused();
            FlashFocusCue();
        }

        private void MarkAsFocused()
        {
            lastFocusOrder = ++focusSequence;
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

        private void MoveByKeyboard(Key key, bool isRepeat)
        {
            bool moveConnectedGroup = IsMoveGroupModifierDown();
            List<Memo> movingMemos = moveConnectedGroup ? GetConnectedMemoGroup() : new List<Memo> { this };

            if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))
            {
                MoveToNextSnap(key, movingMemos);
                return;
            }

            int repeatCount = UpdateArrowRepeatCount(key, isRepeat);
            double multiplier = (Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift)) ? 10 : 1;
            double acceleratedStep = multiplier * (1 + repeatCount / 4);
            double deltaX = 0;
            double deltaY = 0;

            switch (key)
            {
                case Key.Left:
                    deltaX = -acceleratedStep;
                    break;
                case Key.Right:
                    deltaX = acceleratedStep;
                    break;
                case Key.Up:
                    deltaY = -acceleratedStep;
                    break;
                case Key.Down:
                    deltaY = acceleratedStep;
                    break;
                default:
                    break;
            }

            ApplyMoveDelta(movingMemos, deltaX, deltaY, false);
        }

        private int UpdateArrowRepeatCount(Key key, bool isRepeat)
        {
            int repeatCount = isRepeat ? 1 : 0;
            switch (key)
            {
                case Key.Left:
                    leftArrowRepeatCount = isRepeat ? leftArrowRepeatCount + 1 : 0;
                    repeatCount = leftArrowRepeatCount;
                    break;
                case Key.Right:
                    rightArrowRepeatCount = isRepeat ? rightArrowRepeatCount + 1 : 0;
                    repeatCount = rightArrowRepeatCount;
                    break;
                case Key.Up:
                    upArrowRepeatCount = isRepeat ? upArrowRepeatCount + 1 : 0;
                    repeatCount = upArrowRepeatCount;
                    break;
                case Key.Down:
                    downArrowRepeatCount = isRepeat ? downArrowRepeatCount + 1 : 0;
                    repeatCount = downArrowRepeatCount;
                    break;
            }
            return repeatCount;
        }

        private void MoveToNextSnap(Key key, List<Memo> movingMemos)
        {
            System.Windows.Rect boundingBox = GetBoundingBox(movingMemos);
            double? target = null;
            switch (key)
            {
                case Key.Left:
                    target = GetNextSnapPositionX(movingMemos, boundingBox, false);
                    if (target.HasValue)
                        ApplyMoveDelta(movingMemos, target.Value - boundingBox.Left, 0, false);
                    break;
                case Key.Right:
                    target = GetNextSnapPositionX(movingMemos, boundingBox, true);
                    if (target.HasValue)
                        ApplyMoveDelta(movingMemos, target.Value - boundingBox.Left, 0, false);
                    break;
                case Key.Up:
                    target = GetNextSnapPositionY(movingMemos, boundingBox, false);
                    if (target.HasValue)
                        ApplyMoveDelta(movingMemos, 0, target.Value - boundingBox.Top, false);
                    break;
                case Key.Down:
                    target = GetNextSnapPositionY(movingMemos, boundingBox, true);
                    if (target.HasValue)
                        ApplyMoveDelta(movingMemos, 0, target.Value - boundingBox.Top, false);
                    break;
            }
        }

        private double? GetNextSnapPositionX(List<Memo> movingMemos, System.Windows.Rect boundingBox, bool forward)
        {
            List<double> candidates = new List<double>();
            double width = boundingBox.Width;
            double top = boundingBox.Top;
            double bottom = boundingBox.Bottom;
            var movingSet = new HashSet<Memo>(movingMemos);

            foreach (var screen in System.Windows.Forms.Screen.AllScreens)
            {
                double screenLeft = screen.Bounds.Left / dpiFactor;
                double screenRight = screen.Bounds.Right / dpiFactor;
                candidates.Add(screenLeft);
                candidates.Add(screenRight - width);
            }

            foreach (Memo item in GetVisibleMemos())
            {
                if (movingSet.Contains(item))
                    continue;

                if (!IntervalsOverlapOrTouch(top, bottom, item.anchorTop, item.anchorTop + item.Height))
                    continue;

                candidates.Add(item.anchorLeft);
                candidates.Add(item.anchorLeft + item.Width);
                candidates.Add(item.anchorLeft - width);
                candidates.Add(item.anchorLeft + item.Width - width);
            }

            return FindNextCandidate(boundingBox.Left, candidates, forward);
        }

        private double? GetNextSnapPositionY(List<Memo> movingMemos, System.Windows.Rect boundingBox, bool forward)
        {
            List<double> candidates = new List<double>();
            double height = boundingBox.Height;
            double left = boundingBox.Left;
            double right = boundingBox.Right;
            var movingSet = new HashSet<Memo>(movingMemos);

            foreach (var screen in System.Windows.Forms.Screen.AllScreens)
            {
                double screenTop = screen.Bounds.Top / dpiFactor;
                double screenBottom = screen.Bounds.Bottom / dpiFactor;
                candidates.Add(screenTop);
                candidates.Add(screenBottom - height);
            }

            foreach (Memo item in GetVisibleMemos())
            {
                if (movingSet.Contains(item))
                    continue;

                if (!IntervalsOverlapOrTouch(left, right, item.anchorLeft, item.anchorLeft + item.Width))
                    continue;

                candidates.Add(item.anchorTop);
                candidates.Add(item.anchorTop + item.Height);
                candidates.Add(item.anchorTop - height);
                candidates.Add(item.anchorTop + item.Height - height);
            }

            return FindNextCandidate(boundingBox.Top, candidates, forward);
        }

        private void ApplyMoveDelta(List<Memo> movingMemos, double deltaX, double deltaY, bool allowSnap)
        {
            if (movingMemos == null || movingMemos.Count == 0)
                return;

            var targetPositions = new Dictionary<Memo, System.Windows.Point>();
            foreach (Memo memo in movingMemos)
            {
                targetPositions[memo] = new System.Windows.Point(memo.anchorLeft + deltaX, memo.anchorTop + deltaY);
            }

            System.Windows.Rect boundingBox = GetBoundingBox(targetPositions);
            if (allowSnap)
            {
                GetMoveSnapAdjustment(movingMemos, targetPositions, boundingBox.Left, boundingBox.Top, boundingBox.Width, boundingBox.Height, out double snapOffsetX, out double snapOffsetY);
                if (snapOffsetX != 0 || snapOffsetY != 0)
                {
                    foreach (Memo memo in movingMemos)
                    {
                        var position = targetPositions[memo];
                        targetPositions[memo] = new System.Windows.Point(position.X + snapOffsetX, position.Y + snapOffsetY);
                    }
                    boundingBox = GetBoundingBox(targetPositions);
                }
            }

            GetPerMemoReachabilityOffset(targetPositions, out double constraintOffsetX, out double constraintOffsetY);

            foreach (Memo memo in movingMemos)
            {
                var position = targetPositions[memo];
                memo.SetAnchorPosition(position.X + constraintOffsetX, position.Y + constraintOffsetY);
            }
        }

        private void BeginDragAnchor(bool moveConnectedGroup)
        {
            dragMovesConnectedGroup = moveConnectedGroup;
            dragStartMouseX = System.Windows.Forms.Control.MousePosition.X;
            dragStartMouseY = System.Windows.Forms.Control.MousePosition.Y;
            dragStartPositions = new Dictionary<Memo, System.Windows.Point>();

            foreach (Memo memo in moveConnectedGroup ? GetConnectedMemoGroup() : new List<Memo> { this })
            {
                dragStartPositions[memo] = new System.Windows.Point(memo.anchorLeft, memo.anchorTop);
            }
        }

        private void RefreshDragAnchorIfNeeded()
        {
            bool shouldMoveConnectedGroup = IsMoveGroupModifierDown();
            if (dragStartPositions.Count == 0 || shouldMoveConnectedGroup != dragMovesConnectedGroup)
                BeginDragAnchor(shouldMoveConnectedGroup);
        }

        private static double? FindNextCandidate(double currentValue, IEnumerable<double> candidates, bool forward)
        {
            double? bestCandidate = null;
            foreach (double candidate in candidates)
            {
                if (forward)
                {
                    if (candidate <= currentValue + SnapDistance)
                        continue;
                    if (!bestCandidate.HasValue || candidate < bestCandidate.Value)
                        bestCandidate = candidate;
                }
                else
                {
                    if (candidate >= currentValue - SnapDistance)
                        continue;
                    if (!bestCandidate.HasValue || candidate > bestCandidate.Value)
                        bestCandidate = candidate;
                }
            }
            return bestCandidate;
        }

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

            if (!CanInteractNormally())
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

            isdrag = true;
            dragStartLeft = Left;
            dragStartTop = Top;
            BeginDragAnchor(IsMoveGroupModifierDown());
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

        private void Window_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
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

                _HideHSVWheel();
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
                    UpdateDragFromMouse();
                else
                    isdrag = false;
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

        private void UpdateDragFromMouse()
        {
            RefreshDragAnchorIfNeeded();
            double xx = System.Windows.Forms.Control.MousePosition.X;
            double yy = System.Windows.Forms.Control.MousePosition.Y;
            double deltaX = (xx - dragStartMouseX) / dpiFactor;
            double deltaY = (yy - dragStartMouseY) / dpiFactor;
            var movingMemos = dragStartPositions.Keys.ToList();
            var targetPositions = new Dictionary<Memo, System.Windows.Point>();

            foreach (var item in dragStartPositions)
            {
                targetPositions[item.Key] = new System.Windows.Point(item.Value.X + deltaX, item.Value.Y + deltaY);
            }

            System.Windows.Rect boundingBox = GetBoundingBox(targetPositions);
            GetMoveSnapAdjustment(movingMemos, targetPositions, boundingBox.Left, boundingBox.Top, boundingBox.Width, boundingBox.Height, out double snapOffsetX, out double snapOffsetY);
            if (snapOffsetX != 0 || snapOffsetY != 0)
            {
                foreach (Memo memo in movingMemos)
                {
                    var position = targetPositions[memo];
                    targetPositions[memo] = new System.Windows.Point(position.X + snapOffsetX, position.Y + snapOffsetY);
                }
                boundingBox = GetBoundingBox(targetPositions);
            }

            GetPerMemoReachabilityOffset(targetPositions, out double constraintOffsetX, out double constraintOffsetY);

            foreach (Memo memo in movingMemos)
            {
                var position = targetPositions[memo];
                memo.SetAnchorPosition(position.X + constraintOffsetX, position.Y + constraintOffsetY);
            }
        }

        private void GetPerMemoReachabilityOffset(Dictionary<Memo, System.Windows.Point> targetPositions, out double offsetX, out double offsetY)
        {
            offsetX = 0;
            offsetY = 0;

            double minOffsetX = double.NegativeInfinity;
            double maxOffsetX = double.PositiveInfinity;
            double minOffsetY = double.NegativeInfinity;
            double maxOffsetY = double.PositiveInfinity;

            foreach (var item in targetPositions)
            {
                GetReachableOffsetRangeForMemo(item.Key, item.Value.X, item.Value.Y,
                    out double itemMinOffsetX, out double itemMaxOffsetX,
                    out double itemMinOffsetY, out double itemMaxOffsetY);

                minOffsetX = Math.Max(minOffsetX, itemMinOffsetX);
                maxOffsetX = Math.Min(maxOffsetX, itemMaxOffsetX);
                minOffsetY = Math.Max(minOffsetY, itemMinOffsetY);
                maxOffsetY = Math.Min(maxOffsetY, itemMaxOffsetY);
            }

            offsetX = Clamp(0, minOffsetX, maxOffsetX);
            offsetY = Clamp(0, minOffsetY, maxOffsetY);
        }

        private void GetReachableOffsetRangeForMemo(Memo memo, double targetLeft, double targetTop,
            out double minOffsetX, out double maxOffsetX,
            out double minOffsetY, out double maxOffsetY)
        {
            minOffsetX = double.NegativeInfinity;
            maxOffsetX = double.PositiveInfinity;
            minOffsetY = double.NegativeInfinity;
            maxOffsetY = double.PositiveInfinity;

            var targetRect = new System.Windows.Rect(targetLeft, targetTop, memo.Width, memo.Height);
            if (HasMinimumVisibleArea(targetRect))
                return;

            var nearestScreen = System.Windows.Forms.Screen.AllScreens
                .OrderBy(screen => GetDistanceSquaredToScreen(targetRect, screen))
                .FirstOrDefault();

            if (nearestScreen == null)
                return;

            double screenLeft = nearestScreen.Bounds.Left / dpiFactor;
            double screenTop = nearestScreen.Bounds.Top / dpiFactor;
            double screenRight = nearestScreen.Bounds.Right / dpiFactor;
            double screenBottom = nearestScreen.Bounds.Bottom / dpiFactor;

            minOffsetX = screenLeft - memo.Width + MinVisiblePixels - targetLeft;
            maxOffsetX = screenRight - MinVisiblePixels - targetLeft;
            minOffsetY = screenTop - memo.Height + MinVisiblePixels - targetTop;
            maxOffsetY = screenBottom - MinVisiblePixels - targetTop;
        }

        private void Window_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (isEditMode)
            {
                EndDrawingStroke();
                e.Handled = true;
                return;
            }

            isdrag = false;
            dragStartPositions.Clear();
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
            this.CloseMemo();
        }

        private void Window_MouseEnter(object sender, MouseEventArgs e)
        {
            RefreshHSVWheelVisibility();
        }

        private void Window_MouseLeave(object sender, MouseEventArgs e)
        {
            _HideHSVWheel();
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

        private void Window_Deactivated(object sender, EventArgs e)
        {
            if (isEditMode)
                ExitEditMode();
            if (isResizeMode)
                SetResizeMode(false);

            isdrag = false;
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
            MarkAsFocused();
            RefreshAllMemoFeatureOverlays();
            if (flashOnNextActivation)
            {
                flashOnNextActivation = false;
                FlashFocusCue();
            }
            RefreshHSVWheelVisibility();
        }

        private void Window_PreviewMouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
        }

    }
}
