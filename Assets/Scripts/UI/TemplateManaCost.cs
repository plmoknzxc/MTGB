using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Each glyph has its own mip chain, so small card fees stay smooth without
// bleeding adjacent symbols in the shared atlas. The shared pool is untouched.
public sealed class TemplateManaCost : MonoBehaviour
{
    static readonly Dictionary<string,Texture2D> textures=new();
    readonly List<RawImage> icons=new();
    public bool Complete { get; private set; }
    public void Bind(string cost,float diameter,bool alignRight=true,float gap=3)
    {
        var matches=Regex.Matches(cost??"",@"\{([^{}]+)\}");
        Complete=Regex.Replace(cost??"",@"\{([^{}]+)\}","").Trim().Length==0;
        for(int i=0;i<matches.Count;i++) {
            string token=matches[i].Groups[1].Value.ToUpperInvariant();
            var texture=Glyph(token);if(texture==null){Complete=false;break;}
            if(i==icons.Count) {
                var image=new GameObject("ManaIcon"+i,typeof(RectTransform),typeof(RawImage)).GetComponent<RawImage>();
                image.transform.SetParent(transform,false);image.raycastTarget=false;icons.Add(image);
            }
            var icon=icons[i];icon.texture=texture;icon.gameObject.SetActive(true);
            var r=icon.rectTransform;r.anchorMin=r.anchorMax=r.pivot=new Vector2(1,.5f);
            r.sizeDelta=Vector2.one*diameter;
            r.anchorMin=r.anchorMax=r.pivot=new Vector2(alignRight?1:0,.5f);
            r.anchoredPosition=new Vector2(alignRight?-(matches.Count-1-i)*(diameter+gap):i*(diameter+gap),0);
        }
        for(int i=0;i<icons.Count;i++)icons[i].gameObject.SetActive(Complete&&i<matches.Count);
    }
    static Texture2D Glyph(string token)
    {
        if(textures.TryGetValue(token,out var cached))return cached;
        // The supplied card template's native SVG symbols have flat printed
        // colors, without the Forge UI atlas's bevel and drop shadow.
        var printed=Resources.Load<Texture2D>("UI/CardTemplates/ManaSymbols/"+token.Replace('/','_'));
        if(printed!=null){textures[token]=printed;return printed;}
        var main=CardSymbolText.Atlas;
        foreach(var asset in new[]{main}.Concat(main.fallbackSpriteAssets)) {
            var character=asset.spriteCharacterTable.FirstOrDefault(c=>c.name==token);if(character==null)continue;
            var source=asset.spriteSheet;var rect=character.glyph.glyphRect;
            var target=RenderTexture.GetTemporary(source.width,source.height,0,RenderTextureFormat.ARGB32);
            var previous=RenderTexture.active;
            try {
                Graphics.Blit(source,target);RenderTexture.active=target;
                var image=new Texture2D(rect.width,rect.height,TextureFormat.RGBA32,true){name="TemplateMana_"+token,filterMode=FilterMode.Trilinear,wrapMode=TextureWrapMode.Clamp};
                image.ReadPixels(new Rect(rect.x,rect.y,rect.width,rect.height),0,0);image.Apply(true,true);
                textures[token]=image;return image;
            }finally{RenderTexture.active=previous;RenderTexture.ReleaseTemporary(target);}
        }
        return null;
    }
}
