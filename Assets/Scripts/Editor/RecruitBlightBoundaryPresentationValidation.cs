#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public static class RecruitBlightBoundaryPresentationValidation
{
    const string Base=".tools/recruit-blight-boundaries-1009";
    [Serializable] class Meta {public string status,directory,runtimeHash;}
    public static bool Running {get;private set;}
    public static string Output {get;private set;}
    static Transform Root=>GameObject.Find("Canvas").transform;
    static void Check(bool value,string label){if(!value)throw new Exception(label);File.AppendAllText(Output+"/checks.txt",label+"\n");}
    static string Hash(string p){using var h=SHA256.Create();return BitConverter.ToString(h.ComputeHash(File.ReadAllBytes(p))).Replace("-","").ToLowerInvariant();}
    static IEnumerator Frame(){yield return new WaitForSecondsRealtime(.25f);yield return new WaitForEndOfFrame();Canvas.ForceUpdateCanvases();foreach(var p in UnityEngine.Object.FindObjectsByType<ForgeBattlefieldCardPointer>(FindObjectsSortMode.None)){p.ResetHover();p.enabled=false;}}
    static void Click(Button button){Check(button!=null&&button.interactable,"Actual enabled action "+button?.name);var r=(RectTransform)button.transform;foreach(float x in new[]{.5f,.15f,.85f})foreach(float y in new[]{.5f,.25f,.75f}){var p=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(null,r.TransformPoint(new Vector2(Mathf.Lerp(r.rect.xMin,r.rect.xMax,x),Mathf.Lerp(r.rect.yMin,r.rect.yMax,y)))),button=PointerEventData.InputButton.Left};var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(p,hits);if(hits.Count>0&&hits[0].gameObject.GetComponentInParent<Button>()==button){ExecuteEvents.ExecuteHierarchy(hits[0].gameObject,p,ExecuteEvents.pointerClickHandler);return;}}throw new Exception("Ray blocked "+button.name);}
    static Button Action(ForgeBattlefieldController ui,string id){int index=ui.OptionIndex(id);return Root.GetComponentsInChildren<Button>().Single(b=>b.name=="SelectionAction_"+index||b.name=="Option_"+index||b.name=="HandAction_"+index);}
    static ForgeSeatView Read(string folder,string label,int seat){string p=folder+"/"+label+"-seat"+seat+".json";File.Copy(p,Output+"/inputs/"+label+"-seat"+seat+".json",true);File.AppendAllText(Output+"/input-hashes.tsv",p+"\t"+Hash(p)+"\n");return JsonUtility.FromJson<ForgeSeatView>(File.ReadAllText(p));}
    public static void Run(){if(!Application.isPlaying||Running)throw new Exception("Play required/already running");Running=true;Output=Base+"/ui/"+DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffffffZ");Directory.CreateDirectory(Output+"/inputs");File.WriteAllText(Base+"/ui-latest.txt",Output);File.WriteAllText(Output+"/result.txt","RUNNING");foreach(string p in new[]{"Assets/Scripts/Editor/RecruitBlightBoundaryPresentationValidation.cs","Assets/Scripts/UI/ForgeBattlefieldController.CardSelection.cs"})File.AppendAllText(Output+"/sources.tsv",p+"\t"+Hash(p)+"\n");var ui=UnityEngine.Object.FindFirstObjectByType<ForgeBattlefieldController>()??new GameObject("RecruitBlightValidation").AddComponent<ForgeBattlefieldController>();ui.StartCoroutine(Guard(ui));}
    static IEnumerator Guard(ForgeBattlefieldController ui){var it=Validate(ui);try{while(true){object next;try{if(!it.MoveNext())break;next=it.Current;}catch(Exception e){File.WriteAllText(Output+"/result.txt","FAIL "+e);throw;}yield return next;}File.WriteAllText(Output+"/result.txt","PASS_BOUNDED: actual Blight cost picker three-language localized instruction, source/owned IDs, selection/native ok, cancel button callback, guards, fixed fonts/View Return/maxscale; main-language targeted and Defined opponent choices exact actor2/card IDs and native confirm. Native outcomes separate;unchanged Recruit/private/replacement contracts reused.");}finally{(it as IDisposable)?.Dispose();Running=false;}}
    static IEnumerator Validate(ForgeBattlefieldController ui)
    {
        string locale=GamePreferences.Language;float scale=GamePreferences.TextScale;bool enabled=ui.enabled;ui.enabled=false;var module=EventSystem.current.currentInputModule;bool input=module!=null&&module.enabled;if(module!=null)module.enabled=false;
        var notices=UnityEngine.Object.FindObjectsByType<ForgeBattlefieldMigrationNotice>(FindObjectsSortMode.None);var flags=notices.Select(n=>n.enabled).ToArray();foreach(var n in notices)n.enabled=false;
        try{
            var meta=JsonUtility.FromJson<Meta>(File.ReadAllText(Base+"/blight-latest.json"));Check(meta.status=="PASS"&&meta.runtimeHash==JsonUtility.FromJson<Meta>(File.ReadAllText("ForgeRuntime/runtime.json")).runtimeHash,"Exact actual native Blight producer/runtime");
            foreach(string language in new[]{"zh-CN","en-US","ja-JP"}){
                GamePreferences.SetLanguage(language);float end=Time.realtimeSinceStartup+20;while(!GamePreferences.Ready&&Time.realtimeSinceStartup<end)yield return null;Check(GamePreferences.Ready&&GamePreferences.Language==language,"Actual locale "+language);
                foreach(int choice in language=="zh-CN"?new[]{1,2,3}:new[]{1}){
                    string label="choice-"+choice;int actor=choice==1?1:2;string selected=choice==1?"selected-16":choice==2?"selected-26":"selected-42";var v=Read(meta.directory,label,actor);var s=Read(meta.directory,selected,actor);int calls=0,last=-1;
                    ui.Present(null,actor,false,null,null,"");ui.Present(v,actor,true,i=>{calls++;last=i;},null,"");yield return Frame();string tag=language+" "+label;
                    Check(v.request.seat==actor,tag+" correct actual decision seat");Check(ui.OptionIndex("ok")<0,tag+" cannot confirm before actual selection");
                    var heading=GameObject.Find("CardSelection/Instruction").GetComponent<Text>();string expected=choice!=1?"选择一张牌。":language=="zh-CN"?"选择一个由你操控的生物，在其上放置 2 个 -1/-1 指示物以支付费用。":language=="ja-JP"?"コストとして、あなたがコントロールするクリーチャー１体を選び、その上に-1/-1カウンターを2個置く。":"Choose a creature you control to put 2 -1/-1 counter(s) on as a cost.";
                    Check(heading.text==expected,tag+" exact native-cost/choice instruction");Check(!heading.resizeTextForBestFit&&heading.preferredHeight<=heading.rectTransform.rect.height+1,tag+" complete instruction fixed font");float font=heading.fontSize;
                    ui.ViewBattlefield();yield return Frame();Check(!ui.TrySubmitPresentedOption(0,v.request.requestId),tag+" hidden window guard");ui.RestoreBattlefieldWindows();yield return Frame();Check(heading.fontSize==font,tag+" View Return font stable");
                    var chosen=s.request.options.Single(o=>o.selected&&o.card!=null);Check(chosen.card.ownerSeat==actor,tag+" exact owner corresponds to chooser");int index=ui.OptionIndex(chosen.id);Check(index>=0&&!ui.TrySubmitPresentedOption(index,v.request.requestId-1),tag+" exact candidate and stale guard");
                    Click(GameObject.Find("ChoiceCard_"+chosen.card.id+"/Artwork").GetComponent<Button>());Check(calls==1&&last==index&&!ui.TrySubmitPresentedOption(index,v.request.requestId),tag+" native card index and duplicate guard");
                    ui.Present(s,actor,true,i=>{calls++;last=i;},null,"");yield return Frame();int ok=ui.OptionIndex("ok");Check(ok>=0,tag+" native selected confirm supplied");Click(Action(ui,"ok"));Check(calls==2&&last==ok,tag+" original native ok confirmed");
                    ui.Present(v,actor,false,i=>calls++,null,"");yield return Frame();Check(!ui.TrySubmitPresentedOption(index,v.request.requestId)&&calls==2,tag+" readonly context rejects submit");
                    if(choice==1){ui.Present(v,actor,true,i=>{calls++;last=i;},null,"");yield return Frame();int cancel=ui.OptionIndex("cancel");Click(Action(ui,"cancel"));Check(calls==3&&last==cancel,tag+" actual cancel UI callback only;native rollback reused");}
                    ui.Present(Read(meta.directory,choice==3?"finished":"targeted-resolved",actor),actor,false,null,null,"");yield return Frame();Check(!Root.GetComponentsInChildren<Transform>().Any(t=>t.name=="ChoiceCard_"+chosen.card.id),tag+" next priority removes picker");
                }
            }
            GamePreferences.SetLanguage("zh-CN");float ready=Time.realtimeSinceStartup+20;while(!GamePreferences.Ready&&Time.realtimeSinceStartup<ready)yield return null;GamePreferences.SetScale(1.3f);var max=Read(meta.directory,"choice-1",1);ui.Present(max,1,true,null,null,"");yield return Frame();var text=GameObject.Find("CardSelection/Instruction").GetComponent<Text>();Check(!text.resizeTextForBestFit&&text.preferredHeight<=text.rectTransform.rect.height+1,"Maxscale actual cost instruction fits fixed size");
            var method=typeof(ForgeBattlefieldController).GetMethod("ResolutionChoiceInstruction",BindingFlags.Instance|BindingFlags.NonPublic);string unknown="Put 2 -1/-1 counter on a creature you control with a different condition";Check((string)method.Invoke(ui,new object[]{unknown})==unknown,"Unknown extended native instruction keeps full fallback");
        }finally{ui.Present(null,1,false,null,null,"");ui.enabled=enabled;GamePreferences.SetScale(scale);GamePreferences.SetLanguage(locale);if(module!=null)module.enabled=input;for(int i=0;i<notices.Length;i++)if(notices[i]!=null)notices[i].enabled=flags[i];}
    }
}
#endif
