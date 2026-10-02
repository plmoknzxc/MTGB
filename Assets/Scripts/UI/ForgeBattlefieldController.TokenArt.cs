using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public sealed partial class ForgeBattlefieldController
{
    private static bool HasTokenFrame(ForgeCardView card) => !card.faceDown && (card.token || card.copy);
    static Sprite tokenArch;
    private static Sprite TokenArch()
    {
        if(tokenArch!=null)return tokenArch;
        const int w=200,h=240;
        var texture=new Texture2D(w,h,TextureFormat.RGBA32,false){hideFlags=HideFlags.HideAndDontSave,filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp};
        var pixels=new Color[w*h];
        for(int y=0;y<h;y++)for(int x=0;x<w;x++) {
            float nx=(x+.5f-w*.5f)/(w*.5f);
            float top=h-3-35*(1-Mathf.Sqrt(Mathf.Max(0,1-nx*nx)));
            float alpha=Mathf.Clamp01(top-y)*Mathf.Clamp01(Mathf.Min(x,w-1-x))*Mathf.Clamp01(y);
            pixels[y*w+x]=new Color(1,1,1,alpha);
        }
        texture.SetPixels(pixels);texture.Apply(false,true);
        tokenArch=Sprite.Create(texture,new Rect(0,0,w,h),new Vector2(.5f,.5f),100);
        tokenArch.hideFlags=HideFlags.HideAndDontSave;return tokenArch;
    }
    private static void PresentSpecialPermanentFace(RectTransform parent,Sprite sprite,Vector2 size,ForgeCardView card)
    {
        var image=parent.GetComponent<Image>();image.sprite=HasTokenFrame(card)?TokenArch():IntegratedUiUtility.RoundedRect(136,190,5);image.color=Ink;
        parent.gameObject.AddComponent<Mask>().showMaskGraphic=true;
        var r=sprite.rect;
        float titleHeight=HasTokenFrame(card)||card.emblem||card.effectMarker?0:Mathf.Min(12,size.x*.08f);
        var art=Rect(parent,"Illustration",new Vector2(0,-titleHeight*.5f+2),new Vector2(size.x-6,size.y-titleHeight-8)).gameObject.AddComponent<RawImage>();
        art.texture=sprite.texture;art.color=card.tapped?new Color(.65f,.68f,.72f):Color.white;art.raycastTarget=false;
        // Use the illustration region, not the printed rules/loyalty box.
        float w=r.width*.86f,h=r.height*(card.planeswalker?.40f:.38f);
        float aspect=art.rectTransform.rect.width/art.rectTransform.rect.height;
        if(w/h>aspect)w=h*aspect;else h=w/aspect;
        art.uvRect=new Rect((r.x+r.width*.5f-w*.5f)/sprite.texture.width,(r.y+r.height*(card.planeswalker?.69f:.62f)-h*.5f)/sprite.texture.height,w/sprite.texture.width,h/sprite.texture.height);
        if(!HasTokenFrame(card) && !card.emblem && !card.effectMarker) {
            var title=Rect(parent,"PrintedName",new Vector2(0,(size.y-titleHeight)*.5f-1),new Vector2(size.x-4,titleHeight)).gameObject.AddComponent<RawImage>();
            title.texture=sprite.texture;title.raycastTarget=false;
            title.uvRect=new Rect((r.x+r.width*.065f)/sprite.texture.width,(r.y+r.height*.895f)/sprite.texture.height,r.width*.87f/sprite.texture.width,r.height*.052f/sprite.texture.height);
        }
    }
    private bool TryBattlefieldArt(ForgeCardView card, out Sprite sprite)
    {
        sprite = null;
        if (card == null) return false;
        if (card.faceDown) return CardImageLibrary.TryLoadCardBack(out sprite);
        if(card.effectMarker && SpecialEffectArt.TryLoad(card.helperArtName,out sprite))return true;
        if(card.emblem) {
            if(CardImageLibrary.TryLoadPrintingKey(card.tokenArtKey,out sprite))return true;
            if(!catalog.TryGetValue(card.tokenSourcePrintingKey??"",out var emblemSource))return false;
            string emblemName=(card.name??"").Replace("Emblem — ","");
            var emblems=DeckRelatedTokenArt.GetReferences(emblemSource).Where(t=>t.name==emblemName || t.name==card.name || t.name==emblemName+" Emblem").ToArray();
            return emblems.Length==1 && CardImageLibrary.TryLoadPrintingKey(emblems[0].key,out sprite);
        }
        if (catalog.TryGetValue(card.printingKey ?? "", out var definition))
            return CardImageLibrary.TryLoadFullCard(definition, card.backFace ? 1 : 0, out sprite);
        if(card.token && CardImageLibrary.TryLoadPrintingKey(card.tokenArtKey,out sprite))return true;
        if (!card.token || !catalog.TryGetValue(card.tokenSourcePrintingKey ?? "", out var source)) return false;
        string name = card.name ?? "";
        if (name.EndsWith(" Token", StringComparison.Ordinal)) name = name.Substring(0, name.Length - 6);
        var matches = DeckRelatedTokenArt.GetReferences(source).Where(t => string.Equals(t.name, name, StringComparison.OrdinalIgnoreCase)).ToArray();
        // Never guess between different tokens with the same name.
        return matches.Length == 1 && CardImageLibrary.TryLoadPrintingKey(matches[0].key, out sprite);
    }
}
