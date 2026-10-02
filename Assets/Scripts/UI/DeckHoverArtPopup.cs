using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.InputSystem;

public sealed class DeckHoverArtPopup : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    private readonly List<Sprite> images=new List<Sprite>();
    private readonly List<string> imageNames=new List<string>();
    private readonly List<string> imageGroups=new List<string>();
    private readonly List<DeckRelatedTokenArt.Art> dungeons=new List<DeckRelatedTokenArt.Art>();
    private int dungeonSlot=-1, dungeonIndex;
    private bool rightWasDown;
    private string relatedSignature;
    private readonly List<DeckKeywordGlossary.Entry> keywords=new List<DeckKeywordGlossary.Entry>();
    private RectTransform surface,sourceAnchor;
    private Vector2 surfaceSize;
    private int preferenceRevision;
    private Coroutine loading;
    private Coroutine closeDelay;
    private int generation;
    public RectTransform Rect=>(RectTransform)transform;

    public void Show(RectTransform root,MainMenuCardTile tile,Font textFont,string format=null)
    {
        Show(root,tile.Card,(RectTransform)tile.transform,textFont,format);
    }
    public void Show(RectTransform root,CardDefinition card,RectTransform anchor,Font textFont,string format=null)
    {
        if(closeDelay!=null){StopCoroutine(closeDelay);closeDelay=null;}
        gameObject.SetActive(true);surface=root;sourceAnchor=anchor;
        int request=++generation;
        if(loading!=null)StopCoroutine(loading);
        ClearContent();relatedSignature=null;
        rightWasDown=Mouse.current!=null&&Mouse.current.rightButton.isPressed;
        if(card==null||anchor==null){Hide();return;}
        CardImageLibrary.TryLoadFullCard(card,out var front);
        images.Add(front);imageNames.Add(GamePreferences.CardName(card));imageGroups.Add(null);
        if((card.features!=null&&card.features.Exists(f=>f!=null&&f.faceIndex>0))||CardImageLibrary.HasBackFace(card))
        {
            CardImageLibrary.TryLoadBackFace(card,out var back);
            images.Add(back);imageNames.Add(GamePreferences.CardName(card));imageGroups.Add(null);
        }
        AddKeywords(GamePreferences.CardRules(card),card);
        if(card.features!=null)foreach(var feature in card.features)AddKeywords(GamePreferences.FeatureRules(feature),card);
        Build();
        loading=StartCoroutine(DeckRelatedTokenArt.Load(card,related=>{
            if(this==null||!isActiveAndEnabled||request!=generation)return;
            foreach(var art in related)
            {
                AddRelated(art);AddKeywords(art.rules);
            }
            Build();
        },format));
    }
    public void ShowRelatedTokens(RectTransform root,DeckKeywordGlossary.Entry mechanic,IReadOnlyList<DeckRelatedTokenArt.Art> related,RectTransform anchor)
    {
        if(closeDelay!=null){StopCoroutine(closeDelay);closeDelay=null;}
        string signature=(mechanic?.English??"")+":"+string.Join("|",System.Linq.Enumerable.Select(related??System.Array.Empty<DeckRelatedTokenArt.Art>(),a=>a.key));
        if(isActiveAndEnabled&&sourceAnchor==anchor&&surface==root&&relatedSignature==signature&&preferenceRevision==GamePreferences.Revision)return;
        if(loading!=null){StopCoroutine(loading);loading=null;}
        generation++;gameObject.SetActive(true);surface=root;sourceAnchor=anchor;
        ClearContent();relatedSignature=signature;
        rightWasDown=Mouse.current!=null&&Mouse.current.rightButton.isPressed;
        if(related!=null)foreach(var token in related)
        {
            if(token==null)continue;
            AddRelated(token);
        }
        if(mechanic!=null&&mechanic.TokenKey==null)keywords.Add(mechanic);
        Build();
    }
    private void ClearContent()
    {
        images.Clear();imageNames.Clear();imageGroups.Clear();keywords.Clear();dungeons.Clear();dungeonSlot=-1;dungeonIndex=0;
    }
    private void AddRelated(DeckRelatedTokenArt.Art art)
    {
        if(art.group=="dungeon")
        {
            dungeons.Add(art);
            if(dungeonSlot>=0)return;
            dungeonSlot=images.Count;
        }
        images.Add(art.sprite);imageNames.Add(art.name);imageGroups.Add(art.group);
    }
    public bool CycleDungeon()
    {
        if(dungeonSlot<0||dungeons.Count<2)return false;
        dungeonIndex=(dungeonIndex+1)%dungeons.Count;
        images[dungeonSlot]=dungeons[dungeonIndex].sprite;imageNames[dungeonSlot]=dungeons[dungeonIndex].name;
        Build();return true;
    }
    private void Update()
    {
        var mouse=Mouse.current;
        if(mouse==null)return;
        bool down=mouse.rightButton.isPressed,pressed=down&&!rightWasDown;
        rightWasDown=down;
        if(!pressed||surface==null||sourceAnchor==null)return;
        var canvas=surface.GetComponentInParent<Canvas>();
        var camera=canvas!=null&&canvas.renderMode!=RenderMode.ScreenSpaceOverlay?canvas.worldCamera:null;
        var point=mouse.position.ReadValue();
        if(RectTransformUtility.RectangleContainsScreenPoint(sourceAnchor,point,camera)||RectTransformUtility.RectangleContainsScreenPoint(Rect,point,camera))CycleDungeon();
    }
    public void Hide(){if(this==null)return;CancelPending();gameObject.SetActive(false);}
    public void RequestHide(){if(this!=null&&closeDelay==null&&isActiveAndEnabled)closeDelay=StartCoroutine(HideAfterDelay());}
    private void CancelPending(){generation++;if(loading!=null){StopCoroutine(loading);loading=null;}if(closeDelay!=null){StopCoroutine(closeDelay);closeDelay=null;}}
    private void OnDisable(){CancelPending();}
    public void OnPointerEnter(PointerEventData eventData){if(closeDelay!=null){StopCoroutine(closeDelay);closeDelay=null;}}
    public void OnPointerExit(PointerEventData eventData){RequestHide();}
    private System.Collections.IEnumerator HideAfterDelay(){yield return new WaitForSecondsRealtime(.12f);closeDelay=null;Hide();}
    private void AddKeywords(string rules,CardDefinition card=null)
    {
        foreach(var entry in DeckKeywordGlossary.Find(rules,card:card))
            if(!keywords.Exists(k=>k.English==entry.English))keywords.Add(entry);
    }
    private void Build()
    {
        if(surface==null||sourceAnchor==null)return;
        surfaceSize=surface.rect.size;
        preferenceRevision=GamePreferences.Revision;
        foreach(Transform child in transform){child.gameObject.SetActive(false);Destroy(child.gameObject);}
        float availableW=surface.rect.width-32,availableH=surface.rect.height*.86f;
        float helpWidth=keywords.Count>0?Mathf.Min(340,availableW*.27f):0;
        float scale=GamePreferences.TextScale;
        float captionHeight=(imageGroups.Contains("dungeon")?72:imageGroups.Contains("ring")?58:0)*scale;
        int count=Mathf.Max(1,images.Count),columns=Mathf.Min(3,count),rows=Mathf.CeilToInt(count/(float)columns);
        float cardWidth=Mathf.Min(360,Mathf.Min((availableW-helpWidth-16*columns)/columns,(availableH-12*(rows-1)-captionHeight*rows)/rows/1.4f));
        float cardHeight=cardWidth*1.4f,imageWidth=columns*(cardWidth+12)-12;
        float height=rows*(cardHeight+captionHeight+12)-12,width=imageWidth+(helpWidth>0?helpWidth+16:0);
        Rect.sizeDelta=new Vector2(width,height);Rect.anchorMin=Rect.anchorMax=Rect.pivot=Vector2.zero;
        var corners=new Vector3[4];sourceAnchor.GetWorldCorners(corners);
        Vector2 low=(Vector2)surface.InverseTransformPoint(corners[0])-surface.rect.min;
        Vector2 high=(Vector2)surface.InverseTransformPoint(corners[2])-surface.rect.min;
        float x=high.x+16;if(x+width>surface.rect.width-12)x=low.x-width-16;
        for(int i=0;i<images.Count;i++)
        {
            var image=NewImage("CardArt"+i,transform,new Color(.02f,.025f,.035f));
            float imageX=i%columns*(cardWidth+12),imageY=-i/columns*(cardHeight+captionHeight+12);
            Place(image.rectTransform,imageX,imageY-captionHeight,cardWidth,cardHeight);
            if(!string.IsNullOrEmpty(imageGroups[i]))
            {
                // Raised black title tab and a rounded black surround, as on the
                // reference helper-card preview. The card image stays unmodified.
                var surround=NewImage("HelperFrame"+i,transform,Color.black);
                surround.sprite=IntegratedUiUtility.RoundedRect(360,504,18);
                Place(surround.rectTransform,imageX,imageY-captionHeight+8,cardWidth,cardHeight+8);
                image.transform.SetParent(surround.transform,false);
                Place(image.rectTransform,12,-12,cardWidth-24,cardHeight-16);
                var heading=NewImage("HelperHeading"+i,transform,Color.black);
                heading.sprite=IntegratedUiUtility.RoundedRect(300,80,12);
                Place(heading.rectTransform,imageX+cardWidth*.1f,imageY,cardWidth*.8f,captionHeight);
                var caption=CardSymbolText.Create(heading.transform,"HelperTitle"+i,28,Color.white);
                caption.font=GamePreferences.TmpFont;caption.alignment=TMPro.TextAlignmentOptions.Center;
                caption.enableAutoSizing=false;caption.enableWordWrapping=false;
                caption.fontStyle=TMPro.FontStyles.Bold;
                caption.gameObject.AddComponent<PreferenceText>().SetBaseSize(28);
                bool zh=GamePreferences.Language.StartsWith("zh"),ja=GamePreferences.Language.StartsWith("ja");
                bool dungeon=imageGroups[i]=="dungeon";
                caption.text=dungeon?(zh?"地城":ja?"ダンジョン":"Dungeon"):(zh?"魔戒":ja?"指輪":"The Ring");
                Place(caption.rectTransform,0,-4*scale,cardWidth*.8f,44*scale);
                if(dungeon)
                {
                    var number=CardSymbolText.Create(heading.transform,"HelperNumber"+i,18,Color.white);
                    number.font=GamePreferences.TmpFont;number.alignment=TMPro.TextAlignmentOptions.Center;
                    number.enableAutoSizing=false;number.enableWordWrapping=false;
                    number.gameObject.AddComponent<PreferenceText>().SetBaseSize(18);
                    number.text=(dungeonIndex+1)+"/"+dungeons.Count;
                    Place(number.rectTransform,0,-44*scale,cardWidth*.8f,24*scale);
                }
            }
            image.sprite=images[i];image.preserveAspect=true;
            if(images[i]!=null)image.color=Color.white;
            else
            {
                var missing=CardSymbolText.Create(image.transform,"MissingCardArt",20,MenuVisualTheme.Paper);
                missing.font=GamePreferences.TmpFont;missing.richText=false;
                missing.alignment=TMPro.TextAlignmentOptions.Center;missing.enableWordWrapping=true;
                string label=GamePreferences.Language.StartsWith("zh")?"缺失卡图":GamePreferences.Language.StartsWith("ja")?"カード画像なし":"Missing card image";
                missing.text=imageNames[i]+"\n\n"+label;
                Place(missing.rectTransform,16,-16,cardWidth-32,cardHeight-32);
                missing.gameObject.AddComponent<PreferenceText>().SetBaseSize(20);
            }
        }
        if(helpWidth>0)
        {
            var shell=NewImage("KeywordHelp",transform,Color.clear);
            Place(shell.rectTransform,imageWidth+16,0,helpWidth,height);
            shell.gameObject.AddComponent<RectMask2D>();
            var content=new GameObject("Content",typeof(RectTransform)).GetComponent<RectTransform>();content.SetParent(shell.transform,false);
            content.anchorMin=content.anchorMax=content.pivot=new Vector2(0,1);
            float y=0;
            foreach(var entry in keywords)
            {
                var block=NewImage("Keyword_"+entry.English,content,new Color(.045f,.05f,.065f,1));
                block.sprite=IntegratedUiUtility.RoundedRect(340,180,8);block.material=null;
                var text=CardSymbolText.Create(block.transform,"Mechanic",18,MenuVisualTheme.Paper);
                text.font=GamePreferences.TmpFont;text.richText=true;
                text.enableAutoSizing=false;text.enableWordWrapping=true;text.alignment=TMPro.TextAlignmentOptions.TopLeft;
                text.lineSpacing=4;
                text.text="<b><color=#D0B779>"+entry.Name+"</color></b>\n"+CardSymbolText.Format(entry.Help);
                text.gameObject.AddComponent<PreferenceText>().SetBaseSize(18);
                float h=Mathf.Ceil(text.GetPreferredValues(text.text,helpWidth-44,10000).y)+32;
                Place(block.rectTransform,0,-y,helpWidth,h);
                Place(text.rectTransform,16,-16,helpWidth-44,h-32);y+=h+10;
            }
            y=Mathf.Max(0,y-10);content.sizeDelta=new Vector2(helpWidth,y);
            var scroll=shell.gameObject.AddComponent<ScrollRect>();scroll.viewport=shell.rectTransform;scroll.content=content;
            scroll.horizontal=false;scroll.vertical=true;scroll.scrollSensitivity=40;scroll.movementType=ScrollRect.MovementType.Clamped;
            // The explanation column may be taller than the card art. Use the
            // available screen height before scrolling, and show a thumb when needed.
            float helpHeight=Mathf.Min(availableH,y);
            shell.rectTransform.sizeDelta=new Vector2(helpWidth,helpHeight);
            height=Mathf.Max(height,helpHeight);
            if(y>helpHeight)
            {
                var track=NewImage("HelpScrollbar",shell.transform,new Color(.15f,.18f,.23f,.9f));
                Place(track.rectTransform,helpWidth-10,0,6,helpHeight);
                var slide=new GameObject("SlidingArea",typeof(RectTransform)).GetComponent<RectTransform>();
                slide.SetParent(track.transform,false);slide.anchorMin=Vector2.zero;slide.anchorMax=Vector2.one;slide.offsetMin=slide.offsetMax=Vector2.zero;
                var thumb=NewImage("Handle",slide,new Color(.5f,.6f,.73f));
                thumb.rectTransform.anchorMin=Vector2.zero;thumb.rectTransform.anchorMax=Vector2.one;thumb.rectTransform.offsetMin=thumb.rectTransform.offsetMax=Vector2.zero;
                var bar=track.gameObject.AddComponent<Scrollbar>();bar.handleRect=thumb.rectTransform;bar.targetGraphic=thumb;bar.direction=Scrollbar.Direction.BottomToTop;
                scroll.verticalScrollbar=bar;
            }
        }
        Rect.sizeDelta=new Vector2(width,height);
        Rect.anchoredPosition=new Vector2(Mathf.Clamp(x,12,surface.rect.width-width-12),Mathf.Clamp(high.y-height,12,surface.rect.height-height-12));
        transform.SetAsLastSibling();
    }
    private void LateUpdate(){if(surface!=null&&sourceAnchor!=null&&(surface.rect.size!=surfaceSize||preferenceRevision!=GamePreferences.Revision))Build();}
    private static Image NewImage(string name,Transform parent,Color color)
    {
        var go=new GameObject(name,typeof(RectTransform),typeof(Image));go.transform.SetParent(parent,false);
        var image=go.GetComponent<Image>();image.color=color;return image;
    }
    private static void Place(RectTransform rect,float x,float y,float w,float h)
    {rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(0,1);rect.anchoredPosition=new Vector2(x,y);rect.sizeDelta=new Vector2(w,h);}
}
