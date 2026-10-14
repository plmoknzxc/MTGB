#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public static class TemplateCardReadabilityValidation
{
    const string Output=".tools/component-card-readability-1008/";
    static string native;
    static bool running;
    [Serializable] class Pointer { public string folder; }
    static ForgeSeatView Read(string name)=>JsonUtility.FromJson<ForgeSeatView>(File.ReadAllText(native+"/"+name+".json"));
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);File.AppendAllText(Output+"steps.log",message+" PASS\n");}
    static IEnumerator Frame(){yield return new WaitForSecondsRealtime(.5f);Canvas.ForceUpdateCanvases();yield return new WaitForEndOfFrame();}
    static IEnumerator Shot(string name){yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(Output+name+".png");yield return Frame();}
    public static void Run()
    {
        if(running)return;if(!Application.isPlaying)throw new Exception("Play mode required");
        running=true;ComponentCardFaceDemo.Stop();Directory.CreateDirectory(Output);File.WriteAllText(Output+"steps.log","");File.WriteAllText(Output+"result.txt","RUNNING");
        native=JsonUtility.FromJson<Pointer>(File.ReadAllText(".tools/component-card-1008/native-result.json")).folder;
        var ui=UnityEngine.Object.FindFirstObjectByType<ForgeBattlefieldController>()??new GameObject("ReadabilityValidation").AddComponent<ForgeBattlefieldController>();
        ui.StartCoroutine(Guard());
    }
    static IEnumerator Guard()
    {
        var notice=UnityEngine.Object.FindFirstObjectByType<ForgeBattlefieldMigrationNotice>();bool prior=notice!=null&&notice.enabled;if(notice!=null)notice.enabled=false;
        var work=Validate();
        try{while(true){object next;try{if(!work.MoveNext())break;next=work.Current;}catch(Exception e){File.WriteAllText(Output+"result.txt","FAIL "+e);throw;}yield return next;}File.WriteAllText(Output+"result.txt","PASS: focused two-card rendering checks; reused unchanged native snapshots, no new engine or network claim.");}
        finally{running=false;GamePreferences.SetLanguage("zh-CN");if(notice!=null)notice.enabled=prior;}
    }
    static void Face(TemplateCardFace face,string label)
    {
        Check(face!=null,label+" exists");
        foreach(var text in face.GetComponentsInChildren<TextMeshProUGUI>()) {
            if(!text.enabled||string.IsNullOrEmpty(text.text))continue;text.ForceMeshUpdate();
            Check(!text.isTextOverflowing,label+" text fits "+text.name);
            Check(text.textInfo.characterCount>0,label+" has glyphs "+text.name);
        }
        foreach(var cost in face.GetComponentsInChildren<TemplateManaCost>()) {
            Check(cost.Complete&&!cost.GetComponent<TextMeshProUGUI>().enabled,label+" image fee replaces font sprite sizing");
            foreach(var image in cost.GetComponentsInChildren<RawImage>()) {
                var t=(Texture2D)image.texture;
                Check(t.mipmapCount>1&&t.filterMode==FilterMode.Trilinear&&t.wrapMode==TextureWrapMode.Clamp,label+" mana icon has isolated smooth mip chain");
                Check(Mathf.Approximately(image.rectTransform.rect.width,image.rectTransform.rect.height),label+" mana icon is square");
            }
        }
    }
    static IEnumerator Validate()
    {
        GamePreferences.SetLanguage("zh-CN");yield return Frame();
        float readyBy=Time.realtimeSinceStartup+15;while(!GamePreferences.Ready&&Time.realtimeSinceStartup<readyBy)yield return null;
        Check(GamePreferences.Ready&&GamePreferences.Language=="zh-CN","Chinese display resources ready");
        var ui=UnityEngine.Object.FindFirstObjectByType<ForgeBattlefieldController>();
        var present=typeof(ForgeBattlefieldController).GetMethod("PresentComponentFace",BindingFlags.Instance|BindingFlags.NonPublic);
        var inspect=typeof(ForgeBattlefieldController).GetMethod("InspectIntegrated",BindingFlags.Instance|BindingFlags.NonPublic);
        var stacked=Read("stack-adventure");var before=Read("before-hand");
        ui.Present(null,1,false,null,null,"");yield return Frame();ui.Present(stacked,1,false,null,null,"");yield return Frame();
        var stomp=GameObject.Find("StackCard_0").GetComponentInChildren<TemplateCardFace>();Face(stomp,"actual Stomp stack");
        Check(stomp.Cost.text==CardSymbolText.Format("{2}{R}"),"actual adventure fee preserved");
        foreach(string name in new[]{"Elvish Visionary","Bonecrusher Giant"}) {
            var card=stacked.players[0].battlefield.First(c=>c.name==name);var host=GameObject.Find("Card_"+card.id);
            Check(host.GetComponentInChildren<TemplateCardFace>()==null,name+" battlefield has no active template");
            Check(host.GetComponentsInChildren<RawImage>().Any(i=>i.name=="PrintedName")&&host.GetComponentsInChildren<RawImage>().Any(i=>i.name=="Illustration"),name+" battlefield keeps old scan slices");
            inspect.Invoke(ui,new object[]{card});yield return Frame();var art=GameObject.Find("InspectPanel/Art").GetComponent<Image>();
            Check(art.sprite!=null&&art.color==Color.white&&art.GetComponentInChildren<TemplateCardFace>()==null,name+" hover uses original scan");
        }
        yield return Shot("hover-original");GameObject.Find("InspectPanel")?.SetActive(false);yield return Shot("battlefield-original-stack");
        ui.Present(null,1,false,null,null,"");yield return Frame();ui.Present(Read("stack-giant"),1,false,null,null,"");yield return Frame();
        Face(GameObject.Find("StackCard_0").GetComponentInChildren<TemplateCardFace>(),"actual Giant stack");yield return Shot("giant-stack");
        var canvas=GameObject.Find("Canvas").GetComponent<Canvas>();var shelf=new GameObject("ReadabilityComparison",typeof(RectTransform),typeof(Image));shelf.transform.SetParent(canvas.transform,false);
        var sr=(RectTransform)shelf.transform;sr.sizeDelta=new Vector2(1280,600);shelf.GetComponent<Image>().color=new Color(.025f,.04f,.05f,.99f);
        var cards=new[]{before.players[0].hand.First(c=>c.name=="Bonecrusher Giant"),stacked.stackItems.First(s=>s.source.name=="Stomp").source,Read("stack-giant").stackItems.First(s=>s.source.name=="Bonecrusher Giant").source,Read("stack-visionary").stackItems.First(s=>s.source.name=="Elvish Visionary").source};
        for(int i=0;i<cards.Length;i++) {
            var host=new GameObject("Case"+i,typeof(RectTransform),typeof(Image)).GetComponent<RectTransform>();host.SetParent(sr,false);host.sizeDelta=new Vector2(290,405);host.anchoredPosition=new Vector2((i-1.5f)*308,0);present.Invoke(ui,new object[]{host,cards[i],false,false});
        }
        yield return Frame();foreach(var f in shelf.GetComponentsInChildren<TemplateCardFace>())Face(f,"comparison "+f.Title.text);yield return Shot("faces-comparison");
        foreach(string language in new[]{"en-US","ja-JP","zh-CN"}) {
            GamePreferences.SetLanguage(language);yield return Frame();float end=Time.realtimeSinceStartup+15;while(!GamePreferences.Ready&&Time.realtimeSinceStartup<end)yield return null;
            Check(GamePreferences.Ready&&GamePreferences.Language==language,language+" ready");
            for(int i=0;i<cards.Length;i++)present.Invoke(ui,new object[]{sr.Find("Case"+i) as RectTransform,cards[i],false,false});yield return Frame();
            foreach(var f in shelf.GetComponentsInChildren<TemplateCardFace>())Face(f,language+" "+f.Title.text);
        }
        UnityEngine.Object.Destroy(shelf);yield return Frame();ui.Present(null,1,false,null,null,"");yield return Frame();ui.Present(stacked,1,false,null,null,"");yield return Frame();
    }
}
#endif
