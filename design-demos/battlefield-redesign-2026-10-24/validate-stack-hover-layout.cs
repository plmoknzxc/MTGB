const string W="D:/project/MTGB/design-demos/battlefield-redesign-2026-10-24/";
var ui=UnityEngine.Object.FindFirstObjectByType<ForgeBattlefieldController>();
if(ui==null)ui=new GameObject("RedesignPreview").AddComponent<ForgeBattlefieldController>();
if(!Application.isPlaying||UnityEngine.SceneManagement.SceneManager.GetActiveScene().name!=ForgeBattlefieldController.SceneName)return "Open the duel battlefield in Play Mode.";
var flags=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance;
var results=new System.Collections.Generic.List<object>();int checks=0,failed=0,calls=0,last=-1;string oldLanguage=GamePreferences.Language;float oldScale=GamePreferences.TextScale;
void Check(bool pass,string context){checks++;if(!pass)failed++;results.Add(new{pass,context});}
void Save(string status){var method=Type.GetType("Newtonsoft.Json.JsonConvert, Newtonsoft.Json").GetMethod("SerializeObject",new[]{typeof(object)});System.IO.File.WriteAllText(W+"stack-hover-layout.json",(string)method.Invoke(null,new object[]{new{status,checks,failed,scope="Copied-scene display adapters and recorded native choice consumers; no live network or new engine effect claims",results}}));}
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

string Note(ForgeStackItem e)=>(string)typeof(ForgeBattlefieldController).GetMethod("RedesignStackEffect",flags).Invoke(ui,new object[]{e,"EFFECT_SENTINEL"});
ForgeStackItem Native(string name)=>JsonUtility.FromJson<ForgeWireMessage>(System.IO.File.ReadAllText("D:/project/MTGB/.tools/stack-payment-1025/"+name+"/stack.json")).views[0].stackItems.First(e=>e.kind=="spell");
ForgeSeatView Preview(){
 var v=Sample();var entries=new[]{Clone(Native("hybrid")),Clone(Native("gift")),Clone(v.stackItems.Last())};
 for(int i=0;i<entries.Length;i++){entries[i].id=8000+i;entries[i].controllerSeat=i==1?2:1;}
 var x=entries[2];x.hasPaymentReceipt=true;x.hasXValue=true;x.xValue=4;x.payment=new ForgeStackPayment{cost="{4}{B}{G}{U}",manaCost="{4}{B}{G}{U}",spentMana="{B}{G}{U}{G}{G}{B}{U}"};
 v.stackItems=entries;v.stack=entries.Select(e=>e.description).ToArray();v.stackCount=entries.Length;return v;
}

ForgeSeatView Case(int which){
 var v=Packed(7,4,4,true);var entries=Preview().stackItems;var e=Clone(entries[which%3]);
 if(which==3){e.kind="trigger";e.description=e.rulesText="Draw a card.";e.castingChoices=Array.Empty<ForgeCastingChoice>();e.chosenModes=Array.Empty<ForgeChosenMode>();e.payment=new ForgeStackPayment();}
 if(which==4){e=Clone(entries[1]);e.targets=Enumerable.Range(0,5).Select(i=>new ForgeStackTarget{kind="card",id=910000+i,ownerSeat=i%2+1,zone="Battlefield",card=Clone(v.players[0].hand[0])}).ToArray();}
 e.id=81000+which;v.stackItems=new[]{e};v.stackCount=1;v.stack=new[]{e.description};return v;
}
void Hover(){var values=(System.Collections.IDictionary)typeof(ForgeBattlefieldController).GetField("stackCards",flags).GetValue(ui);var item=values.Values.Cast<object>().First(c=>((RectTransform)c.GetType().GetField("row").GetValue(c)).gameObject.activeInHierarchy);typeof(ForgeBattlefieldController).GetField("hoveredStack",flags).SetValue(ui,item);typeof(ForgeBattlefieldController).GetMethod("ShowStackHover",flags).Invoke(ui,new[]{item});}
System.Collections.IEnumerator Run(){
 while(!GamePreferences.Ready)yield return null;yield return Wait(.6f);
 foreach(var config in new[]{(1920,1080,"zh-CN",1f),(1280,720,"en-US",1.3f),(1024,768,"ja-JP",1.3f)}){
 Resize(config.Item1,config.Item2);GamePreferences.SetLanguage(config.Item3);GamePreferences.SetScale(config.Item4);yield return null;while(!GamePreferences.Ready)yield return null;yield return Wait(.3f);
 yield return Present(Case(0));Expanded(true);Quiet();yield return Wait(.2f);
 foreach(bool other in new[]{false,true})foreach(var z in new[]{"Hand","Graveyard","Exile","Command","Companion"}){
 var button=Tab(other,z);Check(!button.enabled&&!button.targetGraphic.raycastTarget&&button.targetGraphic.color.a>.8f,config+" "+other+" "+z+" framed caption only");
 Check(!Click(button),config+" "+other+" "+z+" cannot open list");button.onClick.Invoke();
 Check(typeof(ForgeBattlefieldController).GetField("browsedZone",flags).GetValue(ui)==null,config+" no source browser");
 }
 foreach(int which in new[]{0,1,2,3,4}){
 yield return Present(Case(which));Quiet();yield return Wait(.2f);Hover();yield return Wait(.25f);
 var panel=(RectTransform)Root().Find("InspectPanel");var art=(RectTransform)panel.Find("Art");
 Check(panel.gameObject.activeInHierarchy,config+" case"+which+" hover visible");
 Check(art.Find("ChosenSpellFace")==null||!art.Find("ChosenSpellFace").gameObject.activeSelf,config+" case"+which+" original artwork without second editable face");
 var sections=new[]{"Name","OriginalState","CostRow","Art","TypeLine","Rules","Footer","StackTargets"}.Select(n=>panel.Find(n) as RectTransform).Where(r=>r!=null&&r.gameObject.activeSelf&&r.rect.height>0).ToArray();bool ordered=true;
 for(int i=1;i<sections.Length;i++)if(Bounds(sections[i]).yMax>Bounds(sections[i-1]).yMin+1)ordered=false;
 Check(ordered,config+" case"+which+" vertically ordered sections");
 var bounds=Bounds(panel);Check(bounds.yMin>=-1&&bounds.yMax<=Screen.height+1,config+" case"+which+" whole hover fits screen: "+bounds);
 if(which==3){string rules=panel.Find("Rules").GetComponent<UnityEngine.UI.Text>().text;Check(!rules.Contains("spent to cast")&&!rules.Contains("虚妄")&&!rules.Contains("呼魂"),config+" ability text limited to current ability");}
 }
 }
 Restore();GamePreferences.SetLanguage("zh-CN");GamePreferences.SetScale(1);sizeProperty.SetValue(gameView,3);yield return null;yield return Wait(.3f);yield return Present(Case(0));Quiet();yield return Wait(.3f);Hover();yield return Wait(.4f);Save("COMPLETE");
}
System.Collections.IEnumerator Guard(){var it=Run();for(int i=0;i<100000;i++){bool next=false;object value=null;try{next=it.MoveNext();if(next)value=it.Current;}catch(Exception e){failed++;results.Add(new{exception=e.ToString()});Restore();Save("ERROR");yield break;}if(!next)break;yield return value;}}
Save("STARTING");ui.StartCoroutine(Guard());return "Noninteractive captions and stack hover layout";

