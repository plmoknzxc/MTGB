using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;

public sealed partial class BattlefieldWorldCard
{
    readonly List<MeshRenderer> sagaSegments=new();
    readonly List<TextMeshPro> sagaNumbers=new();
    int sagaSegmentCount;
    bool compactSaga;
    static readonly Regex SagaChapterLine=new(@"^\s*([IVXLCDM]+(?:\s*[,，、]\s*[IVXLCDM]+)*)\s*[—―－-]\s*",RegexOptions.Multiline);

    static int ChapterNumber(string roman)
    {
        int total=0,previous=0;
        for(int i=roman.Length-1;i>=0;i--){
            int n=roman[i] switch{'I'=>1,'V'=>5,'X'=>10,'L'=>50,'C'=>100,'D'=>500,'M'=>1000,_=>0};
            total+=n<previous?-n:n;previous=n;
        }
        return total;
    }
    void BindSagaChapters(ForgeCardView value)
    {
        compactSaga=false;sagaSegmentCount=0;
        foreach(var label in sagaNumbers)label.text="";
        string type=value.currentType??value.originalType??"";
        if(!compactFace||hand||value.faceDown||value.room||!(type.Contains("Saga",StringComparison.OrdinalIgnoreCase)
            ||type.Contains("传纪")||type.Contains("傳紀")||type.Contains("英雄譚")))return;
        string rules=(value.currentRules??value.originalRules??"").Replace("\\n","\n");
        // A shared effect does not merge the chapter positions on the card.
        var chapters=SagaChapterLine.Matches(rules).Cast<Match>().SelectMany(m=>Regex.Matches(m.Groups[1].Value,@"[IVXLCDM]+").Cast<Match>().Select(n=>n.Value)).Distinct().OrderBy(ChapterNumber).ToList();
        if(chapters.Count==0)return;
        int lore=(value.counters??Array.Empty<ForgeCardCounter>()).Where(c=>c.kind=="LORE").Sum(c=>c.count);
        compactSaga=true;sagaSegmentCount=chapters.Count;
        // Remove only the lore-counter entry now represented by this badge;
        // damage, other counters and other status entries remain untouched.
        var loreEntries=(value.counters??Array.Empty<ForgeCardCounter>()).Where(c=>c.kind=="LORE").Select(c=>(c.label??c.kind)+" "+c.count).ToHashSet();
        status.text=string.Join(" · ",(status.text??"").Split('·').Select(s=>s.Trim()).Where(s=>!loreEntries.Contains(s)));
        float span=Mathf.Min(.48f,aspect-.52f),gap=.018f,height=(span-gap*(chapters.Count-1))/chapters.Count;
        for(int i=0;i<chapters.Count;i++){
            if(i==sagaSegments.Count){
                sagaSegments.Add(Surface("Saga chapter segment "+i,Vector3.zero,Vector2.one,Color.white));
                sagaNumbers.Add(Label("Saga chapter numeral "+i,Vector3.zero,Vector2.one,.88f,TextAlignmentOptions.Center));
            }
            bool active=ChapterNumber(chapters[i])==lore;
            float y=aspect*.5f-.135f-height*.5f-i*(height+gap);
            var panel=sagaSegments[i];panel.transform.localPosition=new Vector3(-.387f,y,-.033f);
            panel.transform.localScale=new Vector3(.17f,height,1);
            colors[surfaces.IndexOf(panel)]=active?new Color(.92f,.70f,.26f,.98f):new Color(.025f,.045f,.065f,.92f);
            panel.GetPropertyBlock(properties);properties.SetFloat("_Radius",.04f);panel.SetPropertyBlock(properties);
            var label=sagaNumbers[i];label.text=chapters[i];
            label.transform.localPosition=new Vector3(-.387f,y,-.038f);
            label.rectTransform.sizeDelta=new Vector2(.15f,height-.015f);
            label.fontStyle=FontStyles.Bold;label.fontSize=.88f;
            label.enableAutoSizing=true;label.fontSizeMin=.45f;label.fontSizeMax=.88f;
            label.overflowMode=TextOverflowModes.Ellipsis;
            label.color=active?new Color(.06f,.045f,.02f):new Color(.72f,.76f,.78f);
        }
    }
    void SyncSagaVisibility(bool visible)
    {
        for(int i=0;i<sagaSegments.Count;i++){
            bool show=visible&&compactSaga&&i<sagaSegmentCount;
            sagaSegments[i].enabled=show;sagaNumbers[i].enabled=show;
        }
    }
}
