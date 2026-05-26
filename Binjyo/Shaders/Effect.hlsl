sampler2D implicitInputSampler : register(S0);

// (0=OFF, 1=ON)
float IsEnabled : register(C0);

float4 main(float2 uv : TEXCOORD) : COLOR
{
    // 元のピクセルの色を取得
    float4 color = tex2D(implicitInputSampler, uv);

    // スイッチがON（0.5より大きい）の場合のみ、白黒化を実行
    if (IsEnabled > 0.5)
    {
        // 輝度公式 (Y = 0.299R + 0.587G + 0.114B)
        float gray = 0.299 * color.r + 0.587 * color.g + 0.114 * color.b;
        color.rgb = float3(gray, gray, gray);
    }

    return color;
}