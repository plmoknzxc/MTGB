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
        return TapPaymentText("手牌 ","Hand ","手札 ")+player.handCount;
    }
    private void PresentPlayerSummary(string plate,ForgePlayerView player,OnlineSeriesState series)
    {
        PresentCommanderTax(plate,player);
        int slots=series?.bestOf==3?2:series?.bestOf==5?3:0;
        bool showWins=slots>0;
        var hand=SceneText(plate+"/Sub");
        float handWidth=(showWins?144-slots*16:148)-(HudTheme!=null?9:0);
        hand.rectTransform.anchoredPosition=new Vector2((HudTheme!=null?-82:-99)+handWidth*.5f,-21);
        hand.rectTransform.sizeDelta=new Vector2(handWidth,30);
        hand.alignment=HudTheme!=null?TextAnchor.MiddleLeft:TextAnchor.MiddleCenter;
        if(HudTheme!=null)MenuTypography.Use(hand,MenuTypography.Face.Control);
        PreferenceText.SetSize(hand,16);hand.horizontalOverflow=HorizontalWrapMode.Overflow;
        var row=Node(plate).Find("SeriesWins") as RectTransform;
        if(row==null) {
            row=Rect(Node(plate),"SeriesWins",new Vector2(22,-21),new Vector2(48,20));
            for(int i=0;i<3;i++) {
                var diamond=Panel(row,"Win_"+i,new Vector2(-16+i*16,0),new Vector2(9,9),Paper);
                diamond.localRotation=Quaternion.Euler(0,0,45);
                diamond.GetComponent<Image>().raycastTarget=false;
            }
        }
        row.gameObject.SetActive(showWins);
        row.sizeDelta=new Vector2(slots*16,20);row.anchoredPosition=new Vector2((HudTheme!=null?54:46)-slots*8,-21);
        int wins=Mathf.Clamp(player.seat==1?series?.player1Wins??0:series?.player2Wins??0,0,slots);
        for(int i=0;i<3;i++) {
            var diamond=(RectTransform)row.Find("Win_"+i);diamond.gameObject.SetActive(i<slots);
            diamond.anchoredPosition=new Vector2((i-(slots-1)*.5f)*16,0);
            diamond.GetComponent<Image>().color=i<wins?new Color(.96f,.79f,.43f):new Color(.34f,.41f,.46f);
        }
    }
    private void PresentCommanderTax(string plate,ForgePlayerView player)
    {
        var parent=Node(plate);
        var label=parent.Find("CommanderTax")?.GetComponent<Text>();
        bool show=view?.formatId is "duelcommander" or "brawl" || player.commanderCastCounts is {Length:>0};
        var name=SceneText(plate+"/Name").rectTransform;
        name.anchoredPosition=new Vector2(HudTheme!=null?-12:name.anchoredPosition.x,show?27:18);
        if(HudTheme!=null)name.sizeDelta=new Vector2(140,name.sizeDelta.y);
        if(!show){if(label!=null)label.gameObject.SetActive(false);return;}
        if(label==null){
            label=Label(parent,"CommanderTax","",new Vector2(-25,1),new Vector2(148,22),15,Gold);
            label.alignment=TextAnchor.MiddleLeft;
            label.raycastTarget=false;
            label.resizeTextForBestFit=true;label.resizeTextMinSize=10;label.resizeTextMaxSize=15;
        }
        label.gameObject.SetActive(true);
        if(HudTheme!=null){
            label.rectTransform.anchoredPosition=new Vector2(-12,1);
            label.rectTransform.sizeDelta=new Vector2(140,22);
        }
        var casts=player.commanderCastCounts;
        if(casts==null||casts.Length==0)casts=RecordedCommanderCasts(player);
        label.text=TapPaymentText("主将税 ","Cmdr tax ","統率者税 ")+(casts.Length==0?"—":string.Join(" / ",casts.Select(n=>"+"+(2*n))));
    }

    // Old recordings stored the count on card views. The cast notice captures
    // the source before the increment, and the stack and zones after it.
    private int[] RecordedCommanderCasts(ForgePlayerView player)
    {
        var counts=new SortedDictionary<int,int>();
        void Observe(ForgeCardView card,int increment=0){
            if(card?.commander!=true||card.ownerSeat!=player.seat)return;
            counts.TryGetValue(card.id,out int previous);
            counts[card.id]=Math.Max(previous,card.commanderCastCount+increment);
        }
        foreach(var p in view.players)
            foreach(var zone in new[]{p.commanders,p.hand,p.battlefield,p.exileCards,p.graveyardCards})
                if(zone!=null)foreach(var c in zone)Observe(c);
        foreach(var item in view.stackItems??Array.Empty<ForgeStackItem>())Observe(item.source);
        foreach(var note in view.notices??Array.Empty<ForgeNotice>())
            if(note.kind=="cast"&&note.fromZone=="Command")Observe(note.sourceCard,1);
        return counts.Values.ToArray();
    }
    private void PresentPlayerLife(string plate)
    {
        if(battlefieldNumeralFont==null)battlefieldNumeralFont=Font.CreateDynamicFontFromOSFont(new[]{"Times New Roman","Liberation Serif","Georgia"},24);
        var life=SceneText(plate+"/Life");var preference=life.GetComponent<PreferenceText>();
        if(preference==null)preference=life.gameObject.AddComponent<PreferenceText>();preference.KeepFont=true;preference.enabled=false;
        life.font=battlefieldNumeralFont;life.fontSize=Mathf.RoundToInt(42*GamePreferences.TextScale);
        life.fontStyle=FontStyle.Normal;life.alignment=TextAnchor.MiddleCenter;life.resizeTextForBestFit=false;life.verticalOverflow=VerticalWrapMode.Overflow;
        bool themed=HudTheme!=null;
        life.rectTransform.anchoredPosition=new Vector2(110,themed?9:0);life.rectTransform.sizeDelta=new Vector2(112,themed?57:80);
        if(life.GetComponent<CenteredNumeralMesh>()==null)life.gameObject.AddComponent<CenteredNumeralMesh>();
        var caption=SceneText(plate+"/LifeCaption");
        caption.gameObject.SetActive(themed);
        if(themed){
            caption.text=TapPaymentText("生命","Life","ライフ");
            MenuTypography.Use(caption,MenuTypography.Face.Control);
            PreferenceText.SetSize(caption,12);caption.color=HudMuted;
            caption.alignment=TextAnchor.MiddleCenter;caption.raycastTarget=false;
            caption.rectTransform.anchoredPosition=new Vector2(110,-26);caption.rectTransform.sizeDelta=new Vector2(106,23);
        }
    }
    // Authored paths remain stable for native target and choice bindings.
    private void ApplyDuelLayout()
    {
        ArenaPlace("CornerCluster",new Vector2(1,0),new Vector2(-186,133),new Vector2(300,230));
        ArenaPlace("CornerCluster/ControlRow",new Vector2(.5f,.5f),Vector2.zero,new Vector2(300,160));
        ArenaPlace("CornerCluster/ControlRow/SkipCombatButton",new Vector2(.5f,.5f),new Vector2(48,-20),new Vector2(184,48));
        bool compact = HudTheme != null;
        var center = new Vector2(.5f,.5f);
        ArenaPlace("CornerCluster/ControlRow/PhaseText",center,compact?new Vector2(48,52):new Vector2(0,67),compact?new Vector2(184,38):new Vector2(280,44));
        ArenaTextSize("CornerCluster/ControlRow/PhaseText",22);
        var phase=SceneText("CornerCluster/ControlRow/PhaseText");
        phase.alignment=compact?TextAnchor.MiddleLeft:TextAnchor.MiddleRight;
        var turnSize=compact?new Vector2(184,28):new Vector2(280,36);
        ArenaPlace("CornerCluster/ControlRow/TurnChip",center,compact?new Vector2(48,82):new Vector2(0,105),turnSize);
        Node("CornerCluster/ControlRow/TurnChip").GetComponent<Image>().color=Color.clear;
        var number=SceneText("CornerCluster/ControlRow/TurnChip/TurnNumber");
        number.gameObject.SetActive(compact);
        ArenaPlace("CornerCluster/ControlRow/TurnChip/TurnLabel",center,Vector2.zero,turnSize);
        ArenaTextSize("CornerCluster/ControlRow/TurnChip/TurnLabel",15);
        var turn=SceneText("CornerCluster/ControlRow/TurnChip/TurnLabel");
        turn.alignment=phase.alignment;
        if(compact){
            ArenaPlace("CornerCluster/ControlRow/PhaseText",center,new Vector2(48,49),new Vector2(174,43));
            ArenaPlace("CornerCluster/ControlRow/TurnChip/TurnNumber",center,new Vector2(-49,0),new Vector2(78,26));
            ArenaPlace("CornerCluster/ControlRow/TurnChip/TurnLabel",center,new Vector2(42,0),new Vector2(92,26));
            ArenaTextSize("CornerCluster/ControlRow/TurnChip/TurnNumber",14);
            number.fontStyle=FontStyle.Normal;
            number.alignment=TextAnchor.MiddleLeft;turn.alignment=TextAnchor.MiddleRight;
            FitPhaseLabel(number);
            FitPhaseLabel(phase);
            FitPhaseLabel(turn);
        }
        Node("CornerCluster/ControlRow/RoundText").gameObject.SetActive(false);
        Node("CornerCluster/ControlRow/Divider").gameObject.SetActive(false);
        ArenaPlace("CornerCluster/ManaRow",new Vector2(.5f,.5f),Vector2.zero,new Vector2(300,34));
        Node("PriorityPill").gameObject.SetActive(false);
        StyleAdvanceButton();
    }
    private static void FitPhaseLabel(Text label)
    {
        label.resizeTextForBestFit=true;
        label.resizeTextMinSize=12;
        label.resizeTextMaxSize=label.fontSize;
        label.horizontalOverflow=HorizontalWrapMode.Wrap;
        label.verticalOverflow=VerticalWrapMode.Truncate;
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
        graphic.ApplyTheme(HudTheme,phaseAdvance:true);
        Color actionText=HudTheme!=null?(Color)HudTheme.ActionText:new Color(.13f,.11f,.075f);
        if(HudTheme!=null)((RectTransform)rect.Find("Direction")).anchoredPosition=new Vector2(rect.rect.width*.5f-19,0);
        rect.Find("Direction").GetComponent<StackControlIcon>().color=actionText;
        rect.Find("Direction").GetComponent<StackControlIcon>().SetKind(StackControlIcon.Kind.Right);
        var colors=advanceButton.colors;
        colors.normalColor=colors.selectedColor=HudTheme!=null?new Color(.92f,.92f,.92f):Color.white;
        colors.highlightedColor=Color.white;colors.pressedColor=new Color(.77f,.77f,.77f);
        colors.disabledColor=new Color(.55f,.55f,.55f);colors.fadeDuration=0;
        advanceButton.colors=colors;
        var outline=advanceButton.GetComponent<Outline>();
        if(outline!=null)outline.enabled=false;
        foreach(var text in advanceButton.GetComponentsInChildren<Text>()) {
            text.color=actionText;PreferenceText.SetSize(text,21);text.resizeTextForBestFit=false;
            if(HudTheme!=null)MenuTypography.Use(text,MenuTypography.Face.Control);
            text.rectTransform.anchoredPosition=new Vector2(-14,0);text.rectTransform.sizeDelta=new Vector2(126,44);
            if(HudTheme!=null){
                text.alignment=TextAnchor.MiddleLeft;
                var caption=text.rectTransform;
                caption.anchorMin=Vector2.zero;caption.anchorMax=Vector2.one;
                caption.offsetMin=new Vector2(12,2);caption.offsetMax=new Vector2(-43,-2);
                FitPhaseLabel(text);
            }
            if(view?.request?.kind=="InputPassPriority")
                text.text = view.commanderEvent == OnlineRoomConfig.Loki
                    ? TapPaymentText("让过","Pass","パス") : TapPaymentText("下一步","Next","次へ");
        }
        PresentPhaseTrack();
    }
    private string visiblePhaseKey;
    private float phaseVisibleUntil;
    private void PresentPhaseTrack()
    {
        var row=Node("CornerCluster/ControlRow");
        var track=row.Find("PhaseTrack") as RectTransform;if(track==null)track=Rect(row,"PhaseTrack",new Vector2(56,25),new Vector2(168,5));
        bool themed=HudTheme!=null;
        if(themed)track.anchoredPosition=new Vector2(48,19);
        string phase=view?.phase??"";int current=phase=="MAIN1"?1:phase=="MAIN2"?3:phase.Contains("COMBAT")||phase.Contains("ATTACK")||phase.Contains("BLOCK")||phase.Contains("DAMAGE")?2:phase.Contains("END")||phase.Contains("CLEANUP")?4:0;
        string key=(view?.turnSeat??0)+"/"+(view?.turn??0)+"/"+current;
        bool changed=key!=visiblePhaseKey;
        if(changed){visiblePhaseKey=key;phaseVisibleUntil=Time.unscaledTime+.24f;}
        for(int i=0;i<5;i++){
            var bar=track.Find("Phase_"+i) as RectTransform;if(bar==null)bar=Panel(track,"Phase_"+i,new Vector2(-70+i*35,0),new Vector2(28,5),Paper);
            Color target=i<current?new Color(.71f,.73f,.67f):new Color(.45f,.53f,.54f,.47f);
            if(themed){
                bar.sizeDelta=new Vector2(30,3);
                target=i<current?Color.Lerp(HudInk,HudAccent,.53f):Color.Lerp(HudInk,HudMuted,.29f);
            }
            var fade=bar.GetComponent<ForgePhaseBarFade>()??bar.gameObject.AddComponent<ForgePhaseBarFade>();fade.SetTarget(target);
            bar.GetComponent<Image>().raycastTarget=false;
        }
        var cursor=track.Find("CurrentPhase") as RectTransform;
        var destination=new Vector2(-70+current*35,0);
        var cursorSize=themed?new Vector2(30,4):new Vector2(28,5);
        if(cursor==null) {
            cursor=Panel(track,"CurrentPhase",destination,cursorSize,new Color(.91f,.76f,.48f));
            cursor.GetComponent<Image>().raycastTarget=false;
        } else if(changed)ForgeUiMotion.On(cursor).MoveTo(destination,cursorSize,angle:0,seconds:.24f);
        if(themed){
            cursor.GetComponent<Image>().color=HudAccent;
        }
        cursor.SetAsLastSibling();
    }
    private void PresentPlayerCounters(ForgePlayerView player,bool own)
    {
        var plate=Node(own?"PlayerPlate":"OpponentPlate");
        var row=plate.Find("PlayerCounters") as RectTransform;
        if(row==null)row=Rect(plate,"PlayerCounters",new Vector2(0,own?114:-114),new Vector2(310,30));
        if (IsRedesignLayout) row.anchoredPosition = new Vector2(0, (own ? 1 : -1) * (114 + RedesignPlayerManaReserve));
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
                // Fit normalizes the visible span below. Rasterize at that span
                // first, rather than enlarging a 16px glyph mesh to 28 units.
                glyph.font=poolGlyphFont;preference.SetBaseSize(IsRedesignLayout?32:16);
            } else if(counter.kind=="EXPERIENCE") {
                var icon=Rect(chip,"Symbol",new Vector2(-8,4),new Vector2(28,28)).gameObject.AddComponent<ExperienceCounterIcon>();
                icon.color=Color.white;icon.raycastTarget=false;
                icon.SmoothEdges=IsRedesignLayout;
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
            if (IsRedesignLayout) {
                // The old lower-right numeral covered the shield's outline, so
                // different counts made the same experience symbol look different.
                width=32+Mathf.Max(16,count.preferredWidth)+6;
                chip.sizeDelta=new Vector2(width,34);
                symbol.rectTransform.anchoredPosition=new Vector2(-width*.5f+16,2);
                count.rectTransform.anchoredPosition=new Vector2(-width*.5f+32,-2);
                count.rectTransform.sizeDelta=new Vector2(width-34,30);
            }
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
