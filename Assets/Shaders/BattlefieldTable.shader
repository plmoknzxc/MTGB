Shader "MTGB/Quiet Table"
{
 Properties { _Color("Surface",Color)=(.052,.068,.08,1) _BackdropTex("Stone backdrop",2D)="black"{} }
 SubShader {
  Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Opaque"}
  Pass {
   Tags {"LightMode"="SRPDefaultUnlit"} Cull Off
   HLSLPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   struct A {float4 positionOS:POSITION;float2 uv:TEXCOORD0;};
   struct V {float4 positionCS:SV_POSITION;float2 uv:TEXCOORD0;float3 world:TEXCOORD1;};
   TEXTURE2D(_BackdropTex); SAMPLER(sampler_BackdropTex);
   CBUFFER_START(UnityPerMaterial) float4 _Color; float4 _ViewParams; CBUFFER_END
   V vert(A i){V o;o.positionCS=TransformObjectToHClip(i.positionOS.xyz);o.uv=i.uv;o.world=TransformObjectToWorld(i.positionOS.xyz);return o;}
   half4 frag(V i):SV_Target {
    float2 screenUV=i.positionCS.xy/_ScreenParams.xy;
    // Keep the stone a stable tabletop while cards zoom over it. A small
    // parallax shift adds depth without exposing or repeating the image edge.
    float zoom=1+max(0,_ViewParams.x-1)*.12;
    float2 backdropUV=(screenUV-.5-_ViewParams.yz*.07)/zoom+.5;
    float2 p=i.world.xz;
    float noise=frac(sin(dot(floor(p*320),float2(12.9898,78.233)))*43758.5453);
    float vignette=saturate(1-length(p/float2(17,11))*.6);
    float3 c=_Color.rgb*(.7+vignette*.45)+(noise-.5)*.006;
    if(_ViewParams.w>.5){
     float edge=min(min(screenUV.x,1-screenUV.x),min(screenUV.y,1-screenUV.y));
     float inside=smoothstep(-.08,.08,edge);
     float3 stone=SAMPLE_TEXTURE2D(_BackdropTex,sampler_BackdropTex,saturate(backdropUV)).rgb;
     float3 baseStone=SAMPLE_TEXTURE2D(_BackdropTex,sampler_BackdropTex,screenUV).rgb;
     float3 artwork=lerp(baseStone*.52,stone*.76,inside);
     // The counters sit directly over the artwork beside the two player plaques.
     float edgeLight=smoothstep(.035,.29,min(screenUV.x,1-screenUV.x));
     artwork*=lerp(.52,1.0,edgeLight);
     // Keep highlights in the silver inlay and water below the value of card text.
     float luminance=dot(artwork,float3(.2126,.7152,.0722));
     c=artwork/(1+luminance)+lerp(float3(.005,.009,.012),float3(.008,.014,.018),inside);
    }
    c+=float3(.018,.042,.05)*exp(-dot(p-float2(0,-3),p-float2(0,-3))/22);
    c+=float3(.04,.018,.016)*exp(-dot(p-float2(0,4),p-float2(0,4))/25);
    float center=(1-smoothstep(.008,.022,abs(p.y)))*smoothstep(10,5,abs(p.x));
    c+=center*float3(.04,.05,.054);
    return half4(c,1);
   }
   ENDHLSL
  }
 }
}
