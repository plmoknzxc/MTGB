#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public static class DeckExchangeValidation
{
    const string Folder="Tools/Validation/Reports/DeckExchange";
    static void Check(bool value,string message){if(!value)throw new Exception(message);}
    static string Contents(DeckDefinition d)=>string.Join("|",new[]{d.commanders,d.cards,d.sideboard}.Select(s=>string.Join(";",s.OrderBy(e=>e.PrintingKey).Select(e=>e.PrintingKey+":"+e.count))));
    public static void RunCodec()
    {
        Directory.CreateDirectory(Folder);
        try
        {
            var mountain=new CardDefinition{cardName="Mountain",setCode="10E",collectorNumber=376,cardID=1};
            var shock=new CardDefinition{cardName="Shock",setCode="10E",collectorNumber=232,cardID=2};
            var split=new CardDefinition{cardName="Fire // Ice",setCode="MH2",collectorNumberText="290a",cardID=3};
            var codec=new DeckTextExchange(new[]{mountain,shock,split});codec.AddAlias("Mountain","山脉");codec.AddAlias("Shock","ショック");
            var original=codec.Import("#name 转换测试\r\n#main\r\n20 10E/376\r\n5 10E/232\r\n!side\r\n2 MH2/290a");Check(original.Success,"Native import failed");
            foreach(DeckTextExchange.Format f in Enum.GetValues(typeof(DeckTextExchange.Format)))
            {var round=codec.Import(codec.Export(original.Deck,f));Check(round.Success && Contents(round.Deck)==Contents(original.Deck),"Round trip lost entries: "+f);}
            var localized=codec.Import("デッキ\n20 山脉 (10E) 376\n5 ショック\n\nサイドボード\n2 Fire // Ice (MH2) 290a");Check(localized.Success && Contents(localized.Deck)==Contents(original.Deck),"Localized/split import failed");
            var commander=codec.Import("Commander\n1 Shock\n\nDeck\n2 Mountain\n\nSideboard\n1 Fire // Ice");Check(commander.Success && commander.Deck.commanders.Count==1,"Commander zone lost");
            Check(Contents(codec.Import(codec.Export(commander.Deck,DeckTextExchange.Format.MTGB)).Deck)==Contents(commander.Deck),"Native commander round trip failed");
            foreach(string bad in new[]{"2 Mountain\n1 Not A Real Card","0 Shock","999999999999 Shock","Deck\n1 Shock (10E) 376","#main\n1 NOSET/2","Companion\n1 Shock","#main\n1 10E/376\n#extra\n1 10E/232"})
            {var result=codec.Import(bad);Check(!result.Success && result.Deck==null && result.Errors.Count>0,"Partial/bad import accepted: "+bad);}
            var sb=codec.Import("4x Shock\nSB: 2 Mountain");Check(sb.Success && sb.Deck.cards[0].count==4 && sb.Deck.sideboard[0].count==2,"SB prefix lost");
            var duplicate=codec.Import("#main\n2 10E/232\n3 10E/232");Check(duplicate.Success && duplicate.Deck.cards.Count==1 && duplicate.Deck.cards[0].count==5,"Copies silently capped");
            mountain.forgeSet="TE";
            foreach(string text in new[]{"2 Mountain <borderless anime confetti foil> [TE] (F)\nSB: 1 Shock <prerelease> [10E] (F)","Deck\n2 Mountain <halo foil> (10E) 376 (F)\nSideboard\n1 Shock (10E) 232 *F*","2 Mountain <extended art> (F)\nSB: 1 Shock <showcase>"})
            {
                var decorated=codec.Import(text);
                Check(decorated.Success && decorated.Deck.cards.Single().PrintingKey==mountain.PrintingKey && decorated.Deck.cards.Single().count==2 && decorated.Deck.sideboard.Single().PrintingKey==shock.PrintingKey,"Treatment/finish metadata import failed: "+text);
            }
            Check(!codec.Import("1 Shock <foil> [NOSET] (F)").Success,"Unknown annotated set accepted");
            Check(!codec.Import("1 Shock [10E] 376 (F)").Success,"Mismatched annotated collector accepted");
            var legacySets=new DeckTextExchange(new[]{
                new CardDefinition{cardName="Claws of Gix",setCode="USG",collectorNumber=290},
                new CardDefinition{cardName="Cursed Totem",setCode="MIR",collectorNumber=299},
                new CardDefinition{cardName="Galvanic Blast",setCode="PLST",forgeSet="PLST",collectorNumberText="DDU-45"}});
            Check(legacySets.Import("1 Claws of Gix [UZ] (F)\n2 Cursed Totem [MI] (F)\n2 Galvanic Blast [MB1]").Success,"Legacy/Mystery Booster set representation failed");
            File.WriteAllText(Folder+"/codec-result.txt","PASS: MO/MTGA/MTGB round trips; main/side/commander; CJK aliases; split card and alphanumeric collector; SB prefix; repeated rows; quantities preserved; malformed/unknown/mismatched/unsupported input atomic rejection.");
        }
        catch(Exception e){File.WriteAllText(Folder+"/codec-result.txt","FAIL "+e);throw;}
    }
    static T Field<T>(object obj,string name)=>(T)obj.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(obj);
    static bool running;
    public static void RunUi()
    {
        if(running)return;running=true;Directory.CreateDirectory(Folder);
        var menu=UnityEngine.Object.FindFirstObjectByType<MainMenuController>();Check(menu!=null,"Main scene is required");
        menu.StartCoroutine(Guard(menu));
    }
    static IEnumerator Guard(MainMenuController menu)
    {
        string prefix="Exchange-Test-"+Guid.NewGuid().ToString("N");var before=Directory.GetFiles(DeckRepository.DeckRoot,"*.mdk").ToDictionary(p=>p,File.ReadAllText);
        File.WriteAllText(Folder+"/ui-result.txt","RUNNING");var steps=Validate(menu,prefix);Exception error=null;
        while(true)
        {
            object next=null;bool more=false;try{more=steps.MoveNext();if(more)next=steps.Current;}catch(Exception e){error=e;}
            if(error!=null || !more)break;yield return next;
        }
        try
        {
            foreach(var saved in before)Check(File.ReadAllText(saved.Key)==saved.Value,"An original deck was modified");
            foreach(var path in Directory.GetFiles(DeckRepository.DeckRoot,prefix+"*.mdk"))
            {Check(Path.GetFullPath(path).StartsWith(Path.GetFullPath(DeckRepository.DeckRoot)+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase),"Cleanup escaped deck root");File.Delete(path);}
        }
        catch(Exception e){if(error==null)error=e;}
        File.WriteAllText(Folder+"/ui-result.txt",error==null?"PASS: visible toolbar and hit areas; Save As clone; automatic MTGB paste creates saved deck; errors do not replace deck; export format selection and clipboard; original files unchanged; test decks cleaned.":"FAIL "+error);
        if(error!=null)Debug.LogException(error);running=false;
    }
    static Button Button(string name)=>GameObject.Find(name).GetComponent<Button>();
    static IEnumerator Validate(MainMenuController menu,string prefix)
    {
        float deadline=Time.realtimeSinceStartup+60;
        while(!Field<bool>(menu,"dataReady")){Check(Time.realtimeSinceStartup<deadline,"Catalog load timeout");yield return null;}
        menu.SendMessage("ShowDeckBuilder");yield return new WaitForSecondsRealtime(.7f);
        var row=GameObject.Find("DeckToolbar").GetComponent<RectTransform>();var controls=row.GetComponentsInChildren<Selectable>().Where(s=>s.transform.parent==row).ToArray();
        foreach(var control in controls)
        {
            var rect=(RectTransform)control.transform;var corners=new Vector3[4];rect.GetWorldCorners(corners);
            var bounds=row.rect;bounds.xMin-=1;bounds.xMax+=1;bounds.yMin-=1;bounds.yMax+=1;
            Check(corners.All(c=>bounds.Contains(row.InverseTransformPoint(c))),"Toolbar control clipped: "+control.name);
        }
        var point=RectTransformUtility.WorldToScreenPoint(null,Button("ImportDeckButton").transform.position);var hits=new List<RaycastResult>();
        EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current){position=point},hits);
        Check(hits.Count>0 && hits[0].gameObject.GetComponentInParent<Button>()?.name=="ImportDeckButton","Import button blocked");
        ScreenCapture.CaptureScreenshot(Folder+"/toolbar.png");yield return new WaitForEndOfFrame();
        var original=Contents(Field<DeckDefinition>(menu,"currentDeck"));Button("SaveAsDeckButton").onClick.Invoke();yield return null;
        GameObject.Find("NewDeckName").GetComponent<InputField>().text=prefix+"-copy";Button("SaveCopy").onClick.Invoke();yield return null;
        Check(Contents(Field<DeckDefinition>(menu,"currentDeck"))==original,"Save As changed contents");
        string copyFile=Path.Combine(DeckRepository.DeckRoot,prefix+"-copy.mdk"), copyText=File.ReadAllText(Path.Combine(DeckRepository.DeckRoot,prefix+"-copy.mdk"));
        Button("SaveAsDeckButton").onClick.Invoke();yield return null;
        GameObject.Find("NewDeckName").GetComponent<InputField>().text=prefix+"-copy";Button("SaveCopy").onClick.Invoke();yield return null;
        Check(Field<DeckDefinition>(menu,"currentDeck").name==prefix+"-copy (1)" && File.ReadAllText(copyFile)==copyText,"Same-name Save As overwrote the original");
        Button("ImportDeckButton").onClick.Invoke();yield return null;
        GameObject.Find("DeckText").GetComponent<InputField>().text="#name "+prefix+"-import\n#main\n2 10E/376\n!side\n1 10E/232";
        deadline=Time.realtimeSinceStartup+30;
        while(Field<DeckDefinition>(menu,"currentDeck").name!=prefix+"-import"){Check(Time.realtimeSinceStartup<deadline,"Auto import failed");yield return null;}
        Check(File.Exists(Path.Combine(DeckRepository.DeckRoot,prefix+"-import.mdk")),"Imported deck not saved");
        Button("ImportDeckButton").onClick.Invoke();yield return null;
        GameObject.Find("DeckText").GetComponent<InputField>().text="2 Mountain\n1 definitely-invalid-card";yield return new WaitForSecondsRealtime(.7f);
        Check(GameObject.Find("ImportErrors")!=null && Field<DeckDefinition>(menu,"currentDeck").name==prefix+"-import","Invalid import changed deck");
        ScreenCapture.CaptureScreenshot(Folder+"/invalid-import.png");yield return new WaitForEndOfFrame();Button("Cancel").onClick.Invoke();yield return null;
        string clipboard=GUIUtility.systemCopyBuffer;
        try
        {
            Button("ExportDeckButton").onClick.Invoke();yield return new WaitForSecondsRealtime(.4f);
            foreach(string format in new[]{"MO","MTGA","MTGB"})
            {
                Button(format).onClick.Invoke();Button("Copy").onClick.Invoke();yield return null;
                var codec=Field<DeckTextExchange>(menu,"deckExchange");var imported=codec.Import(GUIUtility.systemCopyBuffer);
                Check(imported.Success && imported.Deck.cards.Sum(e=>e.count)==2 && imported.Deck.sideboard.Sum(e=>e.count)==1,"Clipboard format lost quantities: "+format);
            }
            ScreenCapture.CaptureScreenshot(Folder+"/export.png");yield return new WaitForEndOfFrame();Button("Close").onClick.Invoke();
        }
        finally {GUIUtility.systemCopyBuffer=clipboard;}
    }
}
#endif
