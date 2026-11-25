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
            var alchemy=new DeckTextExchange(new[]{
                new CardDefinition{cardName="Key to the Archive",setCode="YMID",collectorNumber=59},
                new CardDefinition{cardName="Crucias, Titan of the Waves",setCode="YBRO",collectorNumber=18},
                new CardDefinition{cardName="Shove Aside",setCode="YMKM",collectorNumber=16},
                new CardDefinition{cardName="Impetuous Lootmonger",setCode="YOTJ",collectorNumber=12},
                new CardDefinition{cardName="Thought Partition",setCode="YEOE",collectorNumber=4},
                new CardDefinition{cardName="Honor the Past",setCode="YSOS",collectorNumber=1},
                new CardDefinition{cardName="The Mystical Archive",setCode="YSOS",collectorNumber=30}});
            foreach(var item in new[]{("Key to the Archive","Y22","59","YMID/59"),
                ("Crucias, Titan of the Waves","Y23","18","YBRO/18"),
                ("Shove Aside","Y24","16","YMKM/16"),
                ("Impetuous Lootmonger","Y24","12","YOTJ/12"),
                ("Thought Partition","Y25","4","YEOE/4"),
                ("Honor the Past","Y26","1","YSOS/1"),
                ("The Mystical Archive","Y26","30","YSOS/30")})
            {
                var imported=alchemy.Import($"1 {item.Item1} ({item.Item2}) {item.Item3}");
                Check(imported.Success && imported.Deck.cards.Single().PrintingKey==item.Item4,"MTGA Alchemy year import failed: "+item.Item1);
            }
            Check(!alchemy.Import("1 Key to the Archive (Y23) 59").Success,"Wrong Alchemy year accepted");
            Check(!alchemy.Import("1 Key to the Archive (Y22) 58").Success,"Wrong Alchemy collector number accepted");
            var ambiguousYear=new DeckTextExchange(new[]{
                new CardDefinition{cardName="Example",setCode="YMID",collectorNumber=1},
                new CardDefinition{cardName="Example",setCode="YNEO",collectorNumber=1}});
            Check(!ambiguousYear.Import("1 Example (Y22) 1").Success,"Ambiguous Alchemy year accepted");
            var variants=new DeckTextExchange(new[]{split,
                new CardDefinition{cardName=split.cardName,setCode="MH2",collectorNumberText="290b"}});
            Check(!variants.Import("1 Fire // Ice (MH2) 290").Success,"Ambiguous collector suffix accepted");
            Check(!variants.Import("1 Fire // Ice (MH2) 0").Success,"Ambiguous zero placeholder accepted");
            File.WriteAllText(Folder+"/codec-result.txt","PASS: MO/MTGA/MTGB round trips; main/side/commander; CJK aliases; split card and alphanumeric collector; SB prefix; repeated rows; quantities preserved; malformed/unknown/mismatched/unsupported input atomic rejection.");
        }
        catch(Exception e){File.WriteAllText(Folder+"/codec-result.txt","FAIL "+e);throw;}
    }
    public static void RunAlchemyCatalog()
    {
        Directory.CreateDirectory(Folder);
        const string report=Folder+"/alchemy-catalog-result.txt";
        try
        {
            var cards=CardDatabaseRepository.LoadAll();var codec=new DeckTextExchange(cards);
            foreach(var item in new[]{("Key to the Archive","Y22","59","YMID/59"),
                ("Crucias, Titan of the Waves","Y23","18","YBRO/18"),
                ("Shove Aside","Y24","16","YMKM/16"),
                ("Impetuous Lootmonger","Y24","12","YOTJ/12"),
                ("Thought Partition","Y25","4","YEOE/4"),
                ("Honor the Past","Y26","1","YSOS/1"),
                ("The Mystical Archive","Y26","30","YSOS/30"),
                ("Library of Alexandria","SPG","0","SPG/158a"),
                ("Library of Alexandria","SPG","158","SPG/158a"),
                ("Library of Alexandria","SPG","158a","SPG/158a"),
                ("Library of Leng","SPG","158","SPG/158"),
                ("The One Ring","LTR","0","LTR/0")})
            {
                var result=codec.Import($"1 {item.Item1} ({item.Item2}) {item.Item3}");
                Check(result.Success && result.Deck.cards.Single().PrintingKey==item.Item4,"Expected "+item.Item4+" for "+item.Item1+" ("+item.Item2+") "+item.Item3);
            }
            Check(!codec.Import("1 The Mystical Archive (Y26) 31").Success,"A mismatched Archive printing was accepted");
            Check(!codec.Import("1 Mana Crypt (SPG) 0").Success,"An unspecified Mana Crypt variant was chosen");
            Check(!codec.Import("1 Library of Alexandria (SPG) 158b").Success,"An explicit wrong suffix was replaced");
            File.WriteAllText(report,"PASS: Alchemy year and unique collector aliases, exact numeric and alphanumeric printings, ambiguous and mismatched rejection.");
        }
        catch(Exception e){File.WriteAllText(report,"FAIL "+e);throw;}
    }
    static T Field<T>(object obj,string name)=>(T)obj.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(obj);
    static bool running;
    public static void RunUi()
    {
        if(running)return;running=true;Directory.CreateDirectory(Folder);
        var menu=UnityEngine.Object.FindFirstObjectByType<MainMenuController>();Check(menu!=null,"Main scene is required");
        menu.StartCoroutine(Guard(menu));
    }
    public static void RunImportScrollUi()
    {
        Directory.CreateDirectory(Folder);
        var menu=UnityEngine.Object.FindFirstObjectByType<MainMenuController>();
        Check(menu!=null,"Main scene is required");
        menu.StartCoroutine(CheckImportScroll(menu));
    }
    static IEnumerator CheckImportScroll(MainMenuController menu)
    {
        const string report=Folder+"/import-scroll-result.txt";
        File.WriteAllText(report,"RUNNING");
        bool completed=false;
        try
        {
            float deadline=Time.realtimeSinceStartup+60;
            while(!Field<bool>(menu,"dataReady"))
            {
                Check(Time.realtimeSinceStartup<deadline,"Catalog load timeout");yield return null;
            }
            menu.SendMessage("ShowDeckBuilder");yield return null;
            menu.SendMessage("ShowImportDeck");yield return null;
            var input=GameObject.Find("DeckText")?.GetComponent<TMPro.TMP_InputField>();
            Check(input!=null && input.verticalScrollbar!=null && input.textViewport!=null,"Import text cannot scroll");
            yield return new WaitForEndOfFrame();
            var caret=input.textViewport.GetComponentInChildren<TMPro.TMP_SelectionCaret>();
            Check(input.isFocused && caret!=null && caret.isActiveAndEnabled,"Opening Import must focus the field and create its native caret");
            input.text=string.Join("\n",Enumerable.Range(0,90).Select(i=>"1 invalid-card-"+i));
            yield return null;Canvas.ForceUpdateCanvases();input.textComponent.ForceMeshUpdate();
            input.verticalScrollbar.value=0;
            var point=RectTransformUtility.WorldToScreenPoint(null,input.textViewport.TransformPoint(input.textViewport.rect.center));
            var hits=new List<RaycastResult>();
            var wheel=new PointerEventData(EventSystem.current){position=point,scrollDelta=new Vector2(0,-3)};
            EventSystem.current.RaycastAll(wheel,hits);
            Check(hits.Count>0 && hits[0].gameObject.GetComponentInParent<TMPro.TMP_InputField>()==input,"Import text blocks wheel events");
            ExecuteEvents.ExecuteHierarchy(hits[0].gameObject,wheel,ExecuteEvents.scrollHandler);
            Check(input.verticalScrollbar.value>0,"Mouse wheel did not scroll import text");
            deadline=Time.realtimeSinceStartup+30;
            while(GameObject.Find("ImportErrors")==null)
            {
                Check(Time.realtimeSinceStartup<deadline,"Import error feedback timeout");yield return null;
            }
            var errors=GameObject.Find("ImportErrors")?.GetComponent<TMPro.TMP_InputField>();
            Check(errors!=null && errors.verticalScrollbar!=null && errors.gameObject.activeInHierarchy,"Import errors cannot scroll");
            errors.text=string.Join("\n",Enumerable.Range(0,20).Select(i=>"第"+i+"行：无法识别"));
            yield return null;Canvas.ForceUpdateCanvases();errors.textComponent.ForceMeshUpdate();
            errors.verticalScrollbar.value=0;
            errors.OnScroll(new PointerEventData(EventSystem.current){scrollDelta=new Vector2(0,-3)});
            Check(errors.verticalScrollbar.value>0,"Mouse wheel did not scroll error list");
            ScreenCapture.CaptureScreenshot(Folder+"/alchemy-import-scroll.png");
            yield return new WaitForEndOfFrame();
            completed=true;
        }
        finally
        {
            if(GameObject.Find("Cancel")!=null)Button("Cancel").onClick.Invoke();
            File.WriteAllText(report,completed?"PASS: long MTGA import text and error list show scrollbars and respond to wheel events.":"FAIL: see Unity exception log");
        }
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
        var expected=new[]{"BackToTitleButton","DeckDropdown","RenameDeckButton","DeleteDeckButton","NewDeckButton","SaveDeckButton","SaveAsDeckButton","ImportDeckButton","ExportDeckButton"};
        Check(controls.Select(c=>c.name).SequenceEqual(expected),"Unexpected visible deck toolbar order");
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
        var importInput=GameObject.Find("DeckText").GetComponent<TMPro.TMP_InputField>();
        Check(importInput!=null && importInput.verticalScrollbar!=null && importInput.textViewport!=null,"Import text has no scrollable viewport");
        importInput.text=string.Join("\n",Enumerable.Range(0,70).Select(i=>"1 definitely-invalid-card-"+i));
        yield return null;Canvas.ForceUpdateCanvases();
        importInput.verticalScrollbar.value=0;
        importInput.OnScroll(new PointerEventData(EventSystem.current){scrollDelta=new Vector2(0,-3)});
        Check(importInput.verticalScrollbar.value>0,"Import wheel did not scroll long text");
        importInput.text="#name "+prefix+"-import\n#main\n2 10E/376\n!side\n1 10E/232";
        deadline=Time.realtimeSinceStartup+30;
        while(Field<DeckDefinition>(menu,"currentDeck").name!=prefix+"-import"){Check(Time.realtimeSinceStartup<deadline,"Auto import failed");yield return null;}
        Check(File.Exists(Path.Combine(DeckRepository.DeckRoot,prefix+"-import.mdk")),"Imported deck not saved");
        Button("ImportDeckButton").onClick.Invoke();yield return null;
        GameObject.Find("DeckText").GetComponent<TMPro.TMP_InputField>().text="2 Mountain\n1 definitely-invalid-card";yield return new WaitForSecondsRealtime(.7f);
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
