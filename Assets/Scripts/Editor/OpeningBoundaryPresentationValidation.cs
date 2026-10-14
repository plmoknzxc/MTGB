#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class OpeningBoundaryValidationHost : MonoBehaviour { }
public static class OpeningBoundaryPresentationValidation
{
    public static bool Running { get; private set; }
    public static string Output { get; private set; }
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
    static T Field<T>(OnlineLobbyPanel p,string n)=>(T)typeof(OnlineLobbyPanel).GetField(n,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(p);
    static void Call(OnlineLobbyPanel p,string n,params object[] a)=>typeof(OnlineLobbyPanel).GetMethod(n,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(p,a);
    static void Log(string text)=>File.AppendAllText(Output+"/steps.log",text+" PASS\n");
    public static void Run()
    {
        Check(Application.isPlaying&&!Running,"Play required/already running");
        Check(OnlineMatchService.Instance==null||!OnlineMatchService.Instance.IsOnline,"User online session active");
        Running=true;Output=".tools/opening-boundaries-1007/ui/"+DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffffffZ");Directory.CreateDirectory(Output);
        File.WriteAllText(".tools/opening-boundaries-1007/ui/latest.txt",Output);File.WriteAllText(Output+"/result.txt","RUNNING");
        var host=new GameObject("OpeningBoundaryValidationHost").AddComponent<OpeningBoundaryValidationHost>();host.StartCoroutine(Guard(Validate(),host));
    }
    static IEnumerator Guard(IEnumerator iterator,OpeningBoundaryValidationHost host)
    {
        var stack=new Stack<IEnumerator>();stack.Push(iterator);
        try {
            while(stack.Count>0){object next;try{var top=stack.Peek();if(!top.MoveNext()){(stack.Pop() as IDisposable)?.Dispose();continue;}next=top.Current;}catch(Exception e){File.WriteAllText(Output+"/result.txt","FAIL "+e);throw;}
                if(next is IEnumerator nested)stack.Push(nested);else yield return next;}
            File.WriteAllText(Output+"/result.txt","PASS: actual LAN lobby raycast: cancel setup/reenter, BO3/life27 authoritative config, host leave/shutdown, BO1/life37 new room no stale members/readiness, close and reopen. Private London callbacks and three languages reuse existing exact-contract proof; this suite adds no card effects or full-network claim.");
        }finally{while(stack.Count>0)(stack.Pop() as IDisposable)?.Dispose();OnlineMatchService.Instance?.Disconnect();Running=false;UnityEngine.Object.Destroy(host.gameObject);}
    }
    static IEnumerator Wait(Func<bool> condition,string message,float seconds=12)
    {
        float end=Time.realtimeSinceStartup+seconds;while(!condition()&&Time.realtimeSinceStartup<end)yield return null;Check(condition(),message);
    }
    static IEnumerator Frame(){yield return null;yield return new WaitForEndOfFrame();Canvas.ForceUpdateCanvases();}
    static void Click(Button button)
    {
        Check(button!=null&&button.isActiveAndEnabled&&button.interactable,"Button not actionable");
        var pointer=new PointerEventData(EventSystem.current){button=PointerEventData.InputButton.Left};var rect=((RectTransform)button.transform).rect;
        foreach(float x in new[]{0f,-.35f,.35f}){
            pointer.position=RectTransformUtility.WorldToScreenPoint(null,button.transform.TransformPoint(rect.center+new Vector2(rect.width*x,0)));
            var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(pointer,hits);
            if(hits.Count==0||hits[0].gameObject.GetComponentInParent<Button>()!=button)continue;
            ExecuteEvents.ExecuteHierarchy(hits[0].gameObject,pointer,ExecuteEvents.pointerClickHandler);return;
        }
        throw new Exception("Raycast blocked: "+button.name);
    }
    static Button Button(OnlineLobbyPanel p,string name)=>p.GetComponentsInChildren<Button>().Single(b=>b.name==name);
    static IEnumerator OpenSetup(OnlineLobbyPanel p)
    {
        yield return Wait(()=>!Field<bool>(p,"browsing"),"Browser did not finish");yield return Frame();
        Click(Field<Button>(p,"createRoomButton"));yield return Frame();Click(Button(p,"Lan"));yield return Frame();
        Check(Field<bool>(p,"creatingRoom"),"LAN setup missing");
    }
    static IEnumerator Validate()
    {
        string locale=GamePreferences.Language;var panel=OnlineLobbyPanel.Show(UnityEngine.Object.FindFirstObjectByType<MainMenuController>(),null);
        string profile="opening-"+DateTime.UtcNow.Ticks.ToString();profile=OnlineClientProfileStore.NormalizeId(profile);
        var module=EventSystem.current.currentInputModule;bool input=module!=null&&module.enabled;if(module!=null)module.enabled=false;
        try {
            yield return Wait(()=>!Field<bool>(panel,"browsing"),"Initial browser timeout");
            Field<InputField>(panel,"profileInput").SetTextWithoutNotify(profile);Field<InputField>(panel,"portInput").SetTextWithoutNotify("18891");
            Field<InputField>(panel,"nameInput").SetTextWithoutNotify("Opening validation");Field<InputField>(panel,"addressInput").SetTextWithoutNotify("127.0.0.1");
            yield return OpenSetup(panel);Field<InputField>(panel,"lifeInput").SetTextWithoutNotify("27");Field<Dropdown>(panel,"formatDropdown").SetValueWithoutNotify(1);
            Click(Button(panel,"BackToBrowser"));yield return Frame();Check(!Field<bool>(panel,"creatingRoom")&&(OnlineMatchService.Instance==null||!OnlineMatchService.Instance.IsOnline),"Cancelled setup left transport/room");Log("cancel-setup-no-transport");
            for(int round=0;round<2;round++) {
                yield return OpenSetup(panel);int life=round==0?27:37,bestOf=round==0?3:1;string room="opening-r01-"+round;
                Field<InputField>(panel,"roomInput").SetTextWithoutNotify(room);Field<InputField>(panel,"lifeInput").SetTextWithoutNotify(life.ToString());
                Field<Dropdown>(panel,"formatDropdown").SetValueWithoutNotify(bestOf==3?1:0);Field<Dropdown>(panel,"cardFormatDropdown").SetValueWithoutNotify(Array.IndexOf(DeckFormatLegality.OnlineKeys,"none"));
                Field<Toggle>(panel,"customContentToggle").SetIsOnWithoutNotify(false);yield return Frame();Click(Field<Button>(panel,"hostButton"));
                yield return Wait(()=>OnlineMatchService.Instance!=null&&OnlineMatchService.Instance.HasJoinedLobby,"LAN lobby join failed",25);yield return new WaitForSecondsRealtime(.4f);
                var service=OnlineMatchService.Instance;var lobby=service.LobbyState;Check(lobby.config.startingLife==life&&lobby.config.bestOf==bestOf&&lobby.config.firstPlayerSeat==0&&lobby.config.formatId=="none"&&!lobby.config.allowCustomContent,"Authoritative config wrong");
                Check(service.LocalSeat==1&&!service.IsInMatch&&!Field<bool>(panel,"ready"),"New lobby inherited game/readiness");
                File.WriteAllText(Output+"/room-"+round+".json",JsonUtility.ToJson(lobby,true));ScreenCapture.CaptureScreenshot(Output+"/room-"+round+".png");yield return new WaitForEndOfFrame();Log("authoritative-room-"+round+"-life"+life+"-BO"+bestOf);
                Click(Field<Button>(panel,"leaveRoomButton"));yield return Wait(()=>!service.IsOnline&&!service.IsDisconnecting,"LAN shutdown failed");yield return Frame();
                Check(!service.HasJoinedLobby&&service.LocalSeat==0&&!Field<bool>(panel,"ready")&&!Field<bool>(panel,"creatingRoom"),"Leave retained lobby state");Log("leave-room-"+round+"-cleared");
            }
            Click(Button(panel,"BackButton"));yield return Wait(()=>!panel.gameObject.activeSelf,"Lobby close failed");
            panel=OnlineLobbyPanel.Show(UnityEngine.Object.FindFirstObjectByType<MainMenuController>(),null);yield return Wait(()=>!Field<bool>(panel,"browsing"),"Reopened browser timeout");yield return new WaitForSecondsRealtime(.5f);
            Check(!Field<bool>(panel,"creatingRoom")&&!Field<bool>(panel,"ready")&&!OnlineMatchService.Instance.HasJoinedLobby,"Reopened lobby retained pending state");Log("close-and-reopen-clean-browser");
            Click(Button(panel,"BackButton"));yield return Wait(()=>!panel.gameObject.activeSelf,"Final close failed");
        }finally{OnlineMatchService.Instance?.Disconnect();if(module!=null)module.enabled=input;GamePreferences.SetLanguage(locale);}
    }
}
#endif
