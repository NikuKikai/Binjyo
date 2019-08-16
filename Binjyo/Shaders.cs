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
            PixelShader pixelShader = new PixelShader();
            pixelShader.UriSource = new Uri(@"/Resources/HueWheelShader.ps", UriKind.Relative);
            this.PixelShader = pixelShader;

            this.UpdateShaderValue(InputProperty);
            this.UpdateShaderValue(AngleProperty);
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

        public float Angle
        {
            get
            {
                return ((float)(this.GetValue(AngleProperty)));
            }
            set
            {
                this.SetValue(AngleProperty, value);
            }
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
}