using System;
using UnityEngine;
using UnityEngine.UI;

public sealed partial class MainMenuController
{
    private readonly DeckFormatLegality deckLegality = new DeckFormatLegality();
    private bool legalitySubscribed;
    private Dropdown deckFormatDropdown;
    private Dropdown deckBracketDropdown;
    private readonly System.Collections.Generic.List<string> deckFormatOptions = new System.Collections.Generic.List<string>();

    private void CreateDeckFormatControl()
    {
        if(mainDeckCountText==null||deckDropdown==null)return;
        deckFormatDropdown=Instantiate(deckDropdown,mainDeckCountText.transform,false);
        deckFormatDropdown.name="DeckFormatDropdown";
        deckFormatDropdown.onValueChanged=new Dropdown.DropdownEvent();
        var rect=(RectTransform)deckFormatDropdown.transform;
        rect.anchorMin=rect.anchorMax=new Vector2(0,.5f);rect.pivot=new Vector2(0,.5f);
        rect.sizeDelta=new Vector2(96,38); // Width/position follow measured header text at layout time.
        PreferenceText.SetSize(mainDeckCountText,22);
        PreferenceText.SetSize(deckFormatDropdown.captionText,16);
        PreferenceText.SetSize(deckFormatDropdown.itemText,16);
        DropdownChevron.Apply(deckFormatDropdown);
        MenuVisualTheme.StyleArchiveDropdown(deckFormatDropdown);
        var captionFrame=(RectTransform)deckFormatDropdown.captionText.transform.parent;
        captionFrame.anchorMin=Vector2.zero;captionFrame.anchorMax=Vector2.one;
        captionFrame.offsetMin=new Vector2(10,2);captionFrame.offsetMax=new Vector2(-36,-2);
        deckFormatDropdown.captionText.GetComponent<PreferenceText>().Apply();
        deckBracketDropdown=Instantiate(deckFormatDropdown,mainDeckCountText.transform,false);
        deckBracketDropdown.name="CommanderBracketDropdown";
        deckBracketDropdown.onValueChanged=new Dropdown.DropdownEvent();
        deckBracketDropdown.captionText.GetComponent<PreferenceText>().Literal=true;
        deckBracketDropdown.itemText.GetComponent<PreferenceText>().Literal=true;
        deckBracketDropdown.onValueChanged.AddListener(value=>{
            if(currentDeck?.format!="commander" || currentDeck.commanderBracket==value)return;
            currentDeck.commanderBracket=value;
            SaveDeckFormatMetadata();
            RefreshDeckSortControls();
            OnDeckLegalityChanged();
        });
        deckFormatDropdown.template.GetComponentInChildren<Toggle>(true).gameObject.AddComponent<DeckFormatReselect>();
        deckFormatDropdown.onValueChanged.AddListener(index=>{
            if(currentDeck==null||index<0||index>=deckFormatOptions.Count)return;
            string format = deckFormatOptions[index];
            GamePreferences.SetDeckFormat(format);
            OnDeckLegalitySelectionChanged();
            cardDetailsView?.RefreshLegalitySettings();
            RefreshDeckFormatControl();

            var draft = CollectionSearchDraft();
            draft.Format = format == "none" ? 0 : Array.IndexOf(DeckFormatLegality.Keys, format) + 1;
            draft.Status = 0; // Playable, including cards restricted to one copy.
            ApplyAdvancedSearch(draft);
        });
        RefreshDeckFormatControl();
    }

    private void RefreshDeckFormatControl()
    {
        if(deckFormatDropdown==null)return;
        deckFormatOptions.Clear();
        deckFormatOptions.AddRange(DeckFormatLegality.Keys);
        string selected=GamePreferences.DeckFormat;
        if(Array.IndexOf(DeckFormatLegality.Keys,selected)>=0&&!deckFormatOptions.Contains(selected))deckFormatOptions.Add(selected);
        deckFormatDropdown.ClearOptions();
        deckFormatDropdown.AddOptions(deckFormatOptions.ConvertAll(key=>GamePreferences.T(DeckFormatLegality.FormatName(key))));
        deckFormatDropdown.SetValueWithoutNotify(Mathf.Max(0,deckFormatOptions.IndexOf(selected)));
        deckFormatDropdown.RefreshShownValue();
        deckFormatDropdown.interactable=currentDeck!=null;
        bool showBracket=currentDeck?.format=="commander";
        deckBracketDropdown.gameObject.SetActive(showBracket);
        if(showBracket)
        {
            deckBracketDropdown.ClearOptions();
            deckBracketDropdown.AddOptions(DeckCommander.BracketOptions());
            deckBracketDropdown.SetValueWithoutNotify(DeckCommander.EffectiveBracket(currentDeck.format,currentDeck.commanderBracket));
            deckBracketDropdown.RefreshShownValue();
            deckBracketDropdown.interactable=true;
        }
    }

