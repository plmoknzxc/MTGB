const string W="D:/project/MTGB/design-demos/battlefield-redesign-2026-10-24/";
var ui=UnityEngine.Object.FindFirstObjectByType<ForgeBattlefieldController>();var flags=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance;
var results=new System.Collections.Generic.List<object>();int checks=0,failed=0;string languageBefore=GamePreferences.Language;float scaleBefore=GamePreferences.TextScale;
void Check(bool pass,string context){checks++;if(!pass)failed++;results.Add(new{pass,context});}
void Save(string status){var method=Type.GetType("Newtonsoft.Json.JsonConvert, Newtonsoft.Json").GetMethod("SerializeObject",new[]{typeof(object)});System.IO.File.WriteAllText(W+"stack-slide-ui-validation.json",(string)method.Invoke(null,new object[]{new{status,checks,failed,scope="Unity display adapters and native local callbacks; no engine simulation",results}}));}
ForgeSeatView Sample()=>JsonUtility.FromJson<ForgeSeatView>(System.IO.File.ReadAllText(W+"native-full-seat.json"));
Transform Root()=>GameObject.Find("Canvas").transform;
Rect Bounds(RectTransform r){var c=new Vector3[4];r.GetWorldCorners(c);return Rect.MinMaxRect(c.Min(v=>v.x),c.Min(v=>v.y),c.Max(v=>v.x),c.Max(v=>v.y));}
System.Collections.IEnumerator Wait(float seconds=.18f){yield return new WaitForSecondsRealtime(seconds);Canvas.ForceUpdateCanvases();}
bool Click(UnityEngine.UI.Button b){var p=new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current){position=Bounds((RectTransform)b.transform).center,button=UnityEngine.EventSystems.PointerEventData.InputButton.Left};var hits=new System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult>();UnityEngine.EventSystems.EventSystem.current.RaycastAll(p,hits);bool hit=hits.Count>0&&hits[0].gameObject.GetComponentInParent<UnityEngine.UI.Button>()==b;if(hit)UnityEngine.EventSystems.ExecuteEvents.ExecuteHierarchy(hits[0].gameObject,p,UnityEngine.EventSystems.ExecuteEvents.pointerClickHandler);else results.Add(new{button=b.name,blocked=hits.Take(2).Select(h=>h.gameObject.name).ToArray()});return hit;}
void Restore(){GamePreferences.SetLanguage(languageBefore);GamePreferences.SetScale(scaleBefore);}
void ManaChecks(string context,bool five){
 foreach(string name in new[]{"ManaPoolChips","PublicManaPool"}){
  var pool=Root().Find(name).GetComponent<BattlefieldHorizontalScroll>();var cells=pool.content.Cast<RectTransform>().Where(r=>r.gameObject.activeSelf).OrderBy(r=>r.anchoredPosition.x).ToArray();
  float priorEnd=float.NaN;
  foreach(var c in cells){var g=c.Find("Symbol") as RectTransform;var t=c.Find("Count").GetComponent<UnityEngine.UI.Text>();float left=c.anchoredPosition.x-c.rect.width*.5f;
   float glyphRight=left+g.anchoredPosition.x+c.rect.width*.5f+g.rect.width*.5f;
   float numberLeft=left+t.rectTransform.anchoredPosition.x+c.rect.width*.5f;
   Check(Mathf.Abs(numberLeft-glyphRight-4)<.1f,context+" symbol-number gap "+name+"/"+c.name);
   if(!float.IsNaN(priorEnd))Check(Mathf.Abs(left-priorEnd-10)<1.1f,context+" measured inter-entry gap "+name+"/"+c.name);
   priorEnd=numberLeft+t.preferredWidth;
   Check(t.preferredWidth<=t.rectTransform.rect.width+.5f,context+" numeral not clipped "+name+"/"+c.name);
  }
  Check(pool.viewport.rect.width<=310.1f,context+" reserved player lane "+name);
  if(five){int shown=cells.Count(c=>c.GetComponent<CanvasGroup>()?.alpha>.9f);Check(shown==5,context+" five ordinary colors visible "+name);}
 }
 foreach(string plate in new[]{"PlayerPlate","OpponentPlate"}){
  var counter=Root().Find(plate+"/PlayerCounters/Counter_EXPERIENCE");var symbol=counter.Find("Symbol").GetComponent<ExperienceCounterIcon>();var n=counter.Find("Count").GetComponent<UnityEngine.UI.Text>();
  Check(Bounds(n.rectTransform).xMin>=Bounds(symbol.rectTransform).xMax-.1f,context+" experience numeral clear of outline "+plate);
  Check(symbol.SmoothEdges,context+" experience smooth geometry "+plate);
 }
}
System.Collections.IEnumerator Run(){
 while(!GamePreferences.Ready)yield return null;
 foreach(string language in new[]{"zh-CN","en-US","ja-JP"}){
  GamePreferences.SetLanguage(language);yield return null;while(!GamePreferences.Ready)yield return null;
  foreach(float scale in new[]{.85f,1f,1.3f}){
   GamePreferences.SetScale(scale);var v=Sample();foreach(var p in v.players)p.manaPoolEntries=new[]{"W","U","B","R","G"}.Select((s,i)=>new ForgeManaPoolEntry{id="mana:"+s,symbol=s,count=new[]{12,8,5,23,6}[i]}).ToArray();
   ui.Present(v,1,false,null,null,"");yield return Wait(.4f);string context=language+"/"+scale;
   ManaChecks(context,true);var col=Root().Find("StackColumn") as RectTransform;var rail=Root().Find("RedesignStackControls");var viewport=col.Find("Viewport").GetComponent<UnityEngine.UI.ScrollRect>();var ownTexts=viewport.content.GetComponentsInChildren<UnityEngine.UI.Text>().Where(t=>t.name=="CompactOwner").ToArray();
   Check(ownTexts.All(t=>!t.text.Contains("先结算")&&!t.text.Contains("后结算")&&!t.text.Contains("Resolves")&&!t.text.Contains("に解決")),context+" only top direction line");
   foreach(var label in viewport.content.GetComponentsInChildren<UnityEngine.UI.Text>().Where(t=>t.name.StartsWith("Compact")))Check(string.IsNullOrEmpty(label.text)?label.rectTransform.rect.height==0:label.preferredHeight<=label.rectTransform.rect.height+2,context+" label fits or empty slot removed "+label.name);
   var incoming=viewport.content.Find("StackEntry_1/CompactIncoming_0").GetComponent<UnityEngine.UI.Text>();string source=GamePreferences.CardFaceName(v.stackItems[0].source.name);
   Check(incoming.text.Contains(source)&&incoming.text.Contains("1")&&incoming.color.r>incoming.color.b,context+" actual source name, position and enemy color");
   var fills=viewport.content.Cast<RectTransform>().Select(r=>r.GetComponent<UnityEngine.UI.Image>().color).ToArray();Check(fills.Length==2&&fills.All(c=>c.a>=.9f)&&fills[0]!=fills[1],context+" rows opaque and visibly distinct");
   var collapse=rail.Find("CollapseStack").GetComponent<UnityEngine.UI.Button>();var viewButton=rail.Find("ViewStack").GetComponent<UnityEngine.UI.Button>();var railBounds=Bounds((RectTransform)rail);float height=col.rect.height;
   Check(Click(collapse),context+" collapse actual raycast");Check(col.anchoredPosition.x==-28,context+" no instant jump");
   yield return Wait(.06f);float middle=col.anchoredPosition.x;Check(middle>-28&&middle<450,context+" intermediate sliding position");
   ui.Present(v,1,false,null,null,"");Check(Mathf.Abs(col.anchoredPosition.x-middle)<.1f,context+" snapshot preserves animation position");
   yield return Wait(.35f);Check(Bounds(col).xMin>Screen.width&&col.rect.height==height,context+" whole panel offscreen without shrinking");Check(!col.GetComponent<CanvasGroup>().blocksRaycasts&&Bounds((RectTransform)rail)==railBounds,context+" hidden input disabled and controls fixed");
   Check(rail.GetComponentsInChildren<UnityEngine.UI.Graphic>().Where(g=>g.raycastTarget).All(g=>g.GetComponent<UnityEngine.UI.Button>()!=null),context+" only two controls intercept");
   Check(Click(viewButton),context+" hidden magnifier works");yield return Wait();var overview=Root().Find("StackOverview");Check(overview!=null&&overview.gameObject.activeSelf,context+" hidden overview opens");Check(Click(overview.Find("Close").GetComponent<UnityEngine.UI.Button>()),context+" overview closes");
   Check(Click(collapse),context+" expand actual raycast");yield return Wait(.35f);Check(col.anchoredPosition.x==-28&&col.GetComponent<CanvasGroup>().blocksRaycasts,context+" panel restored");
  }
  Save("RUNNING");
 }
 GamePreferences.SetLanguage("zh-CN");yield return null;while(!GamePreferences.Ready)yield return null;GamePreferences.SetScale(1);
 foreach(int count in new[]{0,1,12,30}){
  var v=Sample();var source=v.stackItems;v.stackItems=Enumerable.Range(0,count).Select(i=>{var e=JsonUtility.FromJson<ForgeStackItem>(JsonUtility.ToJson(source[i%2]));e.id=96000+i;if(e.targets!=null)foreach(var target in e.targets)target.stackId=96001;return e;}).ToArray();v.stack=v.stackItems.Select(e=>e.description).ToArray();v.stackCount=count;ui.Present(v,1,false,null,null,"");yield return Wait(.3f);
  var col=Root().Find("StackColumn") as RectTransform;var rail=Root().Find("RedesignStackControls");Check(col.gameObject.activeSelf==(count>0)&&rail.gameObject.activeSelf==(count>0),"zero stack hides both rail and panel "+count);if(count==0)continue;
  var scroll=col.Find("Viewport").GetComponent<UnityEngine.UI.ScrollRect>();float limit=(float)typeof(ForgeBattlefieldController).GetProperty("RedesignStackMaximumHeight",flags).GetValue(ui);Check(Mathf.Abs(scroll.viewport.rect.height-Mathf.Min(limit,scroll.content.rect.height))<1,"no empty tail "+count);
  if(count>2){scroll.verticalNormalizedPosition=0;yield return null;float offset=scroll.content.anchoredPosition.y;Check(offset>100,"long stack scrolls "+count);Click(rail.Find("CollapseStack").GetComponent<UnityEngine.UI.Button>());yield return Wait(.35f);Click(rail.Find("CollapseStack").GetComponent<UnityEngine.UI.Button>());yield return Wait(.35f);Check(Mathf.Abs(scroll.content.anchoredPosition.y-offset)<1,"scroll retained through hide "+count);}
 }
 var buckets=Sample();buckets.players[0].manaPoolEntries=new[]{new ForgeManaPoolEntry{id="mana:W",symbol="W",count=1},new ForgeManaPoolEntry{id="mana:U",symbol="U",count=10},new ForgeManaPoolEntry{id="mana:B",symbol="B",count=100},new ForgeManaPoolEntry{id="mana:R",symbol="R",count=1000},new ForgeManaPoolEntry{id="mana:G",symbol="G",count=23},new ForgeManaPoolEntry{id="mana:C",symbol="C",count=9},new ForgeManaPoolEntry{id="mana:S:W",symbol="W",count=11,snow=true},new ForgeManaPoolEntry{id="mana:restricted",symbol="G",count=4,restricted=true}};
 buckets.request.options=new[]{new ForgeOption{id="mana:W",label="Pay white"},new ForgeOption{id="mana:restricted",label="Pay restricted green"},new ForgeOption{id="ok",label="OK"}};int calls=0,last=-1;
 ui.Present(buckets,1,true,i=>{calls++;last=i;},null,"");yield return Wait(.4f);ManaChecks("large mixed buckets",false);
 var mana=Root().Find("ManaPoolChips").GetComponent<BattlefieldHorizontalScroll>();mana.horizontalNormalizedPosition=0;yield return Wait();Check(Click(mana.content.Find("Pool_W").GetComponent<UnityEngine.UI.Button>())&&calls==1&&last==0,"variable-width white exact native payment");ui.TrySubmitPresentedOption(0,buckets.request.requestId);Check(calls==1,"duplicate payment guard");
 buckets.request.requestId++;ui.Present(buckets,1,true,i=>{calls++;last=i;},null,"");yield return Wait(.2f);
 var next=Root().Find("ManaPoolChips_Navigation/Next").GetComponent<UnityEngine.UI.Button>();for(int i=0;i<10&&next.interactable;i++){Check(Click(next),"variable-width paging reachable "+i);yield return Wait(.3f);}
 var restricted=mana.content.Find("Pool_Restricted_mana:restricted").GetComponent<UnityEngine.UI.Button>();Check(restricted.GetComponent<CanvasGroup>().alpha>.9f,"last restricted bucket fully visible");Check(Click(restricted)&&calls==2&&last==1,"restricted bucket keeps native option identity");
 Restore();yield return null;while(!GamePreferences.Ready)yield return null;ui.Present(Sample(),1,false,null,null,"副本 · 只读布局预览");yield return Wait(.5f);Save("COMPLETE");
}
System.Collections.IEnumerator Guard(){var run=Run();for(int i=0;i<100000;i++){bool next=false;object value=null;try{next=run.MoveNext();if(next)value=run.Current;}catch(Exception e){failed++;results.Add(new{exception=e.ToString()});Restore();Save("ERROR");yield break;}if(!next)break;yield return value;}}
Save("STARTING");ui.StartCoroutine(Guard());return "Focused stack slide, clarity and adaptive mana UI checks started";
