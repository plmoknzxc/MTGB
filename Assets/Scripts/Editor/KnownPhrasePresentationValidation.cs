#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
public static class KnownPhrasePresentationValidation
{
 const string Base=".tools/known-phrases-1009";
 public static bool Running{get;private set;}
 public static string Output{get;private set;}
 static int count;
 static bool reuseZhFaces;
 static bool visualOnly;
 public static void RunVisual(){visualOnly=true;Run();}
 static HashSet<string> previousChecks=new();
 public static void ResumeAfterZhFaces(){reuseZhFaces=true;Run();}
 public static void ResumeKnown(){reuseZhFaces=true;previousChecks=new HashSet<string>(File.ReadAllLines(Base+"/ui/20261009T0255534353515Z/checks.txt"));Run();}
 static Transform Root=>GameObject.Find("Canvas").transform;
 static Transform Inspect=>Root.Find("InspectPanel");
 static ForgeSeatView Read(string name)=>JsonUtility.FromJson<ForgeSeatView>(File.ReadAllText(Base+"/inputs/"+name+".json"));
 static void Check(bool v,string why){if(!v)throw new Exception(why);count++;File.AppendAllText(Output+"/checks.txt",why+"\n");}
 static object Call(ForgeBattlefieldController ui,string name,params object[] args)=>typeof(ForgeBattlefieldController).GetMethod(name,BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static).Invoke(ui,args);
 static string Text(string name)=>Inspect.Find(name).GetComponent<Text>().text;
 static void Capture(string name)=>ScreenCapture.CaptureScreenshot(Output+"/"+name+".png");
 static IEnumerator Frame(){yield return new WaitForSecondsRealtime(.15f);yield return new WaitForEndOfFrame();Canvas.ForceUpdateCanvases();foreach(var p in UnityEngine.Object.FindObjectsByType<ForgeBattlefieldCardPointer>(FindObjectsSortMode.None)){p.ResetHover();p.enabled=false;}}
 static void Click(GameObject go){var r=(RectTransform)go.transform;var e=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(null,r.TransformPoint(r.rect.center)),button=PointerEventData.InputButton.Left};var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(e,hits);Check(hits.Count>0&&hits[0].gameObject.transform.IsChildOf(go.transform),"Exact ray "+go.name);ExecuteEvents.ExecuteHierarchy(hits[0].gameObject,e,ExecuteEvents.pointerClickHandler);}
 public static void Run(){if(Running||!Application.isPlaying)throw new Exception("Play required");Running=true;count=0;Output=Base+"/ui/"+DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffffffZ");Directory.CreateDirectory(Output);File.WriteAllText(Base+"/ui-current.txt",Output);File.WriteAllText(Output+"/result.txt","RUNNING");var ui=UnityEngine.Object.FindFirstObjectByType<ForgeBattlefieldController>();if(ui==null)ui=new GameObject("KnownPhraseValidation").AddComponent<ForgeBattlefieldController>();ui.StartCoroutine(Guard(visualOnly?Visual(ui):Validate(ui)));}
 static IEnumerator Guard(IEnumerator work){var tasks=new Stack<IEnumerator>();tasks.Push(work);try{while(tasks.Count>0){object next;try{var w=tasks.Peek();if(!w.MoveNext()){tasks.Pop();continue;}next=w.Current;}catch(Exception e){File.WriteAllText(Output+"/result.txt","FAIL "+e);throw;}if(next is IEnumerator c)tasks.Push(c);else yield return next;}File.WriteAllText(Output+"/result.txt",(visualOnly?"PASS_VISUAL_BOUNDED: ":"PASS_BOUNDED: ")+count+" checks;three locales exact known face-down/private original and public anonymity/art, bending/Unattach actual chooser rays and permissions, source-matched chapter/body/target/wrapper formatting and rendered full stack text, unknown fallback. Native historical outcomes retained;no new native/network/full-catalog PASS.");}finally{while(tasks.Count>0)(tasks.Pop() as IDisposable)?.Dispose();Running=false;}}
 static IEnumerator Visual(ForgeBattlefieldController ui){string locale=GamePreferences.Language;bool enabled=ui.enabled;ui.enabled=false;var module=EventSystem.current.currentInputModule;bool input=module!=null&&module.enabled;if(module!=null)module.enabled=false;
  try{foreach(string language in new[]{"zh-CN","en-US","ja-JP"}){
   GamePreferences.SetLanguage(language);float end=Time.realtimeSinceStartup+15;while((!GamePreferences.Ready||GamePreferences.Language!=language)&&Time.realtimeSinceStartup<end)yield return null;Check(GamePreferences.Ready&&GamePreferences.Language==language,"Visual actual locale "+language);
   var v=Read("chapter-3");ui.Present(null,1,false,null,null,"");ui.Present(v,1,false,null,null,"");yield return Frame();Inspect.gameObject.SetActive(false);
   var item=v.stackItems.First(s=>s.description.StartsWith("You may sacrifice"));string text=(string)Call(ui,"AbilityEffectText",item.source,item.description);string player=language=="zh-CN"?"玩家 2":language=="ja-JP"?"プレイヤー 2":"Player 2";
   Check(text.Contains("[["+player+"]]"),language+" actual bracketed player identity localized");Check(Root.GetComponentsInChildren<TMPro.TextMeshProUGUI>().Any(t=>t.gameObject.activeInHierarchy&&t.text==CardSymbolText.Format(text)),language+" localized target body rendered");Capture(language+"-chapter-target");yield return new WaitForSecondsRealtime(.25f);
  }
  GamePreferences.SetLanguage("zh-CN");float until=Time.realtimeSinceStartup+15;while(!GamePreferences.Ready&&Time.realtimeSinceStartup<until)yield return null;
  foreach(string name in new[]{"cloak-creature","ring-facedown"}){
   var v=Read(name);ui.Present(null,1,false,null,null,"");ui.Present(v,1,false,null,null,"");yield return Frame();Call(ui,"InspectIntegrated",v.players[0].battlefield.Single(c=>c.faceDown));Canvas.ForceUpdateCanvases();Check(Inspect.gameObject.activeInHierarchy&&Text("Rules").Contains("仅你可见的原牌："),name+" visible private preview capture");Capture("zh-CN-"+name+"-private");yield return new WaitForSecondsRealtime(.25f);Check(Inspect.gameObject.activeInHierarchy,name+" private preview remains visible through capture");Inspect.gameObject.SetActive(false);
  }
  }finally{ui.Present(null,1,false,null,null,"");ui.enabled=enabled;GamePreferences.SetLanguage(locale);if(module!=null)module.enabled=input;}
 }
 static IEnumerator Validate(ForgeBattlefieldController ui){string locale=GamePreferences.Language;float scale=GamePreferences.TextScale;bool enabled=ui.enabled;ui.enabled=false;var module=EventSystem.current.currentInputModule;bool input=module!=null&&module.enabled;if(module!=null)module.enabled=false;var notices=UnityEngine.Object.FindObjectsByType<ForgeBattlefieldMigrationNotice>(FindObjectsSortMode.None);var flags=notices.Select(n=>n.enabled).ToArray();foreach(var n in notices)n.enabled=false;
  try{float end=Time.realtimeSinceStartup+15;while(!GamePreferences.Ready&&Time.realtimeSinceStartup<end)yield return null;
   foreach(string language in new[]{"zh-CN","en-US","ja-JP"}){
    GamePreferences.SetLanguage(language);end=Time.realtimeSinceStartup+15;while((!GamePreferences.Ready||GamePreferences.Language!=language)&&Time.realtimeSinceStartup<end)yield return null;Check(GamePreferences.Ready&&GamePreferences.Language==language,"Actual locale "+language);
    foreach(string name in new[]{"manifest-creature","manifest-noncreature","cloak-creature","cloak-noncreature","ring-facedown"})foreach(bool other in new[]{false,true}){
     if(reuseZhFaces&&language=="zh-CN")continue;
     var v=Read(name+(other?"-other":""));var c=v.players[0].battlefield.Single(x=>x.faceDown);ui.Present(null,1,false,null,null,"");ui.Present(v,other?2:1,false,null,null,"");yield return Frame();Call(ui,"InspectIntegrated",c);Canvas.ForceUpdateCanvases();
     string rules=Text("Rules");string originalLabel=language=="zh-CN"?"仅你可见的原牌：":language=="ja-JP"?"あなたにのみ見える元のカード：":"Original card (visible only to you):";
     Check(other?!rules.Contains(originalLabel)&&string.IsNullOrEmpty(c.knownFacePrintingKey):rules.Contains(originalLabel)&&Text("Name")==GamePreferences.CardFaceName(c.knownFaceName),language+" "+name+" exact private name/public anonymity");
     CardImageLibrary.TryLoadCardBack(out var back);Sprite expected=back;if(!other)CardImageLibrary.TryLoadPrintingKey(c.knownFacePrintingKey,out expected);Check(expected!=null&&Inspect.Find("Art").GetComponent<Image>().sprite==expected,language+" "+name+" exact art permissions");
     string expectedRule=name.StartsWith("manifest")?(language=="zh-CN"?"已显化":language=="ja-JP"?"予示された":"Manifested"):name.StartsWith("cloak")?(language=="zh-CN"?"守护{2}":language=="ja-JP"?"護法{2}":"Ward {2}"):(language=="zh-CN"?"你的持戒者不能被力量大于它的生物阻挡。":language=="ja-JP"?"あなたの指輪所持者は、それより大きいパワーを持つクリーチャーにはブロックされない。":"Your Ring-bearer can't be blocked by creatures with greater power.");
     Check(rules.Contains(expectedRule),language+" "+name+" native paragraph localized");
     if(!other&&name=="ring-facedown"){string known=GamePreferences.FeatureRules(new CardFeatureDefinition{cardName=c.knownFaceName,description=c.knownFaceRules.Replace("\\n","\n")});Check(rules.Contains(known)&&!string.IsNullOrWhiteSpace(known),language+" full private known original rules");}
     if(name=="cloak-creature"||name=="ring-facedown"){Capture(language+"-"+name+(other?"-public":"-private"));yield return new WaitForEndOfFrame();}
     Inspect.gameObject.SetActive(false);
    }
    foreach(string name in new[]{"air","air-finish","unattach"})foreach(bool acting in new[]{true,false}){
     if(previousChecks.Contains(language+" "+name+(acting?" exact native ID and duplicate guard":" readonly guard")))continue;
     var v=Read(name);int calls=0,last=-1;ui.Present(null,1,false,null,null,"");ui.Present(v,1,acting,i=>{calls++;last=i;},null,"");yield return Frame();
     var heading=Root.GetComponentsInChildren<Text>().Single(t=>t.name==(name=="unattach"?"ValueTitle":"Instruction"));string expected=name=="unattach"?(language=="zh-CN"?"将“铸阳锤”卸装以支付费用？":language=="ja-JP"?"コストを支払うために「太陽打ちの槌」をはずしますか？":"Unattach Sunforger to pay this cost?"):(language=="zh-CN"?"可再选择至多一个目标生物或咒语。":language=="ja-JP"?"追加でクリーチャー１体か呪文１つを対象に選んでもよい。":"Select up to another target creature or spell.");
     Check(heading.text==expected,language+" "+name+" exact localized native prompt");Check(!heading.resizeTextForBestFit&&heading.preferredHeight<=heading.rectTransform.rect.height+1,language+" "+name+" fixed size full prompt");float font=heading.fontSize;ui.ViewBattlefield();yield return Frame();ui.RestoreBattlefieldWindows();yield return Frame();Check(font==heading.fontSize,language+" "+name+" view return stable font");
     var option=name=="unattach"?v.request.options.Single(o=>o.id=="ok"):name=="air"?v.request.options.Single(o=>o.card?.name=="Air Elemental"):v.request.options.Single(o=>o.label=="[FINISH TARGETING]");int expectedIndex=ui.OptionIndex(option.id);var button=name=="unattach"?GameObject.Find("ValueOption_"+expectedIndex):name=="air"?GameObject.Find("ChoiceCard_"+option.card.id+"/Artwork"):GameObject.Find("SelectionAction_"+expectedIndex);
     Check(button!=null,language+" "+name+" actual consumer control");Check(!ui.TrySubmitPresentedOption(expectedIndex,v.request.requestId-1),language+" "+name+" stale guard");
     if(acting){Capture(language+"-"+name);yield return new WaitForEndOfFrame();Click(button);Check(calls==1&&last==expectedIndex&&!ui.TrySubmitPresentedOption(expectedIndex,v.request.requestId),language+" "+name+" exact native ID and duplicate guard");}
     else Check(!ui.TrySubmitPresentedOption(expectedIndex,v.request.requestId)&&calls==0,language+" "+name+" readonly guard");
    }
    for(int i=1;i<=5;i++){
     var v=Read("chapter-"+i);ui.Present(null,1,false,null,null,"");ui.Present(v,1,false,null,null,"");yield return Frame();
     foreach(var item in v.stackItems.Where(s=>s.source?.name=="Kardur's Vicious Return"&&s.description!="Kardur's Vicious Return")){
      string text=(string)Call(ui,"AbilityEffectText",item.source,item.description);string needle=language=="zh-CN"?"弃一张牌":language=="ja-JP"?"カード１枚を捨てる":"discards a card";
      if(item.description.StartsWith("I —")||item.description.StartsWith("You may sacrifice"))needle=language=="zh-CN"?"你可以牺牲一个生物":language=="ja-JP"?"クリーチャー１体を生け贄に捧げてもよい":"You may sacrifice a creature";
      if(item.description.StartsWith("III"))needle=language=="zh-CN"?"从你的坟墓场移回战场":language=="ja-JP"?"それを戦場に戻す":"Return target creature card";
      Check(text.Contains(needle),language+" chapter "+i+" exact own paragraph match");
      if(item.description.Contains("Added once"))Check(text.Contains(GamePreferences.CardFaceName(item.source.name)+" (121)")&&(language=="en-US"||!text.Contains("Added once:")),language+" chapter "+i+" added source retained/localized");
      if(item.description.Contains("Grizzly Bears ("))Check(text.Contains(GamePreferences.CardFaceName("Grizzly Bears")+" (")&&!text.Contains("Targeting:"),language+" chapter "+i+" target identity retained");
      // The stack face and hover both use the same formatter. Check actual TMP
      // content, including scrollable tails, rather than a disabled legacy label.
      var body=Root.GetComponentsInChildren<TMPro.TextMeshProUGUI>().Where(t=>t.gameObject.activeInHierarchy&&t.name=="Effect"||t.gameObject.activeInHierarchy&&t.transform.parent?.name=="Effect").ToArray();
      Check(body.Any(t=>t.text==CardSymbolText.Format(text)),language+" chapter "+i+" rendered full body");
     }
     if(i==3){Capture(language+"-chapters");yield return new WaitForEndOfFrame();}
    }
    var custom=new ForgeCardView{faceDown=true,currentRules="Custom ward with an extra condition.",knownFaceName="MTGB Unknown Private Face",knownFaceRules="Unknown original rules."};Check((string)Call(ui,"FaceDownRules",custom)==custom.currentRules&&(string)Call(ui,"KnownFaceRules",custom)==custom.knownFaceRules,language+" unknown private prose preserved");
    var src=Read("chapter-1").stackItems.First().source;string unknown="Custom chapter effect. [Added once: Another Source (999)]";Check((string)Call(ui,"AbilityEffectText",src,unknown)==unknown,language+" unmatched chapter/wrapper preserved");
   }
   GamePreferences.SetLanguage("zh-CN");end=Time.realtimeSinceStartup+15;while(!GamePreferences.Ready&&Time.realtimeSinceStartup<end)yield return null;GamePreferences.SetScale(1.3f);var max=Read("air");ui.Present(null,1,false,null,null,"");ui.Present(max,1,false,null,null,"");yield return Frame();var h=Root.GetComponentsInChildren<Text>().Single(t=>t.name=="Instruction");Check(!h.resizeTextForBestFit&&h.preferredHeight<=h.rectTransform.rect.height+1,"Max scale target prompt fits without shrinking");
  }finally{ui.Present(null,1,false,null,null,"");ui.enabled=enabled;GamePreferences.SetScale(scale);GamePreferences.SetLanguage(locale);if(module!=null)module.enabled=input;for(int i=0;i<notices.Length;i++)if(notices[i]!=null)notices[i].enabled=flags[i];}
 }
}
#endif
