using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public sealed partial class MainMenuController
{
    // Official Game Changers, including every printing of the named card.
    // https://magic.wizards.com/en/formats/commander#gamechangers
    private static readonly HashSet<string> GameChangers = new(StringComparer.OrdinalIgnoreCase)
    {
        "Ad Nauseam", "Ancient Tomb", "Aura Shards", "Biorhythm", "Bolas's Citadel",
        "Braids, Cabal Minion", "Chrome Mox", "Coalition Victory", "Consecrated Sphinx",
        "Crop Rotation", "Cyclonic Rift", "Demonic Tutor", "Drannith Magistrate",
        "Enlightened Tutor", "Farewell", "Field of the Dead", "Fierce Guardianship",
        "Force of Will", "Gaea's Cradle", "Gamble", "Gifts Ungiven", "Glacial Chasm",
        "Grand Arbiter Augustin IV", "Grim Monolith", "Humility", "Imperial Seal",
        "Intuition", "Jeska's Will", "Lion's Eye Diamond", "Mana Vault",
        "Mishra's Workshop", "Mox Diamond", "Mystical Tutor", "Narset, Parter of Veils",
        "Natural Order", "Necropotence", "Notion Thief", "Opposition Agent", "Orcish Bowmasters",
        "Panoptic Mirror", "Rhystic Study", "Seedborn Muse", "Serra's Sanctum",
        "Smothering Tithe", "Survival of the Fittest", "Teferi's Protection", "Tergrid, God of Fright",
        "Thassa's Oracle", "The One Ring", "The Tabernacle at Pendrell Vale",
        "Underworld Breach", "Vampiric Tutor", "Worldly Tutor"
    };

    private readonly Dictionary<string,int> deckGameChangers = new(StringComparer.OrdinalIgnoreCase);
    private int gameChangerCount;

    private static string GameChangerName(string name)
    {
        if (string.IsNullOrEmpty(name)) return null;
        int separator = name.IndexOf(" // ", StringComparison.Ordinal);
        if (separator >= 0) name = name.Substring(0,separator);
        return GameChangers.Contains(name) ? name : null;
    }

    private void RefreshBracketCounts()
    {
        deckGameChangers.Clear();
        gameChangerCount = 0;
        if (currentDeck?.format != "commander") return;
        foreach (var entry in constructionNames) Count(entry.Key,entry.Value);
        if (DeckCompanion.Has(currentDeck)) Count(DeckRepository.ResolveCard(currentDeck.companion,cards)?.cardName,1);

        void Count(string name,int copies)
        {
            string key = GameChangerName(name);
            if (key == null) return;
            deckGameChangers.TryGetValue(key,out int existing);
            deckGameChangers[key] = existing + copies;
            gameChangerCount += copies;
        }
    }

    private static int GameChangerLimit(int bracket) => bracket switch { 1 or 2 => 0, 3 => 3, _ => -1 };

    private string BracketListCaption()
    {
        string title = DeckCompanion.Text("变局牌表","Game Changers","ゲームチェンジャー");
        int limit = GameChangerLimit(currentDeck.commanderBracket);
        return limit >= 0 ? $"{title}  {gameChangerCount}/{limit}" : title;
    }

    private string BracketNotice(CardDefinition card, out bool warning)
    {
        int bracket = DeckCommander.EffectiveBracket(currentDeck?.format,currentDeck?.commanderBracket ?? 0);
        string key = bracket == 0 ? null : GameChangerName(card?.cardName);
        warning = false;
        if (key == null) return null;
        int limit = GameChangerLimit(bracket);
        warning = limit == 0 || limit == 3 && gameChangerCount >= limit &&
            (!deckGameChangers.ContainsKey(key) || gameChangerCount > limit);
        if (limit == 0) return DeckCompanion.Text("分级禁用","Bracket ban","使用不可");
        if (warning) return deckGameChangers.ContainsKey(key)
            ? DeckCompanion.Text("变局超额","Over limit","上限超過")
            : DeckCompanion.Text("变局已满","At limit","上限到達");
        return DeckCompanion.Text("变局牌","Changer","指定カード");
    }

    private void ShowBracketCatalog()
    {
        if (currentDeck?.format != "commander") return;
        var printings = new Dictionary<string,CardDefinition>(StringComparer.OrdinalIgnoreCase);
        foreach (var card in cards) {
            if (card == null || card.forgeAmbiguous || card.customContent) continue;
            string key = GameChangerName(card.cardName);
            if (key != null) printings.TryAdd(key,card);
        }
        // Keep an entry even when this installation does not have that card.
        var names = GameChangers.OrderByDescending(deckGameChangers.ContainsKey)
            .ThenBy(name=>name,StringComparer.OrdinalIgnoreCase).ToArray();
        RenderBracketCatalog(names,printings,0);
    }

    private void RenderBracketCatalog(string[] names,Dictionary<string,CardDefinition> printings,int page,int detailIndex=-1)
    {
        const int pageSize = 6;
        int pages = (names.Length+pageSize-1)/pageSize;
        var panel = PickerPanel("CommanderBracketCatalog",DeckCompanion.Text("指挥官 · 变局牌表","Commander · Game Changers","統率者 · ゲームチェンジャー"),new Vector2(1240,694));
        int bracket = currentDeck.commanderBracket;
        int limit = GameChangerLimit(bracket);
        string rule = limit == 0 ? DeckCompanion.Text("不可使用变局牌","No Game Changers allowed","使用不可")
            : limit == 3 ? DeckCompanion.Text("最多 3 张变局牌","Up to 3 Game Changers","最大3枚")
            : bracket == 0 ? DeckCompanion.Text("未设分级限制","No bracket selected","ブラケット指定なし")
            : DeckCompanion.Text("变局牌数量不限","Unlimited Game Changers","枚数制限なし");
        string counts = string.Format(DeckCompanion.Text("牌表 {0} 张  ·  本卡组 {1} 张","{0} listed  ·  {1} in deck","一覧 {0} 枚  ·  デッキ内 {1} 枚"),names.Length,gameChangerCount);
        var summary = PickerText(panel,"Rule",DeckCommander.BracketLabel(bracket)+"   ·   "+rule,21,MenuVisualTheme.ArchivePaper,TextAnchor.MiddleLeft);
        Place(summary.rectTransform,new Vector2(-210,223),new Vector2(746,52));
        var total = PickerText(panel,"Counts",counts,19,MenuVisualTheme.Stone,TextAnchor.MiddleRight);
        Place(total.rectTransform,new Vector2(390,223),new Vector2(380,52));
        void AddButton(string id,CardDefinition card,Vector2 position,Vector2 size) {
            bool canAdd=DeckRepository.GetAllowedCopiesToAdd(currentDeck,card)>0;
            string caption=canAdd ? DeckCompanion.Text("加入主牌","Add to deck","メインに追加") :
                card==null ? DeckCompanion.Text("未收录","Unavailable","未収録") :
                deckGameChangers.ContainsKey(GameChangerName(card.cardName))
                    ? DeckCompanion.Text("已加入","In deck","追加済み")
                    : DeckCompanion.Text("已达上限","At limit","上限到達");
            PickerButton(panel,id,caption,position,size,()=>{
                if(DeckRepository.AddCard(currentDeck,card,1)<=0)return;
                MarkDeckDirty();
                RefreshDeckGrids();
                RenderBracketCatalog(names,printings,page,detailIndex);
            },canAdd);
        }
        if(detailIndex>=0) {
            var card=printings[names[detailIndex]];
            var art=PickerImage(panel,"InspectArt",Color.white);
            Place(art.rectTransform,new Vector2(-374,-30),new Vector2(298,416));
            art.preserveAspect=true;
            PickerArt(art,card);
            var title=PickerText(panel,"InspectName",GamePreferences.CardName(card),28,MenuVisualTheme.ArchivePaper,TextAnchor.MiddleLeft);
            Place(title.rectTransform,new Vector2(188,147),new Vector2(740,54));
            var type=PickerRuleText(panel,"InspectType",GamePreferences.CardType(card)+"   "+card.manaCost,20,MenuVisualTheme.MainAccent);
            type.alignment=TMPro.TextAlignmentOptions.MidlineLeft;
            Place(type.rectTransform,new Vector2(188,97),new Vector2(740,40));
            ShowBracketRules(panel,card);
            PickerButton(panel,"BackToList",DeckCompanion.Text("返回牌表","Back to list","一覧に戻る"),new Vector2(-489,-293),new Vector2(185,46),()=>RenderBracketCatalog(names,printings,page));
            AddButton("InspectAdd",card,new Vector2(475,-293),new Vector2(206,46));
            FinishPicker(panel);
            return;
        }
        for (int i=0;i<pageSize && page*pageSize+i<names.Length;i++) {
            string name = names[page*pageSize+i];
            printings.TryGetValue(name,out var card);
            Vector2 slot = PickerCardSlot(panel,i);
            var art = PickerImage(panel,"CardArt_"+i,Color.white);
            Place(art.rectTransform,slot+new Vector2(-121,0),new Vector2(118,165));
            art.preserveAspect=true;
            PickerArt(art,card);
            var title = PickerText(panel,"CardName_"+i,card != null ? GamePreferences.CardName(card) : name,20,MenuVisualTheme.ArchivePaper,TextAnchor.MiddleLeft);
            Place(title.rectTransform,slot+new Vector2(65,47),new Vector2(220,72));
            deckGameChangers.TryGetValue(name,out int copies);
            var status = PickerText(panel,"Copies_"+i,copies>0 ? string.Format(DeckCompanion.Text("卡组中 ×{0}","In deck ×{0}","デッキ内 ×{0}"),copies) : "",16,MenuVisualTheme.MainAccent,TextAnchor.MiddleLeft);
            Place(status.rectTransform,slot+new Vector2(65,-9),new Vector2(220,30));
            int index=page*pageSize+i;
            PickerButton(panel,"Inspect_"+i,DeckCompanion.Text("详情","Details","詳細"),slot+new Vector2(-6,-56),new Vector2(84,42),
                ()=>RenderBracketCatalog(names,printings,page,index),card!=null);
            AddButton("Add_"+i,card,slot+new Vector2(111,-56),new Vector2(134,42));
        }
        var number = PickerText(panel,"Page",$"{page+1} / {pages}",19,MenuVisualTheme.ArchivePaper,TextAnchor.MiddleCenter);
        Place(number.rectTransform,new Vector2(0,-293),new Vector2(90,42));
        PickerButton(panel,"Previous","‹",new Vector2(-80,-293),new Vector2(52,42),()=>RenderBracketCatalog(names,printings,page-1),page>0);
        PickerButton(panel,"Next","›",new Vector2(80,-293),new Vector2(52,42),()=>RenderBracketCatalog(names,printings,page+1),page+1<pages);
        PickerButton(panel,"Close","完成",new Vector2(512,-293),new Vector2(144,44),CloseDeckExchange);
        FinishPicker(panel);
    }

    private void ShowBracketRules(RectTransform panel,CardDefinition card)
    {
        var well=PickerImage(panel,"InspectRules",MenuVisualTheme.Recess);
        Place(well.rectTransform,new Vector2(188,-87),new Vector2(740,310));
        var scroll=well.GetComponent<ScrollRect>();
        if(scroll==null) {
            well.raycastTarget=true;
            well.gameObject.AddComponent<RectMask2D>();
            scroll=well.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal=false;
            scroll.scrollSensitivity=40;
            scroll.movementType=ScrollRect.MovementType.Clamped;
            scroll.viewport=well.rectTransform;
            var label=CardSymbolText.Create(well.transform,"Rules",22,MenuVisualTheme.ArchivePaper);
            PreferenceText.SetSize(label,22);
            label.GetComponent<PreferenceText>().Literal=true;
            label.alignment=TMPro.TextAlignmentOptions.TopLeft;
            label.margin=new Vector4(14,12,14,12);
            scroll.content=label.rectTransform;
            scroll.content.anchorMin=new Vector2(0,1);
            scroll.content.anchorMax=Vector2.one;
            scroll.content.pivot=new Vector2(.5f,1);
        }
        string rules=GamePreferences.CardRules(card);
        if(card.features!=null)foreach(var feature in card.features) {
            if(feature==null || feature.faceIndex==0 && feature.featureIndex==0)continue;
            rules+="\n\n"+GamePreferences.FeatureName(feature)+"\n"+GamePreferences.FeatureRules(feature);
        }
        var text=scroll.content.GetComponent<TMPro.TextMeshProUGUI>();
        text.text=CardSymbolText.Format(rules);
        float height=Mathf.Max(310,text.GetPreferredValues(text.text,712,Mathf.Infinity).y+28);
        scroll.content.sizeDelta=new Vector2(0,height);
        scroll.StopMovement();
        scroll.verticalNormalizedPosition=1;
    }
}
