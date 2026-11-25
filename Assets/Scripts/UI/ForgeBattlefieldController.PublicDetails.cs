using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public readonly struct BattlefieldExilePreview
{
    public readonly struct Face
    {
        public readonly bool present,faceUp;
        public readonly Sprite art;
        public readonly int colorBits;
        public Face(bool faceUp,Sprite art,int colorBits)
        {
            present=true;this.faceUp=faceUp;this.art=art;this.colorBits=colorBits;
        }
    }
    public readonly Face first,second;
    // The original battlefield laid the full-size lower card eight units right and twenty down.
    public static readonly Vector2 CanvasOffset=new Vector2(8,-20);
    public static readonly Vector2 SecondOffset=new Vector2(.055f,-.045f);

    public BattlefieldExilePreview(Face first,Face second)
    {
        this.first=first;this.second=second;
    }
}

public sealed partial class ForgeBattlefieldController
{
    private ForgeCardView TemporaryExiledCard(int id)=>(view?.players??Array.Empty<ForgePlayerView>())
        .SelectMany(p=>p.exileCards??Array.Empty<ForgeCardView>()).FirstOrDefault(c=>c.id==id);
    private string ExileCardName(ForgeCardView c)
    {
        string name=c?.faceDown==false?c.name:service?.IsSpectator==true?null:
            !string.IsNullOrEmpty(c?.knownFaceName)?c.knownFaceName:c?.draftedFaceName;
        return string.IsNullOrEmpty(name)?TapPaymentText("面朝下的牌","Face-down card","裏向きのカード"):GamePreferences.CardFaceName(name);
    }
    private string TemporaryExileDescription(ForgeCardView source)
    {
        var ids=source.temporaryExileIds??Array.Empty<int>();if(ids.Length==0)return "";
        string heading=TapPaymentText("以此牌放逐：","Exiled with this card: ","これにより追放：");
        return heading+string.Join("、",ids.Select(id=>{
            var card=TemporaryExiledCard(id);string name=ExileCardName(card);
            return card?.faceDown==true && !string.IsNullOrEmpty(card.draftedFaceName) && service?.IsSpectator!=true
                ? name+TapPaymentText("（抽选时所见）"," (as drafted)","（ドラフト時）"):name;
        }));
    }
    private BattlefieldExilePreview ExilePreview(ForgeCardView first,ForgeCardView second,int count)
    {
        BattlefieldExilePreview.Face Face(ForgeCardView card)
        {
            bool faceUp=card!=null && !card.faceDown;
            Sprite art;
            if(faceUp)TryBattlefieldArt(card,out art);
            else CardImageLibrary.TryLoadCardBack(out art);
            return new BattlefieldExilePreview.Face(faceUp,art,faceUp?BattlefieldWorldCard.FrameColorBits(card):0);
        }
        return new BattlefieldExilePreview(count>0?Face(first):default,count>1?Face(second):default);
    }
    private void PresentTemporaryExile(RectTransform parent,BattlefieldExilePreview preview,Vector2 size)
    {
        var center=BattlefieldExilePreview.CanvasOffset;
        void Place(BattlefieldExilePreview.Face face,string name,Vector2 position)
        {
            if(!face.present)return;
            BattlefieldWorldCard.FramePalette(face.colorBits,out var left,out var right);
            var frame=Color.Lerp(left,right,.5f);
            var layer=Panel(parent,name,position,size,frame);
            var image=layer.GetComponent<Image>();
            image.sprite=face.art;image.raycastTarget=false;
            if(face.faceUp && face.art!=null)PresentCompactBattlefieldFace(layer,face.art,size,false,frame);
            else if(face.art!=null)image.color=Color.white;
            layer.SetAsFirstSibling();
        }
        Place(preview.first,"TemporaryExile_First",center);
        Place(preview.second,"TemporaryExile_Second",center+BattlefieldExilePreview.SecondOffset*size.x);
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
