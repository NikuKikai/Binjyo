sampler2D implicitInputSampler : register(S0);

// (0=OFF, 1=ON)
float IsGray : register(C0);
float IsBinarize : register(C1);
float BinarizeThreshold : register(C2);
float IsQuantize : register(C3);
float QuantizeLevels : register(C4);
float IsHuemap : register(C5);

// 1. 白黒化 (Grayscale)
float4 ApplyGrayscale(float4 color)
{
    // 輝度公式 (Y = 0.299R + 0.587G + 0.114B)
    float gray = 0.299 * color.r + 0.587 * color.g + 0.114 * color.b;
    return float4(gray, gray, gray, color.a);
}

// 2. 二値化 (Binarize)
float4 ApplyBinarize(float4 color, float threshold)
{
    threshold = threshold / 255.0;
    float4 res;
    res.rgb = step(threshold, color.rgb);
    res.a = color.a;
    return res;
}

// 3. 量化 (Quantize)
float4 ApplyQuantize(float4 color, float levels)
{
    float4 res;
    res.r = round(color.r * levels) / levels;
    res.g = round(color.g * levels) / levels;
    res.b = round(color.b * levels) / levels;
    res.a = color.a;
    return res;
}

// 4. 色相マップ (Huemap)
float4 ApplyHuemap(float4 color)
{
    float maxVal = max(color.r, max(color.g, color.b));
    float minVal = min(color.r, min(color.g, color.b));

    if ((maxVal - minVal) < 0.02)
    {
        return float4(0.5, 0.5, 0.5, color.a);
    }
    else
    {
        float h = 0.0;
        if (color.r >= color.b && color.r >= color.g)
        {
            h = 60.0 * (color.g - color.b) / (color.r - min(color.b, color.g));
        }
        else if (color.g > color.r && color.g > color.b)
        {
            h = 60.0 * (color.b - color.r) / (color.g - min(color.b, color.r)) + 120.0;
        }
        else
        {
            h = 60.0 * (color.r - color.g) / (color.b - min(color.g, color.r)) + 240.0;
        }

        if (h < 0.0)
        {
            h += 360.0;
        }

        h = round(h / 10.0) / 36.0; // * 10.0 / 360.0;

        float3 rgb = saturate(float3(abs(h * 6.0 - 3.0) - 1.0, 2.0 - abs(h * 6.0 - 2.0), 2.0 - abs(h * 6.0 - 4.0)));
        return float4(rgb, color.a);
    }
}

float4 main(float2 uv : TEXCOORD) : COLOR
{
    float4 color = tex2D(implicitInputSampler, uv);

    if (IsGray > 0.5)
    {
        color = ApplyGrayscale(color);
    }

    if (IsBinarize > 0.5)
    {
        color = ApplyBinarize(color, BinarizeThreshold);
    }

    if (IsQuantize > 0.5)
    {
        color = ApplyQuantize(color, QuantizeLevels);
    }

    if (IsHuemap > 0.5)
    {
        color = ApplyHuemap(color);
    }

    return color;
}

