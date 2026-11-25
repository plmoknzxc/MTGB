const string W="D:/project/MTGB/design-demos/battlefield-redesign-2026-10-24/";
var ui=UnityEngine.Object.FindFirstObjectByType<ForgeBattlefieldController>();
if(ui==null)ui=new GameObject("RedesignPreview").AddComponent<ForgeBattlefieldController>();
if(!Application.isPlaying||UnityEngine.SceneManagement.SceneManager.GetActiveScene().name!=ForgeBattlefieldController.SceneName)return "Open the duel battlefield in Play Mode.";
var flags=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance;
bool savedUiEnabled=ui.enabled;var inputModule=UnityEngine.EventSystems.EventSystem.current.currentInputModule;bool savedModuleEnabled=inputModule!=null&&inputModule.enabled;ui.enabled=false;if(inputModule!=null)inputModule.enabled=false;
var results=new System.Collections.Generic.List<object>();int checks=0,failed=0,calls=0,last=-1;string oldLanguage=GamePreferences.Language;float oldScale=GamePreferences.TextScale;
void Check(bool pass,string context){checks++;if(!pass)failed++;results.Add(new{pass,context});}
void Save(string status){var method=Type.GetType("Newtonsoft.Json.JsonConvert, Newtonsoft.Json").GetMethod("SerializeObject",new[]{typeof(object)});System.IO.File.WriteAllText(W+"land-stacks-ui.json",(string)method.Invoke(null,new object[]{new{status,checks,failed,scope="Copied-scene display adapters and recorded native choice consumers; no live network or new engine effect claims",results}}));}
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
void Restore(){ui.enabled=savedUiEnabled;if(inputModule!=null)inputModule.enabled=savedModuleEnabled;GamePreferences.SetLanguage(oldLanguage);GamePreferences.SetScale(oldScale);sizeProperty.SetValue(gameView,0);var labels=(string[])group.GetType().GetMethod("GetDisplayTexts").Invoke(group,null);for(int i=labels.Length-1;i>=0;i--)if(labels[i].StartsWith("MTGB hand sections"))group.GetType().GetMethod("RemoveCustomSize").Invoke(group,new object[]{i});}
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

var catalog=(System.Collections.Generic.Dictionary<string,CardDefinition>)typeof(ForgeBattlefieldController).GetField("catalog",flags).GetValue(ui);
ForgeCardView Commander(string key,int id,int seat){var d=catalog[key];return new ForgeCardView{id=id,ownerSeat=seat,zone="Command",commander=true,name=d.cardName,printingKey=key,manaCost=d.manaCost,currentType=d.GetTypeLine(),currentRules=d.description,power=d.attack,toughness=d.defence};}
ForgeSeatView Packed(int h,int gy,int ex,bool background=false,bool companion=true){
 var v=Rich();v.request.requestId=DateTime.UtcNow.Ticks;ForgeCardView[] Copies(ForgeCardView c,int n,int start)=>Enumerable.Range(0,n).Select(i=>{var t=Clone(c);t.id=start+i;return t;}).ToArray();
 v.players[0].hand=Copies(v.players[0].hand[0],h,97000);v.players[0].handCount=h;
 v.playableGraveyard=Copies(v.playableGraveyard[0],gy,92000);v.playableExile=Copies(v.playableExile[0],ex,91000);
 v.playableCommand=new[]{Commander(background?"CLB/2":"C16/46",93000,1),Commander(background?"CLB/221":"C16/48",93001,1)};
 var commands=v.playableCommand.ToList();
 if(companion){var c=Clone(Read("D:/project/MTGB/.tools/companion-editor-1015/pay/1791997047658979700/command-entry-seat1.json").playableCommand.First());c.id=93900;c.ownerSeat=1;commands.Add(c);}
 v.players[0].commandCards=commands.ToArray();
 v.players[0].usableSourceIds=v.playableGraveyard.Concat(v.playableExile).Select(c=>c.id).ToArray();
 v.players[1].hand=Array.Empty<ForgeCardView>();v.players[1].handCount=h;
 v.players[1].publicPlayableGraveyard=Copies(v.playableGraveyard.FirstOrDefault()??v.players[0].hand.FirstOrDefault()??Commander("C16/46",93000,1),gy,95000);foreach(var c in v.players[1].publicPlayableGraveyard){c.zone="Graveyard";c.ownerSeat=2;}
 v.players[1].publicPlayableExile=Copies(v.playableExile.FirstOrDefault()??Commander("C16/46",93000,1),ex,94000);foreach(var c in v.players[1].publicPlayableExile){c.zone="Exile";c.ownerSeat=2;}
 v.players[1].commandCards=commands.Select(c=>{var t=Clone(c);t.id+=10000;t.ownerSeat=2;return t;}).ToArray();
 v.players[1].usableSourceIds=v.players[1].publicPlayableGraveyard.Concat(v.players[1].publicPlayableExile).Select(c=>c.id).ToArray();
 v.request.options=new[]{new ForgeOption{id="ok",label="Pass priority"},new ForgeOption{id="cancel",label="Undo mana"}}
 .Concat(v.players[0].hand.Concat(v.playableGraveyard).Concat(v.playableExile).Concat(commands).Select(c=>new ForgeOption{id="card:"+c.id,label=c.name})).ToArray();
 return v;
}
Vector2? Exposed(ForgeBattlefieldCardPointer card){var b=Bounds((RectTransform)card.transform);foreach(float x in new[]{.995f,.005f,.98f,.02f,.95f,.05f,.5f})foreach(float y in new[]{.5f,.75f,.9f}){var p=new Vector2(Mathf.Lerp(b.xMin,b.xMax,x),Mathf.Clamp(Mathf.Lerp(b.yMin,b.yMax,y),2,Screen.height-2));var hit=Hit(p);if(hit!=null&&hit.GetComponentInParent<ForgeBattlefieldCardPointer>()==card)return p;}return null;}

