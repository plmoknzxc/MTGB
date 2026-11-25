const string W="D:/project/MTGB/design-demos/battlefield-redesign-2026-10-24/";
var ui=UnityEngine.Object.FindFirstObjectByType<ForgeBattlefieldController>();
if(ui==null)ui=new GameObject("RedesignPreview").AddComponent<ForgeBattlefieldController>();
if(!Application.isPlaying||UnityEngine.SceneManagement.SceneManager.GetActiveScene().name!=ForgeBattlefieldController.SceneName)return "Open the duel battlefield in Play Mode.";
var flags=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance;
bool savedUiEnabled=ui.enabled;var inputModule=UnityEngine.EventSystems.EventSystem.current.currentInputModule;bool savedModuleEnabled=inputModule!=null&&inputModule.enabled;ui.enabled=false;if(inputModule!=null)inputModule.enabled=false;
var testEventSystem=UnityEngine.Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>();bool savedEventEnabled=testEventSystem.enabled;testEventSystem.enabled=false;
var results=new System.Collections.Generic.List<object>();int checks=0,failed=0,calls=0,last=-1;string oldLanguage=GamePreferences.Language;float oldScale=GamePreferences.TextScale;
void Check(bool pass,string context){checks++;if(!pass)failed++;results.Add(new{pass,context});}
void Save(string status){var method=Type.GetType("Newtonsoft.Json.JsonConvert, Newtonsoft.Json").GetMethod("SerializeObject",new[]{typeof(object)});System.IO.File.WriteAllText(W+"wish-sideboard-ui.json",(string)method.Invoke(null,new object[]{new{status,checks,failed,scope="Copied-scene display adapters and recorded native choice consumers; no live network or new engine effect claims",results}}));}
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
System.Collections.IEnumerator Run(){
 for(int i=0;i<200&&!GamePreferences.Ready;i++)yield return Wait(.05f);
 testEventSystem.enabled=true;if(inputModule==null){inputModule=testEventSystem.currentInputModule;savedModuleEnabled=inputModule!=null&&inputModule.enabled;}if(inputModule!=null)inputModule.enabled=false;ui.enabled=false;
 foreach(var cfg in new[]{new{w=1920,h=1080,lang="zh-CN"},new{w=1280,h=720,lang="en-US"},new{w=1024,h=768,lang="ja-JP"}}){
  foreach(float scale in new[]{.85f,1f,1.3f}){
   Resize(cfg.w,cfg.h);GamePreferences.SetLanguage(cfg.lang);GamePreferences.SetScale(scale);for(int i=0;i<200&&!GamePreferences.Ready;i++)yield return Wait(.05f);yield return Wait(.2f);
   string tag=cfg.lang+"/"+scale;Check(GamePreferences.Ready&&GamePreferences.Language==cfg.lang,tag+" actual locale/font ready");
   var before=Native("before-permission");yield return Present(before);int count=calls;Open();yield return Wait();Check(Casts().Length==0,tag+" no action from browsing without permission");
   typeof(ForgeBattlefieldController).GetMethod("CloseReviewCards",flags).Invoke(ui,null);
   var response=Native("response-available");yield return Present(response);var stifle=response.players[0].sideboardCards.Single(c=>c.name=="Stifle");
   int expected=Array.FindIndex(response.request.options,o=>o.id=="card:"+stifle.id);Open();yield return Wait();
   var casts=Casts();Check(casts.Length==1,tag+" only native legal Stifle has cast action");
   if(casts.Length==1){
    Check(casts[0].transform.parent.name=="ChoiceCard_"+stifle.id,tag+" cast belongs to exact sideboard physical card");
    var label=casts[0].GetComponentInChildren<UnityEngine.UI.Text>();
    Check(label.text==(cfg.lang=="zh-CN"?"施放":cfg.lang=="en-US"?"Cast":"唱える"),tag+" localized cast label");
    Check(label.preferredWidth<=label.rectTransform.rect.width+1&&label.preferredHeight<=label.rectTransform.rect.height+1,tag+" cast label fits");
    var cb=Bounds(casts[0].GetComponent<RectTransform>());var panel=(RectTransform)typeof(ForgeBattlefieldController).GetField("selectionPanel",flags).GetValue(ui);var pb=Bounds(panel);
    Check(cb.yMin>=pb.yMin&&cb.yMax<=pb.yMax,tag+" cast button stays within selection layout");
    Check(Click(casts[0]),tag+" actual raycast reaches cast button");yield return Wait();
    Check(calls==count+1&&last==expected,tag+" one exact native option submitted");count=calls;
    casts[0]=null;
   }
   // Keep the same browser open while the native request/permission changes.
   yield return Present(response);Open();yield return Wait();Check(Casts().Length==1,tag+" browser starts with native response");
   var spent=Native("permission-used");ui.Present(spent,1,true,i=>{calls++;last=i;},null,"本地界面验证");yield return Wait();
   Check(Root().Find("CardSelectionOverlay").gameObject.activeSelf&&Casts().Length==0,tag+" consumed permission refreshes open browser and removes cast");
   var expired=Native("expired");ui.Present(expired,1,true,i=>{calls++;last=i;},null,"本地界面验证");yield return Wait();
   Check(Casts().Length==0,tag+" expired permission leaves inspection only");
   Check(calls==count,tag+" browsing and permission refresh never submit a game action");
   yield return Present(Native("no-blue/unpayable-response"));Open();yield return Wait();Check(Casts().Length==0,tag+" native insufficient mana has no cast buttons");
   var main=Native("after-wish");yield return Present(main);Open();yield return Wait();
   var land=main.players[0].sideboardCards.Single(c=>c.land);var play=Casts().SingleOrDefault(b=>b.transform.parent.name=="ChoiceCard_"+land.id);
   Check(play!=null&&play.GetComponentInChildren<UnityEngine.UI.Text>().text==(cfg.lang=="zh-CN"?"使用":cfg.lang=="en-US"?"Play":"プレイ"),tag+" permitted sideboard land has localized play action");
   int landBefore=calls;Check(play!=null&&Click(play),tag+" native land action reachable by raycast");yield return Wait();
   Check(calls==landBefore+1&&last==Array.FindIndex(main.request.options,o=>o.id=="card:"+land.id),tag+" exact sideboard land option submitted");
   yield return Present(response);ui.Present(response,1,false,i=>{calls++;last=i;},null,"只读预览");Open();yield return Wait();Check(Casts().Length==0,tag+" non-acting/replay view cannot cast");
   typeof(ForgeBattlefieldController).GetMethod("CloseReviewCards",flags).Invoke(ui,null);
   typeof(ForgeBattlefieldController).GetMethod("OpenZoneBrowser",flags).Invoke(ui,new object[]{2,"备牌"});
   Check(!Root().Find("CardSelectionOverlay").gameObject.activeSelf,tag+" opponent sideboard access rejected");
  }
 }
 Restore();GamePreferences.SetLanguage("zh-CN");GamePreferences.SetScale(1);sizeProperty.SetValue(gameView,3);
 for(int i=0;i<200&&!GamePreferences.Ready;i++)yield return Wait(.05f);yield return Wait(.2f);
 yield return Present(Native("response-available"));Open();yield return Wait(.3f);Save("COMPLETE");
}
System.Collections.IEnumerator Guard(){var it=Run();for(int i=0;i<10000;i++){bool next=false;object value=null;try{next=it.MoveNext();if(next)value=it.Current;}catch(Exception e){failed++;results.Add(new{exception=e.ToString()});Restore();Save("ERROR");yield break;}if(!next)break;yield return value;}}
Save("STARTING");ui.StartCoroutine(Guard());return "Native deferred Wish sideboard browser consumer and request lifecycle";