    private static void FitDeckFormatPopup(Dropdown dropdown,float headerWidth,float captionWidth)
    {
        if(dropdown.template==null||dropdown.itemText==null)return;
        var text=dropdown.itemText;
        var settings=text.GetGenerationSettings(Vector2.zero);
        float width=captionWidth;
        foreach(var option in dropdown.options)
            width=Mathf.Max(width,text.cachedTextGeneratorForLayout.GetPreferredWidth(option.text,settings)/text.pixelsPerUnit+56);
        var popup=dropdown.template;
        popup.anchorMin=popup.anchorMax=Vector2.zero;popup.pivot=new Vector2(0,1);
        popup.anchoredPosition=new Vector2(0,-6);
        popup.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal,Mathf.Min(headerWidth,Mathf.Ceil(width)));
    }

    private void ApplyDeckLegalitySelection()
    {
        if (currentDeck == null) return;
        string format = Array.IndexOf(DeckFormatLegality.Keys, currentDeck.format) >= 0
            ? currentDeck.format : "standard";
        GamePreferences.SetDeckFormat(format);
        GamePreferences.SetDeckBanlist(string.IsNullOrWhiteSpace(currentDeck.banlist)
            ? DeckFormatLegality.Current : currentDeck.banlist);
        RefreshDeckFormatControl();
    }

    private void OnDeckLegalitySelectionChanged()
    {
        if (currentDeck == null) return;
        string format = GamePreferences.DeckFormat;
        string banlist = GamePreferences.DeckBanlist;
        if (currentDeck.format != format || currentDeck.banlist != banlist)
        {
            bool enteringCommander=!DeckCommander.IsFormat(currentDeck.format)&&DeckCommander.IsFormat(format);
            currentDeck.format = format;
            currentDeck.banlist = banlist;
            currentDeck.commanderBracket = DeckCommander.EffectiveBracket(format, currentDeck.commanderBracket);
            SaveDeckFormatMetadata();
            if(enteringCommander)StartCoroutine(AdaptCommanderSideboard(currentDeck));
        }
        RefreshDeckGrids();
    }

    private void SaveDeckFormatMetadata()
    {
        if (loadedDeckIndex >= 0 && loadedDeckIndex < decks.Count)
        {
            try
            {
                // Persist only format settings, leaving unsaved card edits untouched.
                var saved = CloneDeck(decks[loadedDeckIndex]);
                saved.format = currentDeck.format;
                saved.banlist = currentDeck.banlist;
                saved.commanderBracket = currentDeck.commanderBracket;
                DeckRepository.SaveReplacing(decks[loadedDeckIndex].name, saved);
                decks[loadedDeckIndex] = saved;
                deckDirectoryRevision=DeckRepository.DirectoryRevision();
            }
            catch (Exception exception)
            {
                MarkDeckDirty();
                SetStatus("赛制设置未能自动保存：" + exception.Message);
            }
        }
        else MarkDeckDirty();
    }

    private void RefreshDeckLegality()
    {
        if (!legalitySubscribed)
        {
            deckLegality.Changed += OnDeckLegalityChanged;
            legalitySubscribed = true;
        }
        OnDeckLegalityChanged();
        if (currentDeck != null && cards.Count > 0)
            StartCoroutine(deckLegality.Refresh(GamePreferences.DeckFormat, GamePreferences.DeckBanlist, cards));
    }

    private void OnDeckLegalityChanged()
    {
        if (deckBuilderGroup != null)
            foreach (var tile in deckBuilderGroup.GetComponentsInChildren<MainMenuCardTile>(true))
                ApplyLegality(tile);
    }

    private void ApplyLegality(MainMenuCardTile tile)
    {
        if (tile == null || tile.Card == null) return;
        bool companion=tile.Zone==MainMenuCardTileZone.Sideboard&&DeckCompanion.Has(currentDeck)
            &&string.Equals(currentDeck.companion.PrintingKey,tile.Card.PrintingKey,StringComparison.OrdinalIgnoreCase);
        string bracket = BracketNotice(tile.Card, out bool bracketWarning);
        string state = deckLegality.Get(tile.Card, GamePreferences.DeckFormat, GamePreferences.DeckBanlist, tile.Zone==MainMenuCardTileZone.Commander, companion);
        string construction = ConstructionCardProblem(tile);
        bool restrictedOverflow = state == "restricted" && TotalCopiesByName(tile.Card.cardName) > 1;
        bool invalid = state == "not_legal" || state == "banned" || restrictedOverflow || construction != null;
        bool collection = tile.Zone == MainMenuCardTileZone.Collection;
        bool atLimit = collection && !invalid && !bracketWarning && state != "unknown" && currentDeck != null &&
            (state == "restricted" ? TotalCopiesByName(tile.Card.cardName) >= 1 : DeckRepository.GetAllowedCopiesToAdd(currentDeck,tile.Card) <= 0);
        string legality = state == "banned" ? GamePreferences.T("禁用")
            : state == "not_legal" ? DeckCompanion.Text("不合法","Not legal","使用不可")
            : construction ?? (atLimit ? DeckCompanion.Text("已达上限","Limit reached","上限")
            : state == "restricted" ? GamePreferences.T("限1")
            : collection && state == "unknown" ? DeckCompanion.Text("判定中","Checking","確認中") : null);

        Color warning = new Color32(159,48,65,255);
        float below = SetTileNotice(tile,"LegalityBadge",legality,invalid ? warning : new Color32(64,74,91,255),3);
        SetTileNotice(tile,"BracketBadge",bracket,bracketWarning ? warning : new Color32(37,85,92,255),below);
    }

    private static float SetTileNotice(MainMenuCardTile tile,string name,string message,Color color,float top)
    {
        var badge = tile.transform.Find(name) as RectTransform;
        if (string.IsNullOrEmpty(message)) {
            if (badge != null) badge.gameObject.SetActive(false);
            return top;
        }
        if (badge == null) {
            var image = AddImage(tile.transform,name,color);
            image.raycastTarget=false;
            badge=image.rectTransform;
            badge.anchorMin=badge.anchorMax=badge.pivot=new Vector2(0,1);
            var caption=AddText(badge,"Label","",18,Color.white,TextAnchor.MiddleCenter);
            caption.GetComponent<PreferenceText>().Literal=true;
            caption.raycastTarget=false;
            caption.fontStyle=FontStyle.Bold;
            caption.horizontalOverflow=HorizontalWrapMode.Overflow;
            Stretch(caption.rectTransform,0,0,0,0);
        }
        badge.gameObject.SetActive(true);
        badge.GetComponent<Image>().color=color;
        var text=badge.GetComponentInChildren<Text>();
        text.text=message;
        float maxWidth=Mathf.Max(32,((RectTransform)tile.transform).rect.width-6);
        float width=Mathf.Ceil(text.preferredWidth)+12;
        text.resizeTextForBestFit=width>maxWidth;
        text.resizeTextMinSize=12;
        text.resizeTextMaxSize=text.fontSize;
        float height=Mathf.Max(26,text.fontSize+8);
        badge.sizeDelta=new Vector2(Mathf.Min(width,maxWidth),height);
        badge.anchoredPosition=new Vector2(3,-top);
        badge.SetAsLastSibling();
        return top+height+2;
    }

    private int TotalCopiesByName(string name)
    {
        int count = 0;
        if (currentDeck == null) return count;
        foreach(var entry in System.Linq.Enumerable.Concat(System.Linq.Enumerable.Concat(currentDeck.cards,currentDeck.sideboard),currentDeck.commanders))
            if(entry!=null&&string.Equals(DeckRepository.ResolveCard(entry,cards)?.cardName,name,StringComparison.OrdinalIgnoreCase))count+=Math.Max(0,entry.count);
        return count;
    }

}
