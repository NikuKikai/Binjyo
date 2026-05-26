using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;


namespace Binjyo
{
    public interface ISceneItemView
    {
        Guid Id { get; }
        bool ProducesRenderedBitmap { get; }
        void NotifiedClose();
        void NotifiedFocus();
        void NotifiedMove();
        void NotifiedEffect();
        void NotifiedRenderedBitmapUpdated();

        // Scene-level notifications
        void NotifiedCanvasActive();
        void NotifiedDisplayMode();
    }

    public class SceneItem
    {
        public List<ISceneItemView> views = new List<ISceneItemView>();

        public Guid Id { get; } = Guid.NewGuid();
        public Bitmap Bitmap { get; internal set; }
        public Bitmap BitmapTransformed { get; internal set; }
        public int OriginalBitmapWidth { get; internal set; }
        public int OriginalBitmapHeight { get; internal set; }
        public double DpiFactor { get; internal set; } = 1;

        public double Scale { get; internal set; } = 1;
        public bool IsEffectGray { get; private set; }
        public bool IsEffectBinarize { get; internal set; }
        public int PEffectBinarize { get; internal set; } = 128;
        public bool IsEffectQuantize { get; internal set; }  // exclusive to IsEffectBinarize
        public int PEffectQuantize { get; internal set; } = 3;
        public bool IsEffectTransparent { get; internal set; }
        public int PEffectTransparent { get; internal set; } = 128;
        public bool IsEffectHuemap { get; internal set; }

        public List<char> GeometryTransformHistory { get; } = new List<char>();
        public DrawingDocumentData DrawingDocument { get; internal set; } = new DrawingDocumentData();
        public Stack<DrawingDocumentData> DrawingUndoStack { get; } = new Stack<DrawingDocumentData>();

        public double Left { get; internal set; }
        public double Top { get; internal set; } // Logical pixels (WPF units)
        public bool HasAnchorPosition { get; internal set; }
        public long FocusOrder { get; internal set; } = 0;
        public bool IsRenderDirty { get; private set; } = true;
        public BitmapSource RenderedBitmapSource { get; private set; }


        public SceneItem(Bitmap bmp, int left, int top)
        {
            Bitmap = bmp;
            BitmapTransformed = (Bitmap)Bitmap.Clone();
            OriginalBitmapWidth = bmp.Width;
            OriginalBitmapHeight = bmp.Height;
            Left = left;
            Top = top;
            HasAnchorPosition = true;
        }

        public void Close()
        {
            foreach (ISceneItemView view in views) view.NotifiedClose();
            views.Clear();

            RenderedBitmapSource = null;

            Bitmap.Dispose();
            Bitmap = null;
            BitmapTransformed.Dispose();
            BitmapTransformed = null;
        }

        public void MoveTo(double left, double top)
        {
            Left = left;
            Top = top;
            views.ForEach(view => view.NotifiedMove());
        }


        public double GetBaseWidth() => BitmapTransformed.Width / DpiFactor;
        public double GetBaseHeight() => BitmapTransformed.Height / DpiFactor;
        public double GetWidth() => GetBaseWidth() * Scale;
        public double GetHeight() => GetBaseHeight() * Scale;

        public Rect GetBounds()
        {
            return new Rect(Left, Top, GetWidth(), GetHeight());
        }

        public void RegisterView(ISceneItemView view)
        {
            if (!views.Contains(view))
                views.Add(view);
        }
        public void UnregisterView(ISceneItemView view)
        {
            if (views.Contains(view))
                views.Remove(view);
        }

        public bool HasRenderProducer()
        {
            return views.Exists(view => view.ProducesRenderedBitmap);
        }

        public void PublishRenderedBitmap(BitmapSource bitmapSource)
        {
            RenderedBitmapSource = bitmapSource;
            IsRenderDirty = false;
            views.ForEach(view => view.NotifiedRenderedBitmapUpdated());
        }

        #region ======== Imaging ========

        private void ApplyEffects(Bitmap bitmapToUpdate)
        {
            if (IsEffectGray)
                Effects.Gray(bitmapToUpdate);

            if (IsEffectBinarize && PEffectBinarize > 0)
                Effects.Binarize(bitmapToUpdate, PEffectBinarize);
            else if (IsEffectQuantize && PEffectQuantize > 2)
                Effects.Quantize(bitmapToUpdate, PEffectQuantize);

            if (IsEffectHuemap)
                Effects.Huemap(bitmapToUpdate);

            if (IsEffectTransparent && PEffectTransparent > 0)
                Effects.Transparent(bitmapToUpdate, PEffectTransparent);
        }

        public BitmapSource RenderBitmapSource()
        {
            Bitmap bitmap = Bitmap.Clone(
                new Rectangle(0, 0, Bitmap.Width, Bitmap.Height),
                System.Drawing.Imaging.PixelFormat.Format32bppArgb);

            ApplyEffects(bitmap);

            // todo: should not map, instead drawing data should be stored in original coordinate space
            // DrawingDocumentData documentToRender = MapDrawingDocumentToOriginal(item);
            // ApplyDrawingToBitmap(bitmap, documentToRender);

            BitmapSource bitmapSource = bitmap.ToBitmapSource(System.Windows.Media.PixelFormats.Bgra32);
            bitmapSource.Freeze();
            return bitmapSource;
        }

        public void SetEffectGray(bool enabled)
        {
            if (IsEffectGray == enabled)
                return;
            IsEffectGray = enabled;
            views.ForEach(view => view.NotifiedEffect());
        }

        public void SetEffectHuemap(bool enabled)
        {
            if (IsEffectHuemap == enabled)
                return;
            IsEffectHuemap = enabled;
            views.ForEach(view => view.NotifiedEffect());
        }
        #endregion
    }
}
