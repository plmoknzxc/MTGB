using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class ButtonPressSound : MonoBehaviour, IPointerDownHandler, ISubmitHandler
{
    Button button;
    void Awake() => button = GetComponent<Button>();
    public static void Bind(Button target)
    {
        if (Application.isPlaying && !target.TryGetComponent<ButtonPressSound>(out _))
            target.gameObject.AddComponent<ButtonPressSound>();
    }
    public void OnPointerDown(PointerEventData data)
    {
        if (data.button == PointerEventData.InputButton.Left && button != null && button.IsInteractable())
            GameAudio.Play(TableSound.Press);
    }
    public void OnSubmit(BaseEventData data)
    {
        if (button != null && button.IsInteractable()) GameAudio.Play(TableSound.Press);
    }
}
