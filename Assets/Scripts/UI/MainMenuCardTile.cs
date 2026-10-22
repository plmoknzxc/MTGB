using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public enum MainMenuCardTileZone
{
    Collection,
    MainDeck,
    Sideboard,
    Commander
}

public sealed class MainMenuCardTile : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    private MainMenuController controller;
    private CardDefinition card;
    private MainMenuCardTileZone zone;
    private Outline outline;
    private bool suppressNextClick;
    private GameObject quantityControls;
    private Text quantityText;
    private Button decreaseButton;
    private Button increaseButton;
    private GameObject countBadge;
    private Text countBadgeText;
    private bool singleCopyDisplay;
    public void SetSingleCopyDisplay(bool single){singleCopyDisplay=single;}

    public CardDefinition Card => card;
    public int CopyOrderIndex { get; set; } = -1;
    public MainMenuCardTileZone Zone => zone;
    public bool QuantityControlsVisible => quantityControls != null && quantityControls.activeSelf;

    public void ConfigureQuantityControls(GameObject controls, Text count, Button decrease, Button increase, GameObject badge)
    {
        quantityControls = controls;
        quantityText = count;
        decreaseButton = decrease;
        increaseButton = increase;
        countBadge = badge;
        countBadgeText = badge == null ? null : badge.GetComponentInChildren<Text>();
        SetQuantityControls(false, 0, false);
    }

    public void SetCount(int count)
    {
        if (countBadgeText != null) countBadgeText.text = "x" + count;
        if (countBadge != null) countBadge.SetActive(!singleCopyDisplay && count > 1 && !QuantityControlsVisible);
    }

    public void SetQuantityControls(bool visible, int count, bool canIncrease)
    {
        if (quantityControls == null) return;
        quantityText.text = singleCopyDisplay?"1":"x"+count;
        decreaseButton.interactable = count > 0;
        increaseButton.interactable = canIncrease;
        quantityControls.SetActive(visible);
        if (countBadge != null) countBadge.SetActive(!singleCopyDisplay && !visible && count > 1);
    }

    public void Configure(MainMenuController owner, CardDefinition cardDefinition, MainMenuCardTileZone tileZone)
    {
        controller = owner;
        card = cardDefinition;
        zone = tileZone;
        outline = GetComponent<Outline>();
        SetHighlighted(false);
    }

    public void SetHighlighted(bool highlighted)
    {
        if (outline == null) outline = GetComponent<Outline>();
        if (outline == null) return;

        outline.effectColor = highlighted
            ? MenuVisualTheme.Accent
            : MenuVisualTheme.Line;
        outline.effectDistance = highlighted ? new Vector2(3f, 3f) : new Vector2(1.5f, -1.5f);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        controller?.BeginCardHover(this);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        controller?.EndCardHover(this);
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        // Suppress only a click emitted by the drag gesture itself. A completed
        // drag usually emits no click, so the next press starts a fresh gesture.
        if (eventData.button == PointerEventData.InputButton.Left) suppressNextClick = false;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (suppressNextClick)
        {
            suppressNextClick = false;
            return;
        }

        if (eventData.button == PointerEventData.InputButton.Left)
        {
            controller?.OnCardTileSelected(this);
            return;
        }

        // Right-click no longer changes deck quantities.
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left) return;
        suppressNextClick = true;
        controller?.BeginCardDrag(this, eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        controller?.UpdateCardDrag(eventData);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        controller?.EndCardDrag(this, eventData);
    }

    private void OnDisable()
    {
        suppressNextClick = false;
        controller?.EndCardHover(this);
    }
}
