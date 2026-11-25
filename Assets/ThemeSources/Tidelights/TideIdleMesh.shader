Shader "MTGB/UI/Tidelights Idle"
{
    Properties { [PerRendererData] _MainTex ("Portrait", 2D) = "white" {} }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Cull Off ZWrite Off ZTest [unity_GUIZTestMode]
        Blend One OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct Input
            {
                float4 pos : POSITION;
                fixed4 color : COLOR;
                float2 uv : TEXCOORD0;
                float4 breathGrip : TEXCOORD1;
                float4 headWind : TEXCOORD2;
                float4 tipsLid : TEXCOORD3;
            };
            struct Varyings { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; fixed4 color : COLOR; };
            sampler2D _MainTex;
            float4 _Body, _Hair;
            float2 _PixelScale;

            Varyings vert(Input v)
            {
                float2 motion = v.breathGrip.xy * _Body.x + v.breathGrip.zw * _Body.y
                              + v.headWind.xy * _Body.z + v.headWind.zw * _Body.w + v.tipsLid.xy * _Hair.x;
                v.pos.xy += motion * _PixelScale;
                Varyings o;
                o.pos = UnityObjectToClipPos(v.pos);
                o.uv = v.uv;
                o.color = v.color;
                if (v.tipsLid.z > .5 && abs(v.tipsLid.z - _Hair.y) > .5) o.color.a = 0;
                return o;
            }
            fixed4 frag(Varyings i) : SV_Target
            {
                fixed4 c = tex2D(_MainTex, i.uv) * i.color;
                c.rgb *= c.a;
                return c;
            }
            ENDCG
        }
    }
}
