
sampler2D  inputSampler : register(S0);

float hue : register(C0);


float4 main(float2 uv : TEXCOORD) : COLOR
{
    float4 src= tex2D(inputSampler, uv);
    float s = uv[0];
    float v = 1 - uv[1];    // rgbMax
    float rgbMin = v - s*v;

    float r,g,b;
    if (hue >=0 && hue < 60){
        r = v; g = (hue/60) * (v-rgbMin) + rgbMin; b = rgbMin;
    }
    else if (hue < 120){
        r = ((120-hue)/60) * (v-rgbMin) + rgbMin; g = v; b = rgbMin;
    }
    else if (hue < 180){
        r = rgbMin; g = v; b = ((hue-120)/60) * (v-rgbMin) + rgbMin;
    }
    else if (hue < 240){
        r = rgbMin; g = ((240-hue)/60) * (v-rgbMin) + rgbMin; b = v;
    }
    else if (hue < 300){
        r = ((hue-240)/60) * (v-rgbMin) + rgbMin; g = rgbMin; b = v;
    }
    else{
        r = v; g = rgbMin; b = ((360-hue)/60) * (v-rgbMin) + rgbMin;
    }
    
    float4 color = src.a < 0.01 ? float4(0,0,0,0) : float4(r, g, b, 1);
    return color;
}

// fxc SVRectShader.hlsl /T ps_3_0 /Fo Resources\SVRectShader.ps