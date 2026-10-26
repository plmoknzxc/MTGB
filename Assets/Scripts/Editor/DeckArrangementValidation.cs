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

public static class DeckArrangementValidation
{
    const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
    public static bool Running {get;private set;}
    sealed class Host:MonoBehaviour{}
    static readonly List<string> checks=new();
    static MainMenuController ui;
    static string output;
    static object Get(string name)=>typeof(MainMenuController).GetField(name,Hidden).GetValue(ui);
    static void Set(string name,object value)=>typeof(MainMenuController).GetField(name,Hidden).SetValue(ui,value);
    static object Call(string name,params object[] args)=>typeof(MainMenuController).GetMethod(name,Hidden).Invoke(ui,args);
    static void Check(bool ok,string message){checks.Add((ok?"PASS ":"FAIL ")+message);File.WriteAllLines(output+"/checks.txt",checks);if(!ok)throw new Exception(message);}
    static MainMenuCardTile[] Tiles(string name)=>((RectTransform)Get(name)).GetComponentsInChildren<MainMenuCardTile>().Where(t=>t.gameObject.activeSelf).OrderBy(t=>t.transform.GetSiblingIndex()).ToArray();
    static DeckEntry Entry(CardDefinition c,int n)=>new(){setCode=c.setCode,collectorNumber=c.CollectorNumber,cardId=c.cardID,count=n};
    static int Copies(DeckDefinition deck,CardDefinition card)=>deck.cards.Concat(deck.sideboard).Where(e=>DeckRepository.SharesCopyLimit(DeckRepository.ResolveCard(e,(List<CardDefinition>)Get("cards")),card)).Sum(e=>e.count);
    static Vector2 Center(Component c)=>RectTransformUtility.WorldToScreenPoint(null,((RectTransform)c.transform).TransformPoint(((RectTransform)c.transform).rect.center));
    static PointerEventData Pointer(Component c)=>new(EventSystem.current){position=Center(c),button=PointerEventData.InputButton.Left};
    static void Click(Component control)
    {
        var pointer=Pointer(control);var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(pointer,hits);
        Check(hits.Count>0&&(hits[0].gameObject.transform==control.transform||hits[0].gameObject.transform.IsChildOf(control.transform)),"Actual ray reaches "+control.name);
        ExecuteEvents.Execute(control.gameObject,pointer,ExecuteEvents.pointerClickHandler);
    }
    static IEnumerator Screenshot(string name){yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(output+"/"+name+".png");yield return new WaitForEndOfFrame();yield return null;}
    public static void Run(string directory)
    {
        if(Running||!Application.isPlaying)throw new InvalidOperationException("Play required; only one validation at a time");
        output=directory;Directory.CreateDirectory(output);checks.Clear();Running=true;
        new GameObject("DeckArrangementValidation").AddComponent<Host>().StartCoroutine(Guard());
    }
    static IEnumerator Guard()
    {
        var routine=Exercise();
        while(true){object step;try{if(!routine.MoveNext())break;step=routine.Current;}catch(Exception e){File.WriteAllText(output+"/result.txt","FAIL "+e);break;}yield return step;}
        (routine as IDisposable)?.Dispose();Running=false;
    }
    static IEnumerator Exercise()
    {
        while(!GamePreferences.Ready)yield return null;
        ui=UnityEngine.Object.FindFirstObjectByType<MainMenuController>();
        while(!(bool)Get("dataReady"))yield return null;
        var original=(DeckDefinition)Get("currentDeck");bool dirty=(bool)Get("hasUnsavedDeckChanges");float oldScale=GamePreferences.TextScale;
        var catalog=(List<CardDefinition>)Get("cards");
        CardDefinition Card(string name)=>catalog.First(c=>c.cardName==name&&!c.forgeAmbiguous);
        var forest=Card("Forest");var negate=Card("Negate");var duress=Card("Duress");var island=Card("Island");var swamp=Card("Swamp");
        var otherNegate=catalog.First(c=>c.cardName=="Negate"&&!c.forgeAmbiguous&&c.PrintingKey!=negate.PrintingKey);
        var deck=new DeckDefinition{name="Arrangement validation",format="none",cards=new(){Entry(forest,3),Entry(negate,2),Entry(duress,1),Entry(island,1),Entry(swamp,1)},sideboard=new(){Entry(otherNegate,1),Entry(duress,2)}};
        try
        {
            Call("ShowDeckBuilder");Set("currentDeck",deck);Set("hasUnsavedDeckChanges",false);Call("RefreshDeckGrids");yield return null;yield return null;Canvas.ForceUpdateCanvases();
            var dropdown=(Dropdown)Get("deckArrangement");
            Check(dropdown.value==0&&dropdown.captionText.text=="合并排列","Default caption and merged mode");
            Check(Tiles("mainDeckContent").Length==5&&Tiles("sideboardContent").Length==2,"Merged mode has one tile per printing");
            Click(dropdown);yield return null;Canvas.ForceUpdateCanvases();
            var list=dropdown.transform.Find("Dropdown List");Check(list!=null,"Dropdown opens");
            yield return Screenshot("dropdown");
            var option=list.GetComponentsInChildren<Toggle>().First(t=>t.GetComponentInChildren<Text>().text=="单张排列");Click(option);yield return new WaitForSecondsRealtime(.25f);Canvas.ForceUpdateCanvases();
            Check(dropdown.value==1&&dropdown.captionText.text=="单张排列","Option selects single mode and current caption");
            Check(!(bool)Get("hasUnsavedDeckChanges")&&deck.mainOrder.Count==0,"View toggle alone does not mutate or dirty deck");
            Check(Tiles("mainDeckContent").Length==8&&Tiles("sideboardContent").Length==3,"Single mode expands both sections to actual quantities");
            Check(Tiles("mainDeckContent").Concat(Tiles("sideboardContent")).All(t=>!t.transform.Find("CountBackground").gameObject.activeSelf),"Single mode hides all grouped quantity badges");
            var main=Tiles("mainDeckContent");Click(main[1]);yield return null;
            Check(main.Count(t=>t.QuantityControlsVisible)==1,"Only clicked physical copy opens quantity controls");Call("CloseQuantityControls");
            var pointer=Pointer(main[4]);pointer.position+=Vector2.right*10;
            ExecuteEvents.Execute(main[1].gameObject,pointer,ExecuteEvents.beginDragHandler);
            ExecuteEvents.Execute(main[1].gameObject,pointer,ExecuteEvents.endDragHandler);yield return null;Canvas.ForceUpdateCanvases();
            string[] expected={forest.PrintingKey,forest.PrintingKey,negate.PrintingKey,negate.PrintingKey,forest.PrintingKey,duress.PrintingKey,island.PrintingKey,swamp.PrintingKey};
            Check(deck.mainOrder.SequenceEqual(expected),"Actual drag moves one copy across another printing");
            Check(Tiles("mainDeckContent").Select(t=>t.Card.PrintingKey).SequenceEqual(expected),"Single view renders interleaved canonical order");
            yield return Screenshot("individual");
            File.WriteAllText(output+"/ordered.mdk",DeckRepository.SerializeLineDeck(deck));var loaded=DeckRepository.Load(output+"/ordered.mdk");
            Check(loaded!=null&&loaded.mainOrder.SequenceEqual(expected)&&loaded.cards.Sum(e=>e.count)==8,"Disk save/load keeps exact physical order and composition");
            var clone=(DeckDefinition)typeof(MainMenuController).GetMethod("CloneDeck",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{loaded});
            Check(clone.mainOrder.SequenceEqual(expected)&&!ReferenceEquals(clone.mainOrder,loaded.mainOrder),"Editor clone keeps independent copy-order metadata");
            var wire=JsonUtility.FromJson<DeckDefinition>(JsonUtility.ToJson(clone));
            Check(OnlineMessageGuard.ValidateDeckShape(wire,out _)&&wire.mainOrder.SequenceEqual(expected),"Network serialization preserves validated order");
            wire.mainOrder[0]="BAD/999";Check(!OnlineMessageGuard.ValidateDeckShape(wire,out _),"Authority rejects unknown copy in order");
            wire.mainOrder.RemoveAt(0);Check(!OnlineMessageGuard.ValidateDeckShape(wire,out _),"Authority rejects mismatched order length");
            // Delete the interleaved third Forest, not the first Forest in the list.
            var remove=Tiles("mainDeckContent").First(t=>t.CopyOrderIndex==4);ui.ChangeCardQuantity(remove,-1);yield return null;
            Check(deck.mainOrder.SequenceEqual(expected.Where((_,i)=>i!=4)),"Minus removes the exact selected physical slot");
            Check(Tiles("mainDeckContent").Length==7,"Minus immediately removes one tile");
            var increase=Tiles("mainDeckContent").First(t=>t.Card==negate);ui.ChangeCardQuantity(increase,1);yield return null;
            Check(Copies(deck,negate)==4&&Tiles("mainDeckContent").Length==8,"Plus adds one copy across existing versions and zones");
            ui.ChangeCardQuantity(Tiles("mainDeckContent").First(t=>t.Card==negate),1);yield return null;
            Check(Copies(deck,negate)==4&&Tiles("mainDeckContent").Length==8,"Fifth copy remains blocked across versions");
            var moving=Tiles("mainDeckContent").First(t=>t.CopyOrderIndex==2);var before=deck.mainOrder.ToArray();
            var destination=(RectTransform)Get("sideboardContent");var movePointer=Pointer(destination);
            ExecuteEvents.Execute(moving.gameObject,movePointer,ExecuteEvents.beginDragHandler);ExecuteEvents.Execute(moving.gameObject,movePointer,ExecuteEvents.endDragHandler);yield return null;
            Check(deck.mainOrder.SequenceEqual(before.Where((_,i)=>i!=2))&&deck.sideboard.Sum(e=>e.count)==4,"Cross-zone drag moves exactly one selected physical slot");
            Check(DeckCopyOrder.IsValid(deck.cards,deck.mainOrder,500),"Order stays consistent after main/side transfer");
            dropdown.value=0;yield return null;
            Check(Tiles("mainDeckContent").Length==5&&deck.mainOrder.Count==7,"Merged mode retains saved individual positions");
            yield return Screenshot("merged");
            var preserved=deck.mainOrder.ToArray();dropdown.value=1;yield return null;
            Check(Tiles("mainDeckContent").Select(t=>t.Card.PrintingKey).SequenceEqual(preserved),"Switching back restores individual sequence");
            var sortedMode=typeof(MainMenuController).GetNestedType("DeckViewSort",BindingFlags.NonPublic);
            var previousRandom=Get("deckSortRandom");
            try
            {
                Set("deckSortRandom",new System.Random(1022));
                Call("SetDeckViewSort",Enum.Parse(sortedMode,"Shuffle"));yield return null;
                var shuffled=Tiles("mainDeckContent");var shuffledKeys=shuffled.Select(t=>t.Card.PrintingKey).ToArray();
                Check(shuffledKeys.OrderBy(k=>k).SequenceEqual(preserved.OrderBy(k=>k)),"Individual shuffle retains every copy and printing");
                int runs=shuffledKeys.Where((k,i)=>i==0||k!=shuffledKeys[i-1]).Count();
                Check(runs>shuffledKeys.Distinct().Count(),"Seeded individual shuffle separates repeated copies instead of shuffling groups");
                Check(shuffled.Select(t=>t.CopyOrderIndex).OrderBy(i=>i).SequenceEqual(Enumerable.Range(0,preserved.Length)),"Shuffled tiles retain distinct canonical copy indices");
                var display=(IEnumerable<DeckEntry>)Call("DeckDisplayEntries",deck.cards);
                Check(display.SequenceEqual(display.ToArray()),"Shuffled display can be enumerated repeatedly without changing order");
                dropdown.value=0;yield return null;dropdown.value=1;yield return null;
                Check(Tiles("mainDeckContent").Select(t=>t.Card.PrintingKey).SequenceEqual(shuffledKeys),"Merged and individual toggles retain the shuffled copy order");
                Check(deck.mainOrder.SequenceEqual(preserved),"Shuffle remains a view and preserves the saved physical order");
            }
            finally {Set("deckSortRandom",previousRandom);}
            Call("SetDeckViewSort",Enum.Parse(sortedMode,"Original"));yield return null;
            Call("SetDeckViewSort",Enum.Parse(sortedMode,"Cost"));yield return null;Canvas.ForceUpdateCanvases();
            Check(deck.mainOrder.SequenceEqual(preserved),"Cost sorting is a view and leaves persisted order intact");
            var sorted=Tiles("mainDeckContent");var sortedSource=sorted.First(t=>t.Card==forest);var sortedTarget=sorted.First(t=>t.Card==duress);
            int sourceIndex=sortedSource.CopyOrderIndex,targetIndex=sortedTarget.CopyOrderIndex;
            var afterSortDrag=preserved.ToList();string moved=afterSortDrag[sourceIndex];afterSortDrag.RemoveAt(sourceIndex);int insert=targetIndex+(sourceIndex>targetIndex?1:0);afterSortDrag.Insert(insert,moved);
            var sortedPointer=Pointer(sortedTarget);sortedPointer.position+=Vector2.right*10;
            ExecuteEvents.Execute(sortedSource.gameObject,sortedPointer,ExecuteEvents.beginDragHandler);ExecuteEvents.Execute(sortedSource.gameObject,sortedPointer,ExecuteEvents.endDragHandler);yield return null;
            Check(deck.mainOrder.SequenceEqual(afterSortDrag),"Dragging from sorted view maps to exact canonical physical slots");
            dropdown.value=0;yield return null;Canvas.ForceUpdateCanvases();
            var grouped=Tiles("mainDeckContent");var groupedSource=grouped.First(t=>t.Card==forest);var groupedTarget=grouped.First(t=>t.Card==swamp);
            var groupedPointer=Pointer(groupedTarget);groupedPointer.position+=Vector2.right*10;
            ExecuteEvents.Execute(groupedSource.gameObject,groupedPointer,ExecuteEvents.beginDragHandler);ExecuteEvents.Execute(groupedSource.gameObject,groupedPointer,ExecuteEvents.endDragHandler);yield return null;
            Check(deck.mainOrder.TakeLast(2).All(k=>k==forest.PrintingKey)&&DeckCopyOrder.IsValid(deck.cards,deck.mainOrder,500),"Merged drag moves all copies and retains a complete valid physical order");
            // Make persisted first-occurrence order differ from grouped storage.
            var forestCopies=deck.mainOrder.Where(k=>k==forest.PrintingKey).ToList();deck.mainOrder.RemoveAll(k=>k==forest.PrintingKey);deck.mainOrder.InsertRange(1,forestCopies);Call("RefreshDeckGrids");yield return null;Canvas.ForceUpdateCanvases();
            grouped=Tiles("mainDeckContent");groupedSource=grouped.First(t=>t.Card==forest);groupedTarget=grouped.Last(t=>t.Card!=forest);
            groupedPointer=Pointer(groupedTarget);groupedPointer.position+=Vector2.right*10;
            ExecuteEvents.Execute(groupedSource.gameObject,groupedPointer,ExecuteEvents.beginDragHandler);ExecuteEvents.Execute(groupedSource.gameObject,groupedPointer,ExecuteEvents.endDragHandler);yield return null;
            Check(deck.mainOrder.TakeLast(2).All(k=>k==forest.PrintingKey),"Merged drag uses visible first-occurrence order after individual interleaving");
            foreach(float scale in new[]{.85f,1.3f})
            {
                GamePreferences.SetScale(scale);yield return null;yield return null;Canvas.ForceUpdateCanvases();
                Call("LayoutDeckHeader",520f,38f);
                var controls=new[]{"deckCurve","deckArrangement","deckCostSort","deckTypeSort","deckShuffle"}.Select(n=>(RectTransform)((Component)Get(n)).transform).ToArray();
                foreach(var control in controls)Check(control.sizeDelta.x>40&&control.sizeDelta.y>25,"Narrow header keeps usable control "+control.name+" scale="+scale);
                foreach(var control in controls)Check(control.anchoredPosition.x-control.sizeDelta.x>=-520&&control.anchoredPosition.x<=0,"Narrow header control stays within width "+control.name+" scale="+scale);
                Rect Bounds(RectTransform c)=>new(c.anchoredPosition.x-c.sizeDelta.x,c.anchoredPosition.y-c.sizeDelta.y*.5f,c.sizeDelta.x,c.sizeDelta.y);
                for(int i=0;i<controls.Length;i++)for(int j=i+1;j<controls.Length;j++)Check(!Bounds(controls[i]).Overlaps(Bounds(controls[j])),"Header controls do not overlap "+i+"/"+j+" scale="+scale);
                Check(dropdown.captionText.preferredWidth<=dropdown.captionText.rectTransform.rect.width+1,"Current mode caption fits scale="+scale);
            }
            GamePreferences.SetScale(oldScale);dropdown.value=1;yield return null;
            // A full deck exported after a real save/reload is consumed by native test case individual.
            var native=loaded;DeckRepository.AddCard(native,Card("Plains"),52,false);
            File.WriteAllText(output+"/native.mdk",DeckRepository.SerializeLineDeck(native));native=DeckRepository.Load(output+"/native.mdk");
            Check(native.cards.Sum(e=>e.count)==60&&OnlineMessageGuard.ValidateDeckShape(native,out _),"Saved/reloaded 60-card native input retains valid exact order");
            File.WriteAllText(output+"/native-deck.json",JsonUtility.ToJson(native));
            var lobby=UnityEngine.Object.FindFirstObjectByType<OnlineLobbyPanel>(FindObjectsInactive.Include);
            bool createdLobby=lobby==null;
            if(createdLobby){lobby=(OnlineLobbyPanel)typeof(OnlineLobbyPanel).GetMethod("Create",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{UnityEngine.Object.FindFirstObjectByType<Canvas>().transform});lobby.gameObject.SetActive(false);}
            var service=OnlineMatchService.EnsureInstance();Check(!service.IsConnected,"Sideboard test uses an offline service");
            var property=typeof(OnlineMatchService).GetProperty("LobbyState");var previous=property.GetValue(service);
            var selectedField=typeof(OnlineLobbyPanel).GetField("selectedDeck",Hidden);var previousDeck=selectedField.GetValue(lobby);
            var readyField=typeof(OnlineLobbyPanel).GetField("ready",Hidden);var previousReady=readyField.GetValue(lobby);
            try
            {
                var sideboardDeck=JsonUtility.FromJson<DeckDefinition>(JsonUtility.ToJson(native));sideboardDeck.sideboardOrder=DeckCopyOrder.Expand(sideboardDeck.sideboard,null).Select(DeckCopyOrder.Key).ToList();
                property.SetValue(service,new OnlineLobbyState{series=new OnlineSeriesState{betweenGames=true}});selectedField.SetValue(lobby,sideboardDeck);readyField.SetValue(lobby,false);
                var move=typeof(OnlineLobbyPanel).GetMethod("MoveOneCard",Hidden);move.Invoke(lobby,new object[]{sideboardDeck.cards.First(e=>e.PrintingKey==forest.PrintingKey),true});
                Check(OnlineMessageGuard.ValidateDeckShape(sideboardDeck,out _)&&sideboardDeck.cards.Sum(e=>e.count)==59,"Actual BO3 sideboard move reconciles copy order for authority validation");
                move.Invoke(lobby,new object[]{sideboardDeck.sideboard.First(e=>e.PrintingKey==forest.PrintingKey),false});
                Check(OnlineMessageGuard.ValidateDeckShape(sideboardDeck,out _)&&sideboardDeck.cards.Sum(e=>e.count)==60,"Actual BO3 return move keeps valid complete main order");
            }
            finally {property.SetValue(service,previous);selectedField.SetValue(lobby,previousDeck);readyField.SetValue(lobby,previousReady);if(createdLobby)UnityEngine.Object.Destroy(lobby.gameObject);}
            File.WriteAllText(output+"/result.txt","PASS "+checks.Count+" deck arrangement, controls, drag, persistence and wire checks");
        }
        finally {GamePreferences.SetScale(oldScale);Set("currentDeck",original);Set("hasUnsavedDeckChanges",dirty);Call("CloseQuantityControls");Call("RefreshDeckGrids");}
    }
}
#endif
