using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Binjyo
{
    /// <summary>
    /// CanvasSceneItemVisual is the canvas-side view for a SceneItem.
    /// It participates in the shared scene-item notification flow as a render producer.
    /// </summary>
    internal sealed class CanvasItem : ISceneItemView
    {
        private readonly CanvasWindow owner;

        public CanvasItem(CanvasWindow owner, SceneItem item)
        {
            this.owner = owner;
            Item = item;

            Image = new Image
            {
                Stretch = Stretch.Fill,
                SnapsToDevicePixels = true
            };
            RenderOptions.SetBitmapScalingMode(Image, GetConfiguredBitmapScalingMode());

            ContentRoot = new Grid
            {
                Background = Brushes.Transparent
            };
            ContentRoot.Children.Add(Image);

            Border = new Border
            {
                BorderBrush = Brushes.Transparent,
                BorderThickness = new Thickness(2),
                Background = Brushes.Transparent,
                Child = ContentRoot
            };

            Container = new Grid
            {
                Focusable = false,
                Background = Brushes.Transparent,
                Tag = item.Id
            };
            Container.Children.Add(Border);

            Container.MouseLeftButtonDown += Container_MouseLeftButtonDown;

            Image.Source = item.Bitmap;
            NotifiedMove();
            NotifiedTransform();

            item.RegisterView(this);
        }

        public Guid Id => Item.Id;
        public bool IsRenderer => true;
        public SceneItem Item { get; private set; }
        public Grid Container { get; }
        public Border Border { get; }
        public Grid ContentRoot { get; }
        public Image Image { get; }
        public Rect Bounds { get; set; }


        public void NotifiedClose()
        {
            owner.RemoveItem(Id);
        }

        public void NotifiedFocus()
        {
            bool isFocused = Scene.FocusedId == Id;
            Border.BorderBrush = isFocused ? Brushes.Lime : Brushes.Transparent;
            owner.UpdateStatusText();
        }

        public void NotifiedMove()
        {
            Canvas.SetLeft(Container, Item.Left);
            Canvas.SetTop(Container, Item.Top);
        }

        public void NotifiedTransform()
        {
            Border.Width = Item.GetWidth();
            Border.Height = Item.GetHeight();
        }

        public void NotifiedEffect()
        {
            // var bs = Item.RenderBitmapSource();
            // Image.Source = bs;
            // Item.PublishRenderedBitmap(bs);
        }

        public void NotifiedCanvasActive() { } // No need
        public void NotifiedDisplayMode() { } // No need
        public void NotifiedRendered() { } // No need

        private void Container_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // Focus changes must flow through the shared state model,
            // so every view reacts consistently to the same source of truth.
            Scene.Focus(Id);
            e.Handled = true;
        }

        private static BitmapScalingMode GetConfiguredBitmapScalingMode()
        {
            switch ((EBitmapScalingMode)Properties.Settings.Default.BitmapScalingMode)
            {
                case EBitmapScalingMode.NearestNeighbor:
                    return BitmapScalingMode.NearestNeighbor;
                case EBitmapScalingMode.Linear:
                    return BitmapScalingMode.Linear;
                default:
                    return BitmapScalingMode.Fant;
            }
        }
    }
}
