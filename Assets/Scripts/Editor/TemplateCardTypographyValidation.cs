#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// A focused visual check. Reuses the two-card native fixtures without replaying effects.
public static class TemplateCardTypographyValidation
{
    static string Output;
    [Serializable] class Pointer { public string folder; }
    static string native;
    static int checks;
    static ForgeSeatView Read(string name)=>JsonUtility.FromJson<ForgeSeatView>(File.ReadAllText(native+"/"+name+".json"));
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);checks++;File.AppendAllText(Output+"checks.log",message+" PASS\n");}
    static IEnumerator Frame(){yield return new WaitForSecondsRealtime(.4f);Canvas.ForceUpdateCanvases();yield return new WaitForEndOfFrame();}
    static IEnumerator Shot(string name){yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(Output+name+".png");yield return Frame();}
    public static void Run(string output=".tools/template-type-1008/")
    {
        Output=output;
        ComponentCardFaceDemo.Stop();Directory.CreateDirectory(Output);File.WriteAllText(Output+"checks.log","");File.WriteAllText(Output+"result.txt","RUNNING");checks=0;
        native=JsonUtility.FromJson<Pointer>(File.ReadAllText(".tools/component-card-1008/native-result.json")).folder;
        var ui=UnityEngine.Object.FindFirstObjectByType<ForgeBattlefieldController>()??new GameObject("TypographyValidation").AddComponent<ForgeBattlefieldController>();
        ui.StartCoroutine(Guard());
    }
    static IEnumerator Guard()
    {
        var notice=UnityEngine.Object.FindFirstObjectByType<ForgeBattlefieldMigrationNotice>();bool prior=notice!=null&&notice.enabled;if(notice!=null)notice.enabled=false;
        var work=Validate();
        try{while(true){object next;try{if(!work.MoveNext())break;next=work.Current;}catch(Exception e){File.WriteAllText(Output+"result.txt","FAIL "+e);throw;}yield return next;}File.WriteAllText(Output+"result.txt","PASS "+checks+" focused typography checks; no effect or network claim.");}
        finally{GamePreferences.SetLanguage("zh-CN");var shelf=GameObject.Find("TypographyComparison");if(shelf!=null)UnityEngine.Object.Destroy(shelf);if(notice!=null)notice.enabled=prior;}
    }
    static IEnumerator Validate()
    {
        var ui=UnityEngine.Object.FindFirstObjectByType<ForgeBattlefieldController>();
        var present=typeof(ForgeBattlefieldController).GetMethod("PresentComponentFace",BindingFlags.Instance|BindingFlags.NonPublic);
        var before=Read("before-hand");var stacked=Read("stack-giant");
        var cards=new[]{before.players[0].hand.First(c=>c.name=="Bonecrusher Giant"),before.players[0].hand.First(c=>c.name=="Elvish Visionary")};
        GamePreferences.SetLanguage("zh-CN");yield return Frame();ui.Present(null,1,false,null,null,"");yield return Frame();ui.Present(stacked,1,false,null,null,"");yield return Frame();
        GameObject.Find("InspectPanel")?.SetActive(false);
        var shelf=new GameObject("TypographyComparison",typeof(RectTransform),typeof(Image));shelf.transform.SetParent(GameObject.Find("Canvas").transform,false);
        var sr=(RectTransform)shelf.transform;sr.sizeDelta=new Vector2(1280,500);shelf.GetComponent<Image>().color=new Color(.025f,.04f,.05f,.99f);
        for(int i=0;i<4;i++){
            var host=new GameObject("Case"+i,typeof(RectTransform),typeof(Image)).GetComponent<RectTransform>();host.SetParent(sr,false);host.sizeDelta=new Vector2(290,405);host.anchoredPosition=new Vector2((i-1.5f)*308,0);
            if(i%2==1)present.Invoke(ui,new object[]{host,cards[i/2],false,false});
        }
        foreach(string language in new[]{"zh-CN","en-US","ja-JP"}){
            GamePreferences.SetLanguage(language);yield return Frame();float end=Time.realtimeSinceStartup+15;while(!GamePreferences.Ready&&Time.realtimeSinceStartup<end)yield return null;
            Check(GamePreferences.Ready&&GamePreferences.Language==language,language+" ready");
            for(int i=0;i<2;i++){
                var host=sr.Find("Case"+(i*2+1)) as RectTransform;Check((bool)present.Invoke(ui,new object[]{host,cards[i],false,false}),language+" template available "+cards[i].name);
                var face=host.GetComponentInChildren<TemplateCardFace>(true);Check(face!=null&&face.Illustration!=null,language+" face initialized "+cards[i].name);var original=sr.Find("Case"+(i*2)).GetComponent<Image>();
                var texture=(Texture2D)face.Illustration.texture;original.sprite=Sprite.Create(texture,new Rect(0,0,texture.width,texture.height),new Vector2(.5f,.5f));original.preserveAspect=true;
            }
            yield return Frame();
            Check(shelf.activeInHierarchy,language+" comparison visible");
            foreach(var face in shelf.GetComponentsInChildren<TemplateCardFace>()){
                foreach(var text in face.GetComponentsInChildren<TextMeshProUGUI>()){
                    if(!text.enabled||string.IsNullOrEmpty(text.text))continue;text.ForceMeshUpdate();
                    Check(!text.enableAutoSizing,language+" fixed font "+face.Title.text+" "+text.name);
                    Check(!text.isTextOverflowing,language+" fits "+face.Title.text+" "+text.name);
                    Check(text.textInfo.characterCount>0,language+" glyphs "+text.name);
                    if(text.name is "Title" or "Type")Check(text.fontStyle==FontStyles.Normal,language+" native heading weight without faux bold "+text.name);
                    if(text.name is "AdventureTitle" or "AdventureType")Check(text.fontStyle==FontStyles.Normal,language+" regular white heading "+text.name);
                    if(text.name=="Stats")Check(text.font.sourceFontFile.name=="StatsEN"&&text.fontStyle==FontStyles.Normal,language+" battlefield bold serif numerals without faux bold");
                    Check(text.color==(text.name is "AdventureTitle" or "AdventureType"?Color.white:Color.black),language+" printed ink "+text.name);
                    if(language=="zh-CN"&&text.name is "Rules" or "AdventureRules")for(int line=1;line<text.textInfo.lineCount;line++)
                        Check(text.textInfo.lineInfo[line].ascender<=text.textInfo.lineInfo[line-1].descender,language+" body lines have clear separation "+text.name+" "+line);
                }
                foreach(var cost in face.GetComponentsInChildren<TemplateManaCost>())foreach(var icon in cost.GetComponentsInChildren<RawImage>()){
                    Check(Mathf.Approximately(icon.rectTransform.rect.width,cost.name=="AdventureCost"?30:38),language+" printed fee diameter "+cost.name);
                    var corners=new Vector3[4];icon.rectTransform.GetWorldCorners(corners);float pixels=Vector2.Distance(RectTransformUtility.WorldToScreenPoint(null,corners[0]),RectTransformUtility.WorldToScreenPoint(null,corners[1]));
                    Check(pixels>=10,language+" comparison fee remains legible "+cost.name);
                }
                if(language=="zh-CN")Check(face.Rules.fontSize>=22,"readable Chinese body "+face.Title.text);
            }
            yield return Shot("comparison-"+language);
        }
        UnityEngine.Object.Destroy(shelf);GamePreferences.SetLanguage("zh-CN");yield return Frame();
        ui.Present(null,1,false,null,null,"");yield return Frame();ui.Present(stacked,1,false,null,null,"");yield return Frame();
        GameObject.Find("InspectPanel")?.SetActive(false);yield return Shot("stack");
    }
}
#endif
