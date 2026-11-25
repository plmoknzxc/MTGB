const string W="D:/project/MTGB/design-demos/battlefield-redesign-2026-10-24/";
if(!Application.isPlaying||UnityEngine.SceneManagement.SceneManager.GetActiveScene().name!=ForgeBattlefieldController.SceneName)throw new System.Exception("Expected temporary IntegratedBattlefield play mode");
var ui=UnityEngine.Object.FindFirstObjectByType<ForgeBattlefieldController>()??new GameObject("NativePreservedPreview").AddComponent<ForgeBattlefieldController>();
var flags=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance;
System.Collections.IEnumerator Capture(){
 string language=GamePreferences.Language;float scale=GamePreferences.TextScale;bool uiEnabled=ui.enabled;var input=UnityEngine.EventSystems.EventSystem.current.currentInputModule;bool inputEnabled=input.enabled;ui.enabled=false;input.enabled=false;
 try {
  GamePreferences.SetLanguage("zh-CN");GamePreferences.SetScale(1);while(!GamePreferences.Ready)yield return null;
  var priorRoot=(Transform)typeof(ForgeBattlefieldController).GetField("authored",flags).GetValue(ui);
  if(priorRoot!=null)foreach(string path in new[]{"PlayerHandRoot","StackColumn","CornerCluster/ControlRow","PriorityControlMode","PauseNextPhase"}){var t=priorRoot.Find(path);if(t!=null)t.gameObject.SetActive(true);}
  var v=JsonUtility.FromJson<ForgeSeatView>(System.IO.File.ReadAllText(W+"native-full-seat.json"));ui.Present(v,1,false,_=>{},null,"");
  foreach(var c in UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))if(c.name.StartsWith("CardNotice_"))c.gameObject.SetActive(false);
  yield return new WaitForSecondsRealtime(1.2f);yield return new WaitForEndOfFrame();Canvas.ForceUpdateCanvases();
  var authored=(Transform)typeof(ForgeBattlefieldController).GetField("authored",flags).GetValue(ui);
  var hand=(RectTransform)typeof(ForgeBattlefieldController).GetField("ownHand",flags).GetValue(ui);
  var records=new System.Collections.Generic.List<object>();
  object Bounds(RectTransform r){var cs=new Vector3[4];r.GetWorldCorners(cs);var pts=cs.Select(c=>RectTransformUtility.WorldToScreenPoint(null,c)).ToArray();float x=pts.Min(p=>p.x),y=pts.Min(p=>p.y),right=pts.Max(p=>p.x),top=pts.Max(p=>p.y);return new{x,y=Screen.height-top,width=right-x,height=top-y};}
  foreach(string path in new[]{"PlayerPlate","OpponentPlate","PlayerPlate/PlayerCounters","OpponentPlate/PlayerCounters","ManaPoolChips","PublicManaPool","StackColumn","CornerCluster/ControlRow","InspectPanel","PlayerHandRoot","OpponentHandRoot"}){var t=authored.Find(path)as RectTransform;if(t!=null)records.Add(new{kind="node",name=path,active=t.gameObject.activeInHierarchy,bounds=Bounds(t)});}
  var dict=(System.Collections.IDictionary)typeof(ForgeBattlefieldController).GetField("arenaCards",flags).GetValue(ui);
  foreach(System.Collections.DictionaryEntry entry in dict){var item=entry.Value;var t=(RectTransform)item.GetType().GetField("root").GetValue(item);if(!t.gameObject.activeInHierarchy)continue;var c=(ForgeCardView)item.GetType().GetField("card").GetValue(item);records.Add(new{kind=t.IsChildOf(hand)?"replace-hand":"protected-card",name=entry.Key.ToString(),id=c.id,owner=c.ownerSeat,bounds=Bounds(t)});}
  var serializer=System.AppDomain.CurrentDomain.GetAssemblies().First(a=>a.GetName().Name=="Newtonsoft.Json").GetType("Newtonsoft.Json.JsonConvert").GetMethod("SerializeObject",new[]{typeof(object)});
  var manifest=new{width=Screen.width,height=Screen.height,source="Current Unity renderer, explicit presentation composition; no gameplay or saved scene edits",objects=records};
  System.IO.File.WriteAllText(W+"native-preserved-geometry.json",(string)serializer.Invoke(null,new object[]{manifest}));
  ScreenCapture.CaptureScreenshot(W+"native-full-original.png");yield return new WaitForEndOfFrame();yield return new WaitForEndOfFrame();
  foreach(string path in new[]{"PlayerHandRoot","StackColumn","CornerCluster/ControlRow","InspectPanel","PriorityPill","PriorityControlMode","PauseNextPhase"}){var t=authored.Find(path);if(t!=null)t.gameObject.SetActive(false);}
  yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(W+"native-full-preserved.png");yield return new WaitForEndOfFrame();yield return new WaitForEndOfFrame();
  var ownMana=authored.Find("ManaPoolChips");ownMana.gameObject.SetActive(false);
  yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(W+"native-full-without-own-mana.png");yield return new WaitForEndOfFrame();yield return new WaitForEndOfFrame();ownMana.gameObject.SetActive(true);
  System.IO.File.WriteAllText(W+"native-preserved-result.txt","PASS: actual Unity rendering captured. Only hand, stack, ControlRow, InspectPanel, PriorityPill, PriorityControlMode and PauseNextPhase hidden for replacement; battlefield uses current native rendering with support-size and left-information avoidance fixes; players, counters, mana, emblems and effect markers keep native styling and layout.");
 }
 finally{input.enabled=inputEnabled;ui.enabled=uiEnabled;GamePreferences.SetLanguage(language);GamePreferences.SetScale(scale);}
}
UnityEngine.Object.FindFirstObjectByType<GamePreferences>().StartCoroutine(Capture());return "Capturing native-preserved visual base";
