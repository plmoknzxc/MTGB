#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public static class NoncreatureCopyPresentationValidation
{
    const string Output=".tools/noncreature-copies-1007/ui";
    static bool running;
    [Serializable] class Meta {public string folder,status;}
    static void Check(bool ok,string why){if(!ok)throw new Exception(why);}
    static IEnumerator Frame(){yield return new WaitForSecondsRealtime(.25f);yield return new WaitForEndOfFrame();Canvas.ForceUpdateCanvases();}
    static ForgeSeatView Read(string name,string label="resolved"){var r=JsonUtility.FromJson<Meta>(File.ReadAllText(".tools/noncreature-copies-1007/native/"+name+"-result.json"));Check(r.status=="PASS","Native result");return JsonUtility.FromJson<ForgeSeatView>(File.ReadAllText(r.folder+"/"+label+".json"));}
    static Sprite Art(ForgeBattlefieldController ui,ForgeCardView card){object[] args={card,null};bool ok=(bool)typeof(ForgeBattlefieldController).GetMethod("TryBattlefieldArt",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(ui,args);Check(ok&&args[1]!=null,"Missing art "+card.name+" "+card.id);return (Sprite)args[1];}
    static GameObject Card(int id)=>GameObject.Find("Card_"+id);
    static void Click(Button b){var r=(RectTransform)b.transform;var p=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(null,r.TransformPoint(r.rect.center)),button=PointerEventData.InputButton.Left};var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(p,hits);Check(b.interactable&&hits.Count>0&&hits[0].gameObject.GetComponentInParent<Button>()==b,"Blocked original-state ray");ExecuteEvents.ExecuteHierarchy(hits[0].gameObject,p,ExecuteEvents.pointerClickHandler);}
    public static void Run(){Check(Application.isPlaying&&!running,"Play/runner state");running=true;Directory.CreateDirectory(Output);File.WriteAllText(Output+"/result.txt","RUNNING");File.WriteAllText(Output+"/steps.log","");var ui=UnityEngine.Object.FindFirstObjectByType<ForgeBattlefieldController>()??new GameObject("NoncreatureCopyValidation").AddComponent<ForgeBattlefieldController>();ui.StartCoroutine(Guard(ui));}
    static IEnumerator Guard(ForgeBattlefieldController ui){var stack=new Stack<IEnumerator>();stack.Push(Validate(ui));try{while(stack.Count>0){object current;try{var e=stack.Peek();if(!e.MoveNext()){(e as IDisposable)?.Dispose();stack.Pop();continue;}current=e.Current;if(current is IEnumerator child){stack.Push(child);continue;}}catch(Exception error){File.WriteAllText(Output+"/result.txt","FAIL "+error);throw;}yield return current;}File.WriteAllText(Output+"/result.txt","PASS three-language actual artifact token/non-token copy classification, copied artwork texture and frame/size, copied ability tap-state refresh; native Manufactor six IDs with exact token art keys/textures, compact layout/order and quantity. Hidden clone art is adapter; R21 grouping, prior replacement input and language contracts reused. No extra UI first or network claim.");}finally{while(stack.Count>0)(stack.Pop() as IDisposable)?.Dispose();running=false;}}
    static IEnumerator Validate(ForgeBattlefieldController ui){bool enabled=ui.enabled;ui.enabled=false;string locale=GamePreferences.Language;var module=EventSystem.current.currentInputModule;bool input=module!=null&&module.enabled;if(module!=null)module.enabled=false;var notices=UnityEngine.Object.FindObjectsByType<ForgeBattlefieldMigrationNotice>(FindObjectsSortMode.None);var flags=notices.Select(n=>n.enabled).ToArray();foreach(var n in notices)n.enabled=false;
        try{foreach(string language in new[]{"zh-CN","en-US","ja-JP"}){
            GamePreferences.SetLanguage(language);float until=Time.realtimeSinceStartup+15;while(!GamePreferences.Ready&&Time.realtimeSinceStartup<until)yield return null;Check(GamePreferences.Ready,"Locale ready");
            foreach(string name in new[]{"echo-copy","steel-copy","steel-reverse-copy"}){
                var v=Read(name);ui.Present(null,1,false,null,null,"");ui.Present(v,1,false,null,null,"");yield return Frame();var copy=v.players[0].battlefield.Single(c=>c.copy);var original=v.players[0].battlefield.Single(c=>c.name==copy.name&&!c.copy);var expected=Art(ui,original);var actual=Art(ui,copy);
                ScreenCapture.CaptureScreenshot(Output+"/"+language+"-"+name+".png");yield return Frame();Check(actual.texture==expected.texture,"Copy uses original physical card art instead of copied object: "+name);
                var root=Card(copy.id);Check(root!=null,"Copy absent");var art=root.GetComponentsInChildren<RawImage>().Single(r=>r.name=="Illustration");Check(art.texture==expected.texture,"Rendered copy art mismatch");Check(root.GetComponentsInChildren<Mask>().Length>0,"Copy frame absent");Check(root.transform.Find("Visual/TemporaryCopyGlow")==null,"Permanent copy has expiry frame");
                var size=((RectTransform)root.transform).rect.size;var normal=((RectTransform)Card(original.id).transform).rect.size;Check(copy.token?size.x<normal.x:size.x>=normal.x*.99f,"Token/non-token compact classification");
                var after=Read(name,"activated");ui.Present(after,1,false,null,null,"");yield return Frame();Check(Card(copy.id).GetComponentsInChildren<ForgeTappedIndicator>().Length==1,"Copied ability update missing");Check(Art(ui,after.players[0].battlefield.Single(c=>c.id==copy.id)).texture==expected.texture,"Same-ID art refresh lost clone");
                var hidden=JsonUtility.FromJson<ForgeCardView>(JsonUtility.ToJson(copy));hidden.faceDown=true;Check(CardImageLibrary.TryLoadCardBack(out var back)&&Art(ui,hidden).texture==back.texture,"Hidden copy reveals art");
                // The ability-menu resource branch previously bypassed the common
                // loader. This is a display adapter using the actual copied card.
                var probe=new GameObject("CopyChoiceProbe",typeof(RectTransform),typeof(Image));probe.transform.SetParent(GameObject.Find("Canvas").transform,false);var probeRect=(RectTransform)probe.transform;probeRect.sizeDelta=new Vector2(240,330);typeof(ForgeBattlefieldController).GetMethod("DrawAbilityChoice",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(ui,new object[]{probeRect,copy,new ForgeOption{label="{T}: Activate copied ability."},new Vector2(240,330)});Check(probe.GetComponentsInChildren<RawImage>().Any(i=>i.texture==expected.texture),"Ability choice retained physical source art");probe.SetActive(false);UnityEngine.Object.Destroy(probe);
                if(name.StartsWith("steel",StringComparison.Ordinal)){
                    typeof(ForgeBattlefieldController).GetMethod("InspectIntegrated",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(ui,new object[]{after.players[0].battlefield.Single(c=>c.id==copy.id)});yield return Frame();var panel=GameObject.Find("Canvas").transform.Find("InspectPanel");var shown=panel.Find("Art").GetComponent<Image>();Check(shown.sprite.texture==expected.texture,"Current detail art mismatch");var toggle=panel.Find("OriginalState").GetComponent<Button>();Click(toggle);yield return Frame();Check(CardImageLibrary.TryLoadPrintingKey(copy.printingKey,out var physical)&&shown.sprite.texture==physical.texture,"Original toggle lost physical print");Click(toggle);yield return Frame();Check(shown.sprite.texture==expected.texture,"Return toggle lost clone art");panel.gameObject.SetActive(false);
                }
                File.AppendAllText(Output+"/steps.log",language+" "+name+" art/classification/refresh/hidden PASS\n");
            }
            var tokensView=Read("manufactor-regression");ui.Present(tokensView,1,false,null,null,"");yield return Frame();var tokens=tokensView.players[0].battlefield.Where(c=>c.token).ToArray();Check(tokens.Length==6,"Native token count");
            foreach(var group in tokens.GroupBy(c=>c.name)){
                var card=group.First();var expected=Art(ui,card);Check(CardImageLibrary.TryLoadPrintingKey(card.tokenArtKey,out var direct)&&direct.texture==expected.texture,"Token resource not exact native key "+card.name);var root=Card(card.id);Check(root!=null,"Token absent");Check(root.GetComponentsInChildren<RawImage>().Single(i=>i.name=="Illustration").texture==expected.texture,"Token fallback used wrong art");Check(root.GetComponentsInChildren<Text>().Any(t=>t.text=="×2"),"Native pair quantity missing");Check(!root.GetComponentsInChildren<Text>().Any(t=>t.text=="缺失卡图"),"Missing token art");
            }
            var source=tokensView.players[0].battlefield.Single(c=>c.name=="Doubling Season");Check(tokens.Where(c=>Card(c.id)!=null).All(c=>Card(c.id).transform.position.x<Card(source.id).transform.position.x),"Tokens not first in actual single support row");
            ScreenCapture.CaptureScreenshot(Output+"/"+language+"-manufactor.png");yield return Frame();File.AppendAllText(Output+"/steps.log",language+" Manufactor token keys/textures/quantity/order PASS\n");
        }}finally{ui.Present(null,1,false,null,null,"");ui.enabled=enabled;GamePreferences.SetLanguage(locale);if(module!=null)module.enabled=input;for(int i=0;i<notices.Length;i++)if(notices[i]!=null)notices[i].enabled=flags[i];}
    }
}
#endif
