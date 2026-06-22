using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class ArenaWhiteboxCardSync : MonoBehaviour
{
    [SerializeField] private GameRules gameRules;
    [SerializeField] private Transform cardRoot;

    private ArenaWhiteboxInteraction interaction;

    private readonly List<Transform> playerHandCards = new List<Transform>(12);
    private readonly List<Transform> opponentHandCards = new List<Transform>(12);
    private readonly List<Transform> playerBattlefieldCards = new List<Transform>(12);
    private readonly List<Transform> opponentBattlefieldCards = new List<Transform>(12);
    private readonly Dictionary<Renderer, Color> baseColors = new Dictionary<Renderer, Color>(64);
    private static readonly Dictionary<Texture, Material> FrameMaterials = new Dictionary<Texture, Material>(12);
    private static Material highlightMaterial;
    private float nextLegacyHandCleanupTime;

    private static readonly string[] SceneDecorationNames =
    {
        "ArenaCenterDivider",
        "ArenaCenterNeon",
        "ArenaPlayerZoneGlow",
        "ArenaOpponentZoneGlow",
        "ArenaBottomRail",
        "ArenaTopRail",
        "ArenaLeftPlinth",
        "ArenaRightPlinth",
        "PlayerAvatarMedallion",
        "PlayerLifeBadge",
        "OpponentAvatarMedallion",
        "OpponentLifeBadge",
        "MTGA_CenterRail",
        "MTGA_CenterGlow",
        "MTGA_PlayerActionRail",
        "MTGA_OpponentActionRail",
        "MTGA_BottomRail",
        "MTGA_TopRail"
    };

    private void Awake()
    {
        if (gameRules == null) gameRules = Object.FindFirstObjectByType<GameRules>();
        if (cardRoot == null) cardRoot = GameObject.Find("ArenaWhitebox")?.transform;
        if (interaction == null) interaction = Object.FindFirstObjectByType<ArenaWhiteboxInteraction>();

        ConfigureBattlefieldAreas();
        CacheCards();
        HideLegacySceneHands();
        HideSceneDecorations();
        RefreshAll();
    }

    private void Update()
    {
        if (interaction != null && interaction.IsDraggingCard) return;
        ConfigureBattlefieldAreas();
        if (Time.unscaledTime >= nextLegacyHandCleanupTime)
        {
            HideLegacySceneHands();
            HideSceneDecorations();
            nextLegacyHandCleanupTime = Time.unscaledTime + 1f;
        }
        RefreshAll();
    }

    private void ConfigureBattlefieldAreas()
    {
        ConfigureArea("PlayerBattlefieldArea", new Vector3(0f, 0.08f, -1.62f), new Vector3(14.00f, 0.08f, 3.50f));
        ConfigureArea("OpponentBattlefieldArea", new Vector3(0f, 0.08f, 1.62f), new Vector3(14.00f, 0.08f, 3.50f));
    }

    private void ConfigureArea(string name, Vector3 localPosition, Vector3 localScale)
    {
        if (cardRoot == null) return;

        Transform area = cardRoot.Find(name);
        if (area == null) return;

        area.localPosition = localPosition;
        area.localScale = localScale;
    }

    private void CacheCards()
    {
        playerHandCards.Clear();
        opponentHandCards.Clear();
        playerBattlefieldCards.Clear();
        opponentBattlefieldCards.Clear();

        if (cardRoot == null) return;

        foreach (Transform child in cardRoot)
        {
            string name = child.name;
            if (name.StartsWith("PlayerHandCard_") || name.StartsWith("OpponentHandCard_"))
            {
                child.gameObject.SetActive(false);
            }
            else if (name.StartsWith("PlayerBattlefieldCard_") || name == "PlayerCreatureCard" || name == "PlayerSupportCard") playerBattlefieldCards.Add(child);
            else if (name.StartsWith("OpponentBattlefieldCard_") || name == "OpponentCreatureCard" || name == "OpponentSupportCard") opponentBattlefieldCards.Add(child);
        }

        playerHandCards.Sort(CompareTrailingIndex);
        opponentHandCards.Sort(CompareTrailingIndex);
        playerBattlefieldCards.Sort(CompareBattlefieldName);
        opponentBattlefieldCards.Sort(CompareBattlefieldName);
    }

    private void RefreshAll()
    {
        if (gameRules == null) return;

        HideLegacyHands(playerHandCards);
        HideLegacyHands(opponentHandCards);
        RefreshBattlefield(playerBattlefieldCards, gameRules.GetBattlefieldCards(1), 1);
        RefreshBattlefield(opponentBattlefieldCards, gameRules.GetBattlefieldCards(2), 2);
    }

    private static void HideLegacyHands(List<Transform> visuals)
    {
        for (int i = 0; i < visuals.Count; i++)
        {
            if (visuals[i] != null) visuals[i].gameObject.SetActive(false);
        }
    }

    private static void HideLegacySceneHands()
    {
        Transform[] transforms = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None);
        for (int i = 0; i < transforms.Length; i++)
        {
            Transform current = transforms[i];
            string name = current.name;
            if (!name.StartsWith("PlayerHandCard_") && !name.StartsWith("OpponentHandCard_")) continue;
            if (current.GetComponentInParent<Canvas>() != null) continue;

            current.gameObject.SetActive(false);
        }
    }

    private static void HideSceneDecorations()
    {
        Transform[] transforms = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None);
        for (int i = 0; i < transforms.Length; i++)
        {
            Transform current = transforms[i];
            if (!IsSceneDecoration(current.name)) continue;
            current.gameObject.SetActive(false);
        }
    }

    private static bool IsSceneDecoration(string objectName)
    {
        if (objectName.Contains("DeckPile") || objectName.Contains("GravePile")) return true;
        if (objectName.StartsWith("ArenaTile_") || objectName.StartsWith("MTGA_Tile_")) return true;

        for (int i = 0; i < SceneDecorationNames.Length; i++)
        {
            if (objectName == SceneDecorationNames[i]) return true;
        }

        return false;
    }

    private void RefreshBattlefield(List<Transform> visuals, IReadOnlyList<RuntimeCard> cards, int owner)
    {
        EnsureBattlefieldCapacity(visuals, cards.Count, owner);

        int landCount = 0;
        int frontCount = 0;
        for (int i = 0; i < cards.Count; i++)
        {
            if (cards[i].Data.HasType(CardType.Land)) landCount++;
            else frontCount++;
        }

        int landIndex = 0;
        int frontIndex = 0;
        for (int i = 0; i < visuals.Count; i++)
        {
            Transform visual = visuals[i];
            bool active = i < cards.Count;
            visual.gameObject.SetActive(active);
            if (!active) continue;

            RuntimeCard card = cards[i];
            visual.name = owner == 1 ? $"PlayerBattlefieldCard_{i + 1}" : $"OpponentBattlefieldCard_{i + 1}";
            ArenaBattlefieldCardVisual binding = visual.GetComponent<ArenaBattlefieldCardVisual>();
            if (binding == null) binding = visual.gameObject.AddComponent<ArenaBattlefieldCardVisual>();
            binding.Configure(owner, i);

            SetCardColor(visual, GetTypeColor(card.Data.cardTypes));
            bool hasImportedFace = ApplyCardSurface(visual, card.Data);
            SetChildText(visual, "TitleText", hasImportedFace ? "" : card.Data.cardName);
            SetChildText(visual, "PTText", !hasImportedFace && card.Data.HasType(CardType.Creature) ? $"{card.CurrentAttack}/{card.CurrentDefence}" : "");
            SetChildTextSize(visual, "TitleText", 0.052f);
            SetChildTextSize(visual, "PTText", 0.060f);
            SetChildActive(visual, "HandTitle", !hasImportedFace);
            SetChildActive(visual, "HandArt", !hasImportedFace);
            ConfigureGlowVisuals(visual);
            SetCardGlow(visual, "InteractionSelectedGlow", IsBattlefieldActionable(owner, i, card));

            bool isLand = card.Data.HasType(CardType.Land);
            int rowIndex = isLand ? landIndex++ : frontIndex++;
            int rowCount = isLand ? landCount : frontCount;
            LayoutBattlefieldCard(visual, card, owner, rowIndex, rowCount);
        }
    }

    private void EnsureBattlefieldCapacity(List<Transform> visuals, int requiredCount, int owner)
    {
        if (requiredCount <= visuals.Count) return;
        if (visuals.Count == 0) return;

        Transform template = visuals[0];
        while (visuals.Count < requiredCount)
        {
            GameObject clone = Instantiate(template.gameObject, template.parent);
            clone.name = owner == 1 ? $"PlayerBattlefieldCard_{visuals.Count + 1}" : $"OpponentBattlefieldCard_{visuals.Count + 1}";
            clone.SetActive(false);
            visuals.Add(clone.transform);
        }
    }

    private bool IsBattlefieldActionable(int owner, int battlefieldIndex, RuntimeCard card)
    {
        if (gameRules == null || card == null) return false;
        if (gameRules.CanDeclareAttacker(owner, battlefieldIndex)) return true;
        return owner == gameRules.GetCurrentPlayer() && gameRules.CanActivateAnyAbility(card);
    }

    private static void LayoutBattlefieldCard(Transform visual, RuntimeCard card, int owner, int rowIndex, int rowCount)
    {
        bool player = owner == 1;
        bool land = card.Data.HasType(CardType.Land);
        float baseSpacing = land ? 0.78f : 0.92f;
        float rowWidth = 12.10f;
        float spacing = rowCount <= 1 ? 0f : Mathf.Min(baseSpacing, rowWidth / (rowCount - 1));
        float scaleFactor = rowCount <= 1 ? 1f : Mathf.Clamp(spacing / baseSpacing, 0.42f, 1f);
        float centeredIndex = rowIndex - (rowCount - 1) * 0.5f;
        float x = centeredIndex * spacing;

        float z;
        if (player) z = land ? -2.48f : -1.18f;
        else z = land ? 2.48f : 1.18f;

        float y = land ? 0.205f : 0.255f;
        y += rowIndex * 0.003f;
        visual.localPosition = new Vector3(x, y, z);

        Vector3 baseScale = card.Data.HasType(CardType.Creature)
            ? new Vector3(0.58f, 0.040f, 0.80f)
            : new Vector3(0.53f, 0.036f, 0.73f);
        visual.localScale = new Vector3(baseScale.x * scaleFactor, baseScale.y, baseScale.z * scaleFactor);

        float yaw = card.IsTapped ? (player ? 90f : -90f) : Mathf.Clamp(-centeredIndex * 0.85f, -5.5f, 5.5f);
        visual.localEulerAngles = new Vector3(0f, yaw, 0f);
    }

    private Color GetTypeColor(CardType cardTypes)
    {
        if (cardTypes.Has(CardType.Land)) return new Color(0.22f, 0.45f, 0.58f, 1f);
        if (cardTypes.Has(CardType.Creature)) return new Color(0.12f, 0.36f, 0.58f, 1f);
        if (cardTypes.IsInstantOrSorcery()) return new Color(0.55f, 0.18f, 0.12f, 1f);
        if (cardTypes.Has(CardType.Artifact)) return new Color(0.52f, 0.52f, 0.56f, 1f);
        if (cardTypes.Has(CardType.Enchantment) || cardTypes.Has(CardType.Planeswalker)) return new Color(0.62f, 0.48f, 0.72f, 1f);
        return Color.white;
    }

    private void SetCardColor(Transform card, Color color)
    {
        Renderer renderer = card.GetComponent<Renderer>();
        if (renderer == null) return;

        if (!baseColors.ContainsKey(renderer)) baseColors.Add(renderer, renderer.sharedMaterial.color);
        renderer.material.color = color;
    }

    private static bool ApplyCardSurface(Transform card, CardData data)
    {
        bool hasImportedFace = CardImageLibrary.TryLoadFullCard(data, out Sprite cardSprite);
        if (!hasImportedFace) cardSprite = CardVisualStyle.LoadFrame(data);

        Transform surface = card.Find("PsdFrameSurface");
        if (cardSprite == null)
        {
            if (surface != null) surface.gameObject.SetActive(false);
            return false;
        }

        if (surface == null)
        {
            GameObject surfaceObject = GameObject.CreatePrimitive(PrimitiveType.Quad);
            surfaceObject.name = "PsdFrameSurface";
            surfaceObject.transform.SetParent(card, false);
            surface = surfaceObject.transform;
            Collider collider = surfaceObject.GetComponent<Collider>();
            if (collider != null) Destroy(collider);
        }
        surface.gameObject.SetActive(true);

        surface.localPosition = new Vector3(0f, 0.559f, 0f);
        surface.localRotation = Quaternion.Euler(90f, 0f, 0f);
        surface.localScale = new Vector3(0.98f, 0.98f, 1f);

        MeshRenderer renderer = surface.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = GetFrameMaterial(cardSprite.texture);
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        return hasImportedFace;
    }

    private static Material GetFrameMaterial(Texture texture)
    {
        if (FrameMaterials.TryGetValue(texture, out Material material)) return material;

        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Unlit/Transparent");
        if (shader == null) shader = Shader.Find("Standard");

        material = new Material(shader) { name = $"Card Frame {texture.name}" };
        material.mainTexture = texture;
        material.color = Color.white;
        if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", texture);
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", Color.white);
        if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 1f);
        material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        material.SetFloat("_ZWrite", 0f);
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.renderQueue = (int)RenderQueue.Transparent;
        FrameMaterials.Add(texture, material);
        return material;
    }

    private void SetChildText(Transform parent, string childName, string value)
    {
        Transform child = parent.Find(childName);
        if (child == null) return;

        TextMesh textMesh = child.GetComponent<TextMesh>();
        if (textMesh != null) textMesh.text = value;
    }

    private void SetChildTextSize(Transform parent, string childName, float characterSize)
    {
        Transform child = parent.Find(childName);
        if (child == null) return;

        TextMesh textMesh = child.GetComponent<TextMesh>();
        if (textMesh != null) textMesh.characterSize = characterSize;
    }

    private void SetChildActive(Transform parent, string childName, bool active)
    {
        Transform child = parent.Find(childName);
        if (child != null) child.gameObject.SetActive(active);
    }

    private static void SetCardGlow(Transform parent, string glowName, bool active)
    {
        Transform glow = parent.Find(glowName);
        if (glow == null) return;

        ConfigureGlowRenderer(glow);
        glow.gameObject.SetActive(active);
    }

    private static void ConfigureGlowVisuals(Transform parent)
    {
        ConfigureGlow(parent.Find("InteractionHoverGlow"), 1.06f, 0.036f);
        ConfigureGlow(parent.Find("InteractionSelectedGlow"), 1.12f, 0.038f);
    }

    private static void ConfigureGlow(Transform glow, float size, float height)
    {
        if (glow == null) return;

        glow.localPosition = Vector3.zero;
        glow.localScale = new Vector3(size, height, size);
        ConfigureGlowRenderer(glow);
    }

    private static void ConfigureGlowRenderer(Transform glow)
    {
        Renderer renderer = glow.GetComponent<Renderer>();
        if (renderer == null) return;

        renderer.sharedMaterial = GetHighlightMaterial();
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
    }

    private static Material GetHighlightMaterial()
    {
        if (highlightMaterial != null) return highlightMaterial;

        Shader shader = Shader.Find("MTGB/Arena Card Highlight");
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Standard");

        highlightMaterial = new Material(shader) { name = "Arena Card Highlight Runtime" };
        highlightMaterial.SetColor("_Color", new Color(0.04f, 0.65f, 1.00f, 0.26f));
        highlightMaterial.SetColor("_EdgeColor", new Color(0.12f, 0.94f, 1.00f, 0.82f));
        if (highlightMaterial.HasProperty("_Width")) highlightMaterial.SetFloat("_Width", 0.13f);
        if (highlightMaterial.HasProperty("_Intensity")) highlightMaterial.SetFloat("_Intensity", 1.35f);
        if (highlightMaterial.HasProperty("_PulseSpeed")) highlightMaterial.SetFloat("_PulseSpeed", 2.15f);
        return highlightMaterial;
    }

    private static int CompareTrailingIndex(Transform a, Transform b)
    {
        return ParseTrailingIndex(a.name).CompareTo(ParseTrailingIndex(b.name));
    }

    private static int CompareBattlefieldName(Transform a, Transform b)
    {
        int orderA = GetBattlefieldOrder(a.name);
        int orderB = GetBattlefieldOrder(b.name);
        return orderA.CompareTo(orderB);
    }

    private static int GetBattlefieldOrder(string name)
    {
        if (name.Contains("BattlefieldCard_")) return ParseTrailingIndex(name);
        return name.Contains("Creature") ? 0 : 1;
    }

    private static int ParseTrailingIndex(string value)
    {
        int underscore = value.LastIndexOf('_');
        if (underscore < 0 || underscore >= value.Length - 1) return 0;
        if (int.TryParse(value.Substring(underscore + 1), out int result)) return result;
        return 0;
    }
}

public sealed class ArenaBattlefieldCardVisual : MonoBehaviour
{
    [SerializeField] private int owner;
    [SerializeField] private int battlefieldIndex = -1;

    public int Owner => owner;
    public int BattlefieldIndex => battlefieldIndex;

    public void Configure(int newOwner, int newBattlefieldIndex)
    {
        owner = newOwner;
        battlefieldIndex = newBattlefieldIndex;
    }
}
