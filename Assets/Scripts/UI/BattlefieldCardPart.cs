using UnityEngine;

// Stable binding survives renaming/reordering the visible prefab children.
// Each piece is an ordinary mesh or 3D TMP component, never a card Canvas.
[DisallowMultipleComponent]
public sealed class BattlefieldCardPart : MonoBehaviour
{
    [SerializeField] string binding;
    [SerializeField] Color tint = Color.white;
    public string Binding => binding;
    public Color Tint => tint;
    public void Configure(string key, Color color) { binding = key; tint = color; }
}
