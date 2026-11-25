using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public partial class MainMenuController
{
    void OpenPuzzleConditions(RectTransform parent, PuzzleGoal goal)
    {
        var shade=PuzzleModal(parent,"PuzzleConditions");
        var box=MenuVisualTheme.ArchiveSection(shade,"Conditions","胜利条件",0,0,980,795,MenuVisualTheme.SideAccent);
        var rows=goal.conditions.Select(c=>JsonUtility.FromJson<PuzzleCondition>(JsonUtility.ToJson(c))).ToList();
        PuzzleCondition NewRow()=>new PuzzleCondition{metric="cardCount",player="opponent",zone="battlefield",cardType="Creature",op="atMost",value=0};
        if(rows.Count==0)rows.Add(NewRow());
        int index=0;
        Dropdown Picker(string caption,int y,string[] values){
            ReplayWidgets.Label(box,caption,-280,y,292,46,21);
            return PuzzlePicker(box,caption,120,y,585,values);
        }
        var metric=Picker("检查项目",215,new[]{"生命值","本回合受到的伤害","区域内的牌数"});
        var side=Picker("玩家",155,new[]{"我方","对方"});
        var zone=Picker("区域",95,PuzzleCondition.ZoneNames);
        var type=Picker("牌类别",35,new[]{"任意类别"}.Concat(PuzzleCondition.TypeNames).ToArray());
        ReplayWidgets.Label(box,"英文牌名（可留空）",-280,-25,292,46,21);
        var name=PuzzleInput(box,"CardName","",120,-25,585,180);
        var comparison=Picker("比较",-85,new[]{"至少","至多","恰好"});
        ReplayWidgets.Label(box,"数值",-280,-145,292,46,21);
        var value=PuzzleInput(box,"Threshold","",-70,-145,205,10,true);
        var number=ReplayWidgets.Label(box,"",0,286,250,45,22,TextAnchor.MiddleCenter);
        var note=ReplayWidgets.Label(box,"",0,-213,840,48,19,TextAnchor.MiddleCenter);
        Button prev=null,next=null,add=null,remove=null;
        prev=PuzzleButton(box,"上一项",-208,286,150,()=>{if(Store()){index--;Show();}});
        next=PuzzleButton(box,"下一项",208,286,150,()=>{if(Store()){index++;Show();}});
        add=PuzzleButton(box,"添加条件",-181,-271,220,()=>{if(Store()){rows.Add(NewRow());index=rows.Count-1;Show();}});
        remove=PuzzleButton(box,"移除本项",181,-271,220,()=>{rows.RemoveAt(index);index=Math.Min(index,rows.Count-1);Show();});
        PuzzleButton(box,"取消",-181,-338,220,()=>ClosePuzzleModal(parent,shade));
        PuzzleButton(box,"应用条件",181,-338,220,()=>{if(Store()){goal.conditions=rows;ClosePuzzleModal(parent,shade);}},true);
        void FilterControls(int m){zone.interactable=type.interactable=name.interactable=m==2;}
        metric.onValueChanged.AddListener(FilterControls);
        void Show(){
            var row=rows[index];
            number.text=$"条件 {index+1} / {rows.Count}";
            metric.SetValueWithoutNotify(Array.IndexOf(PuzzleCondition.Metrics,row.metric));
            side.SetValueWithoutNotify(Array.IndexOf(PuzzleCondition.Players,row.player));
            comparison.SetValueWithoutNotify(Array.IndexOf(PuzzleCondition.Comparisons,row.op));
            zone.SetValueWithoutNotify(Math.Max(0,Array.IndexOf(LocalPuzzle.Zones,row.zone)));
            type.SetValueWithoutNotify(Array.IndexOf(PuzzleCondition.CardTypes,row.cardType)+1);
            name.text=row.name??"";value.text=row.value.ToString();
            prev.interactable=index>0;next.interactable=index+1<rows.Count;remove.interactable=rows.Count>1;add.interactable=rows.Count<32;
            FilterControls(metric.value);note.text="";
        }
        bool Store(){
            if(!int.TryParse(value.text,out int n) || n<0){note.text="请输入非负整数。";return false;}
            var row=rows[index];
            row.metric=PuzzleCondition.Metrics[metric.value];row.player=PuzzleCondition.Players[side.value];
            row.op=PuzzleCondition.Comparisons[comparison.value];row.value=n;
            row.zone=metric.value==2?LocalPuzzle.Zones[zone.value]:null;
            row.name=metric.value==2 && !string.IsNullOrWhiteSpace(name.text)?name.text.Trim():null;
            row.cardType=metric.value==2 && type.value>0?PuzzleCondition.CardTypes[type.value-1]:null;
            return true;
        }
        Show();
    }
}
