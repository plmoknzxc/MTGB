using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Rendering;

public class ArenaWhiteboxCardSync : MonoBehaviour
{
    [SerializeField] private GameRules gameRules;
    [SerializeField] private Transform cardRoot;

    private ArenaWhiteboxInteraction interaction;
    private ArenaMatchLayoutSettings.BattlefieldLayout layout = new ArenaMatchLayoutSettings.BattlefieldLayout();

    private readonly List<Transform> playerHandCards = new List<Transform>(12);
    private readonly List<Transform> opponentHandCards = new List<Transform>(12);
    private readonly List<Transform> playerBattlefieldCards = new List<Transform>(12);
    private readonly List<Transform> opponentBattlefieldCards = new List<Transform>(12);
    private readonly Dictionary<RuntimeCard, Transform> battlefieldVisualsByCard = new Dictionary<RuntimeCard, Transform>(32);
    private readonly List<LineRenderer> blockLines = new List<LineRenderer>(8);
    private readonly Dictionary<Renderer, Color> baseColors = new Dictionary<Renderer, Color>(64);
    private static readonly Dictionary<Texture, Material> FrameMaterials = new Dictionary<Texture, Material>(12);
    private static Material highlightMaterial;
    private static Material blockLineMaterial;
    private float nextLegacyHandCleanupTime;
    private bool combatAnimationActive;
    private Sequence combatSequence;

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
        if (gameRules == null) gameRules = UnityEngine.Object.FindFirstObjectByType<GameRules>();
        if (cardRoot == null) cardRoot = GameObject.Find("ArenaWhitebox")?.transform;
        if (interaction == null) interaction = UnityEngine.Object.FindFirstObjectByType<ArenaWhiteboxInteraction>();

