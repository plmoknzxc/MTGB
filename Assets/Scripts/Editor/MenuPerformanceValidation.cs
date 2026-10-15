#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public static class MenuPerformanceValidation
{
    const string Root=".tools/menu-performance-1015";
    const BindingFlags Flags=BindingFlags.NonPublic|BindingFlags.Instance;
    static readonly List<string> steps=new();
    public static bool Running{get;private set;}
    static T Field<T>(object o,string n)=>(T)o.GetType().GetField(n,Flags).GetValue(o);
    static void Set(object o,string n,object v)=>o.GetType().GetField(n,Flags).SetValue(o,v);
    static object Call(object o,string n,params object[] a)=>o.GetType().GetMethod(n,Flags).Invoke(o,a);
    static void Check(bool b,string n){steps.Add((b?"PASS ":"FAIL ")+n);}
    public static void Run()
    {
        if(Running)return;Running=true;steps.Clear();Directory.CreateDirectory(Root);
        UnityEngine.Object.FindFirstObjectByType<MainMenuController>().StartCoroutine(Guard(Ui(),"result.txt"));
    }
    public static void Delta()
    {
        if(Running)return;Running=true;steps.Clear();
        UnityEngine.Object.FindFirstObjectByType<MainMenuController>().StartCoroutine(Guard(DeltaUi(),"delta-result.txt"));
    }
    public static void DeltaTail()
    {
        if(Running)return;Running=true;steps.Clear();
        UnityEngine.Object.FindFirstObjectByType<MainMenuController>().StartCoroutine(Guard(DeltaUi(true),"delta-tail-result.txt"));
    }
    static IEnumerator Guard(IEnumerator run,string output)
    {
        File.WriteAllText(Root+"/"+output,"RUNNING");
        while(true){object next;try{if(!run.MoveNext())break;next=run.Current;}catch(Exception e){steps.Add("FAIL "+e);break;}yield return next;}
        (run as IDisposable)?.Dispose();File.WriteAllLines(Root+"/"+output,steps);Running=false;
    }
    static void Click(GameObject go)
    {
        var r=(RectTransform)go.transform;var p=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(null,r.TransformPoint(r.rect.center))};
        var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(p,hits);
        if(hits.Count==0||!hits[0].gameObject.transform.IsChildOf(go.transform))throw new Exception("Blocked ray "+go.name);
        ExecuteEvents.ExecuteHierarchy(hits[0].gameObject,p,ExecuteEvents.pointerClickHandler);
    }
    static IEnumerator Ui()
    {
        var menu=UnityEngine.Object.FindFirstObjectByType<MainMenuController>();
        while(!GamePreferences.Ready||!Field<bool>(menu,"dataReady"))yield return null;
        var deck=Field<DeckDefinition>(menu,"currentDeck");bool dirty=Field<bool>(menu,"hasUnsavedDeckChanges");string locale=GamePreferences.Language;
        try{
            Call(menu,"ShowDeckBuilder");Set(menu,"currentDeck",new DeckDefinition{name="Performance fixture"});
            var timer=Stopwatch.StartNew();Call(menu,"RenderCompanionDeck");double open=timer.Elapsed.TotalMilliseconds;
            var modal=Field<GameObject>(menu,"exchangeDialog");var panel=modal.transform.Find("Panel");
            var choose=panel.Find("ChooseCompanion_0");Check(open<100,"companion cold open <100ms: "+open);
            Check(panel.GetComponentsInChildren<Button>().Count(b=>b.name.StartsWith("ChooseCompanion_"))==10,"ten native companion candidates remain available");
            timer.Restart();Call(menu,"RenderCompanionDeck");double warm=timer.Elapsed.TotalMilliseconds;
            Check(warm<50,"companion repeat <50ms: "+warm);Check(modal==Field<GameObject>(menu,"exchangeDialog")&&choose==panel.Find("ChooseCompanion_0"),"companion modal and candidate controls retained");
            for(int i=0;i<12;i++)yield return null;yield return new WaitForEndOfFrame();Canvas.ForceUpdateCanvases();
            ScreenCapture.CaptureScreenshot(Path.GetFullPath(Root+"/companion-after.png"));
            Click(panel.Find("CloseCompanion").gameObject);yield return null;
            Call(menu,"ShowAuxiliaryDeck",true);modal=Field<GameObject>(menu,"exchangeDialog");panel=modal.transform.Find("Panel");
            var attraction=panel.Find("Attractions");var contraption=panel.Find("Contraptions");
            int font=attraction.GetComponentInChildren<Text>().fontSize;
            for(int i=0;i<6;i++){
                yield return null;yield return new WaitForEndOfFrame();Canvas.ForceUpdateCanvases();
                timer.Restart();Click((i%2==0?contraption:attraction).gameObject);double ms=timer.Elapsed.TotalMilliseconds;
                Check(ms<50,"auxiliary switch "+i+" <50ms: "+ms);
                Check(modal==Field<GameObject>(menu,"exchangeDialog")&&modal.activeInHierarchy&&attraction==panel.Find("Attractions")&&contraption==panel.Find("Contraptions"),"auxiliary switch "+i+" preserves visible surface and tabs");
                Check(attraction.GetComponentInChildren<Text>().fontSize==font,"auxiliary switch "+i+" retains font size");
            }
            yield return null;yield return new WaitForEndOfFrame();Click(panel.Find("ToggleRegistered").gameObject);
            for(int i=0;i<12;i++)yield return null;
            yield return null;yield return new WaitForEndOfFrame();Canvas.ForceUpdateCanvases();
            Check(panel.GetComponentsInChildren<Image>().Count(x=>x.name.StartsWith("AuxiliaryCard_"))>0,"auxiliary catalog cards visible");
            ScreenCapture.CaptureScreenshot(Path.GetFullPath(Root+"/auxiliary-after.png"));
            Click(panel.Find("Close").gameObject);yield return null;
            // Use the same 3000 real recorded views as the baseline, never execute old rules.
            var data=Task.Run(()=>PreviewData());while(!data.IsCompleted)yield return null;
            if(data.IsFaulted)throw data.Exception;steps.AddRange(data.Result);
            ReplayBrowser.Show();var browser=UnityEngine.Object.FindFirstObjectByType<ReplayBrowser>();
            var list=ReplayRepository.ListFiles();browser.Select(Path.GetFullPath(Root+"/preview-stress.mtgbr"));browser.Select(list[0]);
            float deadline=Time.realtimeSinceStartup+10;
            while(Field<ReplayPreview>(browser,"selected")==null&&Time.realtimeSinceStartup<deadline)yield return null;
            Check(Field<ReplayPreview>(browser,"selected")!=null,"browser displays preview before full playback loading");
            yield return new WaitForSecondsRealtime(.8f);
            Check(Field<string>(browser,"selectedPath")==list[0]&&Field<Text>(browser,"details").text.Contains(Field<ReplayPreview>(browser,"selected").players[0]),"stale previous preview never replaces current selection");
            Check(Field<Button>(browser,"play").interactable,"play available after metadata read");
            ScreenCapture.CaptureScreenshot(Path.GetFullPath(Root+"/replay-after.png"));yield return new WaitForEndOfFrame();
            UnityEngine.Object.Destroy(browser.gameObject);
        }finally{Call(menu,"CloseDeckExchange");Set(menu,"currentDeck",deck);Set(menu,"hasUnsavedDeckChanges",dirty);Call(menu,"RefreshDeckGrids");Call(menu,"ShowStartScreenNow");}
    }
    static string[] PreviewData()
    {
        var checks=new List<string>();void Assert(bool ok,string name){checks.Add((ok?"PASS ":"FAIL ")+name);}
        string legacy=Path.GetFullPath(Root+"/preview-stress.mtgbr");
        var timer=Stopwatch.StartNew();var header=ReplayRepository.ReadPreview(legacy);double cold=timer.Elapsed.TotalMilliseconds;
        Assert(cold<100&&header.duration<0,"legacy header <100ms without timeline: "+cold);
        timer.Restart();var complete=ReplayRepository.ReadPreview(legacy,true);
        checks.Add("MEASURE legacy duration background scan ms="+timer.Elapsed.TotalMilliseconds);
        Assert(complete.duration==2999,"legacy duration recovered without card DTOs");
        timer.Restart();var cached=ReplayRepository.ReadPreview(legacy);Assert(timer.Elapsed.TotalMilliseconds<10&&cached.duration==2999,"legacy completed preview cache");
        var full=ReplayRepository.Load(legacy);string modern=ReplayRepository.Save(full,"preview-header",Path.GetFullPath(Root));
        timer.Restart();var fresh=ReplayRepository.ReadPreview(modern);double ms=timer.Elapsed.TotalMilliseconds;
        Assert(ms<100&&fresh.duration==2999&&fresh.players.SequenceEqual(full.players),"new header includes full duration <100ms: "+ms);
        Assert(ReplayRepository.Load(modern).frames.Count==3000,"new optional preview header preserves full playback decode");
        string changed=Path.GetFullPath(Root+"/cache-change.mtgbr");File.Copy(modern,changed,true);ReplayRepository.ReadPreview(changed);
        File.WriteAllBytes(changed,new byte[]{0,1,2});bool rejected=false;try{ReplayRepository.ReadPreview(changed);}catch(InvalidDataException){rejected=true;}
        Assert(rejected,"file replacement invalidates cached metadata");File.Delete(changed);
        return checks.ToArray();
    }
    static IEnumerator DeltaUi(bool onlyTail=false)
    {
        var menu=UnityEngine.Object.FindFirstObjectByType<MainMenuController>();
        while(!GamePreferences.Ready||!Field<bool>(menu,"dataReady"))yield return null;
        var original=Field<DeckDefinition>(menu,"currentDeck");bool dirty=Field<bool>(menu,"hasUnsavedDeckChanges");string language=GamePreferences.Language;
        try{
            Call(menu,"ShowDeckBuilder");var deck=new DeckDefinition{name="Retained picker fixture"};Set(menu,"currentDeck",deck);
            Transform panel=null;
            if(!onlyTail){
            var timer=Stopwatch.StartNew();Call(menu,"RenderCompanionDeck");Check(timer.Elapsed.TotalMilliseconds<100,"final companion cold open <100ms: "+timer.Elapsed.TotalMilliseconds);
            yield return null;yield return new WaitForEndOfFrame();Canvas.ForceUpdateCanvases();
            var modal=Field<GameObject>(menu,"exchangeDialog");panel=modal.transform.Find("Panel");
            Click(panel.Find("ChooseCompanion_0").gameObject);
            Check(DeckCompanion.Has(deck)&&deck.sideboard.Sum(e=>e.count)==1,"retained companion selection adds exactly one sideboard card");
            Check(!panel.Find("ChooseCompanion_0").GetComponent<Button>().interactable,"selected companion button becomes disabled");
            yield return null;yield return new WaitForEndOfFrame();Canvas.ForceUpdateCanvases();Click(panel.Find("ClearCompanion").gameObject);
            Check(!DeckCompanion.Has(deck)&&deck.sideboard.Sum(e=>e.count)==1,"retained clear removes designation only");
            yield return null;yield return new WaitForEndOfFrame();Canvas.ForceUpdateCanvases();Click(panel.Find("ChooseCompanion_1").gameObject);
            var chosen=deck.companion.PrintingKey;Check(DeckCompanion.Has(deck)&&deck.sideboard.Sum(e=>e.count)==2,"different retained button selects its own printing");
            Click(panel.Find("ClearCompanion").gameObject);
            foreach(string locale in new[]{"zh-CN","en-US","ja-JP"}){
                GamePreferences.SetLanguage(locale);yield return null;Call(menu,"RenderCompanionDeck");yield return null;yield return new WaitForEndOfFrame();Canvas.ForceUpdateCanvases();
                var labels=panel.GetComponentsInChildren<Text>().Where(t=>t.name.StartsWith("CompanionName_")).ToArray();
                Check(labels.Length==10&&labels.All(t=>t.preferredHeight<=t.rectTransform.rect.height+1),locale+" retained companion names fit");
                Check(modal==Field<GameObject>(menu,"exchangeDialog"),locale+" retains companion modal through locale update");
            }
            Call(menu,"CloseDeckExchange");
            }else{GamePreferences.SetLanguage("ja-JP");yield return null;}
            Call(menu,"ShowAuxiliaryDeck",true);panel=Field<GameObject>(menu,"exchangeDialog").transform.Find("Panel");
            yield return null;yield return new WaitForEndOfFrame();Canvas.ForceUpdateCanvases();
            Click(panel.Find("ToggleRegistered").gameObject);yield return null;yield return new WaitForEndOfFrame();Canvas.ForceUpdateCanvases();
            Click(panel.Find("ChangeAuxiliary_0").gameObject);Check(deck.attractions.Count==1&&deck.contraptions.Count==0,"retained add action affects attractions only");
            Click(panel.Find("Contraptions").gameObject);yield return null;yield return new WaitForEndOfFrame();Canvas.ForceUpdateCanvases();
            Click(panel.Find("ChangeAuxiliary_0").gameObject);Check(deck.attractions.Count==1&&deck.contraptions.Count==1,"switched retained add action affects contraptions only");
            Click(panel.Find("ToggleRegistered").gameObject);yield return null;yield return new WaitForEndOfFrame();Canvas.ForceUpdateCanvases();
            Click(panel.Find("ChangeAuxiliary_0").gameObject);Check(deck.attractions.Count==1&&deck.contraptions.Count==0,"retained registered remove action uses current section");
            Check(!panel.Find("ChangeAuxiliary_0").gameObject.activeSelf,"unused card controls hidden after final removal");
            for(int i=0;i<12;i++)yield return null;
            Check(panel.GetComponentsInChildren<Image>().All(i=>!i.name.StartsWith("AuxiliaryCard_")),"stale art queue cannot restore removed cards");
            Call(menu,"CloseDeckExchange");GamePreferences.SetLanguage(language);yield return null;
            ReplayBrowser.Show();var browser=UnityEngine.Object.FindFirstObjectByType<ReplayBrowser>();var files=ReplayRepository.ListFiles();browser.Select(files[0]);
            while(Field<ReplayPreview>(browser,"selected")==null)yield return null;
            Call(browser,"OpenSelected",false);browser.Select(files[1]);yield return new WaitForSecondsRealtime(.5f);
            Check(ReplayPlayback.Current==null&&Field<string>(browser,"selectedPath")==files[1],"changing selection cancels pending playback launch");
            bool cancelled=false;try{ReplayRepository.ReadPreview(files[0],true,new System.Threading.CancellationToken(true));}catch(OperationCanceledException){cancelled=true;}
            Check(cancelled,"superseded preview reads honor cancellation");
            var root=browser.transform;yield return new WaitForEndOfFrame();Canvas.ForceUpdateCanvases();Click(Field<Button>(browser,"delete").gameObject);
            Check(Field<RectTransform>(browser,"deleteDialog")!=null,"restyled replay delete dialog opens");
            Call(browser,"CloseDeleteDialog");Check(Field<RectTransform>(browser,"deleteDialog")==null,"replay delete dialog cancels without deleting file");
            UnityEngine.Object.Destroy(browser.gameObject);
        }finally{GamePreferences.SetLanguage(language);Call(menu,"CloseDeckExchange");Set(menu,"currentDeck",original);Set(menu,"hasUnsavedDeckChanges",dirty);Call(menu,"RefreshDeckGrids");Call(menu,"ShowStartScreenNow");}
    }
}
#endif
