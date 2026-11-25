const string W="D:/project/MTGB/design-demos/battlefield-redesign-2026-10-24/";
var ui=UnityEngine.Object.FindFirstObjectByType<ForgeBattlefieldController>();var flags=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance;
var results=new System.Collections.Generic.List<object>();int checks=0,failed=0;float savedScale=GamePreferences.TextScale;
void Check(bool pass,string context){checks++;if(!pass)failed++;results.Add(new{pass,context});}
void Save(string status){var method=Type.GetType("Newtonsoft.Json.JsonConvert, Newtonsoft.Json").GetMethod("SerializeObject",new[]{typeof(object)});System.IO.File.WriteAllText(W+"hand-tray-hover-validation.json",(string)method.Invoke(null,new object[]{new{status,checks,failed,scope="Copied-scene presentation adapters, actual raycasts and native local callbacks",results}}));}
ForgeSeatView Sample()=>JsonUtility.FromJson<ForgeSeatView>(System.IO.File.ReadAllText(W+"native-full-seat.json"));
Transform Root()=>GameObject.Find("Canvas").transform;
Rect Bounds(RectTransform r){var c=new Vector3[4];r.GetWorldCorners(c);return Rect.MinMaxRect(c.Min(v=>v.x),c.Min(v=>v.y),c.Max(v=>v.x),c.Max(v=>v.y));}
Rect Overlap(Rect a,Rect b)=>Rect.MinMaxRect(Mathf.Max(a.xMin,b.xMin),Mathf.Max(a.yMin,b.yMin),Mathf.Min(a.xMax,b.xMax),Mathf.Min(a.yMax,b.yMax));
UnityEngine.EventSystems.PointerEventData Pointer(Vector2 point)=>new(UnityEngine.EventSystems.EventSystem.current){position=point,pressPosition=point,eligibleForClick=true,button=UnityEngine.EventSystems.PointerEventData.InputButton.Left};
GameObject Hit(Vector2 point){var hits=new System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult>();UnityEngine.EventSystems.EventSystem.current.RaycastAll(Pointer(point),hits);return hits.FirstOrDefault().gameObject;}
bool Click(UnityEngine.UI.Button button,Vector2? point=null){var p=Pointer(point??Bounds((RectTransform)button.transform).center);var hit=Hit(p.position);bool correct=hit!=null&&hit.GetComponentInParent<UnityEngine.UI.Button>()==button;if(correct){UnityEngine.EventSystems.ExecuteEvents.ExecuteHierarchy(hit,p,UnityEngine.EventSystems.ExecuteEvents.pointerDownHandler);UnityEngine.EventSystems.ExecuteEvents.ExecuteHierarchy(hit,p,UnityEngine.EventSystems.ExecuteEvents.pointerClickHandler);}else results.Add(new{button=button.name,hit=hit?.name});return correct;}
System.Collections.IEnumerator Wait(float seconds=.22f){yield return new WaitForSecondsRealtime(seconds);Canvas.ForceUpdateCanvases();}
void Expanded(bool value)=>typeof(ForgeBattlefieldController).GetMethod("SetRedesignHandExpanded",flags).Invoke(ui,new object[]{value});
var assembly=typeof(UnityEditor.Editor).Assembly;var gameViewType=assembly.GetType("UnityEditor.GameView");var gameView=UnityEditor.EditorWindow.GetWindow(gameViewType);var sizeProperty=gameViewType.GetProperty("selectedSizeIndex",System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance);int savedSize=(int)sizeProperty.GetValue(gameView);var sizesType=assembly.GetType("UnityEditor.GameViewSizes");var sizes=typeof(UnityEditor.ScriptableSingleton<>).MakeGenericType(sizesType).GetProperty("instance").GetValue(null);var group=sizesType.GetMethod("GetGroup").Invoke(sizes,new object[]{Enum.Parse(assembly.GetType("UnityEditor.GameViewSizeGroupType"),"Standalone")});var added=new System.Collections.Generic.List<int>();
void Resize(int w,int h){int index=((string[])group.GetType().GetMethod("GetDisplayTexts").Invoke(group,null)).Length;var size=Activator.CreateInstance(assembly.GetType("UnityEditor.GameViewSize"),new object[]{Enum.Parse(assembly.GetType("UnityEditor.GameViewSizeType"),"FixedResolution"),w,h,"MTGB hand tray hover"});group.GetType().GetMethod("AddCustomSize").Invoke(group,new[]{size});added.Add(index);sizeProperty.SetValue(gameView,index);gameView.Repaint();}
void Restore(){GamePreferences.SetScale(savedScale);sizeProperty.SetValue(gameView,savedSize);foreach(int i in added.OrderByDescending(i=>i))group.GetType().GetMethod("RemoveCustomSize").Invoke(group,new object[]{i});}
System.Collections.IEnumerator Run(){
 while(!GamePreferences.Ready)yield return null;
 foreach(var size in new[]{new Vector2Int(1280,720),new Vector2Int(1920,1080),new Vector2Int(1024,768)}){
  Resize(size.x,size.y);yield return Wait(.35f);
  foreach(float scale in new[]{.85f,1f,1.3f}){
   GamePreferences.SetScale(scale);
   foreach(int count in new[]{0,1,5,12}){
    var v=Sample();var own=v.players.First(p=>p.seat==1);var source=own.hand;own.hand=Enumerable.Range(0,count).Select(i=>{var c=JsonUtility.FromJson<ForgeCardView>(JsonUtility.ToJson(source[i%source.Length]));c.id=98000+i;return c;}).ToArray();own.handCount=count;
    var stack=v.stackItems;v.stackItems=Enumerable.Range(0,12).Select(i=>{var e=JsonUtility.FromJson<ForgeStackItem>(JsonUtility.ToJson(stack[i%2]));e.id=99000+i;return e;}).ToArray();v.stack=v.stackItems.Select(e=>e.description).ToArray();v.stackCount=12;
    ui.Present(v,1,false,null,null,"");Expanded(true);yield return Wait(.35f);string context=size+"/"+scale+"/"+count;var hand=Root().Find("PlayerHandRoot");var tray=hand.Find("RedesignHandBackplate") as RectTransform;
    Check(tray.gameObject.activeSelf==(count>0),context+" tray follows card presence");if(count==0)continue;
    Check(tray.GetSiblingIndex()==0&&tray.GetComponent<BattlefieldHandPanelTint>()!=null,context+" gradient tray behind hand");
    Check(Bounds(tray).xMin>=392*Root().GetComponent<Canvas>().scaleFactor,context+" player-side mana arrow reserve");
    var before=Bounds(tray);Expanded(false);yield return Wait();Check(Mathf.Abs((before.yMin-Bounds(tray).yMin)/Root().GetComponent<Canvas>().scaleFactor-154)<1,context+" tray tucks with cards");
    var visible=Bounds(tray);var reveal=tray.GetComponent<UnityEngine.UI.Button>();Check(Click(reveal,new Vector2(visible.center.x,visible.yMax-4)),context+" tucked tray reveals through real raycast");yield return Wait();Check(Mathf.Abs(Bounds(tray).yMin-before.yMin)<1,context+" tray restored");
    if(count==12){var track=hand.Find("HandFanNavigation");var navigation=track.GetComponent<ForgeHandFanNavigation>();float offset=navigation.Offset;var pan=Pointer(new Vector2(Bounds(tray).center.x,Bounds(tray).yMax-4));UnityEngine.EventSystems.ExecuteEvents.Execute(tray.gameObject,pan,UnityEngine.EventSystems.ExecuteEvents.beginDragHandler);pan.position+=new Vector2(-45,0);UnityEngine.EventSystems.ExecuteEvents.Execute(tray.gameObject,pan,UnityEngine.EventSystems.ExecuteEvents.dragHandler);UnityEngine.EventSystems.ExecuteEvents.Execute(tray.gameObject,pan,UnityEngine.EventSystems.ExecuteEvents.endDragHandler);yield return Wait();Check(!track.GetComponent<UnityEngine.UI.Image>().raycastTarget&&navigation.Offset<offset&&!pan.eligibleForClick,context+" tray retains blank-space panning without click");}
    var pointers=hand.GetComponentsInChildren<ForgeBattlefieldCardPointer>().Where(p=>p.name.StartsWith("Card_")&&p.GetComponent<CanvasGroup>().alpha>.9f).OrderBy(p=>((RectTransform)p.transform).anchoredPosition.x).ToArray();
    if(count>=5)foreach(var pointer in new[]{pointers[0],pointers[pointers.Length-1]}){
     var face=(RectTransform)pointer.GetType().GetField("face",flags).GetValue(pointer);var canvas=face.GetComponent<Canvas>();float x=Bounds(face).center.x;pointer.OnPointerEnter(Pointer(Bounds((RectTransform)pointer.transform).center));yield return Wait(.5f);
     Check(canvas.overrideSorting&&canvas.sortingOrder>Root().Find("StackColumn").GetComponent<Canvas>().sortingOrder&&canvas.sortingOrder>Root().Find("RedesignStackControls").GetComponent<Canvas>().sortingOrder,context+" hovered face above stack "+pointer.name);
     Check(Mathf.Abs(Bounds(face).center.x-x)<.5f&&face.localScale.x>1.7f,context+" enlarged straight upwards "+pointer.name);
     var intersection=Overlap(Bounds(face),Bounds((RectTransform)Root().Find("StackColumn/Viewport")));
     if(pointer==pointers[pointers.Length-1]&&count==12){Check(intersection.width>1&&intersection.height>1,context+" actual stack overlap exists");if(intersection.width>1&&intersection.height>1){var hit=Hit(intersection.center);Check(hit!=null&&hit.GetComponentInParent<ForgeBattlefieldCardPointer>()==pointer,context+" hovered card wins stack-overlap raycast");}}
     pointer.OnPointerExit(Pointer(Vector2.zero));yield return Wait(.3f);Check(!canvas.overrideSorting&&face.localScale.x<1.01f,context+" exit restores normal layer "+pointer.name);
    }
   }
   Save("RUNNING");
  }
 }
 var pick=JsonUtility.FromJson<ForgeSeatView>(System.IO.File.ReadAllText(W+"action-ui-inputs/hand.json"));int calls=0,last=-1;ui.Present(pick,pick.seat,true,i=>{calls++;last=i;},null,"");yield return Wait(.35f);Expanded(false);
 var choice=Root().Find("PlayerHandRoot").GetComponentsInChildren<UnityEngine.UI.Button>().First(b=>b.name.StartsWith("Card_")&&pick.request.options.Any(o=>!o.disabled&&b.name=="Card_"+o.card?.id));int index=Array.FindIndex(pick.request.options,o=>choice.name=="Card_"+o.card?.id);
 var bounds=Bounds((RectTransform)choice.transform);Check(Click(choice,new Vector2(bounds.center.x,Mathf.Clamp(bounds.center.y,8,Screen.height-8)))&&calls==0,"first tucked card click reveals without selection");yield return Wait();Check(Click(choice)&&calls==1&&last==index,"second card click retains exact native choice");
 var drag=Sample();var card=drag.players.First(p=>p.seat==1).hand[0];drag.request.options=new[]{new ForgeOption{id="card:"+card.id,label=card.name},new ForgeOption{id="ok",label="OK"}};calls=0;last=-1;ui.Present(drag,1,true,i=>{calls++;last=i;},null,"");Expanded(true);yield return Wait(.35f);
 var pointerDrag=Root().Find("PlayerHandRoot").GetComponentsInChildren<ForgeBattlefieldCardPointer>().First(p=>p.name=="Card_"+card.id);var e=Pointer(Bounds((RectTransform)pointerDrag.transform).center);pointerDrag.OnPointerEnter(e);yield return Wait(.3f);pointerDrag.OnBeginDrag(e);e.position=new Vector2(Screen.width*.48f,Screen.height*.58f);pointerDrag.OnDrag(e);yield return Wait(.1f);
 var dragFace=(RectTransform)typeof(ForgeBattlefieldCardPointer).GetField("face",flags).GetValue(pointerDrag);Check(dragFace.GetComponent<Canvas>().overrideSorting,"drag stays above stack");pointerDrag.OnEndDrag(e);yield return Wait(.3f);Check(calls==1&&last==0,"drag keeps exact native play action");Check(!dragFace.GetComponent<Canvas>().overrideSorting,"drag end restores normal layer");
 Restore();yield return Wait(.5f);ui.Present(Sample(),1,false,null,null,"副本 · 只读布局预览");Expanded(true);Save("COMPLETE");
}
System.Collections.IEnumerator Guard(){var it=Run();for(int i=0;i<100000;i++){bool next=false;object value=null;try{next=it.MoveNext();if(next)value=it.Current;}catch(Exception e){failed++;results.Add(new{exception=e.ToString()});Restore();Save("ERROR");yield break;}if(!next)break;yield return value;}}
Save("STARTING");ui.StartCoroutine(Guard());return "Hand tray and hover-over-stack checks started";
