using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

public sealed partial class MainMenuController
{
    DeckTextExchange deckExchange;
    GameObject exchangeDialog;
    int exchangeGeneration;
    Coroutine importDebounce;
    Button deleteDeckButton;
    readonly System.Collections.Generic.Queue<(Image image,CardDefinition card)> pickerArtPending=new();
    readonly System.Collections.Generic.Dictionary<Image,CardDefinition> pickerArtRequests=new();
    readonly System.Collections.Generic.HashSet<Transform> pickerVisible=new();
    Coroutine pickerArtLoader;

    private void CreateDeckExchangeToolbar()
    {
        var parent=deckDropdown.transform.parent;
        reloadButton.gameObject.SetActive(false);
        var heading = AddText(parent,"DeckEditorHeading","卡组编辑",21,MenuVisualTheme.ArchivePaper,TextAnchor.MiddleLeft);
        MenuTypography.Use(heading,MenuTypography.Face.Heading);
        heading.horizontalOverflow = HorizontalWrapMode.Overflow;
        var row=new GameObject("DeckToolbar",typeof(RectTransform),typeof(HorizontalLayoutGroup));row.transform.SetParent(parent,false);
        var rect=(RectTransform)row.transform;rect.anchorMin=new Vector2(0,1);rect.anchorMax=Vector2.one;
        rect.offsetMin=new Vector2(26,-65);rect.offsetMax=new Vector2(-26,-16);
        var layout=row.GetComponent<HorizontalLayoutGroup>();layout.spacing=10;layout.childAlignment=TextAnchor.MiddleLeft;
        layout.childControlWidth=layout.childControlHeight=true;layout.childForceExpandHeight=true;layout.childForceExpandWidth=false;
        void Add(Component control,float width=74)
        {
            control.transform.SetParent(row.transform,false);
            var size=control.gameObject.AddComponent<LayoutElement>();
            size.minWidth=size.preferredWidth=width;
            size.flexibleWidth=0;
            if(control is not Button button)return;
            var label=button.GetComponentInChildren<Text>(true);
            PreferenceText.SetSize(label,18);
            MenuVisualTheme.StyleDeckTool(button,button==saveDeckButton);
        }
        Button AddAction(string name,string caption,Action action)
        {
            var button=CreateDialogButton(row.transform,name,caption,Vector2.zero,MenuVisualTheme.Panel,action);
            Add(button);
            return button;
        }

        Add(backToTitleButton);
        Add(heading,0);
        Add(deckDropdown,220);
        Add(deckNameInput,220);
        AddAction("RenameDeckButton","重命名",BeginRenameDeck);
        deleteDeckButton=AddAction("DeleteDeckButton","删除",ShowDeleteDeck);
        deleteDeckButton.GetComponentInChildren<Text>(true).color=new Color32(255,203,211,255);

        var spacer=new GameObject("ToolbarSpacer",typeof(RectTransform),typeof(LayoutElement));
        spacer.transform.SetParent(row.transform,false);
        spacer.GetComponent<LayoutElement>().flexibleWidth=1;
        Add(newDeckButton);
        Add(saveDeckButton);
        AddAction("SaveAsDeckButton","另存",ShowSaveAsDeck);
        AddAction("ImportDeckButton","导入",RequestImportDeck);
        AddAction("ExportDeckButton","导出",ShowExportDeck);
        var deckCaption=deckDropdown.captionText;
        deckCaption.GetComponent<PreferenceText>().FitSingleLineHeight=false;
        MenuVisualTheme.StyleArchiveControl(deckDropdown);
        MenuTypography.Use(deckCaption,MenuTypography.Face.Control);
        deckCaption.fontStyle=FontStyle.Normal;
        PreferenceText.SetSize(deckCaption,18);
        deckCaption.resizeTextForBestFit=false;
        deckCaption.horizontalOverflow=HorizontalWrapMode.Overflow;
        deckCaption.verticalOverflow=VerticalWrapMode.Overflow;
        row.AddComponent<DeckToolbarAutoSize>().Configure();
        LayoutCollectionSearch();
        UpdateDeleteDeckButton();
    }
    void LayoutCollectionSearch()
    {
        RectTransform Row(string name, float inset, float height)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(HorizontalLayoutGroup));
            var rect = (RectTransform)go.transform;
            rect.SetParent(collectionArea, false);
            rect.anchorMin = new Vector2(.024f, 1);
            rect.anchorMax = new Vector2(.976f, 1);
            rect.offsetMin = new Vector2(0,-inset-height); rect.offsetMax = new Vector2(0,-inset);
            var layout = go.GetComponent<HorizontalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.spacing = 8;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            return rect;
        }
        void Add(RectTransform row, Component control, float width, bool expand)
        {
            control.transform.SetParent(row, false);
            var size = control.gameObject.AddComponent<LayoutElement>();
            size.minWidth = size.preferredWidth = width;
            size.flexibleWidth = expand ? 1 : 0;
            if (control is Button button)
            {
                var label=button.GetComponentInChildren<Text>(true);
                PreferenceText.SetSize(label,16);
                MenuVisualTheme.StyleDeckTool(button);
            }
        }

        var host = collectionSearchInput.transform.parent;
        var search = host.Find("CollectionSearchButton").GetComponent<Button>();
        var reset = host.Find("CollectionSearchResetButton").GetComponent<Button>();
        var title = Row("CollectionHeading", 8, 38);
        Add(title,collectionCountText,48,true);
        collectionCountText.resizeTextForBestFit = true;
        collectionCountText.resizeTextMinSize = 14;
        collectionCountText.resizeTextMaxSize = Mathf.RoundToInt(22*GamePreferences.TextScale);
        Add(title,advancedSearchButton,98,false);
        title.gameObject.AddComponent<DeckToolbarAutoSize>().Configure();
        var query = Row("CollectionQuery", 54, 40);
        Add(query,collectionSearchInput,72,true);
        Add(query,search,58,false);
        Add(query,reset,58,false);
        query.gameObject.AddComponent<DeckToolbarAutoSize>().Configure();
        RefreshAdvancedSearchButton();
    }


    void UpdateDeleteDeckButton()
    {
        if(deleteDeckButton!=null)deleteDeckButton.interactable=loadedDeckIndex>=0 && loadedDeckIndex<decks.Count;
    }
    void ShowDeleteDeck()
    {
        if(loadedDeckIndex<0 || loadedDeckIndex>=decks.Count)return;
        string savedName=decks[loadedDeckIndex].name;
        var panel=ExchangePanel("删除卡组",new Vector2(620,290));
        var message=AddText(panel,"Message",string.Format(GamePreferences.T("确定删除「{0}」？\n此卡组未保存的修改也会丢失。"),savedName),18,MenuVisualTheme.Paper,TextAnchor.MiddleCenter);
        Place(message.rectTransform,new Vector2(0,27),new Vector2(560,92));
        var error=AddText(panel,"Error","",16,new Color(1,.55f,.5f),TextAnchor.MiddleCenter);
        Place(error.rectTransform,new Vector2(0,-39),new Vector2(560,38));
        CreateDialogButton(panel,"ConfirmDelete","删除",new Vector2(128,-102),new Color(.62f,.16f,.19f),()=>{
            try
            {
                if(!DeckRepository.DeleteByName(savedName)){error.text="找不到该卡组文件。请先重载。";return;}
                decks.Clear();decks.AddRange(DeckRepository.LoadAll());
                loadedDeckIndex=decks.Count>0?Mathf.Clamp(loadedDeckIndex,0,decks.Count-1):-1;
                currentDeck=loadedDeckIndex>=0?CloneDeck(decks[loadedDeckIndex]):new DeckDefinition{name="New Deck"};
                ApplyDeckLegalitySelection();
                hasUnsavedDeckChanges=false;
                deckNameInput.SetTextWithoutNotify(currentDeck.name);
                deckNameInput.gameObject.SetActive(false);deckDropdown.gameObject.SetActive(true);
                RefreshDeckDropdown(currentDeck.name);ClearHighlightedTile();RefreshDeckGrids();RefreshSelectedCardInfo();
                UpdateDeleteDeckButton();CloseDeckExchange();SetStatus("已删除卡组："+savedName);
            }
            catch(Exception e){error.text=e.Message;}
        });
        CreateDialogButton(panel,"Cancel","取消",new Vector2(-128,-102),MenuVisualTheme.Raised,CloseDeckExchange);
    }
    void CloseDeckExchange()
    {
        if (exchangeDialog != null && exchangeDialog.name == "CompanionDeck") companionDialogOpen = false;
        if (exchangeDialog != null && exchangeDialog.name == "AuxiliaryDeck") auxiliaryDialogOpen = false;
        exchangeGeneration++;
        pickerArtPending.Clear();
        if(pickerArtLoader!=null){StopCoroutine(pickerArtLoader);pickerArtLoader=null;}
        if(importDebounce!=null){StopCoroutine(importDebounce);importDebounce=null;}
        if(exchangeDialog!=null){exchangeDialog.SetActive(false);Destroy(exchangeDialog);exchangeDialog=null;}
    }
    RectTransform ExchangePanel(string title,Vector2 size)
    {
        CloseDeckExchange();
        exchangeDialog=new GameObject("DeckExchange",typeof(RectTransform),typeof(UnityEngine.UI.Image));exchangeDialog.transform.SetParent(GetRootCanvas().transform,false);
        Stretch((RectTransform)exchangeDialog.transform,0,0,0,0);exchangeDialog.GetComponent<UnityEngine.UI.Image>().color=new Color(0,0,0,.78f);
        var panel=new GameObject("Panel",typeof(RectTransform),typeof(UnityEngine.UI.Image));panel.transform.SetParent(exchangeDialog.transform,false);
        Place((RectTransform)panel.transform,Vector2.zero,size);panel.GetComponent<UnityEngine.UI.Image>().color=MenuVisualTheme.ArchivePanel;
        MenuTypography.Scope(panel.transform);
        var heading=AddText(panel.transform,"Heading",title,25,MenuVisualTheme.ArchivePaper,TextAnchor.MiddleLeft);
        MenuTypography.Use(heading,MenuTypography.Face.Heading);
        Place(heading.rectTransform,new Vector2(0,size.y*.5f-38),new Vector2(size.x-60,40));
        return (RectTransform)panel.transform;
    }
    // Pickers retain their modal surface and controls across tab/page changes.
    RectTransform PickerPanel(string name,string title,Vector2 size)
    {
        pickerArtPending.Clear();pickerVisible.Clear();
        if(exchangeDialog==null || exchangeDialog.name!=name)
        {
            var created=ExchangePanel(title,size);
            exchangeDialog.name=name;
            StyleDeckDialog(created);
            return created;
        }
        var panel=(RectTransform)exchangeDialog.transform.Find("Panel");
        panel.sizeDelta=size;
        panel.Find("Heading").GetComponent<Text>().text=title;
        return panel;
    }
    private void StyleDeckDialog(RectTransform panel)
    {
        exchangeDialog.GetComponent<Image>().color=MenuVisualTheme.DialogShade;
        var heading=panel.Find("Heading").GetComponent<Text>();
        MenuVisualTheme.StyleDeckDialogSurface(panel, heading);
        PreferenceText.SetSize(heading,31);
        StartCoroutine(FitDeckDialog(panel,exchangeDialog));
    }
    void FinishPicker(Transform panel)
    {
        foreach(Transform child in panel)if(child.name!="Heading" && child.name!="DialogLintel" && child.name!="DialogFrame")child.gameObject.SetActive(pickerVisible.Contains(child));
    }
    private Vector2 PickerCardSlot(RectTransform panel, int index)
    {
        var center=new Vector2((index%3-1)*397,index<3?49:-152);
        var backing=PickerImage(panel,"Slot_"+index,MenuVisualTheme.ArchivePanel);
        Place(backing.rectTransform,center,new Vector2(384,187));
        backing.transform.SetAsFirstSibling();
        return center;
    }
    Text PickerText(Transform panel,string name,string value,int size,Color color,TextAnchor alignment)
    {
        var old=panel.Find(name);var text=old!=null?old.GetComponent<Text>():AddText(panel,name,value,size,color,alignment);
        pickerVisible.Add(text.transform);text.gameObject.SetActive(true);text.text=value;text.color=color;text.alignment=alignment;
        text.resizeTextForBestFit=false;PreferenceText.SetSize(text,size);return text;
    }
    TMPro.TextMeshProUGUI PickerRuleText(Transform panel,string name,string value,int size,Color color)
    {
        var old=panel.Find(name);
        var text=old!=null?old.GetComponent<TMPro.TextMeshProUGUI>():CardSymbolText.Create(panel,name,size,color);
        pickerVisible.Add(text.transform);text.gameObject.SetActive(true);
        text.text=CardSymbolText.Format(value);text.color=color;text.alignment=TMPro.TextAlignmentOptions.Center;
        text.enableAutoSizing=false;PreferenceText.SetSize(text,size);
        text.GetComponent<PreferenceText>().Literal=true;
        return text;
    }
    Image PickerImage(Transform panel,string name,Color color)
    {
        var old=panel.Find(name);var image=old!=null?old.GetComponent<Image>():AddImage(panel,name,color);
        pickerVisible.Add(image.transform);image.gameObject.SetActive(true);image.color=color;return image;
    }
    void PickerArt(Image image,CardDefinition card)
    {
        pickerArtRequests.Remove(image);
        if(card!=null && CardImageLibrary.TryReuseFullCard(card,out var cached)){image.sprite=cached;image.color=Color.white;return;}
        else{image.sprite=null;image.color=MenuVisualTheme.Ink;}
        if(card==null)return;
        pickerArtRequests[image]=card;
        pickerArtPending.Enqueue((image,card));
        if(pickerArtLoader==null)pickerArtLoader=StartCoroutine(LoadPickerArt());
    }
    IEnumerator LoadPickerArt()
    {
        yield return null;
        while(pickerArtPending.Count>0){
            var item=pickerArtPending.Dequeue();
            // Obsolete requests never delay the current page or overwrite a cache hit.
            if(ReferenceEquals(item.image,null)||!pickerArtRequests.TryGetValue(item.image,out var wanted)||!ReferenceEquals(wanted,item.card))continue;
            if(item.image==null||!item.image.gameObject.activeInHierarchy){pickerArtRequests.Remove(item.image);continue;}
            item.image.sprite=LoadCardSprite(item.card);item.image.color=Color.white;
            pickerArtRequests.Remove(item.image);
            if(item.image.sprite!=null&&item.image.transform.parent.GetComponent<MainMenuCardTile>()!=null){
                var fallback=item.image.transform.parent.Find("FallbackText");if(fallback!=null)fallback.gameObject.SetActive(false);
            }
            // Decode at most one current, uncached portrait per frame.
            yield return null;
        }
        pickerArtLoader=null;
    }
    Button PickerButton(Transform panel,string name,string label,Vector2 position,Vector2 size,Action action,bool interactable=true)
    {
        var old=panel.Find(name);var button=old!=null?old.GetComponent<Button>():AuxiliaryButton(panel,name,label,position,size,action);
        Place((RectTransform)button.transform,position,size);
        pickerVisible.Add(button.transform);button.gameObject.SetActive(true);
        var colors=button.colors;colors.fadeDuration=0;button.colors=colors;
        button.interactable=interactable;button.onClick.RemoveAllListeners();button.onClick.AddListener(()=>action());
        var text=button.GetComponentInChildren<Text>();text.text=GamePreferences.T(label);PreferenceText.SetSize(text,20);
        MenuVisualTheme.StyleDeckTool(button,name=="Close"||name=="CloseCompanion");
        DisabledButtonVisual.Bind(button);
        text.canvasRenderer.SetAlpha(interactable?1:.38f);
        return button;
    }
    InputField ExchangeInput(Transform parent,string name,Vector2 pos,Vector2 size)
    {
        var go=new GameObject(name,typeof(RectTransform),typeof(UnityEngine.UI.Image),typeof(UnityEngine.UI.InputField));go.transform.SetParent(parent,false);Place((RectTransform)go.transform,pos,size);
        var input=go.GetComponent<UnityEngine.UI.InputField>();input.targetGraphic=go.GetComponent<UnityEngine.UI.Image>();input.targetGraphic.color=MenuVisualTheme.Ink;
        input.textComponent=AddText(go.transform,"Text","",18,MenuVisualTheme.Paper,TextAnchor.MiddleLeft);
        input.textComponent.supportRichText=false;Stretch(input.textComponent.rectTransform,12,10,12,10);
        return input;
    }
    TMPro.TMP_InputField ExchangeScrollableInput(Transform parent,string name,Vector2 pos,Vector2 size,bool readOnly=false)
    {
        var go=new GameObject(name,typeof(RectTransform),typeof(Image));
        // TMP creates its caret and registers its text viewport on the first OnEnable.
        go.SetActive(false);
        go.transform.SetParent(parent,false);Place((RectTransform)go.transform,pos,size);
        var input=go.AddComponent<TMPro.TMP_InputField>();
        var viewport=new GameObject("Text Area",typeof(RectTransform),typeof(RectMask2D));
        viewport.transform.SetParent(go.transform,false);
        Stretch((RectTransform)viewport.transform,12,38,10,10);
        var textObject=new GameObject("Text",typeof(RectTransform),typeof(TMPro.TextMeshProUGUI));
        textObject.transform.SetParent(viewport.transform,false);
        var label=textObject.GetComponent<TMPro.TextMeshProUGUI>();
        label.text="";label.color=MenuVisualTheme.ArchivePaper;label.alignment=TMPro.TextAlignmentOptions.TopLeft;
        label.enableAutoSizing=false;label.textWrappingMode=TMPro.TextWrappingModes.Normal;
        label.richText=false;label.raycastTarget=false;
        // The import dialog can open before the localized font finishes loading.
        // Prefer the loaded CJK asset so pasted names and error lines stay legible.
        var importFont=GamePreferences.TmpFont;
        if(importFont==null)
        {
            string fallback=GamePreferences.Language=="ja-JP"?"NotoSansCJKjp-SDF":"NotoSansCJKsc-SDF";
            importFont=Resources.FindObjectsOfTypeAll<TMPro.TMP_FontAsset>().FirstOrDefault(font=>font.name==fallback);
#if UNITY_EDITOR
            // Editor play-mode previews can skip GamePreferences initialization.
            if(importFont==null)importFont=UnityEditor.AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>("Assets/Fonts/"+fallback+".asset");
#endif
        }
        if(importFont!=null)label.font=importFont;
        Stretch(label.rectTransform,0,0,0,0);
        input.textViewport=(RectTransform)viewport.transform;
        input.textComponent=label;
        input.lineType=TMPro.TMP_InputField.LineType.MultiLineNewline;
        input.richText=false;
        input.readOnly=readOnly;
        input.onFocusSelectAll=false;
        input.scrollSensitivity=2f;input.characterLimit=0;
        PreferenceText.SetSize(label,18);
        var binding=label.GetComponent<PreferenceText>();
        binding.Literal=true;
        binding.SetMenuFace(MenuTypography.Face.Control);
        input.fontAsset=label.font;
        MenuVisualTheme.StyleArchiveControl(input,recessed:true);
        input.customCaretColor=true;
        input.caretColor=MenuVisualTheme.Silver;
        input.caretWidth=3;
        input.caretBlinkRate=.85f;
        input.selectionColor=new Color32(97,158,194,165);
        var track=new GameObject("VerticalScrollbar",typeof(RectTransform),typeof(UnityEngine.UI.Image),typeof(UnityEngine.UI.Scrollbar));
        track.transform.SetParent(go.transform,false);
        var trackRect=(RectTransform)track.transform;
        trackRect.anchorMin=new Vector2(1,0);trackRect.anchorMax=Vector2.one;
        trackRect.offsetMin=new Vector2(-28,10);trackRect.offsetMax=new Vector2(-8,-10);
        var handle=new GameObject("Handle",typeof(RectTransform),typeof(UnityEngine.UI.Image));
        handle.transform.SetParent(track.transform,false);
        Stretch((RectTransform)handle.transform,3,3,3,3);
        var scrollbar=track.GetComponent<UnityEngine.UI.Scrollbar>();
        scrollbar.handleRect=(RectTransform)handle.transform;
        scrollbar.direction=UnityEngine.UI.Scrollbar.Direction.TopToBottom;
        MenuVisualTheme.StyleDeckScrollThumb(scrollbar);
        input.verticalScrollbar=scrollbar;
        go.SetActive(true);
        return input;
    }
    void AdoptNewDeck(DeckDefinition deck)
    {
        DeckRepository.SaveNew(deck); // Commit only after the entire input has passed parsing.
        currentDeck=deck;decks.Clear();decks.AddRange(DeckRepository.LoadAll());
        ApplyDeckLegalitySelection();
        loadedDeckIndex=FindDeckIndexByName(deck.name);hasUnsavedDeckChanges=false;
        UpdateDeleteDeckButton();
        deckNameInput.SetTextWithoutNotify(deck.name);deckNameInput.gameObject.SetActive(false);deckDropdown.gameObject.SetActive(true);
        RefreshDeckDropdown(deck.name);ClearHighlightedTile();RefreshDeckGrids();RefreshSelectedCardInfo();
        CloseDeckExchange();
    }
    void ShowSaveAsDeck()
    {
        if(currentDeck==null)return;
        var panel=ExchangePanel("另存卡组",new Vector2(620,290));
        var name=ExchangeInput(panel,"NewDeckName",new Vector2(0,35),new Vector2(560,46));name.text=currentDeck.name+" - 副本";name.characterLimit=100;
        var error=AddText(panel,"Error","",17,new Color(1,.55f,.5f),TextAnchor.MiddleLeft);Place(error.rectTransform,new Vector2(0,-25),new Vector2(560,56));
        CreateDialogButton(panel,"SaveCopy","另存",new Vector2(130,-102),MenuVisualTheme.Primary,()=>{
            if(string.IsNullOrWhiteSpace(name.text)){error.text="请输入卡组名。";return;}
            try{var copy=CloneDeck(currentDeck);copy.name=name.text.Trim();AdoptNewDeck(copy);}catch(Exception e){error.text=e.Message;}
        });
        CreateDialogButton(panel,"Cancel","取消",new Vector2(-130,-102),MenuVisualTheme.Raised,CloseDeckExchange);
        name.Select();name.ActivateInputField();
    }
    async Task<DeckTextExchange> GetDeckExchange()
    {
        if(deckExchange!=null)return deckExchange;
        var definitions=cards.ToArray();string path=Application.streamingAssetsPath;
        var codec=await Task.Run(()=>{
            var result=new DeckTextExchange(definitions);
            foreach(string locale in new[]{"zh-CN","ja"})
            {
                string file=Path.Combine(path,"CardTranslations",locale+".txt");if(!File.Exists(file))continue;
                foreach(var line in File.ReadLines(file)){var parts=line.Split(new[]{'|'},4);if(parts.Length==4 && !string.IsNullOrWhiteSpace(parts[1]))result.AddAlias(parts[0],parts[1]);}
            }
            return result;
        });
        if(this!=null)deckExchange=codec;return codec;
    }
    void RequestImportDeck()
    {
        if(hasUnsavedDeckChanges) ShowUnsavedChangesDialog(ShowImportDeck);
        else ShowImportDeck();
    }

    static Button ExchangeButton(Transform panel,string name,string caption,Vector2 position,Action action,bool primary=false)
    {
        var button=CreateDialogButton(panel,name,caption,position,MenuVisualTheme.Raised,action);
        MenuVisualTheme.StyleDeckTool(button,primary);
        return button;
    }

    void ShowImportDeck()
    {
        var panel=ExchangePanel("导入卡组",new Vector2(900,660));
        StyleDeckDialog(panel);
        var hint=AddText(panel,"Hint","粘贴后自动识别 MO / MTGA / MTGB，直接创建新卡组。",18,MenuVisualTheme.Stone,TextAnchor.MiddleLeft);Place(hint.rectTransform,new Vector2(0,238),new Vector2(840,36));
        var fields=new GameObject("TextFields",typeof(RectTransform),typeof(VerticalLayoutGroup));
        fields.transform.SetParent(panel,false);
        Place((RectTransform)fields.transform,new Vector2(0,-24),new Vector2(840,472));
        var layout=fields.GetComponent<VerticalLayoutGroup>();
        layout.spacing=12;
        layout.childControlWidth=layout.childControlHeight=true;
        layout.childForceExpandWidth=true;
        layout.childForceExpandHeight=false;
        var input=ExchangeScrollableInput(fields.transform,"DeckText",Vector2.zero,new Vector2(840,472));
        input.gameObject.AddComponent<LayoutElement>().flexibleHeight=1;
        var errors=ExchangeScrollableInput(fields.transform,"ImportErrors",Vector2.zero,new Vector2(840,112),readOnly:true);
        errors.gameObject.AddComponent<LayoutElement>().preferredHeight=112;
        errors.textComponent.color=new Color32(255,203,211,255);
        errors.gameObject.SetActive(false);
        input.onValueChanged.AddListener(value=>{
            if(importDebounce!=null)StopCoroutine(importDebounce);
            exchangeGeneration++;importDebounce=StartCoroutine(ParsePastedDeck(value,errors,exchangeGeneration));
        });
        ExchangeButton(panel,"Paste","粘贴",new Vector2(330,-293),()=>{
            input.text=GUIUtility.systemCopyBuffer;
            input.Select();
            input.ActivateInputField();
            input.MoveTextEnd(false);
        },primary:true);
        ExchangeButton(panel,"Cancel","取消",new Vector2(-330,-293),CloseDeckExchange);
        input.Select();input.ActivateInputField();
    }
    IEnumerator ParsePastedDeck(string text,TMPro.TMP_InputField errors,int generation)
    {
        yield return new WaitForSecondsRealtime(.35f);
        importDebounce=null;
        if(string.IsNullOrWhiteSpace(text)){errors.gameObject.SetActive(false);yield break;}
        var task=GetDeckExchange();while(!task.IsCompleted)yield return null;
        if(generation!=exchangeGeneration || errors==null)yield break;
        if(task.IsFaulted){errors.gameObject.SetActive(true);errors.text=task.Exception.GetBaseException().Message;yield break;}
        var result=task.Result.Import(text);
        if(result.Success)
        {
            try{AdoptNewDeck(result.Deck);}catch(Exception e){errors.gameObject.SetActive(true);errors.text=e.Message;}
        }
        else {errors.gameObject.SetActive(true);errors.text=string.Join("\n",result.Errors);errors.MoveTextStart(false);}
    }
    async void ShowExportDeck()
    {
        if(currentDeck==null)return;
        var panel=ExchangePanel("导出卡组",new Vector2(900,660));int generation=exchangeGeneration;
        StyleDeckDialog(panel);
        var output=ExchangeScrollableInput(panel,"ExportText",new Vector2(0,-10),new Vector2(840,392),readOnly:true);
        var hint=AddText(panel,"Hint","",17,MenuVisualTheme.Stone,TextAnchor.MiddleLeft);Place(hint.rectTransform,new Vector2(0,-244),new Vector2(840,60));
        bool valid=false;
        var copy=ExchangeButton(panel,"Copy","复制",new Vector2(330,-293),()=>{if(valid)GUIUtility.systemCopyBuffer=output.text;},primary:true);copy.interactable=false;
        ExchangeButton(panel,"Close","关闭",new Vector2(-330,-293),CloseDeckExchange);
        try
        {
            var codec=await GetDeckExchange();if(this==null || generation!=exchangeGeneration || panel==null)return;
            var buttons=new System.Collections.Generic.List<UnityEngine.UI.Button>();
            Action<DeckTextExchange.Format> select=format=>{
                try{output.text=codec.Export(currentDeck,format);valid=true;hint.text=GamePreferences.T(format==DeckTextExchange.Format.MTGB
                    ?"MTGB .mdk：保留系列、编号、分区、赛制和禁表日期。"
                    :format==DeckTextExchange.Format.MO
                        ?"MO 纯文本：保留数量与主备牌；不保留版本、赛制或禁表日期。"
                        :"MTGA 文本：保留系列与编号；不保留赛制或禁表日期。");}
                catch(Exception e){output.text="";hint.text=e.Message;valid=false;}
                copy.interactable=valid;
                for(int i=0;i<buttons.Count;i++)MenuVisualTheme.StyleDeckTool(buttons[i],i==(int)format);
                output.MoveTextStart(false);
                output.verticalScrollbar.value=0;
            };
            foreach(DeckTextExchange.Format format in Enum.GetValues(typeof(DeckTextExchange.Format)))
            {var captured=format;buttons.Add(ExchangeButton(panel,format.ToString(),format.ToString(),new Vector2(((int)format-1)*180,222),()=>select(captured)));}
            select(DeckTextExchange.Format.MTGB);
        }
        catch(Exception e){if(hint!=null)hint.text=e.Message;}
    }
}

