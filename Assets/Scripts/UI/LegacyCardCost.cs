using System.Text.RegularExpressions;
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
    bool raisedTitle,mainFirst,paired,sideways;
    TMP_Text separator,mainEmpty,secondaryEmpty;
    public static float IconSize(float width)=>Mathf.Min(22,width*.095f);
    static int Count(string cost)=>Regex.Matches(cost??"",@"\{[^{}]+\}").Count;
    static string Normalize(string cost)=>string.Equals(cost,"no cost",System.StringComparison.OrdinalIgnoreCase)?"":cost??"";
    RectTransform Region(string name)
    {
        var r=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();r.SetParent(Root,false);return r;
    }
    public void Bind(Sprite art,string printedCost,string dynamicCost,string printedAdventure,string dynamicAdventure,bool highTitle=false,bool primaryFirst=false,bool pair=false,bool rotated=false)
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
            mainPatch=Count(printed)>0?Cover(source,mainBounds,sideways):null;adventurePatch=Count(adventurePrinted)>0?Cover(source,adventureBounds,sideways):null;
            mainCover.texture=mainPatch;adventureCover.texture=adventurePatch;
        }
        current=Normalize(dynamicCost);adventureCurrent=Normalize(dynamicAdventure);
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
        // Never erase the native cost if a glyph cannot be rendered.
        mainCover.gameObject.SetActive(mainPatch!=null&&Main.Complete&&Adventure.Complete);
        adventureCover.gameObject.SetActive(adventurePatch!=null&&Main.Complete&&Adventure.Complete);
    }
    // Reuse a quiet strip of the scanned title texture. The narrow feather
    // keeps native title edges intact when the hand card is enlarged.
    static Texture2D Cover(Sprite art,Rect uv,bool rotated)
    {
        var sourceRect=art.rect;var pixel=new Rect(Mathf.Floor(sourceRect.x+uv.x*sourceRect.width),Mathf.Floor(sourceRect.y+uv.y*sourceRect.height),Mathf.Ceil(uv.width*sourceRect.width),Mathf.Ceil(uv.height*sourceRect.height));
        int width=Mathf.Max(4,(int)pixel.width),height=Mathf.Max(4,(int)pixel.height);
        var target=RenderTexture.GetTemporary(art.texture.width,art.texture.height,0,RenderTextureFormat.ARGB32);var previous=RenderTexture.active;
        var patch=new Texture2D(width,height,TextureFormat.RGBA32,true){name="PrintedCostCover",filterMode=FilterMode.Trilinear,wrapMode=TextureWrapMode.Clamp};
        try{
            Graphics.Blit(art.texture,target);RenderTexture.active=target;patch.ReadPixels(pixel,0,0);var pixels=patch.GetPixels();
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
