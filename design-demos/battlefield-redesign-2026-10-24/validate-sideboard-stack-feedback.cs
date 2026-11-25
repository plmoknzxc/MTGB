const string W="D:/project/MTGB/design-demos/battlefield-redesign-2026-10-24/";
var ui=UnityEngine.Object.FindFirstObjectByType<ForgeBattlefieldController>();
if(ui==null)ui=new GameObject("RedesignPreview").AddComponent<ForgeBattlefieldController>();
if(!Application.isPlaying||UnityEngine.SceneManagement.SceneManager.GetActiveScene().name!=ForgeBattlefieldController.SceneName)return "Open the duel battlefield in Play Mode.";
var flags=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance;
bool savedUiEnabled=ui.enabled;var inputModule=UnityEngine.EventSystems.EventSystem.current.currentInputModule;bool savedModuleEnabled=inputModule!=null&&inputModule.enabled;ui.enabled=false;if(inputModule!=null)inputModule.enabled=false;
var testEventSystem=UnityEngine.Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>();bool savedEventEnabled=testEventSystem.enabled;testEventSystem.enabled=false;
var results=new System.Collections.Generic.List<object>();int checks=0,failed=0,calls=0,last=-1;string oldLanguage=GamePreferences.Language;float oldScale=GamePreferences.TextScale;
void Check(bool pass,string context){checks++;if(!pass)failed++;results.Add(new{pass,context});}
void Save(string status){var method=Type.GetType("Newtonsoft.Json.JsonConvert, Newtonsoft.Json").GetMethod("SerializeObject",new[]{typeof(object)});System.IO.File.WriteAllText(W+"sideboard-stack-feedback.json",(string)method.Invoke(null,new object[]{new{status,checks,failed,scope="Copied-scene display adapters and recorded native choice consumers; no live network or new engine effect claims",results}}));}
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

