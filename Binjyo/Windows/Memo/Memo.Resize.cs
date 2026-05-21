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
    public partial class Memo
    {
        private bool IsResizeHandle(ResizeHandle handle)
        {
            return handle != ResizeHandle.None;
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

        private void BeginResize(ResizeHandle handle)
        {
            activeResizeHandle = handle;
            isResizing = true;
            isdrag = false;
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

        private void StopResize()
        {
            isResizing = false;
            activeResizeHandle = ResizeHandle.None;
            if (Mouse.Captured == this)
                Mouse.Capture(null);
            HideCenterInfo();
        }

        private void UpdateResizeFromMouse()
        {
            double mouseX = System.Windows.Forms.Control.MousePosition.X;
            double mouseY = System.Windows.Forms.Control.MousePosition.Y;
            double deltaX = (mouseX - dragStartMouseX) / dpiFactor;
            double deltaY = (mouseY - dragStartMouseY) / dpiFactor;
            double rawScale = GetResizeScaleFromMouseDelta(deltaX, deltaY);
            rawScale = ClampScale(rawScale);
            double snappedScale = ApplyResizeSnap(rawScale);
            ApplyScaleToBounds(snappedScale, out double nextLeft, out double nextTop, out double nextWidth, out double nextHeight);
            scale = snappedScale;
            Left = nextLeft;
            Top = nextTop;
            Width = nextWidth;
            Height = nextHeight;
            UpdateResizeInfoOverlay(snappedScale, nextWidth, nextHeight);
        }

        private void UpdateResizeInfoOverlay(double currentScale, double currentWidth, double currentHeight)
        {
            string scaleText = $"{Math.Round(currentScale * 100):0}%";
            string sizeText = $"{Math.Round(currentWidth):0} x {Math.Round(currentHeight):0}";
            ShowCenterInfoPersistent(scaleText, sizeText);
        }

        private double GetResizeScaleFromMouseDelta(double deltaX, double deltaY)
        {
            double handleVectorX = 0;
            double handleVectorY = 0;

            switch (activeResizeHandle)
            {
                case ResizeHandle.TopLeft:
                    handleVectorX = -GetBaseWidth();
                    handleVectorY = -GetBaseHeight();
                    break;
                case ResizeHandle.TopRight:
                    handleVectorX = GetBaseWidth();
                    handleVectorY = -GetBaseHeight();
                    break;
                case ResizeHandle.BottomLeft:
                    handleVectorX = -GetBaseWidth();
                    handleVectorY = GetBaseHeight();
                    break;
                case ResizeHandle.BottomRight:
                    handleVectorX = GetBaseWidth();
                    handleVectorY = GetBaseHeight();
                    break;
            }

            double denominator = handleVectorX * handleVectorX + handleVectorY * handleVectorY;
            if (denominator <= 0.0001)
                return resizeStartScale;

            double projectedScaleDelta = (deltaX * handleVectorX + deltaY * handleVectorY) / denominator;
            return resizeStartScale + projectedScaleDelta;
        }

        private void ApplyScaleToBounds(double targetScale, out double nextLeft, out double nextTop, out double nextWidth, out double nextHeight)
        {
            nextWidth = GetBaseWidth() * targetScale;
            nextHeight = GetBaseHeight() * targetScale;

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

        private double ApplyResizeSnap(double rawScale)
        {
            if (!IsResizeSnapEnabled() || !IsResizeHandle(activeResizeHandle))
                return rawScale;

            ApplyScaleToBounds(rawScale, out double rawLeft, out double rawTop, out double rawWidth, out double rawHeight);
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

            foreach (Window item in Application.Current.Windows)
            {
                if (item == this || item.Title != "Memo" || !item.IsVisible)
                    continue;

                if (MovesLeftOrRightEdge() &&
                    IntervalsOverlapOrTouch(rawTop, rawBottom, item.Top, item.Top + item.Height))
                {
                    TryResizeSnapCandidate(rawScale, item.Left, true, ref bestScale, ref bestDistance);
                    TryResizeSnapCandidate(rawScale, item.Left + item.Width, true, ref bestScale, ref bestDistance);
                }

                if (MovesTopOrBottomEdge() &&
                    IntervalsOverlapOrTouch(rawLeft, rawRight, item.Left, item.Left + item.Width))
                {
                    TryResizeSnapCandidate(rawScale, item.Top, false, ref bestScale, ref bestDistance);
                    TryResizeSnapCandidate(rawScale, item.Top + item.Height, false, ref bestScale, ref bestDistance);
                }
            }

            return bestScale;
        }

        private void TryResizeSnapCandidate(double rawScale, double targetEdge, bool horizontalEdge, ref double bestScale, ref double bestDistance)
        {
            if (!TryGetResizeScaleForTarget(targetEdge, horizontalEdge, out double candidateScale))
                return;

            candidateScale = ClampScale(candidateScale);
            ApplyScaleToBounds(candidateScale, out double candidateLeft, out double candidateTop, out double candidateWidth, out double candidateHeight);

            double rawMovingEdge = GetMovingEdge(rawScale, horizontalEdge);
            double candidateMovingEdge = horizontalEdge
                ? (MovesLeftEdge() ? candidateLeft : candidateLeft + candidateWidth)
                : (MovesTopEdge() ? candidateTop : candidateTop + candidateHeight);

            double distance = Math.Abs(rawMovingEdge - targetEdge);
            if (distance <= SnapDistance && distance < bestDistance && Math.Abs(candidateMovingEdge - targetEdge) < 0.001)
            {
                bestScale = candidateScale;
                bestDistance = distance;
            }
        }

        private double GetMovingEdge(double candidateScale, bool horizontalEdge)
        {
            ApplyScaleToBounds(candidateScale, out double candidateLeft, out double candidateTop, out double candidateWidth, out double candidateHeight);
            if (horizontalEdge)
                return MovesLeftEdge() ? candidateLeft : candidateLeft + candidateWidth;
            return MovesTopEdge() ? candidateTop : candidateTop + candidateHeight;
        }

        private bool TryGetResizeScaleForTarget(double targetEdge, bool horizontalEdge, out double candidateScale)
        {
            candidateScale = scale;
            if (horizontalEdge)
            {
                if (MovesLeftEdge())
                {
                    candidateScale = (resizeStartRight - targetEdge) / GetBaseWidth();
                    return true;
                }
                if (MovesRightEdge())
                {
                    candidateScale = (targetEdge - resizeStartLeft) / GetBaseWidth();
                    return true;
                }
                return false;
            }

            if (MovesTopEdge())
            {
                candidateScale = (resizeStartBottom - targetEdge) / GetBaseHeight();
                return true;
            }
            if (MovesBottomEdge())
            {
                candidateScale = (targetEdge - resizeStartTop) / GetBaseHeight();
                return true;
            }
            return false;
        }

        private bool MovesLeftEdge()
        {
            return activeResizeHandle == ResizeHandle.TopLeft || activeResizeHandle == ResizeHandle.BottomLeft;
        }

        private bool MovesRightEdge()
        {
            return activeResizeHandle == ResizeHandle.TopRight || activeResizeHandle == ResizeHandle.BottomRight;
        }

        private bool MovesTopEdge()
        {
            return activeResizeHandle == ResizeHandle.TopLeft || activeResizeHandle == ResizeHandle.TopRight;
        }

        private bool MovesBottomEdge()
        {
            return activeResizeHandle == ResizeHandle.BottomLeft || activeResizeHandle == ResizeHandle.BottomRight;
        }

        private bool MovesLeftOrRightEdge()
        {
            return MovesLeftEdge() || MovesRightEdge();
        }

        private bool MovesTopOrBottomEdge()
        {
            return MovesTopEdge() || MovesBottomEdge();
        }

    }
}
