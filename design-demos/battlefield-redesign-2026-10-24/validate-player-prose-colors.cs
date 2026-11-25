const string W="D:/project/MTGB/design-demos/battlefield-redesign-2026-10-24/";
var ui=UnityEngine.Object.FindFirstObjectByType<ForgeBattlefieldController>();var flags=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance;
if(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name!=ForgeBattlefieldController.SceneName||ui==null)return "Open the duel battlefield in Play Mode.";
var names=typeof(ForgeBattlefieldController).GetMethod("PlayerNamesRichText",flags);var field=typeof(ForgeBattlefieldController).GetField("service",flags);var savedService=field.GetValue(ui);
var service=OnlineMatchService.EnsureInstance();if(service.IsConnected||service.IsInMatch)return "Refusing to alter live room state for presentation validation";
var lobby=typeof(OnlineMatchService).GetProperty("LobbyState");var localSeat=typeof(OnlineMatchService).GetProperty("LocalSeat");var savedLobby=lobby.GetValue(service);var savedSeat=localSeat.GetValue(service);string oldLanguage=GamePreferences.Language;float oldScale=GamePreferences.TextScale;
var results=new System.Collections.Generic.List<object>();int checks=0,failed=0;
void Check(bool pass,string context){checks++;if(!pass)failed++;results.Add(new{pass,context});}
string P(string s,int exact=0)=>(string)names.Invoke(ui,new object[]{s,exact});
string Ref(int n)=>"\uE000"+n+"\uE001";
bool Colored(string value,string name,string color)=>value.Contains("<color="+color+">"+name+"</color>");
void SetNames(string own,string other){lobby.SetValue(service,new OnlineLobbyState{players=new System.Collections.Generic.List<OnlineLobbyPlayer>{new(){seat=1,displayName=own},new(){seat=2,displayName=other}}});}
void Save(string status){var method=Type.GetType("Newtonsoft.Json.JsonConvert, Newtonsoft.Json").GetMethod("SerializeObject",new[]{typeof(object)});System.IO.File.WriteAllText(W+"player-prose-colors-validation.json",(string)method.Invoke(null,new object[]{new{status,checks,failed,scope="Prose role words neutral, explicit nicknames colored; no game-rule effects",results}}));}
void Restore(){field.SetValue(ui,savedService);lobby.SetValue(service,savedLobby);localSeat.SetValue(service,savedSeat);GamePreferences.SetLanguage(oldLanguage);GamePreferences.SetScale(oldScale);}
System.Collections.IEnumerator Run(){
 field.SetValue(ui,savedService);localSeat.SetValue(service,1);
 foreach(string language in new[]{"zh-CN","en-US","ja-JP"}){
  field.SetValue(ui,savedService);GamePreferences.SetLanguage(language);yield return null;while(!GamePreferences.Ready)yield return null;field.SetValue(ui,service);
  lobby.SetValue(service,null);string roles="你和我，你的对手；You and I, your opponent; あなたと私、対戦相手。";
  Check(!P(roles).Contains("<color="),language+" no lobby role words stay neutral");Check(!P(Ref(1)+" / "+Ref(2)).Contains("<color="),language+" native references with fallback role names stay neutral");
  SetNames("Sam","Luna");string full=P(roles+" Sam / Luna");Check(Colored(full,"Sam","#5DB9F5")&&Colored(full,"Luna","#F17C7C"),language+" concrete own and enemy names colored");Check(!full.Contains("<color=#5DB9F5>You")&&!full.Contains("<color=#F17C7C>对手")&&!full.Contains("<color=#5DB9F5>你"),language+" nicknames do not color ordinary roles");Check(Colored(P(Ref(1)+" / "+Ref(2)),"Sam","#5DB9F5")&&Colored(P(Ref(2)),"Luna","#F17C7C"),language+" identity references preserve name colors");
  SetNames("You","Opponent");Check(!P("You choose. Your opponent draws.").Contains("<color="),language+" nicknames equal role words do not tint rules prose");Check(Colored(P(Ref(1)),"You","#5DB9F5")&&Colored(P(Ref(2)),"Opponent","#F17C7C"),language+" explicit identity can color a role-like actual nickname");
  SetNames("Same","Same");Check(!P("Same chooses.").Contains("<color="),language+" ambiguous duplicate plain name stays neutral");Check(Colored(P(Ref(1)),"Same","#5DB9F5")&&Colored(P(Ref(2)),"Same","#F17C7C"),language+" duplicate names retain native identity color");
  SetNames("Sam","Luna");Check(!P("Sampler and Lunatic").Contains("<color="),language+" name substrings not colored");SetNames("<Sam>","Luna");Check(P(Ref(1)).Contains("＜Sam＞")&&!P(Ref(1)).Contains("><Sam>"),language+" nicknames remain safe literal user text");
  SetNames("Sam","Luna");localSeat.SetValue(service,3);Check(!P("Sam / Luna "+Ref(1)).Contains("<color="),language+" spectator neutral behavior retained");localSeat.SetValue(service,1);
  foreach(float scale in new[]{.85f,1f,1.3f}){
   GamePreferences.SetScale(scale);var v=JsonUtility.FromJson<ForgeSeatView>(System.IO.File.ReadAllText(W+"native-full-seat.json"));var e=v.stackItems[0];e.controllerSeat=1;e.kind="ability";e.description="";e.rulesText=roles+"\n"+Ref(1)+" / "+Ref(2);e.castingChoices=Array.Empty<ForgeCastingChoice>();v.stackItems=new[]{e};v.stackCount=1;v.stack=new[]{""};
   field.SetValue(ui,savedService);ui.Present(null,1,false,null,null,"");yield return null;field.SetValue(ui,service);ui.Present(v,1,false,null,null,"");field.SetValue(ui,savedService);yield return new WaitForSecondsRealtime(.12f);Canvas.ForceUpdateCanvases();var row=GameObject.Find("Canvas").transform.Find("StackColumn/Viewport/Content/StackEntry_0");var text=row.Find("CompactEffect/Content/CompactNote/Symbols").GetComponent<TMPro.TextMeshProUGUI>();
   Check(Colored(text.text,"Sam","#5DB9F5")&&Colored(text.text,"Luna","#F17C7C"),language+"/"+scale+" actual stack TMP colors names");Check(text.text.Contains(roles)&&!text.text.Contains("<color=#5DB9F5>你"),language+"/"+scale+" actual stack prose roles neutral");Check(row.Find("CompactOwner").GetComponent<UnityEngine.UI.Text>().color.b>row.Find("CompactOwner").GetComponent<UnityEngine.UI.Text>().color.r,language+"/"+scale+" stack ownership label keeps blue");
  }
 }
 Restore();yield return null;while(!GamePreferences.Ready)yield return null;var baseline=JsonUtility.FromJson<ForgeSeatView>(System.IO.File.ReadAllText(W+"native-full-seat.json"));ui.Present(null,1,false,null,null,"");yield return null;ui.Present(baseline,1,false,null,null,"副本 · 只读布局预览");Save("COMPLETE");
}
System.Collections.IEnumerator Guard(){var it=Run();for(int i=0;i<100000;i++){bool next=false;object value=null;try{next=it.MoveNext();if(next)value=it.Current;}catch(Exception e){failed++;results.Add(new{exception=e.ToString()});Restore();Save("ERROR");yield break;}if(!next)break;yield return value;}}
Save("STARTING");ui.StartCoroutine(Guard());return "Player prose color validation started";
