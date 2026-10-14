#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class LiveDeckReloadValidation
{
    public static bool Running;
    public static string Output;
    const BindingFlags Private=BindingFlags.NonPublic|BindingFlags.Instance;
    sealed class Host:MonoBehaviour{}
    static List<string> checks=new();
    static object Call(object owner,string name,params object[] args)=>owner.GetType().GetMethod(name,Private).Invoke(owner,args);
    static object Get(object owner,string name)=>owner.GetType().GetField(name,Private).GetValue(owner);
    static void Set(object owner,string name,object value)=>owner.GetType().GetField(name,Private).SetValue(owner,value);
    static void Check(bool value,string name){checks.Add((value?"PASS ":"FAIL ")+name);}
    public static void Run(){if(Running)throw new Exception("Already running");checks=new();Running=true;Output=".tools/live-gameplay-fixes-1013/deck-ui-"+DateTime.UtcNow.ToString("yyyyMMddTHHmmssfff");Directory.CreateDirectory(Output);var host=new GameObject("DeckRefreshValidation").AddComponent<Host>();UnityEngine.Object.DontDestroyOnLoad(host);host.StartCoroutine(Exercise());}
    static IEnumerator Exercise()
    {
        SceneManager.LoadScene("Main");
        yield return null;
        var menu=UnityEngine.Object.FindFirstObjectByType<MainMenuController>();float deadline=Time.realtimeSinceStartup+40;
        while(!(bool)Get(menu,"dataReady")&&Time.realtimeSinceStartup<deadline)yield return null;
        string path=Path.Combine(DeckRepository.DeckRoot,"livefix-real-ui-owned.mdk"),renamed=Path.Combine(DeckRepository.DeckRoot,"livefix-real-ui-renamed.mdk");
        if(File.Exists(path)||File.Exists(renamed))throw new Exception("Owned test path exists");
        var service=OnlineMatchService.EnsureInstance();object priorDeck=Get(service,"clientRegisteredDeck"),priorLobby=Get(service,"<LobbyState>k__BackingField");
        try{
            File.WriteAllText(path,"#name livefix-real-ui-owned\n#format standard\n#main\n60 10E/376\n!side\n");Call(menu,"RefreshDeckFiles",true);
            var decks=(List<DeckDefinition>)Get(menu,"decks");int index=decks.FindIndex(d=>d.name=="livefix-real-ui-owned");Check(index>=0,"actual main menu discovers new external file");
            Call(menu,"LoadDeckAt",index);File.Move(path,renamed);Call(menu,"ShowDeckBuilder");
            Check(((DeckDefinition)Get(menu,"currentDeck")).name=="livefix-real-ui-renamed" || ((List<DeckDefinition>)Get(menu,"decks")).Any(d=>d.name=="livefix-real-ui-renamed"),"navigation immediately refreshes external rename");
            decks=(List<DeckDefinition>)Get(menu,"decks");Call(menu,"LoadDeckAt",decks.FindIndex(d=>d.name=="livefix-real-ui-renamed"));
            File.WriteAllText(renamed,"#name livefix-real-ui-owned\n#format standard\n#main\n59 10E/376\n!side\n1 10E/376\n");
            yield return new WaitForSecondsRealtime(1.3f);
            Check(DeckRepository.GetTotalCount((DeckDefinition)Get(menu,"currentDeck"),false)==59,"running menu poll picks up edited external card counts");
            checks.Add("INFO main="+JsonUtility.ToJson(Get(menu,"currentDeck"))+" dirty="+Get(menu,"hasUnsavedDeckChanges")+" index="+Get(menu,"loadedDeckIndex")+" nextScan="+Get(menu,"nextDeckScan")+" now="+Time.unscaledTime+" repo="+JsonUtility.ToJson(DeckRepository.Load(renamed)));
            Set(menu,"hasUnsavedDeckChanges",true);((DeckDefinition)Get(menu,"currentDeck")).name="未保存的编辑";File.Delete(renamed);yield return new WaitForSecondsRealtime(1.3f);
            Check(((DeckDefinition)Get(menu,"currentDeck")).name=="未保存的编辑","external delete preserves unsaved editor contents");Check(!((List<DeckDefinition>)Get(menu,"decks")).Any(d=>d.name.StartsWith("livefix-real-ui")),"deleted file disappears from live dropdown data");
            Set(menu,"hasUnsavedDeckChanges",false);Call(menu,"RefreshDeckFiles",true);
            // Simulate the authenticated ownDeck result retained after EndReview
            // is cleared. Both entries read the same existing persistent profile.
            Set(service,"<LobbyState>k__BackingField",new OnlineLobbyState{series=new OnlineSeriesState{gameNumber=2,betweenGames=true}});
            foreach(var own in new[]{new DeckDefinition{name="甲实际出战牌组",cards=new(){new(){setCode="10E",collectorNumber="376",count=60}}},new DeckDefinition{name="乙实际出战牌组",cards=new(){new(){setCode="10E",collectorNumber="367",count=60}}}}){
                Set(service,"clientRegisteredDeck",own);var lobby=OnlineLobbyPanel.Show(menu,new DeckDefinition{name="共享档案中的其他牌组"});
                var displayed=(DeckDefinition)Get(lobby,"selectedDeck");Check(displayed.name==own.name&&displayed.cards[0].collectorNumber==own.cards[0].collectorNumber,"actual sideboard entry uses isolated session pool for "+own.name);
                ScreenCapture.CaptureScreenshot(Path.GetFullPath(Output+"/"+(own.name.StartsWith("甲")?"seat1":"seat2")+".png"));yield return new WaitForEndOfFrame();UnityEngine.Object.Destroy(lobby.gameObject);yield return null;
            }
        }finally{
            if(File.Exists(path))File.Delete(path);if(File.Exists(renamed))File.Delete(renamed);
            Set(service,"clientRegisteredDeck",priorDeck);Set(service,"<LobbyState>k__BackingField",priorLobby);
            File.WriteAllLines(Output+"/result.txt",checks);Running=false;
            Debug.Log("LIVE DECK RELOAD "+Output);
        }
        SceneManager.LoadScene(ForgeBattlefieldController.SceneName);
    }
}
#endif
