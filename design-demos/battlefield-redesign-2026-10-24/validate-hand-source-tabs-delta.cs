const string W="D:/project/MTGB/design-demos/battlefield-redesign-2026-10-24/";
var ui=UnityEngine.Object.FindFirstObjectByType<ForgeBattlefieldController>();
if(ui==null)ui=new GameObject("RedesignPreview").AddComponent<ForgeBattlefieldController>();
if(!Application.isPlaying||UnityEngine.SceneManagement.SceneManager.GetActiveScene().name!=ForgeBattlefieldController.SceneName)return "Open the duel battlefield in Play Mode.";
var flags=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance;
var results=new System.Collections.Generic.List<object>();int checks=0,failed=0,calls=0,last=-1;string oldLanguage=GamePreferences.Language;float oldScale=GamePreferences.TextScale;
void Check(bool pass,string context){checks++;if(!pass)failed++;results.Add(new{pass,context});}
void Save(string status){var method=Type.GetType("Newtonsoft.Json.JsonConvert, Newtonsoft.Json").GetMethod("SerializeObject",new[]{typeof(object)});System.IO.File.WriteAllText(W+"hand-source-tabs-delta-validation.json",(string)method.Invoke(null,new object[]{new{status,checks,failed,scope="Copied-scene display adapters and recorded native choice consumers; no live network or new engine effect claims",results}}));}
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
void Resize(int w,int h){int index=((string[])group.GetType().GetMethod("GetDisplayTexts").Invoke(group,null)).Length;var size=Activator.CreateInstance(assembly.GetType("UnityEditor.GameViewSize"),new object[]{Enum.Parse(assembly.GetType("UnityEditor.GameViewSizeType"),"FixedResolution"),w,h,"MTGB hand source tabs"});group.GetType().GetMethod("AddCustomSize").Invoke(group,new[]{size});added.Add(index);sizeProperty.SetValue(gameView,index);gameView.Repaint();}
void Restore(){GamePreferences.SetLanguage(oldLanguage);GamePreferences.SetScale(oldScale);sizeProperty.SetValue(gameView,savedSize);var labels=(string[])group.GetType().GetMethod("GetDisplayTexts").Invoke(group,null);for(int i=labels.Length-1;i>=0;i--)if(labels[i].StartsWith("MTGB hand source tabs"))group.GetType().GetMethod("RemoveCustomSize").Invoke(group,new object[]{i});}
ForgeSeatView TabSample(){var v=Sample();var native=Read("D:/project/MTGB/.tools/hand-source-tabs-1025/view-1.json");v.playableGraveyard=native.players[0].publicPlayableGraveyard;v.players[1].publicPlayableGraveyard=native.players[1].publicPlayableGraveyard;v.players[0].graveyardCards=native.players[0].graveyardCards;v.players[1].graveyardCards=native.players[1].graveyardCards;v.playableExile=new[]{Clone(v.players[0].hand[0])};v.playableExile[0].id=98701;v.playableExile[0].zone="Exile";v.players[1].publicPlayableExile=new[]{Clone(v.players[0].hand[1])};v.players[1].publicPlayableExile[0].id=98702;v.players[1].publicPlayableExile[0].zone="Exile";v.players[1].publicPlayableExile[0].ownerSeat=2;v.awaitingInput=false;return v;}
UnityEngine.UI.Button Tab(bool other,string zone)=>Root().Find((other?"OtherHandSourceTabs":"HandSourceTabs")+"/Source_"+zone).GetComponent<UnityEngine.UI.Button>();
int[] Shown(bool other)=>Root().Find(other?"OpponentHandRoot":"PlayerHandRoot").GetComponentsInChildren<ForgeBattlefieldCardPointer>().Where(p=>p.gameObject.activeInHierarchy).Select(p=>int.Parse(p.name.Substring(5))).OrderBy(i=>i).ToArray();
System.Collections.IEnumerator Run(){
 foreach(var size in Array.Empty<Vector2Int>()){
  Resize(size.x,size.y);yield return Wait(.3f);
  foreach(string lang in new[]{"zh-CN","en-US","ja-JP"})foreach(float scale in new[]{.85f,1f,1.3f}){
   GamePreferences.SetLanguage(lang);GamePreferences.SetScale(scale);yield return null;while(!GamePreferences.Ready)yield return null;
   var v=TabSample();yield return Present(v);Expanded(true);string context=size+" "+lang+" "+scale;
   foreach(bool other in new[]{false,true}){
    var expected=other?v.players[1].publicPlayableGraveyard:v.playableGraveyard;
    Check(Click(Tab(other,"Graveyard")),context+" graveyard tab raycast "+other);yield return Wait(.05f);
    Check(Shown(other).SequenceEqual(expected.Select(c=>c.id).OrderBy(i=>i)),context+" only supplied playable graveyard IDs "+other);
    Check(Click(Tab(other,"Exile")),context+" exile tab raycast "+other);yield return Wait(.05f);
    Check(Shown(other).SequenceEqual(new[]{other?98702:98701}),context+" only supplied playable exile IDs "+other);
    Check(Click(Tab(other,"Hand")),context+" hand tab raycast "+other);yield return Wait(.05f);
    Check(Shown(other).Length==(other?v.players[1].handCount:v.players[0].handCount),context+" full hand count returns "+other);
    var tabs=(RectTransform)Tab(other,"Hand").transform.parent;
    var b=Bounds(tabs);Check(b.xMin>=0&&b.xMax<=Screen.width&&b.yMin>=0&&b.yMax<=Screen.height,context+" tabs inside screen "+other);
    Check(tabs.GetComponentsInChildren<UnityEngine.UI.Text>().All(t=>t.preferredWidth<=t.rectTransform.rect.width+.5f&&t.preferredHeight<=t.rectTransform.rect.height+.5f),context+" fixed text fits "+other);
   }
   var pos=((RectTransform)Root().Find("OtherHandSourceTabs")).position;Expanded(false);yield return Wait(.05f);Check(Vector3.Distance(pos,((RectTransform)Root().Find("OtherHandSourceTabs")).position)<.1f&&Click(Tab(false,"Hand")),context+" tucked hand tabs stay reachable and opponent fixed");yield return Wait(.05f);Check((bool)typeof(ForgeBattlefieldController).GetField("redesignHandExpanded",flags).GetValue(ui),context+" selecting own source raises local hand");Save("RUNNING");
  }
 }
 Check(calls==0,"all source tabs are local browsing only");
 GamePreferences.SetLanguage("zh-CN");GamePreferences.SetScale(1);Resize(1920,1080);yield return null;while(!GamePreferences.Ready)yield return null;
 var sample=TabSample();yield return Present(sample);Check(Click(Tab(true,"Graveyard")),"delta other graveyard click");yield return Wait();Check(Click(Tab(false,"Graveyard")),"delta own graveyard click after rebuilt canvas");yield return Wait();
 ScreenCapture.CaptureScreenshot(W+"hand-source-tabs-graveyard.png");yield return new WaitForEndOfFrame();
 var empty=Clone(sample);empty.playableGraveyard=Array.Empty<ForgeCardView>();empty.players[1].publicPlayableGraveyard=Array.Empty<ForgeCardView>();yield return Present(empty,1,false);Check(Shown(false).Length==0&&Shown(true).Length==0,"revoked or absent permissions clear selected source without exposing whole cemetery");
 var pick=Read("D:/project/MTGB/Tools/Validation/Reports/Thoughtseize/choice.json");yield return Present(pick);Check(Shown(true).Length==pick.players[1].handCount&&!Tab(true,"Graveyard").interactable,"native hand choice forces real hand and locks other tabs");
 Root().Find("OpponentHandRoot/HandFanNavigation").GetComponent<ForgeHandFanNavigation>().MoveBy(100000);yield return Wait();int option=Array.FindIndex(pick.request.options,o=>o.card!=null&&o.card.name=="Negate");int before=calls;var button=Root().Find("OpponentHandRoot/Card_"+pick.request.options[option].card.id).GetComponent<UnityEngine.UI.Button>();Check(Click(button)&&calls==before+1&&last==option,"native selection remains exact and reachable");
 var pub=Clone(sample);pub.players[1].publicPlayableGraveyard=pub.players[1].publicPlayableGraveyard.Concat(new[]{new ForgeCardView{id=999,name="Private",faceDown=true,knownFaceName="SECRET"}}).ToArray();var spectator=OnlineSpectatorView.Create(new OnlinePlayerViewState{forge=pub},0);Check(spectator.forge.players[1].publicPlayableGraveyard.All(c=>!c.faceDown&&c.name!="Private")&&spectator.forge.players.All(p=>p.hand.Length==0),"spectator new field filters face-down cards and private hands");
 sample.playableCommand=new[]{Clone(sample.playableGraveyard[0])};sample.playableCommand[0].id=98888;sample.playableCommand[0].zone="Command";sample.playableCommand[0].ownerSeat=1;yield return Present(sample);Check(Tab(false,"Command").gameObject.activeInHierarchy&&Click(Tab(false,"Command")),"existing command source remains available");yield return Wait();Check(Shown(false).SequenceEqual(new[]{98888}),"command source keeps native ID");
 yield return Present(TabSample());Click(Tab(false,"Hand"));Click(Tab(true,"Hand"));Quiet();yield return Wait(.4f);ScreenCapture.CaptureScreenshot(W+"hand-source-tabs-hand.png");yield return new WaitForEndOfFrame();Restore();Save("COMPLETE");
}
System.Collections.IEnumerator Guard(){var it=Run();for(int i=0;i<100000;i++){bool next=false;object value=null;try{next=it.MoveNext();if(next)value=it.Current;}catch(Exception e){failed++;results.Add(new{exception=e.ToString()});Restore();Save("ERROR");yield break;}if(!next)break;yield return value;}}
Save("STARTING");ui.StartCoroutine(Guard());return "Hand dock source tabs validation started";
