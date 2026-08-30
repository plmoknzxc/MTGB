using UnityEngine;

[DisallowMultipleComponent]
public sealed class ArenaMatchLayoutController : MonoBehaviour
{
    [SerializeField] private ArenaMatchLayoutProfile profile;
    [SerializeField] private ArenaMatchLayoutSettings layout = new ArenaMatchLayoutSettings();

    private MTGAHandUiController handController;
    private ArenaWhiteboxCardSync battlefieldController;
    private ArenaWhiteboxInteraction interactionController;
    private ArenaHudController hudController;

    public ArenaMatchLayoutProfile Profile => profile;

    public ArenaMatchLayoutSettings Layout
    {
        get
        {
            EnsureLayout();
            return profile != null ? profile.Layout : layout;
        }
    }

    private void Awake()
    {
        ResolveControllers();
        ApplyAll();
    }

    private void Start()
    {
        ApplyAll();
    }

    public string GetLayoutJson()
    {
        EnsureLayout();
        return profile != null ? profile.GetLayoutJson() : JsonUtility.ToJson(layout);
    }

    public void ApplyLayoutJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return;

        if (profile != null)
        {
            profile.ApplyLayoutJson(json);
        }
        else
        {
            ArenaMatchLayoutSettings incoming = JsonUtility.FromJson<ArenaMatchLayoutSettings>(json);
            if (incoming == null) return;
            EnsureLayout();
            layout.CopyFrom(incoming);
        }

        ApplyAll();
    }

    public void ResetLayout()
    {
        if (profile != null) profile.ResetLayout();
        else layout = new ArenaMatchLayoutSettings();
        ApplyAll();
    }

    public void ApplyAll()
    {
        EnsureLayout();
        ResolveControllers();
        ArenaMatchLayoutSettings settings = Layout;
        handController?.ApplyLayoutSettings(settings.hand);
        if (hudController == null) hudController = Object.FindFirstObjectByType<ArenaHudController>();
        battlefieldController?.ApplyLayoutSettings(settings.battlefield);
        interactionController?.ApplyLayoutSettings(settings.stack, settings.hud);
        hudController?.ApplyLayoutSettings(settings.hud);
    }

    public void SetProfile(ArenaMatchLayoutProfile value)
    {
        profile = value;
        ApplyAll();
    }

    private void ResolveControllers()
    {
        if (handController == null) handController = Object.FindFirstObjectByType<MTGAHandUiController>();
        if (battlefieldController == null) battlefieldController = Object.FindFirstObjectByType<ArenaWhiteboxCardSync>();
        if (interactionController == null) interactionController = Object.FindFirstObjectByType<ArenaWhiteboxInteraction>();
        if (hudController == null) hudController = Object.FindFirstObjectByType<ArenaHudController>();
    }

    private void EnsureLayout()
    {
        if (layout == null) layout = new ArenaMatchLayoutSettings();
        layout.Sanitize();
    }
}
