using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public sealed partial class ForgeBattlefieldController
{
    private readonly Dictionary<string,string> counterLaneKeys=new Dictionary<string,string>();
    private static string SeriesPresentationKey(OnlineSeriesState series)=>series==null?"":series.bestOf+"/"+series.player1Wins+"/"+series.player2Wins;
    private static string PlayerSummary(ForgePlayerView player,OnlineSeriesState series)
    {
        string hand=TapPaymentText("手牌 ","Hand ","手札 ")+player.handCount;
        if(series?.bestOf!=3)return hand;
        int wins=player.seat==1?series.player1Wins:series.player2Wins;
        return hand+TapPaymentText(" · 胜 "," · Wins "," · 勝利 ")+wins;
    }
    private void PresentPlayerLife(string plate)
    {
        if(battlefieldNumeralFont==null)battlefieldNumeralFont=Font.CreateDynamicFontFromOSFont(new[]{"Times New Roman","Liberation Serif","Georgia"},24);
        var life=SceneText(plate+"/Life");var preference=life.GetComponent<PreferenceText>();
        if(preference==null)preference=life.gameObject.AddComponent<PreferenceText>();preference.KeepFont=true;preference.enabled=false;
        life.font=battlefieldNumeralFont;life.fontSize=Mathf.RoundToInt(42*GamePreferences.TextScale);
        life.fontStyle=FontStyle.Normal;life.alignment=TextAnchor.MiddleCenter;life.resizeTextForBestFit=false;life.verticalOverflow=VerticalWrapMode.Overflow;
        // The numeric region is between the divider at x=49 and the plate edge at x=171.
        life.rectTransform.anchoredPosition=new Vector2(110,0);life.rectTransform.sizeDelta=new Vector2(112,80);
        if(life.GetComponent<CenteredNumeralMesh>()==null)life.gameObject.AddComponent<CenteredNumeralMesh>();
        Node(plate+"/LifeCaption").gameObject.SetActive(false);
    }
    // Authored paths remain stable for native target and choice bindings.
    private void ApplyDuelLayout()
    {
        ArenaPlace("CornerCluster",new Vector2(1,0),new Vector2(-186,133),new Vector2(300,230));
        ArenaPlace("CornerCluster/ControlRow",new Vector2(.5f,.5f),Vector2.zero,new Vector2(300,160));
        ArenaPlace("CornerCluster/ControlRow/SkipCombatButton",new Vector2(.5f,.5f),new Vector2(48,-20),new Vector2(184,48));
        ArenaPlace("CornerCluster/ControlRow/PhaseText",new Vector2(.5f,.5f),new Vector2(0,67),new Vector2(280,44));
        ArenaTextSize("CornerCluster/ControlRow/PhaseText",22);
        SceneText("CornerCluster/ControlRow/PhaseText").alignment=TextAnchor.MiddleRight;
        ArenaPlace("CornerCluster/ControlRow/TurnChip",new Vector2(.5f,.5f),new Vector2(0,105),new Vector2(280,36));
        Node("CornerCluster/ControlRow/TurnChip").GetComponent<Image>().color=Color.clear;
        Node("CornerCluster/ControlRow/TurnChip/TurnNumber").gameObject.SetActive(false);
        ArenaPlace("CornerCluster/ControlRow/TurnChip/TurnLabel",new Vector2(.5f,.5f),Vector2.zero,new Vector2(280,36));
        ArenaTextSize("CornerCluster/ControlRow/TurnChip/TurnLabel",15);
        SceneText("CornerCluster/ControlRow/TurnChip/TurnLabel").alignment=TextAnchor.MiddleRight;
        Node("CornerCluster/ControlRow/RoundText").gameObject.SetActive(false);
        Node("CornerCluster/ControlRow/Divider").gameObject.SetActive(false);
        ArenaPlace("CornerCluster/ManaRow",new Vector2(.5f,.5f),Vector2.zero,new Vector2(300,34));
        Node("PriorityPill").gameObject.SetActive(false);
        StyleAdvanceButton();
    }
    private void StyleAdvanceButton()
    {
        if(advanceButton==null)return;
        var image=advanceButton.GetComponent<Image>();
        image.color=Color.clear;
        var rect=(RectTransform)advanceButton.transform;
        var face=rect.Find("ActionFace") as RectTransform;
        if(face==null){
            face=Rect(rect,"ActionFace",Vector2.zero,rect.sizeDelta);face.SetAsFirstSibling();
            face.gameObject.AddComponent<ForgeActionButtonGraphic>().raycastTarget=false;
            var arrow=Rect(rect,"Direction",new Vector2(61,0),new Vector2(22,22)).gameObject.AddComponent<StackControlIcon>();arrow.SetKind(StackControlIcon.Kind.Right);arrow.color=new Color(.13f,.11f,.075f);arrow.raycastTarget=false;
        }
        var graphic=face.GetComponent<ForgeActionButtonGraphic>();advanceButton.targetGraphic=graphic;
        rect.Find("Direction").GetComponent<StackControlIcon>().SetKind(StackControlIcon.Kind.Right);
        var colors=advanceButton.colors;colors.normalColor=Color.white;colors.highlightedColor=new Color(1.05f,1.05f,1.05f);colors.pressedColor=new Color(.87f,.87f,.87f);colors.disabledColor=new Color(.55f,.55f,.55f);advanceButton.colors=colors;
        var outline=advanceButton.GetComponent<Outline>();
        if(outline!=null)outline.enabled=false;
        foreach(var text in advanceButton.GetComponentsInChildren<Text>()) {
            text.color=new Color(.13f,.11f,.075f);PreferenceText.SetSize(text,21);text.resizeTextForBestFit=false;
            text.rectTransform.anchoredPosition=new Vector2(-14,0);text.rectTransform.sizeDelta=new Vector2(126,44);
            if(view?.request?.kind=="InputPassPriority")text.text=TapPaymentText("下一步","Next","次へ");
        }
        PresentPhaseTrack();
    }
    private string visiblePhaseKey;
    private float phaseVisibleUntil;
    private void PresentPhaseTrack()
    {
        var row=Node("CornerCluster/ControlRow");
        var track=row.Find("PhaseTrack") as RectTransform;if(track==null)track=Rect(row,"PhaseTrack",new Vector2(56,25),new Vector2(168,5));
        string phase=view?.phase??"";int current=phase=="MAIN1"?1:phase=="MAIN2"?3:phase.Contains("COMBAT")||phase.Contains("ATTACK")||phase.Contains("BLOCK")||phase.Contains("DAMAGE")?2:phase.Contains("END")||phase.Contains("CLEANUP")?4:0;
        string key=(view?.turnSeat??0)+"/"+(view?.turn??0)+"/"+current;
        bool changed=key!=visiblePhaseKey;
        if(changed){visiblePhaseKey=key;phaseVisibleUntil=Time.unscaledTime+.24f;}
        for(int i=0;i<5;i++){
            var bar=track.Find("Phase_"+i) as RectTransform;if(bar==null)bar=Panel(track,"Phase_"+i,new Vector2(-70+i*35,0),new Vector2(28,5),Paper);
            Color target=i<current?new Color(.71f,.73f,.67f):i==current?new Color(.91f,.76f,.48f):new Color(.45f,.53f,.54f,.47f);
            var fade=bar.GetComponent<ForgePhaseBarFade>()??bar.gameObject.AddComponent<ForgePhaseBarFade>();fade.SetTarget(target);
            bar.GetComponent<Image>().raycastTarget=false;
        }
    }
    private void PresentPlayerCounters(ForgePlayerView player,bool own)
    {
        var plate=Node(own?"PlayerPlate":"OpponentPlate");
        var row=plate.Find("PlayerCounters") as RectTransform;
        if(row==null)row=Rect(plate,"PlayerCounters",new Vector2(0,own?114:-114),new Vector2(310,30));
        var counters=(player.counters??Array.Empty<ForgeCardCounter>()).Where(c=>c.count>0).ToArray();
        // Only rebuild when the actual counter values or locale changes.
        string pageKey="counters:"+player.seat;
        int page=pages.TryGetValue(pageKey,out int saved)?saved:0;
        string counterKey=player.seat+"/"+page+"/"+string.Join("|",counters.Select(c=>c.kind+":"+c.count+":"+c.label))+"/"+GamePreferences.Revision;
        string slot=own?"own":"opponent";
        if(counterLaneKeys.TryGetValue(slot,out var oldKey)&&oldKey==counterKey)return;
        counterLaneKeys[slot]=counterKey;
        Clear(row);
        var chips=new List<RectTransform>();
        foreach(var counter in counters) {
            var chip=Rect(row,"Counter_"+counter.kind,Vector2.zero,new Vector2(48,34));
            if(counter.kind=="ENERGY") {
                if(poolGlyphFont==null)poolGlyphFont=Resources.Load<Font>("UI/ManaSymbols/ManaGlyphs");
                var glyph=Rect(chip,"Symbol",new Vector2(-8,4),new Vector2(30,32)).gameObject.AddComponent<Text>();
                glyph.font=poolGlyphFont;glyph.text="\uE907";glyph.fontSize=16;glyph.color=Color.white;
                glyph.alignment=TextAnchor.MiddleCenter;glyph.horizontalOverflow=HorizontalWrapMode.Overflow;glyph.verticalOverflow=VerticalWrapMode.Overflow;glyph.raycastTarget=false;
                var preference=glyph.gameObject.AddComponent<PreferenceText>();preference.KeepFont=true;preference.enabled=false;
                glyph.font=poolGlyphFont;preference.SetBaseSize(16);
            } else if(counter.kind=="EXPERIENCE") {
                var icon=Rect(chip,"Symbol",new Vector2(-8,4),new Vector2(28,28)).gameObject.AddComponent<ExperienceCounterIcon>();
                icon.color=Color.white;icon.raycastTarget=false;
            } else if(PlayerCounterIcons.TryLoad(counter.kind,out var sprite)) {
                var icon=Rect(chip,"Symbol",new Vector2(-8,4),new Vector2(28,28)).gameObject.AddComponent<Image>();
                icon.sprite=sprite;icon.preserveAspect=true;icon.raycastTarget=false;
            } else Label(chip,"Symbol",counter.label??counter.kind,new Vector2(-8,4),new Vector2(48,28),12,Paper).raycastTarget=false;
            var symbol=chip.Find("Symbol").GetComponent<Graphic>();
            if(symbol is Image || symbol is ExperienceCounterIcon || counter.kind=="ENERGY") {
                symbol.rectTransform.sizeDelta=new Vector2(32,32);
                symbol.gameObject.AddComponent<PlayerCounterSymbolFit>();
            }
            var count=Label(chip,"Count",counter.count.ToString(),new Vector2(-2,-7),new Vector2(24,30),18,Color.white);
            if(battlefieldNumeralFont==null)battlefieldNumeralFont=Font.CreateDynamicFontFromOSFont(new[]{"Times New Roman","Liberation Serif","Georgia"},24);
            var numeral=count.GetComponent<PreferenceText>()??count.gameObject.AddComponent<PreferenceText>();numeral.KeepFont=true;numeral.enabled=false;
            count.font=battlefieldNumeralFont;count.fontSize=Mathf.RoundToInt(18*GamePreferences.TextScale);count.fontStyle=FontStyle.Normal;count.supportRichText=false;
            count.alignment=TextAnchor.MiddleLeft;count.horizontalOverflow=HorizontalWrapMode.Overflow;count.raycastTarget=false;
            var shadow=count.gameObject.AddComponent<Outline>();shadow.effectColor=new Color(0,0,0,.95f);shadow.effectDistance=new Vector2(1,-1);
            float width=24+Mathf.Max(16,count.preferredWidth)+8;
            chip.sizeDelta=new Vector2(width,34);chips.Add(chip);
            ((RectTransform)chip.Find("Symbol")).anchoredPosition=new Vector2(-width*.5f+16,4);
            count.rectTransform.pivot=new Vector2(0,.5f);count.rectTransform.anchoredPosition=new Vector2(-width*.5f+22,-7);count.rectTransform.sizeDelta=new Vector2(width-26,30);
        }
        // Pack by the real numeral widths rather than an arbitrary type count.
        var packed=new List<List<RectTransform>>();var current=new List<RectTransform>();float used=0;
        foreach(var chip in chips) {
            if(current.Count>0 && used+chip.rect.width>280){packed.Add(current);current=new List<RectTransform>();used=0;}
            current.Add(chip);used+=chip.rect.width;
        }
        if(current.Count>0)packed.Add(current);
        int maxPage=Math.Max(0,packed.Count-1);page=Mathf.Clamp(page,0,maxPage);pages[pageKey]=page;
        foreach(var chip in chips)chip.gameObject.SetActive(false);
        float x=-140;
        if(packed.Count>0)foreach(var chip in packed[page]){chip.gameObject.SetActive(true);chip.anchoredPosition=new Vector2(x+chip.rect.width*.5f,0);x+=chip.rect.width;}
        if(maxPage>0)foreach(int direction in new[]{-1,1}) {
            var button=StackIcon(row,direction<0?"Previous":"Next",direction<0?StackControlIcon.Kind.Left:StackControlIcon.Kind.Right,()=>{pages[pageKey]=Mathf.Clamp(page+direction,0,maxPage);PresentIntegrated(connectionText.text);});
            ((RectTransform)button.transform).anchoredPosition=new Vector2(direction*166,0);((RectTransform)button.transform).sizeDelta=new Vector2(24,28);
            button.interactable=direction<0?page>0:page<maxPage;
        }
    }
}
