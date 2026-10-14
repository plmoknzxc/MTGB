using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public sealed partial class ForgeBattlefieldController
{
    private static string DungeonAbilityText(ForgeCardView source,string paragraph)
    {
        if(source==null||source.faceDown||!(source.currentType??"").Split(' ').Contains("Dungeon"))return paragraph;
        var room=System.Text.RegularExpressions.Regex.Match(paragraph,@"\s+\[Room: ([^\r\n]*)\]$");
        string rule=room.Success?paragraph.Substring(0,room.Index):paragraph;
        string original=(source.originalRules??source.currentRules??"").Replace("\\n","\n");
        string[] native=original.Split('\n');
        string Normalize(string value)=>System.Text.RegularExpressions.Regex.Replace(value.Trim(),@"\s+"," ");
        string Lookup(string value)=>source.name=="Undercity"?System.Text.RegularExpressions.Regex.Replace(value,@"\bArchive\b","Archives"):value;
        int line=Array.FindIndex(native,value=>Normalize(value)==Normalize(Lookup(rule)));
        if(line<0)return paragraph;
        string[] translated=GamePreferences.FeatureRules(new CardFeatureDefinition{cardName=source.name,description=original}).Replace("\\n","\n").Split('\n');
        if(native.Length!=translated.Length)return paragraph;
        string localized=translated[line];
        if(room.Success){
            string nativeRoom=native[line].Split('—')[0].Trim();
            int separator=localized.IndexOfAny(new[]{'—','―','～','〜'});
            string roomName=Lookup(room.Groups[1].Value)==nativeRoom&&separator>=0?localized.Substring(0,separator).Trim():room.Groups[1].Value;
            localized+="\n["+TapPaymentText("房间：","Room: ","部屋：")+roomName+"]";
        }
        return localized;
    }
    private static string DungeonRoomText(ForgeCardView dungeon,string label)
    {
        if(dungeon==null || GamePreferences.Language=="en-US")return label;
        string original=dungeon.originalRules??dungeon.currentRules??"";
        var feature=new CardFeatureDefinition{cardName=dungeon.name,description=original};
        string[] english=original.Replace("\\n","\n").Split('\n');
        string[] translated=GamePreferences.FeatureRules(feature).Replace("\\n","\n").Split('\n');
        if(english.Length!=translated.Length)return label;
        int separator=(label??"").IndexOf('—');
        if(separator<0)return label;
        string room=label.Substring(0,separator).Trim();
        // The pinned Undercity room script says Archive; its Oracle says Archives.
        if(dungeon.name=="Undercity"&&room=="Archive")room="Archives";
        int line=Array.FindIndex(english,text=>text.StartsWith(room+" —",StringComparison.Ordinal));
        return line>=0 && translated[line]!=english[line] ? translated[line] : label;
    }
    // The legal branches come from Forge; the client never invents room successors.
    private void PresentDungeonRoomChoices(ForgeCardView dungeon,long captured)
    {
        float height=Mathf.Min(590,surface.rect.height-52),width=Mathf.Min(720,surface.rect.width-52);
        landChoiceMenu.sizeDelta=new Vector2(width,height);
        landChoiceMenu.anchoredPosition=Vector2.zero;
        landChoiceMenu.GetComponent<Image>().color=Ink;
        Label(landChoiceMenu,"Title",TapPaymentText("选择下一个房间","Choose the next room","次の部屋を選択"),new Vector2(0,height/2-26),new Vector2(width-36,34),23,Paper);
        float artHeight=height-88,artWidth=artHeight/1.4f;
        var art=Rect(landChoiceMenu,"DungeonMap",new Vector2(-width*.5f+artWidth*.5f+22,-12),new Vector2(artWidth,artHeight)).gameObject.AddComponent<Image>();
        TryBattlefieldArt(dungeon,out var sprite);
        art.sprite=sprite;art.preserveAspect=true;art.color=sprite==null?Ink:Color.white;art.raycastTarget=false;
        if(sprite==null)Label(art.rectTransform,"MissingArt",TapPaymentText("缺失卡图","Missing artwork","画像がありません"),Vector2.zero,new Vector2(artWidth,48),18,Paper);
        float choiceWidth=width-artWidth-64;
        var content=Scroll(landChoiceMenu,"RoomChoices",new Vector2(artWidth*.5f+10,-12),new Vector2(choiceWidth,height-88));
        float y=0;
        foreach(var option in view.request.options??Array.Empty<ForgeOption>()) {
            int index=OptionIndex(option.id);
            string description=DungeonRoomText(dungeon,option.label);
            var text=new GameObject("Measure",typeof(RectTransform),typeof(Text)).GetComponent<Text>();
            text.font=GamePreferences.Font;text.fontSize=Mathf.RoundToInt(18*GamePreferences.TextScale);text.text=description;text.rectTransform.sizeDelta=new Vector2(choiceWidth-30,0);
            float rowHeight=Mathf.Max(94,text.preferredHeight+32);Destroy(text.gameObject);
            var button=Button(content,"DungeonRoom_"+index,description,Vector2.zero,new Vector2(choiceWidth,rowHeight),()=>Choose(index,captured),new Color(.14f,.37f,.44f));
            var rect=(RectTransform)button.transform;rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(.5f,1);rect.anchoredPosition=new Vector2(0,-y);
            var label=button.GetComponentInChildren<Text>();PreferenceText.SetSize(label,18);label.resizeTextForBestFit=false;label.alignment=TextAnchor.MiddleLeft;label.supportRichText=false;
            label.rectTransform.sizeDelta=new Vector2(choiceWidth-24,rowHeight-18);
            button.interactable=canAct&&!submitted;y+=rowHeight+16;
        }
        content.sizeDelta=new Vector2(choiceWidth,y);
        if(y>height-88)AddStackScrollIndicator(content);
        landChoiceMenu.gameObject.SetActive(true);landChoiceMenu.SetAsLastSibling();
    }
}
