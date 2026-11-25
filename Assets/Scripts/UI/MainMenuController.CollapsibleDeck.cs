using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

public sealed partial class MainMenuController
{
    private bool detailsCollapsed, sideboardCollapsed, floatingDeckDetails;
    private float detailsCollapseProgress, sideboardCollapseProgress, floatingCloseAt;
    private RectTransform deckLayoutRoot, deckArea, collectionArea, mainArea, sideArea;
    private Button detailsCollapseButton, sideboardCollapseButton;
    private RectTransform detailChevron, sideChevron, detailSeam, sidePlate;
    private CanvasGroup sideAreaGroup, sideHeadingGroup;
    private DeckHoverArtPopup deckArtHover;
    private DeckHoverArtPopup detailsArtHover;

    private void CreateDeckCollapseControls()
    {
        deckLayoutRoot=(RectTransform)deckBuilderGroup.transform;
        RectTransform FindArea(string name)=>System.Array.Find(deckLayoutRoot.GetComponentsInChildren<RectTransform>(true),r=>r.name==name);
        deckArea=FindArea("DeckBackplate");collectionArea=FindArea("CollectionBackplate");mainArea=FindArea("MainDeckScroll");sideArea=FindArea("SideboardScroll");
        if(deckArea==null||mainArea==null||sideArea==null)return;
        sidePlate=MenuVisualTheme.Detail(deckLayoutRoot,"SideboardBackplate",MenuVisualTheme.SideSurface).rectTransform;
        sidePlate.SetSiblingIndex(deckArea.GetSiblingIndex());
        MenuVisualTheme.SectionTop(sidePlate,MenuVisualTheme.SideAccent);
        sideAreaGroup=sideArea.GetComponent<CanvasGroup>();
        if(sideAreaGroup==null)sideAreaGroup=sideArea.gameObject.AddComponent<CanvasGroup>();
        sideHeadingGroup=sideboardCountText.GetComponent<CanvasGroup>();
        if(sideHeadingGroup==null)sideHeadingGroup=sideboardCountText.gameObject.AddComponent<CanvasGroup>();
        detailsCollapseButton=MakeCollapseArrow("CollapseCardDetails",out detailChevron,()=>{
            detailsCollapsed=!detailsCollapsed;floatingDeckDetails=false;previewVisible=false;if(detailsArtHover!=null)detailsArtHover.Hide();
            hoveredTile=null;floatingCloseAt=0;
        });
        sideboardCollapseButton=MakeCollapseArrow("CollapseSideboard",out sideChevron,()=>sideboardCollapsed=!sideboardCollapsed);
        detailSeam=MenuVisualTheme.Detail(deckLayoutRoot,"DetailSeam",MenuVisualTheme.Etching).rectTransform;
        detailSeam.SetSiblingIndex(detailsCollapseButton.transform.GetSiblingIndex());
        UpdateDeckCollapseLayout();
    }

    private Button MakeCollapseArrow(string name,out RectTransform chevron,UnityEngine.Events.UnityAction action)
    {
        var go=new GameObject(name,typeof(RectTransform),typeof(Image),typeof(Button));
        go.transform.SetParent(deckLayoutRoot,false);
        var button=go.GetComponent<Button>();button.onClick.AddListener(action);
        MenuVisualTheme.StyleArchiveControl(button);
        var icon=new GameObject("Arrow",typeof(RectTransform),typeof(DropdownChevron)).GetComponent<DropdownChevron>();
        icon.transform.SetParent(go.transform,false);
        icon.raycastTarget=false;
        chevron=icon.rectTransform;
        Place(chevron,Vector2.zero,new Vector2(20,20));
        chevron.localScale=Vector3.one*1.5f;
        icon.color=MenuVisualTheme.Silver;
        return button;
    }

    private static void DeckRegion(RectTransform rect,float x,float y,float width,float height)
    {
        rect.anchorMin=rect.anchorMax=rect.pivot=Vector2.zero;
        rect.anchoredPosition=new Vector2(x,y);rect.sizeDelta=new Vector2(width,height);
    }

