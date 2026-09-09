// Unlit, alpha-blended, vertex-coloured lines. The backdrop grid stores both its colour
// and its distance fade in the mesh's vertex colours, so this shader only has to pass
// them through — one draw call for the whole grid.
Shader "Spatial Math/Grid Lines"
{
    Properties
    {
        _Tint ("Tint", Color) = (1, 1, 1, 1)
    }

    SubShader
    {
        Tags
        {
            "Queue"           = "Transparent"
            "RenderType"      = "Transparent"
            "IgnoreProjector" = "True"
            "RenderPipeline"  = "UniversalPipeline"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off          // the grid is scenery; it must never occlude the graph
        Cull Off

        Pass
        {
            // Tells URP which of its passes this belongs to. Without a LightMode tag the
            // renderer has to fall back on defaults to decide whether to draw us at all.
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                half4  color      : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4  color      : COLOR;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _Tint;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.color      = IN.color;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                return IN.color * _Tint;
            }
            ENDHLSL
        }
    }

    Fallback Off
}
