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
        void NotifiedMove();
        void NotifiedTransform();  // scale, flip, rotate
        void NotifiedEffect();

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

        public double Left { get; internal set; }
        public double Top { get; internal set; } // Logical pixels (WPF units)
        public double Scale { get; internal set; } = 1;
        public bool IsFlipX { get; private set; } = false;
        public bool IsFlipY { get; private set; } = false;
        public double Rotation { get; private set; } = 0; // degrees
        public bool IsEffectGray { get; private set; }
        public bool IsEffectBinarize { get; private set; }
        public int PEffectBinarize { get; private set; } = 128;
        public bool IsEffectQuantize { get; private set; }  // exclusive to IsEffectBinarize
        public int PEffectQuantize { get; private set; } = 3;
        public bool IsEffectTransparent { get; private set; }
        public int PEffectTransparent { get; private set; } = 128;
        public bool IsEffectHuemap { get; private set; }

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
        }
        public static SceneItem FromScreenPos(double screenX, double screenY, double dpiFactor)
        {
            var x = screenX / dpiFactor;
            var y = screenY / dpiFactor;
            return new SceneItem(null, x, y);
        }

        #region ======== Informations =======
        public double GetBaseWidth() => Bitmap.Width / DpiFactor;  // logical
        public double GetBaseHeight() => Bitmap.Height / DpiFactor;
        public Size GetBaseSize() => new Size(GetBaseWidth(), GetBaseHeight());

        public double GetDisplayWidth() => GetDisplaySize().Width;  // logical
        public double GetDisplayHeight() => GetDisplaySize().Height;
        public Size GetDisplaySize()
        {
            var w = GetBaseWidth();
            var h = GetBaseHeight();
            // rotation
            var tr = new TransformGroup();
            tr.Children.Add(new ScaleTransform(Scale, Scale));
            tr.Children.Add(new RotateTransform(Rotation));
            var rect = tr.TransformBounds(new Rect(0, 0, w, h));
            return new Size(rect.Width, rect.Height);
        }

        public Rect GetBounds() // logical
        {
            var size = GetDisplaySize();
            return new Rect(Left, Top, size.Width, size.Height);
        }
        public double GetMinScale() => Math.Max(25.0 / GetBaseWidth(), 25.0 / GetBaseHeight());
        public double GetMaxScale() => 10;
        /// <summary>
        /// Input x,y is physical
        /// </summary>
        public bool ContainsPt(double x, double y)
        {
            return GetBounds().Contains(x / DpiFactor, y / DpiFactor);
        }
        /// <summary>
        /// logical
        /// </summary>
        public Point GetCenter()
        {
            var size = GetDisplaySize();
            return new Point(Left + size.Width / 2, Top + size.Height / 2);
        }
        /// <summary>
        /// Input x,y is logical and local to Window. Output is physical and local to bitmap.
        /// </summary>
        public Point PtDisplay2Bitmap(double x, double y)
        {
            var w = GetBaseWidth();
            var h = GetBaseHeight();

            var tr = new TransformGroup();
            tr.Children.Add(new ScaleTransform(Scale, Scale));
            tr.Children.Add(new RotateTransform(Rotation));
            var rect = tr.TransformBounds(new Rect(0, 0, w, h));

            x += rect.X;
            y += rect.Y;

            var trInv = new TransformGroup();
            trInv.Children.Add(new RotateTransform(-Rotation));
            trInv.Children.Add(new ScaleTransform(1.0 / Scale, 1.0 / Scale));
            var pt = trInv.Transform(new Point(x, y)); // logical on non-transformed UI

            pt.X = IsFlipX ? w - pt.X : pt.X;
            pt.Y = IsFlipY ? h - pt.Y : pt.Y;

            return new Point(Math.Round(pt.X * DpiFactor), Math.Round(pt.Y * DpiFactor)); // physical on bitmap
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
            if (IsFlipX == isFlippedHorizontal && IsFlipY == isFlippedVertical)
                return;
            IsFlipX = isFlippedHorizontal;
            IsFlipY = isFlippedVertical;
            views.ForEach(view => view.NotifiedTransform());
        }
        public void SetRotation(double rotation)
        {
            rotation %= 360;
            if (Rotation == rotation) return;
            Rotation = rotation;
            views.ForEach(view => view.NotifiedTransform());
        }
        public void RotateAroundCenter(double deg)
        {
            if (deg % 360 == 0) return;
            var rot = (Rotation + deg) % 360;
            var w = GetBaseWidth();
            var h = GetBaseHeight();

            var tr = new TransformGroup();
            tr.Children.Add(new ScaleTransform(Scale, Scale));
            tr.Children.Add(new RotateTransform(Rotation));
            var rect = tr.TransformBounds(new Rect(0, 0, w, h));

            var centerX = Left + rect.Width / 2;
            var centerY = Top + rect.Height / 2;

            var trTgt = new TransformGroup();
            trTgt.Children.Add(new ScaleTransform(Scale, Scale));
            trTgt.Children.Add(new RotateTransform(rot));
            var rectTgt = trTgt.TransformBounds(new Rect(0, 0, w, h));

            Left = centerX - rectTgt.Width / 2;
            Top = centerY - rectTgt.Height / 2;
            Rotation = rot;
            views.ForEach(view => view.NotifiedTransform());
        }
        public void SetRotationAroundCenter(double rotation)
        {
            rotation %= 360;
            if (Rotation == rotation) return;

            var w = GetBaseWidth();
            var h = GetBaseHeight();

            var tr = new TransformGroup();
            tr.Children.Add(new ScaleTransform(Scale, Scale));
            tr.Children.Add(new RotateTransform(Rotation));
            var rect = tr.TransformBounds(new Rect(0, 0, w, h));

            var centerX = Left + rect.Width / 2;
            var centerY = Top + rect.Height / 2;

            var trTgt = new TransformGroup();
            trTgt.Children.Add(new ScaleTransform(Scale, Scale));
            trTgt.Children.Add(new RotateTransform(rotation));
            var rectTgt = trTgt.TransformBounds(new Rect(0, 0, w, h));

            Left = centerX - rectTgt.Width / 2;
            Top = centerY - rectTgt.Height / 2;
            Rotation = rotation;
            views.ForEach(view => view.NotifiedTransform());
        }
        public void SetRotationAroundScrCenter(double rotation, double centerX, double centerY)
        {
            rotation %= 360;
            if (Rotation == rotation) return;

            var w = GetBaseWidth();
            var h = GetBaseHeight();

            // var tr = new TransformGroup();
            // tr.Children.Add(new ScaleTransform(Scale, Scale));
            // tr.Children.Add(new RotateTransform(Rotation));
            // var rect = tr.TransformBounds(new Rect(0, 0, w, h));

            // var centerX = Left + rect.Width / 2;
            // var centerY = Top + rect.Height / 2;
            centerX /= DpiFactor;
            centerY /= DpiFactor;

            var trTgt = new TransformGroup();
            trTgt.Children.Add(new ScaleTransform(Scale, Scale));
            trTgt.Children.Add(new RotateTransform(rotation));
            var rectTgt = trTgt.TransformBounds(new Rect(0, 0, w, h));

            Left = centerX - rectTgt.Width / 2;
            Top = centerY - rectTgt.Height / 2;
            Rotation = rotation;
            views.ForEach(view => view.NotifiedTransform());
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

        public void SetEffectTransparent(bool enabled, int? p = null)
        {
            if (IsEffectTransparent == enabled && PEffectTransparent == p) return;
            IsEffectTransparent = enabled;
            if (p.HasValue) PEffectTransparent = Math.Max(Math.Min(p.Value, 245), 10);
            views.ForEach(view => view.NotifiedEffect());
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
