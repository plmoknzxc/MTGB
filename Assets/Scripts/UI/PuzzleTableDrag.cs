using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class PuzzleTableDrag : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerEnterHandler, IPointerExitHandler
{
    public RectTransform overlay;
    public string cardName;
    public Action<int,int,int> place;
    public GameObject catalogReader;
    public Action closeReader;
    RectTransform ghost;
    DeckHoverArtPopup preview;
    CardDefinition previewCard;
    bool showing;

    public void BindPreview(DeckHoverArtPopup popup, CardDefinition card)
    {
        if (preview == popup && previewCard == card) return;
        preview = popup;
        previewCard = card;
        if (showing) ShowPreview();
    }

    public void OnPointerEnter(PointerEventData e)
    {
        if (!e.dragging && (catalogReader == null || !catalogReader.activeSelf) && GetComponent<Button>().IsInteractable()) ShowPreview();
    }

    public void OnPointerExit(PointerEventData e) => HidePreview();

    void ShowPreview()
    {
        if (previewCard == null || preview == null || (catalogReader != null && catalogReader.activeSelf))
        { HidePreview(); return; }
        showing = true;
        preview.transform.SetAsLastSibling();
        preview.Show(overlay, previewCard, (RectTransform)transform, null, namedFaces:true);
    }

    void HidePreview()
    {
        if (showing && preview != null) preview.Hide();
        showing = false;
    }

    public void OnBeginDrag(PointerEventData e)
    {
        if (place == null || !GetComponent<Button>().IsInteractable()) return;
        HidePreview();
        if (catalogReader != null && catalogReader.activeSelf) closeReader?.Invoke();
        e.eligibleForClick = false;
        ghost = ReplayWidgets.Panel(overlay,"DraggingCard",0,0,184,52,MenuVisualTheme.Raised);
        ghost.gameObject.AddComponent<CanvasGroup>().blocksRaycasts=false;
        ReplayWidgets.Label(ghost,cardName,0,0,178,48,19,TextAnchor.MiddleCenter);
        OnDrag(e);
    }
    public void OnDrag(PointerEventData e)
    {
        if(ghost!=null && RectTransformUtility.ScreenPointToLocalPointInRectangle(overlay,e.position,e.pressEventCamera,out var point))
            ghost.anchoredPosition=point;
    }
    public void OnEndDrag(PointerEventData e)
    {
        if(ghost==null)return;
        var slot=e.pointerCurrentRaycast.gameObject?.GetComponentInParent<PuzzleTableDrop>();
        Clear();
        if(slot!=null)place(slot.side,slot.zone,slot.place);
    }
    void OnDisable(){HidePreview();Clear();}
    void Clear(){if(ghost!=null)Destroy(ghost.gameObject);ghost=null;}
}