ForgeSeatView Native(string phase){var message=JsonUtility.FromJson<ForgeWireMessage>(System.IO.File.ReadAllText("D:/project/MTGB/.tools/redesign-lands-1026/"+phase+".json"));var state=message.views[0];state.awaitingInput=message.request?.seat==1;state.request=message.request;return state;}
ForgeSeatView Preview()=>Read("D:/project/MTGB/Assets/Scenes/PreviewData/BattlefieldRedesignSample.json");
(ForgeCardView card,RectTransform root,int count,string group,UnityEngine.UI.Button button)[] Lands(bool own){
 var entries=(System.Collections.IDictionary)typeof(ForgeBattlefieldController).GetField("arenaCards",flags).GetValue(ui);
 return entries.Values.Cast<object>().Select(o=>{var t=o.GetType();return(card:(ForgeCardView)t.GetField("card").GetValue(o),root:(RectTransform)t.GetField("root").GetValue(o),count:(int)t.GetField("representedCount").GetValue(o),group:(string)t.GetField("groupKey").GetValue(o),button:(UnityEngine.UI.Button)t.GetField("button").GetValue(o));})
 .Where(x=>x.root.gameObject.activeInHierarchy&&x.group.StartsWith(own?"YourLands:":"OpponentLands:")).ToArray();
}
System.Collections.IEnumerator Run(){
 for(int n=0;n<200&&!GamePreferences.Ready;n++)yield return Wait(.05f);
 if(inputModule==null){inputModule=UnityEngine.EventSystems.EventSystem.current.currentInputModule;savedModuleEnabled=inputModule!=null&&inputModule.enabled;}ui.enabled=false;if(inputModule!=null)inputModule.enabled=false;
 foreach(var cfg in new[]{new{w=1920,h=1080,lang="zh-CN",scale=1f},new{w=1280,h=720,lang="en-US",scale=1.3f},new{w=1024,h=768,lang="ja-JP",scale=1.3f}}){
  Resize(cfg.w,cfg.h);GamePreferences.SetLanguage(cfg.lang);GamePreferences.SetScale(cfg.scale);
  for(int n=0;n<200&&!GamePreferences.Ready;n++)yield return Wait(.05f);yield return Wait(.2f);
  Check(GamePreferences.Language==cfg.lang&&GamePreferences.Ready,cfg.lang+" actual locale ready");
  var v=Preview();v.request.requestId=DateTime.UtcNow.Ticks;yield return Present(v);Expanded(false);yield return Wait();
  foreach(bool own in new[]{true,false}){
   var cards=Lands(own);var physical=v.players.First(p=>p.seat==(own?1:2)).battlefield.Where(c=>c.land&&!c.creature).ToArray();
   Check(cards.Sum(c=>c.count)==physical.Length,cfg.lang+" every land represented "+own);
   Check(cards.Any(c=>c.count>1),cfg.lang+" duplicate lands collapse "+own);
   foreach(var card in cards){
    var tint=card.root.Find("Visual/Border").GetComponent<BattlefieldLandFrameTint>();
    Check(tint!=null,cfg.lang+" land frame "+own+"/"+card.card.name);
    if(card.count>1)Check(card.root.Find("Visual/LandStackBack_1")!=null,cfg.lang+" actual rear layer "+own+"/"+card.card.name);
    var b=Bounds(card.root);
    Check(b.xMin>=0&&b.xMax<=Screen.width,cfg.lang+" land within width "+own+"/"+card.card.name);
   }
   var plate=Root().Find(own?"PlayerPlate":"OpponentPlate");var mana=Root().Find(own?"ManaPoolChips":"PublicManaPool");
   Check(!cards.Any(c=>Overlap(Bounds(c.root),Bounds((RectTransform)plate))||Overlap(Bounds(c.root),Bounds((RectTransform)mana))),cfg.lang+" lands clear player and mana "+own);
  }
  foreach(int mask in new[]{0,2,3,7}){
   var item=Lands(true).First(c=>c.card.landManaColors==mask);var tint=item.root.Find("Visual/Border").GetComponent<BattlefieldLandFrameTint>();
   var tf=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance;
   var a=(Color)typeof(BattlefieldLandFrameTint).GetField("left",tf).GetValue(tint);var b=(Color)typeof(BattlefieldLandFrameTint).GetField("right",tf).GetValue(tint);
   Check(mask==3?a!=b:a==b,cfg.lang+" single/dual/multi frame "+mask);
  }
  var before=Native("before");before.request.requestId=DateTime.UtcNow.Ticks;yield return Present(before);Expanded(false);yield return Wait();
  var group=Lands(true).First(c=>c.card.name=="Hallowed Fountain"&&c.count==3);
  int prior=calls;Check(Click(group.button),cfg.lang+" grouped land raycast expands");yield return Wait();
  Check(calls==prior,cfg.lang+" expanding is not a mana activation");
  var single=Lands(true).First(c=>c.card.id==group.card.id&&c.count==1);
  Check(Click(single.button),cfg.lang+" exact member raycast");yield return Wait();
  Check(calls==prior+1&&before.request.options[last].id=="card:"+single.card.id,cfg.lang+" exact native physical id submitted");
  var after=Native("after-mana");after.request.requestId=DateTime.UtcNow.Ticks;yield return Present(after);yield return Wait();
  Check(Lands(true).Any(c=>c.card.name=="Hallowed Fountain"&&c.card.tapped&&c.count==2),cfg.lang+" activated tapped lands regroup");
  Check(Lands(true).Any(c=>c.card.name=="Hallowed Fountain"&&!c.card.tapped&&c.count==1),cfg.lang+" untapped land remains separate");
  var linked=after.players[0].battlefield.First(c=>c.landPresentationKnown&&!c.landStackable);
  Check(Lands(true).Any(c=>c.card.id==linked.id&&c.count==1),cfg.lang+" linked object never hidden in stack");
  // Same eligibility but different printing may share a land stack.
  var left=Clone(after.players[0].battlefield.First(c=>c.name=="Hallowed Fountain"&&c.tapped));var right=Clone(left);right.id+=30000;right.printingKey="RNA/251";
  after.players[0].battlefield=new[]{left,right};after.request.options=new[]{new ForgeOption{id="ok",label="Pass"}};after.request.requestId=DateTime.UtcNow.Ticks;yield return Present(after);yield return Wait();
  Check(Lands(true).Length==1&&Lands(true)[0].count==2,cfg.lang+" same land alternate artwork groups");
  right.counters=new[]{new ForgeCardCounter{kind="CHARGE",count=1}};after.request.requestId=DateTime.UtcNow.Ticks;yield return Present(after);yield return Wait();
  Check(Lands(true).Length==2,cfg.lang+" differing counters remain separate");
  right.counters=Array.Empty<ForgeCardCounter>();after.stackItems=new[]{new ForgeStackItem{id=45001,targets=new[]{new ForgeStackTarget{kind="card",zone="Battlefield",id=left.id,ownerSeat=1,card=left}}}};after.request.requestId=DateTime.UtcNow.Ticks;yield return Present(after);yield return Wait();
  Check(Lands(true).Length==2,cfg.lang+" confirmed targeted land stays individually visible");
  after.stackItems=Array.Empty<ForgeStackItem>();after.awaitingInput=true;after.request=new ForgeRequest{requestId=DateTime.UtcNow.Ticks,kind="InputSelectEntitiesFromList",seat=1,playerSeat=1,minCount=1,maxCount=1,options=new[]{new ForgeOption{id="card:"+left.id,card=left,label=left.name},new ForgeOption{id="card:"+right.id,card=right,label=right.name}}};
  yield return Present(after);yield return Wait();
  Check(Lands(true).Length==2,cfg.lang+" individual choice exposes both physical lands");
 }
 var profile=typeof(ForgeBattlefieldController).GetField("redesignProfile",flags).GetValue(ui);
 var old=Native("after-mana");old.request.requestId=DateTime.UtcNow.Ticks;typeof(ForgeBattlefieldController).GetField("redesignProfile",flags).SetValue(ui,null);
 yield return Present(old);yield return Wait();
 Check(!Lands(true).Any(c=>c.card.name=="Hallowed Fountain"&&c.card.tapped&&c.count>1),"original profile keeps existing grouping");
 Check(!Lands(true).Any(c=>c.root.GetComponentInChildren<BattlefieldLandFrameTint>()!=null),"original profile has no new frame material");
 typeof(ForgeBattlefieldController).GetField("redesignProfile",flags).SetValue(ui,profile);
 Restore();GamePreferences.SetLanguage("zh-CN");GamePreferences.SetScale(1);sizeProperty.SetValue(gameView,3);
 for(int n=0;n<200&&!GamePreferences.Ready;n++)yield return Wait(.05f);yield return Wait(.3f);
 yield return Present(Preview());Expanded(false);Quiet();yield return Wait(.4f);Save("COMPLETE");
}
System.Collections.IEnumerator Guard(){var it=Run();for(int i=0;i<10000;i++){bool next=false;object value=null;try{next=it.MoveNext();if(next)value=it.Current;}catch(Exception e){failed++;results.Add(new{exception=e.ToString()});Restore();Save("ERROR");yield break;}if(!next)break;yield return value;}}
Save("STARTING");ui.StartCoroutine(Guard());return "New-scene land stacks, mana frames and exact native member selection";

