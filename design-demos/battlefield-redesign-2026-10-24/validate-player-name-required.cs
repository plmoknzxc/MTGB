const string W="D:/project/MTGB/design-demos/battlefield-redesign-2026-10-24/";
var ui=UnityEngine.Object.FindFirstObjectByType<ForgeBattlefieldController>();if(ui==null||UnityEngine.SceneManagement.SceneManager.GetActiveScene().name!=ForgeBattlefieldController.SceneName)return "Open the duel battlefield in Play Mode.";
var flags=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance;var service=OnlineMatchService.EnsureInstance();if(service.IsOnline||service.IsInMatch)return "No live room state may be used by this display validation";
var lobbyProperty=typeof(OnlineMatchService).GetProperty("LobbyState");var savedLobby=lobbyProperty.GetValue(service);var serviceField=typeof(ForgeBattlefieldController).GetField("service",flags);var savedService=serviceField.GetValue(ui);string oldLanguage=GamePreferences.Language;float oldScale=GamePreferences.TextScale;
var results=new System.Collections.Generic.List<object>();int checks=0,failed=0;
void Check(bool pass,string context){checks++;if(!pass)failed++;results.Add(new{pass,context});}
void Save(string status){var method=Type.GetType("Newtonsoft.Json.JsonConvert, Newtonsoft.Json").GetMethod("SerializeObject",new[]{typeof(object)});System.IO.File.WriteAllText(W+"player-name-required-validation.json",(string)method.Invoke(null,new object[]{new{status,checks,failed,scope="Name-required lobby entry and stack owner identity presentation; no server deployment or new game-rule effects",results}}));}
var fixture=new GameObject("NameRequiredValidation");fixture.SetActive(false);var panel=fixture.AddComponent<OnlineLobbyPanel>();
UnityEngine.UI.Text Label(string name){var g=new GameObject(name,typeof(RectTransform),typeof(UnityEngine.UI.Text));g.transform.SetParent(fixture.transform,false);return g.GetComponent<UnityEngine.UI.Text>();}
var nameObject=new GameObject("NameInput",typeof(RectTransform),typeof(UnityEngine.UI.InputField));nameObject.transform.SetParent(fixture.transform,false);var input=nameObject.GetComponent<UnityEngine.UI.InputField>();input.textComponent=Label("InputText");var status=Label("Status");
typeof(OnlineLobbyPanel).GetField("nameInput",flags).SetValue(panel,input);typeof(OnlineLobbyPanel).GetField("browserStatus",flags).SetValue(panel,status);typeof(OnlineLobbyPanel).GetField("connectionStatusText",flags).SetValue(panel,status);
void Restore(){serviceField.SetValue(ui,savedService);lobbyProperty.SetValue(service,savedLobby);GamePreferences.SetLanguage(oldLanguage);GamePreferences.SetScale(oldScale);if(fixture!=null)UnityEngine.Object.Destroy(fixture);}
System.Collections.IEnumerator Run(){
 var sentinel=new OnlineLobbyState{roomId="name-guard-sentinel"};lobbyProperty.SetValue(service,sentinel);string profile=service.CurrentProfileId;
 foreach(string bad in new string[]{null,""," ","\t\r\n","\u3000","\u00a0",new string('a',25)}){
  Check(!OnlineMessageGuard.ValidateDisplayName(bad,out string error)&&!string.IsNullOrWhiteSpace(error),"shared validator rejects "+(bad==null?"null":JsonUtility.ToJson(new OnlineLobbyPlayer{displayName=bad})));
  Check(!OnlineMessageGuard.ValidateHello(new OnlineHelloMessage{displayName=bad,roomId="main",manifest=new OnlineContentManifest()},out _),"server hello rejects invalid name");
  Check(!service.Connect("127.0.0.1",7777,bad)&&!service.StartLanHost(7777,bad),"public client and host reject before networking");
  Check(!service.IsOnline&&ReferenceEquals(lobbyProperty.GetValue(service),sentinel)&&service.CurrentProfileId==profile,"invalid input cannot clear lobby, change profile or start transport");
 }
 lobbyProperty.SetValue(service,savedLobby);
 foreach(string good in new[]{"你","我","You","Same","Sam",new string('a',24)}){Check(OnlineMessageGuard.ValidateDisplayName(good,out _),"valid literal name accepted: "+good);Check(OnlineMessageGuard.ValidateHello(new OnlineHelloMessage{displayName=good,roomId="main",manifest=new OnlineContentManifest()},out _),"valid server hello shape: "+good);}
 foreach(string language in new[]{"zh-CN","en-US","ja-JP"}){
  GamePreferences.SetLanguage(language);yield return null;while(!GamePreferences.Ready)yield return null;
  foreach(string bad in new[]{""," \t ","\u3000"}){
   input.text=bad;status.text="";Check(!(bool)typeof(OnlineLobbyPanel).GetMethod("ValidatePlayerName",flags).Invoke(panel,null)&&!string.IsNullOrWhiteSpace(status.text),language+" empty UI field shows required-name message");
   foreach(string action in new[]{"SaveBrowserIdentity","CreateSelectedRoom","JoinListedRoom","BeginRoomCreation","Connect","Host"}){
    object[] args=action=="JoinListedRoom"?new object[]{null}:action=="BeginRoomCreation"?new object[]{false}:null;
    typeof(OnlineLobbyPanel).GetMethod(action,flags).Invoke(panel,args);
    Check(!service.IsOnline&&service.CurrentProfileId==profile,language+"/"+action+" blocks blank before saving or transport");
   }
  }
  foreach(string valid in new[]{"你","Sam"}){input.text=valid;Check((bool)typeof(OnlineLobbyPanel).GetMethod("ValidatePlayerName",flags).Invoke(panel,null),language+" UI accepts actual nickname "+valid);}
  foreach(var pair in new[]{new[]{"你","Luna"},new[]{"Same","Same"},new[]{"Ready","<Luna>"},new[]{new string('名',24),"Luna"}}){
   foreach(float scale in new[]{.85f,1f,1.3f}){
    GamePreferences.SetScale(scale);var v=JsonUtility.FromJson<ForgeSeatView>(System.IO.File.ReadAllText(W+"native-full-seat.json"));var mock=new OnlineLobbyState{players=new System.Collections.Generic.List<OnlineLobbyPlayer>{new(){seat=1,displayName=pair[0]},new(){seat=2,displayName=pair[1]}}};
    ui.Present(null,1,false,null,null,"");yield return null;lobbyProperty.SetValue(service,mock);serviceField.SetValue(ui,service);ui.Present(v,1,false,null,null,"");serviceField.SetValue(ui,savedService);lobbyProperty.SetValue(service,savedLobby);yield return new WaitForSecondsRealtime(.1f);Canvas.ForceUpdateCanvases();
    var content=GameObject.Find("Canvas").transform.Find("StackColumn/Viewport/Content");var opponent=content.Find("StackEntry_0/CompactOwner").GetComponent<UnityEngine.UI.Text>();var own=content.Find("StackEntry_1/CompactOwner").GetComponent<UnityEngine.UI.Text>();string safe=pair[1].Replace("<","＜").Replace(">","＞");
    Check(opponent.text.StartsWith(safe+" · ")&&own.text.StartsWith(pair[0]+" · "),language+"/"+scale+" exact owner names including role-like, duplicate and literal markup names");
    Check(opponent.color.r>opponent.color.b&&own.color.b>own.color.r,language+"/"+scale+" owner colors remain enemy red and own blue");
    Check(own.preferredHeight<=own.rectTransform.rect.height+2&&opponent.preferredHeight<=opponent.rectTransform.rect.height+2,language+"/"+scale+" owner label fits at maximum name length");
   }
  }
  Save("RUNNING");
 }
 Restore();yield return null;while(!GamePreferences.Ready)yield return null;var baseline=JsonUtility.FromJson<ForgeSeatView>(System.IO.File.ReadAllText(W+"native-full-seat.json"));ui.Present(null,1,false,null,null,"");yield return null;ui.Present(baseline,1,false,null,null,"副本 · 只读布局预览");Save("COMPLETE");
}
System.Collections.IEnumerator Guard(){var it=Run();for(int i=0;i<100000;i++){bool next=false;object value=null;try{next=it.MoveNext();if(next)value=it.Current;}catch(Exception e){failed++;results.Add(new{exception=e.ToString()});Restore();Save("ERROR");yield break;}if(!next)break;yield return value;}}
Save("STARTING");ui.StartCoroutine(Guard());return "Required-name and owner-label validation started";