Transform Side()=>Root().Find("ZonePiles_PlayerBottomLeft/Pile_备牌");
void Lit(bool expected,string tag){
 var node=Side();var text=node.Find("ZoneEntry/ZoneCaption").GetComponent<UnityEngine.UI.Text>();var bg=node.GetComponent<UnityEngine.UI.Image>();
 Check(expected?text.color.b>.95f&&text.color.g>.8f&&text.color.r<.5f:text.color.r>.6f,tag+" caption availability color");
 Check(expected?bg.color.a>.4f:bg.color.a<.05f,tag+" availability background");
}
ForgeStackGlow[] Glows()=>Root().Find("StackColumn/Viewport").GetComponentsInChildren<ForgeStackGlow>(true);
Color32[] GlowMesh(ForgeStackGlow g){
 using(var vh=new UnityEngine.UI.VertexHelper()){
  typeof(ForgeStackGlow).GetMethod("OnPopulateMesh",flags,null,new[]{typeof(UnityEngine.UI.VertexHelper)},null).Invoke(g,new object[]{vh});
  var vertices=new System.Collections.Generic.List<UnityEngine.UIVertex>();vh.GetUIVertexStream(vertices);return vertices.Select(v=>v.color).ToArray();
 }
}
System.Collections.IEnumerator Run(){
 for(int i=0;i<200&&!GamePreferences.Ready;i++)yield return Wait(.05f);ui.enabled=false;testEventSystem.enabled=true;
 if(inputModule==null){inputModule=testEventSystem.currentInputModule;savedModuleEnabled=inputModule!=null&&inputModule.enabled;}if(inputModule!=null)inputModule.enabled=false;
 foreach(var cfg in new[]{new{w=1920,h=1080,lang="zh-CN"},new{w=1280,h=720,lang="en-US"},new{w=1024,h=768,lang="ja-JP"}}){
  Resize(cfg.w,cfg.h);GamePreferences.SetLanguage(cfg.lang);GamePreferences.SetScale(cfg.w==1920?1:1.3f);for(int i=0;i<200&&!GamePreferences.Ready;i++)yield return Wait(.05f);yield return Wait(.2f);
  Check(GamePreferences.Ready&&GamePreferences.Language==cfg.lang,cfg.lang+" locale ready");
  foreach(var state in new[]{new{file="before-permission",on=false},new{file="response-available",on=true},new{file="permission-used",on=false},new{file="expired",on=false},new{file="no-blue/unpayable-response",on=false},new{file="after-wish",on=true}}){
   yield return Present(Native(state.file));Lit(state.on,cfg.lang+"/"+state.file);
   if(state.on){
    var hover=Side().GetComponent<ZonePileHover>();hover.OnPointerEnter(Event(Bounds((RectTransform)Side()).center));yield return Wait(.2f);hover.OnPointerExit(Event(Bounds((RectTransform)Side()).center));yield return Wait(.4f);Lit(true,cfg.lang+" highlight persists after hover");
    int before=calls;Check(Click(Side().GetComponent<UnityEngine.UI.Button>()),cfg.lang+" glowing entry raycast opens browser");yield return Wait();Check(calls==before&&Casts().Length>0,cfg.lang+" entry only browses native available actions");
    typeof(ForgeBattlefieldController).GetMethod("CloseReviewCards",flags).Invoke(ui,null);
   }
  }
  var pending=Native("response-available");foreach(var o in pending.request.options)if(o.id.StartsWith("card:")&&pending.players[0].sideboardCards.Any(c=>o.id=="card:"+c.id))o.requiresPaymentCheck=true;
  yield return Present(pending);Lit(false,cfg.lang+" payment not confirmed");
  var active=Native("response-available");yield return Present(active);ui.Present(active,1,false,null,null,"只读");yield return Wait();Lit(false,cfg.lang+" replay/non-acting");
  var v=Sample();v.awaitingInput=true;v.request.requestId=DateTime.UtcNow.Ticks;
  yield return Present(v);yield return Wait(.2f);
  var glows=Glows();Check(glows.Count(g=>g.isActiveAndEnabled)==1,cfg.lang+" exactly top stack glows");
  var first=glows.Single(g=>g.isActiveAndEnabled);
  Check(first.transform.parent.name=="StackEntry_0"&&first.rectTransform.rect.width>300,cfg.lang+" glow fitted to full compact row");
  Check(glows.All(g=>!g.raycastTarget),cfg.lang+" glow never intercepts input");
  var colors=GlowMesh(first);yield return Wait(.5f);Check(!colors.SequenceEqual(GlowMesh(first)),cfg.lang+" travelling glow actually animates");
  var topId=v.stackItems[0].id;var nextId=v.stackItems[1].id;
  var swapped=Clone(v);swapped.stackItems=new[]{swapped.stackItems[1],swapped.stackItems[0]};swapped.stack=swapped.stack.Reverse().ToArray();swapped.request.requestId++;
  yield return Present(swapped);glows=Glows();Check(glows.Count(g=>g.isActiveAndEnabled)==1,cfg.lang+" reordering keeps one glow");
  var entryRoot=glows.Single(g=>g.isActiveAndEnabled).transform.parent;
  var dict=(System.Collections.IDictionary)typeof(ForgeBattlefieldController).GetField("stackCards",flags).GetValue(ui);
  Check(dict.Values.Cast<object>().Any(item=>((ForgeStackItem)item.GetType().GetField("entry").GetValue(item)).id==nextId&&(RectTransform)item.GetType().GetField("row").GetValue(item)==entryRoot),cfg.lang+" glow follows new top ID");
  var ability=Clone(v);ability.stackItems[0].kind="trigger";ability.request.requestId+=10;
  yield return Present(ability);Check(Glows().Count(g=>g.isActiveAndEnabled)==1,cfg.lang+" ability stack retains row glow");
  var targeting=Clone(v);targeting.request.kind="SelectStackTarget";targeting.request.requestId+=20;targeting.request.options=new[]{new ForgeOption{id="stack:"+topId,label="Target"},new ForgeOption{id="cancel",label="Cancel"}};
  yield return Present(targeting);Check(!Glows().Any(g=>g.isActiveAndEnabled),cfg.lang+" choosing stack target suppresses glow");
  yield return Present(v);Check(Glows().Count(g=>g.isActiveAndEnabled)==1,cfg.lang+" return to priority restores glow");
  var empty=Clone(v);empty.stackItems=Array.Empty<ForgeStackItem>();empty.stack=Array.Empty<string>();empty.stackCount=0;empty.request.requestId+=30;
  yield return Present(empty);Check(!Glows().Any(g=>g.isActiveAndEnabled),cfg.lang+" empty stack leaves no stale glow");
 }
 Restore();GamePreferences.SetLanguage("zh-CN");GamePreferences.SetScale(1);sizeProperty.SetValue(gameView,3);for(int i=0;i<200&&!GamePreferences.Ready;i++)yield return Wait(.05f);yield return Wait(.2f);
 yield return Present(Native("response-available"));Save("COMPLETE");
}
System.Collections.IEnumerator Guard(){var it=Run();for(int i=0;i<10000;i++){bool next=false;object value=null;try{next=it.MoveNext();if(next)value=it.Current;}catch(Exception e){failed++;results.Add(new{exception=e.ToString()});Restore();Save("ERROR");yield break;}if(!next)break;yield return value;}}
Save("STARTING");ui.StartCoroutine(Guard());return "Sideboard eligibility highlight and compact stack travelling glow";

