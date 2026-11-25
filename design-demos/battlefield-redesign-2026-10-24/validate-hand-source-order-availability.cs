const string W="D:/project/MTGB/design-demos/battlefield-redesign-2026-10-24/";
var ui=UnityEngine.Object.FindFirstObjectByType<ForgeBattlefieldController>();
if(ui==null)ui=new GameObject("RedesignPreview").AddComponent<ForgeBattlefieldController>();
if(!Application.isPlaying||UnityEngine.SceneManagement.SceneManager.GetActiveScene().name!=ForgeBattlefieldController.SceneName)return "Open the duel battlefield in Play Mode.";
var flags=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance;
var results=new System.Collections.Generic.List<object>();int checks=0,failed=0,calls=0,last=-1;string oldLanguage=GamePreferences.Language;float oldScale=GamePreferences.TextScale;
void Check(bool pass,string context){checks++;if(!pass)failed++;results.Add(new{pass,context});}
void Save(string status){var method=Type.GetType("Newtonsoft.Json.JsonConvert, Newtonsoft.Json").GetMethod("SerializeObject",new[]{typeof(object)});System.IO.File.WriteAllText(W+"hand-source-order-availability-validation.json",(string)method.Invoke(null,new object[]{new{status,checks,failed,scope="Copied-scene display adapters and recorded native choice consumers; no live network or new engine effect claims",results}}));}
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
int Id(ForgeBattlefieldCardPointer p)=>int.Parse(p.name.Substring(5));
ForgeBattlefieldCardPointer Card(bool other,int id)=>Cards(other).FirstOrDefault(p=>Id(p)==id);
bool Glow(bool other,int id)=>Card(other,id)?.GetComponentInChildren<ForgePlayableGlow>()!=null;
ForgeSeatView PrioritySample(bool enabled=true,bool uncertain=false){
 var v=FourSources();v.awaitingInput=true;v.prioritySeat=1;v.request=new ForgeRequest{requestId=880001,seat=1,playerSeat=1,kind="InputPassPriority",message="选择操作",options=new[]{
 new ForgeOption{id="card:"+v.players[0].hand[0].id,label=v.players[0].hand[0].name,disabled=!enabled,requiresPaymentCheck=uncertain},
 new ForgeOption{id="card:"+v.playableCommand[0].id,label=v.playableCommand[0].name,disabled=!enabled,requiresPaymentCheck=uncertain},
 new ForgeOption{id="pass",label="下一步"}
 }};return v;
}
void NormalCards(string context){foreach(bool other in new[]{false,true})foreach(var p in Cards(other)){
 var c=p.GetComponent<CanvasGroup>();if(c.alpha>.02f){var v=(ForgeSeatView)typeof(ForgeBattlefieldController).GetField("view",flags).GetValue(ui);var card=(other?Array.Empty<ForgeCardView>():v.players[0].hand.Concat(v.playableCommand??Array.Empty<ForgeCardView>()).ToArray()).FirstOrDefault(x=>x.id==Id(p));if(other||card!=null)Check(Mathf.Abs(c.alpha-1)<.001f,context+" hand/commander not dimmed "+other+" "+p.name);}
}}
System.Collections.IEnumerator Run(){
 while(!GamePreferences.Ready)yield return null;yield return Wait(.2f);
 foreach(var config in new[]{(1920,1080,"zh-CN",1f),(1024,768,"ja-JP",1.3f)}){
  Resize(config.Item1,config.Item2);GamePreferences.SetLanguage(config.Item3);GamePreferences.SetScale(config.Item4);yield return null;while(!GamePreferences.Ready)yield return null;yield return Wait(.2f);
  var v=PrioritySample();yield return Present(v);Expanded(true);Quiet();yield return Wait(.2f);
  foreach(bool other in new[]{false,true}){
   var titles=Tab(other,"Hand").transform.parent.GetComponentsInChildren<UnityEngine.UI.Button>().OrderBy(b=>((RectTransform)b.transform).anchoredPosition.x).Select(b=>b.name).ToArray();
   Check(titles.SequenceEqual(new[]{"Source_Hand","Source_Graveyard","Source_Exile","Source_Command"}),config+" requested physical header order "+other);
   Check(Tab(other,"Hand").transform.parent.GetComponentsInChildren<UnityEngine.UI.Text>().All(t=>t.preferredWidth<=t.rectTransform.rect.width+.5f),config+" labels fit "+other);
  }
  Check(Glow(false,v.players[0].hand[0].id)&&Glow(false,v.playableCommand[0].id),config+" actionable hand and commander glow");
  Check(!Glow(false,v.players[0].hand[1].id),config+" unavailable hand does not glow");
  NormalCards(config.ToString());
  foreach(string zone in new[]{"Hand","Command"}){var nav=Nav(false,zone);foreach(float move in new[]{100000f,-200000f,100000f}){nav.MoveBy(move);Quiet();yield return Wait(.05f);NormalCards(config+" page "+zone);}}
  var disabled=PrioritySample(false);yield return Present(disabled);Check(!Glow(false,disabled.players[0].hand[0].id)&&!Glow(false,disabled.playableCommand[0].id),config+" disabled actions do not glow");NormalCards(config+" disabled");
  var uncertain=PrioritySample(true,true);yield return Present(uncertain);Check(!Glow(false,uncertain.players[0].hand[0].id)&&!Glow(false,uncertain.playableCommand[0].id),config+" unresolved affordability does not glow");
  var retained=PrioritySample();retained.playableCommand=Array.Empty<ForgeCardView>();retained.players[0].commandCards=new[]{Clone(v.playableCommand[0])};yield return Present(retained);
  Check(Card(false,v.playableCommand[0].id)!=null,config+" idle commander retained from public command zone");NormalCards(config+" public commander");
  var current=PrioritySample();yield return Present(current);
  int exile=current.playableExile[0].id,grave=current.playableGraveyard[0].id,otherExile=current.players[1].publicPlayableExile[0].id;
  Check(Card(false,exile)!=null&&Card(false,grave)!=null&&Card(true,otherExile)!=null,config+" permitted sources visible");
  var revoked=Clone(current);revoked.playableExile=revoked.playableGraveyard=Array.Empty<ForgeCardView>();revoked.players[1].publicPlayableExile=revoked.players[1].publicPlayableGraveyard=Array.Empty<ForgeCardView>();yield return Present(revoked,1,false);
  Check(Card(false,exile)==null&&Card(false,grave)==null&&Card(true,otherExile)==null,config+" permission loss removes source cards and hit surfaces");
  Check(Card(false,current.players[0].hand[0].id)!=null&&Card(false,current.playableCommand[0].id)!=null,config+" ordinary hand and commander remain after permission loss");
  yield return Present(current,1,false);Check(Card(false,exile)!=null&&Card(false,grave)!=null,config+" permission restoration repopulates sources");
 }
 Check(calls==0,"presentation changes submit no native actions");
 GamePreferences.SetLanguage("zh-CN");GamePreferences.SetScale(1);Resize(1920,1080);yield return null;while(!GamePreferences.Ready)yield return null;yield return Wait(.2f);yield return Present(PrioritySample());Expanded(true);Quiet();yield return Wait(.2f);ScreenCapture.CaptureScreenshot(W+"hand-source-order-normal-colours.png");yield return Wait(.3f);
 Restore();Save("COMPLETE");
}
System.Collections.IEnumerator Guard(){var it=Run();for(int i=0;i<100000;i++){bool next=false;object value=null;try{next=it.MoveNext();if(next)value=it.Current;}catch(Exception e){failed++;results.Add(new{exception=e.ToString()});Restore();Save("ERROR");yield break;}if(!next)break;yield return value;}}
Save("STARTING");ui.StartCoroutine(Guard());return "Order and source permission lifecycle validation";
