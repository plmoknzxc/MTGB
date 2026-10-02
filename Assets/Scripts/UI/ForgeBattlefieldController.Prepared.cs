using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public sealed partial class ForgeBattlefieldController
{
    private static bool HasPreparedSpell(ForgeCardView card)=>card?.preparedSpell!=null&&card.preparedSpell.id>0&&!string.IsNullOrEmpty(card.preparedSpell.name);

    private bool IsPreparedSpell(ForgeCardView card) => card?.stateName=="PreparedSpell";

    private ForgeCardView PreparedSource(string optionId) => (view?.players??Array.Empty<ForgePlayerView>())
        .SelectMany(p=>p.battlefield??Array.Empty<ForgeCardView>())
        .FirstOrDefault(c=>HasPreparedSpell(c)&&optionId=="card:"+c.preparedSpell.id);

    // A prepared spell uses its own name, cost and rules with the source illustration.
    // It is a spell card, not an ability shield or the source creature's full face.
    private void PresentPreparedSpellFace(RectTransform parent,ForgeCardView card,Vector2 size,bool force=false)
    {
        var face=parent.Find("PreparedSpellFace") as RectTransform;
        if(!force&&!IsPreparedSpell(card)&&!(card?.stateName=="Secondary"&&!card.creature&&!card.land)){if(face!=null)face.gameObject.SetActive(false);return;}
        parent.GetComponent<Image>().sprite=null;parent.GetComponent<Image>().color=Color.clear;
        if(face!=null&&face.Find("Name").GetComponent<Text>().text==card.name) {
            face.Find("Cost").GetComponent<TMPro.TextMeshProUGUI>().text=CardSymbolText.Format(card.manaCost??"");
            face.gameObject.SetActive(true);return;
        }
        if(face==null)face=Panel(parent,"PreparedSpellFace",Vector2.zero,size,Ink);else Clear(face);
        face.gameObject.SetActive(true);face.sizeDelta=size;face.anchoredPosition=Vector2.zero;
        face.GetComponent<Image>().sprite=IntegratedUiUtility.RoundedRect(240,335,9);
        float top=size.y*.5f;
        var title=Label(face,"Name",card.name,new Vector2(-30,top-20),new Vector2(size.x-82,29),17,Paper);
        title.alignment=TextAnchor.MiddleLeft;title.resizeTextForBestFit=true;title.resizeTextMinSize=11;title.resizeTextMaxSize=17;
        var cost=CardSymbolText.Create(face,"Cost",20,Paper);
        IntegratedUiUtility.Place(cost.rectTransform,new Vector2(size.x*.5f-42,top-20),new Vector2(72,28));
        cost.textWrappingMode=TMPro.TextWrappingModes.NoWrap;cost.enableAutoSizing=true;cost.fontSizeMin=12;cost.fontSizeMax=20;
        cost.alignment=TMPro.TextAlignmentOptions.MidlineRight;cost.text=CardSymbolText.Format(card.manaCost??"");
        Sprite sprite=null;
        if(catalog.TryGetValue(card.printingKey??"",out var definition))CardImageLibrary.TryLoadFullCard(definition,0,out sprite);
        float artHeight=size.y*.37f;
        var art=Rect(face,"Art",new Vector2(0,top-39-artHeight*.5f),new Vector2(size.x-16,artHeight)).gameObject.AddComponent<RawImage>();
        art.texture=sprite?.texture;art.color=sprite==null?new Color(.12f,.18f,.22f):Color.white;
        if(sprite!=null){var r=sprite.rect;art.uvRect=new Rect((r.x+r.width*.07f)/sprite.texture.width,(r.y+r.height*.43f)/sprite.texture.height,r.width*.86f/sprite.texture.width,r.height*.38f/sprite.texture.height);}
        var type=Label(face,"Type",card.currentType,new Vector2(0,top-52-artHeight),new Vector2(size.x-20,24),13,Paper);type.alignment=TextAnchor.MiddleLeft;
        float rulesHeight=size.y-artHeight-85;
        var rules=Panel(face,"Rules",new Vector2(0,-top+13+rulesHeight*.5f),new Vector2(size.x-16,rulesHeight),new Color(.87f,.89f,.85f));
        var content=Scroll(rules,"Text",Vector2.zero,new Vector2(size.x-28,rulesHeight-10));
        var body=Label(content,"Body",card.currentRules?.Replace("\\n","\n")??"",Vector2.zero,new Vector2(size.x-34,rulesHeight-10),14,new Color(.06f,.08f,.1f));body.alignment=TextAnchor.UpperLeft;
        float height=Mathf.Max(rulesHeight-10,body.preferredHeight+4);content.sizeDelta=new Vector2(size.x-28,height);
        body.rectTransform.anchorMin=body.rectTransform.anchorMax=body.rectTransform.pivot=new Vector2(.5f,1);body.rectTransform.sizeDelta=new Vector2(size.x-34,height);
        foreach(var graphic in face.GetComponentsInChildren<Graphic>())graphic.raycastTarget=false;
    }
}
