using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public sealed partial class MainMenuController
{
    private sealed class PuzzleEditorSlot
    {
        public PuzzleCard row;
        public PuzzleTableDrop drop;
        public PuzzleTableDrag drag;
        public Button button;
        public Image face;
        public RawImage art;
        public Text empty,caption,sequence;
        public string artName;
    }
    private static readonly string[] PuzzleStarters={"Plains","Island","Swamp","Mountain","Forest","Lightning Bolt"};
    private List<CardDefinition> puzzlePool;
    private Dictionary<string,CardDefinition> puzzleDefinitions;
    private Dictionary<string,string> puzzleNames;
    private Dictionary<CardDefinition,string> puzzleSearchTerms;
    private string puzzleCatalogLanguage;

    private void EnsurePuzzleCatalog()
    {
        if(puzzlePool!=null && puzzleCatalogLanguage==GamePreferences.Language)return;
        puzzleDefinitions=new Dictionary<string,CardDefinition>(StringComparer.OrdinalIgnoreCase);
        foreach(var card in cards)
            if(!card.forgeAmbiguous && !string.IsNullOrEmpty(card.forgeSet) && !string.IsNullOrWhiteSpace(card.cardName))
                puzzleDefinitions.TryAdd(card.cardName,card);
        puzzlePool=puzzleDefinitions.Values.OrderBy(c=>c.cardName,StringComparer.OrdinalIgnoreCase).ToList();
        puzzleNames=new Dictionary<string,string>(puzzlePool.Count,StringComparer.OrdinalIgnoreCase);
        puzzleSearchTerms=new Dictionary<CardDefinition,string>(puzzlePool.Count);
        foreach(var card in puzzlePool)
        {
            string display=GamePreferences.CardName(card);
            puzzleNames.Add(card.cardName,display);
            string terms=card.cardName+"\n"+display;
            if(card.features!=null)
                foreach(var face in card.features)
                    if(face!=null && !string.IsNullOrEmpty(face.cardName) && face.cardName!=card.cardName)
                        terms+="\n"+face.cardName+"\n"+GamePreferences.FeatureName(face);
            puzzleSearchTerms.Add(card,terms.ToUpperInvariant());
        }
        puzzleCatalogLanguage=GamePreferences.Language;
    }

    private void OpenPuzzleEditor(LocalPuzzle source, OnlineMatchService match = null)
    {
        var draft=source?.Copy() ?? new LocalPuzzle { title="新残局" };
        if(source==null || source.IsExample)
        {
            draft.id=Guid.NewGuid().ToString("N");
            var profile=OnlineClientProfileStore.Load(OnlineClientProfileStore.ResolveCurrentProfileId());
            draft.author=string.IsNullOrWhiteSpace(profile.displayName)
                ? OnlineClientProfileStore.DefaultDisplayName(profile.id) : profile.displayName;
            if(source!=null)draft.title+="（副本）";
        }
        draft.PrepareTable();
        EnsurePuzzleCatalog();
        var pool=puzzlePool;var defs=puzzleDefinitions;var names=puzzleNames;
        bool BackRow(string name)=>defs.TryGetValue(name,out var def) && def.HasType(CardType.Land) && !def.HasType(CardType.Creature);
        draft.PrepareTableRows(BackRow);
        string original=JsonUtility.ToJson(draft);
        RectTransform root=null,confirm=null;
        void Return()
        {
            if (match != null) match.ReturnFromPuzzleEditor();
            else OpenPuzzleLibrary(source?.LibraryKey);
        }
        void Back()
        {
            if(JsonUtility.ToJson(draft)==original){Return();return;}
            if(confirm!=null)return;
            confirm=PuzzleModal(root,"UnsavedPuzzle");
            ReplayWidgets.Label(confirm,"放弃未保存的修改并返回？",0,55,740,75,32,TextAnchor.MiddleCenter);
            PuzzleButton(confirm,"继续编辑",-165,-60,260,()=>{ClosePuzzleModal(root,confirm);confirm=null;}).Select();
            PuzzleButton(confirm,"放弃修改",165,-60,260,Return);
        }
        string backLabel = match == null ? "返回残局列表" : match.IsInMatch ? "返回当前对局" : "返回残局结算";
        root=ShowLocalPage("LocalPuzzleEditor","PuzzleEditor","残局编辑器",backLabel,Back);
        var previewObject=new GameObject("PuzzleCardPreview",typeof(RectTransform),typeof(CanvasGroup));
        previewObject.transform.SetParent(root,false);
        previewObject.GetComponent<CanvasGroup>().blocksRaycasts=false;
        var preview=previewObject.AddComponent<DeckHoverArtPopup>();
        previewObject.SetActive(false);
        var catalog=MenuVisualTheme.ArchiveSection(root,"Catalog","卡库",-620,-3,390,670,MenuVisualTheme.LibraryAccent);
        var search=PuzzleInput(catalog,"PuzzleCardSearch","",0,205,348,80);
        ReplayWidgets.Label(catalog,"搜索卡名（中文或英文）",0,247,342,32,16);
        string Name(PuzzleCard r)=>names.TryGetValue(r.name,out var n)?n:r.name;
        string piece=null;
        PuzzleCard selected=null;
        int catalogPage=0;
        const int cols=LocalPuzzle.TableColumns, pageSize=2*cols, hitCount=4;
        int[] zones={0,0}, pages={0,0};
        var mat=ReplayWidgets.Panel(root,"PuzzleTable",201,23,1226,618,new Color(.075f,.10f,.13f,.98f));
        string texture=MenuThemeCatalog.Current.BattlefieldBackdrop;
        if(!string.IsNullOrEmpty(texture))
        {
            var ground=ReplayWidgets.Rect(mat,"Playmat",0,0,1224,616).gameObject.AddComponent<RawImage>();
            ground.texture=MenuThemeCatalog.Current.Load<Texture2D>(texture);
            ground.color=new Color(.75f,.78f,.80f,ground.texture==null?0:.74f);ground.raycastTarget=false;
        }
        var lanes=new RectTransform[2];
        for(int s=0;s<2;s++)lanes[s]=ReplayWidgets.Rect(mat,s==0?"OwnHalf":"OpponentHalf",0,s==0?-146:146,1220,288);
        var toolbar=ReplayWidgets.Panel(root,"Selection",201,-322,1226,61,MenuVisualTheme.ArchivePanel);
        var reader=ReplayWidgets.Panel(mat,"CatalogReader",0,0,1226,618,MenuVisualTheme.ArchivePanel);
        reader.gameObject.SetActive(false);
        var readerEmpty=ReplayWidgets.Label(reader,"没有匹配的卡牌",0,0,600,52,24,TextAnchor.MiddleCenter);
        var readerArt=ReplayWidgets.Rect(reader,"SelectedFaces",0,0,1,1).gameObject.AddComponent<DeckHoverArtPopup>();
        readerArt.gameObject.AddComponent<CanvasGroup>().blocksRaycasts=false;
        CardDefinition reading=null;
        var summary=ReplayWidgets.Label(toolbar,"",-455,0,286,48,18);
        var status=ReplayWidgets.Label(root,"悬停查看卡面，拖牌上桌或选牌后点击空位。",-399,-405,821,66,19);
        var hits=new List<CardDefinition>();
        var results=new Button[hitCount];
        var thumbnails=new Image[hitCount];
        var missingArt=new Text[hitCount];
        var displayed=new CardDefinition[hitCount];
        Button browse=null;
        void SetReader(bool open)
        {
            preview.Hide();
            reader.gameObject.SetActive(open);toolbar.gameObject.SetActive(!open);
            browse.GetComponentInChildren<Text>().text=open?"返回":"查看详情";
            status.text=open?"点选左侧卡牌查看大图，也可以直接拖牌上桌。":
                piece!=null?"已选「"+names[piece]+"」，点击空位放置。":"悬停查看卡面，拖牌上桌或选牌后点击空位。";
            if(!open){reading=null;return;}
            reader.SetAsLastSibling();
            CatalogPage();
        }
        var pageLabel=ReplayWidgets.Label(catalog,"",0,-268,158,50,19,TextAnchor.MiddleCenter);
        Button prev=null,next=null,edit=null,earlier=null,later=null,together=null,duplicate=null,remove=null;
        var tabs=new Button[2,PuzzleZones.Length];
        int[] paintedZone={-1,-1};
        var legends=new Text[2];
        var back=new Button[2];var more=new Button[2];var pageText=new Text[2];
        var slots=new PuzzleEditorSlot[2,pageSize];
        browse=PuzzleButton(catalog,"查看详情",104,295,134,()=>SetReader(!reader.gameObject.activeSelf));
        void DropPoint(Transform tr,int side,int zone,int place)
        {
            var drop=tr.gameObject.AddComponent<PuzzleTableDrop>();drop.side=side;drop.zone=zone;drop.place=place;
        }
        PuzzleTableDrag Drag(Button b,string name,Action<int,int,int> action)
        {
            var d=b.GetComponent<PuzzleTableDrag>()??b.gameObject.AddComponent<PuzzleTableDrag>();
            d.overlay=root;d.cardName=name;d.place=action;
            d.catalogReader=reader.gameObject;
            if(d.closeReader==null)d.closeReader=()=>SetReader(false);
            return d;
        }
        void Place(string name,PuzzleCard from,int side,int zone,int place)
        {
            if(!defs.TryGetValue(name,out var def))return;
            if(zone==0 && (def.cardTypes&PermanentTypes)==0){status.text="这张牌不能作为永久物摆在战场上。";return;}
            if(from==null && draft.cards.Sum(c=>c.count)>=500){status.text="局面最多可放 500 张牌。";return;}
            if(zone==0 && place==0)place=draft.FreePlace(side,BackRow(name));
            var before=zone!=0 && place>0?draft.cards.Where(c=>c.side==side && c.zone==zone).ElementAtOrDefault(place-1):null;
            if(before!=null && from==before)return;
            var occupied=zone==0?draft.cards.Find(c=>c.side==side && c.zone==0 && c.tablePlace==place):null;
            if(occupied!=null && occupied!=from && (from==null || from.zone!=0 || from.side!=side))
            {status.text="这个位置已有牌，请选空位。";return;}
            var row=from;
            if(row==null || row.zone!=0 && row.count>1)
            {
                if(row!=null)row.count--;
                row=new PuzzleCard{name=name};draft.cards.Add(row);
            }
            if(occupied!=null && occupied!=row)
            {
                occupied.side=row.side;occupied.tablePlace=row.tablePlace;
            }
            if(from!=null && from.zone==0 && zone!=0)
            {
                draft.RemoveLastCopy(row);
                row=new PuzzleCard{name=name};draft.cards.Add(row);
            }
            else if(from!=null && from.zone!=0 && zone==0)
            {
                draft.cards.Remove(row);draft.cards.Add(row);
            }
            row.side=side;row.zone=zone;row.tablePlace=zone==0?place:0;
            if(zone!=0)
            {
                draft.cards.Remove(row);
                if(before==null)draft.cards.Add(row);else draft.cards.Insert(draft.cards.IndexOf(before),row);
            }
            zones[side]=zone;
            pages[side]=zone==0?(place-1)/pageSize:draft.cards.Where(c=>c.side==side&&c.zone==zone).ToList().IndexOf(row)/pageSize;
            selected=row;piece=null;status.text=zone==0?"桌上的编号为双方共用的进场次序。":"已放入"+(side==0?"我方":"对方")+PuzzleZones[zone]+"。";
            Draw();
        }
        void CatalogPage()
        {
            int last=Math.Max(0,(hits.Count-1)/hitCount);catalogPage=Mathf.Clamp(catalogPage,0,last);
            pageLabel.text=hits.Count==0?"无匹配卡牌":$"{catalogPage+1} / {last+1}";prev.interactable=catalogPage>0;next.interactable=catalogPage<last;
            CardDefinition choice=null;
            for(int i=0;i<hitCount;i++)
            {
                var b=results[i];int idx=catalogPage*hitCount+i;b.gameObject.SetActive(idx<hits.Count);if(idx>=hits.Count)continue;
                var card=hits[idx];b.GetComponentInChildren<Text>().text=names[card.cardName];
                if(choice==null || piece==card.cardName)choice=card;
                if(displayed[i]!=card)
                {
                    displayed[i]=card;
                    bool found=CardImageLibrary.TryLoadThumbnail(card,out var sprite);
                    thumbnails[i].sprite=sprite;thumbnails[i].enabled=found;
                    missingArt[i].gameObject.SetActive(!found);
                }
                MenuVisualTheme.StyleDeckTool(b,piece==card.cardName);
                Drag(b,names[card.cardName],(s,z,p)=>Place(card.cardName,null,s,z,p)).BindPreview(preview,card);
            }
            if(reader.gameObject.activeSelf)
            {
                readerEmpty.gameObject.SetActive(choice==null);
                if(choice==null)readerArt.Hide();
                else if(choice!=reading)readerArt.Show(reader,choice,reader,null,namedFaces:true,centered:true);
                reading=choice;
            }
        }
        for(int i=0;i<hitCount;i++)
        {
            int slot=i;
            results[i]=PuzzleButton(catalog,"添加卡牌",i%2==0?-89:89,i<2?65:-138,170,()=>{
                piece=hits[catalogPage*hitCount+slot].cardName;selected=null;
                if(!reader.gameObject.activeSelf)status.text="已选「"+names[piece]+"」，点击空位放置。";
                Draw();
            });
            var tile=(RectTransform)results[i].transform;tile.sizeDelta=new Vector2(170,195);
            var title=results[i].GetComponentInChildren<Text>();title.rectTransform.anchoredPosition=new Vector2(0,-78);
            title.rectTransform.sizeDelta=new Vector2(156,34);title.horizontalOverflow=HorizontalWrapMode.Wrap;
            title.resizeTextMaxSize=20;
            var image=ReplayWidgets.Rect(tile,"Thumbnail",0,17,148,148).gameObject.AddComponent<Image>();
            image.raycastTarget=false;image.preserveAspect=true;
            thumbnails[i]=image;
            missingArt[i]=ReplayWidgets.Label(tile,"缺失卡图",0,17,150,72,18,TextAnchor.MiddleCenter);
        }
        prev=PuzzleButton(catalog,"上一页",-119,-268,100,()=>{catalogPage--;CatalogPage();});
        next=PuzzleButton(catalog,"下一页",119,-268,100,()=>{catalogPage++;CatalogPage();});
        void Search()
        {
            string term=search.text.Trim();
            if(term.Length==0)
            {
                hits.Clear();
                foreach(var starter in PuzzleStarters)if(defs.TryGetValue(starter,out var card))hits.Add(card);
                foreach(var card in pool)if(Array.IndexOf(PuzzleStarters,card.cardName)<0)hits.Add(card);
                catalogPage=0;CatalogPage();return;
            }
            hits.Clear();
            string key=term.ToUpperInvariant();
            int exact=0;
            foreach(var card in pool)
            {
                if(puzzleSearchTerms[card].IndexOf(key,StringComparison.Ordinal)<0)continue;
                if(string.Equals(term,card.cardName,StringComparison.OrdinalIgnoreCase) || string.Equals(term,names[card.cardName],StringComparison.OrdinalIgnoreCase))hits.Insert(exact++,card);
                else hits.Add(card);
            }
            catalogPage=0;CatalogPage();
        }
        search.onValueChanged.AddListener(_=>Search());
        void Navigate(int side,int zone)
        {
            zones[side]=zone;pages[side]=0;Draw();
        }
        for(int s=0;s<2;s++)
        {
            int side=s;var lane=lanes[s];
            var title=ReplayWidgets.Label(lane,side==0?"我方":"对方",-553,111,91,46,25);
            title.color=side==0?MenuVisualTheme.MainAccent:MenuVisualTheme.SideAccent;
            for(int j=0;j<PuzzleZones.Length;j++)
            {
                int zone=j;
                tabs[s,j]=PuzzleButton(lane,PuzzleZones[j],-420+j*129,111,124,()=>Navigate(side,zone));
                DropPoint(tabs[s,j].transform,side,zone,0);
            }
            legends[s]=ReplayWidgets.Label(lane,"同批同时进场",258,111,173,38,16,TextAnchor.MiddleCenter);
            legends[s].color=MenuVisualTheme.Silver;
            back[s]=PuzzleButton(lane,"‹",398,111,58,()=>{pages[side]--;Draw();});
            pageText[s]=ReplayWidgets.Label(lane,"",465,111,78,40,18,TextAnchor.MiddleCenter);
            more[s]=PuzzleButton(lane,"›",544,111,58,()=>{pages[side]++;Draw();});
            for(int j=0;j<pageSize;j++)
            {
                var tile=ReplayWidgets.Panel(lane,"Place"+(j+1),-521+(j%cols)*149,0,138,101,MenuVisualTheme.Raised);
                var slot=slots[s,j]=new PuzzleEditorSlot{face=tile.GetComponent<Image>()};
                slot.button=tile.gameObject.AddComponent<Button>();slot.button.targetGraphic=slot.face;
                slot.drop=tile.gameObject.AddComponent<PuzzleTableDrop>();slot.drop.side=side;
                slot.empty=ReplayWidgets.Label(tile,"＋",0,0,50,48,20,TextAnchor.MiddleCenter);
                slot.empty.color=new Color(.78f,.84f,.85f,.42f);
                slot.art=ReplayWidgets.Rect(tile,"Illustration",0,9,132,77).gameObject.AddComponent<RawImage>();
                slot.art.raycastTarget=false;
                var bottom=ReplayWidgets.Panel(tile,"Caption",0,-34,132,27,new Color(.06f,.09f,.12f,.96f));
                slot.caption=ReplayWidgets.Label(bottom,"",0,0,125,26,16,TextAnchor.MiddleCenter);
                slot.caption.resizeTextForBestFit=true;slot.caption.resizeTextMinSize=12;slot.caption.resizeTextMaxSize=17;
                var badge=ReplayWidgets.Panel(tile,"Sequence",-24,33,84,27,new Color(.07f,.11f,.15f,.97f));
                slot.sequence=ReplayWidgets.Label(badge,"",0,0,80,26,16,TextAnchor.MiddleCenter);
                slot.button.onClick.AddListener(()=>{
                    var row=slot.row;int zone=slot.drop.zone;
                    if(piece!=null && (row==null || zone!=0)){Place(piece,null,side,zone,slot.drop.place);return;}
                    if(row!=null){selected=row;piece=null;status.text=Name(row);Draw();}
                });
                slot.drag=Drag(slot.button,"",(ds,dz,dp)=>{if(slot.row!=null)Place(slot.row.name,slot.row,ds,dz,dp);});
            }
        }
        void Draw()
        {
            var batches=draft.EntryBatches();
            var batchNumbers=new Dictionary<PuzzleCard,int>();
            for(int i=0;i<batches.Count;i++)foreach(var card in batches[i])batchNumbers.Add(card,i);
            for(int s=0;s<2;s++)
            {
                int z=zones[s];
                var rows=draft.cards.Where(c=>c.side==s&&c.zone==z).ToList();
                int max=z==0?rows.Select(c=>c.tablePlace).DefaultIfEmpty(0).Max():rows.Count;
                int last=Math.Max(0,(max-1)/pageSize);
                if(z==0 && rows.Count>0 && rows.Count(c=>c.tablePlace>last*pageSize)>=cols && draft.cards.Sum(c=>c.count)<500)last++;
                pages[s]=Mathf.Clamp(pages[s],0,last);
                for(int j=0;j<PuzzleZones.Length;j++)
                {
                    var tab=tabs[s,j];
                    string text=PuzzleZones[j]+" "+draft.cards.Where(c=>c.side==s && c.zone==j).Sum(c=>c.count);
                    tab.GetComponentInChildren<Text>().text=text;
                    tab.name=text;
                    if(paintedZone[s]!=z && (j==z||j==paintedZone[s]))
                        MenuVisualTheme.StyleDeckTool(tab,j==z);
                }
                paintedZone[s]=z;legends[s].gameObject.SetActive(z==0);
                pageText[s].text=$"{pages[s]+1}/{last+1}";back[s].interactable=pages[s]>0;more[s].interactable=pages[s]<last;
                for(int j=0;j<pageSize;j++)
                {
                    int place=pages[s]*pageSize+j+1;
                    var r=z==0?rows.Find(c=>c.tablePlace==place):rows.ElementAtOrDefault(place-1);
                    var slot=slots[s,j];var tile=slot.face.rectTransform;
                    int visualRow=z==0 && s==1?1-j/cols:j/cols;
                    tile.anchoredPosition=new Vector2(-521+(j%cols)*149,26-visualRow*111);
                    tile.name="Place"+place;slot.drop.zone=z;slot.drop.place=place;slot.row=r;
                    slot.face.color=r==selected&&r!=null?MenuVisualTheme.MainAccent:r==null?new Color(.09f,.13f,.16f,.33f):MenuVisualTheme.Raised;
                    slot.empty.gameObject.SetActive(r==null);
                    slot.caption.transform.parent.gameObject.SetActive(r!=null);
                    slot.sequence.transform.parent.gameObject.SetActive(r!=null);
                    slot.drag.enabled=r!=null;
                    if(slot.artName!=r?.name){
                        slot.artName=r?.name;slot.art.texture=null;
                        CardDefinition def=null;
                        if(r!=null)defs.TryGetValue(r.name,out def);
                        slot.drag.BindPreview(preview,def);
                        if(def!=null && CardImageLibrary.TryLoadThumbnail(def,out var sprite)){
                            var uv=sprite.rect;var tex=sprite.texture;slot.art.texture=tex;
                            slot.art.uvRect=new Rect((uv.x+.095f*uv.width)/tex.width,(uv.y+.49f*uv.height)/tex.height,.81f*uv.width/tex.width,.38f*uv.height/tex.height);
                        }
                    }
                    slot.art.enabled=slot.art.texture!=null;
                    if(r==null)continue;
                    slot.caption.text=slot.drag.cardName=Name(r);
                    slot.sequence.text=z==0?$"批次 {batchNumbers[r]+1:00}":z==2?$"库位 {place}":"×"+r.count;
                }
            }
            bool sequence=selected!=null&&(selected.zone==0||selected.zone==2);
            var list=sequence?draft.cards.Where(r=>r.zone==selected.zone&&(r.zone==0||r.side==selected.side)).ToList():null;
            int index=sequence?list.IndexOf(selected):-1;
            bool battlefield=selected?.zone==0;
            int count=battlefield?batches.Count:list?.Count??0;
            if(battlefield)index=batchNumbers[selected];
            bool grouped=battlefield&&batches[index].Count>1;
            summary.text=selected==null?(piece==null?"点选桌上的牌进行编辑":names[piece]):Name(selected)+(sequence?(battlefield?"　批次 ":"　牌库 ")+(index+1)+"/"+count:"　×"+selected.count);
            edit.interactable=selected?.zone==0;remove.interactable=selected!=null;
            duplicate.interactable=selected!=null && draft.cards.Sum(c=>c.count)<500;
            earlier.interactable=sequence&&index>0;later.interactable=sequence&&index<count-1;
            together.interactable=battlefield&&(grouped||index>0);
            together.GetComponentInChildren<Text>().text=grouped?"拆为下一批":"与前批同时";
            earlier.GetComponentInChildren<Text>().text=selected?.zone==2?"靠近库顶":"提前一批";
            later.GetComponentInChildren<Text>().text=selected?.zone==2?"远离库顶":"延后一批";
            CatalogPage();
        }
        edit=PuzzleButton(toolbar,"状态",-251,0,104,()=>{
            if(selected!=null&&defs.TryGetValue(selected.name,out var d))OpenPuzzleCardState(root,draft,selected,d,Draw);
        });
        void Shift(int step)
        {
            draft.ShiftOrder(selected,step);
            if(selected.zone==2)pages[selected.side]=draft.cards.Where(c=>c.zone==2&&c.side==selected.side).ToList().IndexOf(selected)/pageSize;
            Draw();
        }
        earlier=PuzzleButton(toolbar,"提前一批",-115,0,148,()=>Shift(-1));
        later=PuzzleButton(toolbar,"延后一批",43,0,148,()=>Shift(1));
        together=PuzzleButton(toolbar,"与前批同时",209,0,164,()=>{
            if(draft.ToggleSimultaneous(selected)) {status.text="相同批次的牌同时进场。";Draw();}
        });
        duplicate=PuzzleButton(toolbar,"复制",371,0,140,()=>{
            var copy=JsonUtility.FromJson<PuzzleCard>(JsonUtility.ToJson(selected));
            copy.key=null;copy.count=1;copy.entryBatch=0;
            if(copy.zone==0){copy.tablePlace=draft.FreePlace(copy.side,BackRow(copy.name));draft.cards.Add(copy);pages[copy.side]=(copy.tablePlace-1)/pageSize;}
            else{draft.cards.Insert(draft.cards.IndexOf(selected)+1,copy);pages[copy.side]=draft.cards.Where(c=>c.zone==copy.zone&&c.side==copy.side).ToList().IndexOf(copy)/pageSize;}
            selected=copy;zones[copy.side]=copy.zone;status.text="已复制一张牌。";Draw();
        });
        remove=PuzzleButton(toolbar,"移除",530,0,136,()=>{
            bool detached=draft.RemoveLastCopy(selected);if(selected.count==0)selected=null;
            status.text=detached?"已移除卡牌并解除与它的结附。":"已移除一张牌。";Draw();
        });
        PuzzleButton(root,"残局设定",104,-405,185,()=>OpenPuzzleSettings(root,draft));
        void Save(bool play)
        {
            try
            {
                if(play)draft.EngineSetup();
                LocalPuzzleStore.Save(draft);
                if (match != null) match.ReturnFromPuzzleEditor(draft, play);
                else OpenPuzzleLibrary(draft.LibraryKey,play?draft:null);
            }
            catch(Exception e){status.text=e.Message;}
        }
        PuzzleButton(root,"保存残局",376,-405,210,()=>Save(false));
        PuzzleButton(root,match != null && source != null ? "保存并重新试玩" : "保存并试玩",669,-405,265,()=>Save(true),true);
        Search();Draw();
    }

    void OpenPuzzleSettings(RectTransform root,LocalPuzzle draft)
    {
        var shade=PuzzleModal(root,"PuzzleSettings");
        var panel=MenuVisualTheme.ArchiveSection(shade,"Setup","残局设定",0,0,1000,818,MenuVisualTheme.SideAccent);
        InputField Field(string caption,string value,int y,int limit,bool number=false,int x=90,int width=690)
        {
            ReplayWidgets.Label(panel,caption,-350,y,190,47,22);
            return PuzzleInput(panel,caption,value,x,y,width,limit,number);
        }
        Dropdown Choice(string caption,string[] values,int y,int current)
        {
            ReplayWidgets.Label(panel,caption,-350,y,190,47,22);
            return PuzzlePicker(panel,caption,90,y,690,values,current);
        }
        var title=Field("名称",draft.title,292,60);var hint=Field("关卡说明",draft.description,195,1200);
        hint.lineType=InputField.LineType.MultiLineNewline;
        ((RectTransform)hint.transform).sizeDelta=new Vector2(690,122);
        var hintText=hint.textComponent;
        hintText.rectTransform.anchorMin=Vector2.zero;hintText.rectTransform.anchorMax=Vector2.one;
        hintText.rectTransform.offsetMin=new Vector2(14,8);hintText.rectTransform.offsetMax=new Vector2(-14,-8);
        hintText.alignment=TextAnchor.UpperLeft;hintText.horizontalOverflow=HorizontalWrapMode.Wrap;
        var human=Field("我方生命",draft.humanLife.ToString(),97,3,true);var ai=Field("对方生命",draft.aiLife.ToString(),37,3,true);
        var side=Choice("行动方",new[]{"我方","对方"},-23,draft.firstSide);
        var phase=Choice("起始阶段",PuzzlePhases,-83,draft.phase);
        var goal=Choice("目标",new[]{"限时获胜","限时存活","满足全部条件","满足任一条件"},-143,
            draft.goal!=null ? (draft.goal.mode=="all"?2:3) : (draft.survive?1:0));
        var rules=draft.goal==null ? new PuzzleGoal() : JsonUtility.FromJson<PuzzleGoal>(JsonUtility.ToJson(draft.goal));
        var edit=PuzzleButton(panel,"编辑条件",320,-143,226,()=>OpenPuzzleConditions(shade,rules));
        var times = new[]{"state","win","survive"};
        var timingLabel = ReplayWidgets.Label(panel,"检查时机",-350,-266,190,47,22);
        var timing = PuzzlePicker(panel,"检查时机",90,-266,690,new[]{"局面满足时","赢得对局时","存活到期或提前获胜时"},Array.IndexOf(times,rules.checkAt));
        var note=ReplayWidgets.Label(panel,"",0,-308,850,46,19,TextAnchor.MiddleCenter);
        void GoalLayout(int selected) {
            bool custom=selected>=2;
            edit.gameObject.SetActive(custom);
            timingLabel.gameObject.SetActive(custom);
            timing.gameObject.SetActive(custom);
            note.text="";
            var rect=(RectTransform)goal.transform;
            rect.anchoredPosition=new Vector2(custom?-30:90,-143);
            rect.sizeDelta=new Vector2(custom?450:690,52);
        }
        goal.onValueChanged.AddListener(GoalLayout);
        GoalLayout(goal.value);
        var turns=Field("回合数",draft.turns.ToString(),-203,2,true,-183,144);
        var turnHint=ReplayWidgets.Label(panel,"双方的回合分别计数。",177,-203,523,45,19,TextAnchor.MiddleLeft);
        turnHint.color=MenuVisualTheme.Silver;
        PuzzleButton(panel,"取消",-160,-361,235,()=>ClosePuzzleModal(root,shade)).Select();
        PuzzleButton(panel,"应用",160,-361,235,()=>{
            if(string.IsNullOrWhiteSpace(title.text)||!int.TryParse(human.text,out int h)||h<1||h>200||!int.TryParse(ai.text,out int a)||a<1||a>200||
                !int.TryParse(turns.text,out int t)||t<1||t>30){note.text="请填写名称、1～200 的生命和 1～30 回合。";return;}
            if(goal.value>=2){
                rules.mode=goal.value==2?"all":"any";
                rules.checkAt=times[timing.value];
                try {rules.Validate();} catch(ArgumentException e){note.text=e.Message;return;}
            }
            draft.title=title.text.Trim();draft.description=hint.text.Trim();draft.humanLife=h;draft.aiLife=a;draft.turns=t;
            draft.firstSide=side.value;draft.phase=phase.value;draft.survive=goal.value==1;
            draft.goal=goal.value>=2?rules:null;ClosePuzzleModal(root,shade);
        },true);
    }
}
