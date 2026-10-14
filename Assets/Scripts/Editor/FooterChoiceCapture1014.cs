#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
public static class FooterChoiceCapture1014 {
 const string In=".tools/delayed-marker-review-1014",Out=".tools/choice-footer-review-1014";
 const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
 sealed class Host:MonoBehaviour{}
 static List<string> checks=new(); public static bool Running;
 static object Get(object o,string f)=>o.GetType().GetField(f,Hidden).GetValue(o);
 static ForgeSeatView Read(string n)=>JsonUtility.FromJson<ForgeSeatView>(File.ReadAllText(In+"/"+n+".json"));
 static void Check(bool ok,string text){if(!ok)throw new Exception(text);checks.Add("PASS "+text);}
 public static void Run(){Directory.CreateDirectory(Out);checks.Clear();Running=true;new GameObject("FooterChoiceProbe").AddComponent<Host>().StartCoroutine(Guard(Exercise()));}
 static IEnumerator Guard(IEnumerator e){float scale=GamePreferences.TextScale;while(true){bool more;object next;try{more=e.MoveNext();next=more?e.Current:null;}catch(Exception ex){checks.Add("FAIL "+ex);more=false;next=null;}if(!more)break;yield return next;}GamePreferences.SetScale(scale);File.WriteAllLines(Out+"/result.txt",checks);Running=false;}
 static Rect Bounds(RectTransform r){var c=new Vector3[4];r.GetWorldCorners(c);var root=r.GetComponentInParent<Canvas>().transform;var a=root.InverseTransformPoint(c[0]);var b=root.InverseTransformPoint(c[2]);return Rect.MinMaxRect(a.x,a.y,b.x,b.y);}
 static void Capture(string n,RectTransform panel=null){var tex=ScreenCapture.CaptureScreenshotAsTexture();int x=0,y=0,w=tex.width,h=Mathf.FloorToInt(tex.height*.83f);if(panel!=null){var c=new Vector3[4];panel.GetWorldCorners(c);var a=RectTransformUtility.WorldToScreenPoint(null,c[0]);var b=RectTransformUtility.WorldToScreenPoint(null,c[2]);x=Mathf.Max(0,(int)a.x-24);y=Mathf.Max(0,(int)a.y-24);w=Mathf.Min(tex.width-x,Mathf.CeilToInt(b.x)-x+24);h=Mathf.Min(tex.height-y,Mathf.CeilToInt(b.y)-y+24);}var crop=new Texture2D(w,h,TextureFormat.RGBA32,false);crop.SetPixels(tex.GetPixels(x,y,w,h));crop.Apply();File.WriteAllBytes(Out+"/"+n+".png",crop.EncodeToPNG());UnityEngine.Object.Destroy(crop);UnityEngine.Object.Destroy(tex);}
 static IEnumerator Exercise(){
  while(!GamePreferences.Ready)yield return null;
  var ui=UnityEngine.Object.FindFirstObjectByType<ForgeBattlefieldController>()??new GameObject("FooterCaptureBoard").AddComponent<ForgeBattlefieldController>();ui.enabled=false;
  foreach(float scale in new[]{1f,1.3f})foreach(string kind in new[]{"Surveil","Scry"}){
   GamePreferences.SetScale(scale);var v=Read("surveil");v.request.kind=kind;v.request.requestId+=10000+(scale>1?100:0)+(kind=="Scry"?10:0);int selected=-1;ui.Present(v,v.seat,true,i=>selected=i,null,"");yield return new WaitForSecondsRealtime(.8f);Canvas.ForceUpdateCanvases();
   foreach(var t in UnityEngine.Object.FindObjectsByType<Text>(FindObjectsSortMode.None))if(t.text.Contains("请从联机大厅")||t.text=="返回主菜单")t.transform.parent.gameObject.SetActive(false);
   var panel=(RectTransform)Get(ui,"selectionPanel");var tile=panel.GetComponentsInChildren<RectTransform>().Single(r=>r.name.StartsWith("ChoiceCard_"));var art=(RectTransform)tile.Find("Artwork");var buttons=panel.Find("SelectionFooter").GetComponentsInChildren<Button>();
   Check(buttons.Length==2,kind+" two destination footer buttons at "+scale);Check(tile.GetComponentsInChildren<Button>().Length==0,kind+" no destination controls inside card frame at "+scale);
   foreach(var b in buttons){var bb=Bounds((RectTransform)b.transform);Check(bb.yMax<Bounds(tile).yMin-12,kind+" footer separated from artwork frame at "+scale);Check(bb.yMin>=Bounds(panel).yMin+11.9f,kind+" footer remains within panel at "+scale);}
   ui.Present(v,v.seat,true,i=>selected=i,null,"");yield return null;buttons=panel.Find("SelectionFooter").GetComponentsInChildren<Button>();Check(buttons.Length==2&&tile.GetComponentsInChildren<Button>().Length==0,kind+" cached refresh preserves footer at "+scale);
   if(kind=="Surveil"){yield return new WaitForEndOfFrame();Capture(scale==1?"surveil":"surveil-scale-1.3",panel);}
   buttons[0].onClick.Invoke();Check(selected>=0&&v.request.options[selected].id.StartsWith("scry-top:"),kind+" single click submits native top option at "+scale);
  }
  GamePreferences.SetScale(1f);
  var force=Read("force-request-800");ui.Present(force,force.seat,true,i=>{},null,"");yield return new WaitForSecondsRealtime(.85f);var menu=(RectTransform)Get(ui,"landChoiceMenu");Check(menu!=null&&menu.gameObject.activeInHierarchy,"Force native casting-method window");var bodies=menu.GetComponentsInChildren<Text>(true).Where(t=>t.name=="Effect").ToArray();Check(bodies.Length==2&&bodies.All(t=>!t.text.Contains("target noncreature")&&!t.text.Contains("Counter target")),"Both Force native casting options retain Chinese rules");Check(menu.GetComponentsInChildren<Text>(true).Where(t=>t.name=="Mode").All(t=>t.text=="施放"),"Counter action is not misidentified as keyword mode");yield return new WaitForEndOfFrame();Capture("force-casting-method",menu);
  force=Read("force-InputSelectCardsFromList-initial");ui.Present(force,force.seat,true,i=>{},null,"");yield return new WaitForSecondsRealtime(.85f);Check(force.request.options.Any(o=>o.card?.id==36),"Force native exile cost uses owned blue hand card");yield return new WaitForEndOfFrame();Capture("force-select-cost");
 }
}
#endif
