using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class OnlineSideboardCardTile : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    private OnlineLobbyPanel owner;
    private Outline outline;

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
            owner?.BeginSideboardCardDrag(this, eventData);
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
