using System;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.UI;

public sealed partial class ForgeBattlefieldController
{
    private bool IsRedesignLand(ForgeCardView card,bool hand)=>IsRedesignLayout&&!hand&&card.land&&!card.creature&&!card.faceDown&&card.zone=="Battlefield";

    private string ArenaPresentationGroupKey(ForgeCardView card)
    {
        if(!IsRedesignLand(card,false)||!card.landPresentationKnown||!card.landStackable)return ArenaGroupKey(card);
        if((view?.stackItems??Array.Empty<ForgeStackItem>()).Any(e=>(e.targets??Array.Empty<ForgeStackTarget>()).Any(t=>t.kind=="card"&&!t.leftZone&&t.zone=="Battlefield"&&t.id==card.id)))
            return ArenaGroupKey(card)+"/target:"+card.id;
        int option=OptionIndex("card:"+card.id);
        string eligibility=option<0?"none":view.request.options[option].disabled+"/"+view.request.options[option].requiresPaymentCheck+"/"+view.request.options[option].selected;
        return ArenaGroupKeyCore(card,true)+"/land:"+card.landManaColors+"/option:"+eligibility;
    }

    private static int RedesignLandColors(ForgeCardView card)
    {
        if(card.landPresentationKnown)return card.landManaColors&31;
        // Older recordings have no production projection. Use only explicit
        // public basic types / mana-production clauses, never printed card color.
        int colors=0;
        var types=new[]{"Plains","Island","Swamp","Mountain","Forest"};
        for(int i=0;i<types.Length;i++)if((card.basicLandTypes??Array.Empty<string>()).Contains(types[i]))colors|=1<<i;
        foreach(Match clause in Regex.Matches((card.currentRules??"").Replace("\\n","\n"),@"(?:\bAdd\b|加える|加)\s*([^\r\n。.]+)",RegexOptions.IgnoreCase))
            foreach(Match symbol in Regex.Matches(clause.Groups[1].Value,@"\{([WUBRG])\}",RegexOptions.IgnoreCase)){
                int index="WUBRG".IndexOf(symbol.Groups[1].Value.ToUpperInvariant());if(index>=0)colors|=1<<index;
            }
        return colors;
    }

    private static void RedesignLandPalette(ForgeCardView card,out Color left,out Color right)
    {
        int mask=RedesignLandColors(card);
        var palette=new[]{new Color(.91f,.85f,.65f),new Color(.26f,.57f,.88f),new Color(.57f,.39f,.70f),new Color(.86f,.34f,.24f),new Color(.29f,.65f,.40f)};
        var colors=Enumerable.Range(0,5).Where(i=>(mask&(1<<i))!=0).Select(i=>palette[i]).ToArray();
        left=right=colors.Length==0?new Color(.48f,.51f,.53f):colors.Length>2?new Color(.83f,.66f,.30f):colors[0];
        if(colors.Length==2)right=colors[1];
    }

    private static void TintRedesignLandBorder(Image image,ForgeCardView card)
    {
        image.color=Color.white;
        RedesignLandPalette(card,out var left,out var right);
        (image.GetComponent<BattlefieldLandFrameTint>()??image.gameObject.AddComponent<BattlefieldLandFrameTint>()).Configure(left,right);
    }

    private void PresentRedesignLandLayers(RectTransform face,ForgeCardView card,Vector2 size,int count)
    {
        for(int layer=Math.Min(2,count-1);layer>0;layer--){
            var back=Panel(face,"LandStackBack_"+layer,new Vector2(layer*3,layer*3),size,Color.white);
            var image=back.GetComponent<Image>();image.sprite=IntegratedUiUtility.RoundedRect(140,125,9);
            TintRedesignLandBorder(image,card);
            var paper=Panel(back,"Paper",Vector2.zero,size-Vector2.one*4,new Color(.075f,.09f,.10f));
            paper.GetComponent<Image>().sprite=IntegratedUiUtility.RoundedRect(140,125,8);
        }
    }
}
