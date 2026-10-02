using System;
using UnityEngine;
using UnityEngine.EventSystems;

public sealed class ForgeChoiceDismiss : MonoBehaviour, IPointerClickHandler
{
    public Action Dismiss;
    public bool ListenForRightClick;
    private void Update()
    {
        // Listen on the backdrop even when a button/scroll view owns the pointer event.
        if(ListenForRightClick && UnityEngine.InputSystem.Mouse.current?.rightButton.wasPressedThisFrame==true)
            Dismiss?.Invoke();
    }
    public void OnPointerClick(PointerEventData data)
    {
        if(data.button==PointerEventData.InputButton.Left || data.button==PointerEventData.InputButton.Right)Dismiss?.Invoke();
    }
}
