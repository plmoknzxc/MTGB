using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// The page header, gutter and scrollbar are siblings of the scrolling viewport.
// Wheel events over its content already go directly to the ScrollRect.
public sealed class JournalPageScroll : MonoBehaviour, IScrollHandler
{
    private ScrollRect page;

    public void SetPage(ScrollRect visiblePage)
    {
        page = visiblePage;
        page.scrollSensitivity = 48f;
    }

    public void OnScroll(PointerEventData eventData)
    {
        if (page != null && page.isActiveAndEnabled)
            page.OnScroll(eventData);
    }
}
