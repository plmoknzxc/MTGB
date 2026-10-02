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

public static class DeckCollapseValidation
{
    const BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
    const string Output=".tools/deck-collapse/";
    static T Field<T>(MainMenuController ui,string name)=>(T)typeof(MainMenuController).GetField(name,Flags).GetValue(ui);
    static void Check(bool value,string message){if(!value)throw new Exception(message);}
    public static void Run()
    {
        Check(Application.isPlaying,"Play Mode required");
        var ui=UnityEngine.Object.FindFirstObjectByType<MainMenuController>();
        Directory.CreateDirectory(Output);File.WriteAllText(Output+"result.txt","RUNNING");
        ui.StartCoroutine(Guard(ui));
    }
    static IEnumerator Guard(MainMenuController ui)
    {
        var events=EventSystem.current;bool enabled=events.enabled;events.enabled=false;
        var run=Validate(ui);
        try
        {
            while(true)
            {
                object next;
                try{if(!run.MoveNext())break;next=run.Current;}
                catch(Exception e){File.WriteAllText(Output+"result.txt","FAIL: "+e);throw;}
                yield return next;
            }
        }
        finally{events.enabled=enabled;}
    }
    static IEnumerator Validate(MainMenuController ui)
    {
        while(!Field<bool>(ui,"dataReady"))yield return null;
        typeof(MainMenuController).GetMethod("ShowDeckBuilder",Flags).Invoke(ui,null);
        yield return null;
        var main=Field<RectTransform>(ui,"mainArea");
        var detailButton=Field<Button>(ui,"detailsCollapseButton");
        var sideButton=Field<Button>(ui,"sideboardCollapseButton");
        var preview=Field<CanvasGroup>(ui,"cardPreviewGroup");
        float width=main.rect.width,height=main.rect.height;
        detailButton.onClick.Invoke();sideButton.onClick.Invoke();
        yield return null;yield return null;
        Check(main.rect.width>width&&Field<float>(ui,"detailsCollapseProgress")<1,"Missing animated width transition");
        while(Field<float>(ui,"detailsCollapseProgress")<1||Field<float>(ui,"sideboardCollapseProgress")<1)yield return null;
        yield return null;
        Check(main.rect.width>width+100&&main.rect.height>height+100,"Main deck did not reclaim both regions");
        Check(preview.alpha==0&&!preview.blocksRaycasts,"Closed detail panel blocks input");
        Check(Field<CanvasGroup>(ui,"sideAreaGroup").alpha==0,"Sideboard remains visible");
        ScreenCapture.CaptureScreenshot(Output+"collapsed.png");yield return new WaitForEndOfFrame();
        var cards=Field<List<CardDefinition>>(ui,"cards");
        var card=cards.First(c=>c.cardName=="Delver of Secrets"&&CardImageLibrary.HasBackFace(c));
        var tile=Field<RectTransform>(ui,"collectionContent").GetComponentInChildren<MainMenuCardTile>();
        var oldCard=tile.Card;
        tile.Configure(ui,card,MainMenuCardTileZone.Collection);
        var face=tile.transform.Find("CardFace").GetComponent<Image>();var oldSprite=face.sprite;
        CardImageLibrary.TryLoadFullCard(card,out Sprite sprite);face.sprite=sprite;
        ui.BeginCardHover(tile);
        yield return new WaitForSecondsRealtime(.5f);
        Check(!Field<bool>(ui,"floatingDeckDetails"),"Hover popup appeared without its delay");
        yield return new WaitForSecondsRealtime(.85f);
        Check(Field<bool>(ui,"floatingDeckDetails")&&preview.alpha==0,"Delayed art popup missing or old detail panel visible");
        var popup=Field<DeckHoverArtPopup>(ui,"deckArtHover");
        Check(popup.gameObject.activeInHierarchy&&popup.transform.Find("CardArt1")!=null,"Double face art popup missing");
        Check(popup.GetComponentsInChildren<Button>().Length==0,"Unexpected editing controls in art popup");
        var front=Field<RectTransform>(ui,"previewFrontRect");var back=Field<RectTransform>(ui,"previewBackRect");
        Check(back.gameObject.activeInHierarchy&&back.GetComponent<Image>().sprite!=null,"Back face missing in popup");
        Check(front.anchoredPosition.x<0&&back.anchoredPosition.x>0,"Double faces overlap");
        ScreenCapture.CaptureScreenshot(Output+"dual-hover.png");yield return new WaitForEndOfFrame();
        ui.EndCardHover(tile);
        yield return new WaitForSecondsRealtime(.4f);
        // Reopening the dock must preserve the same two-face renderer and restore the deck geometry.
        detailButton.onClick.Invoke();sideButton.onClick.Invoke();
        while(Field<float>(ui,"detailsCollapseProgress")>0||Field<float>(ui,"sideboardCollapseProgress")>0)yield return null;
        yield return null;
        Check(Mathf.Abs(main.rect.width-width)<1&&Mathf.Abs(main.rect.height-height)<1,"Expanded layout did not restore");
        Check(preview.alpha==1&&preview.blocksRaycasts&&back.gameObject.activeInHierarchy,"Restored detail/dual faces unavailable");
        ScreenCapture.CaptureScreenshot(Output+"restored-dual.png");yield return new WaitForEndOfFrame();
        tile.Configure(ui,oldCard,MainMenuCardTileZone.Collection);face.sprite=oldSprite;
        File.WriteAllText(Output+"result.txt","PASS: animated collapse, reclaimed width/height, closed input, delayed hover, double-face popup and restored dock.");
    }
}
#endif
