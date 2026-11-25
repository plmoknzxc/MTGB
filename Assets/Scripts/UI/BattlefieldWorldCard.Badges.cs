using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

public sealed partial class BattlefieldWorldCard
{
    sealed class WorldBadge { public MeshRenderer back,icon;public TextMeshPro text;public bool hasIcon,showBack; }
    readonly List<WorldBadge> keywordBadges=new(),counterBadges=new();
    int keywordBadgeCount,counterBadgeCount;
    WorldBadge Badge(List<WorldBadge> list,int index,string prefix)
    {
        if(index==list.Count)list.Add(new WorldBadge{
            back=Surface(prefix+" backing "+index,Vector3.zero,Vector2.one,new Color(.025f,.045f,.065f,.94f)),
            icon=Surface(prefix+" icon "+index,Vector3.zero,Vector2.one,Color.white),
            text=Label(prefix+" value "+index,Vector3.zero,Vector2.one,.88f,TextAlignmentOptions.Center)});
        return list[index];
    }
    void PlaceBadge(WorldBadge b,float x,float y,float width,string text,Sprite sprite,Color color,bool granted=false,float height=.155f,float iconSize=.135f,bool bareIcon=false)
    {
        b.back.transform.localPosition=new Vector3(x,y,-.041f);b.back.transform.localScale=new Vector3(width,height,1);
        colors[surfaces.IndexOf(b.back)]=granted?new Color(.12f,.51f,.65f,.98f):new Color(.025f,.045f,.065f,.94f);
        b.back.GetPropertyBlock(properties);properties.SetFloat("_Radius",.12f);b.back.SetPropertyBlock(properties);
        b.hasIcon=sprite!=null;
        b.showBack=!bareIcon||granted||!string.IsNullOrEmpty(text);
        float iconX=string.IsNullOrEmpty(text)?x:x-width*.5f+iconSize*.5f+.005f;
        b.icon.transform.localPosition=new Vector3(iconX,y,-.047f);b.icon.transform.localScale=Vector3.one*iconSize;
        colors[surfaces.IndexOf(b.icon)]=Color.white;
        SetBadgeArt(b.icon,sprite);
        float textWidth=width-(b.hasIcon?iconSize+.01f:.02f);
        b.text.text=text;b.text.transform.localPosition=new Vector3(x+(b.hasIcon?iconSize*.5f:0),y,-.049f);
        b.text.rectTransform.sizeDelta=new Vector2(Mathf.Max(.015f,textWidth),height-.01f);
        b.text.color=color;b.text.fontStyle=FontStyles.Bold;b.text.enableAutoSizing=true;b.text.fontSizeMin=.55f;b.text.fontSizeMax=.90f;
        b.text.overflowMode=TextOverflowModes.Ellipsis;
    }
    void SetBadgeArt(MeshRenderer renderer,Sprite sprite)
    {
        var rect=sprite!=null?sprite.textureRect:new Rect(0,0,1,1);
        var texture=sprite!=null?sprite.texture:Texture2D.whiteTexture;
        renderer.GetPropertyBlock(properties);
        properties.SetTexture("_MainTex",texture);
        properties.SetVector("_UVRect",new Vector4(rect.x/texture.width,rect.y/texture.height,rect.width/texture.width,rect.height/texture.height));
        properties.SetFloat("_Radius",0);
        renderer.SetPropertyBlock(properties);
    }
    void PlaceCounter(string kind,int count,float y)
    {
        const float height=.158f,iconSize=.108f,right=.463f;
        var badge=Badge(counterBadges,counterBadgeCount++,"Counter");
        string digits=count.ToString();
        float width=Mathf.Min(.49f,.31f+.056f*(digits.Length-1));
        float left=right-width;
        badge.showBack=badge.hasIcon=true;
        badge.back.transform.localPosition=new Vector3(left+width*.5f,y,-.041f);
        badge.back.transform.localScale=new Vector3(width,height,1);
        colors[surfaces.IndexOf(badge.back)]=Color.white;
        SetBadgeArt(badge.back,PermanentCounterIcon.Plate(kind));

        var symbol=PermanentCounterIcon.Symbol(kind);
        float divider=left+width*.41f;
        badge.icon.transform.localPosition=new Vector3(left+width*.22f,y,-.047f);
        badge.icon.transform.localScale=Vector3.one*iconSize;
        SetBadgeArt(badge.icon,symbol);

        var number=badge.text;
        number.font=table.numberFont!=null?table.numberFont:table.font;
        number.fontStyle=FontStyles.Bold;
        number.text=digits;
        number.color=new Color(.98f,.96f,.91f);
        number.transform.localPosition=new Vector3((divider+right-.018f)*.5f,y,-.049f);
        number.rectTransform.sizeDelta=new Vector2(right-divider-.028f,height-.024f);
        number.enableAutoSizing=true;number.fontSizeMin=.50f;number.fontSizeMax=1.05f;
    }
    void BindWorldBadges(ForgeCardView value)
    {
        keywordBadgeCount=counterBadgeCount=0;
        if(!compactFace||hand||value.faceDown)return;
        var counters=(value.counters??Array.Empty<ForgeCardCounter>()).Where(c=>c.count>0&&!(compactSaga&&c.kind=="LORE")
            &&!(value.planeswalker&&c.kind=="LOYALTY")&&!(value.battle&&c.kind=="DEFENSE")).ToArray();
        // Cycling and its landcycling variants are hand-zone actions. Forge's
        // keyword projection may retain them on a permanent's view, but they
        // do not describe an ability usable from the battlefield.
        var keywords=(value.keywords??Array.Empty<string>()).Where(k=>KeywordIcons.IsDisplayable(k)
            &&!k.Trim().EndsWith("CYCLING",StringComparison.OrdinalIgnoreCase)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        float used=0,left=-.43f,available=compactRoom&&counters.Length>0?.46f:.86f;
        float y=compactRoom?.18f:-aspect*.5f+.29f;
        float gap=.012f,slot=.16f,height=.155f,iconSize=.135f;
        if(value.creature){
            if(keywords.Length>1)
                keywords=keywords.OrderByDescending(k=>string.Equals(k,"DEFENDER",StringComparison.OrdinalIgnoreCase))
                    .ThenByDescending(k=>KeywordIcons.TryLoad(k,out _)).ToArray();
            // Size the row from the actual P/T shield, including large and signed values.
            available=statsPlate.transform.localPosition.x-statsPlate.transform.localScale.x*.5f-left-.025f;
            y=statsPlate.transform.localPosition.y;
            int count=Mathf.Clamp(keywords.Length,1,4);
            slot=Mathf.Clamp((available-gap*(count-1))/count,.125f,.19f);
            height=compactRoom?.13f:.18f;
            iconSize=Mathf.Min(slot-.008f,height);
        }
        for(int i=0;i<keywords.Length;i++){
            KeywordIcons.TryLoad(keywords[i],out var sprite);
            string parameter=KeywordIcons.Parameter(value,keywords[i],out bool mana);
            if(mana)parameter=Symbols(parameter);
            string text=sprite!=null?parameter:keywords[i].Equals("UNBLOCKABLE",StringComparison.OrdinalIgnoreCase)
                ?GamePreferences.Language=="zh-CN"?"不能被阻挡":GamePreferences.Language=="ja-JP"?"ブロックされない":"Can't be blocked"
                :DeckKeywordGlossary.KeywordName(keywords[i]);
            float width=sprite==null?Mathf.Min(available,value.creature?.50f:.78f):string.IsNullOrEmpty(parameter)?slot:iconSize+.155f;
            float remainderWidth=value.creature?Mathf.Min(slot,.134f):.16f;
            bool overflow=used+width>available+.0001f||(i<keywords.Length-1&&used+width+gap+remainderWidth>available+.0001f);
            if(overflow){width=remainderWidth;sprite=null;text="+"+(keywords.Length-i);}
            var b=Badge(keywordBadges,keywordBadgeCount++,"Keyword");
            PlaceBadge(b,left+used+width*.5f,y,width,text,sprite,Color.white,
                !overflow&&(value.grantedKeywords??Array.Empty<string>()).Contains(keywords[i],StringComparer.OrdinalIgnoreCase),
                height,iconSize,bareIcon:value.creature);
            used+=width+gap;if(overflow)break;
        }
        var represented=new HashSet<string>();
        float counterY=compactRoom?.18f:aspect*.5f-.11f;
        const float counterStep=.169f;
        if(compactSaga){
            int lore=(value.counters??Array.Empty<ForgeCardCounter>()).Where(c=>c.kind=="LORE").Sum(c=>c.count);
            PlaceCounter("LORE",lore,counterY);
            counterY-=counterStep;
        }
        int capacity=compactRoom?1:compactSaga?2:3;
        for(int i=0;i<Math.Min(capacity,counters.Length);i++){
            bool more=i==capacity-1&&counters.Length>capacity;
            PlaceCounter(more?"MORE":counters[i].kind,more?counters.Length-i:counters[i].count,counterY-i*counterStep);
        }
        foreach(var c in counters)represented.Add((c.label??c.kind)+" "+c.count);
        status.text=string.Join(" · ",(status.text??"").Split('·').Select(s=>s.Trim()).Where(s=>!represented.Contains(s)));
    }
    void SyncWorldBadgeVisibility(bool visible)
    {
        void Sync(List<WorldBadge> list,int count){for(int i=0;i<list.Count;i++){
            bool show=visible&&i<count;list[i].back.enabled=show&&list[i].showBack;list[i].text.enabled=show;list[i].icon.enabled=show&&list[i].hasIcon;
        }}
        Sync(keywordBadges,keywordBadgeCount);Sync(counterBadges,counterBadgeCount);
    }
}
