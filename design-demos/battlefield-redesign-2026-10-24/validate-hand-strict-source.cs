const string W="D:/project/MTGB/design-demos/battlefield-redesign-2026-10-24/";
var ui=UnityEngine.Object.FindFirstObjectByType<ForgeBattlefieldController>();
if(ui==null)ui=new GameObject("RedesignPreview").AddComponent<ForgeBattlefieldController>();
if(!Application.isPlaying||UnityEngine.SceneManagement.SceneManager.GetActiveScene().name!=ForgeBattlefieldController.SceneName)return "Open the duel battlefield in Play Mode.";
var flags=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance;
var results=new System.Collections.Generic.List<object>();int checks=0,failed=0,calls=0,last=-1;string oldLanguage=GamePreferences.Language;float oldScale=GamePreferences.TextScale;
void Check(bool pass,string context){checks++;if(!pass)failed++;results.Add(new{pass,context});}
void Save(string status){var method=Type.GetType("Newtonsoft.Json.JsonConvert, Newtonsoft.Json").GetMethod("SerializeObject",new[]{typeof(object)});System.IO.File.WriteAllText(W+"hand-strict-source-validation.json",(string)method.Invoke(null,new object[]{new{status,checks,failed,scope="Copied-scene display adapters and recorded native choice consumers; no live network or new engine effect claims",results}}));}
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
const string N="D:/project/MTGB/.tools/strict-source-1025/";
ForgeWireMessage Packet(string path)=>JsonUtility.FromJson<ForgeWireMessage>(System.IO.File.ReadAllText(N+path));
ForgeSeatView Native(ForgeWireMessage packet,int viewer){
 var v=Clone(packet.views[viewer-1]);v.awaitingInput=packet.request?.seat==viewer;v.request=v.awaitingInput?Clone(packet.request):null;return v;
}
int CardId(ForgeBattlefieldCardPointer p)=>int.Parse(p.name.Substring(5));
int[] SourceIds(bool other,ForgeSeatView v){
 var outside=v.players.SelectMany(p=>(p.graveyardCards??Array.Empty<ForgeCardView>()).Concat(p.exileCards??Array.Empty<ForgeCardView>())).Select(c=>c.id).ToHashSet();
 return Cards(other).Select(CardId).Where(outside.Contains).OrderBy(i=>i).ToArray();
}
void Expected(ForgeSeatView v,int viewer,string context){
 foreach(var p in v.players){bool other=p.seat!=viewer;Check(SourceIds(other,v).SequenceEqual((p.usableSourceIds??Array.Empty<int>()).OrderBy(i=>i)),context+" exact source eligibility seat "+p.seat);}
 foreach(var p in Cards(false)){int id=CardId(p);bool realHand=v.players[viewer-1].hand.Any(c=>c.id==id);if(realHand&&p.GetComponent<CanvasGroup>().alpha>.02f)Check(p.GetComponent<CanvasGroup>().alpha==1,context+" real hand retained in original colour");}
}
System.Collections.IEnumerator Run(){
 while(!GamePreferences.Ready)yield return null;yield return Wait(.2f);Resize(1920,1080);yield return Wait(.3f);
 foreach(string path in new[]{"no-mana/before.json","payment/before.json","payment/after.json","payment/paying.json","timing/before.json","timing/after.json","exile/before.json","exile/after.json"}){
  var packet=Packet(path);foreach(int viewer in new[]{1,2}){var v=Native(packet,viewer);yield return Present(v,viewer);Expanded(true);Quiet();yield return Wait(.1f);Expected(v,viewer,path+" viewer"+viewer);}
 }
 var before=Native(Packet("payment/before.json"),1);yield return Present(before);int[] first=SourceIds(false,before);var after=Native(Packet("payment/after.json"),1);yield return Present(after,1,false);
 Check(first.Length>0&&SourceIds(false,after).Length==0,"real mana spent removes retained graveyard sources in same view lifecycle");
 var timing=Native(Packet("timing/before.json"),1);yield return Present(timing);var later=Native(Packet("timing/after.json"),1);yield return Present(later,1,false);Expected(later,1,"real turn timing transition");
 var current=Native(Packet("exile/before.json"),1);yield return Present(current);
 int exile=current.players[0].exileCards[0].id;Check(SourceIds(false,current).Contains(exile),"actual cast-from-exile action displayed");
 var uncertain=Clone(current);foreach(var option in uncertain.request.options.Where(o=>o.id=="card:"+exile))option.requiresPaymentCheck=true;yield return Present(uncertain,1,false);
 Check(!SourceIds(false,uncertain).Contains(exile),"unconfirmed payment cannot fall back to stale permission or eligibility metadata");
 var disabled=Clone(current);foreach(var option in disabled.request.options.Where(o=>o.id=="card:"+exile))option.disabled=true;yield return Present(disabled,1,false);Check(!SourceIds(false,disabled).Contains(exile),"disabled action removed despite retained exile permission");
 var privateView=Clone(current);int secret=999001,handId=privateView.players[0].hand[0].id;privateView.players[0].exileCards=privateView.players[0].exileCards.Concat(new[]{new ForgeCardView{id=secret,zone="Exile",faceDown=true,knownFaceName="PRIVATE"}}).ToArray();privateView.players[0].usableSourceIds=privateView.players[0].usableSourceIds.Concat(new[]{secret,handId}).ToArray();
 var spectator=OnlineSpectatorView.Create(new OnlinePlayerViewState{forge=privateView},0);
 Check(!spectator.forge.players[0].usableSourceIds.Contains(secret)&&!spectator.forge.players[0].usableSourceIds.Contains(handId)&&spectator.forge.players.All(p=>p.hand.Length==0),"spectator keeps public outside-hand eligibility only");
 yield return Present(spectator.forge,1);Expected(spectator.forge,1,"spectator strict public source display");
 foreach(int viewer in new[]{1,2}){
  var own=Native(Packet("exile/before.json"),viewer);var other=Native(Packet("exile/before.json"),3-viewer);string original=JsonUtility.ToJson(own);
  ui.PresentReplay(own,viewer,new[]{"玩家一","玩家二"},false,other);yield return Wait(.2f);Expected(own,viewer,"new replay preserves frozen eligibility");
  Check(JsonUtility.ToJson(own)==original,"replay leaves source recording immutable viewer "+viewer);
  var legacy=Clone(own);var legacyOther=Clone(other);foreach(var p in legacy.players)p.usableSourceIds=null;foreach(var p in legacyOther.players)p.usableSourceIds=null;
  ui.PresentReplay(legacy,viewer,new[]{"玩家一","玩家二"},false,legacyOther);yield return Wait(.2f);Expected(own,viewer,"legacy replay recovers positive native options");
  legacy.awaitingInput=false;legacy.request=null;ui.PresentReplay(legacy,viewer,new[]{"玩家一","玩家二"},false);yield return Wait(.2f);
  Check(SourceIds(false,legacy).Length==0&&SourceIds(true,legacy).Length==0,"legacy permission without proof never guesses current usability viewer "+viewer);
 }
 Check(calls==0,"eligibility display makes no native submissions");
 yield return Present(current);Expanded(true);Quiet();yield return Wait(.2f);ScreenCapture.CaptureScreenshot(W+"hand-strict-source-eligible.png");yield return Wait(.3f);
 Restore();Save("COMPLETE");
}
System.Collections.IEnumerator Guard(){var it=Run();for(int i=0;i<100000;i++){bool next=false;object value=null;try{next=it.MoveNext();if(next)value=it.Current;}catch(Exception e){failed++;results.Add(new{exception=e.ToString()});Restore();Save("ERROR");yield break;}if(!next)break;yield return value;}}
Save("STARTING");ui.StartCoroutine(Guard());return "Strict sources from recorded native priority/payment transitions";
