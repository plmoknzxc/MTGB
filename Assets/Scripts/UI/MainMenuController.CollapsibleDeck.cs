using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

public sealed partial class MainMenuController
{
    private bool detailsCollapsed, sideboardCollapsed, floatingDeckDetails;
    private float detailsCollapseProgress, sideboardCollapseProgress, floatingCloseAt;
    private RectTransform deckLayoutRoot, deckArea, mainArea, sideArea;
    private Button detailsCollapseButton, sideboardCollapseButton;
    private CanvasGroup sideAreaGroup, sideHeadingGroup;
    private float expandedDeckWidth;
    private DeckHoverArtPopup deckArtHover;
    private DeckHoverArtPopup detailsArtHover;

    private void CreateDeckCollapseControls()
    {
        deckLayoutRoot=(RectTransform)deckBuilderGroup.transform;
        RectTransform FindArea(string name)=>System.Array.Find(deckLayoutRoot.GetComponentsInChildren<RectTransform>(true),r=>r.name==name);
        deckArea=FindArea("DeckBackplate");mainArea=FindArea("MainDeckScroll");sideArea=FindArea("SideboardScroll");
        if(deckArea==null||mainArea==null||sideArea==null)return;
        var grid=mainDeckContent.GetComponent<GridLayoutGroup>();
        expandedDeckWidth=grid.cellSize.x*4+grid.spacing.x*3+grid.padding.horizontal+74;
        sideAreaGroup=sideArea.GetComponent<CanvasGroup>();
        if(sideAreaGroup==null)sideAreaGroup=sideArea.gameObject.AddComponent<CanvasGroup>();
        sideHeadingGroup=sideboardCountText.GetComponent<CanvasGroup>();
        if(sideHeadingGroup==null)sideHeadingGroup=sideboardCountText.gameObject.AddComponent<CanvasGroup>();
        detailsCollapseButton=MakeCollapseArrow("CollapseCardDetails","‹",()=>{
            detailsCollapsed=!detailsCollapsed;floatingDeckDetails=false;previewVisible=false;if(detailsArtHover!=null)detailsArtHover.Hide();
            hoveredTile=null;floatingCloseAt=0;
        });
        sideboardCollapseButton=MakeCollapseArrow("CollapseSideboard","‹",()=>sideboardCollapsed=!sideboardCollapsed);
        UpdateDeckCollapseLayout();
    }

    private Button MakeCollapseArrow(string name,string label,UnityEngine.Events.UnityAction action)
    {
        var go=new GameObject(name,typeof(RectTransform),typeof(Image),typeof(Button));
        go.transform.SetParent(deckLayoutRoot,false);
        var image=go.GetComponent<Image>();image.sprite=IntegratedUiUtility.RoundedRect(32,32,8);
        image.color=MenuVisualTheme.Raised;
        var button=go.GetComponent<Button>();button.onClick.AddListener(action);
        var text=AddText(go.transform,"Arrow",label,36,MenuVisualTheme.Paper,TextAnchor.MiddleCenter);
        text.raycastTarget=false;Stretch(text.rectTransform,0,0,0,0);
        go.AddComponent<DeckCollapseArrowHover>();
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
        float right=w*.745f,normalLeft=right-expandedDeckWidth,left=Mathf.Lerp(normalLeft,60,d);
        DeckRegion(deckArea,left,h*.01f,right-left,h*.89f);
        DeckRegion(mainDeckCountText.rectTransform,left+10,h*.86f,right-left-20,h*.04f);
        float bottom=Mathf.Lerp(h*.315f,h*.055f,s);
        DeckRegion(mainArea,left+10,bottom,right-left-20,h*.855f-bottom);
        // Slide the sideboard out intact, rather than scaling its cards during the transition.
        DeckRegion(sideArea,left+10,h*.01f-s*h*.28f,right-left-20,h*.26f);
        sideAreaGroup.alpha=1-s;sideAreaGroup.blocksRaycasts=sideAreaGroup.interactable=!sideboardCollapsed;
        DeckRegion(sideboardCountText.rectTransform,left+10,Mathf.Lerp(h*.275f,h*.013f,s),right-left-68,h*.035f);
        sideHeadingGroup.alpha=1-s;
        DeckRegion((RectTransform)sideboardCollapseButton.transform,(left+right)*.5f-24,Mathf.Lerp(h*.275f-5,h*.01f,s),48,42);
        sideboardCollapseButton.GetComponentInChildren<Text>().rectTransform.localRotation=Quaternion.Euler(0,0,sideboardCollapsed?-90:90);
        DeckRegion((RectTransform)detailsCollapseButton.transform,Mathf.Lerp(normalLeft-48,6,d),h*.455f-46,42,92);
        detailsCollapseButton.GetComponentInChildren<Text>().text=detailsCollapsed?"›":"‹";
        ResizeDeckColumns(mainDeckContent);ResizeDeckColumns(sideboardContent);

        if(floatingDeckDetails)
        {
            bool over=deckArtHover!=null&&Mouse.current!=null&&RectTransformUtility.RectangleContainsScreenPoint(deckArtHover.Rect,
                Mouse.current.position.ReadValue(),DeckPointerCamera());
            if(draggedTile!=null||(!over&&hoveredTile==null&&Time.unscaledTime>=floatingCloseAt))floatingDeckDetails=false;
        }
        if(!floatingDeckDetails&&deckArtHover!=null&&deckArtHover.gameObject.activeSelf)deckArtHover.Hide();
        float detailWidth=Mathf.Max(1,normalLeft-10-w*.005f);
        DeckRegion(previewPanelRect,w*.005f-d*(detailWidth+40),h*.01f,detailWidth,h*.89f);
        cardPreviewGroup.alpha=1-d;
        cardPreviewGroup.blocksRaycasts=cardPreviewGroup.interactable=!detailsCollapsed;
    }

    private static void ResizeDeckColumns(RectTransform content)
    {
        var grid=content.GetComponent<GridLayoutGroup>();
        const float preferredWidth=180;
        float available=content.rect.width-grid.padding.horizontal;
        int columns=Mathf.Max(1,Mathf.FloorToInt((available+grid.spacing.x)/(preferredWidth+grid.spacing.x)));
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
