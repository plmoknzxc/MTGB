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

    private void CreateDeckExchangeToolbar()
    {
        var parent=deckDropdown.transform.parent;
        CreateDialogButton(parent,"SaveAsDeckButton","另存",Vector2.zero,MenuVisualTheme.Raised,ShowSaveAsDeck);
        CreateDialogButton(parent,"ImportDeckButton","导入",Vector2.zero,MenuVisualTheme.Raised,()=>{
            if(hasUnsavedDeckChanges)ShowUnsavedChangesDialog(ShowImportDeck);else ShowImportDeck();
        });
        CreateDialogButton(parent,"ExportDeckButton","导出",Vector2.zero,MenuVisualTheme.Raised,ShowExportDeck);
        deleteDeckButton=CreateDialogButton(parent,"DeleteDeckButton","删除",Vector2.zero,new Color(.62f,.16f,.19f),ShowDeleteDeck);
        reloadButton.gameObject.SetActive(false);
        var row=new GameObject("DeckToolbar",typeof(RectTransform),typeof(HorizontalLayoutGroup));row.transform.SetParent(parent,false);
        var rect=(RectTransform)row.transform;rect.anchorMin=new Vector2(0,1);rect.anchorMax=Vector2.one;
        rect.pivot=new Vector2(.5f,1);rect.offsetMin=new Vector2(32,-80);rect.offsetMax=new Vector2(-32,-32);
        var layout=row.GetComponent<HorizontalLayoutGroup>();layout.spacing=10;layout.childAlignment=TextAnchor.MiddleLeft;
        layout.childControlWidth=layout.childControlHeight=true;layout.childForceExpandHeight=true;layout.childForceExpandWidth=false;
        string[] order={"BackToTitleButton","DeckDropdown","DeckNameInput","RenameDeckButton","NewDeckButton","SaveDeckButton","SaveAsDeckButton","ImportDeckButton","ExportDeckButton","DeleteDeckButton","CollectionSearchInput","CollectionSearchButton","CollectionSearchResetButton","AdvancedSearchButton"};
        foreach(string name in order)
        {
            if(name=="CollectionSearchInput")
            {
                var spacer=new GameObject("ToolbarSpacer",typeof(RectTransform),typeof(LayoutElement));
                spacer.transform.SetParent(row.transform,false);spacer.GetComponent<LayoutElement>().flexibleWidth=1;
            }
            var child=parent.GetComponentsInChildren<Transform>(true).First(t=>t.name==name);child.SetParent(row.transform,false);
            var size=child.gameObject.AddComponent<LayoutElement>();
            size.minWidth=size.preferredWidth=name=="DeckDropdown" || name=="DeckNameInput"?220:name=="CollectionSearchInput"?280:name=="AdvancedSearchButton"?110:name=="RenameDeckButton"?94:74;
            size.flexibleWidth=0;
            var button=child.GetComponent<UnityEngine.UI.Button>();if(button==null)continue;
            var image=child.GetComponent<UnityEngine.UI.Image>();image.sprite=IntegratedUiUtility.RoundedRect(100,48,6);
            image.color=name=="SaveDeckButton"?new Color(.22f,.43f,.69f):name=="DeleteDeckButton"?new Color(.62f,.16f,.19f):new Color(.19f,.25f,.34f);
            var outline=child.GetComponent<Outline>();if(outline==null)outline=child.gameObject.AddComponent<Outline>();
            outline.effectDistance=new Vector2(1,-1);outline.effectColor=name=="DeleteDeckButton"?new Color(.9f,.38f,.39f,.9f):new Color(.39f,.52f,.68f,.85f);
            var colors=button.colors;colors.normalColor=Color.white;colors.highlightedColor=new Color(1.3f,1.3f,1.3f);colors.pressedColor=new Color(.7f,.8f,.95f);button.colors=colors;
            foreach(var text in child.GetComponentsInChildren<Text>()){text.color=Color.white;text.fontSize=19;text.raycastTarget=false;}
        }
        deckDropdown.captionText.fontSize=19;deckDropdown.captionText.horizontalOverflow=HorizontalWrapMode.Wrap;
        deckDropdown.captionText.verticalOverflow=VerticalWrapMode.Truncate;deckDropdown.captionText.resizeTextForBestFit=true;
        deckDropdown.captionText.resizeTextMinSize=13;deckDropdown.captionText.resizeTextMaxSize=25;
        row.AddComponent<DeckToolbarAutoSize>().Configure();
        UpdateDeleteDeckButton();
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
        exchangeGeneration++;
        if(importDebounce!=null){StopCoroutine(importDebounce);importDebounce=null;}
        if(exchangeDialog!=null){exchangeDialog.SetActive(false);Destroy(exchangeDialog);exchangeDialog=null;}
    }
    RectTransform ExchangePanel(string title,Vector2 size)
    {
        CloseDeckExchange();
        exchangeDialog=new GameObject("DeckExchange",typeof(RectTransform),typeof(UnityEngine.UI.Image));exchangeDialog.transform.SetParent(GetRootCanvas().transform,false);
        Stretch((RectTransform)exchangeDialog.transform,0,0,0,0);exchangeDialog.GetComponent<UnityEngine.UI.Image>().color=new Color(0,0,0,.78f);
        var panel=new GameObject("Panel",typeof(RectTransform),typeof(UnityEngine.UI.Image));panel.transform.SetParent(exchangeDialog.transform,false);
        Place((RectTransform)panel.transform,Vector2.zero,size);panel.GetComponent<UnityEngine.UI.Image>().color=MenuVisualTheme.Panel;
        var heading=AddText(panel.transform,"Heading",title,25,Color.white,TextAnchor.MiddleLeft);
        Place(heading.rectTransform,new Vector2(0,size.y*.5f-38),new Vector2(size.x-60,40));
        return (RectTransform)panel.transform;
    }
    InputField ExchangeInput(Transform parent,string name,Vector2 pos,Vector2 size,bool multiline=false)
    {
        var go=new GameObject(name,typeof(RectTransform),typeof(UnityEngine.UI.Image),typeof(UnityEngine.UI.InputField));go.transform.SetParent(parent,false);Place((RectTransform)go.transform,pos,size);
        var input=go.GetComponent<UnityEngine.UI.InputField>();input.targetGraphic=go.GetComponent<UnityEngine.UI.Image>();input.targetGraphic.color=MenuVisualTheme.Ink;
        input.textComponent=AddText(go.transform,"Text","",18,MenuVisualTheme.Paper,multiline?TextAnchor.UpperLeft:TextAnchor.MiddleLeft);
        input.textComponent.supportRichText=false;Stretch(input.textComponent.rectTransform,12,10,12,10);
        // The parser rejects oversized input as a whole. Never truncate a pasted deck into a valid partial list.
        input.lineType=multiline?InputField.LineType.MultiLineNewline:InputField.LineType.SingleLine;input.characterLimit=0;
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
    void ShowImportDeck()
    {
        var panel=ExchangePanel("导入卡组",new Vector2(900,660));
        var hint=AddText(panel,"Hint","粘贴后自动识别 MO / MTGA / MTGB，直接创建新卡组。",18,MenuVisualTheme.Muted,TextAnchor.MiddleLeft);Place(hint.rectTransform,new Vector2(0,238),new Vector2(840,36));
        var input=ExchangeInput(panel,"DeckText",new Vector2(0,38),new Vector2(840,348),true);
        var errors=ExchangeInput(panel,"ImportErrors",new Vector2(0,-205),new Vector2(840,112),true);errors.readOnly=true;errors.gameObject.SetActive(false);
        input.onValueChanged.AddListener(value=>{
            if(importDebounce!=null)StopCoroutine(importDebounce);
            exchangeGeneration++;importDebounce=StartCoroutine(ParsePastedDeck(value,errors,exchangeGeneration));
        });
        CreateDialogButton(panel,"Paste","粘贴",new Vector2(330,-293),MenuVisualTheme.Primary,()=>input.text=GUIUtility.systemCopyBuffer);
        CreateDialogButton(panel,"Cancel","取消",new Vector2(-330,-293),MenuVisualTheme.Raised,CloseDeckExchange);
        input.Select();input.ActivateInputField();
    }
    IEnumerator ParsePastedDeck(string text,InputField errors,int generation)
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
        var output=ExchangeInput(panel,"ExportText",new Vector2(0,-10),new Vector2(840,392),true);output.readOnly=true;
        var hint=AddText(panel,"Hint","",17,MenuVisualTheme.Muted,TextAnchor.MiddleLeft);Place(hint.rectTransform,new Vector2(0,-244),new Vector2(840,60));
        bool valid=false;
        var copy=CreateDialogButton(panel,"Copy","复制",new Vector2(330,-293),MenuVisualTheme.Primary,()=>{if(valid)GUIUtility.systemCopyBuffer=output.text;});copy.interactable=false;
        CreateDialogButton(panel,"Close","关闭",new Vector2(-330,-293),MenuVisualTheme.Raised,CloseDeckExchange);
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
                for(int i=0;i<buttons.Count;i++)buttons[i].GetComponent<UnityEngine.UI.Image>().color=i==(int)format?MenuVisualTheme.Primary:MenuVisualTheme.Raised;
                output.MoveTextStart(false);
            };
            foreach(DeckTextExchange.Format format in Enum.GetValues(typeof(DeckTextExchange.Format)))
            {var captured=format;buttons.Add(CreateDialogButton(panel,format.ToString(),format.ToString(),new Vector2(((int)format-1)*180,222),MenuVisualTheme.Raised,()=>select(captured)));}
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
    }

    private readonly System.Collections.Generic.List<Item> items = new System.Collections.Generic.List<Item>();

    public void Configure()
    {
        items.Clear();
        foreach (Transform child in transform)
        {
            if (child.GetComponent<Button>() == null) continue;
            var layout = child.GetComponent<LayoutElement>();
            var label = child.GetComponentInChildren<Text>(true);
            if (layout == null || label == null) continue;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.verticalOverflow = VerticalWrapMode.Truncate;
            items.Add(new Item { layout = layout, label = label, baseWidth = layout.preferredWidth });
        }
        Refresh();
    }

    private void LateUpdate() { Refresh(); }

    private void Refresh()
    {
        foreach (Item item in items)
        {
            if (item.label == null || item.layout == null) continue;
            if (item.lastText == item.label.text && item.lastSize == item.label.fontSize && item.lastFont == item.label.font) continue;
            item.lastText = item.label.text;
            item.lastSize = item.label.fontSize;
            item.lastFont = item.label.font;
            float width = Mathf.Max(item.baseWidth, Mathf.Ceil(item.label.preferredWidth) + 22f);
            item.layout.minWidth = item.layout.preferredWidth = width;
        }
    }
}
