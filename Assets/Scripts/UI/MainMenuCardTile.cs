using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public enum MainMenuCardTileZone
{
    Collection,
    MainDeck,
    Sideboard
}

public sealed class MainMenuCardTile : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    private MainMenuController controller;
    private CardDefinition card;
    private MainMenuCardTileZone zone;
    private Outline outline;
    private bool suppressNextClick;

    public CardDefinition Card => card;
    public MainMenuCardTileZone Zone => zone;

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
            ? new Color(1f, 0.72f, 0.20f, 0.95f)
            : new Color(0f, 0f, 0f, 0.65f);
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

        if (eventData.button == PointerEventData.InputButton.Right)
        {
            controller?.OnCardTileClicked(this);
        }
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
        controller?.EndCardHover(this);
    }
}
