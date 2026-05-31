using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Binjyo
{
    public partial class MemoD11
    {
        private const double FeaturePointRadius = 2.5;

        public static void RefreshAllStitchVisuals()
        {
            StitchSessionService.RefreshVisuals();
        }

        public void RefreshStitchVisuals()
        {
            InvalidateDrawingOverlay();
            RenderRequest();
        }

        private void ToggleFeaturePoints()
        {
            StitchSessionService.ToggleMode();
        }

        private WriteableBitmap RenderFeatureOverlayBitmap(int pixelWidth, int pixelHeight)
        {
            StitchOverlayData overlayData = StitchSessionService.GetOverlayData(Item);
            IReadOnlyList<FastCornerPoint> featurePoints = overlayData.FeaturePoints;
            if (featurePoints == null || featurePoints.Count == 0)
                return null;

            var matchedIndices = new HashSet<int>(overlayData.MatchedPointIndices ?? Array.Empty<int>());
            if (!overlayData.ShowAllPoints && matchedIndices.Count == 0)
                return null;

            double dpiFactor = Item.DpiFactor <= 0 ? 1.0 : Item.DpiFactor;
            var unmatchedGeometry = new GeometryGroup();
            var matchedGeometry = new GeometryGroup();

            for (int i = 0; i < featurePoints.Count; i++)
            {
                bool isMatched = matchedIndices.Contains(i);
                if (!overlayData.ShowAllPoints && !isMatched)
                    continue;

                FastCornerPoint point = featurePoints[i];
                Point center = new Point(point.X, point.Y);
                var ellipse = new EllipseGeometry(center, FeaturePointRadius * dpiFactor, FeaturePointRadius * dpiFactor);
                if (isMatched)
                    matchedGeometry.Children.Add(ellipse);
                else
                    unmatchedGeometry.Children.Add(ellipse);
            }

            if (matchedGeometry.Children.Count == 0 && unmatchedGeometry.Children.Count == 0)
                return null;

            DrawingVisual visual = new DrawingVisual();
            using (DrawingContext dc = visual.RenderOpen())
            {
                if (unmatchedGeometry.Children.Count > 0)
                    dc.DrawGeometry(new SolidColorBrush(Color.FromArgb(255, 0, 255, 160)), null, unmatchedGeometry);
                if (matchedGeometry.Children.Count > 0)
                    dc.DrawGeometry(new SolidColorBrush(Color.FromArgb(255, 255, 64, 64)), null, matchedGeometry);
            }

            RenderTargetBitmap renderTarget = new RenderTargetBitmap(pixelWidth, pixelHeight, 96, 96, PixelFormats.Pbgra32);
            renderTarget.Render(visual);
            return new WriteableBitmap(renderTarget);
        }
    }
}
