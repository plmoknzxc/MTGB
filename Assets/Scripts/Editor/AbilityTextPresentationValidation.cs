#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

public static class AbilityTextPresentationValidation
{
    public static string Output {get;private set;}
    public static bool Running {get;private set;}
    const string Base=".tools/ability-text-1006";
    const string Native=".tools/number-auction-1006/native/hellion-large-result.json";
    [Serializable] sealed class Meta {public string folder,status,runtimeHash;}
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
    static string Hash(string p){using var h=SHA256.Create();return BitConverter.ToString(h.ComputeHash(File.ReadAllBytes(p))).Replace("-","").ToLowerInvariant();}
    static Transform Root=>GameObject.Find("Canvas").transform;
    static IEnumerator Frame(){yield return new WaitForSecondsRealtime(.25f);yield return new WaitForEndOfFrame();Canvas.ForceUpdateCanvases();}
    public static void Run()
    {
        Check(Application.isPlaying&&!Running,"Play required/already running");Running=true;
        Output=Base+"/"+DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffffffZ");Directory.CreateDirectory(Output+"/inputs");File.WriteAllText(Base+"/latest.txt",Output);File.WriteAllText(Output+"/result.txt","RUNNING");
        var meta=JsonUtility.FromJson<Meta>(File.ReadAllText(Native));Check(meta.status=="PASS"&&meta.runtimeHash==JsonUtility.FromJson<Meta>(File.ReadAllText("ForgeRuntime/runtime.json")).runtimeHash,"Native proof stale");
        foreach(string p in new[]{Native,meta.folder+"/number-final.json",meta.folder+"/resolved.json"}){File.Copy(p,Output+"/inputs/"+Path.GetFileName(p),true);File.AppendAllText(Output+"/input-hashes.tsv",p+"\t"+Hash(p)+"\n");}
        File.WriteAllText(Output+"/producer-hashes.tsv",string.Join("\n",new[]{"Assets/Scripts/Editor/AbilityTextPresentationValidation.cs","Assets/Scripts/UI/ForgeBattlefieldController.AbilityFace.cs","Assets/Scripts/UI/ForgeBattlefieldController.Stack.cs","Assets/Scripts/UI/ForgeBattlefieldController.StackList.cs","Assets/Scripts/UI/CardSymbolText.cs"}.Select(p=>p+"\t"+Hash(p)))+"\n");
        var go=new GameObject("AbilityTextValidation");go.AddComponent<Host>().StartCoroutine(Guard(Exercise(),go));
    }
    sealed class Host:MonoBehaviour{}
    static IEnumerator Guard(IEnumerator e,GameObject go)
    {
        Exception error=null;while(true){bool more=false;object current=null;try{more=e.MoveNext();if(more)current=e.Current;}catch(Exception ex){error=ex;break;}if(!more)break;if(current is IEnumerator nested){yield return Nested(nested,ex=>error=ex);if(error!=null)break;}else yield return current;}
        try{(e as IDisposable)?.Dispose();}catch(Exception ex){error??=ex;}
        File.WriteAllText(Output+"/result.txt",error==null?"PASS: real Volcano Hellion trigger input translated by exact paragraph with actual target retained; three locales, raycast scrolling reaches final text, scrollbar/viewport, unchanged-render and view/return font/scroll stability, hover rules use same text; changed/unknown rule adapter remains native. No new rule or live-network proof.":"FAIL\n"+error);Running=false;UnityEngine.Object.Destroy(go);
    }
    static IEnumerator Nested(IEnumerator e,Action<Exception> fail){while(true){bool more=false;object current=null;try{more=e.MoveNext();if(more)current=e.Current;}catch(Exception ex){fail(ex);break;}if(!more)break;if(current is IEnumerator child)yield return Nested(child,fail);else yield return current;}}
    static IEnumerator Exercise()
    {
        var ui=UnityEngine.Object.FindFirstObjectByType<ForgeBattlefieldController>()??new GameObject("AbilityTextBattlefield").AddComponent<ForgeBattlefieldController>();bool enabled=ui.enabled;ui.enabled=false;
        var module=EventSystem.current.currentInputModule;bool active=module!=null&&module.enabled;if(module!=null)module.enabled=false;string locale=GamePreferences.Language;
        var flags=UnityEngine.Object.FindObjectsByType<ForgeBattlefieldMigrationNotice>(FindObjectsSortMode.None).Select(n=>(n,n.enabled)).ToArray();foreach(var f in flags)f.n.enabled=false;
        try{foreach(string lang in new[]{"zh-CN","en-US","ja-JP"}){
            int revision=GamePreferences.Revision;bool change=locale!=lang;GamePreferences.SetLanguage(lang);float until=Time.realtimeSinceStartup+20;while((!GamePreferences.Ready||change&&revision==GamePreferences.Revision)&&Time.realtimeSinceStartup<until)yield return null;Check(GamePreferences.Ready,"Locale unavailable");yield return Frame();
            var view=JsonUtility.FromJson<ForgeSeatView>(File.ReadAllText(Output+"/inputs/number-final.json"));var entry=view.stackItems.Single();int submits=0;
            ui.Present(null,1,false,null,null,"");ui.Present(view,1,true,i=>submits++,null,"");yield return Frame();
            foreach(var pointer in UnityEngine.Object.FindObjectsByType<ForgeBattlefieldCardPointer>(FindObjectsSortMode.None)){pointer.ResetHover();pointer.enabled=false;}
            var face=Root.Find("StackColumn/Viewport/Content/StackEntry_0/StackCard_0/AbilityFace");
            if(face==null)face=Root.GetComponentsInChildren<Transform>().First(t=>t.name=="AbilityFace"&&t.parent.name=="StackCard_0");
            var effect=face.Find("EffectClip/Effect").GetComponent<Text>();var symbols=effect.GetComponentInChildren<TextMeshProUGUI>();
            var catalog=(System.Collections.Generic.Dictionary<string,CardDefinition>)typeof(ForgeBattlefieldController).GetField("catalog",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(ui);
            string expected=GamePreferences.CardRules(catalog[entry.source.printingKey]).Split('\n').Last();Check(effect.text.StartsWith(expected,StringComparison.Ordinal),"Printed effect localization missing "+lang);
            Check(effect.text.Contains(GamePreferences.CardFaceName("Grizzly Bears"))&&effect.text.Contains("183")&&!effect.text.Contains("Zone Changer")&&!effect.text.Contains("Targeting:"),"Target identity/context");
            Check(!effect.resizeTextForBestFit&&!symbols.enableAutoSizing&&symbols.text==CardSymbolText.Format(effect.text),"Font/symbol source mismatch");
            float font=symbols.fontSize;var scroll=face.Find("EffectClip").GetComponent<ScrollRect>();var track=face.Find("EffectScrollbar");float excess=scroll.content.rect.height-scroll.viewport.rect.height;
            File.AppendAllText(Output+"/steps.log",$"{lang} font={font} content={scroll.content.rect.height:R} viewport={scroll.viewport.rect.height:R} track={track.gameObject.activeSelf}\n");
            Check(track.gameObject.activeSelf==(excess>.1f),"Scrollbar gate");
            if(lang=="zh-CN"){ScreenCapture.CaptureScreenshot(Output+"/trigger-top.png");yield return Frame();}
            if(excess>.1f){
                var rect=scroll.viewport;var point=RectTransformUtility.WorldToScreenPoint(null,rect.TransformPoint(rect.rect.center));var p=new PointerEventData(EventSystem.current){position=point,scrollDelta=new Vector2(0,-50)};var hits=new System.Collections.Generic.List<RaycastResult>();EventSystem.current.RaycastAll(p,hits);Check(hits.Count>0&&hits[0].gameObject.GetComponentInParent<ScrollRect>()==scroll,"Ability scrolling blocked");ExecuteEvents.ExecuteHierarchy(hits[0].gameObject,p,ExecuteEvents.scrollHandler);yield return Frame();
                Check(scroll.verticalNormalizedPosition<.01f,"Cannot reach last effect line");symbols.ForceMeshUpdate();var last=symbols.textInfo.characterInfo[symbols.textInfo.characterCount-1];var local=rect.InverseTransformPoint(symbols.rectTransform.TransformPoint(last.bottomLeft));Check(local.y>=rect.rect.yMin-2,"Last text line clipped");
            }
            Vector2 position=scroll.content.anchoredPosition;ui.Present(view,1,true,i=>submits++,null,"");yield return Frame();Check((position-scroll.content.anchoredPosition).sqrMagnitude<1,"Repeated render resets scroll");
            ui.ViewBattlefield();yield return Frame();ui.RestoreBattlefieldWindows();yield return Frame();Check(Mathf.Abs(symbols.fontSize-font)<.01f&&(position-scroll.content.anchoredPosition).sqrMagnitude<1&&submits==0,"View return/font/selection drift");
            var cardPointer=face.parent.GetComponent<ForgeBattlefieldCardPointer>();cardPointer.enabled=true;ExecuteEvents.Execute(cardPointer.gameObject,new PointerEventData(EventSystem.current),ExecuteEvents.pointerEnterHandler);yield return Frame();
            Check(Root.Find("InspectPanel/Rules").GetComponent<Text>().text==effect.text,"Hover and face rule mismatch");cardPointer.ResetHover();cardPointer.enabled=false;
            if(lang=="zh-CN"){ScreenCapture.CaptureScreenshot(Output+"/trigger-bottom.png");yield return Frame();}
            // Deliberately changed description is a renderer adapter only. It
            // must never be substituted with unchanged printed card rules.
            string changed=entry.description.Replace("an amount of damage of your choice","123 damage");var method=typeof(ForgeBattlefieldController).GetMethod("AbilityEffectText",BindingFlags.Instance|BindingFlags.NonPublic);string result=(string)method.Invoke(ui,new object[]{entry.source,changed});Check(result.Contains("123 damage")&&!result.StartsWith(expected,StringComparison.Ordinal)||lang=="en-US"&&result.Contains("123 damage"),"Changed rule silently replaced");
            ui.Present(JsonUtility.FromJson<ForgeSeatView>(File.ReadAllText(Output+"/inputs/resolved.json")),1,false,null,null,"");yield return Frame();Check(face==null||!face.gameObject.activeInHierarchy,"Resolved ability remains");File.AppendAllText(Output+"/steps.log","PASS actual trigger input / scroll / hover / language / unknown-rule adapter "+lang+"\n");
        }}finally{ui.Present(null,1,false,null,null,"");ui.enabled=enabled;GamePreferences.SetLanguage(locale);if(module!=null)module.enabled=active;foreach(var f in flags)if(f.n!=null)f.n.enabled=f.enabled;}
    }
}
#endif
