using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class OnlineSideboardCardTile : MonoBehaviour, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    private OnlineLobbyPanel owner;
    private Outline outline;
    private bool suppressNextClick;
    private GameObject quantityControls, countBadge;
    private Button decreaseButton, increaseButton;

    public void ConfigureQuantityControls(GameObject controls, GameObject badge, Button decrease, Button increase)
    {
        quantityControls = controls; countBadge = badge;
        decreaseButton = decrease; increaseButton = increase;
    }

    public void SetQuantityControls(bool visible, bool canIncrease)
    {
        if (quantityControls == null) return;
        quantityControls.SetActive(visible);
        countBadge.SetActive(!visible);
        decreaseButton.interactable = visible && Entry.count > 0;
        increaseButton.interactable = visible && canIncrease;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (suppressNextClick) { suppressNextClick = false; return; }
        if (eventData.button == PointerEventData.InputButton.Left) owner?.SelectSideboardQuantity(this);
    }

    public DeckEntry Entry { get; private set; }
    public CardDefinition Card { get; private set; }
    public Sprite CardSprite { get; private set; }
    public bool IsSideboard { get; private set; }

    public void Configure(
        OnlineLobbyPanel panel,
        DeckEntry entry,
        CardDefinition card,
        Sprite cardSprite,
        bool isSideboard)
    {
        owner = panel;
        Entry = entry;
        Card = card;
        CardSprite = cardSprite;
        IsSideboard = isSideboard;
        outline = GetComponent<Outline>();
        SetDragging(false);
    }

    public void SetDragging(bool dragging)
    {
        if (outline == null) outline = GetComponent<Outline>();
        if (outline == null) return;
        outline.effectColor = dragging ? ObsidianArenaTheme.Blue : ObsidianArenaTheme.LineSoft;
        outline.effectDistance = dragging ? new Vector2(3f, 3f) : new Vector2(1.5f, -1.5f);
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left)
        {
            suppressNextClick = true;
            owner?.BeginSideboardCardDrag(this, eventData);
        }
    }

    public void OnDrag(PointerEventData eventData)
    {
        owner?.UpdateSideboardCardDrag(eventData);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        owner?.EndSideboardCardDrag(this, eventData);
    }
}
