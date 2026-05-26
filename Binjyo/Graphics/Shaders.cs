using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace Binjyo
{
    public class HueWheelEffect : ShaderEffect
    {
        public static readonly DependencyProperty InputProperty = ShaderEffect.RegisterPixelShaderSamplerProperty(
            "Input",
            typeof(HueWheelEffect),
            0);

        public static readonly DependencyProperty AngleProperty = DependencyProperty.Register(
            "Angle",
            typeof(float),
            typeof(HueWheelEffect),
            new UIPropertyMetadata((float)210, PixelShaderConstantCallback(0)));

        public HueWheelEffect()
        {
            PixelShader pixelShader = new PixelShader
            {
                UriSource = new Uri(@"/Resources/HueWheelShader.ps", UriKind.Relative)
            };
            this.PixelShader = pixelShader;

            this.UpdateShaderValue(InputProperty);
            this.UpdateShaderValue(AngleProperty);
        }
        public Brush Input
        {
            get => (Brush)(this.GetValue(InputProperty));
            set => this.SetValue(InputProperty, value);
        }

        public float Angle
        {
            get => (float)(this.GetValue(AngleProperty));
            set => this.SetValue(AngleProperty, value);
        }
    }

    public class SaturationValueEffect : ShaderEffect
    {
        public static readonly DependencyProperty InputProperty = ShaderEffect.RegisterPixelShaderSamplerProperty(
            "Input",
            typeof(SaturationValueEffect),
            0);

        public static readonly DependencyProperty HueProperty = DependencyProperty.Register(
            "Hue",
            typeof(float),
            typeof(SaturationValueEffect),
            new UIPropertyMetadata((float)0, PixelShaderConstantCallback(0)));

        public SaturationValueEffect()
        {
            PixelShader pixelShader = new PixelShader();
            pixelShader.UriSource = new Uri(@"/Resources/SVRectShader.ps", UriKind.Relative);
            this.PixelShader = pixelShader;

            this.UpdateShaderValue(InputProperty);
            this.UpdateShaderValue(HueProperty);
        }
        public Brush Input
        {
            get
            {
                return ((Brush)(this.GetValue(InputProperty)));
            }
            set
            {
                this.SetValue(InputProperty, value);
            }
        }

        public float Hue
        {
            get
            {
                return ((float)(this.GetValue(HueProperty)));
            }
            set
            {
                this.SetValue(HueProperty, value);
            }
        }
    }

    public class ImageEffect : ShaderEffect
    {
        private static readonly PixelShader _pixelShader = new PixelShader
        {
            UriSource = new Uri(@"/Resources/Effect.ps", UriKind.Relative)
        };

        // S0: First sampler is defaultly source image (Implicit Input)
        public static readonly DependencyProperty InputProperty =
            RegisterPixelShaderSamplerProperty("Input", typeof(ImageEffect), 0);

        // C0
        public static readonly DependencyProperty IsGrayProperty =
            DependencyProperty.Register(
                nameof(IsGray),
                typeof(double), // シェーダーへ送るため float 互換の double 型にする
                typeof(ImageEffect),
                new UIPropertyMetadata(0.0, PixelShaderConstantCallback(0))
            );
        public double IsGray
        {
            get => (double)GetValue(IsGrayProperty);
            set => SetValue(IsGrayProperty, value);
        }

        public ImageEffect()
        {
            this.PixelShader = _pixelShader;

            UpdateShaderValue(InputProperty);
            UpdateShaderValue(IsGrayProperty);
        }

    }
}