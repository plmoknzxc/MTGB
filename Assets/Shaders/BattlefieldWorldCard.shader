Shader "MTGB/World Card"
{
 Properties {
  _MainTex("Face",2D)="white"{} _Color("Tint",Color)=(1,1,1,1)
  _UVRect("Sprite UV",Vector)=(0,0,1,1) _Shadow("Shadow",Float)=0
  _Radius("Corner radius",Float)=0.025
  _CutCorner("Cut corner",Float)=0
  _Bevel("Frame bevel",Float)=0
  _TopFade("Top vignette",Float)=0
  _BottomFade("Bottom vignette",Float)=0
  _MipBias("Face mip adjustment",Float)=0
  _ImageSharpness("Local art sharpness",Range(0,1))=0
  _SourceTexelSize("Local art texel size",Vector)=(0,0,0,0)
  _Brightness("Card brightness",Float)=1
  _UseVertexColor("Vector glyph colors",Float)=0
  _Contact("Compact contact shadow",Float)=0
  _FrameTint("Colored metal frame",Float)=0
  _FrameFinish("Dark engraved frame",Float)=0
  _FrameWidth("Metal inner edge",Float)=0
  _FrameAspect("Metal aspect",Float)=1
  _FrameColorA("Frame left color",Color)=(1,1,1,1)
  _FrameColorB("Frame right color",Color)=(1,1,1,1)
  _PaperSize("Printed frame size",Vector)=(1.04,1.12,0,0)
  _PaperHeader("Extended card header",Float)=0
  _FooterTrim("Inset footer finish",Float)=0
  _NameTex("Header paper stock",2D)="white"{}
  _NameUVRect("Header paper source",Vector)=(0,0,1,1)
  _ActionHint("Action hint shape",Float)=0
  _ActionBounds("Action surface and outline size",Vector)=(1.2,1.6,1.025,1.425)
  _ActionCorners("Action radius and cut corner",Vector)=(.025,0,0,0)
  _StatBadge("Loyalty or defense badge",Float)=0
  _StatBadgeSize("Badge local size",Vector)=(.28,.21,0,0)
  _WoundMark("Toughness claw mark",Float)=0
  _DeathDissolve("Death dissolve",Range(0,1))=0
  _ClipRect("Viewport",Vector)=(0,0,1,1)
 }
 SubShader {
  Tags {"RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent"}
  Pass {
   Tags {"LightMode"="SRPDefaultUnlit"}
   Blend SrcAlpha OneMinusSrcAlpha
   ZWrite On Cull Off
   HLSLPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   struct A {float4 positionOS:POSITION;float2 uv:TEXCOORD0;float4 color:COLOR;};
   struct V {float4 positionCS:SV_POSITION;float2 uv:TEXCOORD0;float4 color:COLOR;};
   TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
   TEXTURE2D(_NameTex); SAMPLER(sampler_NameTex);
   CBUFFER_START(UnityPerMaterial)
    float4 _Color,_UVRect,_ClipRect,_FrameColorA,_FrameColorB,_PaperSize,_NameUVRect,_ActionBounds,_ActionCorners,_StatBadgeSize,_SourceTexelSize;float _Shadow,_Radius,_CutCorner,_Bevel,_TopFade,_BottomFade,_MipBias,_ImageSharpness,_Brightness,_Contact,_FrameTint,_FrameWidth,_FrameAspect,_ActionHint,_StatBadge,_PaperHeader,_FooterTrim;
    float _UseVertexColor,_FrameFinish;
    float _WoundMark;
    float _DeathDissolve;
   CBUFFER_END
   V vert(A i){V o;o.positionCS=TransformObjectToHClip(i.positionOS.xyz);o.uv=i.uv;o.color=i.color;return o;}
   float FrameUV(float value,float inner){
    // Preserve the transparent exterior and outer lip. Widen only the metal
    // between the lip and opening, without scaling the card's silhouette.
    float edge=min(value,1-value);
    float mapped=edge<.027?edge:edge<inner?lerp(.027,.056,(edge-.027)/(inner-.027)):lerp(.056,.5,(edge-inner)/(.5-inner));
    return value<.5?mapped:1-mapped;
   }
   half4 ActionGlow(float2 uv){
    float2 size=max(_ActionBounds.zw,float2(.01,.01));
    float2 p=(uv-.5)*_ActionBounds.xy/size;
    float2 q=abs(p)-(.5-_ActionCorners.x);
    float2 outer=max(q,0);
    float outerLength=length(outer);
    float d=outerLength+min(max(q.x,q.y),0)-_ActionCorners.x;
    // Convert the existing rounded-card contour to local card units, so the
    // line and halo stay equally fine on short lands and tall planeswalkers.
    float2 normal=outerLength>.00001?outer/outerLength:(q.x>q.y?float2(1,0):float2(0,1));
    d/=max(length(normal/size),.00001);
    if(_ActionCorners.y>0){
     float cut=(abs(p.x)+abs(p.y)-(1-_ActionCorners.y))/length(1/size);
     d=max(d,cut);
    }
    float aa=max(fwidth(d),.0005);
    float alpha;
    float3 tint;
    if(_ActionHint>2.5){
     // Use screen pixels so the ink separator survives at hand-tray scale.
     // Differentiate the UV axes, not the distance: its nearest edge switches
     // along the diagonals of a rectangular card, even deep inside the art.
     float pixelSize=max(dot(fwidth(p)*size,normal),.0005);
     float px=d/pixelSize;
     float2 fromEdge=(.5-abs(p))*size;
     bool chosen=_ActionHint>3.5;
     float lengthAlong=max(fromEdge.x,fromEdge.y);
     float corner=1-smoothstep(.10,.105,lengthAlong/min(size.x,size.y));
     float sideTick=saturate(1-abs(p.y)*size.y/(size.x*.088));
     float topInlay=step(.35,p.y)*saturate((.15-abs(p.x))/.035);
     float weight=chosen?corner:max(sideTick,topInlay);
     // Widen inwards only. Adjacent cards keep individually readable edges.
     float inner=-1.8-weight*(chosen?3.2:4);
     float outer=chosen?lerp(-.25,1.5,corner):1.5+topInlay;
     float core=smoothstep(inner-.6,inner+.6,px)*(1-smoothstep(outer-.6,outer+.6,px));
     float key=smoothstep(inner-2.4,inner-1.3,px)*(1-smoothstep(outer+1,outer+2.2,px));
     float halo=chosen?0:.26*exp2(-max(px-outer,0)*.78);
     alpha=max(key,halo*smoothstep(outer,outer+1,px));
     float3 ink=float3(.008,.012,.018);
     float3 light=chosen?lerp(float3(.74,.44,.43),float3(1,.83,.73),corner):
                         lerp(float3(.12,.97,.45),float3(.40,1,.65),weight);
     tint=lerp(light,lerp(ink,light,core),key);
    } else {
     float outside=smoothstep(-aa,aa,d);
     float edgeLight=1-smoothstep(.003,.009+aa,d);
     float halo=exp2(-max(d,0)*60)*.23;
     alpha=outside*(edgeLight*.72+halo);
     tint=_ActionHint>1.5?float3(1,.70,.21):float3(.24,.83,.96);
    }
    alpha*=_Color.a;
    clip(alpha-.005);
    return half4(tint*_Color.rgb,alpha);
   }
   float PaperGrain(float2 p){
    float2 cell=floor(p),f=frac(p);f=f*f*(3-2*f);
    float4 h=frac(sin(float4(dot(cell,float2(127.1,311.7)),dot(cell+float2(1,0),float2(127.1,311.7)),dot(cell+float2(0,1),float2(127.1,311.7)),dot(cell+1,float2(127.1,311.7))))*43758.5453);
    return lerp(lerp(h.x,h.y,f.x),lerp(h.z,h.w,f.x),f.y);
   }
   float PaperContour(float2 p,float2 halfSize,float radius){
    float2 q=abs(p)-halfSize+radius;
    return length(max(q,0))+min(max(q.x,q.y),0)-radius;
   }
   half4 PrintedFrame(float2 uv){
    float2 size=max(_PaperSize.xy,float2(.1,.1)),p=(uv-.5)*size;
    float outer=PaperContour(p,size*.5,.026);
    if(_CutCorner>0){
     float cut=_CutCorner*min(size.x,size.y);
     outer=max(outer,(abs(p.x)+abs(p.y)-(size.x+size.y)*.5+cut)*.70710678);
    }
    // The whole card frame grows upwards. Its original art opening retains
    // both its center and dimensions beneath the new name compartment.
    float header=max(_PaperHeader,0);
    float frameInset=_CutCorner>0?.028:.042;
    float inner=PaperContour(p+float2(0,header*.5),float2(size.x,size.y-header)*.5-frameInset,.020);
    float aa=max(fwidth(outer),.0005);
    float mask=(1-smoothstep(-aa,aa,outer))*smoothstep(-aa,aa,inner);
    // A printed color band, a narrow dark cut edge, and low-contrast paper
    // grain. No raised bevel or directional metallic highlight.
    float3 tint=lerp(_FrameColorA.rgb,_FrameColorB.rgb,smoothstep(.43,.57,uv.x));
    float3 ink=lerp(float3(.022,.024,.026),tint,.46);
    float detail=1-smoothstep(.002,.012,max(fwidth(p.x),fwidth(p.y)));
    ink*=1+(PaperGrain(p*150)-.5)*.055*detail;
    float2 nameHalf=float2(size.x*.5-.042,max((header-.044)*.5,.001));
    float2 nameCenter=float2(0,size.y*.5-.042-nameHalf.y);
    float2 nameP=p-nameCenter;
    float nameDistance=max(PaperContour(nameP,nameHalf,.004),
      (abs(nameP.x)+abs(nameP.y)-nameHalf.x-nameHalf.y+.025)*.70710678);
    float nameMask=(1-smoothstep(-aa,aa,nameDistance))*step(.05,header);
    float2 nameUV=saturate((p-nameCenter)/(nameHalf*2)+.5);
    // Only the plain paper field is reused, never the rejected capsule edges.
    float2 sourceUV=_NameUVRect.xy+lerp(float2(.16,.20),float2(.84,.80),nameUV)*_NameUVRect.zw;
    float3 stock=SAMPLE_TEXTURE2D(_NameTex,sampler_NameTex,sourceUV).rgb;
    float grain=(PaperGrain(p*310)-.5)*.028;
    stock*=lerp(float3(1,1,1),.87+tint*.13,.26);
    stock*=.955+.045*nameUV.y+grain;
    float shoulder=smoothstep(.82,1,abs(nameUV.x-.5)*2);
    stock=lerp(stock,lerp(float3(.13,.15,.16),tint,.48),shoulder*.52);
    ink=lerp(ink,stock,nameMask);
    // Fine engraved edge and a restrained two-colour line follow the existing
    // card frame. Neither adds a separate capsule over the illustration.
    float2 innerNameHalf=nameHalf-float2(.010,.010);
    float inset=max(PaperContour(nameP,innerNameHalf,.003),
      (abs(nameP.x)+abs(nameP.y)-innerNameHalf.x-innerNameHalf.y+.015)*.70710678);
    float nameRim=nameMask*smoothstep(-.006-aa,.002+aa,inset);
    ink=lerp(ink,lerp(float3(.10,.12,.13),tint,.72),nameRim*.86);
    float topTrace=nameMask*(1-smoothstep(.004,.011,abs((p-nameCenter).y-nameHalf.y+.018)));
    ink=lerp(ink,lerp(tint,float3(.88,.86,.77),.24),topTrace*.82);
    float lowerTrace=nameMask*(1-smoothstep(.005,.015,abs((p-nameCenter).y+nameHalf.y-.012)));
    ink=lerp(ink,lerp(tint,float3(.26,.30,.30),.22),lowerTrace*.76);
    float edge=smoothstep(-.006-aa,-.006+aa,outer);
    float keyline=1-smoothstep(.001,.004+aa,inner);
    ink=lerp(ink,float3(.012,.014,.016),max(edge,keyline*.65));
    float alpha=mask*_Color.a;clip(alpha-.005);
    return half4(ink*_Brightness,alpha);
   }
   half4 EngravedFrame(float2 uv){
    // Match PrintedFrame's silhouette and art opening exactly. This finish
    // changes surface treatment only, not the physical card or input bounds.
    float2 size=max(_PaperSize.xy,float2(.1,.1)),p=(uv-.5)*size;
    float outer=PaperContour(p,size*.5,.026);
    if(_CutCorner>0){
     float cut=_CutCorner*min(size.x,size.y);
     outer=max(outer,(abs(p.x)+abs(p.y)-(size.x+size.y)*.5+cut)*.70710678);
    }
    float header=max(_PaperHeader,0);
    float frameInset=_CutCorner>0?.028:.042;
    float inner=PaperContour(p+float2(0,header*.5),float2(size.x,size.y-header)*.5-frameInset,.020);
    float aa=max(fwidth(outer),.0005);
    float mask=(1-smoothstep(-aa,aa,outer))*smoothstep(-aa,aa,inner);
    float3 tint=lerp(_FrameColorA.rgb,_FrameColorB.rgb,smoothstep(.43,.57,uv.x));
    float detail=1-smoothstep(.002,.012,max(fwidth(p.x),fwidth(p.y)));
    float grain=(PaperGrain(p*float2(110,230))-.5)*.018*detail;
    float3 brass=float3(.56,.44,.27);
    float3 iron=float3(.045,.058,.065)+tint*.035;
    float edgeDistance=-outer;
    // The broad enamel inner rail carries card identity; antique brass is
    // reserved for the outer tooling, never used as every card's main colour.
    float lip=1-smoothstep(.010-aa,.018+aa,edgeDistance);
    float crest=1-smoothstep(.001,.004+aa,abs(edgeDistance-.006));
    float3 metal=lerp(iron,brass*.60,lip);
    metal+=crest*float3(.20,.17,.115);
    float artLip=1-smoothstep(.010,.028+aa,inner);
    metal=lerp(metal,tint*.48,artLip*.98);
    float outerCut=1-smoothstep(.001,.004+aa,edgeDistance);
    metal=lerp(metal,float3(.014,.018,.020),outerCut*.85);
    // The dark name compartment uses more of the existing header, leaving
    // clear space around the glyphs without growing the card's silhouette.
    float2 nameHalf=float2(size.x*.5-.042,max((header-.012)*.5,.001));
    float2 nameCenter=float2(0,size.y*.5-.014-nameHalf.y);
    float2 nameP=p-nameCenter;
    float nameDistance=max(PaperContour(nameP,nameHalf,.004),
      (abs(nameP.x)+abs(nameP.y)-nameHalf.x-nameHalf.y+.025)*.70710678);
    float nameMask=(1-smoothstep(-aa,aa,nameDistance))*step(.05,header);
    float2 nameUV=saturate(nameP/(nameHalf*2)+.5);
    float3 field=float3(.010,.013,.016)+tint*lerp(.13,.17,nameUV.y);
    // The art-opening lip already supplies a separator. No extra bright rule
    // through the bottom of the name field where CJK descenders can touch it.
    metal=lerp(metal,field,nameMask)+grain;
    float alpha=mask*_Color.a;clip(alpha-.005);
    return half4(metal*_Brightness,alpha);
   }
   float ClawDistance(float2 uv,float rootX,float bottom,float top,float width){
    float t=(uv.y-bottom)/(top-bottom);
    float spine=rootX+.46*t-.07*sin(saturate(t)*3.14159);
    float taper=saturate(min(t/.42,(1-t)/.58));
    return max(abs(uv.x-spine)-width*taper,max(bottom-uv.y,uv.y-top));
   }
   half4 ToughnessClaws(float2 uv){
    float d=min(ClawDistance(uv,.04,.15,.89,.059),
                min(ClawDistance(uv,.22,.02,.995,.074),ClawDistance(uv,.44,.12,.87,.056)));
    float aa=max(fwidth(d)*.58,.004);
    float alpha=1-smoothstep(.016-aa,.016+aa,d);
    clip(alpha*_Color.a-.01);
    float core=1-smoothstep(-aa,aa,d);
    float3 red=lerp(float3(.64,.018,.038),float3(1,.17,.12),smoothstep(.60,.94,uv.y));
    return half4(lerp(float3(.17,.021,.037),red,core)*_Brightness,alpha*_Color.a);
   }
   float AshHash(float2 cell){return frac(sin(dot(cell,float2(127.1,311.7)))*43758.5453);}
   float AshNoise(float2 pos){
    float2 i=floor(pos),f=frac(pos);f=f*f*(3-2*f);
    return lerp(lerp(AshHash(i),AshHash(i+float2(1,0)),f.x),
                lerp(AshHash(i+float2(0,1)),AshHash(i+1),f.x),f.y);
   }
   half4 StatBadge(float2 uv){
    // Work in card units so wide numbers keep the same edge thickness and
    // clipped corners. The defense shield leaves a shallow point below its text.
    float2 size=max(_StatBadgeSize.xy,float2(.01,.01));
    float2 p=(uv-.5)*size,h=size*.5;
    if(_StatBadge>2.5){
     if(_FrameFinish>.5){
      // An inset graphite P/T plate: a clipped upper-left shoulder echoes the
      // name frame, while a fine bronze lip stays readable at table distance.
      // Distances use card units so multi-digit widths keep the same tooling.
      float cut=size.y*.22;
      float d=max(abs(p.x)-h.x,abs(p.y)-h.y);
      d=max(d,(-p.x+p.y-h.x-h.y+cut)*.70710678);
      d=max(d,(p.x-p.y-h.x-h.y+size.y*.08)*.70710678);
      float aa=max(fwidth(d),.0005);
      float mask=1-smoothstep(-aa,aa,d);
      float edge=-d;
      float detail=1-smoothstep(.002,.012,max(fwidth(p.x),fwidth(p.y)));
      float grain=(PaperGrain(p*float2(160,220))-.5)*.012*detail;
      float3 ink=lerp(float3(.018,.028,.035),float3(.065,.080,.086),uv.y)+grain;
      float lip=1-smoothstep(.008-aa,.016+aa,edge);
      ink=lerp(ink,float3(.54,.43,.27),lip*.88);
      float topLight=1-smoothstep(.001,.004+aa,abs(p.y-h.y+.009));
      ink+=topLight*float3(.15,.125,.075);
      float cutEdge=1-smoothstep(.001,.003+aa,edge);
      ink=lerp(ink,float3(.025,.029,.029),cutEdge*.72);
      clip(mask*_Color.a-.005);
      return half4(ink*_Brightness,mask*_Color.a);
     }
     // Keep the original light-stock P/T tab when the dark finish is off.
     float cut=size.y*.10;
     float d=max(abs(p.y)-h.y,abs(p.x)-h.x);
     d=max(d,(abs(p.x)+abs(p.y)-(h.x+h.y-cut))*.70710678);
     float aa=max(fwidth(d),.0005);
     float mask=1-smoothstep(-aa,aa,d);
     float rim=smoothstep(-.014-aa,-.014+aa,d);
     float3 ink=lerp(float3(.84,.83,.77),float3(.14,.17,.18),rim);
     clip(mask*_Color.a-.005);
     return half4(ink*_Brightness,mask*_Color.a);
    }
    float cut=size.y*.19;
    float d=max(abs(p.x)-h.x,abs(p.y)-h.y);
    if(_StatBadge<1.5){
     // Match ForgeLoyaltyShield's five-point outline in card-local units.
     float shoulder=-h.y*.30;
     float upperSide=lerp(h.x*.84,h.x*.92,saturate((p.y-shoulder)/(h.y*1.20)));
     float lowerSide=h.x*.84*saturate((p.y+h.y*.96)/(shoulder+h.y*.96));
     float side=p.y>=shoulder?upperSide:lowerSide;
     d=max(max(p.y-h.y*.90,-p.y-h.y*.96),abs(p.x)-side);
    }else{
     float slope=size.y*.32/h.x;
     d=max(d,(abs(p.x)*slope-p.y-h.y)/sqrt(1+slope*slope));
     d=max(d,(abs(p.x)+p.y-(h.x+h.y-cut*.65))*.70710678);
    }
    float aa=max(fwidth(d),.0005);
    float mask=1-smoothstep(-aa,aa,d);
    bool loyalty=_StatBadge<1.5;
    float3 baseInk=loyalty?float3(.066,.049,.096):float3(.031,.066,.079);
    float3 glaze=loyalty?float3(.22,.15,.27):float3(.11,.21,.23);
    float3 rgb=lerp(baseInk,glaze,uv.y*.72);
    float3 silver=loyalty?float3(.72,.60,.76):float3(.55,.74,.74);
    float rim=smoothstep(-.017-aa,-.017+aa,d)*(1-smoothstep(-.004-aa,-.004+aa,d));
    rgb=lerp(rgb,silver*(.60+.40*uv.y),rim);
    float alpha=mask*_Color.a;clip(alpha-.005);
    return half4(rgb*_Brightness,alpha);
   }
   half4 frag(V i):SV_Target {
    float2 screen=GetNormalizedScreenSpaceUV(i.positionCS);
    clip(min(min(screen.x-_ClipRect.x,_ClipRect.z-screen.x),min(screen.y-_ClipRect.y,_ClipRect.w-screen.y)));
    if(_DeathDissolve>0){
     float2 px=i.positionCS.xy;
     float threshold=.44*AshNoise(px*.065)+.18*AshNoise(px*.23)+.14*i.uv.y+.012;
     clip(threshold-_DeathDissolve);
    }
    if(_ActionHint>.5)return ActionGlow(i.uv);
    if(_WoundMark>.5)return ToughnessClaws(i.uv);
    if(_StatBadge>.5)return StatBadge(i.uv);
    if(_FrameTint>1.5){
     if(_FrameFinish>.5)return EngravedFrame(i.uv);
     return PrintedFrame(i.uv);
    }
    float2 q=abs(i.uv-.5)-(.5-_Radius);
    float d=length(max(q,0))+min(max(q.x,q.y),0)-_Radius;
    if(_CutCorner>0)d=max(d,(abs(i.uv.x-.5)+abs(i.uv.y-.5)-(1-_CutCorner))*.70710678);
    float aa=max(fwidth(d),.0005);
    float mask=1-smoothstep(-aa,aa,d);
    if(_Shadow>.5){
     float2 p=abs(i.uv-.5)*2;float edge=max(p.x,p.y);
     float shadow=(1-smoothstep(.65,1,edge))*.38;
     if(_Contact>1.5)shadow=(1-smoothstep(.80,1,edge))*.26;
     else if(_Contact>.5)shadow=(1-smoothstep(.70,.93,edge))*.48+(1-smoothstep(.68,1,edge))*.16;
     return half4(0,0,0,shadow*_Color.a);
    }
    float2 textureUV=i.uv;
    if(_FrameWidth>.056)textureUV=float2(FrameUV(i.uv.x,_FrameWidth),FrameUV(i.uv.y,.027+(_FrameWidth-.027)/max(_FrameAspect,.5)));
    float2 imageUV=_UVRect.xy+textureUV*_UVRect.zw;
    half4 c=SAMPLE_TEXTURE2D_BIAS(_MainTex,sampler_MainTex,imageUV,_MipBias)*_Color;
    if(_ImageSharpness>.001&&_SourceTexelSize.x>0&&_SourceTexelSize.y>0){
     // Sharpen only the on-screen illustration. Clamp taps to its source crop
     // so printed card text and frame pixels cannot bleed into the artwork.
     float2 stepUV=max(_SourceTexelSize.xy,fwidth(imageUV)*.55);
     float2 lo=_UVRect.xy+stepUV*.5;
     float2 hi=_UVRect.xy+_UVRect.zw-stepUV*.5;
     half3 neighbors=(
       SAMPLE_TEXTURE2D_BIAS(_MainTex,sampler_MainTex,clamp(imageUV+float2(stepUV.x,0),lo,hi),_MipBias).rgb+
       SAMPLE_TEXTURE2D_BIAS(_MainTex,sampler_MainTex,clamp(imageUV-float2(stepUV.x,0),lo,hi),_MipBias).rgb+
       SAMPLE_TEXTURE2D_BIAS(_MainTex,sampler_MainTex,clamp(imageUV+float2(0,stepUV.y),lo,hi),_MipBias).rgb+
       SAMPLE_TEXTURE2D_BIAS(_MainTex,sampler_MainTex,clamp(imageUV-float2(0,stepUV.y),lo,hi),_MipBias).rgb)*.25;
     c.rgb=saturate(c.rgb+(c.rgb-neighbors)*_ImageSharpness);
    }
    c*=lerp(half4(1,1,1,1),i.color,_UseVertexColor);
    if(_FooterTrim>.5){
     float3 tint=lerp(_FrameColorA.rgb,_FrameColorB.rgb,smoothstep(.42,.58,i.uv.x));
     float grain=(PaperGrain(i.uv*float2(190,45))-.5)*.025;
     c.rgb=lerp(c.rgb,lerp(float3(.026,.035,.040),tint,.13),.62)+grain;
     float edgeTop=1-smoothstep(.008,.040,1-i.uv.y);
     float edgeBottom=1-smoothstep(.015,.070,i.uv.y);
     c.rgb+=edgeTop*lerp(tint,float3(.68,.73,.72),.45)*.24;
     c.rgb=lerp(c.rgb,lerp(tint,float3(.13,.17,.17),.25),edgeBottom*.44);
    }
    if(_FrameTint>.5){
     float3 tint=lerp(_FrameColorA.rgb,_FrameColorB.rgb,smoothstep(.44,.56,i.uv.x));
     // Keep the metal's highlights and tooling while coloring its dark enamel.
     float luminance=dot(c.rgb,float3(.2126,.7152,.0722));
     c.rgb=tint*(.23+.77*luminance);
    }
    if(_Bevel>0){
     float light=max(1-smoothstep(0,_Bevel,i.uv.x),1-smoothstep(0,_Bevel,1-i.uv.y));
     float dark=max(1-smoothstep(0,_Bevel,1-i.uv.x),1-smoothstep(0,_Bevel,i.uv.y));
     c.rgb=c.rgb*(1-.4*dark)+.20*light;
    }
    c.rgb*=_Brightness;
    c.a*=mask*lerp(1,smoothstep(0,.78,i.uv.y),saturate(_TopFade));
    c.a*=lerp(1,smoothstep(0,.85,1-i.uv.y),saturate(_BottomFade));clip(c.a-.005);return c;
   }
   ENDHLSL
  }
 }
}
