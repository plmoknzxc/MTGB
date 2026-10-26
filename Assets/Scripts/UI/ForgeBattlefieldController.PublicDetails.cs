using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public sealed partial class ForgeBattlefieldController
{
    private ForgeCardView TemporaryExiledCard(int id)=>(view?.players??Array.Empty<ForgePlayerView>())
        .SelectMany(p=>p.exileCards??Array.Empty<ForgeCardView>()).FirstOrDefault(c=>c.id==id);
    private string TemporaryExileDescription(ForgeCardView source)
    {
        var ids=source.temporaryExileIds??Array.Empty<int>();if(ids.Length==0)return "";
        string heading=ids.Length==1?TapPaymentText("暂时放逐：","Temporarily exiled: ","一時的に追放：")
            :TapPaymentText("暂时放逐（","Temporarily exiled (","一時的に追放（")+ids.Length+TapPaymentText("张）：", "): ","枚）：");
        return heading+string.Join("、",ids.Select(id=>{
            var card=TemporaryExiledCard(id);return card==null||card.faceDown?TapPaymentText("面朝下的牌","Face-down card","裏向きのカード"):GamePreferences.CardFaceName(card.name);
        }));
    }
    private void PresentTemporaryExile(RectTransform parent,ForgeCardView card,Vector2 size)
    {
        int[] ids=card.temporaryExileIds??Array.Empty<int>();
        if(ids.Length>0) {
            var linked=TemporaryExiledCard(ids[0]);Sprite art=null;
            if(linked!=null&&!linked.faceDown)TryBattlefieldArt(linked,out art);else CardImageLibrary.TryLoadCardBack(out art);
            var tucked=Panel(parent,"TemporaryExile_"+ids[0],new Vector2(8,-20),size,Ink);
            var image=tucked.GetComponent<Image>();image.sprite=art;image.preserveAspect=false;image.raycastTarget=false;
            if(linked!=null&&!linked.faceDown&&art!=null)PresentCompactBattlefieldFace(tucked,art,size,false);
            tucked.SetAsFirstSibling();
        }
        if(ids.Length>1) {
            var badge=Panel(parent,"TemporaryExileCount",Vector2.zero,new Vector2(size.x,22),Ink);
            var label=Label(badge,"Value",TapPaymentText("放逐 ","Exiled ","追放 ")+ids.Length,Vector2.zero,new Vector2(size.x,22),12,Paper);
            float width=Mathf.Min(size.x,Mathf.Ceil(label.preferredWidth)+12),height=Mathf.Max(22,Mathf.Ceil(label.preferredHeight)+4);
            badge.sizeDelta=new Vector2(width,height);label.rectTransform.sizeDelta=new Vector2(width-4,height);
            badge.anchoredPosition=new Vector2(0,-size.y*.5f+height*.5f+2);label.raycastTarget=false;
        }
    }
    private static bool ShouldShowSpentMana(ForgeCardView card)
    {
        if(card==null||card.faceDown||string.IsNullOrEmpty(card.spentMana))return false;
        return ManaPaymentAffectsRules(card);
    }
    private static bool ManaPaymentAffectsRules(ForgeCardView card)
    {
        if(card==null||card.faceDown)return false;
        // Keep the receipt in the snapshot, but only annotate cards whose rules
        // depend on that payment. Native rules remain English across UI locales.
        if((card.keywords??Array.Empty<string>()).Any(k=>string.Equals(k,"Sunburst",StringComparison.OrdinalIgnoreCase)))return true;
        return new[]{card.originalRules,card.currentRules,card.originalAbilityText,card.currentAbilityText}.Any(text=>
            !string.IsNullOrEmpty(text)&&(text.IndexOf("spent to cast",StringComparison.OrdinalIgnoreCase)>=0
                ||System.Text.RegularExpressions.Regex.IsMatch(text.Replace("\\n","\n"),@"(?:^|[\r\n])\s*(?:Sunburst|Converge)\b",System.Text.RegularExpressions.RegexOptions.IgnoreCase)));
    }
    private void PresentSpentMana(RectTransform parent,ForgeCardView card,Vector2 position,float width)
    {
        var badge=parent.Find("SpentMana") as RectTransform;
        if(!ShouldShowSpentMana(card)){if(badge!=null)badge.gameObject.SetActive(false);return;}
        if(badge==null){badge=Panel(parent,"SpentMana",position,new Vector2(width,28),Ink);badge.anchorMin=badge.anchorMax=new Vector2(.5f,1);}
        badge.gameObject.SetActive(true);badge.anchoredPosition=position;
        var label=badge.Find("Label")?.GetComponent<Text>()??Label(badge,"Label","",Vector2.zero,new Vector2(width,28),15,Paper);
        label.text=TapPaymentText("已支付 ","Spent ","支払済み ")+card.spentMana;label.raycastTarget=false;
        SymbolizeStackLabel(label);
    }
}
