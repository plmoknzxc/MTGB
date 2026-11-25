const string W="D:/project/MTGB/design-demos/battlefield-redesign-2026-10-24/";
if(!Application.isPlaying||UnityEngine.SceneManagement.SceneManager.GetActiveScene().name!=ForgeBattlefieldController.SceneName)throw new System.Exception("Temporary integrated play scene required");
var ui=UnityEngine.Object.FindFirstObjectByType<ForgeBattlefieldController>()??new GameObject("ActionUiInventory").AddComponent<ForgeBattlefieldController>();
var flags=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance;
var serializer=System.AppDomain.CurrentDomain.GetAssemblies().First(a=>a.GetName().Name=="Newtonsoft.Json").GetType("Newtonsoft.Json.JsonConvert").GetMethod("SerializeObject",new[]{typeof(object)});
string Json(object o)=>(string)serializer.Invoke(null,new object[]{o});
object Bounds(RectTransform r){var c=new Vector3[4];r.GetWorldCorners(c);return new{x=c.Min(p=>p.x),y=Screen.height-c.Max(p=>p.y),width=c.Max(p=>p.x)-c.Min(p=>p.x),height=c.Max(p=>p.y)-c.Min(p=>p.y)};}
System.Collections.IEnumerator Run(){string language=GamePreferences.Language;float scale=GamePreferences.TextScale;bool enabled=ui.enabled;ui.enabled=false;var input=UnityEngine.EventSystems.EventSystem.current.currentInputModule;bool inputEnabled=input.enabled;input.enabled=false;var samples=new System.Collections.Generic.List<object>();
 try{GamePreferences.SetScale(1);typeof(ForgeBattlefieldController).GetField("previewControlMode",flags).SetValue(ui,ForgeControlMode.On);
 foreach(string locale in new[]{"zh-CN","en-US","ja-JP"}){
 GamePreferences.SetLanguage(locale);while(!GamePreferences.Ready)yield return null;
 foreach(string path in System.IO.Directory.GetFiles(W+"action-ui-inputs","*.json").OrderBy(p=>p)){
 var v=JsonUtility.FromJson<ForgeSeatView>(System.IO.File.ReadAllText(path));int actor=v.request?.seat>0?v.request.seat:v.seat;
 ui.Present(null,actor,false,null,null,"");ui.Present(v,actor,true,_=>{},null,"");yield return new WaitForSecondsRealtime(.3f);yield return new WaitForEndOfFrame();Canvas.ForceUpdateCanvases();
 foreach(var c in UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))if(c.name.StartsWith("CardNotice_"))c.gameObject.SetActive(false);
 var root=(Transform)typeof(ForgeBattlefieldController).GetField("authored",flags).GetValue(ui);var dock=root.Find("CornerCluster/ControlRow");
 var buttons=dock.GetComponentsInChildren<UnityEngine.UI.Button>().Where(b=>b.gameObject.activeInHierarchy).Select(b=>new{name=b.name,label=string.Join(" ",b.GetComponentsInChildren<UnityEngine.UI.Text>().Select(t=>t.text).Where(t=>!string.IsNullOrWhiteSpace(t))),enabled=b.interactable,bounds=Bounds((RectTransform)b.transform)}).ToArray();
 string[] names={"CardSelection","PileSelection","CardAbilityChoices","AbilityOrder","DamageAllocation","MulliganChoices","TargetOrigin","ForgeChoices","ChoiceHintPanel"};
 var panels=root.GetComponentsInChildren<RectTransform>().Where(r=>r.gameObject.activeInHierarchy&&names.Contains(r.name)).Select(r=>new{name=r.name,inDock=r.IsChildOf(dock),bounds=Bounds(r),buttons=r.GetComponentsInChildren<UnityEngine.UI.Button>().Where(b=>b.gameObject.activeInHierarchy).Select(b=>new{name=b.name,label=string.Join(" ",b.GetComponentsInChildren<UnityEngine.UI.Text>().Select(t=>t.text).Where(t=>!string.IsNullOrWhiteSpace(t))),enabled=b.interactable,bounds=Bounds((RectTransform)b.transform)}).ToArray()}).ToArray();
 string key=System.IO.Path.GetFileNameWithoutExtension(path);string numericValue=dock.GetComponentsInChildren<UnityEngine.UI.Text>().FirstOrDefault(t=>t.name=="NumericValue")?.text;samples.Add(new{key,locale,kind=v.request?.kind,sourceCard=v.request?.sourceCard?.name,numericValue,buttons,panels});
 if(locale=="zh-CN"){ScreenCapture.CaptureScreenshot(W+"native-action-"+key+".png");yield return new WaitForEndOfFrame();}
 }
 }
 System.IO.File.WriteAllText(W+"native-action-ui.json",Json(new{scope="Actual native request UI projection and geometry, not rule execution",samples}));
 System.IO.File.WriteAllText(W+"native-action-ui-result.txt","PASS: captured "+samples.Count+" native UI projections; undo adapter separately marked.");
 }finally{input.enabled=inputEnabled;ui.enabled=enabled;GamePreferences.SetLanguage(language);GamePreferences.SetScale(scale);}
}
UnityEngine.Object.FindFirstObjectByType<GamePreferences>().StartCoroutine(Run());return "Capturing bounded action/UI inventory in three locales";
