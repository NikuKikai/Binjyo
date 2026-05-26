using System;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;


namespace Binjyo
{
    public partial class Memo
    {

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


        #region ======== Center Info ========

        private void ShowCenterInfoPersistent(string title, string detail)
        {
            if (resizeInfoOverlay == null)
                return;

            SetCenterInfoText(title, detail);
            resizeInfoOverlay.BeginAnimation(OpacityProperty, null);
            resizeInfoOverlay.Visibility = Visibility.Visible;
            resizeInfoOverlay.Opacity = 1;
        }

        private void ShowCenterInfoFading(string title, string detail)
        {
            if (resizeInfoOverlay == null)
                return;

            SetCenterInfoText(title, detail);
            resizeInfoOverlay.BeginAnimation(OpacityProperty, null);
            resizeInfoOverlay.Visibility = Visibility.Visible;
            resizeInfoOverlay.Opacity = 1;

            if (centerInfoFadeCompletedHandler != null)
                resizeInfoOverlay.BeginAnimation(OpacityProperty, null);

            var animation = new DoubleAnimationUsingKeyFrames();
            animation.KeyFrames.Add(new DiscreteDoubleKeyFrame(1, KeyTime.FromTimeSpan(TimeSpan.Zero)));
            animation.KeyFrames.Add(new DiscreteDoubleKeyFrame(1, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(550))));
            animation.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(900))));
            centerInfoFadeCompletedHandler = (s, e) =>
            {
                resizeInfoOverlay.Visibility = Visibility.Collapsed;
                resizeInfoOverlay.Opacity = 1;
                centerInfoFadeCompletedHandler = null;
            };
            animation.Completed += centerInfoFadeCompletedHandler;
            resizeInfoOverlay.BeginAnimation(OpacityProperty, animation);
        }

        private void SetCenterInfoText(string title, string detail)
        {
            resizeScaleText.Text = title;
            resizeScaleTextStrokeLeft.Text = title;
            resizeScaleTextStrokeRight.Text = title;
            resizeScaleTextStrokeTop.Text = title;
            resizeScaleTextStrokeBottom.Text = title;
            resizeSizeText.Text = detail;
            resizeSizeTextStrokeLeft.Text = detail;
            resizeSizeTextStrokeRight.Text = detail;
            resizeSizeTextStrokeTop.Text = detail;
            resizeSizeTextStrokeBottom.Text = detail;
        }

        private void HideCenterInfo()
        {
            if (resizeInfoOverlay == null)
                return;

            resizeInfoOverlay.BeginAnimation(OpacityProperty, null);
            resizeInfoOverlay.Visibility = Visibility.Collapsed;
            resizeInfoOverlay.Opacity = 1;
            centerInfoFadeCompletedHandler = null;
        }

        private static string ThrToPercentInfo(bool enabled, int threshold)
        {
            var perc = (int)Math.Round(threshold * 100.0 / 255.0 / 10.0) * 10;
            return enabled ? $"{perc}%" : "Off";
        }

        #endregion


        #region ======== HSVWheel ========

        private static bool isHSVWheel = false;

        private void UpdateHSVWheel()
        {
            var wbmp = (WriteableBitmap)image.Source;
            if (wbmp == null) return;

            // pos on UI display coords
            var x = System.Windows.Forms.Control.MousePosition.X / dpiFactor - Left;
            var y = System.Windows.Forms.Control.MousePosition.Y / dpiFactor - Top;

            if (x < 0 || x >= Width || y < 0 || y >= Height)
                return;

            var ptOnBmp = MapPosUI2Bmp(x, y);
            var px = wbmp.GetPixel((int)ptOnBmp.X, (int)ptOnBmp.Y);

            popup.IsOpen = true;
            popup.HorizontalOffset = x + 200;
            popup.VerticalOffset = y + 10;

            //  Update Hue marker
            float hue = px.GetH();
            HSV_SV.Hue = hue;
            var radius = HSVWheel.Width / 2 - HSVWheel.StrokeThickness / 2;
            var angle = (hue + 210) / 180 * Math.PI;
            var xc = HSVWheel.Width / 2 + Math.Cos(angle) * radius;
            var yc = HSVWheel.Height / 2 + Math.Sin(angle) * radius;
            HueMark.Margin = new Thickness(xc - HueMark.Width / 2, yc - HueMark.Height / 2, 0, 0);

            //  Update SV marker
            var v = (double)Math.Max(Math.Max(px.R, px.G), px.B) / 255;
            var s = (double)Math.Min(Math.Min(px.R, px.G), px.B) / 255;
            if (v == 0) s = 1;
            else s = (v - s) / v; // S of HSV is different from px.GetSaturation(), which is S of HSL(?)

            if (v < 0.5) SVMark.Stroke = new SolidColorBrush(Colors.White);
            else SVMark.Stroke = new SolidColorBrush(Colors.Black);

            SVMark.Margin = new Thickness(
                HSVWheel.Width / 2 - HSVRect.Width / 2 + s * HSVRect.Width - SVMark.Width / 2,
                HSVWheel.Height / 2 + HSVRect.Height / 2 - v * HSVRect.Height - SVMark.Height / 2, 0, 0);

            // Show text
            HSVText.Text = String.Format("H{0: 000}°   S{1: 000}    L{2: 000}", (int)px.GetH(), (int)(px.GetS() * 100), (int)(px.GetV() * 100));
            RGBText.Text = String.Format("R{0: 000}    G{1: 000}    B{2: 000}", px.R, px.G, px.B);
            CoordText.Text = String.Format("X{0: 0000}    Y{1: 0000}", ptOnBmp.X, ptOnBmp.Y);
        }

        private void HideHSVWheel()
        {
            popup.IsOpen = false;
        }

        private bool ShouldShowHSVWheel()
        {
            return isHSVWheel && !isEditMode && !isdrag && !isResizing && Scene.DisplayMode != EDisplayMode.Minimized;
        }

        private void RefreshHSVWheelVisibility()
        {
            if (isHSVWheel && !isEditMode && !isdrag && !isResizing && Scene.DisplayMode == EDisplayMode.Expanded)
                UpdateHSVWheel();
            else
                HideHSVWheel();
        }

        private static void RefreshAllMemoHSVWheelVisibility()
        {
            foreach (Memo memo in Application.Current.Windows.OfType<Window>().OfType<Memo>())
            {
                memo.RefreshHSVWheelVisibility();
            }
        }

        private Point MapPosUI2Bmp(double x, double y)
        {
            var w = Width;
            var h = Height;

            // Render(Bmp2UI) Order: scale -> flip (-> rotate)
            // Reverse flip
            if (Item.IsFlippedHorizontal)
                x = w - 1 - x;
            if (Item.IsFlippedVertical)
                y = h - 1 - y;
            // Reverse scale
            x /= Item.Scale;
            y /= Item.Scale;

            return new Point(x, y);
        }
        #endregion

    }
}
