using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public sealed partial class MainMenuController
{
    private static readonly string[] PuzzleZones = { "战场", "手牌", "牌库", "坟墓场", "放逐区" };
    private static readonly string[] PuzzlePhases = { "战斗前主阶段", "战斗开始步骤", "战斗后主阶段", "结束步骤" };
    private const CardType PermanentTypes = CardType.Artifact | CardType.Battle | CardType.Creature |
        CardType.Enchantment | CardType.Land | CardType.Planeswalker;

    private static Button PuzzleButton(Transform parent, string caption, float x, float y, float width, Action click, bool primary = false)
    {
        var button = ReplayWidgets.Button(parent, caption, x, y, width, click, primary);
        MenuVisualTheme.StyleDeckTool(button, primary);
        return button;
    }

    private Dropdown PuzzlePicker(Transform parent, string name, float x, float y, float width, string[] options, int selected = 0)
    {
        var picker = Instantiate(deckDropdown, parent);
        picker.name = name;
        picker.onValueChanged = new Dropdown.DropdownEvent();
        var rect = (RectTransform)picker.transform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f,.5f);
        rect.anchoredPosition = new Vector2(x,y);
        rect.sizeDelta = new Vector2(width,52);
        picker.ClearOptions(); picker.AddOptions(options.ToList());
        picker.SetValueWithoutNotify(selected); picker.RefreshShownValue();
        MenuVisualTheme.StyleArchiveDropdown(picker);
        return picker;
    }

    private static InputField PuzzleInput(Transform parent, string name, string initial, float x, float y, float width, int limit, bool integer = false)
    {
        var field = ReplayWidgets.Input(parent, initial ?? "", x, y, width);
        field.name = name;
        if (integer) field.contentType = InputField.ContentType.IntegerNumber;
        field.characterLimit = limit;
        MenuVisualTheme.StyleSearchInput(field);
        return field;
    }

    private void OpenPuzzleLibrary(string selectedId = null, LocalPuzzle restart = null)
    {
        if (AwaitCardData(() => OpenPuzzleLibrary(selectedId, restart))) return;
        SetScreenVisible(startScreenGroup, false);
        var root = ShowLocalPage("LocalPuzzles","PuzzleLibrary","残局挑战","返回玩法选择",OpenLocalPlayPage);
        var shelf = MenuVisualTheme.ArchiveSection(root,"PuzzleShelf","关卡列表",-544,-28,552,710,MenuVisualTheme.SideAccent);
        var brief = MenuVisualTheme.ArchiveSection(root,"PuzzleBrief","关卡详情",286,-28,1068,710,MenuVisualTheme.MainAccent);
        var status = ReplayWidgets.Label(root,"",0,-416,1640,50,20);
        status.color=MenuVisualTheme.Silver;

        const int perPage=6;
        var rows=new Button[perPage];
        var titles=new Text[perPage];
        var selectedMarks=new Image[perPage];
        var list=new List<LocalPuzzle>();
        LocalPuzzle selected=null;
        int page=0;
        var pager=ReplayWidgets.Label(shelf,"",0,-229,140,42,20,TextAnchor.MiddleCenter);
        Button prev=null,next=null,delete=null;
        prev=PuzzleButton(shelf,"上一页",-165,-229,130,()=>{page--;DrawList();});
        next=PuzzleButton(shelf,"下一页",165,-229,130,()=>{page++;DrawList();});
        PuzzleButton(shelf,"打开残局文件夹",-77,-307,291,()=>{
            try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(LocalPuzzleStore.DirectoryPath){UseShellExecute=true}); }
            catch(Exception error){status.text=error.Message;}
        });

        var title=ReplayWidgets.Label(brief,"",0,239,995,54,40);
        MenuTypography.Use(title,MenuTypography.Face.Heading);
        title.GetComponent<PreferenceText>().Literal=true;
        title.resizeTextForBestFit=true; title.resizeTextMinSize=20; title.resizeTextMaxSize=40;
        var author=ReplayWidgets.Label(brief,"",0,189,995,44,28);
        author.name="PuzzleAuthor";
        author.color=MenuVisualTheme.Silver;
        author.resizeTextForBestFit=true; author.resizeTextMinSize=24; author.resizeTextMaxSize=28;
        author.GetComponent<PreferenceText>().Literal=true;
        var objective=ReplayWidgets.Label(brief,"",0,146,995,36,24);
        objective.color=MenuVisualTheme.MainAccent;
        var descriptionArea=ReplayWidgets.Panel(brief,"Description",0,86,995,72,Color.clear);
        var objectivePosition=objective.rectTransform.anchoredPosition;
        var descriptionTop=descriptionArea.offsetMax;
        descriptionArea.gameObject.AddComponent<RectMask2D>();
        var descriptionScroll=descriptionArea.gameObject.AddComponent<ScrollRect>();
        descriptionScroll.viewport=descriptionArea;descriptionScroll.horizontal=false;
        descriptionScroll.movementType=ScrollRect.MovementType.Clamped;
        var description=CardSymbolText.Create(descriptionArea,"PuzzleDescription",23,MenuVisualTheme.ArchivePaper);
        description.alignment=TMPro.TextAlignmentOptions.TopLeft;
        description.textWrappingMode=TMPro.TextWrappingModes.Normal;
        var descriptionRect=description.rectTransform;
        descriptionRect.anchorMin=Vector2.up;descriptionRect.anchorMax=Vector2.one;descriptionRect.pivot=Vector2.up;
        descriptionRect.sizeDelta=Vector2.zero;
        descriptionScroll.content=descriptionRect;
        description.gameObject.AddComponent<ContentSizeFitter>().verticalFit=ContentSizeFitter.FitMode.PreferredSize;
        description.GetComponent<PreferenceText>().Literal=true;

        var life=new Text[2];
        var contents=new Text[2];
        var faces=new Image[2,3];
        for(int side=0;side<2;side++)
        {
            var plate=ReplayWidgets.Panel(brief,"Seat"+side,side==0?-254:254,-65,490,220,MenuVisualTheme.Recess);
            MenuVisualTheme.SectionTop(plate,side==0?MenuVisualTheme.MainAccent:MenuVisualTheme.SideAccent);
            life[side]=ReplayWidgets.Label(plate,"",0,77,454,47,25);
            MenuTypography.Use(life[side],MenuTypography.Face.Heading);
            for(int i=0;i<3;i++)
            {
                var image=ReplayWidgets.Rect(plate,"Card"+i,-183+i*90,-22,83,117).gameObject.AddComponent<Image>();
                image.preserveAspect=true; image.raycastTarget=false; faces[side,i]=image;
            }
            contents[side]=ReplayWidgets.Label(plate,"",144,-20,160,154,18);
        }
        PuzzleButton(shelf,"刷新",154,-307,144,()=>Reload(selected?.LibraryKey));
        PuzzleButton(root,"新建残局",294,401,180,()=>OpenPuzzleEditor(null));
        PuzzleButton(root,"导入残局",492,401,180,()=>OpenPuzzleFile(root,null,(path,_)=>{
            var imported=LocalPuzzleStore.Import(path);
            Reload(imported.LibraryKey,"已导入「"+imported.title+"」。");
        }));
        PuzzleButton(brief,"导出残局",-239,-276,150,()=>OpenPuzzleFile(root,selected,(path,overwrite)=>{
            LocalPuzzleStore.Export(selected,path,overwrite);
            Reload(selected.LibraryKey,"已导出到 "+path);
        }));
        delete=PuzzleButton(brief,"删除残局",-77,-276,150,()=>{
            var shade=PuzzleModal(root,"DeletePuzzle");
            var box=MenuVisualTheme.ArchiveSection(shade,"Confirm","删除残局",0,0,690,274,MenuVisualTheme.Warning);
            ReplayWidgets.Label(box,"删除「"+selected.title+"」的关卡文件？",0,0,620,82,23);
            PuzzleButton(box,"取消",-145,-87,245,()=>ClosePuzzleModal(root,shade)).Select();
            PuzzleButton(box,"确认删除",145,-87,245,()=>{
                int index=list.IndexOf(selected);
                try {
                    LocalPuzzleStore.Delete(selected);
                    ClosePuzzleModal(root,shade);
                    Reload(list[Math.Max(0,index-1)].LibraryKey,"已删除残局。");
                }
                catch(Exception e){ClosePuzzleModal(root,shade);status.text=e.Message;}
            });
        });
        DisabledButtonVisual.Bind(delete);
        PuzzleButton(brief,"修改残局",-414,-276,174,()=>OpenPuzzleEditor(selected));
        PuzzleButton(brief,"开始挑战",319,-276,359,()=>Launch(selected),true);

        for(int i=0;i<perPage;i++)
        {
            int slot=i;
            var row=PuzzleButton(shelf,"关卡",0,215-i*73,494,()=>Select(list[page*perPage+slot]));
            row.name="PuzzleRow"+i;
            ((RectTransform)row.transform).sizeDelta=new Vector2(494,63);
            var text=row.GetComponentInChildren<Text>();
            text.GetComponent<PreferenceText>().Literal=true;
            text.rectTransform.sizeDelta=new Vector2(449,55);
            text.rectTransform.anchoredPosition=Vector2.zero;
            text.horizontalOverflow=HorizontalWrapMode.Wrap; text.resizeTextForBestFit=true;
            text.resizeTextMinSize=14; text.resizeTextMaxSize=25; text.alignment=TextAnchor.MiddleLeft;
            rows[i]=row; titles[i]=text;
            var mark=ReplayWidgets.Panel(row.transform,"SelectedMark",-241,0,5,43,MenuVisualTheme.SideAccent);
            selectedMarks[i]=mark.GetComponent<Image>();
            selectedMarks[i].raycastTarget=false;
        }

        string AuthorLine(LocalPuzzle puzzle)
        {
            string player=string.IsNullOrWhiteSpace(puzzle.author)
                ? DeckCompanion.Text("未署名","Uncredited","記載なし") : puzzle.author.Trim();
            return DeckCompanion.Text("作者  ","Author  ","作者  ")+player;
        }

        void Launch(LocalPuzzle puzzle)
        {
            try { OnlineMatchService.EnsureInstance().StartLocalPuzzle(puzzle); }
            catch(Exception error){status.text=error.Message;}
        }
        void Reload(string key,string message="")
        {
            try {
                list=LocalPuzzleStore.List(out int unreadable);
                int index=Math.Max(0,list.FindIndex(p=>p.LibraryKey==key));
                page=index/perPage; Select(list[index]);
                status.text=message+(unreadable>0?$"　有 {unreadable} 个残局文件未能读取。":"");
            }
            catch(Exception error){
                if(selected==null){list=LocalPuzzle.Examples();Select(list[0]);}
                status.text=error.Message;
            }
        }
        void DrawList()
        {
            int pages=(list.Count+perPage-1)/perPage;
            pager.text=$"{page+1} / {pages}";
            pager.gameObject.SetActive(pages>1);prev.gameObject.SetActive(pages>1);next.gameObject.SetActive(pages>1);
            prev.interactable=page>0;next.interactable=page+1<pages;
            for(int i=0;i<perPage;i++){
                int index=page*perPage+i;rows[i].gameObject.SetActive(index<list.Count);
                if(index>=list.Count)continue;
                var p=list[index];
                MenuVisualTheme.StyleDeckOption(rows[i],p==selected);
                selectedMarks[i].enabled=p==selected;
                titles[i].text=p.title;
            }
        }
        void Select(LocalPuzzle puzzle)
        {
            if(selected!=puzzle)OnlineMatchService.EnsureInstance().PreparePuzzleOpening(puzzle,root.gameObject);
            selected=puzzle;
            title.text=puzzle.title;
            author.gameObject.SetActive(!puzzle.IsExample);
            if (!puzzle.IsExample) author.text=AuthorLine(puzzle);
            var freedSpace=puzzle.IsExample ? Vector2.up*author.rectTransform.sizeDelta.y : Vector2.zero;
            objective.rectTransform.anchoredPosition=objectivePosition+freedSpace;
            descriptionArea.offsetMax=descriptionTop+freedSpace;
            objective.text=puzzle.Objective+"　 ·　"+(puzzle.firstSide==0?"我方先行":"对方先行")+"　 ·　"+PuzzlePhases[puzzle.phase];
            description.text=puzzle.Brief;
            descriptionScroll.StopMovement();descriptionRect.anchoredPosition=Vector2.zero;
            delete.interactable=!puzzle.IsExample;
            for(int side=0;side<2;side++){
                life[side].text=(side==0?"我方":"对方")+"　 ·　生命 "+(side==0?puzzle.humanLife:puzzle.aiLife);
                var groups=puzzle.cards.Where(r=>r.side==side).ToList();
                var visible=groups.Where(r=>r.zone==0 || r.zone==1 && side==0 || r.zone==3 || r.zone==4)
                    .OrderByDescending(r=>r.zone==1).Select(r=>r.name).Distinct().Take(3);
                int filled=0;
                foreach(string name in visible){
                    var card=cards.FirstOrDefault(c=>c.cardName==name && !c.forgeAmbiguous);
                    if(card!=null && CardImageLibrary.TryLoadThumbnail(card,out var sprite))faces[side,filled++].sprite=sprite;
                }
                for(int i=0;i<3;i++)faces[side,i].enabled=i<filled;
                contents[side].rectTransform.anchoredPosition=new Vector2(-143+filled*93,-20);
                contents[side].text=groups.Count==0?"起始区域为空":string.Join("\n",groups.GroupBy(r=>r.zone)
                    .OrderBy(g=>g.Key).Select(g=>PuzzleZones[g.Key]+"　"+g.Sum(r=>r.count)+" 张"));
            }
            DrawList();
        }
        Reload(selectedId);
        if(restart!=null)Launch(restart);
    }

    private static RectTransform PuzzleModal(RectTransform root,string name)
    {
        var shade=ReplayWidgets.Panel(root,name,0,0,1640,900,new Color(.03f,.04f,.06f,.94f));
        shade.gameObject.AddComponent<CanvasGroup>().ignoreParentGroups=true;
        var group=root.GetComponent<CanvasGroup>();
        if(group==null) group=root.gameObject.AddComponent<CanvasGroup>();
        group.interactable=false;
        return shade;
    }

    private static void ClosePuzzleModal(RectTransform root,RectTransform shade)
    {
        root.GetComponent<CanvasGroup>().interactable=true;
        shade.gameObject.SetActive(false);
        Destroy(shade.gameObject);
    }

    private void OpenPuzzleCardState(RectTransform root,LocalPuzzle draft,PuzzleCard row,CardDefinition card,Action changed)
    {
        var shade=PuzzleModal(root,"PuzzleCardState");
        var special=PuzzleCard.PrintedCounter(card);
        var subtypes=(card.subtypes??"").Split(' ');
        bool aura=subtypes.Contains("Aura");
        bool attaches=aura || subtypes.Contains("Equipment") || subtypes.Contains("Fortification");
        int extra=(special.kind!=null ? 66 : 0)+(attaches ? 66 : 0);
        var panel=MenuVisualTheme.ArchiveSection(shade,"CardState",GamePreferences.CardFaceName(row.name),0,0,760,552+extra,MenuVisualTheme.MainAccent);
        int top=147+extra/2;
        var names=new[]{"横置","召唤失调","已标记伤害","+1/+1 指示物"};
        for(int i=0;i<names.Length;i++) ReplayWidgets.Label(panel,names[i],-167,top-i*66,305,50,22);
        var tap=PuzzlePicker(panel,"PuzzleTapped",153,top,302,new[]{"未横置","横置"},row.tapped ? 1 : 0);
        var sick=PuzzlePicker(panel,"PuzzleSick",153,top-66,302,new[]{"无","有"},row.summoningSick ? 1 : 0);
        var damage=PuzzleInput(panel,"PuzzleDamage",row.damage.ToString(),153,top-132,302,2,true);
        var counters=PuzzleInput(panel,"PuzzleCounters",row.plusCounters.ToString(),153,top-198,302,2,true);
        InputField printed=null;
        int y=top-264;
        if(special.kind!=null)
        {
            ReplayWidgets.Label(panel,special.label,-167,y,305,50,22);
            printed=PuzzleInput(panel,"PuzzlePrintedCounter",(row.overrideCounter?row.counterValue:special.initial).ToString(),153,y,302,2,true);
            y-=66;
        }
        Dropdown attachment=null;
        var targets=new List<(string label,string player,PuzzleCard target,int copy)> { ("未指定",null,null,0) };
        if(attaches)
        {
            if(aura) { targets.Add(("我方玩家","human",null,0)); targets.Add(("对方玩家","ai",null,0)); }
            int selected=0;
            foreach(var group in draft.cards.Where(r=>r.zone==0 && r!=row).GroupBy(r=>(r.side,r.name)))
            {
                string label=(group.Key.side==0?"我方　":"对方　")+GamePreferences.CardFaceName(group.Key.name);
                int total=group.Sum(r=>r.count), ordinal=0;
                foreach(var target in group)
                    for(int i=0;i<target.count;i++)
                    {
                        ordinal++;
                        targets.Add((label+(total>1?"　#"+ordinal:""),null,target,i));
                    }
            }
            if (!string.IsNullOrEmpty(row.attachedTo)) selected=Math.Max(0,targets.FindIndex(t=>t.player==row.attachedTo || (t.target?.key==row.attachedTo && t.copy==row.targetCopy)));
            ReplayWidgets.Label(panel,"结附对象",-167,y,305,50,22);
            attachment=PuzzlePicker(panel,"PuzzleAttachment",103,y,402,targets.Select(t=>t.label).ToArray(),selected);
        }
        var feedback=ReplayWidgets.Label(panel,$"应用于这一行的 {row.count} 张牌。"+(attaches?"对象须符合牌面条件。":""),0,-126-extra/2,660,48,19);
        void Close() => ClosePuzzleModal(root,shade);
        PuzzleButton(panel,"取消",-165,-212-extra/2,282,Close).Select();
        PuzzleButton(panel,"应用",165,-212-extra/2,282,()=>{
            int n=special.initial;
            if(!int.TryParse(damage.text,out int d)||!int.TryParse(counters.text,out int c)||d<0||c<0||d>99||c>99 ||
                (printed!=null && (!int.TryParse(printed.text,out n)||n<0||n>99)))
            { feedback.text="伤害与指示物数量应为 0～99。"; return; }
            row.tapped=tap.value==1; row.summoningSick=sick.value==1; row.damage=d; row.plusCounters=c;
            row.overrideCounter=n!=special.initial; row.counterValue=row.overrideCounter?n:0;
            if(attachment!=null)
            {
                var target=targets[attachment.value];
                if(target.target!=null && string.IsNullOrEmpty(target.target.key)) target.target.key=Guid.NewGuid().ToString("N");
                row.attachedTo=target.player??target.target?.key; row.targetCopy=target.copy;
            }
            changed(); Close();
        },true);
    }
}
