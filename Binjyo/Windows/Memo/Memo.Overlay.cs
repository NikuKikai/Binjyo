using System;
using System.Windows;
using System.Windows.Media.Animation;


namespace Binjyo
{
    public partial class Memo
    {
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

    }
}
