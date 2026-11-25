const string W="D:/project/MTGB/design-demos/battlefield-redesign-2026-10-24/";
var ui=UnityEngine.Object.FindFirstObjectByType<ForgeBattlefieldController>();
if(ui==null)ui=new GameObject("RedesignPreview").AddComponent<ForgeBattlefieldController>();
if(!Application.isPlaying||UnityEngine.SceneManagement.SceneManager.GetActiveScene().name!=ForgeBattlefieldController.SceneName)return "Open the duel battlefield in Play Mode.";
var flags=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance;
var results=new System.Collections.Generic.List<object>();int checks=0,failed=0,calls=0,last=-1;string oldLanguage=GamePreferences.Language;float oldScale=GamePreferences.TextScale;
void Check(bool pass,string context){checks++;if(!pass)failed++;results.Add(new{pass,context});}
void Save(string status){var method=Type.GetType("Newtonsoft.Json.JsonConvert, Newtonsoft.Json").GetMethod("SerializeObject",new[]{typeof(object)});System.IO.File.WriteAllText(W+"hand-dock-cards-only-validation.json",(string)method.Invoke(null,new object[]{new{status,checks,failed,scope="Copied-scene display adapters and recorded native choice consumers; no live network or new engine effect claims",results}}));}
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
void Restore(){GamePreferences.SetLanguage(oldLanguage);GamePreferences.SetScale(oldScale);sizeProperty.SetValue(gameView,0);var labels=(string[])group.GetType().GetMethod("GetDisplayTexts").Invoke(group,null);for(int i=labels.Length-1;i>=0;i--)if(labels[i].StartsWith("MTGB hand sections"))group.GetType().GetMethod("RemoveCustomSize").Invoke(group,new object[]{i});}
ForgeSeatView TabSample(){var v=Sample();var native=Read("D:/project/MTGB/.tools/hand-source-tabs-1025/view-1.json");v.playableGraveyard=native.players[0].publicPlayableGraveyard;v.players[1].publicPlayableGraveyard=native.players[1].publicPlayableGraveyard;v.players[0].graveyardCards=native.players[0].graveyardCards;v.players[1].graveyardCards=native.players[1].graveyardCards;v.playableExile=new[]{Clone(v.players[0].hand[0])};v.playableExile[0].id=98701;v.playableExile[0].zone="Exile";v.players[1].publicPlayableExile=new[]{Clone(v.players[0].hand[1])};v.players[1].publicPlayableExile[0].id=98702;v.players[1].publicPlayableExile[0].zone="Exile";v.players[1].publicPlayableExile[0].ownerSeat=2;v.awaitingInput=false;return v;}
UnityEngine.UI.Button Tab(bool other,string zone)=>Root().Find((other?"OtherHandSourceTabs":"HandSourceTabs")+"/Source_"+zone).GetComponent<UnityEngine.UI.Button>();
int[] Shown(bool other)=>Root().Find(other?"OpponentHandRoot":"PlayerHandRoot").GetComponentsInChildren<ForgeBattlefieldCardPointer>().Where(p=>p.gameObject.activeInHierarchy).Select(p=>int.Parse(p.name.Substring(5))).OrderBy(i=>i).ToArray();
ForgeSeatView FourSources(bool many=false){
 var v=Read(W+"hand-source-tabs-input.json");
 var template=Read(W+"native-full-seat.json").players[1].commandCards.First(c=>c.commander);
 ForgeCardView[] Zone(ForgeCardView c,string zone,int seat,int id,int count)=>Enumerable.Range(0,count).Select(i=>{var copy=Clone(c);copy.id=id+i;copy.zone=zone;copy.ownerSeat=seat;copy.commander=zone=="Command";return copy;}).ToArray();
 int count=many?9:1;
 v.playableExile=Zone(v.players[0].hand[4],"Exile",1,91000,count);
 v.playableGraveyard=Zone(v.playableGraveyard[0],"Graveyard",1,92000,many?9:2);
 v.playableCommand=Zone(template,"Command",1,93000,count);
 v.players[1].publicPlayableExile=Zone(v.players[0].hand[3],"Exile",2,94000,count);
 v.players[1].publicPlayableGraveyard=Zone(v.playableGraveyard[0],"Graveyard",2,95000,count);
 v.players[1].commandCards=Zone(template,"Command",2,96000,count);
 if(many){v.players[0].hand=Zone(v.players[0].hand[0],"Hand",1,97000,12);v.players[0].handCount=12;v.players[1].hand=Array.Empty<ForgeCardView>();v.players[1].handCount=12;}
 return v;
}
ForgeHandFanNavigation Nav(bool other,string zone)=>Root().Find((other?"OpponentHandRoot/":"PlayerHandRoot/")+(zone=="Hand"?"HandFanNavigation":"SourceNavigation_"+zone)).GetComponent<ForgeHandFanNavigation>();
ForgeBattlefieldCardPointer[] Cards(bool other)=>Root().Find(other?"OpponentHandRoot":"PlayerHandRoot").GetComponentsInChildren<ForgeBattlefieldCardPointer>().Where(p=>p.gameObject.activeInHierarchy).ToArray();
bool IsShown(ForgeBattlefieldCardPointer p)=>p.GetComponent<CanvasGroup>().alpha>.02f;
bool Drag(bool other,string zone){
 var nav=Nav(other,zone);var b=Bounds((RectTransform)nav.transform);
 float low=Mathf.Max(2,b.yMin),high=Mathf.Min(Screen.height-2,b.yMax);
 foreach(float x in new[]{.02f,.98f,.5f,.25f,.75f})foreach(float y in new[]{.5f,.2f,.8f}){
  var point=new Vector2(Mathf.Lerp(b.xMin,b.xMax,x),Mathf.Lerp(low,high,y));
  var hit=Hit(point);if(hit==null)continue;
  var target=UnityEngine.EventSystems.ExecuteEvents.GetEventHandler<UnityEngine.EventSystems.IBeginDragHandler>(hit);
  if(target==null)continue;
  var pointer=target.GetComponent<ForgeBattlefieldCardPointer>();
  var routed=pointer==null?target.GetComponent<ForgeHandFanNavigation>():typeof(ForgeBattlefieldCardPointer).GetField("handNavigation",flags).GetValue(pointer) as ForgeHandFanNavigation;
  if(routed!=nav)continue;
  var e=Event(point);e.button=UnityEngine.EventSystems.PointerEventData.InputButton.Middle;
  UnityEngine.EventSystems.ExecuteEvents.Execute(target,e,UnityEngine.EventSystems.ExecuteEvents.beginDragHandler);
  e.position=point+Vector2.left*120;
  UnityEngine.EventSystems.ExecuteEvents.Execute(target,e,UnityEngine.EventSystems.ExecuteEvents.dragHandler);
  UnityEngine.EventSystems.ExecuteEvents.Execute(target,e,UnityEngine.EventSystems.ExecuteEvents.endDragHandler);
  return true;
 }
 return false;
}
ForgeSeatView Rich(bool extra=false){
 var v=FourSources();v.players[0].commandCards=v.playableCommand.Select(c=>Clone(c)).ToArray();
 v.players[0].usableSourceIds=v.playableGraveyard.Concat(v.playableExile).Select(c=>c.id).ToArray();
 v.players[1].usableSourceIds=v.players[1].publicPlayableGraveyard.Concat(v.players[1].publicPlayableExile).Select(c=>c.id).ToArray();
 v.awaitingInput=true;var options=new System.Collections.Generic.List<ForgeOption>{new(){id="ok",label="Pass priority"},new(){id="cancel",label="Undo mana"}};
 options.AddRange(v.players[0].hand.Concat(v.playableGraveyard).Concat(v.playableExile).Concat(v.playableCommand).Select(c=>new ForgeOption{id="card:"+c.id,label=c.name}));
 if(extra){options.Add(new ForgeOption{id="retry",label="Reselect"});options.Add(new ForgeOption{id="life:4",label="Pay 4 life"});}
 v.request=new ForgeRequest{kind="InputPassPriority",requestId=991001+(extra?1:0),seat=1,playerSeat=1,message="本地布局样例",options=options.ToArray()};
 return v;
}
UnityEngine.UI.Button NativeButton(string name)=>typeof(ForgeBattlefieldController).GetField(name,flags).GetValue(ui) as UnityEngine.UI.Button;
System.Collections.IEnumerator Run(){
 while(!GamePreferences.Ready)yield return null;yield return Wait(.2f);
 foreach(var size in new[]{(1920,1080,"zh-CN",1f),(1280,720,"en-US",1.3f),(1024,768,"ja-JP",1.3f)}){
  Resize(size.Item1,size.Item2);GamePreferences.SetLanguage(size.Item3);GamePreferences.SetScale(size.Item4);yield return null;while(!GamePreferences.Ready)yield return null;yield return Wait(.2f);
  foreach(bool extra in new[]{false,true}){
   var v=Rich(extra);yield return Present(v);Expanded(true);Quiet();yield return Wait(.2f);string context=size+" extra="+extra;
   var frame=(RectTransform)Root().Find("RedesignHandBackplate");var fb=Bounds(frame);var hand=(RectTransform)Root().Find("PlayerHandRoot");float scale=fb.width/frame.rect.width;
   Check(Mathf.Abs(hand.anchoredPosition.y+6)<.1f,context+" cards move down fifty units");
   Check(Mathf.Abs(frame.rect.height-318)<.1f,context+" dock top lowered fifty units while bottom stays at screen edge");
   Check(Mathf.Abs(frame.rect.width-(hand.rect.width-32))<.1f,context+" frame wraps card sections only");
   Check(frame.GetSiblingIndex()<Root().Find("CornerCluster").GetSiblingIndex(),context+" shared frame stays behind native footer");
   float footerLeft=(float)typeof(ForgeBattlefieldController).GetField("redesignFooterLeft",flags).GetValue(ui);Check(Mathf.Abs(fb.xMax-(footerLeft-32)*scale)<1,context+" card frame fills to actual footer gutter");
   Check(hand.anchoredPosition.x+hand.rect.width<=footerLeft-15,context+" card region stops before actual footer controls");
   var titles=(RectTransform)Tab(false,"Hand").transform.parent;Check(Mathf.Abs(titles.rect.width-(hand.rect.width-48))<.1f,context+" title and source widths reflow together");
   var controls=Root().Find("CornerCluster/ControlRow").GetComponentsInChildren<UnityEngine.UI.Button>().Where(b=>b.gameObject.activeInHierarchy).ToArray();
   Check(controls.All(b=>!Overlap(fb,Bounds((RectTransform)b.transform))),context+" footer stays outside decorative frame");
   Check(titles.GetComponentsInChildren<UnityEngine.UI.Text>().All(t=>t.preferredWidth<=t.rectTransform.rect.width+.5f&&t.preferredHeight<=t.rectTransform.rect.height+.5f),context+" source title text fits");
   var cards=Cards(false).Where(IsShown).ToArray();int intersections=0;foreach(var p in cards)foreach(var b in controls)if(Overlap(Bounds((RectTransform)p.transform),Bounds((RectTransform)b.transform)))intersections++;
   Check(intersections==0,context+" resting source cards clear footer buttons");
   Check(cards.All(p=>Bounds((RectTransform)p.transform).yMin>=-1),context+" raised resting card bottoms remain on screen");
   foreach(string field in new[]{"advanceButton","undoManaButton"}){
    var button=NativeButton(field);Check(button.gameObject.activeInHierarchy&&button.interactable,context+" native "+field+" visible and enabled");
    int expected=field=="advanceButton"?0:1,before=calls;Check(Click(button)&&calls==before+1&&last==expected,context+" native "+field+" actual raycast invokes original option");
    yield return Present(v,1,false);Expanded(true);Quiet();yield return Wait();
   }
   Expanded(false);yield return Wait();Check(Click(Tab(false,"Hand")),context+" tucked title remains reachable");yield return Wait();Check((bool)typeof(ForgeBattlefieldController).GetField("redesignHandExpanded",flags).GetValue(ui),context+" title raises lower dock");
   Save("RUNNING");
   if(size.Item1==1920&&!extra){Quiet();yield return Wait(.2f);ScreenCapture.CaptureScreenshot(W+"hand-dock-lower-cards-only.png");yield return Wait(.3f);}
   if(size.Item1==1024&&extra){Quiet();yield return Wait(.2f);ScreenCapture.CaptureScreenshot(W+"hand-dock-lower-cards-only-narrow.png");yield return Wait(.3f);}
  }
 }
 GamePreferences.SetLanguage("zh-CN");GamePreferences.SetScale(1);Resize(1920,1080);yield return null;while(!GamePreferences.Ready)yield return null;yield return Wait(.2f);yield return Present(Rich());Expanded(true);Quiet();yield return Wait(.2f);
 Restore();Save("COMPLETE");
}
System.Collections.IEnumerator Guard(){var it=Run();for(int i=0;i<100000;i++){bool next=false;object value=null;try{next=it.MoveNext();if(next)value=it.Current;}catch(Exception e){failed++;results.Add(new{exception=e.ToString()});Restore();Save("ERROR");yield break;}if(!next)break;yield return value;}}
Save("STARTING");ui.StartCoroutine(Guard());return "Lower full-width dock and native footer validation";
