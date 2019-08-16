
sampler2D  inputSampler : register(S0);

float angleOffset : register(C0);


float4 main(float2 uv : TEXCOORD) : COLOR
{
    float4 src= tex2D(inputSampler, uv);
    float2 p = uv - float2(0.5, 0.5);
    float angle = atan2(p.y, p.x) *180/3.141596;

    if (angleOffset > 0)
    {
        angle = (angle - angleOffset + 360*2) % 360;
    }
    else
    {
        angle = (-angle - angleOffset + 360*2) % 360;
    }

    float r,g,b;
    if (angle >=0 && angle < 60){
        r = 1; g = (angle/60) * 1; b = 0;
    }
    else if (angle < 120){
        r = ((120-angle)/60) * 1; g = 1; b = 0;
    }
    else if (angle < 180){
        r = 0; g = 1; b = ((angle-120)/60) * 1;
    }
    else if (angle < 240){
        r = 0; g = ((240-angle)/60) * 1; b = 1;
    }
    else if (angle < 300){
        r = ((angle-240)/60)*1; g = 0; b = 1;
    }
    else{
        r = 1; g = 0; b = ((360-angle)/60)*1;
    }
    
    float4 color = src.a < 0.01 ? float4(0,0,0,0) : float4(r, g, b, 1);
    return color;
}

// fxc HueWheelShader.hlsl /T ps_3_0 /Fo Resources\HueWheelShader.ps