using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;


namespace Binjyo
{
    public interface ISceneItemView
    {
        Guid Id { get; }
        bool IsRenderer { get; }
        void NotifiedClose();
        void NotifiedFocus();
        void NotifiedMove();
        void NotifiedTransform();  // scale, flip, rotate
        void NotifiedEffect();
        void NotifiedRendered();

        // Scene-level notifications
        void NotifiedCanvasActive();
        void NotifiedDisplayMode();
    }

    public class SceneItem
    {
        public List<ISceneItemView> views = new List<ISceneItemView>();

        public Guid Id { get; } = Guid.NewGuid();
        public WriteableBitmap Bitmap { get; internal set; }

        // TODO remove
        public Bitmap BitmapTransformed { get; internal set; }
        public double DpiFactor { get; internal set; } = 1;

        public double Left { get; internal set; }
        public double Top { get; internal set; } // Logical pixels (WPF units)
        public double Scale { get; internal set; } = 1;
        public bool IsFlippedHorizontal { get; internal set; } = false;
        public bool IsFlippedVertical { get; internal set; } = false;
        public double Rotation { get; internal set; } = 0; // degrees
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

        public long FocusOrder { get; internal set; } = 0;
        public WriteableBitmap RenderedWBitmap { get; private set; }


        public SceneItem(WriteableBitmap bmp, int left, int top)
        {
            Bitmap = bmp;
            Left = left;
            Top = top;
        }

        #region ======== Informations =======
        public double GetBaseWidth() => Bitmap.Width / DpiFactor;
        public double GetBaseHeight() => Bitmap.Height / DpiFactor;
        public double GetWidth() => GetBaseWidth() * Scale;
        public double GetHeight() => GetBaseHeight() * Scale;
        public Rect GetBounds() => new Rect(Left, Top, GetWidth(), GetHeight());
        public double GetMinScale() => Math.Max(25.0 / GetBaseWidth(), 25.0 / GetBaseHeight());
        public double GetMaxScale() => 10;
        #endregion


        #region ======== Base Operations =======
        public void Close()
        {
            var viewsToNotify = views.ToList();
            views.Clear(); // Clear before notifying to avoid potential conflicts

            foreach (ISceneItemView view in viewsToNotify) view.NotifiedClose();

            Bitmap = null;
            RenderedWBitmap = null;
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

        public void PublishRenderedBitmap(WriteableBitmap wbmp)
        {
            RenderedWBitmap = wbmp;
            views.ForEach(view => view.NotifiedRendered());
        }
        #endregion


        #region ======== Transform =======

        public void SetPos(double left, double top)
        {
            Left = left;
            Top = top;
            views.ForEach(view => view.NotifiedMove());
        }

        public void SetScale(double scale)
        {
            scale = Math.Max(GetMinScale(), Math.Min(GetMaxScale(), scale));

            if (Scale == scale) return;
            Scale = scale;
            views.ForEach(view => view.NotifiedTransform());
        }
        public void SetFlip(bool isFlippedHorizontal, bool isFlippedVertical)
        {
            if (IsFlippedHorizontal == isFlippedHorizontal && IsFlippedVertical == isFlippedVertical)
                return;
            IsFlippedHorizontal = isFlippedHorizontal;
            IsFlippedVertical = isFlippedVertical;
            views.ForEach(view => view.NotifiedTransform());
        }
        public void SetRotation(double rotation)
        {
            rotation = rotation % 360;
            if (Rotation == rotation) return;
            Rotation = rotation;
            views.ForEach(view => view.NotifiedTransform());
        }
        #endregion



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
            return null;
            // Bitmap bitmap = Bitmap.Clone(
            //     new Rectangle(0, 0, Bitmap.Width, Bitmap.Height),
            //     System.Drawing.Imaging.PixelFormat.Format32bppArgb);

            // ApplyEffects(bitmap);

            // // todo: should not map, instead drawing data should be stored in original coordinate space
            // // DrawingDocumentData documentToRender = MapDrawingDocumentToOriginal(item);
            // // ApplyDrawingToBitmap(bitmap, documentToRender);

            // BitmapSource bitmapSource = bitmap.ToBitmapSource(System.Windows.Media.PixelFormats.Bgra32);
            // bitmapSource.Freeze();
            // return bitmapSource;
        }

        public void SetEffectGray(bool enabled)
        {
            if (IsEffectGray == enabled) return;
            IsEffectGray = enabled;
            views.ForEach(view => view.NotifiedEffect());
        }

        public void SetEffectHuemap(bool enabled)
        {
            if (IsEffectHuemap == enabled) return;
            IsEffectHuemap = enabled;
            views.ForEach(view => view.NotifiedEffect());
        }
        #endregion
    }
}
