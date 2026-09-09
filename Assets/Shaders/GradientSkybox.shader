// A three-stop vertical gradient used as the scene's skybox, replacing Unity's default
// blue "empty project" sky. Skybox meshes hand the shader object-space positions that
// happen to be direction vectors from the camera, so the vertical component of that
// direction is all we need to place a pixel on the gradient.
Shader "Spatial Math/Gradient Skybox"
{
    Properties
    {
        _TopColor     ("Top Color",     Color) = (0.02, 0.03, 0.07, 1)
        _HorizonColor ("Horizon Color", Color) = (0.07, 0.12, 0.21, 1)
        _BottomColor  ("Bottom Color",  Color) = (0.01, 0.01, 0.02, 1)
        _Falloff      ("Falloff",       Range(0.2, 5.0)) = 1.4
    }

    SubShader
    {
        // Deliberately no "RenderPipeline" tag here. The skybox is drawn by Unity's own
        // skybox pass rather than by URP's renderer loop, so leaving the subshader
        // unrestricted is what keeps it matching.
        Tags
        {
            "Queue"       = "Background"
            "RenderType"  = "Background"
            "PreviewType" = "Skybox"
        }

        Cull Off
        ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 viewDir    : TEXCOORD0;
            };

            // Grouping material properties this way keeps the shader SRP Batcher friendly.
            CBUFFER_START(UnityPerMaterial)
                float4 _TopColor;
                float4 _HorizonColor;
                float4 _BottomColor;
                float  _Falloff;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.viewDir    = IN.positionOS.xyz;   // doubles as the direction we are looking
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                // -1 = straight down, 0 = the horizon, +1 = straight up.
                float height = normalize(IN.viewDir).y;

                // Raising to a power keeps a tight band of colour around the horizon and
                // lets the top and bottom settle into flat colour.
                float blend = saturate(pow(abs(height), _Falloff));

                float3 color = height >= 0.0
                    ? lerp(_HorizonColor.rgb, _TopColor.rgb,    blend)
                    : lerp(_HorizonColor.rgb, _BottomColor.rgb, blend);

                return half4(color, 1.0);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
