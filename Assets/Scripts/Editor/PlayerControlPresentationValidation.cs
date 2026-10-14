#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public static class PlayerControlPresentationValidation
{
    const string Output=".tools/player-control-ui";
    static bool running;
    static readonly System.Collections.Generic.HashSet<string> launches=new System.Collections.Generic.HashSet<string>();
    public static void RunOnce(string launch){if(launches.Add(launch))Run();}
    static void Check(bool condition,string message){if(!condition)throw new Exception(message);}
    public static void Run()
    {
        if(!Application.isPlaying)throw new Exception("Enter IntegratedBattlefield Play Mode first.");
        if(running)return;
        Directory.CreateDirectory(Output);File.WriteAllText(Output+"/result.txt","RUNNING");
        ForgeControlModeValidation.Policy();
        var ui=UnityEngine.Object.FindFirstObjectByType<ForgeBattlefieldController>();
        if(ui==null)ui=new GameObject("PlayerControlValidation").AddComponent<ForgeBattlefieldController>();
        running=true;ui.StartCoroutine(Guard(Validate(ui)));
    }
    static IEnumerator Guard(IEnumerator iterator)
    {
        string original=GamePreferences.Language;
        try {
            while(true) {
                object next;
                try{if(!iterator.MoveNext())break;next=iterator.Current;}
                catch(Exception error){File.WriteAllText(Output+"/result.txt","FAIL "+error);throw;}
                yield return next;
            }
            File.WriteAllText(Output+"/result.txt","PASS: real whole-turn/both seats/combat-only request replay in zh-CN/en-US/ja-JP; automatic controlled hand with no banner/toggle, mana owner, pool/card/payment/target/attack raycast and original index, independently actionable stale guard, duplicate/viewer guard, release, spectator private projection and AUTO main phase policy. Isolated actual-request replay, not a full network-match test. Effects are separately asserted by Test-PlayerControl.py.");
        }finally{running=false;GamePreferences.SetLanguage(original);}
    }
    static ForgeSeatView Read(string name)=>JsonUtility.FromJson<ForgeSeatView>(File.ReadAllText("Tools/Validation/Reports/PlayerControl/"+name+".json"));
    static void HandMatches(ForgeCardView[] cards)
    {
        var hand=GameObject.Find("PlayerHandRoot").transform;
        var actual=Enumerable.Range(0,hand.childCount).Select(i=>hand.GetChild(i)).Where(t=>t.gameObject.activeInHierarchy&&t.name.StartsWith("Card_")).Select(t=>t.name).OrderBy(n=>n).ToArray();
        var expected=(cards??Array.Empty<ForgeCardView>()).Select(c=>"Card_"+c.id).OrderBy(n=>n).ToArray();
        Check(actual.SequenceEqual(expected),"Wrong player's hand remains displayed");
    }
    static Vector2 Center(Component button)=>RectTransformUtility.WorldToScreenPoint(null,button.transform.TransformPoint(((RectTransform)button.transform).rect.center));
    static void Click(Button button)
    {
        var rect=((RectTransform)button.transform).rect;
        foreach(float y in new[]{0f,.35f,.45f,-.35f})foreach(float x in new[]{0f,-.45f,-.3f,.3f,.45f}) {
            var pointer=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(null,button.transform.TransformPoint(rect.center+new Vector2(rect.width*x,rect.height*y))),button=PointerEventData.InputButton.Left};
            var hits=new System.Collections.Generic.List<RaycastResult>();EventSystem.current.RaycastAll(pointer,hits);
            if(hits.Count==0||hits[0].gameObject.GetComponentInParent<Button>()!=button)continue;
            ExecuteEvents.ExecuteHierarchy(hits[0].gameObject,pointer,ExecuteEvents.pointerClickHandler);return;
        }
        throw new Exception("Controlled action has no exposed click point: "+button.name);
    }
    static IEnumerator Validate(ForgeBattlefieldController ui)
    {
        foreach(string language in new[]{"zh-CN","en-US","ja-JP"}) {
            int revision=GamePreferences.Revision;bool changing=GamePreferences.Language!=language;
            GamePreferences.SetLanguage(language);float deadline=Time.realtimeSinceStartup+8;
            while(changing&&GamePreferences.Revision==revision&&Time.realtimeSinceStartup<deadline)yield return null;
            Check(!changing||GamePreferences.Revision!=revision,"Locale not loaded");
            foreach(string test in new[]{"turn-one","turn-two","combat"}) {
                int master=test=="turn-two"?2:1,slave=3-master;
                foreach(string phase in new[]{"casting-Opt","controlled-payment","controlled-target","controlled-scry","controlled-attack","released"}) {
                    var view=Read(test+"-"+phase);int calls=0,last=-1;Action<int> submit=i=>{calls++;last=i;};
                    ui.Present(null,master,false,null,null,"");ui.Present(view,master,true,submit,null,"");
                    yield return new WaitForSecondsRealtime(.18f);Canvas.ForceUpdateCanvases();
                    var bar=GameObject.Find("ControlledPlayerHandBar");
                    if(phase=="released") {
                        Check(bar==null||!bar.activeInHierarchy,"Control banner persisted after release");
                        HandMatches(view.players[master-1].hand);
                        Check(!ui.TrySubmitPresentedOption(0,123456),"Released/stale control still actionable");
                    }else {
                        Check(view.request.seat==master&&view.request.playerSeat==slave,"Operator/player identity conflated");
                        Check(bar==null&&GameObject.Find("PersonalHandToggle")==null,"Unwanted control banner/toggle");
                        HandMatches(view.players[slave-1].hand);
                        if(phase=="casting-Opt"||phase=="controlled-payment") {
                            int expectedBlue=phase=="casting-Opt"?2:1;
                            Check(view.players[slave-1].manaPool[1]==expectedBlue&&view.players[master-1].manaPool[1]==0,"Replay does not distinguish mana owners");
                            var pool=GameObject.Find("ManaPoolChips");
                            Check(pool!=null&&pool.transform.Find("Pool_U/Count").GetComponent<Text>().text==expectedBlue.ToString()&&pool.transform.Find("Pool_U").gameObject.activeSelf,"Mana row displays master instead of controlled player");
                        }
                        if(phase=="casting-Opt") {
                            var opt=view.players[slave-1].hand.First(c=>c.name=="Opt");int expectedIndex=ui.OptionIndex("card:"+opt.id);
                            Click(GameObject.Find("Card_"+opt.id).GetComponent<Button>());Check(calls==1&&last==expectedIndex,"Controlled hand submitted wrong card index");
                        }else if(phase=="controlled-attack") {
                            var option=view.request.options.First(o=>o.id.StartsWith("attack:")&&o.defenderKind=="player"&&o.defenderId==view.players[master-1].targetId);
                            Check(ui.BeginAttackSelection(option.attackerId,view.request.requestId),"Controlled attacker cannot be selected");yield return null;
                            var plate=GameObject.Find("PlayerPlate").GetComponent<Button>();
                            Check(ui.TryChooseAttackAt(Center(plate),view.request.requestId),"Controlled attack cannot target controlling player");
                            Check(calls==1&&last==ui.OptionIndex(option.id),"Attack lost original option index");
                        }else {
                            int index=phase=="controlled-payment"?Array.FindIndex(view.request.options,o=>o.id.StartsWith("mana:")):
                                phase=="controlled-target"?Array.FindIndex(view.request.options,o=>o.id=="player:"+view.players[slave-1].targetId):ui.OptionIndex("confirm-scry");
                            if(phase=="controlled-payment"&&index<0) {
                                var source=view.players[slave-1].battlefield.First(c=>c.name=="Mountain"&&ui.OptionIndex("card:"+c.id)>=0);
                                index=ui.OptionIndex("card:"+source.id);
                            }
                            Check(index>=0,"Required replay option missing: "+phase);
                            ui.Choose(index,view.request.requestId-1);Check(calls==0,"Actionable stale request accepted");
                            if(phase=="controlled-target") {
                                var plate=GameObject.Find("OpponentPlate").GetComponent<Button>();
                                Check(ui.TryChooseTargetAt(Center(plate),view.request.requestId),"Controlled self-target cannot be clicked");
                            }else {
                                string option=view.request.options[index].id;
                                var button=GameObject.Find(phase=="controlled-scry"?"SelectionAction_"+index:option.StartsWith("mana:")?"Pool_"+option.Substring(5):option.StartsWith("card:")?"Card_"+option.Substring(5):"Option_"+index)?.GetComponent<Button>();
                                Check(button!=null,"Required action has no rendered button");Click(button);
                            }
                            Check(calls==1&&last==index,"Controlled action chose wrong index");
                        }
                        ui.Choose(last,view.request.requestId);Check(calls==1,"Duplicate control submission accepted");
                    }
                    ScreenCapture.CaptureScreenshot(Output+"/"+language+"-"+test+"-"+phase+".png");yield return new WaitForEndOfFrame();
                }
                var denied=Read(test+"-slave-view");int deniedCalls=0;ui.Present(denied,slave,false,_=>deniedCalls++,null,"");yield return null;
                HandMatches(denied.players[slave-1].hand);ui.Choose(0,123456);Check(deniedCalls==0,"Controlled player can submit");
                var controlled=Read(test+"-casting-Opt");
                var publicState=OnlineSpectatorView.Create(new OnlinePlayerViewState{matchId="control-public",forge=controlled},3).forge;
                Check(publicState.request==null&&publicState.players.All(p=>p.hand.Length==0),"Spectator projection reveals control hand/choice");
                Check(controlled.players[slave-1].hand.Length>0,"Spectator projection mutated private source");
                ui.Present(publicState,1,false,null,null,"");yield return null;HandMatches(Array.Empty<ForgeCardView>());
                ScreenCapture.CaptureScreenshot(Output+"/"+language+"-"+test+"-spectator.png");yield return new WaitForEndOfFrame();
            }
        }
    }
}
#endif
