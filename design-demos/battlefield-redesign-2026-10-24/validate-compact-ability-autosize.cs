const string W="D:/project/MTGB/design-demos/battlefield-redesign-2026-10-24/";
var ui=UnityEngine.Object.FindFirstObjectByType<ForgeBattlefieldController>();
if(ui==null)ui=new GameObject("RedesignPreview").AddComponent<ForgeBattlefieldController>();
if(!Application.isPlaying||UnityEngine.SceneManagement.SceneManager.GetActiveScene().name!=ForgeBattlefieldController.SceneName)return "Open the duel battlefield in Play Mode.";
var flags=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance;
bool savedUiEnabled=ui.enabled;var inputModule=UnityEngine.EventSystems.EventSystem.current?.currentInputModule;bool savedModuleEnabled=inputModule!=null&&inputModule.enabled;ui.enabled=false;if(inputModule!=null)inputModule.enabled=false;
var testEventSystem=UnityEngine.Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>();bool savedEventEnabled=testEventSystem.enabled;testEventSystem.enabled=false;
var results=new System.Collections.Generic.List<object>();int checks=0,failed=0,calls=0,last=-1;string oldLanguage=GamePreferences.Language;float oldScale=GamePreferences.TextScale;
void Check(bool pass,string context){checks++;if(!pass)failed++;results.Add(new{pass,context});}
void Save(string status){var method=Type.GetType("Newtonsoft.Json.JsonConvert, Newtonsoft.Json").GetMethod("SerializeObject",new[]{typeof(object)});System.IO.File.WriteAllText(W+"compact-ability-autosize.json",(string)method.Invoke(null,new object[]{new{status,checks,failed,scope="Copied-scene display adapters and recorded native choice consumers; no live network or new engine effect claims",results}}));}
ForgeSeatView Read(string path)=>JsonUtility.FromJson<ForgeSeatView>(System.IO.File.ReadAllText(path));
ForgeSeatView Sample()=>Read(W+"native-full-seat.json");
T Clone<T>(T value)=>JsonUtility.FromJson<T>(JsonUtility.ToJson(value));
Transform Root()=>GameObject.Find("Canvas").transform;
Rect Bounds(RectTransform r){var c=new Vector3[4];r.GetWorldCorners(c);return Rect.MinMaxRect(c.Min(v=>v.x),c.Min(v=>v.y),c.Max(v=>v.x),c.Max(v=>v.y));}
bool Overlap(Rect a,Rect b)=>Mathf.Min(a.xMax,b.xMax)-Mathf.Max(a.xMin,b.xMin)>1&&Mathf.Min(a.yMax,b.yMax)-Mathf.Max(a.yMin,b.yMin)>1;
UnityEngine.EventSystems.PointerEventData Event(Vector2 p)=>new(UnityEngine.EventSystems.EventSystem.current){position=p,pressPosition=p,button=UnityEngine.EventSystems.PointerEventData.InputButton.Left,eligibleForClick=true};
GameObject Hit(Vector2 p){var hits=new System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult>();UnityEngine.EventSystems.EventSystem.current.RaycastAll(Event(p),hits);return hits.FirstOrDefault().gameObject;}
bool Click(UnityEngine.UI.Button button){var b=Bounds((RectTransform)button.transform);foreach(float x in new[]{.5f,.1f,.9f})foreach(float y in new[]{.5f,.1f,.9f}){var p=new Vector2(Mathf.Lerp(b.xMin,b.xMax,x),Mathf.Clamp(Mathf.Lerp(b.yMin,b.yMax,y),2,Screen.height-2));var hit=Hit(p);if(hit!=null&&hit.GetComponentInParent<UnityEngine.UI.Button>()==button){UnityEngine.EventSystems.ExecuteEvents.ExecuteHierarchy(hit,Event(p),UnityEngine.EventSystems.ExecuteEvents.pointerClickHandler);return true;}}return false;}
void Expanded(bool value)=>typeof(ForgeBattlefieldController).GetMethod("SetRedesignHandExpanded",flags).Invoke(ui,new object[]{value});
void Quiet(){foreach(var p in UnityEngine.Object.FindObjectsByType<ForgeBattlefieldCardPointer>(FindObjectsSortMode.None))p.ResetHover();typeof(ForgeBattlefieldController).GetMethod("LeaveInspectCard",flags).Invoke(ui,null);}
System.Collections.IEnumerator Wait(float t=.2f){yield return new WaitForSecondsRealtime(t);Canvas.ForceUpdateCanvases();}
System.Collections.IEnumerator Present(ForgeSeatView v,int seat=1,bool clear=true){if(clear){ui.Present(null,seat,false,null,null,"");yield return null;}ui.Present(v,seat,true,i=>{calls++;last=i;},null,"本地界面验证");Quiet();yield return Wait();}
ForgeBattlefieldCardPointer[] Other()=>Root().Find("OpponentHandRoot").GetComponentsInChildren<ForgeBattlefieldCardPointer>().Where(p=>p.gameObject.activeInHierarchy).ToArray();
bool Visible(ForgeBattlefieldCardPointer p)=>p.GetComponent<CanvasGroup>().alpha>.02f;
void Clearance(string context){var field=Root().Find("ForgePermanents").GetComponentsInChildren<ForgeBattlefieldCardPointer>().Where(p=>p.gameObject.activeInHierarchy).ToArray();var pool=Root().Find("PublicManaPool");int n=0;foreach(var p in Other().Where(Visible)){var b=Bounds((RectTransform)p.transform);foreach(var f in field)if(Overlap(b,Bounds((RectTransform)f.transform)))n++;if(pool!=null&&Overlap(b,Bounds((RectTransform)pool)))n++;}Check(n==0,context+" resting opponent cards clear battlefield and mana");}
var assembly=typeof(UnityEditor.Editor).Assembly;var gameViewType=assembly.GetType("UnityEditor.GameView");var gameView=UnityEditor.EditorWindow.GetWindow(gameViewType);var sizeProperty=gameViewType.GetProperty("selectedSizeIndex",System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance);int savedSize=(int)sizeProperty.GetValue(gameView);var sizesType=assembly.GetType("UnityEditor.GameViewSizes");var sizes=typeof(UnityEditor.ScriptableSingleton<>).MakeGenericType(sizesType).GetProperty("instance").GetValue(null);var group=sizesType.GetMethod("GetGroup").Invoke(sizes,new object[]{Enum.Parse(assembly.GetType("UnityEditor.GameViewSizeGroupType"),"Standalone")});var added=new System.Collections.Generic.List<int>();
void Resize(int w,int h){int index=((string[])group.GetType().GetMethod("GetDisplayTexts").Invoke(group,null)).Length;var size=Activator.CreateInstance(assembly.GetType("UnityEditor.GameViewSize"),new object[]{Enum.Parse(assembly.GetType("UnityEditor.GameViewSizeType"),"FixedResolution"),w,h,"MTGB hand sections"});group.GetType().GetMethod("AddCustomSize").Invoke(group,new[]{size});added.Add(index);sizeProperty.SetValue(gameView,index);gameView.Repaint();}
void Restore(){testEventSystem.enabled=savedEventEnabled;ui.enabled=savedUiEnabled;if(inputModule!=null)inputModule.enabled=savedModuleEnabled;GamePreferences.SetLanguage(oldLanguage);GamePreferences.SetScale(oldScale);sizeProperty.SetValue(gameView,0);var labels=(string[])group.GetType().GetMethod("GetDisplayTexts").Invoke(group,null);for(int i=labels.Length-1;i>=0;i--)if(labels[i].StartsWith("MTGB hand sections"))group.GetType().GetMethod("RemoveCustomSize").Invoke(group,new object[]{i});}

