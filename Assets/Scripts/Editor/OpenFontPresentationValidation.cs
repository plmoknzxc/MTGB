#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Real card data and the production Bind renderer; display evidence only.
public static class OpenFontPresentationValidation
{
    const string Output=".tools/open-font-preview-1014/";
    static readonly string[] Names={"Lightning Bolt","Force of Negation","Bonecrusher Giant","Mishra's Bauble","The Eldest Reborn","Teferi, Time Raveler","Uro, Titan of Nature's Wrath","The Wandering Emperor"};
    [Serializable] sealed class Observation { public string locale,card,slot,font,text; public float size; public int lines; public bool fits; }
    [Serializable] sealed class Result { public string status,scope; public int checks; public List<Observation> labels=new(); }
    static Result result;
    static GameObject shelf;
    static GameObject previewCamera;
    static bool running;
    static void Check(bool value,string message){if(!value)throw new Exception(message);result.checks++;File.AppendAllText(Output+"runtime-checks.log",message+" PASS\n");}
    static IEnumerator Frame(){yield return new WaitForSecondsRealtime(.2f);Canvas.ForceUpdateCanvases();yield return new WaitForEndOfFrame();}
    public static void Run()
    {
        if(!Application.isPlaying)throw new Exception("Play mode required");if(running)return;running=true;
        Directory.CreateDirectory(Output);File.WriteAllText(Output+"runtime-checks.log","");File.WriteAllText(Output+"runtime-result.txt","RUNNING");
        result=new Result{status="RUNNING",scope="Eight real catalog printings, three locale switches, production TemplateCardFace.Bind, glyphs/wrapping/font weights/mana symbols/preference scan/rebind; no game effect or network claim."};
        var runner=UnityEngine.Object.FindFirstObjectByType<ForgeBattlefieldController>()??new GameObject("OpenFontValidationRunner").AddComponent<ForgeBattlefieldController>();
        runner.StartCoroutine(Guard());
    }
    static IEnumerator Guard()
    {
        string locale=GamePreferences.Language;float scale=GamePreferences.TextScale;
        var work=Validate();
        try {
            while(true){object next;try{if(!work.MoveNext())break;next=work.Current;}catch(Exception e){result.status="FAIL "+e;throw;}yield return next;}
            result.status="PASS";
        }finally{
            File.WriteAllText(Output+"runtime-result.json",JsonUtility.ToJson(result,true));File.WriteAllText(Output+"runtime-result.txt",result.status+" "+result.checks);
            if(shelf!=null)UnityEngine.Object.Destroy(shelf);if(previewCamera!=null)UnityEngine.Object.Destroy(previewCamera);GamePreferences.SetScale(scale);GamePreferences.SetLanguage(locale);running=false;
        }
    }
    static void Bind(TemplateCardFace face,CardDefinition card,Sprite art)
    {
        var adventure=card.forgeSplitType=="Adventure"?card.features.FirstOrDefault(f=>f.cardName!=card.cardName):null;
        bool walker=card.HasType(CardType.Planeswalker),saga=(card.subtypes??"").Split(' ').Contains("Saga");
        bool fourRows=walker&&(System.Text.RegularExpressions.Regex.Matches(card.description??"",@"(?:^|\n)\s*\[?[+−-]?\d+\]?:").Count>=4||(card.description??"").Length>400);
        string colors=string.Concat("WUBRG".Where(c=>(card.manaCost??"").Contains(c.ToString())));
        string color=card.HasType(CardType.Land)?"L":colors.Length>1?"M":colors.Length==1?colors:card.HasType(CardType.Artifact)?"A":"C";
        string frame=(adventure!=null?"Adventure":walker?fourRows?"Planeswalker4":"Planeswalker":saga?"Saga":"Basic")+color;
        string stats=card.HasType(CardType.Creature)?card.attack+"/"+card.defence:walker?card.loyalty.ToString():null;
        void Draw(string selectedFrame)=>face.Bind(art,GamePreferences.CardName(card),card.manaCost,GamePreferences.CardType(card),GamePreferences.CardRules(card),stats,card.manaCost,
            adventure==null?null:GamePreferences.FeatureName(adventure),adventure?.manaCost,adventure==null?null:GamePreferences.FeatureType(adventure,"Instant — Adventure"),adventure==null?null:GamePreferences.FeatureRules(adventure),selectedFrame,false);
        Draw(frame);
        if(walker&&!fourRows&&!face.Fits)Draw("Planeswalker4"+color);
    }
    static void Audit(TemplateCardFace face,string locale,string card)
    {
        Check(face.Fits,locale+" readable template "+card);
        Check(face.GetComponentsInChildren<RawImage>().Any(i=>i.name=="Frame"&&i.texture!=null),locale+" existing frame texture "+card);
        if(card=="Lightning Bolt")Check(face.Title.font.HasCharacters("𫠒𫚉☐ˣ⁵",out uint[] missing,true,true),locale+" rare CJK/checkbox/math fallback glyphs");
        foreach(var t in face.GetComponentsInChildren<TextMeshProUGUI>()){
            if(!t.enabled||string.IsNullOrEmpty(t.text))continue;t.ForceMeshUpdate(true,true);
            Check(!t.isTextOverflowing,locale+" no clipping "+card+" "+t.name);
            Check(t.textInfo.characterCount>0,locale+" text mesh "+card+" "+t.name);
            Check(t.fontStyle==FontStyles.Normal,locale+" no synthetic bold "+card+" "+t.name);
            foreach(var c in t.textInfo.characterInfo.Take(t.textInfo.characterCount)){
                if(c.elementType!=TMP_TextElementType.Character||char.IsWhiteSpace(c.character))continue;
                Check(c.textElement!=null&&c.textElement.glyph!=null&&c.textElement.glyph.index!=0&&c.textElement.unicode!=0x25A1,locale+" glyph "+card+" "+t.name+" U+"+((int)c.character).ToString("X4"));
            }
            string region=locale.StartsWith("zh")?"CN":locale.StartsWith("ja")?"JP":"EN";
            if(t.name is "Title" or "Type" or "AdventureTitle" or "AdventureType")Check(t.font.sourceFontFile.name=="Name"+region,locale+" regional heading "+card+" "+t.name);
            if(t.name is "Rules" or "AdventureRules"||t.name.StartsWith("RuleRow"))Check(t.font.sourceFontFile.name=="Rules"+region,locale+" regional body "+card+" "+t.name);
            Check(t.font.fallbackFontAssetTable.Contains(GamePreferences.TmpFont),locale+" current locale fallback "+t.name);
            for(int line=1;line<t.textInfo.lineCount;line++){
                var previous=t.textInfo.characterInfo.Take(t.textInfo.characterCount).Where(c=>c.lineNumber==line-1&&c.isVisible).ToArray();
                var current=t.textInfo.characterInfo.Take(t.textInfo.characterCount).Where(c=>c.lineNumber==line&&c.isVisible).ToArray();
                if(previous.Length>0&&current.Length>0)Check(current.Max(c=>c.topRight.y)<=previous.Min(c=>c.bottomLeft.y)+.5f,locale+" visible glyph lines separated "+card+" "+t.name+" "+line);
            }
            result.labels.Add(new Observation{locale=locale,card=card,slot=t.name,font=t.font.sourceFontFile.name,text=t.text,size=t.fontSize,lines=t.textInfo.lineCount,fits=!t.isTextOverflowing});
        }
        foreach(var cost in face.GetComponentsInChildren<TemplateManaCost>())Check(cost.Complete,locale+" independent mana icons "+card+" "+cost.name);
    }
    static IEnumerator Validate()
    {
        while(!GamePreferences.Ready)yield return null;
        var catalog=ForgeCardCatalog.Load(ForgeCardCatalog.Root,new List<string>(),System.Threading.CancellationToken.None,null,false);
        var cards=Names.Select(name=>catalog.First(c=>c.cardName==name&&File.Exists("CardImages/"+c.PrintingKey+".jpg"))).ToArray();
        var arts=new List<Sprite>();
        foreach(var card in cards){var texture=new Texture2D(2,2,TextureFormat.RGBA32,true){filterMode=FilterMode.Trilinear};Check(texture.LoadImage(File.ReadAllBytes("CardImages/"+card.PrintingKey+".jpg")),"Exact printing art "+card.PrintingKey);arts.Add(Sprite.Create(texture,new Rect(0,0,texture.width,texture.height),Vector2.one*.5f));}
        shelf=new GameObject("OpenFontPreview",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler));
        var canvas=shelf.GetComponent<Canvas>();var scaler=shelf.GetComponent<CanvasScaler>();
        previewCamera=new GameObject("PreviewCamera",typeof(Camera));var camera=previewCamera.GetComponent<Camera>();
        var render=new RenderTexture(1840,1280,24){antiAliasing=4};camera.targetTexture=render;camera.orthographic=true;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.035f,.045f,.055f);camera.cullingMask=1<<30;
        canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1;scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1840,1280);
        var faces=new List<TemplateCardFace>();
        for(int i=0;i<cards.Length;i++){
            var host=new GameObject("Case"+i,typeof(RectTransform)).GetComponent<RectTransform>();host.SetParent(shelf.transform,false);host.sizeDelta=new Vector2(400,559);host.anchoredPosition=new Vector2((i%4-1.5f)*445,i<4?300:-306);
            faces.Add(host.gameObject.AddComponent<TemplateCardFace>());
        }
        foreach(string locale in new[]{"zh-CN","ja-JP","en-US","zh-CN"}){
            GamePreferences.SetLanguage(locale);yield return Frame();float deadline=Time.realtimeSinceStartup+30;while((!GamePreferences.Ready||GamePreferences.Language!=locale)&&Time.realtimeSinceStartup<deadline)yield return null;
            Check(GamePreferences.Ready&&GamePreferences.Language==locale,locale+" ready");
            for(int i=0;i<cards.Length;i++)Bind(faces[i],cards[i],arts[i]);
            foreach(var t in shelf.GetComponentsInChildren<Transform>(true))t.gameObject.layer=30;
            yield return Frame();
            Capture(render,"preview-"+locale);
            for(int i=0;i<cards.Length;i++)Audit(faces[i],locale,cards[i].cardName);
            var sizes=faces.SelectMany(f=>f.GetComponentsInChildren<TextMeshProUGUI>()).ToDictionary(t=>t,t=>t.fontSize);
            GamePreferences.SetScale(1.3f);GamePreferences.BindScene();yield return Frame();Check(sizes.All(p=>Mathf.Approximately(p.Key.fontSize,p.Value)),locale+" preference scan preserves paper sizes");GamePreferences.SetScale(1);yield return Frame();
            Capture(render,"preview-"+locale);
        }
        camera.targetTexture=null;render.Release();UnityEngine.Object.Destroy(render);
        foreach(var art in arts){UnityEngine.Object.Destroy(art.texture);UnityEngine.Object.Destroy(art);}
    }
    static void Capture(RenderTexture render,string name)
    {
        var prior=RenderTexture.active;RenderTexture.active=render;var image=new Texture2D(render.width,render.height,TextureFormat.RGBA32,false);
        try{image.ReadPixels(new Rect(0,0,render.width,render.height),0,0);image.Apply();File.WriteAllBytes(Output+name+".png",image.EncodeToPNG());}
        finally{RenderTexture.active=prior;UnityEngine.Object.Destroy(image);}
    }
}
#endif
