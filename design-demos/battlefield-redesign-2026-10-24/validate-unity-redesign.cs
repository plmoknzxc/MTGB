const string W="D:/project/MTGB/design-demos/battlefield-redesign-2026-10-24/";
if(!Application.isPlaying || UnityEngine.SceneManagement.SceneManager.GetActiveScene().name!=ForgeBattlefieldController.SceneName)throw new Exception("Open the duel battlefield in Play Mode.");
var ui=UnityEngine.Object.FindFirstObjectByType<ForgeBattlefieldController>();
var flags=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance;
var rows=new System.Collections.Generic.List<object>(); int checks=0,failed=0;
float savedScale=GamePreferences.TextScale; string savedLanguage=GamePreferences.Language;
void Check(bool pass,string context){checks++;if(!pass)failed++;rows.Add(new {pass,context});}
Rect Bounds(RectTransform r){var cs=new Vector3[4];r.GetWorldCorners(cs);return Rect.MinMaxRect(cs.Min(p=>p.x),cs.Min(p=>p.y),cs.Max(p=>p.x),cs.Max(p=>p.y));}
ForgeSeatView Read(string key)=>JsonUtility.FromJson<ForgeSeatView>(System.IO.File.ReadAllText(W+"action-ui-inputs/"+key+".json"));
Transform Root()=>GameObject.Find("Canvas").transform;
System.Collections.IEnumerator Settle(){yield return null;yield return null;Canvas.ForceUpdateCanvases();}
void Save(string status){System.IO.File.WriteAllText(W+"unity-redesign-validation.json",(string)System.Type.GetType("Newtonsoft.Json.JsonConvert, Newtonsoft.Json").GetMethod("SerializeObject",new[]{typeof(object)}).Invoke(null,new object[]{new{status,checks,failed,rows}}));}
bool Hit(UnityEngine.UI.Button button, bool click=false){
 var bounds=Bounds((RectTransform)button.transform);var visiblePoint=new Vector2((Mathf.Max(0,bounds.xMin)+Mathf.Min(Screen.width,bounds.xMax))*.5f,(Mathf.Max(0,bounds.yMin)+Mathf.Min(Screen.height,bounds.yMax))*.5f);
 var p=new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current){position=visiblePoint,button=UnityEngine.EventSystems.PointerEventData.InputButton.Left};
 var hits=new System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult>();UnityEngine.EventSystems.EventSystem.current.RaycastAll(p,hits);
 bool ok=hits.Count>0&&hits[0].gameObject.GetComponentInParent<UnityEngine.UI.Button>()==button;
 if(click&&ok){UnityEngine.EventSystems.ExecuteEvents.ExecuteHierarchy(hits[0].gameObject,p,UnityEngine.EventSystems.ExecuteEvents.pointerDownHandler);UnityEngine.EventSystems.ExecuteEvents.ExecuteHierarchy(hits[0].gameObject,p,UnityEngine.EventSystems.ExecuteEvents.pointerUpHandler);UnityEngine.EventSystems.ExecuteEvents.ExecuteHierarchy(hits[0].gameObject,p,UnityEngine.EventSystems.ExecuteEvents.pointerClickHandler);}
 return ok;
}
System.Collections.IEnumerator Run(){
 while(!GamePreferences.Ready)yield return null;
 foreach(string language in new[]{"zh-CN","en-US","ja-JP"}){
  GamePreferences.SetLanguage(language);yield return null;while(!GamePreferences.Ready)yield return null;
  foreach(float scale in new[]{.85f,1f,1.3f}){
   GamePreferences.SetScale(scale);
   foreach(string file in System.IO.Directory.GetFiles(W+"action-ui-inputs/","*.json")){
    string key=System.IO.Path.GetFileNameWithoutExtension(file);var v=Read(key);int calls=0,last=-1;
    ui.Present(null,1,false,null,null,"");yield return Settle();ui.Present(v,v.seat,true,i=>{calls++;last=i;},null,"布局交互检查");yield return Settle();
    string label=language+"/"+scale+"/"+key;
    var root=Root();var mode=root.Find("PriorityControlMode") as RectTransform;
    var modeBounds=Bounds(mode);Check(modeBounds.yMin>=0&&modeBounds.yMin<14,label+" mode bottom anchored");
    var dock=root.Find("CornerCluster/ControlRow");var mana=root.Find("YourManaPool") as RectTransform;
    foreach(var b in dock.GetComponentsInChildren<UnityEngine.UI.Button>(false).Where(b=>b.name!="EndTurnButton"&&b.gameObject.activeInHierarchy)){
      var bounds=Bounds((RectTransform)b.transform);Check(bounds.yMin>=0&&bounds.xMax<=Screen.width+1,label+" button on screen "+b.name);
      if(b.interactable)Check(Hit(b),label+" button raycast "+b.name);
      foreach(var t in b.GetComponentsInChildren<UnityEngine.UI.Text>().Where(t=>!string.IsNullOrWhiteSpace(t.text)))Check(t.preferredHeight<=t.rectTransform.rect.height+2,label+" text fits "+b.name+"/"+t.text);
    }
    if(language=="zh-CN"&&scale==1f&&new[]{"priority","undo","phyrexian","manual-number","hand","piles","search"}.Contains(key)){ScreenCapture.CaptureScreenshot(W+"unity-"+key+".png");yield return new WaitForEndOfFrame();}
    rows.Add(new{context=label,buttons=dock.GetComponentsInChildren<UnityEngine.UI.Button>(false).Select(b=>b.name).ToArray()});Save("RUNNING");
   }
  }
 }
 GamePreferences.SetLanguage("zh-CN");yield return null;while(!GamePreferences.Ready)yield return null;GamePreferences.SetScale(1);
 foreach(string key in new[]{"priority","payment","number","undo","target"}){
   var v=Read(key);int calls=0,last=-1;ui.Present(null,1,false,null,null,"");yield return Settle();ui.Present(v,v.seat,true,i=>{calls++;last=i;},null,"");yield return Settle();
   var dock=Root().Find("CornerCluster/ControlRow");var b=dock.GetComponentsInChildren<UnityEngine.UI.Button>(false).FirstOrDefault(x=>x.interactable&&x.name!="Decrease"&&x.name!="Increase");
   if(b!=null){bool hit=Hit(b,true);Check(hit&&calls==1&&last>=0&&last<v.request.options.Length,"native action "+key+"/"+b.name);if(calls==1){b.onClick.Invoke();Check(calls==1,"duplicate guard "+key);}}
   else rows.Add(new{context="no enabled footer commit in "+key});
   ui.Present(v,v.seat,false,i=>calls++,null,"");yield return Settle();int before=calls;ui.TrySubmitPresentedOption(0,v.request.requestId);Check(calls==before,"nonacting guard "+key);
 }
 var baseline=Read("priority");ui.Present(null,1,false,null,null,"");yield return Settle();ui.Present(baseline,1,true,_=>{},null,"");yield return Settle();
 var hand=Root().Find("PlayerHandRoot") as RectTransform;float raised=hand.anchoredPosition.y;
 var p=new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current){position=new Vector2(Screen.width*.48f,Screen.height*.5f)};var hits2=new System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult>();UnityEngine.EventSystems.EventSystem.current.RaycastAll(p,hits2);
 rows.Add(new{context="empty battlefield hit",hits=hits2.Take(3).Select(h=>h.gameObject.name).ToArray()});
 typeof(ForgeBattlefieldController).GetMethod("HandleRedesignBattlefieldPointer",flags).Invoke(ui,new object[]{p.position});yield return Settle();Check(hand.anchoredPosition.y<raised-100,"field click lowers hand");
 ScreenCapture.CaptureScreenshot(W+"unity-hand-lowered.png");yield return new WaitForEndOfFrame();
 var card=hand.GetComponentsInChildren<UnityEngine.UI.Button>(false).First(b=>b.name.StartsWith("Card_"));Check(Hit(card,true),"tucked hand raycast");yield return Settle();Check(Mathf.Abs(hand.anchoredPosition.y-raised)<1,"hand click raises hand");
 foreach(int count in new[]{0,1,5,12,30}){
  var v=Read("priority");var entries=v.stackItems;v.stackItems=Enumerable.Range(0,count).Select(i=>{var e=JsonUtility.FromJson<ForgeStackItem>(JsonUtility.ToJson(entries[i%entries.Length]));e.id=90000+i;return e;}).ToArray();v.stack=v.stackItems.Select(e=>e.description).ToArray();v.stackCount=count;
  ui.Present(null,1,false,null,null,"");yield return Settle();ui.Present(v,1,false,null,null,"");yield return Settle();
  var column=Root().Find("StackColumn");Check(column.gameObject.activeSelf==(count>0),"stack visibility "+count);
  if(count>0){var scroll=column.Find("Viewport").GetComponent<UnityEngine.UI.ScrollRect>();scroll.verticalNormalizedPosition=0;yield return Settle();float travel=scroll.content.rect.height-scroll.viewport.rect.height;Check(count<5||scroll.content.anchoredPosition.y>travel-2,"stack bottom reachable "+count);if(count==30){ScreenCapture.CaptureScreenshot(W+"unity-stack-30-bottom.png");yield return new WaitForEndOfFrame();scroll.verticalNormalizedPosition=1;yield return Settle();ScreenCapture.CaptureScreenshot(W+"unity-stack-30-top.png");yield return new WaitForEndOfFrame();}}
 }
 GamePreferences.SetLanguage(savedLanguage);yield return null;while(!GamePreferences.Ready)yield return null;GamePreferences.SetScale(savedScale);ui.Present(null,1,false,null,null,"");yield return Settle();ui.Present(baseline,1,false,null,null,"副本 · 只读布局预览");typeof(ForgeBattlefieldController).GetMethod("SetRedesignHandExpanded",flags).Invoke(ui,new object[]{true});yield return new WaitForSecondsRealtime(.8f);ScreenCapture.CaptureScreenshot(W+"unity-redesign-final.png");yield return new WaitForEndOfFrame();Save("COMPLETE");
}
System.Collections.IEnumerator Guard(){var iter=Run();for(int frame=0;frame<100000;frame++){bool next=false;object value=null;try{next=iter.MoveNext();if(next)value=iter.Current;}catch(Exception e){rows.Add(new{exception=e.ToString()});failed++;Save("ERROR");GamePreferences.SetLanguage(savedLanguage);GamePreferences.SetScale(savedScale);yield break;}if(!next)break;yield return value;}}
Save("STARTING");ui.StartCoroutine(Guard());return "Native copied-scene validation started";