// Toolbar labels change width when the locale or the user's text scale changes.
// Give each button its actual single-line width before the layout group positions it.
public sealed class DeckToolbarAutoSize : MonoBehaviour
{
    private sealed class Item
    {
        public LayoutElement layout;
        public Text label;
        public float baseWidth;
        public string lastText;
        public int lastSize;
        public Font lastFont;
        public Dropdown dropdown;
        public int lastRevision=-1;
    }

    private readonly System.Collections.Generic.List<Item> items = new System.Collections.Generic.List<Item>();

    public void Configure()
    {
        items.Clear();
        foreach (Transform child in transform)
        {
            var dropdown=child.GetComponent<Dropdown>();
            if (child.GetComponent<Button>() == null && dropdown == null && child.name != "DeckEditorHeading") continue;
            var layout = child.GetComponent<LayoutElement>();
            var label = dropdown!=null?dropdown.captionText:child.GetComponentInChildren<Text>(true);
            if (layout == null || label == null) continue;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.verticalOverflow = dropdown!=null ? VerticalWrapMode.Overflow : VerticalWrapMode.Truncate;
            items.Add(new Item { layout = layout, label = label, baseWidth = layout.preferredWidth, dropdown=dropdown });
        }
        Refresh();
    }

    private void LateUpdate() { Refresh(); }
    public void RefreshNames() { Refresh(true); }

