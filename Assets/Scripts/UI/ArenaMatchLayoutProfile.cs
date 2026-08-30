using UnityEngine;

[CreateAssetMenu(menuName = "MTGB/Arena Match Layout Profile", fileName = "ArenaMatchLayoutProfile")]
public sealed class ArenaMatchLayoutProfile : ScriptableObject
{
    [SerializeField] private ArenaMatchLayoutSettings layout = new ArenaMatchLayoutSettings();

    public ArenaMatchLayoutSettings Layout
    {
        get
        {
            EnsureLayout();
            return layout;
        }
    }

    public string GetLayoutJson()
    {
        EnsureLayout();
        return JsonUtility.ToJson(layout);
    }

    public void ApplyLayoutJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return;
        ArenaMatchLayoutSettings incoming = JsonUtility.FromJson<ArenaMatchLayoutSettings>(json);
        if (incoming == null) return;
        EnsureLayout();
        layout.CopyFrom(incoming);
    }

    public void ResetLayout()
    {
        layout = new ArenaMatchLayoutSettings();
    }

    private void EnsureLayout()
    {
        if (layout == null) layout = new ArenaMatchLayoutSettings();
        layout.Sanitize();
    }
}
