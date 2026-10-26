using System;
using UnityEngine;
using UnityEngine.EventSystems;

// A click that merely reveals the tucked hand must not choose a card as well.
public sealed class BattlefieldRedesignHandInput : MonoBehaviour, IPointerDownHandler
{
    private Func<bool> expand;
    private bool revealClick;
    public void Configure(Func<bool> onExpand) => expand = onExpand;
    public void OnPointerDown(PointerEventData data)
    {
        revealClick = data.button == PointerEventData.InputButton.Left && expand?.Invoke() == true;
    }
    public bool ConsumeRevealClick()
    {
        bool result = revealClick;
        revealClick = false;
        return result;
    }
}
