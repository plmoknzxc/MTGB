using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// uGUI only raises onValueChanged for a different option. Explicitly choosing the
// current format should also restore its filter after the player edited or reset it.
public sealed class DeckFormatReselect : MonoBehaviour, IPointerClickHandler, ISubmitHandler
{
    private Dropdown dropdown;

    private void Start()
    {
        if (GetComponent<Toggle>().isOn)
            dropdown = GetComponentInParent<Dropdown>();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left) Reapply();
    }

    public void OnSubmit(BaseEventData eventData) => Reapply();

    private void Reapply()
    {
        if (dropdown == null) return;
        var owner = dropdown;
        dropdown = null;
        owner.onValueChanged.Invoke(owner.value);
    }
}
