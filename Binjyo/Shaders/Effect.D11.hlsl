Texture2D InputTexture : register(t0);
SamplerState InputSampler : register(s0);

cbuffer Constants : register(b0)
{
    float4 Sizes;  // baseWidth, baseHeight, renderWidth, renderHeight
    float4 RenderAndFlags;    // borderSize, unused
    float4 EffectParamsA;
    float4 EffectParamsB;
    float4 InverseRow0;
    float4 InverseRow1;
};

float4 ApplyGrayscale(float4 color)
{
    float gray = 0.299 * color.r + 0.587 * color.g + 0.114 * color.b;
    return float4(gray, gray, gray, color.a);
}

float4 ApplyBinarize(float4 color, float threshold)
{
    threshold = threshold / 255.0;
    color.rgb = step(threshold, color.rgb);
    return color;
}

float4 ApplyQuantize(float4 color, float levels)
{
    levels = max(levels, 1.0);
    color.r = round(color.r * levels) / levels;
    color.g = round(color.g * levels) / levels;
    color.b = round(color.b * levels) / levels;
    return color;
}

float4 ApplyHuemap(float4 color)
{
    float maxVal = max(color.r, max(color.g, color.b));
    float minVal = min(color.r, min(color.g, color.b));

    if ((maxVal - minVal) < 0.02)
        return float4(0.5, 0.5, 0.5, color.a);

    float h = 0.0;
    if (color.r >= color.b && color.r >= color.g)
        h = 60.0 * (color.g - color.b) / (color.r - min(color.b, color.g));
    else if (color.g > color.r && color.g > color.b)
        h = 60.0 * (color.b - color.r) / (color.g - min(color.b, color.r)) + 120.0;
    else
        h = 60.0 * (color.r - color.g) / (color.b - min(color.g, color.r)) + 240.0;

    if (h < 0.0)
        h += 360.0;

    h = round(h / 10.0) / 36.0;
    float3 rgb = saturate(float3(abs(h * 6.0 - 3.0) - 1.0, 2.0 - abs(h * 6.0 - 2.0), 2.0 - abs(h * 6.0 - 4.0)));
    return float4(rgb, color.a);
}

float4 main(float4 position : SV_POSITION) : SV_Target
{
    float2 outputPoint = position.xy;
    float2 localPoint;
    localPoint.x = outputPoint.x * InverseRow0.x + outputPoint.y * InverseRow0.y + InverseRow0.z;
    localPoint.y = outputPoint.x * InverseRow1.x + outputPoint.y * InverseRow1.y + InverseRow1.z;

    float2 uv = localPoint / Sizes.xy;
    if (uv.x < 0.0 || uv.x > 1.0 || uv.y < 0.0 || uv.y > 1.0)
        return float4(0.0, 0.0, 0.0, 0.0);

    float4 color = InputTexture.Sample(InputSampler, uv);

    if (EffectParamsA.x > 0.5)
        color = ApplyGrayscale(color);
    if (EffectParamsA.y > 0.5)
        color = ApplyBinarize(color, EffectParamsA.z);
    if (EffectParamsA.w > 0.5)
        color = ApplyQuantize(color, EffectParamsB.x);
    if (EffectParamsB.y > 0.5)
        color = ApplyHuemap(color);

    float border = EffectParamsB.z;
    if (RenderAndFlags.x > 0.5 &&
        (outputPoint.x < border || outputPoint.y < border ||
         outputPoint.x >= (Sizes.z - border) || outputPoint.y >= (Sizes.w - border)))
    {
        return float4(0.0, 1.0, 0.0, 1.0);
    }

    return color;
}
