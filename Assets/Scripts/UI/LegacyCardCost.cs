using System.Text.RegularExpressions;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Keep the original scanned face. Only remove its printed cost and attach the
// authoritative preview cost above the card, following its rotation and hover.
public sealed class LegacyCardCost : MonoBehaviour
{
    public RectTransform Root { get; private set; }
    public TemplateManaCost Main { get; private set; }
    public TemplateManaCost Adventure { get; private set; }
    Sprite source;
    string printed,adventurePrinted,current,adventureCurrent;
    RawImage mainCover,adventureCover;
    Texture2D mainPatch,adventurePatch;
    Rect mainBounds,adventureBounds;
    bool raisedTitle,mainFirst,paired,sideways,printedIsSecondary,hovered;
    ForgeBattlefieldCardPointer pointer;
    TMP_Text separator,mainEmpty,secondaryEmpty;
    public static float IconSize(float width)=>Mathf.Min(22,width*.095f);
    static int Count(string cost)=>Regex.Matches(cost??"",@"\{[^{}]+\}").Count;
    static string Normalize(string cost)=>string.Equals(cost,"no cost",System.StringComparison.OrdinalIgnoreCase)?"":cost??"";
    static bool SameCost(string a,string b)=>Regex.Replace(Normalize(a),@"\s+","").ToUpperInvariant()==Regex.Replace(Normalize(b),@"\s+","").ToUpperInvariant();
    RectTransform Region(string name)
    {
        var r=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();r.SetParent(Root,false);return r;
    }
    public void Bind(Sprite art,string printedCost,string dynamicCost,string printedAdventure,string dynamicAdventure,bool highTitle=false,bool primaryFirst=false,bool pair=false,bool rotated=false,bool secondaryFace=false)
    {
        if(Root==null){
            Root=new GameObject("LegacyCostOverlay",typeof(RectTransform)).GetComponent<RectTransform>();Root.SetParent(transform,false);
            Root.anchorMin=Vector2.zero;Root.anchorMax=Vector2.one;Root.offsetMin=Root.offsetMax=Vector2.zero;
            mainCover=Region("PrintedCostCover").gameObject.AddComponent<RawImage>();mainCover.raycastTarget=false;
            adventureCover=Region("PrintedAdventureCostCover").gameObject.AddComponent<RawImage>();adventureCover.raycastTarget=false;
            Main=Region("DynamicCost").gameObject.AddComponent<TemplateManaCost>();
            Adventure=Region("DynamicAdventureCost").gameObject.AddComponent<TemplateManaCost>();
            separator=CardSymbolText.Create(Root,"CostSeparator",18,Color.white);separator.text="/";
            mainEmpty=CardSymbolText.Create(Root,"MainWithoutManaCost",18,Color.white);mainEmpty.text="—";
            secondaryEmpty=CardSymbolText.Create(Root,"SecondaryWithoutManaCost",18,Color.white);secondaryEmpty.text="—";
            foreach(var text in new[]{separator,mainEmpty,secondaryEmpty}){text.raycastTarget=false;text.alignment=TextAlignmentOptions.Center;text.textWrappingMode=TextWrappingModes.NoWrap;}
        }
        if(source!=art||printed!=printedCost||adventurePrinted!=printedAdventure||raisedTitle!=highTitle||sideways!=rotated){
            source=art;printed=printedCost;adventurePrinted=printedAdventure;
            raisedTitle=highTitle;sideways=rotated;
            if(mainPatch!=null)Destroy(mainPatch);if(adventurePatch!=null)Destroy(adventurePatch);
            // Older scans place the last symbol farther left; long printed
            // costs use a tighter pitch. Include the full circle and shadow.
            float mainLeft=Mathf.Max(.06f,.938f-Count(printed)*(Count(printed)>6?.05f:.056f)-.026f);
            float adventureLeft=Mathf.Max(.07f,.488f-Count(adventurePrinted)*.048f-.008f);
            mainBounds=new Rect(mainLeft,highTitle?.918f:.89f,.938f-mainLeft,highTitle?.052f:.066f);
            adventureBounds=new Rect(adventureLeft,.329f,.488f-adventureLeft,.044f);
            if(sideways){
                mainBounds=new Rect(.043f,.5f-Count(printed)*.035f-.023f,.086f,Count(printed)*.035f+.016f);
                adventureBounds=new Rect(.043f,.963f-Count(adventurePrinted)*.035f-.023f,.086f,Count(adventurePrinted)*.035f+.011f);
            }
            mainPatch=Count(printed)>0?Cover(source,ref mainBounds,sideways,highTitle?0:Count(printed)):null;
            adventurePatch=Count(adventurePrinted)>0?Cover(source,ref adventureBounds,sideways):null;
            mainCover.texture=mainPatch;adventureCover.texture=adventurePatch;
        }
        current=Normalize(dynamicCost);adventureCurrent=Normalize(dynamicAdventure);
        printedIsSecondary=secondaryFace;pointer=GetComponentInParent<ForgeBattlefieldCardPointer>();
        mainFirst=primaryFirst;paired=pair||printedAdventure!=null;
        Root.gameObject.SetActive(true);Root.SetAsLastSibling();Layout();
    }
    public void Hide(){if(Root!=null)Root.gameObject.SetActive(false);}
    void Layout()
    {
        if(Root==null||source==null)return;
        var box=((RectTransform)transform).rect.size;float scale=Mathf.Min(box.x/source.rect.width,box.y/source.rect.height);
        var face=source.rect.size*scale;
        void Place(RawImage patch,Rect uv){patch.gameObject.SetActive(patch.texture!=null);patch.rectTransform.sizeDelta=new Vector2(face.x*uv.width,face.y*uv.height);patch.rectTransform.anchoredPosition=new Vector2(face.x*(uv.center.x-.5f),face.y*(uv.center.y-.5f));}
        Place(mainCover,mainBounds);Place(adventureCover,adventureBounds);
        int n=Count(current),a=Count(adventureCurrent),total=Mathf.Max(1,n)+(paired?Mathf.Max(1,a):0);
        float diameter=IconSize(face.x),gap=0;
        diameter=Mathf.Min(diameter,(face.x-12)/(total+(paired?.55f:0)));
        float Width(int count)=>Mathf.Max(1,count)*diameter+Mathf.Max(0,count-1)*gap;
        float mw=Width(n),aw=paired?Width(a):0,sw=paired?diameter*.55f:0;
        float width=mw+aw+sw,left=face.x*.5f-6-width,y=face.y*.5f+diameter*.5f+2;
        void Group(RectTransform r,float x,float w){r.anchorMin=r.anchorMax=new Vector2(.5f,.5f);r.pivot=new Vector2(0,.5f);r.sizeDelta=new Vector2(w,diameter+4);r.anchoredPosition=new Vector2(x,y);}
        float mx=left+(paired&&!mainFirst?aw+sw:0),ax=left+(mainFirst?mw+sw:0);
        Group((RectTransform)Main.transform,mx,mw);Group((RectTransform)Adventure.transform,ax,aw);
        Main.Bind(current,diameter,false,gap);Adventure.Bind(paired?adventureCurrent:"",diameter,false,gap);
        // These marks follow the icon geometry, including after preference rebinding.
        float textScale=Mathf.Max(.01f,GamePreferences.TextScale);
        Group(separator.rectTransform,left+(mainFirst?mw:aw),sw);PreferenceText.SetSize(separator,diameter*.85f/textScale);separator.gameObject.SetActive(paired);
        Group(mainEmpty.rectTransform,mx,mw);PreferenceText.SetSize(mainEmpty,diameter*.8f/textScale);mainEmpty.gameObject.SetActive(paired&&n==0);
        Group(secondaryEmpty.rectTransform,ax,aw);PreferenceText.SetSize(secondaryEmpty,diameter*.8f/textScale);secondaryEmpty.gameObject.SetActive(paired&&a==0);
        RefreshPrintedCosts();
    }
    void RefreshPrintedCosts()
    {
        hovered=pointer!=null&&pointer.IsHovered;
        // Reveal the actual scanned symbols at their original position; keep
        // both authoritative preview strips above the card untouched.
        bool complete=Main.Complete&&Adventure.Complete;
        mainCover.gameObject.SetActive(mainPatch!=null&&complete&&!(hovered&&!SameCost(printed,printedIsSecondary?adventureCurrent:current)));
        adventureCover.gameObject.SetActive(adventurePatch!=null&&complete&&!(hovered&&!SameCost(adventurePrinted,adventureCurrent)));
    }
    void LateUpdate(){if(Root!=null&&hovered!=(pointer!=null&&pointer.IsHovered))RefreshPrintedCosts();}
    // Reuse a quiet strip of the scanned title texture. The narrow feather
    // keeps native title edges intact when the hand card is enlarged.
    static readonly Vector2[] circleSamples=CircleSamples();
    static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Sprite,Dictionary<(Rect,int),Rect>> printedBounds=new();
    static Vector2[] CircleSamples()
    {
        var samples=new Vector2[24];
        for(int i=0;i<samples.Length;i++){float angle=(i+.5f)*Mathf.PI*2/samples.Length;samples[i]=new Vector2(Mathf.Cos(angle),Mathf.Sin(angle));}
        return samples;
    }
    // Localized scans can print much larger symbols than the usual frame.
    // Widen only after finding the complete, evenly spaced circle sequence;
    // otherwise retain the established bounds rather than cover title text.
    static Rect WidenPrintedCost(Sprite art,Rect bounds,int count)
    {
        var cached=printedBounds.GetOrCreateValue(art);
        if(cached.TryGetValue((bounds,count),out var found))return found;
        Rect Complete(Rect value){cached[(bounds,count)]=value;return value;}
        int width=(int)art.rect.width,height=(int)art.rect.height,bottom=Mathf.FloorToInt(height*.86f);
        var sample=new Texture2D(width,Mathf.CeilToInt(height*.12f),TextureFormat.RGB24,false);
        Color[] pixels;
        try{sample.ReadPixels(new Rect(art.rect.x,art.rect.y+bottom,width,sample.height),0,0);pixels=sample.GetPixels();}
        finally{Destroy(sample);}
        float Light(float x,float y){var c=pixels[Mathf.Clamp(Mathf.RoundToInt(y)-bottom,0,pixels.Length/width-1)*width+Mathf.Clamp(Mathf.RoundToInt(x),0,width-1)];return(c.r+c.g+c.b)/3;}
        float Score(int x,int y,int radius,out int votes){
            float score=0;votes=0;
            foreach(var d in circleSamples){float contrast=(Light(x+d.x*radius*1.18f,y+d.y*radius*1.18f)+Light(x+d.x*radius*.75f,y+d.y*radius*.75f))*.5f-Light(x+d.x*radius,y+d.y*radius);score+=contrast;if(contrast>.08f)votes++;}
            return score/circleSamples.Length;
        }
        float best=.11f;int cx=0,cy=0,r=0;
        for(int radius=Mathf.RoundToInt(width*.018f);radius<=Mathf.RoundToInt(width*.044f);radius++)
            for(int y=Mathf.RoundToInt(height*.894f);y<Mathf.RoundToInt(height*.958f);y+=2)
                for(int x=Mathf.RoundToInt(width*.865f);x<Mathf.RoundToInt(width*.94f);x+=2){
                    float score=Score(x,y,radius,out int votes);
                    if(votes>=10&&score>best){best=score;cx=x;cy=y;r=radius;}
                }
        if(r==0)return Complete(bounds);
        int right=cx,left=cx;
        for(int i=1;i<count;i++){
            best=.11f;int next=0;
            for(int x=Mathf.Max(0,Mathf.RoundToInt(left-2.8f*r));x<Mathf.RoundToInt(left-1.8f*r);x++){
                float score=Score(x,cy,r,out int votes);if(votes>=10&&score>best){best=score;next=x;}
            }
            if(next==0)return Complete(bounds);
            left=next;
        }
        float padding=r+Mathf.Ceil(r*.25f)+3;
        if((left-padding)/width>=bounds.xMin)return Complete(bounds);
        return Complete(Rect.MinMaxRect(Mathf.Max(.06f,(left-padding)/width),Mathf.Min(bounds.yMin,(cy-padding)/height),
            Mathf.Max(bounds.xMax,(right+padding)/width),Mathf.Max(bounds.yMax,(cy+padding)/height)));
    }
    static Texture2D Cover(Sprite art,ref Rect uv,bool rotated,int symbols=0)
    {
        var target=RenderTexture.GetTemporary(art.texture.width,art.texture.height,0,RenderTextureFormat.ARGB32);var previous=RenderTexture.active;
        try{
            Graphics.Blit(art.texture,target);RenderTexture.active=target;
            if(!rotated&&symbols>0)uv=WidenPrintedCost(art,uv,symbols);
            var sourceRect=art.rect;var pixel=new Rect(Mathf.Floor(sourceRect.x+uv.x*sourceRect.width),Mathf.Floor(sourceRect.y+uv.y*sourceRect.height),Mathf.Ceil(uv.width*sourceRect.width),Mathf.Ceil(uv.height*sourceRect.height));
            int width=Mathf.Max(4,(int)pixel.width),height=Mathf.Max(4,(int)pixel.height);
            var patch=new Texture2D(width,height,TextureFormat.RGBA32,true){name="PrintedCostCover",filterMode=FilterMode.Trilinear,wrapMode=TextureWrapMode.Clamp};
            patch.ReadPixels(pixel,0,0);var pixels=patch.GetPixels();
            // Clone the adjacent blank title texture, preserving its fine grain
            // and bevel across the fee region instead of stretching two pixels.
            int strip=6;
            int reach=36;
            var sample=new Texture2D(rotated?width:reach,rotated?reach:height,TextureFormat.RGBA32,false);
            sample.ReadPixels(rotated?new Rect(pixel.x,pixel.y-reach,width,reach):new Rect(pixel.x-reach,pixel.y,reach,height),0,0);
            var candidates=sample.GetPixels();Destroy(sample);
            // A neighbouring strip can contain a letter or the old circle's
            // shadow. Select the quietest short strip in the adjacent title.
            int best=0;float bestScore=float.PositiveInfinity;
            for(int start=0;start<=reach-strip;start++){
                float score=0;
                for(int across=3;across<(rotated?width:height)-3;across++)
                    for(int along=0;along<strip-1;along++){
                        var a=candidates[rotated?(start+along)*width+across:across*reach+start+along];
                        var b=candidates[rotated?(start+along+1)*width+across:across*reach+start+along+1];
                        score+=Mathf.Abs(a.r-b.r)+Mathf.Abs(a.g-b.g)+Mathf.Abs(a.b-b.b);
                    }
                if(score<bestScore){bestScore=score;best=start;}
            }
            for(int y=0;y<height;y++){
                Color left=(pixels[y*width]+pixels[y*width+1])*.5f,right=(pixels[y*width+width-1]+pixels[y*width+width-2])*.5f;
                for(int x=0;x<width;x++){
                    var c=rotated?candidates[(best+y%strip)*width+x]:candidates[y*reach+best+x%strip];
                    if(!rotated){c=Color.Lerp(left,c,Mathf.Clamp01(x/5f));c=Color.Lerp(right,c,Mathf.Clamp01((width-1-x)/5f));}
                    c.a=Mathf.Clamp01(Mathf.Min(x,width-1-x,y,height-1-y)/2f);pixels[y*width+x]=c;
                }
            }
            patch.SetPixels(pixels);patch.Apply(true,true);return patch;
        }finally{RenderTexture.active=previous;RenderTexture.ReleaseTemporary(target);}
    }
    void OnRectTransformDimensionsChange()=>Layout();
    void OnDestroy(){if(mainPatch!=null)Destroy(mainPatch);if(adventurePatch!=null)Destroy(adventurePatch);}
}
