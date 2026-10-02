using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public sealed partial class ForgeBattlefieldController
{
    private static Sprite emblemShape;
    private static Sprite EmblemShape()
    {
        if(emblemShape!=null)return emblemShape;
        const int w=160,h=110;
        var texture=new Texture2D(w,h,TextureFormat.RGBA32,false){hideFlags=HideFlags.HideAndDontSave,filterMode=FilterMode.Bilinear};
        var pixels=new Color[w*h];
        for(int y=0;y<h;y++)for(int x=0;x<w;x++) {
            float edge=23*Mathf.Abs((x+.5f-w*.5f)/(w*.5f));
            pixels[y*w+x]=new Color(1,1,1,Mathf.Clamp01(y-edge)*Mathf.Clamp01(Mathf.Min(x,w-1-x)));
        }
        texture.SetPixels(pixels);texture.Apply(false,true);
        emblemShape=Sprite.Create(texture,new Rect(0,0,w,h),new Vector2(.5f,.5f));
        emblemShape.hideFlags=HideFlags.HideAndDontSave;return emblemShape;
    }
    private void PresentEmblems(ForgePlayerView player,bool own)
    {
        var emblems=(player.emblems??Array.Empty<ForgeCardView>()).Concat(player.effectMarkers??Array.Empty<ForgeCardView>()).Concat(own?view.globalEffects??Array.Empty<ForgeCardView>():Array.Empty<ForgeCardView>()).ToArray();
        // Command-zone objects have their own lane; they never join permanent groups.
        for(int i=0;i<emblems.Length;i++) {
            var card=emblems[i];
            var plate=(RectTransform)Node(own?"PlayerPlate":"OpponentPlate");
            var local=new Vector3(-67+(i%2)*134,own?102+(i/2)*96:-178-(i/2)*96,0);
            var position=cardsRoot.InverseTransformPoint(plate.TransformPoint(local));
            DrawArenaCard(cardsRoot,card,1,(own?"Your":"Opponent")+"Emblem:"+card.id,"emblem:"+card.id,new Vector2(position.x,position.y),new Vector2(120,82),false,0,i);
        }
    }
    private static Sprite effectShape;
    private static Sprite EffectMarkerShape()
    {
        if(effectShape!=null)return effectShape;
        var texture=new Texture2D(160,110,TextureFormat.RGBA32,false){hideFlags=HideFlags.HideAndDontSave,filterMode=FilterMode.Bilinear};
        var pixels=new Color[160*110];
        for(int y=0;y<110;y++)for(int x=0;x<160;x++) {
            float inset=Mathf.Max(0,14-Mathf.Min(y,109-y));
            pixels[y*160+x]=new Color(1,1,1,Mathf.Clamp01(Mathf.Min(x,159-x)-inset));
        }
        texture.SetPixels(pixels);texture.Apply(false,true);
        effectShape=Sprite.Create(texture,new Rect(0,0,160,110),new Vector2(.5f,.5f));
        effectShape.hideFlags=HideFlags.HideAndDontSave;return effectShape;
    }
    private static void PresentFaceDownFace(RectTransform face,Sprite sprite,Vector2 size)
    {
        var image=face.GetComponent<Image>();image.sprite=IntegratedUiUtility.RoundedRect(140,125,7);image.color=Ink;
        face.gameObject.AddComponent<Mask>().showMaskGraphic=true;
        var art=Rect(face,"FaceDownBack",Vector2.zero,size).gameObject.AddComponent<RawImage>();
        art.texture=sprite.texture;art.raycastTarget=false;
        var r=sprite.rect;float height=r.width*size.y/size.x;
        art.uvRect=new Rect(r.x/sprite.texture.width,(r.y+(r.height-height)*.5f)/sprite.texture.height,r.width/sprite.texture.width,height/sprite.texture.height);
    }
    private IEnumerator AnimateFaceUp(RectTransform back,RectTransform front)
    {
        const float duration=.32f;
        front.localScale=new Vector3(0,1,1);
        for(float t=0;t<duration;t+=Time.unscaledDeltaTime) {
            if(back==null || front==null)break;
            float progress=t/duration;
            back.localScale=new Vector3(Mathf.Max(0,1-progress*2),1,1);
            front.localScale=new Vector3(Mathf.Max(0,progress*2-1),1,1);
            yield return null;
        }
        if(back!=null)Destroy(back.gameObject);
        if(front!=null)front.localScale=Vector3.one;
    }
}
