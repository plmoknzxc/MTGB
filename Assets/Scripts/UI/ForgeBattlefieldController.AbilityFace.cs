using UnityEngine;
using UnityEngine.UI;

public sealed partial class ForgeBattlefieldController
{
    private static Sprite abilityShape;
    private static Sprite AbilityShape()
    {
        if(abilityShape!=null)return abilityShape;
        const int width=240,height=335;
        var pixels=new Color[width*height];
        for(int y=0;y<height;y++)for(int x=0;x<width;x++) {
            // Straight sides and a shallow elliptical base, as on an ability card.
            float half=y<54?120*Mathf.Sqrt(Mathf.Max(0,1-Mathf.Pow((54-y-.5f)/54,2))):120;
            pixels[y*width+x]=new Color(1,1,1,Mathf.Clamp01(half-Mathf.Abs(x+.5f-120)));
        }
        var texture=new Texture2D(width,height,TextureFormat.RGBA32,false){hideFlags=HideFlags.HideAndDontSave,wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Bilinear};
        texture.SetPixels(pixels);texture.Apply(false,true);
        abilityShape=Sprite.Create(texture,new Rect(0,0,width,height),new Vector2(.5f,.5f),100);
        abilityShape.hideFlags=HideFlags.HideAndDontSave;return abilityShape;
    }
    // Ability objects use their source illustration and their own effect text.
    // They are not a second copy of the permanent's full rules card.
    private void PresentAbilityFace(RectTransform parent, ForgeCardView source, string effect, string kind, bool visible)
    {
        var face=parent.Find("AbilityFace") as RectTransform;
        if(!visible){if(face!=null)face.gameObject.SetActive(false);return;}
        parent.GetComponent<Image>().sprite=null;parent.GetComponent<Image>().color=Color.clear;
        if(face==null) {
            // Place uses centre anchors. A top-pivot stack tile already offsets that
            // anchor; adding parent.rect.center here would apply the offset twice.
            face=Panel(parent,"AbilityFace",Vector2.zero,new Vector2(240,335),Ink);
            face.GetComponent<Image>().sprite=AbilityShape();
            face.GetComponent<Image>().raycastTarget=false;
            var name=Label(face,"Name","",new Vector2(0,146),new Vector2(220,32),18,Paper);
            name.raycastTarget=false;name.resizeTextForBestFit=true;name.resizeTextMinSize=12;name.resizeTextMaxSize=18;
            var art=Rect(face,"Art",new Vector2(0,69),new Vector2(218,118)).gameObject.AddComponent<RawImage>();art.raycastTarget=false;
            var text=Label(face,"Effect","",new Vector2(0,-65),new Vector2(218,138),18,Paper);
            text.alignment=TextAnchor.UpperLeft;text.supportRichText=false;text.raycastTarget=false;
            text.resizeTextForBestFit=true;text.resizeTextMinSize=12;text.resizeTextMaxSize=18;
        }
        face.gameObject.SetActive(true);
        face.localScale=new Vector3(parent.rect.width/240f,parent.rect.height/335f,1);
        face.Find("Name").GetComponent<Text>().text=source?.name ?? "异能";
        string prefix=source?.name+" ("+source?.id+") - ";
        string effectText=effect!=null && effect.StartsWith(prefix,System.StringComparison.Ordinal)?effect.Substring(prefix.Length):effect ?? "";
        // Forge appends zone-change debugging context to trigger descriptions.
        effectText=System.Text.RegularExpressions.Regex.Replace(effectText,@"\s*\[Zone Changer: [^\r\n]*\]\s*$","");
        face.Find("Effect").GetComponent<Text>().text=effectText;
        Sprite sprite=null;
        TryBattlefieldArt(source, out sprite);
        var image=face.Find("Art").GetComponent<RawImage>();image.texture=sprite?.texture;
        image.color=sprite==null?new Color(.12f,.19f,.24f):Color.white;
        if(sprite!=null){var r=sprite.rect;image.uvRect=new Rect((r.x+r.width*.07f)/sprite.texture.width,(r.y+r.height*.43f)/sprite.texture.height,r.width*.86f/sprite.texture.width,r.height*.38f/sprite.texture.height);}
    }
}
