using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public sealed partial class ForgeBattlefieldController
{
    private readonly Dictionary<long,RectTransform> cardNoticeWindows=new();
    private readonly Dictionary<long,string> cardNoticeLayouts=new();
    private readonly HashSet<long> dismissedCardNotices=new();
    private static bool IsCardNotice(ForgeNotice notice)=>notice.kind is "card-reveal" or "card-move";
    private void ResetCardNotices()
    {
        foreach(var window in cardNoticeWindows.Values)if(window!=null)Destroy(window.gameObject);
        cardNoticeWindows.Clear();cardNoticeLayouts.Clear();dismissedCardNotices.Clear();
    }
    private static string NoticeZone(string zone)=>zone switch {
        "Library"=>TapPaymentText("牌库","library","ライブラリー"),"Hand"=>TapPaymentText("手牌","hand","手札"),
        "Graveyard"=>TapPaymentText("坟墓场","graveyard","墓地"),"Exile"=>TapPaymentText("放逐区","exile","追放領域"),
        "Battlefield"=>TapPaymentText("战场","battlefield","戦場"),"Stack"=>TapPaymentText("堆叠","stack","スタック"),
        "Command"=>TapPaymentText("统帅区","command zone","統率領域"),"Sideboard"=>TapPaymentText("备牌","sideboard","サイドボード"),
        _=>TapPaymentText("原区域","its previous zone","元の領域")
    };
    private string CardNoticeDescription(ForgeNotice notice)
    {
        string source=notice.sourceCard?.faceDown==false?GamePreferences.CardFaceName(notice.sourceCard.name):null;
        string reason=notice.reason=="cost"?TapPaymentText("支付费用","paying a cost","コストの支払い"):
            notice.reason=="effect"?TapPaymentText("效果结算","an effect resolving","効果の解決"):TapPaymentText("规则处理","a game action","ゲームの処理");
        string cause=string.IsNullOrEmpty(source)?reason:TapPaymentText($"因《{source}》{reason}",$"{source}: {reason}",$"「{source}」：{reason}");
        if(notice.kind=="card-move")return cause+"\n"+NoticeZone(notice.fromZone)+" → "+NoticeZone(notice.toZone);
        var zones=(notice.cards??Array.Empty<ForgeCardView>()).Select(c=>c.zone).Where(z=>!string.IsNullOrEmpty(z)).Distinct();
        return cause+"\n"+TapPaymentText("展示卡牌，来自：","Cards shown from: ","公開されたカードの領域：")+string.Join("、",zones.Select(NoticeZone));
    }
    private void CloseCardNotice(long id)
    {
        dismissedCardNotices.Add(id);
        if(cardNoticeWindows.Remove(id,out var window)&&window!=null)Destroy(window.gameObject);
        cardNoticeLayouts.Remove(id);
    }
    private void PresentCardNotices(ForgeNotice[] notes)
    {
        foreach(var note in notes.Where(IsCardNotice).OrderBy(n=>n.id)) {
            if(dismissedCardNotices.Contains(note.id)||(!cardNoticeWindows.ContainsKey(note.id)&&note.id<=lastNoticeId))continue;
            string key=GamePreferences.Revision+"/"+surface.rect.size+"/"+note.kind+"/"+CardNoticeDescription(note)+"/"+
                string.Join("|",(note.cards??Array.Empty<ForgeCardView>()).Select(c=>c.id+":"+c.name+":"+c.printingKey+":"+c.faceDown));
            if(cardNoticeLayouts.TryGetValue(note.id,out string previous)&&previous==key)continue;
            bool created=!cardNoticeWindows.TryGetValue(note.id,out var window);
            Vector2 position=created?new Vector2((cardNoticeWindows.Count%5)*28-56,56-(cardNoticeWindows.Count%5)*28):window.anchoredPosition;
            if(created) {
                window=Panel(surface,"CardNotice_"+note.id,position,new Vector2(400,360),Ink);
                SetOverlayLayer(window,NoticeLayerOffset,true);cardNoticeWindows.Add(note.id,window);
            } else Clear(window);
            var cards=note.cards??Array.Empty<ForgeCardView>();
            float width=Mathf.Min(cards.Length<=1?300:380,surface.rect.width-48);
            var description=Label(window,"Description",CardNoticeDescription(note),Vector2.zero,new Vector2(width-32,80),17,Paper);
            description.supportRichText=false;description.raycastTarget=false;description.resizeTextForBestFit=false;PreferenceText.SetSize(description,17);
            float descriptionHeight=Mathf.Max(40,description.preferredHeight+6);
            int columns=Math.Max(1,Math.Min(cards.Length,Mathf.FloorToInt((width-24)/110)));
            float cardWidth=Mathf.Min(cards.Length<=1?145:116,(width-32)/columns-8),cardHeight=cardWidth*1.4f;
            float nameHeight=Mathf.Max(24,Mathf.Ceil(18*GamePreferences.TextScale)),rowHeight=cardHeight+nameHeight+12;
            float gridHeight=Math.Max(1,Mathf.Ceil(cards.Length/(float)columns))*rowHeight;
            float height=Mathf.Min(74+descriptionHeight+gridHeight,Mathf.Min(600,surface.rect.height-70));
            window.sizeDelta=new Vector2(width,height);
            window.anchoredPosition=new Vector2(Mathf.Clamp(position.x,surface.rect.xMin+width*.5f,surface.rect.xMax-width*.5f),Mathf.Clamp(position.y,surface.rect.yMin+height*.5f,surface.rect.yMax-height*.5f));
            window.GetComponent<Image>().sprite=IntegratedUiUtility.RoundedRect(Mathf.RoundToInt(width),Mathf.RoundToInt(height),10);
            Color[] accents={new Color(.3f,.76f,.9f),new Color(.92f,.68f,.28f),new Color(.72f,.55f,.94f),new Color(.4f,.8f,.56f)};
            Color accent=accents[(int)(note.id%accents.Length)];
            var edge=window.GetComponent<Outline>()??window.gameObject.AddComponent<Outline>();edge.effectColor=accent;edge.effectDistance=new Vector2(2,-2);
            var shadow=window.GetComponents<Shadow>().FirstOrDefault(s=>s.GetType()==typeof(Shadow))??window.gameObject.AddComponent<Shadow>();shadow.effectColor=new Color(0,0,0,.85f);shadow.effectDistance=new Vector2(8,-8);
            var header=Panel(window,"Header",new Vector2(0,height*.5f-23),new Vector2(width,46),new Color(.07f+accent.r*.25f,.07f+accent.g*.25f,.07f+accent.b*.25f));
            header.gameObject.AddComponent<ForgeCardNoticeDrag>().Window=window;
            var title=Label(header,"Title",note.kind=="card-move"?TapPaymentText("卡牌移区","Cards moved","カードの移動"):TapPaymentText("展示卡牌","Revealed cards","公開されたカード"),new Vector2(-22,0),new Vector2(width-78,42),20,Paper);
            title.raycastTarget=false;title.fontStyle=FontStyle.Bold;
            long id=note.id;
            var close=Button(header,"Close","×",new Vector2(width*.5f-25,0),new Vector2(42,42),()=>CloseCardNotice(id),Color.clear);
            close.GetComponentInChildren<Text>().raycastTarget=false;
            description.rectTransform.sizeDelta=new Vector2(width-32,descriptionHeight);
            description.rectTransform.anchoredPosition=new Vector2(0,height*.5f-54-descriptionHeight*.5f);
            float areaHeight=Mathf.Max(60,height-74-descriptionHeight);
            var content=Scroll(window,"Cards",new Vector2(0,height*.5f-62-descriptionHeight-areaHeight*.5f),new Vector2(width-24,areaHeight));
            content.sizeDelta=new Vector2(width-24,Math.Max(areaHeight,gridHeight));
            for(int i=0;i<cards.Length;i++) {
                var card=cards[i];float x=-(width-24)*.5f+(i%columns+.5f)*(width-24)/columns,y=-(i/columns)*rowHeight-cardHeight*.5f-nameHeight-4;
                var art=Panel(content,"Card_"+card.id,new Vector2(x,y),new Vector2(cardWidth,cardHeight),Color.clear);
                art.anchorMin=art.anchorMax=new Vector2(.5f,1);
                TryBattlefieldArt(card,out var sprite);
                var image=art.GetComponent<Image>();image.sprite=sprite;image.preserveAspect=true;image.color=sprite==null?Ink:Color.white;
                PresentComponentFace(art,card);
                var name=Label(content,"Name_"+card.id,card.faceDown?TapPaymentText("面朝下的牌","Face-down card","裏向きのカード"):GamePreferences.CardFaceName(card.name),new Vector2(x,y+cardHeight*.5f+nameHeight*.5f+4),new Vector2(cardWidth+4,nameHeight),13,Paper);
                name.rectTransform.anchorMin=name.rectTransform.anchorMax=new Vector2(.5f,1);name.raycastTarget=false;
                foreach(var graphic in art.GetComponentsInChildren<Graphic>())graphic.raycastTarget=false;
            }
            cardNoticeLayouts[note.id]=key;
        }
    }
}