    private void UpdateDeckCollapseLayout()
    {
        if(deckArea==null||!IsDeckBuilderVisible())return;
        float step=Mathf.Min(Time.unscaledDeltaTime,.05f)/.28f;
        detailsCollapseProgress=Mathf.MoveTowards(detailsCollapseProgress,detailsCollapsed?1:0,step);
        sideboardCollapseProgress=Mathf.MoveTowards(sideboardCollapseProgress,sideboardCollapsed?1:0,step);
        float d=Mathf.SmoothStep(0,1,detailsCollapseProgress),s=Mathf.SmoothStep(0,1,sideboardCollapseProgress);
        float w=deckLayoutRoot.rect.width,h=deckLayoutRoot.rect.height;
        float margin=w*.0135f, gap=Mathf.Clamp(w*.0115f,12,24);
        float detailWidth=Mathf.Clamp(w*.174f,260,380), libraryWidth=Mathf.Clamp(w*.215f,310,460);
        float left=Mathf.Lerp(margin+detailWidth+gap,margin+40,d);
        float libraryLeft=w-margin-libraryWidth, right=libraryLeft-gap;
        float top=h-100, baseY=h*.02f;
        DeckRegion(collectionArea,libraryLeft,baseY,libraryWidth,top-baseY);
        float headerHeight=LayoutDeckHeader(right-left-36,48);
        DeckRegion(mainDeckCountText.rectTransform,left+18,top-headerHeight-7,right-left-36,headerHeight);
        float workspaceHeight=top-baseY;
        var sideGrid=sideboardContent.GetComponent<GridLayoutGroup>();
        var viewport=sideArea.GetComponent<ScrollRect>().viewport;
        float viewportInsets=viewport.offsetMin.y-viewport.offsetMax.y;
        float sideHeight=Mathf.Clamp(sideGrid.cellSize.y+sideGrid.padding.vertical+viewportInsets+5,workspaceHeight*.22f,workspaceHeight*.40f);
        float sideHeadingY=baseY+sideHeight+4;
        float sideTop=Mathf.Lerp(sideHeadingY,baseY,s)+48;
        float mainBase=sideTop+12;
        DeckRegion(sidePlate,left,baseY,right-left,sideTop-baseY);
        DeckRegion(deckArea,left,mainBase,right-left,top-mainBase);
        DeckRegion(mainArea,left+12,mainBase+8,right-left-24,top-headerHeight-24-mainBase);
        // Slide the sideboard out intact, rather than scaling its cards during the transition.
        DeckRegion(sideArea,left+12,baseY-s*(sideHeight+baseY),right-left-24,sideHeight);
        sideAreaGroup.alpha=1-s;sideAreaGroup.blocksRaycasts=sideAreaGroup.interactable=!sideboardCollapsed;
        DeckRegion(sideboardCountText.rectTransform,left+12,Mathf.Lerp(sideHeadingY,baseY,s),right-left-62,42);
        sideHeadingGroup.alpha=1;
        DeckRegion((RectTransform)sideboardCollapseButton.transform,right-49,Mathf.Lerp(sideHeadingY,baseY,s),42,42);
        sideChevron.localRotation=Quaternion.Euler(0,0,180*s);
        DeckRegion((RectTransform)detailsCollapseButton.transform,Mathf.Lerp(margin+detailWidth-InspectorToolInset-InspectorToolSize,margin,d),
            top-InspectorToolInset-InspectorToolSize,InspectorToolSize,InspectorToolSize);
        detailChevron.localRotation=Quaternion.Euler(0,0,-90+180*d);
        DeckRegion(detailSeam,Mathf.Lerp(margin+detailWidth,margin-3,d),baseY,1,top-baseY);
        FitCardGrid(collectionContent,2);
        FitCardGrid(mainDeckContent);
        FitCardGrid(sideboardContent);

        if(floatingDeckDetails)
        {
            bool over=deckArtHover!=null&&Mouse.current!=null&&RectTransformUtility.RectangleContainsScreenPoint(deckArtHover.Rect,
                Mouse.current.position.ReadValue(),DeckPointerCamera());
            if(draggedTile!=null||(!over&&hoveredTile==null&&Time.unscaledTime>=floatingCloseAt))floatingDeckDetails=false;
        }
        if(!floatingDeckDetails&&deckArtHover!=null&&deckArtHover.gameObject.activeSelf)deckArtHover.Hide();
        DeckRegion(previewPanelRect,Mathf.Lerp(margin,-detailWidth-6,d),baseY,detailWidth,top-baseY);
        cardPreviewGroup.alpha=1-d;
        cardPreviewGroup.blocksRaycasts=cardPreviewGroup.interactable=!detailsCollapsed;
    }

    private static void FitCardGrid(RectTransform content,int columns=0)
    {
        var grid=content.GetComponent<GridLayoutGroup>();
        float available=content.rect.width-grid.padding.horizontal;
        if(columns==0) columns=Mathf.Max(1,Mathf.RoundToInt((available+grid.spacing.x)/(202f+grid.spacing.x)));
        if(grid.constraintCount!=columns)grid.constraintCount=columns;
        float width=Mathf.Max(1,(available-grid.spacing.x*(columns-1))/columns);
        var size=new Vector2(width,width*1.4f);
        if((grid.cellSize-size).sqrMagnitude>.01f)grid.cellSize=size;
    }

    private Camera DeckPointerCamera()
    {
        var canvas=deckLayoutRoot.GetComponentInParent<Canvas>();
        return canvas.renderMode==RenderMode.ScreenSpaceOverlay?null:canvas.worldCamera;
    }

