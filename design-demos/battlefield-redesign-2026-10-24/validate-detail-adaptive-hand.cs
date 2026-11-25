const string W="D:/project/MTGB/design-demos/battlefield-redesign-2026-10-24/";
var ui=UnityEngine.Object.FindFirstObjectByType<ForgeBattlefieldController>();
if(ui==null)ui=new GameObject("RedesignPreview").AddComponent<ForgeBattlefieldController>();
if(!Application.isPlaying||UnityEngine.SceneManagement.SceneManager.GetActiveScene().name!=ForgeBattlefieldController.SceneName)return "Open the duel battlefield in Play Mode.";
var flags=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance;
bool savedUiEnabled=ui.enabled;var inputModule=UnityEngine.EventSystems.EventSystem.current.currentInputModule;bool savedModuleEnabled=inputModule!=null&&inputModule.enabled;ui.enabled=false;if(inputModule!=null)inputModule.enabled=false;
var testEventSystem=UnityEngine.Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>();bool savedEventEnabled=testEventSystem.enabled;testEventSystem.enabled=false;
var results=new System.Collections.Generic.List<object>();int checks=0,failed=0,calls=0,last=-1;string oldLanguage=GamePreferences.Language;float oldScale=GamePreferences.TextScale;
void Check(bool pass,string context){checks++;if(!pass)failed++;results.Add(new{pass,context});}
void Save(string status){var method=Type.GetType("Newtonsoft.Json.JsonConvert, Newtonsoft.Json").GetMethod("SerializeObject",new[]{typeof(object)});System.IO.File.WriteAllText(W+"detail-adaptive-hand-spacing.json",(string)method.Invoke(null,new object[]{new{status,checks,failed,scope="Copied-scene display adapters and recorded native choice consumers; no live network or new engine effect claims",results}}));}
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

ForgeStackItem Paid(string file)=>JsonUtility.FromJson<ForgeWireMessage>(System.IO.File.ReadAllText("D:/project/MTGB/.tools/stack-payment-1025/"+file+"/stack.json")).views[0].stackItems.First(e=>e.kind=="spell");
ForgeSeatView Case(int n){
 var v=Sample();v.request.requestId=DateTime.UtcNow.Ticks;
 v.players[0].sideboardCards=v.players[0].hand.Take(2).ToArray();
 if(n==1){var a=Clone(Paid("hybrid"));var b=Clone(Paid("gift"));a.id=8101;b.id=8102;v.stackItems=new[]{a,b,Clone(v.stackItems[1])};}
 if(n==2){var a=Clone(v.stackItems[0]);a.kind="trigger";a.description="Draw a card.";a.rulesText="Draw a card.";a.castingChoices=Array.Empty<ForgeCastingChoice>();a.targets=Array.Empty<ForgeStackTarget>();v.stackItems=new[]{a};}
 if(n==3){var a=Clone(v.stackItems[0]);var target=Clone(a.targets[0]);a.targets=Enumerable.Range(0,5).Select(i=>{var t=Clone(target);t.stackId=v.stackItems[1].id;t.id=900+i;return t;}).ToArray();v.stackItems=new[]{a,v.stackItems[1]};}
 v.stackCount=v.stackItems.Length;v.stack=v.stackItems.Select(e=>e.description).ToArray();return v;
}
object Stack(int index)=>((System.Collections.IDictionary)typeof(ForgeBattlefieldController).GetField("stackCards",flags).GetValue(ui)).Values.Cast<object>().First(i=>((RectTransform)i.GetType().GetField("row").GetValue(i)).name=="StackEntry_"+index);
void Hover(int index){var item=Stack(index);typeof(ForgeBattlefieldController).GetField("hoveredStack",flags).SetValue(ui,item);typeof(ForgeBattlefieldController).GetMethod("ShowStackHover",flags).Invoke(ui,new[]{item});}

