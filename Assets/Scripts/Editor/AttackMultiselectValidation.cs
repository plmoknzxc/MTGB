#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

// Replays real offered native attack options through the production UI. This
// consumer evidence is separate from the native combat outcomes in native.py.
public static class AttackMultiselectValidation
{
    const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    sealed class Host:MonoBehaviour { }
    static readonly List<string> Checks=new();
    public static bool Running;
    public static string Output;
    static void Check(bool ok,string description){if(!ok)throw new Exception(description);Checks.Add("PASS "+description);}
    static object Get(object owner,string field)=>owner.GetType().GetField(field,Private).GetValue(owner);
    static object Call(object owner,string name,params object[] args)=>owner.GetType().GetMethod(name,Private).Invoke(owner,args);
    static bool Legal(ForgeRequest request,int index,List<int> members)=>(bool)typeof(OnlineMatchSession).GetMethod("LegalAttackGroup",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{request,index,members});
    static ForgeSeatView View(string directory,string name){var m=JsonUtility.FromJson<ForgeWireMessage>(File.ReadAllText(directory+"/"+name+".json"));var v=m.views[0];v.request=m.request;v.awaitingInput=true;return v;}
    static Button Card(int id)=>GameObject.Find("Card_"+id).GetComponent<Button>();
    public static void Run()
    {
        if(!Application.isPlaying||Running)throw new Exception("Play required");
        Running=true;Checks.Clear();Output=".tools/attack-multiselect-1013/ui-"+DateTime.UtcNow.ToString("yyyyMMddTHHmmssfff");Directory.CreateDirectory(Output);
        new GameObject("AttackMultiselectValidation").AddComponent<Host>().StartCoroutine(Guard(Exercise()));
    }
    static IEnumerator Guard(IEnumerator task)
    {
        while(true){bool more;object next;try{more=task.MoveNext();next=more?task.Current:null;}catch(Exception e){Checks.Add("FAIL "+e);more=false;next=null;}if(!more)break;yield return next;}
        File.WriteAllLines(Output+"/result.txt",Checks);Running=false;Debug.Log("ATTACK MULTISELECT "+Output);
    }
    static IEnumerator Exercise()
    {
        float deadline=Time.realtimeSinceStartup+30;while(!GamePreferences.Ready&&Time.realtimeSinceStartup<deadline)yield return null;
        var directory=Directory.GetDirectories(".tools/attack-multiselect-1013","native-*").OrderByDescending(d=>d).First(d=>File.ReadAllText(d+"/result.json").Contains("\"PASS\""));
        foreach(var notice in UnityEngine.Object.FindObjectsByType<ForgeBattlefieldMigrationNotice>(FindObjectsSortMode.None))notice.enabled=false;
        var ui=UnityEngine.Object.FindFirstObjectByType<ForgeBattlefieldController>()??new GameObject("AttackBoard").AddComponent<ForgeBattlefieldController>();ui.enabled=false;
        var v=View(directory,"initial");var ids=v.players[0].battlefield.Where(c=>c.name=="Goblin Piker").Select(c=>c.id).ToArray();int a=ids[0],b=ids[1];
        int choice=-1,groupChoice=-1,count=0;int[] group=null;
        Action<int> choose=i=>{choice=i;count++;};Action<int,int[]> chooseGroup=(i,g)=>{groupChoice=i;group=g;count++;};
        ui.Present(v,1,true,choose,null,"",chooseGroup);yield return new WaitForSecondsRealtime(.35f);Canvas.ForceUpdateCanvases();
        Card(a).onClick.Invoke();Card(b).onClick.Invoke();
        Check(((List<int>)Get(ui,"selectedAttackers")).SequenceEqual(new[]{a,b}),"Click two creatures selects both locally");
        Check(count==0,"Local selection causes no native or network request");
        var arrows=GameObject.Find("Canvas").GetComponentsInChildren<ForgeTargetArrow>().Where(x=>x.name.StartsWith("AttackTargetArrow")&&x.gameObject.activeInHierarchy).ToArray();
        Check(arrows.Length==2,"Both selected creatures keep a visible arrow");
        Check(arrows.All(x=>x.GetComponent<Canvas>().sortingOrder>=20),"Both arrows use the top overlay layer");
        ui.UpdateAttackArrowAt(RectTransformUtility.WorldToScreenPoint(null,GameObject.Find("OpponentPlate").transform.position));
        yield return new WaitForSecondsRealtime(.25f);Canvas.ForceUpdateCanvases();
        foreach(var arrow in arrows){var mesh=arrow.canvasRenderer.GetMesh();Check(mesh!=null&&mesh.vertexCount>0&&mesh.colors.Any(c=>c.a>.3f),"Arrow renders an opaque ribbon after its reveal animation");}
        ScreenCapture.CaptureScreenshot(Path.GetFullPath(Output+"/two-arrows.png"));yield return new WaitForEndOfFrame();
        Card(a).onClick.Invoke();Check(((List<int>)Get(ui,"selectedAttackers")).SequenceEqual(new[]{b}),"Click one selected creature removes only that member");
        ui.BeginAttackSelection(a,v.request.requestId);ui.BeginAttackSelection(a,v.request.requestId);
        Check(((List<int>)Get(ui,"selectedAttackers")).SequenceEqual(new[]{b,a}),"Dragging adds to group and never toggles existing member off");
        int player=v.players[1].targetId;int destination=(int)Call(ui,"AttackOption","player",player);
        Check(destination>=0,"Common legal player destination offered");
        var forbidden=v.request.options.Where(o=>!(o.id.StartsWith("attack:")&&o.attackerId==b&&o.defenderKind=="player")).ToArray();var saved=v.request.options;v.request.options=forbidden;
        Check((int)Call(ui,"AttackOption","player",player)<0,"A target illegal for one selected creature is not offered to the group");v.request.options=saved;
        GameObject.Find("OpponentPlate").GetComponent<Button>().onClick.Invoke();
        Check(count==1&&groupChoice==destination&&group.SequenceEqual(new[]{b,a}),"Avatar submits exactly one group response containing both physical IDs");
        Check(Legal(v.request,groupChoice,group.ToList()),"Host accepts exact shared legal attack group");
        Check(!Legal(v.request,groupChoice,new List<int>{a,a}),"Host rejects duplicate members");
        Check(!Legal(v.request,groupChoice,new List<int>{a,999999}),"Host rejects missing or foreign attackers");
        Call(ui,"ClearAttackSelection");count=0;group=null;ui.Present(v,1,true,choose,null,"",chooseGroup);Card(a).onClick.Invoke();Card(b).onClick.Invoke();
        var walker=v.players[1].battlefield.First(c=>c.planeswalker);
        var walkerPosition=RectTransformUtility.WorldToScreenPoint(null,Card(walker.id).transform.position);
        Check(ui.TryChooseAttackAt(walkerPosition,v.request.requestId)&&count==1&&group.SequenceEqual(new[]{a,b})&&v.request.options[groupChoice].defenderId==walker.id,"Dropping either group arrow on a planeswalker submits both exactly once");
        var draft=View(directory,"player-group");count=0;ui.Present(draft,1,true,choose,null,"",chooseGroup);yield return null;
        Check(((List<int>)Get(ui,"selectedAttackers")).Count==0,"New authoritative request clears local draft arrows");
        Card(a).onClick.Invoke();Check(count==1&&draft.request.options[choice].id=="attack-remove:"+a,"Click assigned attacker submits exact individual recall");
        var all=View(directory,"all-one-recalled");count=0;ui.Present(all,1,true,choose,null,"",chooseGroup);yield return null;
        Card(a).onClick.Invoke();Check(count==1&&all.request.options[choice].id=="attack-remove:"+a,"Individual click also recalls after All Attack");
        Call(ui,"ClearAttackSelection");count=0;group=null;groupChoice=-1;ui.Present(v,1,true,choose,null,"",chooseGroup);Card(a).onClick.Invoke();
        GameObject.Find("OpponentPlate").GetComponent<Button>().onClick.Invoke();Check(count==1&&v.request.options[choice].attackerId==a&&groupChoice==-1&&group==null,"Single attacker retains ordinary native response");
        // Replaying a consumed old request for a separate case requires a fresh
        // local fixture; production requests advance their authoritative ID.
        Call(ui,"ClearAttackSelection");count=0;ui.Present(v,1,true,choose,null,"",chooseGroup);Card(a).onClick.Invoke();Card(b).onClick.Invoke();
        Check(!ui.BeginAttackSelection(ids[2],v.request.requestId-1),"Stale drag cannot alter current group");
        Call(ui,"ClickAttackCard",ids[2],v.request.requestId-1);Check(((List<int>)Get(ui,"selectedAttackers")).Count==2&&count==0,"Stale click cannot alter or submit group");
        v.phase="MAIN2";v.request=new ForgeRequest{kind="InputPassPriority",seat=1,requestId=v.request.requestId+1,options=new[]{new ForgeOption{id="ok"}}};ui.Present(v,1,true,choose,null,"",chooseGroup);
        Check(((List<int>)Get(ui,"selectedAttackers")).Count==0,"Leaving declaration clears group");
        Check(!GameObject.Find("Canvas").GetComponentsInChildren<ForgeTargetArrow>().Any(x=>x.name.StartsWith("AttackTargetArrow")&&x.gameObject.activeInHierarchy),"Leaving declaration hides every draft arrow");
    }
}
#endif