    private static float Measure(Text label,string value)=>label.cachedTextGeneratorForLayout.GetPreferredWidth(value,label.GetGenerationSettings(Vector2.zero))/label.pixelsPerUnit;
    private void Refresh(bool force=false)
    {
        foreach (Item item in items)
        {
            if (item.label == null || item.layout == null) continue;
            if (!force&&item.lastText == item.label.text && item.lastSize == item.label.fontSize && item.lastFont == item.label.font&&item.lastRevision==GamePreferences.Revision) continue;
            item.lastText = item.label.text;
            item.lastSize = item.label.fontSize;
            item.lastFont = item.label.font;
            item.lastRevision=GamePreferences.Revision;
            float width = Mathf.Max(item.baseWidth, Mathf.Ceil(item.label.preferredWidth) + 22f);
            if(item.dropdown!=null) {
                var dropdown=item.dropdown;
                dropdown.itemText.GetComponent<PreferenceText>()?.Apply();
                float minimum=Mathf.Max(item.baseWidth,Mathf.Ceil(Measure(dropdown.itemText,"Imported Deck (000)"))+48);
                width=Mathf.Clamp(Mathf.Ceil(item.label.preferredWidth)+48,minimum,Mathf.Max(minimum,440));
                float popup=width;
                foreach(var option in dropdown.options)popup=Mathf.Max(popup,Mathf.Ceil(Measure(dropdown.itemText,option.text))+32);
                var canvas=dropdown.GetComponentInParent<Canvas>().rootCanvas.transform as RectTransform;
                if(canvas!=null)popup=Mathf.Max(width,Mathf.Min(popup,canvas.rect.width-40));
                dropdown.template.sizeDelta=new Vector2(popup-width,dropdown.template.sizeDelta.y);
                dropdown.template.anchoredPosition=new Vector2((popup-width)*.5f,dropdown.template.anchoredPosition.y);
            }
            item.layout.minWidth = item.layout.preferredWidth = width;
        }
    }
}
