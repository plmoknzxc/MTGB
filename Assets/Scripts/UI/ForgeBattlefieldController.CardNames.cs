using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public sealed partial class ForgeBattlefieldController
{
    private sealed class NameFace { public CardDefinition card; public int face; }
    private sealed class NameEntry { public int index; public string canonical,localized; public NameFace face; }
    private Dictionary<string,NameFace> nameFaces;
    private NameEntry[] nameEntries=Array.Empty<NameEntry>(), filteredNames=Array.Empty<NameEntry>();
    private long nameRequest;
    private int nameLanguageRevision=-1,namePage,nameSelection=-1;
    private string nameQuery="";

    // Build once from exact local face names. The list never loads every card image.
    private void IndexNameFaces()
    {
        if(nameFaces!=null)return;
        nameFaces=new Dictionary<string,NameFace>(StringComparer.Ordinal);
        foreach(var card in catalog.Values) {
            if(!nameFaces.ContainsKey(card.cardName))nameFaces.Add(card.cardName,new NameFace{card=card,face=0});
            foreach(var feature in card.features??new List<CardFeatureDefinition>())
                if(feature!=null&&!string.IsNullOrEmpty(feature.cardName)&&!nameFaces.ContainsKey(feature.cardName))
                    nameFaces.Add(feature.cardName,new NameFace{card=card,face=feature.faceIndex});
        }
    }

    private void PresentCardNameChoices(long captured)
    {
        IndexNameFaces();
        if(nameRequest!=captured||nameLanguageRevision!=GamePreferences.Revision) {
            bool newRequest=nameRequest!=captured;
            nameEntries=view.request.options.Select((o,i)=>new {o,i}).Where(x=>!string.IsNullOrEmpty(x.o.cardName))
                .GroupBy(x=>x.o.cardName,StringComparer.Ordinal).Select(g=>g.First())
                .Select(x=>new NameEntry{index=x.i,canonical=x.o.cardName,localized=GamePreferences.CardFaceName(x.o.cardName),
                    face=nameFaces.TryGetValue(x.o.cardName,out var face)?face:null}).ToArray();
            nameRequest=captured;nameLanguageRevision=GamePreferences.Revision;
            if(newRequest){nameQuery="";namePage=0;nameSelection=-1;}
        }
        float width=Mathf.Min(930,surface.rect.width-48),height=Mathf.Min(650,surface.rect.height-48);
        float listWidth=width-306,listHeight=height-190;
        int pageSize=Math.Max(1,Mathf.FloorToInt(listHeight/58));
        landChoiceMenu.sizeDelta=new Vector2(width,height);landChoiceMenu.anchoredPosition=Vector2.zero;
        landChoiceMenu.GetComponent<Image>().color=Ink;
        Label(landChoiceMenu,"NameTitle",TapPaymentText("指定牌名","Name a card","カード名を指定"),new Vector2(0,height/2-28),new Vector2(width-40,34),23,Paper);
        var inputRoot=Panel(landChoiceMenu,"NameSearch",new Vector2(-136,height/2-82),new Vector2(listWidth,42),new Color(.025f,.035f,.05f));
        var searchText=Label(inputRoot,"Text",nameQuery,Vector2.zero,new Vector2(listWidth-18,36),18,Paper);
        searchText.supportRichText=false;searchText.alignment=TextAnchor.MiddleLeft;
        var placeholder=Label(inputRoot,"Placeholder",TapPaymentText("搜索牌名","Search card names","カード名を検索"),Vector2.zero,new Vector2(listWidth-18,36),18,new Color(.5f,.58f,.65f));
        placeholder.alignment=TextAnchor.MiddleLeft;placeholder.supportRichText=false;
        var input=inputRoot.gameObject.AddComponent<InputField>();input.textComponent=searchText;input.placeholder=placeholder;
        input.lineType=InputField.LineType.SingleLine;input.characterLimit=120;input.SetTextWithoutNotify(nameQuery);input.interactable=canAct&&!submitted;
        var rows=Rect(landChoiceMenu,"NameRows",new Vector2(-136,-13),new Vector2(listWidth,listHeight));
        var status=Label(landChoiceMenu,"NamePage", "",new Vector2(-136,-height/2+72),new Vector2(listWidth-142,28),16,Paper);
        var preview=Panel(landChoiceMenu,"NamePreview",new Vector2(width/2-146,9),new Vector2(254,356),Ink);
        preview.GetComponent<Image>().preserveAspect=true;
        var previewName=Label(landChoiceMenu,"NamePreviewTitle","",new Vector2(width/2-146,height/2-87),new Vector2(268,70),18,Paper);previewName.supportRichText=false;
        var canonical=Label(landChoiceMenu,"NameCanonical","",new Vector2(width/2-146,-height/2+132),new Vector2(268,48),15,Paper);canonical.supportRichText=false;
        var missing=Label(preview,"MissingArt","",Vector2.zero,new Vector2(230,100),18,Paper);missing.raycastTarget=false;
        var confirm=Button(landChoiceMenu,"NameConfirm",TapPaymentText("确认牌名","Confirm name","名前を確定"),new Vector2(width/2-146,-height/2+37),new Vector2(240,44),()=>Choose(nameSelection,captured),new Color(.18f,.32f,.49f));
        Button previous=null,next=null;
        Action showPreview=()=>{
            var entry=nameEntries.FirstOrDefault(e=>e.index==nameSelection);
            confirm.interactable=entry!=null&&canAct&&!submitted;
            previewName.text=entry?.localized??TapPaymentText("请选择牌名","Select a card name","カード名を選択");
            canonical.text=entry==null?"":entry.canonical;
            Sprite sprite=null;
            if(entry?.face!=null)CardImageLibrary.TryLoadFullCard(entry.face.card,entry.face.face,out sprite);
            preview.GetComponent<Image>().sprite=sprite;preview.GetComponent<Image>().color=sprite==null?Ink:Color.white;
            missing.text=entry!=null&&sprite==null?TapPaymentText("缺失卡图","Missing card image","カード画像なし"):"";
        };
        Action render=null;
        render=()=>{
            Clear(rows);
            int pagesCount=Math.Max(1,(filteredNames.Length+pageSize-1)/pageSize);namePage=Mathf.Clamp(namePage,0,pagesCount-1);
            var visible=filteredNames.Skip(namePage*pageSize).Take(pageSize).ToArray();
            for(int row=0;row<visible.Length;row++) {
                var entry=visible[row];
                var button=Button(rows,"NameOption_"+entry.index,entry.localized,new Vector2(0,listHeight/2-28-row*58),new Vector2(listWidth,52),()=>{nameSelection=entry.index;render();},
                    nameSelection==entry.index?new Color(.18f,.38f,.55f):new Color(.13f,.19f,.25f));
                button.interactable=canAct&&!submitted;
                var text=button.GetComponentInChildren<Text>();PreferenceText.SetSize(text,18);text.resizeTextForBestFit=false;text.supportRichText=false;text.alignment=TextAnchor.MiddleLeft;
            }
            status.text=TapPaymentText($"{filteredNames.Length} 个牌名 · {namePage+1}/{pagesCount}",$"{filteredNames.Length} names · {namePage+1}/{pagesCount}",$"{filteredNames.Length} 件・{namePage+1}/{pagesCount}");
            previous.interactable=canAct&&!submitted&&namePage>0;next.interactable=canAct&&!submitted&&namePage+1<pagesCount;
            showPreview();
        };
        Action filter=()=>{
            string term=nameQuery.Trim();
            filteredNames=nameEntries.Where(e=>term.Length==0||e.canonical.IndexOf(term,StringComparison.OrdinalIgnoreCase)>=0||e.localized.IndexOf(term,StringComparison.OrdinalIgnoreCase)>=0)
                .OrderBy(e=>term.Length==0?0:string.Equals(e.canonical,term,StringComparison.OrdinalIgnoreCase)||string.Equals(e.localized,term,StringComparison.OrdinalIgnoreCase)?0:
                    e.canonical.StartsWith(term,StringComparison.OrdinalIgnoreCase)||e.localized.StartsWith(term,StringComparison.OrdinalIgnoreCase)?1:2).ToArray();
            if(!filteredNames.Any(e=>e.index==nameSelection))nameSelection=-1;
            render();
        };
        previous=Button(landChoiceMenu,"NamePrevious","‹",new Vector2(-136-listWidth/2+24,-height/2+72),new Vector2(44,32),()=>{namePage--;render();},Ink);
        next=Button(landChoiceMenu,"NameNext","›",new Vector2(-136+listWidth/2-24,-height/2+72),new Vector2(44,32),()=>{namePage++;render();},Ink);
        input.onValueChanged.AddListener(value=>{nameQuery=value;namePage=0;filter();});
        int cancel=OptionIndex("cancel");
        if(cancel>=0) {
            var cancelButton=Button(landChoiceMenu,"NameCancel",TapPaymentText("取消","Cancel","キャンセル"),new Vector2(-136,-height/2+30),new Vector2(160,38),()=>Choose(cancel,captured),Ink);
            cancelButton.interactable=canAct&&!submitted;
        }
        filter();landChoiceMenu.gameObject.SetActive(true);landChoiceMenu.SetAsLastSibling();
    }
}