ForgeSeatView Native(string file,int actor=1){
 var m=JsonUtility.FromJson<ForgeWireMessage>(System.IO.File.ReadAllText("D:/project/MTGB/.tools/wish-sideboard-1026/"+file+".json"));
 var v=m.views.Single(v=>v.seat==actor);v.request=m.request.seat==actor?m.request:null;v.awaitingInput=m.request.seat==actor;return v;
}
void Open()=>typeof(ForgeBattlefieldController).GetMethod("OpenZoneBrowser",flags).Invoke(ui,new object[]{1,"备牌"});
UnityEngine.UI.Button[] Casts()=>Root().Find("CardSelectionOverlay").GetComponentsInChildren<UnityEngine.UI.Button>().Where(b=>b.name=="CastComponentCard").ToArray();

Transform Face()=>Root().Find("StackColumn/Viewport").GetComponentsInChildren<RectTransform>().First(t=>t.name=="AbilityFace"&&t.gameObject.activeInHierarchy);
TMPro.TextMeshProUGUI Body()=>Face().Find("EffectClip/Effect/Symbols").GetComponent<TMPro.TextMeshProUGUI>();
ForgeSeatView LongAbility(string lang){
 var v=Native("response-available");v.request.requestId+=500;
 var e=v.stackItems[0];e.kind="trigger";e.source=Clone(e.source);e.source.name=e.source.originalName="Text layout adapter";e.source.printingKey="";
 e.source.originalRules=e.source.currentRules="";
 string sentence=lang=="zh-CN"?"此异能的完整效果需要保留，并且允许滚动查看每一个段落。":lang=="ja-JP"?"この能力の効果をすべて保持し、スクロールして最後まで確認できます。":"This ability keeps its complete effect text and permits scrolling to every final paragraph.";
 e.description=e.rulesText=string.Join("\n",Enumerable.Repeat(sentence,14))+"\nEND_OF_ABILITY";return v;
}
System.Collections.IEnumerator Run(){
 for(int i=0;i<200&&!GamePreferences.Ready;i++)yield return Wait(.05f);ui.enabled=false;testEventSystem.enabled=true;
 if(inputModule==null){inputModule=testEventSystem.currentInputModule;savedModuleEnabled=inputModule!=null&&inputModule.enabled;}if(inputModule!=null)inputModule.enabled=false;
 foreach(var cfg in new[]{new{w=1920,h=1080,lang="zh-CN"},new{w=1280,h=720,lang="en-US"},new{w=1024,h=768,lang="ja-JP"}}){
  foreach(float scale in new[]{.85f,1f,1.3f}){
   Resize(cfg.w,cfg.h);GamePreferences.SetLanguage(cfg.lang);GamePreferences.SetScale(scale);for(int i=0;i<200&&!GamePreferences.Ready;i++)yield return Wait(.05f);yield return Wait(.2f);
   string tag=cfg.lang+"/"+scale;Check(GamePreferences.Ready&&GamePreferences.Language==cfg.lang,tag+" locale ready");
   foreach(bool longer in new[]{false,true,false}){
    var v=longer?LongAbility(cfg.lang):Native("response-available");yield return Present(v,1,false);
    var face=Face();var body=Body();var clip=(RectTransform)face.Find("EffectClip");var scroll=clip.GetComponent<UnityEngine.UI.ScrollRect>();
    Check(body.fontSize/scale>=21.9f,tag+" readable compact body floor");
    if(!longer){
     Check(body.fontSize/scale>18,tag+" short sentence enlarged beyond old thumbnail cap");
     Check(!face.Find("EffectScrollbar").gameObject.activeSelf,tag+" short sentence no unnecessary scrollbar");
     Check(body.GetPreferredValues(body.text,208,10000).y+8<=clip.rect.height+1,tag+" short sentence completely fits");
     Check(Mathf.Abs(scroll.content.anchoredPosition.y)<1,tag+" recycled long body scroll reset for short text");
    }else{
     Check(body.text.Contains("END_OF_ABILITY"),tag+" complete long effect retained");
     Check(face.Find("EffectScrollbar").gameObject.activeSelf,tag+" long effect has scrollbar at readable floor");
     Check(scroll.content.rect.height>clip.rect.height,tag+" long effect content can scroll");
     var point=Bounds(clip).center;var hit=Hit(point);var ev=Event(point);ev.scrollDelta=new Vector2(0,-1);
     bool delivered=hit!=null&&UnityEngine.EventSystems.ExecuteEvents.ExecuteHierarchy(hit,ev,UnityEngine.EventSystems.ExecuteEvents.scrollHandler)!=null;
     yield return Wait();
     Check(delivered&&scroll.content.anchoredPosition.y>0,tag+" actual wheel reaches effect scroll");
     scroll.verticalNormalizedPosition=0;yield return Wait();body.ForceMeshUpdate();
     var last=body.textInfo.characterInfo.Take(body.textInfo.characterCount).Last(c=>c.isVisible);var world=body.rectTransform.TransformPoint((last.bottomLeft+last.topRight)*.5f);
     Check(RectTransformUtility.RectangleContainsScreenPoint(clip,world,null),tag+" scroll reaches final visible character");
    }
    Check(calls==0,tag+" sizing and scrolling never submit a game action");
   }
  }
 }
 // Original layout continues using its existing full-size face range.
 var profileField=typeof(ForgeBattlefieldController).GetField("redesignProfile",flags);var profile=profileField.GetValue(ui);
 try{profileField.SetValue(ui,null);yield return Present(Native("response-available"));Check(Body().fontSize/GamePreferences.TextScale<=18.1f,"old profile unaffected");}
 finally{profileField.SetValue(ui,profile);}
 Restore();GamePreferences.SetLanguage("zh-CN");GamePreferences.SetScale(1);sizeProperty.SetValue(gameView,3);
 for(int i=0;i<200&&!GamePreferences.Ready;i++)yield return Wait(.05f);yield return Wait(.2f);
 yield return Present(Native("response-available"));Save("COMPLETE");
}
System.Collections.IEnumerator Guard(){var it=Run();for(int i=0;i<10000;i++){bool next=false;object value=null;try{next=it.MoveNext();if(next)value=it.Current;}catch(Exception e){failed++;results.Add(new{exception=e.ToString()});Restore();Save("ERROR");yield break;}if(!next)break;yield return value;}}
Save("STARTING");ui.StartCoroutine(Guard());return "Compact ability body short/long/short text sizing and actual scroll";