ForgeSeatView HandOnly(int own,int other){
 var v=Sample();v.request.requestId=DateTime.UtcNow.Ticks;v.awaitingInput=false;
 var baseCard=v.players[0].hand[0];v.players[0].hand=Enumerable.Range(0,own).Select(i=>{var c=Clone(baseCard);c.id=76000+i;return c;}).ToArray();v.players[0].handCount=own;
 v.players[1].hand=Array.Empty<ForgeCardView>();v.players[1].handCount=other;
 v.playableGraveyard=v.playableExile=Array.Empty<ForgeCardView>();
 foreach(var p in v.players){p.commandCards=p.publicPlayableGraveyard=p.publicPlayableExile=Array.Empty<ForgeCardView>();p.usableSourceIds=Array.Empty<int>();}
 return v;
}
float DockWidth(bool other)=>((RectTransform)Root().Find(other?"RedesignOtherHandBackplate":"RedesignHandBackplate")).rect.width;
System.Collections.IEnumerator Run(){
 for(int i=0;i<200&&!GamePreferences.Ready;i++)yield return Wait(.05f);
 if(inputModule==null){inputModule=testEventSystem.currentInputModule;savedModuleEnabled=inputModule!=null&&inputModule.enabled;}ui.enabled=false;if(inputModule!=null)inputModule.enabled=false;
 foreach(var cfg in new[]{new{w=1920,h=1080,lang="zh-CN",scale=1f},new{w=1280,h=720,lang="en-US",scale=1.3f},new{w=1024,h=768,lang="ja-JP",scale=1.3f}}){
  Resize(cfg.w,cfg.h);GamePreferences.SetLanguage(cfg.lang);GamePreferences.SetScale(cfg.scale);
  for(int i=0;i<200&&!GamePreferences.Ready;i++)yield return Wait(.05f);yield return Wait(.3f);
  Check(GamePreferences.Ready&&GamePreferences.Language==cfg.lang,cfg.lang+" actual locale ready");
  float[] previous={0,0};
  foreach(int n in new[]{0,1,2,5,12}){
   yield return Present(HandOnly(n,n));Expanded(true);yield return Wait();
   foreach(bool other in new[]{false,true}){
    string tag=cfg.lang+"/"+n+"/"+other;var plate=(RectTransform)Root().Find(other?"RedesignOtherHandBackplate":"RedesignHandBackplate");
    Check(plate.gameObject.activeSelf==(n>0),tag+" no empty tray");
    Check(Cards(other).Length==n,tag+" every physical hand card retained");
    if(n==0)continue;
    Check(Cards(other).All(IsShown),tag+" every card exposed");
    Check(DockWidth(other)>=previous[other?1:0]-.1f,tag+" occupied width grows or compresses at lane limit");
    if(cfg.w==1920&&n==2)Check(DockWidth(other)<450,tag+" two cards no stretched empty width");
    if(cfg.w==1920&&n==5)Check(DockWidth(other)<1000,tag+" five complete card faces without unnecessary empty width");
    if(cfg.w==1920&&(n==2||n==5)){
     var bounds=Cards(other).Select(c=>Bounds((RectTransform)c.transform)).OrderBy(b=>b.center.x).ToArray();
     Check(Enumerable.Range(1,bounds.Length-1).All(i=>bounds[i].xMin>=bounds[i-1].xMax-1),tag+" sufficient lane shows complete faces without overlap");
    }
    previous[other?1:0]=DockWidth(other);
    var b=Bounds(plate);Check(b.xMin>=-1&&b.xMax<=Screen.width+1,tag+" tray within screen");
    var root=(RectTransform)Root().Find(other?"OpponentHandRoot":"PlayerHandRoot");var tabs=(RectTransform)Root().Find(other?"OtherHandSourceTabs":"HandSourceTabs");
    Check(Mathf.Abs(Bounds(root).center.x-Bounds(plate).center.x)<2&&Mathf.Abs(Bounds(tabs).center.x-Bounds(plate).center.x)<2,tag+" cards captions frame centered together");
   }
   Clearance(cfg.lang+"/"+n);
   Expanded(false);yield return Wait();
   Check(!Cards(false).Any(IsShown),cfg.lang+"/"+n+" tuck hides own cards");
   Check(Cards(true).All(IsShown),cfg.lang+"/"+n+" own movement leaves opponent visible");
  }
  var packed=Packed(9,5,4);foreach(var p in packed.players)p.usableSourceIds=p.usableSourceIds.Concat(p.commandCards.Select(c=>c.id)).ToArray();
  yield return Present(packed);Expanded(true);yield return Wait();
  foreach(bool other in new[]{false,true})Check(Cards(other).Length>=19,cfg.lang+" many sources retain all cards "+other);
  yield return Present(Case(3));Expanded(false);Hover(0);yield return Wait();
  var panel=(RectTransform)Root().Find("InspectPanel");var title=(RectTransform)panel.Find("Name");var original=(RectTransform)panel.Find("OriginalState");var cost=(RectTransform)panel.Find("CostRow");
  Check(-original.anchoredPosition.x<=8.1f&&-original.anchoredPosition.y<=8.1f,cfg.lang+" original state at top right edge");
  Check(cost.gameObject.activeSelf,cfg.lang+" valid casting cost beside title");
  Check(Mathf.Abs(cost.anchoredPosition.y-title.anchoredPosition.y)<1,cfg.lang+" name and mana share row");
  Check(!Overlap(Bounds(title),Bounds(cost)),cfg.lang+" title and cost do not overlap");
  var content=(RectTransform)panel.Find("Rules/RuleScroll/Content");var body=content.GetComponentInChildren<TMPro.TextMeshProUGUI>();var target=(RectTransform)typeof(ForgeBattlefieldController).GetField("stackTargetDetails",flags).GetValue(ui);
  Check(body.fontSize>=20&&Mathf.Abs(body.lineSpacing-5)<.1f,cfg.lang+" deck detail body typography");
  Check(panel.Find("Name").GetComponent<UnityEngine.UI.Text>().fontStyle==FontStyle.Bold,cfg.lang+" deck detail title bold");
  Check(!body.isTextTruncated,cfg.lang+" full effect not truncated");
  Check(target.parent==content&&-target.anchoredPosition.y>=-body.rectTransform.anchoredPosition.y+body.rectTransform.rect.height,cfg.lang+" targets after complete rule text");
  var scroll=content.parent.GetComponent<UnityEngine.UI.ScrollRect>();scroll.verticalNormalizedPosition=0;yield return Wait();
  Check(Overlap(Bounds(target),Bounds(scroll.viewport)),cfg.lang+" scroll reaches targets");
  Check(Bounds(panel).xMin>=-1&&Bounds(panel).xMax<=Screen.width+1&&Bounds(panel).yMin>=-1&&Bounds(panel).yMax<=Screen.height+1,cfg.lang+" full detail remains on screen");
  var land=Sample().players[0].battlefield.First(c=>c.land);typeof(ForgeBattlefieldController).GetField("hoveredStack",flags).SetValue(ui,null);
  typeof(ForgeBattlefieldController).GetMethod("InspectIntegrated",flags).Invoke(ui,new object[]{land});yield return Wait();
  Check(!Root().Find("InspectPanel/CostRow").gameObject.activeSelf,cfg.lang+" no mana cost on land title");

  var catalogNow=(System.Collections.Generic.Dictionary<string,CardDefinition>)typeof(ForgeBattlefieldController).GetField("catalog",flags).GetValue(ui);
  var dual=catalogNow.Values.FirstOrDefault(d=>d.forgeSplitType=="Modal"&&d.features?.Any(f=>f.faceIndex==1)==true);
  Check(dual!=null,cfg.lang+" real dual face definition available");
  if(dual!=null){
   var c=Clone(Sample().players[0].hand[0]);c.id=88501;c.printingKey=dual.PrintingKey;c.name=dual.cardName;c.originalManaCost=c.manaCost=dual.manaCost;c.originalRules=c.currentRules=dual.description;
   typeof(ForgeBattlefieldController).GetMethod("InspectIntegrated",flags).Invoke(ui,new object[]{c});yield return Wait();
   var rev=(RectTransform)panel.Find("ReverseArt");
   Check(rev!=null&&rev.gameObject.activeSelf,cfg.lang+" both faces preserved");
   Check(Bounds(rev).xMin>=Bounds(panel).xMin&&Bounds(rev).xMax<=Bounds(panel).xMax,cfg.lang+" reverse art inside wider detail");
  }
  var changed=Clone(Sample().players[0].hand[1]);changed.id=88502;changed.manaCost="{7}{B}{B}{B}{B}";
  typeof(ForgeBattlefieldController).GetMethod("InspectIntegrated",flags).Invoke(ui,new object[]{changed});yield return Wait();
  var nameLabel=panel.Find("Name").GetComponent<UnityEngine.UI.Text>();nameLabel.text="A Very Long Card Name With Several Distinct Words";
  typeof(ForgeBattlefieldController).GetMethod("LayoutInspectPanel",flags).Invoke(ui,new object[]{changed,null,false});yield return Wait();
  title=(RectTransform)panel.Find("Name");cost=(RectTransform)panel.Find("CostRow");
  Check(nameLabel.preferredHeight<=title.rect.height+1,cfg.lang+" long name wraps without truncation beside cost");
  Check(!Overlap(Bounds(title),Bounds(cost)),cfg.lang+" long name separate from five cost symbols");
  Check(Bounds(cost).xMax<=Bounds(panel).xMax-1,cfg.lang+" changed cost within detail");
  Check(panel.Find("CostRow").GetComponentsInChildren<UnityEngine.UI.RawImage>().Count(i=>i.gameObject.activeSelf)==5,cfg.lang+" all changed symbols retained");
  Check(((RectTransform)panel.Find("Footer")).anchoredPosition.y>((RectTransform)panel.Find("Rules")).anchoredPosition.y,cfg.lang+" stats before rules as deck detail");
  var originalButton=panel.Find("OriginalState").GetComponent<UnityEngine.UI.Button>();originalButton.onClick.Invoke();yield return Wait();
  var headerSymbols=panel.Find("CostRow").GetComponentInChildren<TMPro.TextMeshProUGUI>(true);
  Check(!headerSymbols.tintAllSprites,cfg.lang+" original state restores original cost tint");

 }
 Restore();GamePreferences.SetLanguage("zh-CN");GamePreferences.SetScale(1);sizeProperty.SetValue(gameView,3);
 for(int i=0;i<200&&!GamePreferences.Ready;i++)yield return Wait(.05f);yield return Wait(.3f);
 yield return Present(HandOnly(5,2));Expanded(true);Quiet();yield return Wait(.3f);Save("COMPLETE");
}
System.Collections.IEnumerator Guard(){var it=Run();for(int i=0;i<10000;i++){bool next=false;object value=null;try{next=it.MoveNext();if(next)value=it.Current;}catch(Exception e){failed++;results.Add(new{exception=e.ToString()});Restore();Save("ERROR");yield break;}if(!next)break;yield return value;}}
Save("STARTING");ui.StartCoroutine(Guard());return "Detail typography and adaptive hand tray display regression";

