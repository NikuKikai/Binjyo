using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Binjyo
{
    /// <summary>
    /// CanvasSceneItemVisual is the canvas-side view for a SceneItem.
    /// It participates in the shared scene-item notification flow as a render producer.
    /// </summary>
    internal sealed class CanvasItem : ISceneItemView
    {
        private readonly CanvasWindow owner;
        public Guid Id => Item.Id;
        public bool IsRenderer => true;
        public SceneItem Item { get; private set; }
        internal Grid Container { get; }
        private Border Border { get; }
        private Grid ContentRoot { get; }
        private Image Image { get; }
        public ImageEffect Effect { get; private set; }


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

            Effect = new ImageEffect();

            Image.Source = item.Bitmap;
            Image.Effect = Effect;

            NotifiedMove();
            NotifiedTransform();

            item.RegisterView(this);
        }


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
            Effect.IsGray = Item.IsEffectGray ? 1 : 0;
            Effect.IsHuemap = Item.IsEffectHuemap ? 1 : 0;
            Effect.IsBinarize = Item.IsEffectBinarize ? 1 : 0;
            Effect.BinarizeThreshold = Item.PEffectBinarize;
            Effect.IsQuantize = Item.IsEffectQuantize ? 1 : 0;
            Effect.QuantizeLevels = Item.PEffectQuantize;
            Image.Opacity = Item.IsEffectTransparent ? Item.PEffectTransparent / 255.0 : 1;
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