        ConfigureBattlefieldAreas();
        CacheCards();
        HideLegacySceneHands();
        HideSceneDecorations();
        GameEvents.OnCombatAnimationRequested -= HandleCombatAnimationRequested;
        GameEvents.OnCombatAnimationRequested += HandleCombatAnimationRequested;
        GameEvents.OnDamageDealt -= HandleDamageDealt;
        GameEvents.OnDamageDealt += HandleDamageDealt;
        GameEvents.OnCreatureDied -= HandleCreatureDied;
        GameEvents.OnCreatureDied += HandleCreatureDied;
        GameEvents.OnPlayerDamaged -= HandlePlayerDamaged;
        GameEvents.OnPlayerDamaged += HandlePlayerDamaged;
        GameEvents.OnDamagePrevented -= HandleDamagePrevented;
        GameEvents.OnDamagePrevented += HandleDamagePrevented;
        GameEvents.OnCreatureRegenerated -= HandleCreatureRegenerated;
        GameEvents.OnCreatureRegenerated += HandleCreatureRegenerated;
        RefreshAll();
    }

    private void OnDestroy()
    {
        GameEvents.OnCombatAnimationRequested -= HandleCombatAnimationRequested;
        GameEvents.OnDamageDealt -= HandleDamageDealt;
        GameEvents.OnCreatureDied -= HandleCreatureDied;
        GameEvents.OnPlayerDamaged -= HandlePlayerDamaged;
        GameEvents.OnDamagePrevented -= HandleDamagePrevented;
        GameEvents.OnCreatureRegenerated -= HandleCreatureRegenerated;
        if (combatSequence != null)
        {
            combatSequence.Kill(false);
            combatSequence = null;
        }
    }

    private void Update()
    {
        if (combatAnimationActive)
        {
            RefreshBlockLines();
            return;
        }

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

    private bool HandleCombatAnimationRequested(Action onComplete)
    {
        if (combatAnimationActive) return true;
        if (gameRules == null || cardRoot == null) return false;

        RefreshAll();

        combatAnimationActive = true;
        combatSequence = DOTween.Sequence();
        int animatedPairs = 0;
        animatedPairs += InsertCombatAnimations(gameRules.GetBattlefieldCards(1), combatSequence, animatedPairs);
        animatedPairs += InsertCombatAnimations(gameRules.GetBattlefieldCards(2), combatSequence, animatedPairs);

        if (animatedPairs == 0)
        {
            combatSequence.AppendInterval(0.12f);
        }
        else
        {
            combatSequence.AppendInterval(0.10f);
        }

        combatSequence.OnComplete(() =>
        {
            combatAnimationActive = false;
            combatSequence = null;
            onComplete?.Invoke();
            RefreshAll();
        });
        return true;
    }

    private void HandleDamageDealt(RuntimeCard target, int damage)
    {
        if (target == null || damage <= 0) return;
        if (!battlefieldVisualsByCard.TryGetValue(target, out Transform visual)) return;

        visual.DOPunchScale(new Vector3(0.08f, 0f, 0.08f), 0.25f, 4, 0.45f);
        ShowFloatingText($"-{damage}", visual.position + Vector3.up * 0.42f, new Color(1f, 0.30f, 0.14f, 1f));
    }

    private void HandleCreatureDied(RuntimeCard card)
    {
        if (card == null) return;
        if (!battlefieldVisualsByCard.TryGetValue(card, out Transform visual)) return;

        ShowFloatingText("\u6b7b\u4ea1", visual.position + Vector3.up * 0.58f, new Color(0.95f, 0.10f, 0.08f, 1f));
    }

    private void HandlePlayerDamaged(int player, int damage, int remainingLife)
    {
        if (damage <= 0 || cardRoot == null) return;

        Vector3 localPosition = new Vector3(0f, 1.12f, player == OnlinePlayerContext.LocalPlayerId ? -3.05f : 3.05f);
        ShowFloatingText($"-{damage}", cardRoot.TransformPoint(localPosition), new Color(1f, 0.34f, 0.12f, 1f));
    }

    private void HandleDamagePrevented(RuntimeCard source, RuntimeCard target, int damage)
    {
        if (target == null || damage <= 0) return;
        if (!battlefieldVisualsByCard.TryGetValue(target, out Transform visual)) return;

        visual.DOPunchScale(new Vector3(0.045f, 0f, 0.045f), 0.26f, 3, 0.40f);
        ShowFloatingText($"\u9632\u6b62 {damage}", visual.position + Vector3.up * 0.48f, new Color(0.30f, 0.86f, 1f, 1f));
    }

    private void HandleCreatureRegenerated(RuntimeCard card)
    {
        if (card == null) return;
        if (!battlefieldVisualsByCard.TryGetValue(card, out Transform visual)) return;

        ShowFloatingText("\u91cd\u751f", visual.position + Vector3.up * 0.58f, new Color(0.28f, 1f, 0.58f, 1f));
    }

    private void ShowFloatingText(string value, Vector3 worldPosition, Color color)
    {
        GameObject labelObject = new GameObject("CombatFeedbackText");
        if (cardRoot != null) labelObject.transform.SetParent(cardRoot, true);
        labelObject.transform.position = worldPosition;

        Camera camera = Camera.main;
        if (camera != null) labelObject.transform.rotation = camera.transform.rotation;

        TextMesh textMesh = labelObject.AddComponent<TextMesh>();
        textMesh.text = value;
        textMesh.fontSize = 46;
        textMesh.characterSize = 0.11f;
        textMesh.anchor = TextAnchor.MiddleCenter;
        textMesh.alignment = TextAlignment.Center;
        textMesh.color = color;

        labelObject.transform.DOMove(worldPosition + Vector3.up * 0.42f, 0.70f).SetEase(Ease.OutCubic);
        DOTween.To(() => textMesh.color, nextColor => textMesh.color = nextColor, new Color(color.r, color.g, color.b, 0f), 0.70f)
            .SetEase(Ease.InQuad)
            .OnComplete(() =>
            {
                if (labelObject != null) Destroy(labelObject);
            });
    }

    private int InsertCombatAnimations(IReadOnlyList<RuntimeCard> attackers, Sequence sequence, int startIndex)
    {
        int animated = 0;
        for (int i = 0; i < attackers.Count; i++)
        {
            RuntimeCard attacker = attackers[i];
            if (attacker == null || !attacker.IsAttacking) continue;
            if (!battlefieldVisualsByCard.TryGetValue(attacker, out Transform attackerVisual)) continue;

            if (attacker.Blockers.Count == 0)
            {
                InsertUnblockedAttackAnimation(sequence, attacker, attackerVisual, startIndex + animated);
                animated++;
                continue;
            }

            for (int blockerIndex = 0; blockerIndex < attacker.Blockers.Count; blockerIndex++)
            {
                RuntimeCard blocker = attacker.Blockers[blockerIndex];
                if (blocker == null || blocker.BlockingTarget != attacker) continue;
                if (!battlefieldVisualsByCard.TryGetValue(blocker, out Transform blockerVisual)) continue;

                InsertClashAnimation(sequence, attackerVisual, blockerVisual, startIndex + animated);
                animated++;
            }
        }

        return animated;
    }

    private static void InsertClashAnimation(Sequence sequence, Transform attackerVisual, Transform blockerVisual, int index)
    {
        KillCombatTweens(attackerVisual);
        KillCombatTweens(blockerVisual);

        Vector3 attackerOrigin = attackerVisual.localPosition;
        Vector3 blockerOrigin = blockerVisual.localPosition;
        Vector3 direction = blockerOrigin - attackerOrigin;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f) direction = Vector3.forward;
        direction.Normalize();

        float at = index * 0.045f;
        sequence.Insert(at, attackerVisual.DOLocalMove(attackerOrigin + direction * 0.24f, 0.16f).SetLoops(2, LoopType.Yoyo).SetEase(Ease.OutQuad));
        sequence.Insert(at, blockerVisual.DOLocalMove(blockerOrigin - direction * 0.17f, 0.16f).SetLoops(2, LoopType.Yoyo).SetEase(Ease.OutQuad));
        sequence.Insert(at + 0.04f, attackerVisual.DOPunchRotation(new Vector3(0f, 2.5f, 0f), 0.24f, 4, 0.45f));
        sequence.Insert(at + 0.04f, blockerVisual.DOPunchRotation(new Vector3(0f, -2.5f, 0f), 0.24f, 4, 0.45f));
        sequence.Insert(at + 0.05f, attackerVisual.DOPunchScale(new Vector3(0.075f, 0f, 0.075f), 0.28f, 5, 0.55f));
        sequence.Insert(at + 0.05f, blockerVisual.DOPunchScale(new Vector3(0.075f, 0f, 0.075f), 0.28f, 5, 0.55f));
        InsertFlash(sequence, attackerVisual, at + 0.12f);
        InsertFlash(sequence, blockerVisual, at + 0.12f);
    }

    private static void InsertUnblockedAttackAnimation(Sequence sequence, RuntimeCard attacker, Transform attackerVisual, int index)
    {
        KillCombatTweens(attackerVisual);

        Vector3 origin = attackerVisual.localPosition;
        Vector3 direction = attacker.OwnerPlayerID == OnlinePlayerContext.LocalPlayerId ? Vector3.forward : Vector3.back;
        float at = index * 0.045f;
        sequence.Insert(at, attackerVisual.DOLocalMove(origin + direction * 0.58f, 0.19f).SetLoops(2, LoopType.Yoyo).SetEase(Ease.OutCubic));
        sequence.Insert(at + 0.04f, attackerVisual.DOPunchScale(new Vector3(0.085f, 0f, 0.085f), 0.30f, 5, 0.55f));
        InsertFlash(sequence, attackerVisual, at + 0.12f);
    }

    private static void InsertFlash(Sequence sequence, Transform visual, float at)
    {
        Renderer renderer = visual.GetComponent<Renderer>();
        if (renderer == null) return;

        Material material = renderer.material;
        Color original = material.color;
        Color flash = new Color(1f, 0.18f, 0.12f, original.a);
        Sequence flashSequence = DOTween.Sequence();
        flashSequence.Append(DOTween.To(() => material.color, value => material.color = value, flash, 0.06f));
        flashSequence.Append(DOTween.To(() => material.color, value => material.color = value, original, 0.14f));
        sequence.Insert(at, flashSequence);
    }

    private static void KillCombatTweens(Transform visual)
    {
        if (visual == null) return;
        visual.DOKill(false);
    }

    public void ApplyLayoutSettings(ArenaMatchLayoutSettings.BattlefieldLayout settings)
    {
        if (settings == null) return;

        layout.CopyFrom(settings);
        layout.Sanitize();
        ConfigureBattlefieldAreas();
        RefreshAll();
    }

    private void ConfigureBattlefieldAreas()
    {
        ConfigureArea("PlayerBattlefieldArea", layout.playerDropAreaPosition, layout.playerDropAreaScale);
        ConfigureArea("OpponentBattlefieldArea", layout.opponentDropAreaPosition, layout.opponentDropAreaScale);
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
        battlefieldVisualsByCard.Clear();
        int localPlayer = OnlinePlayerContext.LocalPlayerId;
        int opponentPlayer = OnlinePlayerContext.OpponentPlayerId;
        RefreshBattlefield(playerBattlefieldCards, gameRules.GetBattlefieldCards(localPlayer), localPlayer);
        RefreshBattlefield(opponentBattlefieldCards, gameRules.GetBattlefieldCards(opponentPlayer), opponentPlayer);
        RefreshBlockLines();
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
        Transform[] transforms = UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsSortMode.None);
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
        Transform[] transforms = UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsSortMode.None);
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
            visual.name = owner == OnlinePlayerContext.LocalPlayerId ? $"PlayerBattlefieldCard_{i + 1}" : $"OpponentBattlefieldCard_{i + 1}";
            ArenaBattlefieldCardVisual binding = visual.GetComponent<ArenaBattlefieldCardVisual>();
            if (binding == null) binding = visual.gameObject.AddComponent<ArenaBattlefieldCardVisual>();
            binding.Configure(owner, i);

            SetCardColor(visual, GetTypeColor(card.Data.cardTypes));
            bool hasImportedFace = ApplyCardSurface(visual, card.Data);
            SetChildText(visual, "TitleText", hasImportedFace ? "" : card.Data.cardName);
            SetChildText(visual, "PTText", !hasImportedFace && card.Data.HasType(CardType.Creature) ? $"{card.CurrentAttack}/{card.CurrentDefence}" : "");
            SetChildTextSize(visual, "TitleText", 0.052f);
            SetChildTextSize(visual, "PTText", 0.060f);
            SetBlockDamageOrderText(visual, card);
            SetChildActive(visual, "HandTitle", !hasImportedFace);
            SetChildActive(visual, "HandArt", !hasImportedFace);
            ConfigureGlowVisuals(visual);
            bool pendingAttacker = gameRules != null && gameRules.IsPendingAttacker(card);
            SetCardGlow(visual, "InteractionSelectedGlow", IsBattlefieldActionable(owner, i, card));

            bool isLand = card.Data.HasType(CardType.Land);
            int rowIndex = isLand ? landIndex++ : frontIndex++;
            int rowCount = isLand ? landCount : frontCount;
            LayoutBattlefieldCard(visual, card, owner, rowIndex, rowCount, pendingAttacker);
            battlefieldVisualsByCard[card] = visual;
        }
    }

    private void RefreshBlockLines()
    {
        int lineIndex = 0;
        AddBlockLinesForAttackers(gameRules.GetBattlefieldCards(1), ref lineIndex);
        AddBlockLinesForAttackers(gameRules.GetBattlefieldCards(2), ref lineIndex);

        for (int i = lineIndex; i < blockLines.Count; i++)
        {
            if (blockLines[i] != null) blockLines[i].gameObject.SetActive(false);
        }
    }

    private void AddBlockLinesForAttackers(IReadOnlyList<RuntimeCard> attackers, ref int lineIndex)
    {
        for (int i = 0; i < attackers.Count; i++)
        {
            RuntimeCard attacker = attackers[i];
            if (attacker == null || !attacker.IsAttacking || attacker.Blockers.Count == 0) continue;
            if (!battlefieldVisualsByCard.TryGetValue(attacker, out Transform attackerVisual)) continue;

            for (int blockerIndex = 0; blockerIndex < attacker.Blockers.Count; blockerIndex++)
            {
                RuntimeCard blocker = attacker.Blockers[blockerIndex];
                if (blocker == null || blocker.BlockingTarget != attacker) continue;
                if (!battlefieldVisualsByCard.TryGetValue(blocker, out Transform blockerVisual)) continue;

                LineRenderer line = EnsureBlockLine(lineIndex++);
                Vector3 start = blockerVisual.position;
                Vector3 end = attackerVisual.position;
                float lineY = Mathf.Max(start.y, end.y) + 0.22f;
                start.y = lineY;
                end.y = lineY;
                line.SetPosition(0, start);
                line.SetPosition(1, end);
                line.gameObject.SetActive(true);
            }
        }
    }

    private LineRenderer EnsureBlockLine(int index)
    {
        while (blockLines.Count <= index)
        {
            GameObject lineObject = new GameObject($"BlockLink_{blockLines.Count + 1}");
            if (cardRoot != null) lineObject.transform.SetParent(cardRoot, false);

            LineRenderer line = lineObject.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.positionCount = 2;
            line.widthMultiplier = 0.035f;
            line.numCapVertices = 4;
            line.numCornerVertices = 3;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.material = GetBlockLineMaterial();
            line.startColor = new Color(0.08f, 0.95f, 1f, 0.92f);
            line.endColor = new Color(1f, 0.62f, 0.12f, 0.88f);
            line.gameObject.SetActive(false);
            blockLines.Add(line);
        }

        return blockLines[index];
    }

    private void EnsureBattlefieldCapacity(List<Transform> visuals, int requiredCount, int owner)
    {
        if (requiredCount <= visuals.Count) return;
        if (visuals.Count == 0) return;

        Transform template = visuals[0];
        while (visuals.Count < requiredCount)
        {
            GameObject clone = Instantiate(template.gameObject, template.parent);
            clone.name = owner == OnlinePlayerContext.LocalPlayerId ? $"PlayerBattlefieldCard_{visuals.Count + 1}" : $"OpponentBattlefieldCard_{visuals.Count + 1}";
            clone.SetActive(false);
            visuals.Add(clone.transform);
        }
    }

    private bool IsBattlefieldActionable(int owner, int battlefieldIndex, RuntimeCard card)
    {
        if (gameRules == null || card == null) return false;
        if (gameRules.IsChoosingAttackers()) return gameRules.IsPendingAttacker(card);
        if (gameRules.IsChoosingBlockers())
        {
            if (gameRules.CanCancelBlocker(owner, battlefieldIndex)) return true;
            if (owner == OnlinePlayerContext.LocalPlayerId) return gameRules.CanDeclareBlockerAgainstAnyAttacker(owner, battlefieldIndex);
            return gameRules.CanAnyHumanBlockerBlock(card);
        }
        if (gameRules.CanDeclareAttacker(owner, battlefieldIndex)) return true;
        if (card.HasType(CardType.Land)) return gameRules.CanActivateManaAbility(card) || gameRules.CanActivateAnyAbility(card);
        return gameRules.CanActivateAnyAbility(card);
    }

    private void LayoutBattlefieldCard(Transform visual, RuntimeCard card, int owner, int rowIndex, int rowCount, bool pendingAttacker)
    {
        bool player = owner == OnlinePlayerContext.LocalPlayerId;
        bool land = card.Data.HasType(CardType.Land);
        float baseSpacing = land ? layout.landSpacing : layout.frontSpacing;
        float spacing = GetBattlefieldSpacing(rowCount, baseSpacing);
        float scaleFactor = rowCount <= 1 ? 1f : Mathf.Clamp(spacing / baseSpacing, layout.minimumScale, 1f);
        float centeredIndex = GetCenteredRowIndex(rowIndex, rowCount);
        float x = GetBattlefieldX(rowIndex, rowCount, baseSpacing);

        float z;
        if (player) z = land ? layout.playerLandRowZ : layout.playerFrontRowZ;
        else z = land ? layout.opponentLandRowZ : layout.opponentFrontRowZ;

        float y = land ? layout.landRowY : layout.frontRowY;
        y += rowIndex * layout.rowYStep;
        if (!land && TryGetBlockingLayout(card, player, out float blockingX, out float blockingY, out float blockingZ))
        {
            x = blockingX;
            y = blockingY;
            z = blockingZ;
        }
        else if (!land && (pendingAttacker || card.IsAttacking))
        {
            z += player ? layout.attackOffsetZ : -layout.attackOffsetZ;
        }

        visual.localPosition = new Vector3(x, y, z);

        Vector3 baseScale = card.Data.HasType(CardType.Creature)
            ? layout.creatureScale
            : layout.permanentScale;
        visual.localScale = new Vector3(baseScale.x * scaleFactor, baseScale.y, baseScale.z * scaleFactor);

        float yaw = card.IsTapped
            ? (player ? 90f : -90f)
            : Mathf.Clamp(centeredIndex * layout.idleYawPerCard, -layout.idleYawLimit, layout.idleYawLimit);
        visual.localEulerAngles = new Vector3(0f, yaw, 0f);
    }

    private bool TryGetBlockingLayout(RuntimeCard blocker, bool player, out float x, out float y, out float z)
    {
        x = 0f;
        y = layout.blockerBaseY;
        z = player ? layout.blockerPlayerZ : layout.blockerOpponentZ;

        if (gameRules == null || blocker == null || !blocker.IsBlocking || blocker.BlockingTarget == null) return false;

        RuntimeCard attacker = blocker.BlockingTarget;
        IReadOnlyList<RuntimeCard> attackerBattlefield = gameRules.GetBattlefieldCards(attacker.OwnerPlayerID);
        int attackerIndex = GetFrontRowIndex(attackerBattlefield, attacker);
        int attackerCount = CountFrontRowCards(attackerBattlefield);
        if (attackerIndex < 0 || attackerCount <= 0) return false;

        x = GetBattlefieldX(attackerIndex, attackerCount, layout.frontSpacing);
        int blockerIndex = GetBlockerIndex(attacker, blocker);
        int blockerCount = Mathf.Max(1, attacker.Blockers.Count);
        float centeredBlockerIndex = blockerIndex - (blockerCount - 1) * 0.5f;
        x += centeredBlockerIndex * layout.blockerSpacingX;
        y += blockerIndex * layout.blockerLayerY;
        z = player ? layout.blockerPlayerZ : layout.blockerOpponentZ;
        return true;
    }

    private float GetBattlefieldX(int rowIndex, int rowCount, float baseSpacing)
    {
        return GetCenteredRowIndex(rowIndex, rowCount) * GetBattlefieldSpacing(rowCount, baseSpacing);
    }

    private float GetBattlefieldSpacing(int rowCount, float baseSpacing)
    {
        return rowCount <= 1 ? 0f : Mathf.Min(baseSpacing, layout.rowWidth / (rowCount - 1));
    }

    private static float GetCenteredRowIndex(int rowIndex, int rowCount)
    {
        return rowIndex - (rowCount - 1) * 0.5f;
    }

    private static int CountFrontRowCards(IReadOnlyList<RuntimeCard> cards)
    {
        int count = 0;
        for (int i = 0; i < cards.Count; i++)
        {
            RuntimeCard card = cards[i];
            if (card != null && !card.Data.HasType(CardType.Land)) count++;
        }
        return count;
    }

    private static int GetFrontRowIndex(IReadOnlyList<RuntimeCard> cards, RuntimeCard target)
    {
        int rowIndex = 0;
        for (int i = 0; i < cards.Count; i++)
        {
            RuntimeCard card = cards[i];
            if (card == null || card.Data.HasType(CardType.Land)) continue;
            if (ReferenceEquals(card, target)) return rowIndex;
            rowIndex++;
        }
        return -1;
    }

    private static int GetBlockerIndex(RuntimeCard attacker, RuntimeCard blocker)
    {
        if (attacker == null || attacker.Blockers == null) return 0;
        for (int i = 0; i < attacker.Blockers.Count; i++)
        {
            if (ReferenceEquals(attacker.Blockers[i], blocker)) return i;
        }
        return 0;
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

    private static bool ApplyCardSurface(Transform card, CardDefinition data)
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

    private void SetBlockDamageOrderText(Transform visual, RuntimeCard card)
    {
        TextMesh textMesh = EnsureDamageOrderText(visual);
        if (textMesh == null) return;

        if (gameRules == null || card == null)
        {
            textMesh.text = string.Empty;
            return;
        }

        string value = string.Empty;
        int incomingCombatDamage = gameRules.GetCombatDamagePreviewToCard(card);
        if (incomingCombatDamage > 0)
        {
            value = string.IsNullOrEmpty(value)
                ? $"-{incomingCombatDamage}"
                : $"{value} -{incomingCombatDamage}";
        }

        if (card.DamagePreventionRemaining != 0 || card.SourceDamagePreventionRemaining != 0)
        {
            value = string.IsNullOrEmpty(value) ? "\u9632" : $"{value} \u9632";
        }

        if (card.RegenerationShields > 0)
        {
            value = string.IsNullOrEmpty(value) ? "\u91cd\u751f" : $"{value} \u91cd\u751f";
        }

        textMesh.text = value;
    }

    private static TextMesh EnsureDamageOrderText(Transform parent)
    {
        Transform child = parent.Find("DamageOrderText");
        if (child == null)
        {
            GameObject textObject = new GameObject("DamageOrderText");
            textObject.transform.SetParent(parent, false);
            textObject.transform.localPosition = new Vector3(-0.34f, 0.574f, 0.40f);
            textObject.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

            TextMesh mesh = textObject.AddComponent<TextMesh>();
            mesh.fontSize = 36;
            mesh.characterSize = 0.070f;
            mesh.anchor = TextAnchor.MiddleCenter;
            mesh.alignment = TextAlignment.Center;
            mesh.color = new Color(1f, 0.92f, 0.18f, 1f);
            return mesh;
        }

        return child.GetComponent<TextMesh>();
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

    private static Material GetBlockLineMaterial()
    {
        if (blockLineMaterial != null) return blockLineMaterial;

        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Unlit/Color");
        if (shader == null) shader = Shader.Find("Standard");

        blockLineMaterial = new Material(shader) { name = "Arena Block Link Runtime" };
        Color color = new Color(0.08f, 0.95f, 1f, 0.88f);
        blockLineMaterial.color = color;
        if (blockLineMaterial.HasProperty("_BaseColor")) blockLineMaterial.SetColor("_BaseColor", color);
        if (blockLineMaterial.HasProperty("_Color")) blockLineMaterial.SetColor("_Color", color);
        if (blockLineMaterial.HasProperty("_Surface")) blockLineMaterial.SetFloat("_Surface", 1f);
        blockLineMaterial.renderQueue = (int)RenderQueue.Transparent;
        return blockLineMaterial;
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
