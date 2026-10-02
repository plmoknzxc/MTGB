using UnityEngine;

public sealed partial class ForgeBattlefieldController
{
    private static readonly Color GrantedKeywordColor=new Color(.36f,.72f,1f);
    public static string ModifiedStatsText(ForgeCardView card,bool darkBackground)
    {
        string Value(int value,int baseline) {
            if(!card.hasStatBaseline || value==baseline)return value.ToString();
            string color=value>baseline?(darkBackground?"63E69B":"16894C"):(darkBackground?"FF7184":"D12D46");
            return "<color=#"+color+">"+value+"</color>";
        }
        return Value(card.power,card.basePower)+"/"+Value(card.toughness,card.baseToughness);
    }
}
