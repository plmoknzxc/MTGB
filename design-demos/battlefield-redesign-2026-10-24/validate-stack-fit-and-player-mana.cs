const string W="D:/project/MTGB/design-demos/battlefield-redesign-2026-10-24/";
var ui=UnityEngine.Object.FindFirstObjectByType<ForgeBattlefieldController>();
var flags=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance;
var rows=new System.Collections.Generic.List<object>();int checks=0,failed=0;
string savedLanguage=GamePreferences.Language;float savedScale=GamePreferences.TextScale;
void Check(bool pass,string context){checks++;if(!pass)failed++;rows.Add(new{pass,context});}
void Save(string status){var json=Type.GetType("Newtonsoft.Json.JsonConvert, Newtonsoft.Json").GetMethod("SerializeObject",new[]{typeof(object)});System.IO.File.WriteAllText(W+"stack-fit-player-mana-validation.json",(string)json.Invoke(null,new object[]{new{status,checks,failed,rows}}));}
Rect Bounds(RectTransform r){var c=new Vector3[4];r.GetWorldCorners(c);return Rect.MinMaxRect(c.Min(p=>p.x),c.Min(p=>p.y),c.Max(p=>p.x),c.Max(p=>p.y));}
ForgeSeatView Sample()=>JsonUtility.FromJson<ForgeSeatView>(System.IO.File.ReadAllText(W+"native-full-seat.json"));
Transform Root()=>GameObject.Find("Canvas").transform;
System.Collections.IEnumerator Settle(float seconds=.35f){yield return new WaitForSecondsRealtime(seconds);Canvas.ForceUpdateCanvases();}
bool Click(UnityEngine.UI.Button b){var rect=Bounds((RectTransform)b.transform);var e=new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current){position=rect.center,button=UnityEngine.EventSystems.PointerEventData.InputButton.Left};var hits=new System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult>();UnityEngine.EventSystems.EventSystem.current.RaycastAll(e,hits);bool hit=hits.Count>0&&hits[0].gameObject.GetComponentInParent<UnityEngine.UI.Button>()==b;if(!hit)rows.Add(new{button=b.name,blockedBy=hits.Take(3).Select(h=>h.gameObject.name).ToArray()});if(hit){UnityEngine.EventSystems.ExecuteEvents.ExecuteHierarchy(hits[0].gameObject,e,UnityEngine.EventSystems.ExecuteEvents.pointerClickHandler);}return hit;}
var assembly=typeof(UnityEditor.Editor).Assembly;var gameViewType=assembly.GetType("UnityEditor.GameView");var gameView=UnityEditor.EditorWindow.GetWindow(gameViewType);var sizeProperty=gameViewType.GetProperty("selectedSizeIndex",System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance);int savedSize=(int)sizeProperty.GetValue(gameView);var sizesType=assembly.GetType("UnityEditor.GameViewSizes");var sizes=typeof(UnityEditor.ScriptableSingleton<>).MakeGenericType(sizesType).GetProperty("instance").GetValue(null);var group=sizesType.GetMethod("GetGroup").Invoke(sizes,new object[]{Enum.Parse(assembly.GetType("UnityEditor.GameViewSizeGroupType"),"Standalone")});var added=new System.Collections.Generic.List<int>();
void Resize(int w,int h){int index=((string[])group.GetType().GetMethod("GetDisplayTexts").Invoke(group,null)).Length;var size=Activator.CreateInstance(assembly.GetType("UnityEditor.GameViewSize"),new object[]{Enum.Parse(assembly.GetType("UnityEditor.GameViewSizeType"),"FixedResolution"),w,h,"MTGB stack fit"});group.GetType().GetMethod("AddCustomSize").Invoke(group,new[]{size});added.Add(index);sizeProperty.SetValue(gameView,index);gameView.Repaint();}
void Restore(){sizeProperty.SetValue(gameView,savedSize);foreach(int i in added.OrderByDescending(i=>i))group.GetType().GetMethod("RemoveCustomSize").Invoke(group,new object[]{i});GamePreferences.SetLanguage(savedLanguage);GamePreferences.SetScale(savedScale);}
void ManaGeometry(string label){
 var canvas=Root();var arena=(System.Collections.IDictionary)typeof(ForgeBattlefieldController).GetField("arenaCards",flags).GetValue(ui);
 foreach(string name in new[]{"ManaPoolChips","PublicManaPool"}){
  var pool=canvas.Find(name) as RectTransform;if(!pool.gameObject.activeSelf)continue;var box=Bounds(pool);box.xMin-=32*pool.lossyScale.x;box.xMax+=32*pool.lossyScale.x;Check(box.xMin>=0&&box.xMax<=Screen.width&&box.yMin>=0&&box.yMax<=Screen.height,label+" mana on screen "+name);
  foreach(string node in new[]{"PlayerPlateWithZones","OpponentPlateWithZones","PlayerPlate/PlayerCounters","OpponentPlate/PlayerCounters","StackColumn/Viewport","GearBox"}){var r=canvas.Find(node) as RectTransform;if(r!=null&&r.gameObject.activeInHierarchy)Check(!box.Overlaps(Bounds(r)),label+" mana clear "+name+"/"+node);}
  foreach(System.Collections.DictionaryEntry pair in arena){var r=(RectTransform)pair.Value.GetType().GetField("root").GetValue(pair.Value);var card=(ForgeCardView)pair.Value.GetType().GetField("card").GetValue(pair.Value);if(!r.gameObject.activeInHierarchy||card?.zone=="Hand")continue;var visible=Bounds(r);var lane=r.GetComponentInParent<BattlefieldHorizontalScroll>();if(lane!=null){var clip=Bounds(lane.viewport);visible=Rect.MinMaxRect(Mathf.Max(visible.xMin,clip.xMin),Mathf.Max(visible.yMin,clip.yMin),Mathf.Min(visible.xMax,clip.xMax),Mathf.Min(visible.yMax,clip.yMax));if(visible.width<=0||visible.height<=0)continue;}Check(!box.Overlaps(visible),label+" mana clear card "+name+"/"+pair.Key);}
 }
}
System.Collections.IEnumerator Run(){
 while(!GamePreferences.Ready)yield return null;
 foreach(string language in new[]{"zh-CN","en-US","ja-JP"}){
  GamePreferences.SetLanguage(language);yield return null;while(!GamePreferences.Ready)yield return null;
  foreach(float scale in new[]{.85f,1f,1.3f}){
   GamePreferences.SetScale(scale);
   foreach(int count in new[]{0,1,2,12,30}){
    var v=Sample();var source=v.stackItems;v.stackItems=Enumerable.Range(0,count).Select(i=>{var e=JsonUtility.FromJson<ForgeStackItem>(JsonUtility.ToJson(source[i%source.Length]));e.id=91000+i;return e;}).ToArray();v.stack=v.stackItems.Select(e=>e.description).ToArray();v.stackCount=count;
    ui.Present(null,1,false,null,null,"");yield return null;ui.Present(v,1,false,null,null,"");yield return Settle(.12f);string label=language+"/"+scale+"/"+count;
    var column=Root().Find("StackColumn") as RectTransform;Check(column.gameObject.activeSelf==(count>0),label+" visibility");if(count==0)continue;
    var viewport=column.Find("Viewport") as RectTransform;var scroll=viewport.GetComponent<UnityEngine.UI.ScrollRect>();float limit=(float)typeof(ForgeBattlefieldController).GetProperty("RedesignStackMaximumHeight",flags).GetValue(ui);
    Check(Mathf.Abs(viewport.rect.height-Mathf.Min(limit,scroll.content.rect.height))<1,label+" fits content without empty tail");
    Check(column.rect.width==320,label+" enlarged width");var title=scroll.content.GetComponentsInChildren<UnityEngine.UI.Text>(true).First(t=>t.name=="CompactTitle");Check(title.fontSize>=Mathf.RoundToInt(22*scale)-1,label+" enlarged title");
    if(count>=12){scroll.verticalNormalizedPosition=0;yield return null;Check(scroll.content.anchoredPosition.y>=scroll.content.rect.height-viewport.rect.height-2,label+" scroll bottom");}
    if(count==2){var collapse=column.Find("CollapseStack").GetComponent<UnityEngine.UI.Button>();Check(Click(collapse),label+" hide button reachable");yield return Settle(.1f);Check(!viewport.gameObject.activeSelf&&column.rect.height==44,label+" hides rows and background");if(language=="zh-CN"&&scale==1){yield return Settle(.4f);ScreenCapture.CaptureScreenshot(W+"stack-fit-hidden.png");yield return new WaitForEndOfFrame();}Check(Click(collapse),label+" show button reachable");yield return Settle(.1f);Check(viewport.gameObject.activeSelf,label+" restores rows");if(language=="zh-CN"&&scale==1){yield return Settle(.4f);ScreenCapture.CaptureScreenshot(W+"stack-fit-two.png");yield return new WaitForEndOfFrame();}}
   }
  }
  Save("RUNNING");
 }
 GamePreferences.SetLanguage("zh-CN");yield return null;while(!GamePreferences.Ready)yield return null;
 foreach(var size in new[]{new Vector2Int(1280,720),new Vector2Int(1920,1080),new Vector2Int(1024,768)}){
  Resize(size.x,size.y);yield return Settle(.2f);
  foreach(float scale in new[]{1f,1.3f}){
   GamePreferences.SetScale(scale);
   foreach(bool dense in new[]{false,true}){
    var v=Sample();if(dense)foreach(var player in v.players)player.battlefield=Enumerable.Range(0,8).SelectMany(n=>player.battlefield.Select(card=>{var c=JsonUtility.FromJson<ForgeCardView>(JsonUtility.ToJson(card));c.id+=n*1000;c.counters=new[]{new ForgeCardCounter{kind="P1P1",count=n}};return c;})).ToArray();
    ui.Present(null,1,false,null,null,"");yield return null;ui.Present(v,1,false,null,null,"");yield return Settle(.55f);Check(Screen.width==size.x&&Screen.height==size.y,"actual size "+size);ManaGeometry(size+"/"+scale+"/dense="+dense);
    var own=Root().Find("ManaPoolChips") as RectTransform;var other=Root().Find("PublicManaPool") as RectTransform;Check(own.anchoredPosition==new Vector2(205,194)&&other.anchoredPosition==new Vector2(205,-242),"fixed player mana "+size+"/"+scale+"/"+dense);
    if(scale==1&&!dense){ScreenCapture.CaptureScreenshot(W+"player-mana-"+size.x+"x"+size.y+".png");yield return new WaitForEndOfFrame();}if(scale==1&&dense&&size.x==1280){ScreenCapture.CaptureScreenshot(W+"player-mana-dense.png");yield return new WaitForEndOfFrame();}
   }
  }
 }
 sizeProperty.SetValue(gameView,savedSize);GamePreferences.SetScale(1);var baseline=Sample();ui.Present(null,1,false,null,null,"");yield return null;ui.Present(baseline,1,false,null,null,"");typeof(ForgeBattlefieldController).GetMethod("SetRedesignHandExpanded",flags).Invoke(ui,new object[]{true});yield return Settle(.6f);
 var hand=Root().Find("PlayerHandRoot");var pointers=hand.GetComponentsInChildren<ForgeBattlefieldCardPointer>().Where(p=>p.gameObject.name.StartsWith("Card_")&&p.GetComponent<CanvasGroup>().alpha>.9f).OrderBy(p=>((RectTransform)p.transform).anchoredPosition.x).ToArray();
 foreach(var pointer in new[]{pointers[0],pointers[pointers.Length-1]}){var face=(RectTransform)pointer.GetType().GetField("face",flags).GetValue(pointer);var before=Bounds(face).center;float x=((RectTransform)pointer.transform).anchoredPosition.x;pointer.OnPointerEnter(new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current));yield return Settle(.7f);Check(Mathf.Abs(face.localPosition.x)<.05f,"hover has zero horizontal lift "+pointer.name);Check(Mathf.Abs(Bounds(face).center.x-before.x)<.5f,"hover screen x stable "+pointer.name);Check(Mathf.Abs(((RectTransform)pointer.transform).anchoredPosition.x-x)<.05f&&face.localScale.x>1.7f&&face.localPosition.y>0,"hover grows upward "+pointer.name);if(pointer==pointers[pointers.Length-1]){ScreenCapture.CaptureScreenshot(W+"hand-hover-vertical-right.png");yield return new WaitForEndOfFrame();}pointer.OnPointerExit(new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current));yield return Settle(.4f);}
 var manaScroll=Root().Find("ManaPoolChips").GetComponent<BattlefieldHorizontalScroll>();var visibleCells=manaScroll.content.Cast<RectTransform>().Count(r=>r.gameObject.activeInHierarchy&&r.GetComponent<CanvasGroup>()?.alpha>.9f);Check(visibleCells==5,"five mana types visible at once");var nextMana=Root().Find("ManaPoolChips_Navigation/Next").GetComponent<UnityEngine.UI.Button>();Check(Click(nextMana),"relocated mana next arrow reachable");yield return Settle(.4f);Check(manaScroll.content.anchoredPosition.x<0,"relocated mana pages to remaining buckets");
 var native=Sample();native.request.options=new[]{new ForgeOption{id="mana:W",label="Pay white"},new ForgeOption{id="ok",label="OK"}};int calls=0,last=-1;ui.Present(native,1,true,i=>{calls++;last=i;},null,"");yield return Settle(.3f);manaScroll.horizontalNormalizedPosition=0;yield return Settle(.2f);var white=Root().Find("ManaPoolChips/Content/Pool_W").GetComponent<UnityEngine.UI.Button>();Check(Click(white)&&calls==1&&last==0,"relocated mana submits exact original option");ui.TrySubmitPresentedOption(0,native.request.requestId);Check(calls==1,"mana duplicate guard");
 Restore();yield return null;while(!GamePreferences.Ready)yield return null;ui.Present(null,1,false,null,null,"");yield return null;ui.Present(Sample(),1,false,null,null,"副本 · 只读布局预览");typeof(ForgeBattlefieldController).GetMethod("SetRedesignHandExpanded",flags).Invoke(ui,new object[]{true});yield return Settle(.6f);ScreenCapture.CaptureScreenshot(W+"stack-fit-player-mana-final.png");yield return new WaitForEndOfFrame();Save("COMPLETE");
}
System.Collections.IEnumerator Guard(){var iterator=Run();for(int step=0;step<100000;step++){bool next=false;object value=null;try{next=iterator.MoveNext();if(next)value=iterator.Current;}catch(Exception e){failed++;rows.Add(new{exception=e.ToString()});Restore();Save("ERROR");yield break;}if(!next)break;yield return value;}}
Save("STARTING");ui.StartCoroutine(Guard());return "Focused stack, hide, hover and player mana checks started";
