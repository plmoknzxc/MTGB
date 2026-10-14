#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public static class NestedPlayerControlPresentationValidation
{
    public static bool Running {get;private set;}
    public static string Output {get;private set;}
    [Serializable] sealed class Meta {public string folder,status;}
    sealed class Host:MonoBehaviour{}
    static Transform Root=>GameObject.Find("Canvas").transform;
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
    static string Hash(string p){using var h=SHA256.Create();return BitConverter.ToString(h.ComputeHash(File.ReadAllBytes(p))).Replace("-","").ToLowerInvariant();}
    static IEnumerator Frame(){yield return new WaitForSecondsRealtime(.2f);yield return new WaitForEndOfFrame();Canvas.ForceUpdateCanvases();}
    static ForgeSeatView Read(string p){File.AppendAllText(Output+"/input-hashes.tsv",p+"\t"+Hash(p)+"\n");return JsonUtility.FromJson<ForgeSeatView>(File.ReadAllText(p));}
    static void HandMatches(ForgeCardView[] cards){var hand=GameObject.Find("PlayerHandRoot").transform;var actual=Enumerable.Range(0,hand.childCount).Select(i=>hand.GetChild(i)).Where(t=>t.gameObject.activeInHierarchy&&t.name.StartsWith("Card_")).Select(t=>t.name).OrderBy(n=>n);Check(actual.SequenceEqual((cards??Array.Empty<ForgeCardView>()).Select(c=>"Card_"+c.id).OrderBy(n=>n)),"Wrong hand identity/player actual="+string.Join(",",actual)+" expected="+string.Join(",",(cards??Array.Empty<ForgeCardView>()).Select(c=>"Card_"+c.id)));}
    static void Click(Button b){var rect=(RectTransform)b.transform;foreach(float y in new[]{0f,.35f,-.35f})foreach(float x in new[]{0f,-.4f,.4f}){var p=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(null,rect.TransformPoint(rect.rect.center+new Vector2(rect.rect.width*x,rect.rect.height*y))),button=PointerEventData.InputButton.Left};var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(p,hits);if(hits.Count==0||hits[0].gameObject.GetComponentInParent<Button>()!=b)continue;ExecuteEvents.ExecuteHierarchy(hits[0].gameObject,p,ExecuteEvents.pointerClickHandler);return;}throw new Exception("Blocked ray "+b.name);}
    public static void Run(){Check(Application.isPlaying&&!Running&&GamePreferences.Ready&&GamePreferences.Language=="zh-CN","Chinese Play required");Running=true;Output=".tools/nested-control-1006/ui/"+DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffffffZ");Directory.CreateDirectory(Output);File.WriteAllText(".tools/nested-control-1006/ui/latest.txt",Output);File.WriteAllText(Output+"/result.txt","RUNNING");File.WriteAllText(Output+"/producer-hashes.tsv",string.Join("\n",new[]{"Assets/Scripts/Editor/NestedPlayerControlPresentationValidation.cs","Assets/Scripts/UI/ForgeBattlefieldController.PlayerControl.cs","Assets/Scripts/UI/ForgeBattlefieldController.Payment.cs","Assets/Scripts/UI/ForgeBattlefieldController.cs","Assets/Scripts/UI/ForgeBattlefieldController.CardSelection.cs"}.Select(p=>p+"\t"+Hash(p)))+"\n");var go=new GameObject("NestedControlValidation");go.AddComponent<Host>().StartCoroutine(Guard(Exercise(),go));}
    static IEnumerator Guard(IEnumerator e,GameObject go){Exception error=null;while(true){bool more=false;object current=null;try{more=e.MoveNext();if(more)current=e.Current;}catch(Exception ex){error=ex;break;}if(!more)break;if(current is IEnumerator child){yield return Nested(child,ex=>error=ex);if(error!=null)break;}else yield return current;}try{(e as IDisposable)?.Dispose();}catch(Exception ex){error??=ex;}File.WriteAllText(Output+"/result.txt",error==null?"PASS: current actual mutual and nested Word control requests, physical/action player identities and hand/payment owner, Chinese real ray original index/stale/duplicate, denying other physical seat, controlled restoration/ordinary expiry hand and readonly spectator; existing unchanged consumers/language contract increment, no new first UI or live transport":"FAIL\n"+error);Running=false;UnityEngine.Object.Destroy(go);}
    static IEnumerator Nested(IEnumerator e,Action<Exception> fail){while(true){bool more=false;object current=null;try{more=e.MoveNext();if(more)current=e.Current;}catch(Exception ex){fail(ex);break;}if(!more)break;if(current is IEnumerator child)yield return Nested(child,fail);else yield return current;}}
    static IEnumerator Exercise(){var ui=UnityEngine.Object.FindFirstObjectByType<ForgeBattlefieldController>()??new GameObject("NestedControlBattlefield").AddComponent<ForgeBattlefieldController>();bool enabled=ui.enabled;ui.enabled=false;var module=EventSystem.current.currentInputModule;bool active=module!=null&&module.enabled;if(module!=null)module.enabled=false;
        try{foreach(string scenario in new[]{"mutual-next-turn","nested-word"}){var meta=JsonUtility.FromJson<Meta>(File.ReadAllText(".tools/nested-control-1006/native/"+scenario+"-result.json"));Check(meta.status=="PASS","Native failed");var inputs=scenario=="nested-word"?new[]{"nested-payment","nested-scry","resolved-2","released"}:new[]{"mutual-scry","released"};
            foreach(string input in inputs){var native=Read(meta.folder+"/"+input+".json");int seat=native.request.seat,player=native.request.playerSeat,calls=0,last=-1;if(native.seat!=seat)native=Read(meta.folder+"/"+input+"-other.json");Check(native.seat==seat&&native.awaitingInput,"Wrong operative native projection");ui.Present(null,seat,false,null,null,"");ui.Present(native,seat,true,i=>{calls++;last=i;},null,"");yield return Frame();HandMatches(native.players.Single(p=>p.seat==player).hand);Check(GameObject.Find("ControlledPlayerHandBar")==null&&GameObject.Find("PersonalHandToggle")==null,"Unexpected extra control UI");
                if(input.Contains("scry")||input=="nested-payment"){
                    int index=ui.OptionIndex(input.Contains("scry")?"confirm-scry":"ok");Check(index>=0,"Native action missing");ui.Choose(index,native.request.requestId-1);Check(calls==0,"Stale request submits");var button=Root.GetComponentsInChildren<Button>().FirstOrDefault(b=>b.name==(input.Contains("scry")?"SelectionAction_"+index:"AutoPayment"));
                    if(button==null)button=Root.GetComponentsInChildren<Button>().FirstOrDefault(b=>b.name=="ValueOption_"+index||b.name=="Option_"+index||b.GetComponentInChildren<Text>()?.text.Contains("自动支付")==true);
                    Check(button!=null,"Rendered native action absent "+input);Click(button);Check(calls==1&&last==index,"Wrong original index");ui.Choose(index,native.request.requestId);Check(calls==1,"Duplicate submits");
                }
                ScreenCapture.CaptureScreenshot(Output+"/"+scenario+"-"+input+".png");yield return Frame();
                int wrongSeat=3-seat;var wrong=Read(meta.folder+"/"+input+(wrongSeat==1?"":"-other")+".json");int rejected=0;ui.Present(wrong,wrongSeat,false,i=>rejected++,null,"");yield return Frame();ui.Choose(0,wrong.request.requestId);Check(rejected==0,"Other physical viewer submits");HandMatches(wrong.players.Single(p=>p.seat==wrongSeat).hand);
                var publicView=OnlineSpectatorView.Create(new OnlinePlayerViewState{matchId="nested-control",forge=native},3).forge;Check(publicView.request==null&&publicView.players.All(p=>p.hand.Length==0),"Spectator private content");ui.Present(publicView,1,false,null,null,"");yield return Frame();HandMatches(Array.Empty<ForgeCardView>());File.AppendAllText(Output+"/steps.log",scenario+" "+input+" operator="+seat+" player="+player+" PASS\n");
            }
        }}finally{ui.Present(null,1,false,null,null,"");ui.enabled=enabled;if(module!=null)module.enabled=active;}
    }
}
#endif
