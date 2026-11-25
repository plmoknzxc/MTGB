Shader "UI/MTGB/SanctumMoonlight"
{
    Properties { [PerRendererData] _MainTex ("Artwork", 2D) = "white" {} }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Cull Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;

            struct Varyings
            {
                float4 position : SV_POSITION;
                float2 uv : TEXCOORD0;
                fixed4 tint : COLOR;
            };

            Varyings vert(appdata_full input)
            {
                Varyings output;
                output.position = UnityObjectToClipPos(input.vertex);
                output.uv = input.texcoord.xy;
                output.tint = input.color;
                return output;
            }

            fixed4 frag(Varyings input) : SV_Target
            {
                half4 art = tex2D(_MainTex,input.uv);
                half brightness = dot(art.rgb,half3(0.2126,0.7152,0.0722));
                half warmth = (art.r - art.b) / max(max(art.r,art.b),0.02h);
                half3 silverLight = brightness * half3(0.79,1.02,1.19);
                art.rgb = lerp(art.rgb,silverLight,0.16 + 0.84 * smoothstep(0.02,0.45,warmth));
                return art * input.tint;
            }
            ENDCG
        }
    }
}
