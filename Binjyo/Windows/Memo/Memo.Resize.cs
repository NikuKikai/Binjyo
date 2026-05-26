using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Drawing;
using System.Drawing.Imaging;
using System.Reflection;
using System.Runtime.InteropServices;

using Rect = System.Drawing.Rectangle;

namespace Binjyo
{
    public enum ResizeHandle
    {
        None,
        TopLeft,
        TopRight,
        BottomLeft,
        BottomRight
    }


    public partial class Memo
    {
        private bool isResizing = false;
        private bool isResizeMode = false;
        private double resizeStartScale = 1;
        private double resizeStartLeft = 0;
        private double resizeStartTop = 0;
        private double resizeStartRight = 0;
        private double resizeStartBottom = 0;


        private void SetResizeMode(bool enabled)
        {
            if (isEditMode) return;

            isResizeMode = enabled && CanInteract;
            if (!isResizeMode)
                StopResize();
            UpdateResizeVisuals();
        }

        private void BeginResize(ResizeHandle handle)
        {
            if (handle == ResizeHandle.None) return;
            activeResizeHandle = handle;
            isResizing = true;
            resizeStartScale = scale;
            resizeStartLeft = Left;
            resizeStartTop = Top;
            resizeStartRight = Left + Width;
            resizeStartBottom = Top + Height;
            dragStartMouseX = System.Windows.Forms.Control.MousePosition.X;
            dragStartMouseY = System.Windows.Forms.Control.MousePosition.Y;
            Mouse.Capture(this);
            UpdateResizeInfoOverlay(scale, Width, Height);
        }
        private void UpdateResize()
        {
            double mouseX = System.Windows.Forms.Control.MousePosition.X;
            double mouseY = System.Windows.Forms.Control.MousePosition.Y;
            double deltaX = (mouseX - dragStartMouseX) / dpiFactor;
            double deltaY = (mouseY - dragStartMouseY) / dpiFactor;

            double rawScale = CalcResizeScaleFromMouseDelta(deltaX, deltaY);
            rawScale = ClampScale(rawScale);

            double snappedScale = ApplyResizeSnap(rawScale);
            CalcBoundsByScale(
                snappedScale,
                out double nextLeft, out double nextTop,
                out double nextWidth, out double nextHeight
            );

            Item.SetScale(snappedScale);
            Item.SetPos(nextLeft, nextTop);
            UpdateResizeInfoOverlay(snappedScale, nextWidth, nextHeight);
        }
        private void StopResize()
        {
            isResizing = false;
            activeResizeHandle = ResizeHandle.None;
            if (Mouse.Captured == this)
                Mouse.Capture(null);
            HideCenterInfo();
        }

