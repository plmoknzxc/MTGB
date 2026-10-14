#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
public static class PaymentHintValidation1014 {
 const string Out=".tools/payment-hints-1014"; const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
 sealed class Host:MonoBehaviour{} public static bool Running; static List<string> checks=new();
 static object Get(object o,string n)=>o.GetType().GetField(n,Hidden).GetValue(o);
 static ForgeSeatView Read(string p)=>JsonUtility.FromJson<ForgeSeatView>(File.ReadAllText(p));
 static void Check(bool ok,string n){if(!ok)throw new Exception(n);checks.Add("PASS "+n);}
 static void Capture(string n){var t=ScreenCapture.CaptureScreenshotAsTexture();File.WriteAllBytes(Out+"/"+n+".png",t.EncodeToPNG());UnityEngine.Object.Destroy(t);}
 public static void Run(){if(Running)return;Directory.CreateDirectory(Out);checks.Clear();Running=true;new GameObject("PaymentHintProbe").AddComponent<Host>().StartCoroutine(Guard(Exercise()));}
 static IEnumerator Guard(IEnumerator e){while(true){bool more;object next;try{more=e.MoveNext();next=more?e.Current:null;}catch(Exception ex){checks.Add("FAIL "+ex);more=false;next=null;}if(!more)break;yield return next;}GamePreferences.SetScale(1);File.WriteAllLines(Out+"/ui-result.txt",checks);Running=false;}
 static Rect Bounds(RectTransform r){var c=new Vector3[4];r.GetWorldCorners(c);return Rect.MinMaxRect(c[0].x,c[0].y,c[2].x,c[2].y);}
 static IEnumerator Exercise(){
  while(!GamePreferences.Ready)yield return null;
  foreach(var n in UnityEngine.Object.FindObjectsByType<ForgeBattlefieldMigrationNotice>(FindObjectsSortMode.None))n.enabled=false;
  var ui=UnityEngine.Object.FindFirstObjectByType<ForgeBattlefieldController>()??new GameObject("HintBoard").AddComponent<ForgeBattlefieldController>();ui.enabled=false;ui.GetType().GetField("service",Hidden).SetValue(ui,null);
  foreach(float scale in new[]{1f,1.3f}) {
   GamePreferences.SetScale(scale);
   foreach(int step in new[]{0,1,2}) {
    var p=Read(".tools/cost-unification-1014/exile-"+step+".json");int sent=-1;ui.Present(p,p.request.seat,true,i=>sent=i,null,"");yield return new WaitForSecondsRealtime(.7f);
    var panel=(RectTransform)Get(ui,"choiceHintPanel");var text=(TMPro.TextMeshProUGUI)Get(ui,"manaPaymentPrompt");
    Check(panel.gameObject.activeInHierarchy&&panel.anchoredPosition==new Vector2(0,65),"Shared mana-hint position exile "+step+" scale "+scale);
    Check(!text.text.Contains("As an additional")&&!text.text.Contains("Card.Green")&&!text.text.Contains("Allosaurus Rider"),"Source-only generated cost translated "+step+" scale "+scale+": "+text.text);
    Check(text.text.Contains("选择 2 张")&&text.text.Contains("已选 "+step+" / 2")&&!text.text.Contains("放逐0张"),"Total and progress distinct "+step+" scale "+scale);
    Check(panel.GetComponent<Image>().color.a>.9f&&!panel.GetComponent<Image>().raycastTarget&&!text.raycastTarget,"Readable nonblocking hint frame "+step+" scale "+scale);
    Check(text.GetPreferredValues(text.text,text.rectTransform.rect.width,Mathf.Infinity).y<=text.rectTransform.rect.height+1,"Text fits without shrinking "+step+" scale "+scale);
    var node=ui.GetType().GetMethod("Node",Hidden).Invoke(ui,new object[]{"CornerCluster/ControlRow/TurnChip/TurnLabel"}) as RectTransform;
    Check(!Bounds(panel).Overlaps(Bounds(node)),"Hint clear of phase label "+step+" scale "+scale);
    yield return new WaitForEndOfFrame();if(step==2)Capture("exile-complete-"+scale);
    Check(ui.TryCancelPresentedChoice()&&sent>=0&&p.request.options[sent].id=="cancel","Original cancel retained "+step+" scale "+scale);
   }
   foreach(string name in new[]{"life-additional-tax-confirmation","energy-unaffected-floor-confirmation"}) {
    var p=Read(".tools/cost-unification-1014/"+name+".json");ui.Present(p,p.request.seat,true,i=>{},null,"");yield return new WaitForSecondsRealtime(.7f);
    var panel=(RectTransform)Get(ui,"choiceHintPanel");var text=(TMPro.TextMeshProUGUI)Get(ui,"manaPaymentPrompt");Check(panel.gameObject.activeInHierarchy&&panel.anchoredPosition==new Vector2(0,65),name+" uses same framed position "+scale);
    Check(!text.text.Contains("Do you")&&!text.text.Contains("Pay ")&&!text.text.Contains("Player"),name+" Chinese prompt "+scale+": "+text.text);
    var dock=(RectTransform)Get(ui,"targetChoiceActions");Check(dock.Find("PaymentPrompt")==null,"No phase-dock duplicate "+name+" "+scale);yield return new WaitForEndOfFrame();if(scale==1)Capture(name);
   }
   var picker=Read(".tools/cost-unification-1014/exile-2.json");picker.players.First(p=>p.seat==picker.request.seat).handCount=11;ui.Present(picker,picker.request.seat,true,i=>{},null,"");yield return new WaitForSecondsRealtime(.7f);
   var instruction=(Text)Get(ui,"selectionInstructionText");Check(instruction.gameObject.activeInHierarchy&&!instruction.text.Contains("As an additional")&&!instruction.text.Contains("Card.Green")&&instruction.text.Contains("选择 2 张"),"Offboard window shares translated effect and total (layout adapter) "+scale+": "+instruction.text);
   Check(!((RectTransform)Get(ui,"choiceHintPanel")).gameObject.activeSelf,"Window owns its own instruction "+scale);yield return new WaitForEndOfFrame();if(scale==1)Capture("exile-window");
  }
  var mana=Read(".tools/bolt-amber-flow-1014/bolt-04-mana-payment.json");int accepted=-1;ui.Present(mana,mana.request.seat,true,i=>accepted=i,null,"");yield return null;
  var frame=(RectTransform)Get(ui,"choiceHintPanel");Check(frame.gameObject.activeInHierarchy&&frame.anchoredPosition==new Vector2(0,65),"Ordinary mana shares same framed hint");
  var actions=(RectTransform)Get(ui,"manaPaymentActions");actions.Find("AutoPayment").GetComponent<Button>().onClick.Invoke();Check(accepted>=0&&mana.request.options[accepted].id=="ok","Auto payment keeps original reply");
  mana.awaitingInput=false;ui.Present(mana,mana.seat,false,null,null,"");yield return null;Check(!frame.gameObject.activeSelf,"Hint hides when input ends");
 }
}
#endif
