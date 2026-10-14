using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public sealed partial class ForgeBattlefieldController
{
    private long creatureTypeRequest;
    private int creatureTypePage;
    private string creatureTypeQuery="";

    // Native single-choice strings are types, not abilities with repeated card art.
    // Search and paging retain the original option index and never invent types.
    private void PresentCreatureTypeChoices(long captured)
    {
        long context=view.request.contextId>0?view.request.contextId:captured;
        if(creatureTypeRequest!=context) {
            creatureTypeRequest=context;creatureTypePage=0;creatureTypeQuery="";
        }
        float width=Mathf.Min(760,surface.rect.width-48),height=Mathf.Min(510,surface.rect.height-48);
        float rowsHeight=height-172,cellWidth=(width-40)/3;
        int rowsPerPage=Math.Max(1,Mathf.FloorToInt(rowsHeight/54)),pageSize=rowsPerPage*3;
        landChoiceMenu.sizeDelta=new Vector2(width,height);landChoiceMenu.anchoredPosition=Vector2.zero;
        landChoiceMenu.GetComponent<Image>().color=Ink;
        Label(landChoiceMenu,"TypeTitle",TapPaymentText("选择生物类别","Choose a creature type","クリーチャー・タイプを選ぶ"),
            new Vector2(0,height/2-28),new Vector2(width-40,34),23,Paper);
        var searchRoot=Panel(landChoiceMenu,"TypeSearch",new Vector2(0,height/2-78),new Vector2(width-40,40),new Color(.025f,.035f,.05f));
        var text=Label(searchRoot,"Text",creatureTypeQuery,Vector2.zero,new Vector2(width-58,34),18,Paper);
        text.supportRichText=false;text.alignment=TextAnchor.MiddleLeft;
        var placeholder=Label(searchRoot,"Placeholder",TapPaymentText("搜索生物类别","Search creature types","クリーチャー・タイプを検索"),
            Vector2.zero,new Vector2(width-58,34),18,new Color(.5f,.58f,.65f));
        placeholder.supportRichText=false;placeholder.alignment=TextAnchor.MiddleLeft;
        var input=searchRoot.gameObject.AddComponent<InputField>();input.textComponent=text;input.placeholder=placeholder;
        input.lineType=InputField.LineType.SingleLine;input.characterLimit=120;input.SetTextWithoutNotify(creatureTypeQuery);
        input.interactable=canAct&&!submitted;
        var rows=Rect(landChoiceMenu,"TypeRows",new Vector2(0,-18),new Vector2(width-40,rowsHeight));
        var status=Label(landChoiceMenu,"TypePage","",new Vector2(0,-height/2+32),new Vector2(width-170,28),16,Paper);
        Button previous=null,next=null;
        Action render=()=>{
            Clear(rows);
            string term=creatureTypeQuery.Trim();
            var entries=view.request.options.Select((o,i)=>new {o,i,label=SearchInsertionMenu.Label(o.label)})
                .Where(e=>e.o.id.StartsWith("choice:")&&(term.Length==0||e.o.label.IndexOf(term,StringComparison.OrdinalIgnoreCase)>=0||e.label.IndexOf(term,StringComparison.OrdinalIgnoreCase)>=0)).ToArray();
            int pages=Math.Max(1,(entries.Length+pageSize-1)/pageSize);creatureTypePage=Mathf.Clamp(creatureTypePage,0,pages-1);
            var visible=entries.Skip(creatureTypePage*pageSize).Take(pageSize).ToArray();
            for(int n=0;n<visible.Length;n++) {
                var entry=visible[n];
                var button=Button(rows,"TypeOption_"+entry.i,entry.label,
                    new Vector2((n%3-1)*cellWidth,rowsHeight/2-24-n/3*54),new Vector2(cellWidth-8,46),()=>Choose(entry.i,captured),entry.o.selected?Gold:new Color(.13f,.19f,.25f));
                button.interactable=canAct&&!submitted;
                var label=button.GetComponentInChildren<Text>();PreferenceText.SetSize(label,17);
                label.resizeTextForBestFit=false;label.supportRichText=false;
            }
            status.text=TapPaymentText($"{entries.Length} 个类别 · {creatureTypePage+1}/{pages}",
                $"{entries.Length} types · {creatureTypePage+1}/{pages}",$"{entries.Length} 件・{creatureTypePage+1}/{pages}");
            previous.interactable=canAct&&!submitted&&creatureTypePage>0;
            next.interactable=canAct&&!submitted&&creatureTypePage+1<pages;
        };
        previous=Button(landChoiceMenu,"TypePrevious","‹",new Vector2(-width/2+42,-height/2+32),new Vector2(44,32),()=>{creatureTypePage--;render();},Ink);
        next=Button(landChoiceMenu,"TypeNext","›",new Vector2(width/2-42,-height/2+32),new Vector2(44,32),()=>{creatureTypePage++;render();},Ink);
        input.onValueChanged.AddListener(value=>{creatureTypeQuery=value;creatureTypePage=0;render();});
        render();landChoiceMenu.gameObject.SetActive(true);landChoiceMenu.SetAsLastSibling();
    }
}