        private void UpdateResizeVisuals()
        {
            resizeOverlay.Visibility = isResizeMode && CanInteract && !isEditMode
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        private ResizeHandle GetResizeHandleAtMousePosition()
        {
            double localX = System.Windows.Forms.Control.MousePosition.X / dpiFactor - Left;
            double localY = System.Windows.Forms.Control.MousePosition.Y / dpiFactor - Top;

            if (localX >= ResizeHandleInset && localX <= ResizeHandleInset + ResizeHandleSize &&
                localY >= ResizeHandleInset && localY <= ResizeHandleInset + ResizeHandleSize)
                return ResizeHandle.TopLeft;

            if (localX >= Width - ResizeHandleInset - ResizeHandleSize && localX <= Width - ResizeHandleInset &&
                localY >= ResizeHandleInset && localY <= ResizeHandleInset + ResizeHandleSize)
                return ResizeHandle.TopRight;

            if (localX >= ResizeHandleInset && localX <= ResizeHandleInset + ResizeHandleSize &&
                localY >= Height - ResizeHandleInset - ResizeHandleSize && localY <= Height - ResizeHandleInset)
                return ResizeHandle.BottomLeft;

            if (localX >= Width - ResizeHandleInset - ResizeHandleSize && localX <= Width - ResizeHandleInset &&
                localY >= Height - ResizeHandleInset - ResizeHandleSize && localY <= Height - ResizeHandleInset)
                return ResizeHandle.BottomRight;

            return ResizeHandle.None;
        }

        private Cursor GetCursorForResizeHandle(ResizeHandle handle)
        {
            switch (handle)
            {
                case ResizeHandle.TopLeft:
                case ResizeHandle.BottomRight:
                    return Cursors.SizeNWSE;
                case ResizeHandle.TopRight:
                case ResizeHandle.BottomLeft:
                    return Cursors.SizeNESW;
                default:
                    return Cursors.Arrow;
            }
        }

        private double ClampScale(double s)
        {
            return Math.Max(Item.GetMinScale(), Math.Min(Item.GetMaxScale(), s));
        }

        private void UpdateResizeInfoOverlay(double currentScale, double currentWidth, double currentHeight)
        {
            string scaleText = $"{Math.Round(currentScale * 100):0}%";
            string sizeText = $"{Math.Round(currentWidth):0} x {Math.Round(currentHeight):0}";
            ShowCenterInfoPersistent(scaleText, sizeText);
        }

        private double CalcResizeScaleFromMouseDelta(double deltaX, double deltaY)
        {
            var handleVectorX = Item.GetBaseWidth();
            var handleVectorY = Item.GetBaseHeight();

            switch (activeResizeHandle)
            {
                case ResizeHandle.TopLeft:
                    handleVectorX = -handleVectorX;
                    handleVectorY = -handleVectorY;
                    break;
                case ResizeHandle.TopRight:
                    handleVectorY = -handleVectorY;
                    break;
                case ResizeHandle.BottomLeft:
                    handleVectorX = -handleVectorX;
                    break;
                case ResizeHandle.BottomRight:
                    break;
            }

            double denominator = handleVectorX * handleVectorX + handleVectorY * handleVectorY;
            if (denominator <= 0.0001)
                return resizeStartScale;

            double projectedScaleDelta = (deltaX * handleVectorX + deltaY * handleVectorY) / denominator;
            return resizeStartScale + projectedScaleDelta;
        }

        private double ApplyResizeSnap(double rawScale)
        {
            if (Keyboard.IsKeyDown(Key.Space) == Properties.Settings.Default.SnapMemo ||
                activeResizeHandle == ResizeHandle.None)
                return rawScale;

            CalcBoundsByScale(
                rawScale, out double rawLeft, out double rawTop, out double rawWidth, out double rawHeight
            );
            double rawRight = rawLeft + rawWidth;
            double rawBottom = rawTop + rawHeight;
            double bestScale = rawScale;
            double bestDistance = SnapDistance + 1;

            foreach (var screen in System.Windows.Forms.Screen.AllScreens)
            {
                TryResizeSnapCandidate(rawScale, screen.Bounds.Left / dpiFactor, true, ref bestScale, ref bestDistance);
                TryResizeSnapCandidate(rawScale, screen.Bounds.Right / dpiFactor, true, ref bestScale, ref bestDistance);
                TryResizeSnapCandidate(rawScale, screen.Bounds.Top / dpiFactor, false, ref bestScale, ref bestDistance);
                TryResizeSnapCandidate(rawScale, screen.Bounds.Bottom / dpiFactor, false, ref bestScale, ref bestDistance);
            }

            foreach (Window other in Application.Current.Windows)
            {
                if (other == this || other.Title != "Memo" || !other.IsVisible)
                    continue;

                if (IsResizeX() &&
                    Geo.DoSegmentsOverlap(rawTop, rawBottom, other.Top, other.Top + other.Height))
                {
                    TryResizeSnapCandidate(rawScale, other.Left, true, ref bestScale, ref bestDistance);
                    TryResizeSnapCandidate(rawScale, other.Left + other.Width, true, ref bestScale, ref bestDistance);
                }

                if (IsResizeY() &&
                    Geo.DoSegmentsOverlap(rawLeft, rawRight, other.Left, other.Left + other.Width))
                {
                    TryResizeSnapCandidate(rawScale, other.Top, false, ref bestScale, ref bestDistance);
                    TryResizeSnapCandidate(rawScale, other.Top + other.Height, false, ref bestScale, ref bestDistance);
                }
            }

            return bestScale;
        }

        private void CalcBoundsByScale(
            double targetScale, out double nextLeft, out double nextTop, out double nextWidth, out double nextHeight)
        {
            nextWidth = Item.GetBaseWidth() * targetScale;
            nextHeight = Item.GetBaseHeight() * targetScale;

            switch (activeResizeHandle)
            {
                case ResizeHandle.TopLeft:
                    nextLeft = resizeStartRight - nextWidth;
                    nextTop = resizeStartBottom - nextHeight;
                    break;
                case ResizeHandle.TopRight:
                    nextLeft = resizeStartLeft;
                    nextTop = resizeStartBottom - nextHeight;
                    break;
                case ResizeHandle.BottomLeft:
                    nextLeft = resizeStartRight - nextWidth;
                    nextTop = resizeStartTop;
                    break;
                case ResizeHandle.BottomRight:
                default:
                    nextLeft = resizeStartLeft;
                    nextTop = resizeStartTop;
                    break;
            }
        }

        private void TryResizeSnapCandidate(
            double scale, double targetEdge, bool horizontalEdge, ref double bestScale, ref double bestDistance)
        {
            if (!TryGetResizeScaleForTarget(targetEdge, horizontalEdge, out double candidateScale))
                return;

            candidateScale = ClampScale(candidateScale);
            CalcBoundsByScale(candidateScale, out double candidateLeft, out double candidateTop, out double candidateWidth, out double candidateHeight);

            double rawMovingEdge = GetMovingEdge(scale, horizontalEdge);
            double candidateMovingEdge = horizontalEdge
                ? (IsResizeLeft ? candidateLeft : candidateLeft + candidateWidth)
                : (IsResizeTop ? candidateTop : candidateTop + candidateHeight);

            double distance = Math.Abs(rawMovingEdge - targetEdge);
            if (distance <= SnapDistance && distance < bestDistance && Math.Abs(candidateMovingEdge - targetEdge) < 0.001)
            {
                bestScale = candidateScale;
                bestDistance = distance;
            }
        }

        private double GetMovingEdge(double candidateScale, bool horizontalEdge)
        {
            CalcBoundsByScale(candidateScale, out double candidateLeft, out double candidateTop, out double candidateWidth, out double candidateHeight);
            if (horizontalEdge)
                return IsResizeLeft ? candidateLeft : candidateLeft + candidateWidth;
            return IsResizeTop ? candidateTop : candidateTop + candidateHeight;
        }

        private bool TryGetResizeScaleForTarget(double targetEdge, bool horizontalEdge, out double scale)
        {
            scale = Item.Scale;
            if (horizontalEdge)
            {
                if (IsResizeLeft)
                {
                    scale = (resizeStartRight - targetEdge) / Item.GetBaseWidth();
                    return true;
                }
                if (IsResizeRight)
                {
                    scale = (targetEdge - resizeStartLeft) / Item.GetBaseWidth();
                    return true;
                }
                return false;
            }

            if (IsResizeTop)
            {
                scale = (resizeStartBottom - targetEdge) / Item.GetBaseHeight();
                return true;
            }
            if (IsResizeBottom)
            {
                scale = (targetEdge - resizeStartTop) / Item.GetBaseHeight();
                return true;
            }
            return false;
        }

        private bool IsResizeLeft => activeResizeHandle == ResizeHandle.TopLeft || activeResizeHandle == ResizeHandle.BottomLeft;
        private bool IsResizeRight => activeResizeHandle == ResizeHandle.TopRight || activeResizeHandle == ResizeHandle.BottomRight;
        private bool IsResizeTop => activeResizeHandle == ResizeHandle.TopLeft || activeResizeHandle == ResizeHandle.TopRight;
        private bool IsResizeBottom => activeResizeHandle == ResizeHandle.BottomLeft || activeResizeHandle == ResizeHandle.BottomRight;
        private bool IsResizeX => IsResizeLeft || IsResizeRight;
        private bool IsResizeY => IsResizeTop || IsResizeBottom;

    }
}