    private void OpenFloatingDeckDetails()
    {
        if(hoveredTile==null||draggedTile!=null)return;
        floatingDeckDetails=true;floatingCloseAt=Time.unscaledTime+.12f;
        if(deckArtHover==null){var go=new GameObject("DeckArtHover",typeof(RectTransform));go.transform.SetParent(deckLayoutRoot,false);deckArtHover=go.AddComponent<DeckHoverArtPopup>();}
        deckArtHover.Show(deckLayoutRoot,hoveredTile,cardInfoText.font,currentDeck?.format);
    }

    private bool ShowDetailsRelatedHover(CardDefinition card,RectTransform anchor,DeckKeywordGlossary.Entry mechanic,bool show)
    {
        if(this==null)return false;
        if(!show){if(detailsArtHover!=null)detailsArtHover.RequestHide();return false;}
        if(detailsCollapsed||card==null||anchor==null||deckLayoutRoot==null)return false;
        if(DeckMechanicArt.HasArt(mechanic))
        {
            var helpers=DeckMechanicArt.GetReferences(card,true,mechanic,currentDeck?.format);
            if(detailsArtHover==null)
            {
                var go=new GameObject("DeckDetailsRelatedArtHover",typeof(RectTransform));
                go.transform.SetParent(deckLayoutRoot,false);detailsArtHover=go.AddComponent<DeckHoverArtPopup>();
            }
            detailsArtHover.ShowRelatedTokens(deckLayoutRoot,mechanic,helpers,anchor);
            return true;
        }
        if(!ReferenceEquals(detailTokenArtCard,card))return false;
        if(detailTokenArts==null||detailTokenArts.Count==0)return false;
        string help=mechanic?.EnglishHelp??string.Empty;
        bool createsToken=mechanic?.TokenKey!=null||help.IndexOf("token",System.StringComparison.OrdinalIgnoreCase)>=0
            ||help.IndexOf("create",System.StringComparison.OrdinalIgnoreCase)>=0
            ||(mechanic?.English??string.Empty).StartsWith("Amass",System.StringComparison.OrdinalIgnoreCase);
        if(!createsToken){if(detailsArtHover!=null)detailsArtHover.RequestHide();return false;}
        var matchingTokens=RelatedTokensForMechanic(mechanic);
        if(matchingTokens.Count==0){if(detailsArtHover!=null)detailsArtHover.RequestHide();return false;}
        if(detailsArtHover==null)
        {
            var go=new GameObject("DeckDetailsRelatedArtHover",typeof(RectTransform));
            go.transform.SetParent(deckLayoutRoot,false);
            detailsArtHover=go.AddComponent<DeckHoverArtPopup>();
        }
        detailsArtHover.ShowRelatedTokens(deckLayoutRoot,mechanic,matchingTokens,anchor);
        return true;
    }

    private System.Collections.Generic.List<DeckRelatedTokenArt.Art> RelatedTokensForMechanic(DeckKeywordGlossary.Entry mechanic)
    {
        var result=new System.Collections.Generic.List<DeckRelatedTokenArt.Art>();
        if(mechanic?.TokenKey!=null)
        {
            foreach(var art in detailTokenArts)if(art.key==mechanic.TokenKey)result.Add(art);
            return result;
        }
        string name=mechanic?.English??string.Empty;
        string hint=null;
        bool army=false;
        if(name.StartsWith("Amass",System.StringComparison.OrdinalIgnoreCase))
        {
            army=true;
            hint=name.StartsWith("Amass ",System.StringComparison.OrdinalIgnoreCase)?name.Substring(6).Split(' ')[0]:"Zombie";
            if(hint.EndsWith("ies",System.StringComparison.OrdinalIgnoreCase))hint=hint.Substring(0,hint.Length-3)+"y";
            else if(hint.EndsWith("s",System.StringComparison.OrdinalIgnoreCase))hint=hint.Substring(0,hint.Length-1);
        }
        else if(name.Equals("Investigate",System.StringComparison.OrdinalIgnoreCase))hint="Clue";
        else if(name.Equals("Afterlife",System.StringComparison.OrdinalIgnoreCase))hint="Spirit";
        else if(name.Equals("Recruit",System.StringComparison.OrdinalIgnoreCase))hint="Human Soldier";
        else if(name.Equals("Encore",System.StringComparison.OrdinalIgnoreCase)||name.Equals("Myriad",System.StringComparison.OrdinalIgnoreCase))hint="Copy";
        else if(name.Equals("Populate",System.StringComparison.OrdinalIgnoreCase))return new System.Collections.Generic.List<DeckRelatedTokenArt.Art>(detailTokenArts);
        else if(detailTokenArts.Count==1)return new System.Collections.Generic.List<DeckRelatedTokenArt.Art>(detailTokenArts);
        else hint=name;

        foreach(var art in detailTokenArts)
            if(art!=null&&art.name.IndexOf(hint,System.StringComparison.OrdinalIgnoreCase)>=0)result.Add(art);
        if(result.Count==0&&army)
            foreach(var art in detailTokenArts)
                if(art!=null&&art.name.IndexOf("Army",System.StringComparison.OrdinalIgnoreCase)>=0)result.Add(art);
        return result;
    }
}
