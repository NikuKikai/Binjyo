struct VSOut
{
    float4 Position : SV_POSITION;
};

VSOut main(uint id : SV_VertexID)
{
    float2 positions[4] =
    {
        float2(-1.0,  1.0),
        float2( 1.0,  1.0),
        float2(-1.0, -1.0),
        float2( 1.0, -1.0)
    };

    VSOut output;
    output.Position = float4(positions[id], 0.0, 1.0);
    return output;
}
