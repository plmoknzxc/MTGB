#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

public sealed class RoomBrowserValidationServer : MonoBehaviour
{
    public OnlineRoomDirectoryServer server;
    public string snapshot;
    void Update(){server?.Pump(snapshot);}
    void OnDestroy(){server?.Dispose();}
}
public static class RoomBrowserValidation
{
    const string Output=".tools/room-browser";
    static void Check(bool value,string error){if(!value)throw new Exception(error);}
    static void Call(OnlineLobbyPanel panel,string method,params object[] args)=>typeof(OnlineLobbyPanel).GetMethod(method,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(panel,args);
    static T Field<T>(OnlineLobbyPanel panel,string field)=>(T)typeof(OnlineLobbyPanel).GetField(field,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(panel);
    static bool running;
    public static void Run(){if(running)return;running=true;Directory.CreateDirectory(Output);File.WriteAllText(Output+"/result.txt","RUNNING");var watch=System.Diagnostics.Stopwatch.StartNew();var panel=OnlineLobbyPanel.Show(UnityEngine.Object.FindFirstObjectByType<MainMenuController>(),null);watch.Stop();File.WriteAllText(Output+"/entry-after.txt",watch.Elapsed.TotalMilliseconds.ToString("F1")+" ms; hidden sideboard tiles="+panel.GetComponentsInChildren<OnlineSideboardCardTile>(true).Length);panel.StartCoroutine(Guard(Validate(panel)));}
    static IEnumerator Guard(IEnumerator run){while(true){object next;try{if(!run.MoveNext()){running=false;yield break;}next=run.Current;}catch(Exception e){running=false;File.WriteAllText(Output+"/result.txt","FAIL "+e);throw;}yield return next;}}
    static IEnumerator Validate(OnlineLobbyPanel panel)
    {
        yield return new WaitForSecondsRealtime(5);
        var address=Field<InputField>(panel,"addressInput");var port=Field<InputField>(panel,"portInput");
        Field<InputField>(panel,"profileInput").SetTextWithoutNotify("browser-validation");
        Check(panel.GetComponentsInChildren<OnlineSideboardCardTile>(true).Length==0,"Browser eagerly loaded sideboard cards");
        address.text="";Call(panel,"RefreshBrowser");
        var emptyLabel=Field<Text>(panel,"browserEmpty");int emptyId=emptyLabel.GetInstanceID(),emptySize=emptyLabel.fontSize;
        Check(emptyLabel.text=="正在搜索房间…"&&Field<Text>(panel,"browserStatus").text=="","Search status is not centered");
        ScreenCapture.CaptureScreenshot(Output+"/searching.png");
        yield return new WaitForSecondsRealtime(.6f);
        Check(emptyLabel.fontSize==emptySize,"Search text resized after preference binding");
        yield return new WaitForSecondsRealtime(3);
        Check(Field<Text>(panel,"browserEmpty").GetInstanceID()==emptyId&&emptyLabel.fontSize==emptySize,"Refresh rebuilt/resized empty label");
        address.text="127.0.0.1";port.text="18779";
        Check(GameObject.Find("RoomBrowserPage")!=null&&GameObject.Find("MatchSetupCard")==null,"Browser mixed with setup");
        Call(panel,"BeginRoomCreation",true);yield return new WaitForSecondsRealtime(4.5f);
        Check(!Field<bool>(panel,"creatingRoom"),"Bad server opened room settings");
        Check(Field<Text>(panel,"browserStatus").text.Contains("无法连接"),"No bad-server error");
        port.text="18777";Call(panel,"BeginRoomCreation",false);yield return null;
        Check(Field<bool>(panel,"creatingRoom")&&GameObject.Find("RoomBrowserPage")==null,"LAN choice did not open independent settings");
        Field<InputField>(panel,"roomInput").text="browser-test";
        Field<Toggle>(panel,"customContentToggle").isOn=true;
        ScreenCapture.CaptureScreenshot(Output+"/setup.png");yield return new WaitForSecondsRealtime(.25f);
        Call(panel,"CreateSelectedRoom");
        float deadline=Time.realtimeSinceStartup+25;
        while(!OnlineMatchService.Instance.HasJoinedLobby&&Time.realtimeSinceStartup<deadline)yield return null;
        Check(OnlineMatchService.Instance.HasJoinedLobby,"LAN host never entered room");
        OnlineMatchService.Instance.SetSpectator(true);yield return new WaitForSecondsRealtime(.6f);
        Check(OnlineMatchService.Instance.IsSpectator,"Host could not switch to observer");
        ScreenCapture.CaptureScreenshot(Output+"/lobby-observer.png");yield return new WaitForSecondsRealtime(.25f);
        OnlineMatchService.Instance.SetSpectator(false);yield return new WaitForSecondsRealtime(.6f);
        Check(!OnlineMatchService.Instance.IsSpectator,"Host could not return to player seat");
        ScreenCapture.CaptureScreenshot(Output+"/lobby.png");yield return new WaitForSecondsRealtime(.25f);
        var query=OnlineRoomDiscovery.Query("127.0.0.1",18777);while(!query.IsCompleted)yield return null;
        Check(!query.IsFaulted&&query.Result.rooms.Single().roomId=="browser-test","Real host directory wrong");
        Check(query.Result.rooms[0].players==1,"Browsing occupied a seat");
        Check(query.Result.rooms[0].allowCustomContent,"Directory omitted authoritative DIY setting");
        var scan=OnlineRoomDiscovery.ScanLan();while(!scan.IsCompleted)yield return null;
        Check(!scan.IsFaulted&&scan.Result.Any(p=>p.Port==18777),"UDP LAN discovery missed host");
        Call(panel,"LeaveRoom");yield return new WaitForSecondsRealtime(1);
        Check(!OnlineMatchService.Instance.IsOnline,"Host did not disconnect");
        var fixture=new GameObject("DirectoryValidationFixture").AddComponent<RoomBrowserValidationServer>();
        fixture.server=new OnlineRoomDirectoryServer(18778);
        fixture.snapshot=JsonUtility.ToJson(new OnlineRoomDirectory{instanceId=fixture.server.InstanceId,rooms=new[]{new OnlineRoomListing{roomId="周末摩登",lanHosted=true,allowCustomContent=true,hostName="Player",players=1,bestOf=3,format="modern"},new OnlineRoomListing{roomId="练习对局",hostName="Player 2",players=2,bestOf=1,format="timeless",inMatch=true}}});
        port.text="18778";Call(panel,"RefreshBrowser");yield return new WaitForSecondsRealtime(5);
        Check(Field<System.Collections.Generic.List<OnlineRoomListing>>(panel,"browserRooms").Any(r=>r.roomId=="周末摩登"),"Server room not listed");
        Check(Field<System.Collections.Generic.List<OnlineRoomListing>>(panel,"browserRooms").Count==2,"LAN/direct query duplicated rooms");
        Check(!OnlineMatchService.Instance.IsOnline,"Refresh started game transport");
        var badges=panel.GetComponentsInChildren<Text>().Where(t=>t.name=="DiyBadge").Select(t=>t.text).ToArray();
        Check(badges.Contains("DIY 允许")&&badges.Contains("DIY 禁用"),"Missing DIY list badges");
        ScreenCapture.CaptureScreenshot(Output+"/browser.png");yield return new WaitForSecondsRealtime(.25f);
        Call(panel,"BeginRoomCreation",true);yield return new WaitForSecondsRealtime(1);
        Check(Field<bool>(panel,"creatingRoom"),"Verified server did not open settings");
        fixture.snapshot=JsonUtility.ToJson(new OnlineRoomDirectory());
        var empty=OnlineRoomDiscovery.Query("127.0.0.1",18778);while(!empty.IsCompleted)yield return null;
        Check(!empty.IsFaulted&&empty.Result.rooms.Length==0,"Empty server not recognized");
        UnityEngine.Object.Destroy(fixture.gameObject);
        Call(panel,"BrowserBack");
        File.WriteAllText(Output+"/result.txt","PASS: centered search; stable persistent status text; no eager sideboard cards; authoritative DIY directory and both list badges; independent browser/setup; bad endpoint blocked; real LAN host creation and UDP discovery; read-only TCP directory (one seat); room listing/full/in-match state; verified server opens settings; empty server reachable.");
    }
}
#endif
