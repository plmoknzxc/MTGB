Shader "MTGB/Arena Card Highlight"
{
    Properties
    {
        _Color ("Inner Color", Color) = (0.05, 0.75, 1.0, 0.28)
        _EdgeColor ("Edge Color", Color) = (0.15, 0.95, 1.0, 0.86)
        _Width ("Edge Width", Range(0.02, 0.35)) = 0.16
        _Intensity ("Intensity", Range(0.2, 3.0)) = 1.25
        _PulseSpeed ("Pulse Speed", Range(0.0, 8.0)) = 2.2
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
        }

        Pass
        {
            Name "ArenaCardHighlight"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha One
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half4 _EdgeColor;
                half _Width;
                half _Intensity;
                half _PulseSpeed;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half2 uv = saturate(input.uv);
                half2 edgeDistance = min(uv, 1.0h - uv);
                half edge = min(edgeDistance.x, edgeDistance.y);
                half rim = 1.0h - smoothstep(0.0h, _Width, edge);
                half softFill = 1.0h - smoothstep(0.14h, 0.50h, distance(uv, half2(0.5h, 0.5h)));
                half pulse = 0.78h + 0.22h * sin(_Time.y * _PulseSpeed);

                half alpha = saturate((rim * _EdgeColor.a + softFill * _Color.a * 0.35h) * pulse);
                half3 color = lerp(_Color.rgb, _EdgeColor.rgb, rim) * _Intensity;
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
