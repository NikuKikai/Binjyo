using System;
using System.Collections.Generic;
using System.IO;
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
        void NotifiedTransform(bool moveOnly);
        void NotifiedEffect();
        void NotifiedOpacity();

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
        public double DpiFactor { get; internal set; } = 1;

        public double Left { get; private set; }
        public double Top { get; private set; } // Logical pixels (WPF units)
        public double Right { get; private set; }
        public double Bottom { get; private set; } // Logical pixels (WPF units)
        public double Width => Right - Left;
        public double Height => Bottom - Top;
        public double Scale { get; internal set; } = 1;
        public bool IsFlipX { get; private set; } = false;
        public bool IsFlipY { get; private set; } = false;
        public double Rotation { get; private set; } = 0; // degrees
        public bool IsEffectGray { get; private set; }
        public bool IsEffectBinarize { get; private set; }
        public int PEffectBinarize { get; private set; } = 128;
        public bool IsEffectQuantize { get; private set; }  // exclusive to IsEffectBinarize
        public int PEffectQuantize { get; private set; } = 3;
        public bool IsOpacity { get; private set; }
        public double Opacity { get; private set; } = 0.5;
        public bool IsEffectHuemap { get; private set; }
        public TransformGroup Transform { get; internal set; } = new TransformGroup();
        public TransformGroup TransformInv { get; internal set; } = new TransformGroup();

        public Clipper2Lib.Paths64 Collider { get; internal set; }

        public List<char> GeometryTransformHistory { get; } = new List<char>();
        public DrawingDocumentData DrawingDocument { get; internal set; } = new DrawingDocumentData();
        public Stack<DrawingDocumentData> DrawingUndoStack { get; } = new Stack<DrawingDocumentData>();

        public long FocusOrder { get; internal set; } = 0;
        public WriteableBitmap RenderedWBitmap { get; private set; }


        public SceneItem(WriteableBitmap bmp, double left, double top)
        {
            Bitmap = bmp;
            Left = left;
            Top = top;
            Right = left + GetBaseWidth();
            Bottom = top + GetBaseHeight();
            Collider = new Clipper2Lib.Paths64();
            Collider.Add(Clipper2Lib.Clipper.MakePath(new int[] {
                    0, 0, bmp.PixelWidth, 0, bmp.PixelWidth, bmp.PixelHeight, 0, bmp.PixelHeight
            }));
        }

        #region ======== Informations =======
        public double GetBaseWidth() => Bitmap.Width / DpiFactor;  // logical
        public double GetBaseHeight() => Bitmap.Height / DpiFactor;
        public Point GetCenter() => new Point(Left + Width / 2, Top + Height / 2);
        public Rect GetBounds() => new Rect(Left, Top, Width, Height);

        public double GetMinScale() => Math.Max(25.0 / GetBaseWidth(), 25.0 / GetBaseHeight());
        public double GetMaxScale() => 10;
        /// <summary>
        /// Input x,y is logical and local to Window. Output is physical and local to bitmap.
        /// </summary>
        public Point PtDisplay2Bitmap(double x, double y)
        {
            var pt = TransformInv.Transform(new Point(x, y));
            return new Point(pt.X * DpiFactor, pt.Y * DpiFactor); // physical on bitmap
        }
        /// <summary>
        /// Input x,y is physical on screen
        /// </summary>
        public bool InWindow(int x, int y)
        {
            return GetBounds().Contains(x / DpiFactor, y / DpiFactor);
        }
        public bool InCollider(int x, int y)
        {
            var pt = PtDisplay2Bitmap(x / DpiFactor, y / DpiFactor);

            int containCnt = 0;
            foreach (var path in Collider)
            {
                var hit = Clipper2Lib.Clipper.PointInPolygon(
                    new Clipper2Lib.Point64(pt.X, pt.Y),
                    path
                );
                if (hit != Clipper2Lib.PointInPolygonResult.IsOutside)
                {
                    var positive = Clipper2Lib.Clipper.IsPositive(path);
                    containCnt += positive? 1: -1;
                }
            }
            return containCnt > 0;
        }


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

        #endregion


        #region ======== Transform =======
        private Rect UpdateTransform()
        {
            var w = GetBaseWidth();
            var h = GetBaseHeight();
            Transform.Children.Clear();
            Transform.Children.Add(new ScaleTransform(
                IsFlipX ? -1 : 1, IsFlipY ? -1 : 1,
                w / 2, h / 2));
            Transform.Children.Add(new ScaleTransform(Scale, Scale));
            Transform.Children.Add(new RotateTransform(Rotation, 0, 0));
            var rect = Transform.TransformBounds(new Rect(0, 0, w, h));
            Transform.Children.Add(new TranslateTransform(-rect.X, -rect.Y));

            TransformInv.Children.Clear();
            TransformInv.Children.Add(new TranslateTransform(rect.X, rect.Y));
            TransformInv.Children.Add(new RotateTransform(-Rotation, 0, 0));
            TransformInv.Children.Add(new ScaleTransform(1.0 / Scale, 1.0 / Scale));
            TransformInv.Children.Add(new ScaleTransform(
                IsFlipX ? -1.0 : 1.0, IsFlipY ? -1.0 : 1.0,
                w / 2, h / 2));
            return rect;
        }

        public void SetPos(double left, double top)
        {
            Right += left - Left;
            Bottom += top - Top;
            Left = left;
            Top = top;
            views.ForEach(view => view.NotifiedTransform(true));
        }

        public void SetScale(double scale)
        {
            scale = Math.Max(GetMinScale(), Math.Min(GetMaxScale(), scale));

            if (Scale == scale) return;
            Right = Left + (Right - Left) / Scale * scale;
            Bottom = Top + (Bottom - Top) / Scale * scale;
            Scale = scale;
            UpdateTransform();
            views.ForEach(view => view.NotifiedTransform(false));
        }
        public void SetFlip(bool isFlippedHorizontal, bool isFlippedVertical)
        {
            if (IsFlipX == isFlippedHorizontal && IsFlipY == isFlippedVertical)
                return;
            IsFlipX = isFlippedHorizontal;
            IsFlipY = isFlippedVertical;
            UpdateTransform();
            views.ForEach(view => view.NotifiedTransform(false));
        }
        public void ResetTransform()
        {
            IsFlipX = false;
            IsFlipY = false;
            Rotation = 0;
            Scale = 1;
            Right = Left + GetBaseWidth();
            Bottom = Top + GetBaseHeight();
            UpdateTransform();
            views.ForEach(view => view.NotifiedTransform(false));
        }
        public void SetRotationCentered(double rotation)
        {
            rotation %= 360;
            if (Rotation == rotation) return;
            Rotation = rotation;
            var rectTgt = UpdateTransform();

            // var trTgt = new TransformGroup();
            // trTgt.Children.Add(new ScaleTransform(Scale, Scale));
            // trTgt.Children.Add(new RotateTransform(rotation));
            // var rectTgt = trTgt.TransformBounds(new Rect(0, 0, GetBaseWidth(), GetBaseHeight()));

            var center = GetCenter();
            Left = center.X - rectTgt.Width / 2;
            Top = center.Y - rectTgt.Height / 2;
            Right = Left + rectTgt.Width;
            Bottom = Top + rectTgt.Height;
            views.ForEach(view => view.NotifiedTransform(false));
        }
        #endregion


        #region ======== Imaging ========

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

        public void SetOpacity(bool enabled, double? p = null)
        {
            if (IsOpacity == enabled && Opacity == p) return;
            IsOpacity = enabled;
            if (p.HasValue) Opacity = Math.Max(Math.Min(p.Value, 0.9), 0.1);
            views.ForEach(view => view.NotifiedOpacity());
        }
        public void SetEffectBinarize(bool enabled, int? p = null)
        {
            if (IsEffectBinarize == enabled && PEffectBinarize == p) return;
            IsEffectBinarize = enabled;
            if (p.HasValue) PEffectBinarize = Math.Max(Math.Min(p.Value, 245), 10);
            views.ForEach(view => view.NotifiedEffect());
        }
        public void SetEffectQuantize(bool enabled, int? p = null)
        {
            if (IsEffectQuantize == enabled && PEffectQuantize == p) return;
            IsEffectQuantize = enabled;
            if (p.HasValue) PEffectQuantize = Math.Max(Math.Min(p.Value, 16), 3);
            views.ForEach(view => view.NotifiedEffect());
        }
        #endregion


        private void RenderSaveTo(Stream stream, bool applyEdit = true)
        {
            var wbmp = applyEdit ? Scene.RenderOffscreen(this) : Bitmap;
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(wbmp));
            using (var s = stream)
                encoder.Save(stream);
        }

        internal void Save(bool applyEdit = true, bool dialog = true)
        {
            if (dialog)
            {
                Microsoft.Win32.SaveFileDialog dlg = new Microsoft.Win32.SaveFileDialog();
                string name = DateTime.Now.ToString("yyyy-MM-dd-hh-mm-ss");
                dlg.FileName = name;
                dlg.Filter = "Png Image|*.png"; //|Bitmap Image|*.bmp|Gif Image|*.gif";

                if (dlg.ShowDialog() == true)
                    RenderSaveTo(dlg.OpenFile(), applyEdit);
            }
            else
            {
                var desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                string name = DateTime.Now.ToString("yyyy-MM-dd-hh-mm-ss");
                string filePath = System.IO.Path.Combine(desktopPath, $"{name}.png");
                RenderSaveTo(File.Create(filePath), applyEdit);
            }
        }
        internal void CopyToClipboard(bool applyEdit)
        {
            var wbmp = applyEdit ? Scene.RenderOffscreen(this) : Bitmap;
            wbmp.Freeze();
            Clipboard.SetImage(wbmp);
        }
    }
}
