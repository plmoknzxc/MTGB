using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class PlayerAvatarPicker : MonoBehaviour
{
    internal const float Height=88;
    const int Columns=4, VisibleRows=3;
    const float TileWidth=170, TileHeight=214, Gap=12, Stride=TileHeight+Gap;
    const float GridWidth=Columns*TileWidth+(Columns-1)*Gap, SheetWidth=GridWidth+64;

    sealed class Tile
    {
        internal RectTransform rect;
        internal Button button;
        internal RawImage art;
        internal Text name;
        internal GameObject equipped;
        internal PlayerPortrait item;
    }

    Text heading, currentName, changeLabel, empty;
    PlayerAvatarGraphic currentFace;
    Button change;
    Canvas gallery;
    RectTransform sheet, content;
    ScrollRect scroll;
    CanvasGroup background, settingsSurface;
    bool backgroundInteractable;
    float settingsAlpha;
    readonly List<Tile> tiles=new();
    readonly List<PlayerPortrait> filtered=new();
    IReadOnlyList<PlayerPortrait> catalog;
    int firstRow=-1;

    internal static RectTransform Create(Transform parent)
    {
        var root=ReplayWidgets.Rect(parent,"PlayerAvatarPicker",0,0,0,Height);
        root.gameObject.SetActive(false);
        var picker=root.gameObject.AddComponent<PlayerAvatarPicker>();
        var portrait=ReplayWidgets.Rect(root,"CurrentPortrait",0,0,74,74);
        portrait.anchorMin=portrait.anchorMax=new Vector2(0,.5f);
        portrait.anchoredPosition=new Vector2(36,0);
        picker.currentFace=portrait.gameObject.AddComponent<PlayerAvatarGraphic>();
        picker.currentFace.raycastTarget=false;
        picker.heading=RowLabel(root,15,15);
        picker.currentName=RowLabel(root,-13,21);
        picker.change=ReplayWidgets.Button(root,"更换",0,0,100,()=>picker.Open(PlayerAvatarGraphic.Catalog));
        var action=(RectTransform)picker.change.transform;
        action.anchorMin=action.anchorMax=new Vector2(1,.5f);
        action.anchoredPosition=new Vector2(-50,0);
        picker.changeLabel=picker.change.GetComponentInChildren<Text>();
        picker.changeLabel.GetComponent<PreferenceText>().Literal=true;
        root.gameObject.SetActive(true);
        return root;
    }

    static Text RowLabel(Transform parent,float y,int size)
    {
        var label=ReplayWidgets.Label(parent,"",0,0,0,38,size);
        var r=label.rectTransform;
        r.anchorMin=new Vector2(0,.5f); r.anchorMax=new Vector2(1,.5f);
        r.offsetMin=new Vector2(84,y-19); r.offsetMax=new Vector2(-110,y+19);
        label.resizeTextForBestFit=true; label.resizeTextMinSize=12; label.resizeTextMaxSize=size+6;
        label.GetComponent<PreferenceText>().Literal=true;
        return label;
    }

    void OnEnable(){GamePreferences.Changed+=Refresh;Refresh();}
    void OnDisable(){GamePreferences.Changed-=Refresh;Close();}
    void Refresh()
    {
        var current=PlayerAvatarGraphic.Resolve(GamePreferences.AvatarId);
        currentFace.SetAvatar(current.Id);
        heading.text=DeckCompanion.Text("玩家头像","Player portrait","プレイヤーの肖像");
        heading.color=MenuVisualTheme.MistBlue;
        currentName.text=current.Name;
        currentName.color=MenuVisualTheme.ArchivePaper;
        changeLabel.text=DeckCompanion.Text("更换","Change","変更");
        MenuVisualTheme.StyleDeckTool(change);
    }

    void Open(IReadOnlyList<PlayerPortrait> portraits)
    {
        if(gallery!=null)return;
        var owner=GetComponentInParent<Canvas>().rootCanvas;
        int order=owner.GetComponentsInChildren<Canvas>().Max(c=>c.sortingOrder);
        gallery=ReplayWidgets.Canvas("AvatarGallery",Mathf.Min(short.MaxValue,Mathf.Max(order,450)+1));
        var shade=ReplayWidgets.Panel(gallery.transform,"Backdrop",0,0,0,0,MenuVisualTheme.DialogShade);
        shade.anchorMin=Vector2.zero;shade.anchorMax=Vector2.one;shade.offsetMin=shade.offsetMax=Vector2.zero;
        var dismiss=shade.gameObject.AddComponent<Button>();
        dismiss.transition=Selectable.Transition.None;dismiss.onClick.AddListener(Close);
        dismiss.navigation=new Navigation{mode=Navigation.Mode.None};

        catalog=portraits;
        int rows=Mathf.Clamp(Mathf.CeilToInt(catalog.Count/(float)Columns),1,VisibleRows);
        bool searchable=catalog.Count>VisibleRows*Columns;
        float viewHeight=rows*Stride-Gap, gridTop=searchable?151:92;
        float height=gridTop+viewHeight+26;
        sheet=ReplayWidgets.Panel(gallery.transform,"Portraits",0,0,SheetWidth,height,MenuVisualTheme.ArchivePanel);
        var title=ReplayWidgets.Label(sheet,heading.text,0,0,360,60,30);
        title.GetComponent<PreferenceText>().Literal=true;
        MenuVisualTheme.StyleDeckDialogSurface(sheet,title);
        var back=ReplayWidgets.Button(sheet,DeckCompanion.Text("返回","Back","戻る"),SheetWidth/2-77,height/2-36,104,Close);
        MenuVisualTheme.StyleDeckTool(back);
        if(searchable)BuildSearch(height);

        var viewport=ReplayWidgets.Panel(sheet,"Viewport",0,height/2-gridTop-viewHeight/2,GridWidth,viewHeight,MenuVisualTheme.Recess);
        viewport.gameObject.AddComponent<RectMask2D>();
        scroll=viewport.gameObject.AddComponent<ScrollRect>();
        scroll.viewport=viewport;scroll.horizontal=false;scroll.movementType=ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity=68;
        content=ReplayWidgets.Rect(viewport,"PortraitGrid",0,0,GridWidth,viewHeight);
        content.anchorMin=content.anchorMax=content.pivot=new Vector2(0,1);
        scroll.content=content;
        if(searchable){
            var bar=DefaultControls.CreateScrollbar(new DefaultControls.Resources()).GetComponent<Scrollbar>();
            bar.transform.SetParent(sheet,false);
            bar.SetDirection(Scrollbar.Direction.BottomToTop,false);
            var track=(RectTransform)bar.transform;
            track.sizeDelta=new Vector2(20,viewHeight);
            track.anchoredPosition=new Vector2(GridWidth/2+14,viewport.anchoredPosition.y);
            MenuVisualTheme.StyleDeckScrollThumb(bar);
            scroll.verticalScrollbar=bar;
            scroll.verticalScrollbarVisibility=ScrollRect.ScrollbarVisibility.AutoHide;
        }
        empty=ReplayWidgets.Label(viewport,DeckCompanion.Text("没有匹配的头像","No matching portraits","一致する肖像はありません"),0,0,GridWidth,50,20,TextAnchor.MiddleCenter);
        empty.color=MenuVisualTheme.MistBlue;empty.GetComponent<PreferenceText>().Literal=true;
        for(int i=0;i<Math.Min(catalog.Count,Columns*(VisibleRows+1));i++)CreateTile();
        scroll.onValueChanged.AddListener(_=>BindVisible());
        Filter("",true);
        if(!owner.TryGetComponent(out background))background=owner.gameObject.AddComponent<CanvasGroup>();
        backgroundInteractable=background.interactable;
        background.interactable=false;
        var settings=GetComponentInParent<GameSettingsPanel>();
        if(settings!=null){
            if(!settings.TryGetComponent(out settingsSurface))settingsSurface=settings.gameObject.AddComponent<CanvasGroup>();
            settingsAlpha=settingsSurface.alpha;
            settingsSurface.alpha=0;
        }
        Fit();
        back.Select();
    }

    void BuildSearch(float height)
    {
        var search=ReplayWidgets.Input(sheet,"",0,height/2-107,GridWidth);
        search.name="PortraitSearch";
        var placeholder=ReplayWidgets.Label(search.transform,DeckCompanion.Text("搜索头像","Search portraits","肖像を検索"),0,0,GridWidth-24,46,19);
        placeholder.GetComponent<PreferenceText>().Literal=true;
        search.placeholder=placeholder;
        MenuVisualTheme.StyleSearchInput(search);
        search.onValueChanged.AddListener(query=>Filter(query,false));
    }

    void CreateTile()
    {
        var tile=new Tile();
        tile.rect=ReplayWidgets.Panel(content,"PortraitTile",0,0,TileWidth,TileHeight,Color.white);
        tile.rect.anchorMin=tile.rect.anchorMax=tile.rect.pivot=Vector2.up;
        tile.button=tile.rect.gameObject.AddComponent<Button>();
        tile.button.targetGraphic=tile.rect.GetComponent<Image>();
        tile.button.onClick.AddListener(()=>{
            if(tile.item==null)return;
            string id=tile.item.Id;
            Close();
            GamePreferences.SetAvatar(id);
        });
        const float imageSize=TileWidth-4;
        var art=ReplayWidgets.Rect(tile.rect,"Portrait",TileWidth/2,-TileWidth/2,imageSize,imageSize);
        art.anchorMin=art.anchorMax=Vector2.up;
        tile.art=art.gameObject.AddComponent<RawImage>();tile.art.raycastTarget=false;

        var footer=ReplayWidgets.Panel(tile.rect,"Caption",TileWidth/2,-TileWidth-(TileHeight-TileWidth)/2+1,imageSize,TileHeight-TileWidth-2,MenuVisualTheme.ArchivePanel);
        footer.anchorMin=footer.anchorMax=Vector2.up;
        footer.GetComponent<Image>().raycastTarget=false;
        tile.name=ReplayWidgets.Label(footer,"",0,0,imageSize-12,TileHeight-TileWidth-4,19,TextAnchor.MiddleCenter);
        tile.name.resizeTextForBestFit=true;tile.name.resizeTextMinSize=12;tile.name.resizeTextMaxSize=24;
        tile.name.GetComponent<PreferenceText>().Literal=true;
        tile.name.color=MenuVisualTheme.ArchivePaper;

        var flag=ReplayWidgets.Panel(art,"Equipped",0,0,80,30,MenuVisualTheme.Recess);
        flag.anchorMin=flag.anchorMax=flag.pivot=Vector2.zero;
        flag.anchoredPosition=new Vector2(5,5);
        flag.GetComponent<Image>().raycastTarget=false;
        var label=ReplayWidgets.Label(flag,DeckCompanion.Text("使用中","In use","使用中"),0,0,76,30,15,TextAnchor.MiddleCenter);
        label.GetComponent<PreferenceText>().Literal=true;label.color=MenuVisualTheme.SideAccent;
        tile.equipped=flag.gameObject;
        tiles.Add(tile);
    }

    void Filter(string query,bool locateEquipped)
    {
        filtered.Clear();
        string term=query.Trim();
        filtered.AddRange(catalog.Where(p=>term.Length==0||p.Matches(term)));
        float height=Mathf.CeilToInt(filtered.Count/(float)Columns)*Stride-Gap;
        float overflow=Mathf.Max(0,height-scroll.viewport.rect.height);
        scroll.StopMovement();
        content.sizeDelta=new Vector2(GridWidth,Mathf.Max(0,height));
        int selected=locateEquipped?filtered.FindIndex(p=>p.Id==GamePreferences.AvatarId):-1;
        content.anchoredPosition=new Vector2(0,Mathf.Clamp((selected/Columns)*Stride,0,overflow));
        empty.gameObject.SetActive(filtered.Count==0);
        firstRow=-1;
        BindVisible();
    }

    void BindVisible()
    {
        int row=Mathf.Max(0,Mathf.FloorToInt(content.anchoredPosition.y/Stride));
        if(row==firstRow)return;
        firstRow=row;
        string equipped=GamePreferences.AvatarId;
        for(int slot=0;slot<tiles.Count;slot++){
            var tile=tiles[slot];
            int index=row*Columns+slot;
            tile.rect.gameObject.SetActive(index<filtered.Count);
            if(index>=filtered.Count){tile.item=null;tile.art.texture=null;continue;}
            var item=filtered[index];
            tile.rect.anchoredPosition=new Vector2(index%Columns*(TileWidth+Gap),-(index/Columns)*Stride);
            tile.item=item;tile.art.texture=item.Load();tile.art.uvRect=item.UV;tile.name.text=item.Name;
            bool selected=item.Id==equipped;
            tile.equipped.SetActive(selected);
            var colors=ColorBlock.defaultColorBlock;
            colors.normalColor=selected?MenuVisualTheme.SideAccent:MenuVisualTheme.Etching;
            colors.highlightedColor=colors.selectedColor=MenuVisualTheme.ArchivePaper;
            colors.pressedColor=MenuVisualTheme.SideAccent;colors.fadeDuration=.1f;
            tile.button.colors=colors;
        }
    }

    void LateUpdate()
    {
        if(gallery==null)return;
        Fit();
        if(Input.GetKeyDown(KeyCode.Escape))Close();
    }

    void Fit()
    {
        var area=((RectTransform)gallery.transform).rect;
        float scale=Mathf.Min(1,(area.width-24)/SheetWidth,(area.height-24)/sheet.sizeDelta.y);
        if(scale>0)sheet.localScale=Vector3.one*scale;
    }

    void Close()
    {
        if(gallery==null)return;
        gallery.gameObject.SetActive(false);
        Destroy(gallery.gameObject);gallery=null;
        if(background!=null)background.interactable=backgroundInteractable;
        if(settingsSurface!=null)settingsSurface.alpha=settingsAlpha;
        background=null;settingsSurface=null;catalog=null;tiles.Clear();filtered.Clear();firstRow=-1;
        if(isActiveAndEnabled && EventSystem.current!=null)change.Select();
    }
}
