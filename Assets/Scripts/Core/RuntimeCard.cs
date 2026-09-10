using System.Collections.Generic;
using System.Text;
using UnityEngine;

public sealed class SpellTargetReference
{
    public RuntimeCard Card { get; }
    public int CardZoneChangeVersion { get; }
    public int PlayerID { get; }
    public GameStackItem StackItem { get; }
    public long StackObjectId { get; }
    public GameZone DeclaredZone { get; }
    public bool IsCardTarget => Card != null;
    public bool IsPlayerTarget => PlayerID > 0;
    public bool IsStackTarget => StackItem != null && StackObjectId > 0;
    public bool HasCurrentCardIdentity =>
        Card != null && Card.ZoneChangeVersion == CardZoneChangeVersion;

    private SpellTargetReference(
        RuntimeCard card,
        int cardZoneChangeVersion,
        int playerID,
        GameStackItem stackItem,
        long stackObjectId,
        GameZone declaredZone)
    {
        Card = card;
        CardZoneChangeVersion = cardZoneChangeVersion;
        PlayerID = playerID;
        StackItem = stackItem;
        StackObjectId = stackObjectId;
        DeclaredZone = declaredZone;
    }

    public static SpellTargetReference ForCard(RuntimeCard card)
    {
        return card != null
            ? new SpellTargetReference(
                card,
                card.ZoneChangeVersion,
                0,
                null,
                0,
                card.CurrentZone)
            : null;
    }

    public static SpellTargetReference ForPlayer(int playerID)
    {
        return playerID > 0
            ? new SpellTargetReference(null, -1, playerID, null, 0, GameZone.None)
            : null;
    }

    public static SpellTargetReference ForStackItem(GameStackItem stackItem)
    {
        return stackItem != null && stackItem.StackObjectId > 0
            ? new SpellTargetReference(
                null,
                -1,
                0,
                stackItem,
                stackItem.StackObjectId,
                GameZone.Stack)
            : null;
    }
}

public partial class RuntimeCard
{
    private const int PreventAllDamage = -1;
    private int damagePreventionRemaining;
    private int sourceDamagePreventionRemaining;
    private int regenerationShields;
    private bool hasDeathtouchDamageSinceStateCheck;
    private readonly List<ContinuousEffectRuntime> continuousEffects = new List<ContinuousEffectRuntime>();
    private readonly Dictionary<string, int> counters =
        new Dictionary<string, int>(System.StringComparer.OrdinalIgnoreCase);
    private long nextContinuousEffectId = 1;
    private long nextContinuousEffectTimestamp = 1;
    private readonly List<CardFeatureRuntime> features;
    private readonly HashSet<int> unlockedRoomFeatures = new HashSet<int>();
    private readonly List<RuntimeCard> attachedCards = new List<RuntimeCard>();
    private readonly List<SpellTargetReference> spellTargets = new List<SpellTargetReference>();
    private readonly SpellCastProfile castProfile = new SpellCastProfile();

    // Static Data
    [field: System.NonSerialized] public CardDefinition Data { get; private set; }
    public long ObjectId { get; private set; }
    public GameZone CurrentZone { get; private set; }
    [field: System.NonSerialized] public CardFeatureRuntime SpellFeature { get; private set; }
    [field: System.NonSerialized] public RuntimeCard PreparedSource { get; private set; }
    [field: System.NonSerialized] public RuntimeCard PreparedCopy { get; private set; }
    [field: System.NonSerialized] public CardFeatureRuntime ResolvingFeature { get; private set; }
    [field: System.NonSerialized] public CardRuleEntryDefinition ResolvingRuleEntry { get; private set; }
    public int ResolvingChapter { get; private set; }
    public GameEventContext ResolvingGameEvent { get; private set; }
    public IReadOnlyList<CardFeatureRuntime> Features => features;
    public CardFeatureRuntime PrimaryFeature { get; private set; }
    public CardFeatureRuntime Characteristics => SpellFeature ?? PrimaryFeature;
    public int CurrentFaceIndex { get; private set; }
    public int ClassLevel { get; private set; } = 1;
    public bool IsCaseSolved { get; private set; }
    public bool CanCastPrimaryFromAdventureExile { get; private set; }
    public bool IsPrepared { get; private set; }
    public bool IsSpellCopy { get; private set; }
    public bool IsToken { get; private set; }
    public bool IsEmblem { get; private set; }
    [field: System.NonSerialized] public RuntimeCard AttachedTo { get; private set; }
    public IReadOnlyList<RuntimeCard> AttachedCards => attachedCards;
    
    // Dynamic Stats
    public int CurrentAttack;
    public int CurrentDefence;
    public int CurrentLoyalty { get; private set; }
    public int CurrentBattleDefense { get; private set; }
    public int BattleProtectorPlayerID { get; private set; }
    public int DamageTaken { get; private set; }
    public bool IsTapped;
    public int OwnerPlayerID; // 1 or 2
    public int ControllerPlayerID { get; private set; }
    private int baseControllerPlayerID;
    public int TurnEnteredBattlefield;
    public long BattlefieldEntrySequence;
    public int ZoneChangeVersion { get; private set; }
    public bool IsDead { get; private set; }
    public int DamagePreventionRemaining => damagePreventionRemaining;
    public int SourceDamagePreventionRemaining => sourceDamagePreventionRemaining;
    public int RegenerationShields => regenerationShields;
    public bool HasDeathtouchDamageSinceStateCheck => hasDeathtouchDamageSinceStateCheck;
    public IReadOnlyList<SpellTargetReference> SpellTargets => spellTargets;
    public SpellCastProfile CastProfile => castProfile;
    internal bool IsInitializingScript { get; private set; }
    public SpellCastDeclaration CurrentCastDeclaration { get; private set; }
    public RuntimeCard TargetCard =>
        spellTargets.Count > 0 && spellTargets[0].IsCardTarget
            ? spellTargets[0].Card
            : null;
    public int TargetCardZoneChangeVersion =>
        spellTargets.Count > 0 && spellTargets[0].IsCardTarget
            ? spellTargets[0].CardZoneChangeVersion
            : -1;
    public int TargetPlayerID =>
        spellTargets.Count > 0 && spellTargets[0].IsPlayerTarget
            ? spellTargets[0].PlayerID
            : 0;
    public bool HasSpellTarget
    {
        get
        {
            for (int i = 0; i < spellTargets.Count; i++)
            {
                SpellTargetReference target = spellTargets[i];
                if (target != null
                    && (target.IsPlayerTarget || target.HasCurrentCardIdentity))
                {
                    return true;
                }
            }

            return false;
        }
    }
    public bool HasCurrentTargetCardIdentity =>
        spellTargets.Count > 0 && spellTargets[0].HasCurrentCardIdentity;
    
    // Dynamic Abilities (Script Context)
    public ICardScriptInstance ScriptInstance; 
    
    // Keywords cache for fast lookup (managed by Lua or C#)
    private readonly HashSet<string> baseKeywords =
        new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> calculatedKeywords =
        new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> copiableKeywords =
        new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
    private CardType calculatedCardTypes;
    private CardSupertype calculatedSupertypes;
    private string calculatedSubtypes;
    private ManaColorFlags calculatedColors;
    private string calculatedRulesText;
    private string calculatedCardName;
    private ManaCost calculatedManaCost;
    private CardType copiableCardTypes;
    private CardSupertype copiableSupertypes;
    private string copiableSubtypes;
    private ManaColorFlags copiableColors;
    private string copiableRulesText;
    private string copiableCardName;
    private ManaCost copiableManaCost;
    private int copiablePower;
    private int copiableToughness;

    // Activated Abilities — lazy-initialized; most cards never add abilities
    private List<ActivatedAbility> _abilities;
    public List<ActivatedAbility> Abilities
    {
        get
        {
            if (_abilities == null) _abilities = new List<ActivatedAbility>(1);
            return _abilities;
        }
    }

    // Combat State
    public bool IsAttacking;
    public bool IsBlocked;
    public bool WasBlockedThisCombat;
    public bool IsBlocking;
    public object AttackTarget; // Usually int PlayerID, but could be Planeswalker
    public RuntimeCard BlockingTarget; // The attacker this card is blocking
    public List<RuntimeCard> Blockers = new List<RuntimeCard>(); // Creatures blocking this card

    public RuntimeCard(CardDefinition data, int ownerID, bool initializeScript = true)
    {
        Data = data;
        OwnerPlayerID = ownerID;
        ControllerPlayerID = ownerID;
        baseControllerPlayerID = ownerID;
        CurrentZone = GameZone.Library;
        features = CardFeatureRuntimeFactory.Create(data);
        PrimaryFeature = FindPrimaryFeature(0) ?? (features.Count > 0 ? features[0] : null);
        
        // Initialize Stats
        CurrentAttack = PrimaryFeature != null ? PrimaryFeature.Power : data.attack;
        CurrentDefence = PrimaryFeature != null ? PrimaryFeature.Toughness : data.defence;
        CurrentLoyalty = (PrimaryFeature != null
                ? PrimaryFeature.HasType(CardType.Planeswalker)
                : data.HasType(CardType.Planeswalker))
            ? Mathf.Max(0, PrimaryFeature != null ? PrimaryFeature.Loyalty : data.loyalty)
            : 0;
        CurrentBattleDefense = (PrimaryFeature != null
                ? PrimaryFeature.HasType(CardType.Battle)
                : data.HasType(CardType.Battle))
            ? Mathf.Max(0, PrimaryFeature != null ? PrimaryFeature.BattleDefense : data.battleDefense)
            : 0;
        DamageTaken = 0;
        IsTapped = false;
        RecalculateContinuousEffects();
        
        if (initializeScript) EnsureScriptInstance();
    }

    public void AssignObjectId(long objectId)
    {
        if (objectId <= 0 || ObjectId > 0) return;
        ObjectId = objectId;
    }

    public void ReassignObjectId(long objectId)
    {
        if (objectId <= 0) return;
        ObjectId = objectId;
    }

    public void AppendDeterministicState(StringBuilder builder)
    {
        if (builder == null) return;
        builder.Append(ObjectId).Append(':')
            .Append(ZoneChangeVersion).Append(':')
            .Append(Data?.PrintingKey ?? string.Empty).Append(':')
            .Append((int)CurrentZone).Append(':')
            .Append(OwnerPlayerID).Append(':')
            .Append(baseControllerPlayerID).Append(':')
            .Append(ControllerPlayerID).Append(':')
            .Append(CurrentFaceIndex).Append(':')
            .Append(SpellFeature?.FeatureIndex ?? -1).Append(':')
            .Append(CurrentAttack).Append(':')
            .Append(CurrentDefence).Append(':')
            .Append(CurrentLoyalty).Append(':')
            .Append(CurrentBattleDefense).Append(':')
            .Append(BattleProtectorPlayerID).Append(':')
            .Append(DamageTaken).Append(':')
            .Append(IsTapped ? 1 : 0).Append(':')
            .Append(IsDead ? 1 : 0).Append(':')
            .Append(IsToken ? 1 : 0).Append(':')
            .Append(IsSpellCopy ? 1 : 0).Append(':')
            .Append(IsEmblem ? 1 : 0).Append(':')
            .Append(IsPrepared ? 1 : 0).Append(':')
            .Append(IsCaseSolved ? 1 : 0).Append(':')
            .Append(ClassLevel).Append(':')
            .Append(damagePreventionRemaining).Append(':')
            .Append(sourceDamagePreventionRemaining).Append(':')
            .Append(regenerationShields).Append(':')
            .Append(IsAttacking ? 1 : 0).Append(':')
            .Append(IsBlocked ? 1 : 0).Append(':')
            .Append(WasBlockedThisCombat ? 1 : 0).Append(':')
            .Append(IsBlocking ? 1 : 0).Append(':')
            .Append((int)calculatedCardTypes).Append(':')
            .Append((int)calculatedSupertypes).Append(':')
            .Append(calculatedSubtypes ?? string.Empty).Append(':')
            .Append((int)calculatedColors).Append(':')
            .Append(calculatedCardName ?? string.Empty).Append(':')
            .Append(calculatedManaCost.ToString()).Append(':')
            .Append(calculatedRulesText ?? string.Empty).Append(':');
        AppendAttackTargetState(builder);
        builder.Append(':').Append(BlockingTarget?.ObjectId ?? 0).Append(':')
            .Append(AttachedTo?.ObjectId ?? 0).Append(':');

        AppendSortedDictionary(builder, counters);
        AppendSortedSet(builder, baseKeywords);
        AppendSortedSet(builder, calculatedKeywords);

        builder.Append("T[");
        for (int i = 0; i < spellTargets.Count; i++)
        {
            SpellTargetReference target = spellTargets[i];
            builder.Append(target?.Card?.ObjectId ?? 0).Append('/')
                .Append(target?.CardZoneChangeVersion ?? -1).Append('/')
                .Append(target?.PlayerID ?? 0).Append(',');
        }
        builder.Append("]B[");
        for (int i = 0; i < Blockers.Count; i++)
        {
            builder.Append(Blockers[i]?.ObjectId ?? 0).Append(',');
        }
        builder.Append("]A[");
        for (int i = 0; i < attachedCards.Count; i++)
        {
            builder.Append(attachedCards[i]?.ObjectId ?? 0).Append(',');
        }
        builder.Append("]R[");
        var rooms = new List<int>(unlockedRoomFeatures);
        rooms.Sort();
        for (int i = 0; i < rooms.Count; i++) builder.Append(rooms[i]).Append(',');
        builder.Append("]E[");
        SortContinuousEffects();
        for (int i = 0; i < continuousEffects.Count; i++)
        {
            ContinuousEffectRuntime effect = continuousEffects[i];
            builder.Append(effect.effectId).Append('/')
                .Append(effect.timestamp).Append('/')
                .Append((int)effect.layer).Append('/')
                .Append((int)effect.copyEffectSublayer).Append('/')
                .Append((int)effect.powerToughnessSublayer).Append('/')
                .Append((int)effect.duration).Append('/')
                .Append((int)effect.operation).Append('/')
                .Append(effect.source?.ObjectId ?? 0).Append('/')
                .Append((int)effect.cardTypes).Append('/')
                .Append((int)effect.colors).Append('/')
                .Append(effect.keyword ?? string.Empty).Append('/')
                .Append(effect.rulesText ?? string.Empty).Append('/')
                .Append(effect.controllerPlayerId).Append('/')
                .Append(effect.linkId ?? string.Empty).Append('/')
                .Append(effect.dependencyKeys ?? string.Empty).Append('/')
                .Append(effect.characteristicDefining ? 1 : 0).Append('/')
                .Append((int)effect.reads).Append('/')
                .Append((int)effect.writes).Append('/')
                .Append(effect.copySource?.ObjectId ?? 0).Append('/')
                .Append(effect.copySourceZoneChangeVersion).Append('/')
                .Append((int)effect.supertypes).Append('/')
                .Append(effect.subtypes ?? string.Empty).Append('/')
                .Append(effect.cardName ?? string.Empty).Append('/')
                .Append(effect.power).Append('/')
                .Append(effect.toughness).Append(',');
        }
        builder.Append("]");
    }

    private void AppendAttackTargetState(StringBuilder builder)
    {
        if (AttackTarget is int playerTarget)
        {
            builder.Append('P').Append(playerTarget);
        }
        else if (AttackTarget is RuntimeCard permanentTarget)
        {
            builder.Append('C')
                .Append(permanentTarget.ObjectId)
                .Append('/')
                .Append(permanentTarget.ZoneChangeVersion);
        }
        else
        {
            builder.Append('N');
        }
    }

    private static void AppendSortedDictionary(
        StringBuilder builder,
        Dictionary<string, int> values)
    {
        var keys = new List<string>(values.Keys);
        keys.Sort(System.StringComparer.OrdinalIgnoreCase);
        builder.Append("C[");
        for (int i = 0; i < keys.Count; i++)
        {
            string key = keys[i];
            builder.Append(key).Append('=').Append(values[key]).Append(',');
        }
        builder.Append(']');
    }

    private static void AppendSortedSet(StringBuilder builder, HashSet<string> values)
    {
        var entries = new List<string>(values);
        entries.Sort(System.StringComparer.OrdinalIgnoreCase);
        builder.Append("K[");
        for (int i = 0; i < entries.Count; i++)
        {
            builder.Append(entries[i]).Append(',');
        }
        builder.Append(']');
    }

    public void SetCurrentZone(GameZone zone)
    {
        CurrentZone = zone;
    }

    public void SetResolvingGameEvent(GameEventContext gameEvent)
    {
        ResolvingGameEvent = gameEvent;
    }

    public void ClearResolvingGameEvent()
    {
        ResolvingGameEvent = null;
    }

    public bool HasKeyword(string keywordName)
    {
        return !string.IsNullOrWhiteSpace(keywordName) && calculatedKeywords.Contains(keywordName);
    }

    public bool HasSubtype(params string[] aliases)
    {
        if (aliases == null
            || aliases.Length == 0
            || string.IsNullOrWhiteSpace(calculatedSubtypes))
        {
            return false;
        }

        string[] values = calculatedSubtypes.Split(
            new[] { ',', '\uff0c', ';', '\uff1b' },
            System.StringSplitOptions.RemoveEmptyEntries);
        for (int valueIndex = 0; valueIndex < values.Length; valueIndex++)
        {
            string value = values[valueIndex].Trim();
            for (int aliasIndex = 0; aliasIndex < aliases.Length; aliasIndex++)
            {
                if (value.Equals(
                        aliases[aliasIndex],
                        System.StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }

        return false;
    }

    // Helper for Lua to add keywords
    public void AddKeyword(string keyword)
    {
        if (string.IsNullOrWhiteSpace(keyword)) return;
        baseKeywords.Add(keyword.Trim());
        RecalculateContinuousEffects();
    }

    public void RemoveKeyword(string keyword)
    {
        if (string.IsNullOrWhiteSpace(keyword)) return;
        baseKeywords.Remove(keyword.Trim());
        RecalculateContinuousEffects();
    }

    public void AddLoyaltyCounters(int amount)
    {
        if (Data == null || !HasType(CardType.Planeswalker) || amount == 0) return;
        CurrentLoyalty = Mathf.Max(0, CurrentLoyalty + amount);
    }

    public void AddBattleDefenseCounters(int amount)
    {
        if (Data == null || !HasType(CardType.Battle) || amount == 0) return;
        CurrentBattleDefense = Mathf.Max(0, CurrentBattleDefense + amount);
    }

    public bool SetBattleProtector(int playerID)
    {
        if (!HasType(CardType.Battle)
            || (playerID != 1 && playerID != 2)
            || BattleProtectorPlayerID == playerID)
        {
            return false;
        }

        BattleProtectorPlayerID = playerID;
        return true;
    }

    public int GetCounters(string counterName)
    {
        string key = NormalizeCounterName(counterName);
        if (key == "loyalty") return CurrentLoyalty;
        if (key == "defense" || key == "defence") return CurrentBattleDefense;
        return counters.TryGetValue(key, out int count) ? count : 0;
    }

    public int AddCounters(string counterName, int amount)
    {
        string key = NormalizeCounterName(counterName);
        if (string.IsNullOrEmpty(key) || amount == 0) return GetCounters(key);
        if (key == "loyalty")
        {
            AddLoyaltyCounters(amount);
            return CurrentLoyalty;
        }
        if (key == "defense" || key == "defence")
        {
            AddBattleDefenseCounters(amount);
            return CurrentBattleDefense;
        }

        int next = Mathf.Max(0, GetCounters(key) + amount);
        if (next == 0) counters.Remove(key);
        else counters[key] = next;

        if (key == "+1/+1" || key == "-1/-1")
        {
            RecalculatePowerToughness();
        }

        return next;
    }

    public bool CancelOpposingPowerToughnessCounters()
    {
        int positive = GetCounters("+1/+1");
        int negative = GetCounters("-1/-1");
        int cancelled = Mathf.Min(positive, negative);
        if (cancelled <= 0) return false;

        AddCounters("+1/+1", -cancelled);
        AddCounters("-1/-1", -cancelled);
        return true;
    }

    public IEnumerable<string> GetKeywords()
    {
        return calculatedKeywords;
    }

    public void AddPowerToughnessModifier(int power, int toughness, string source = null, bool untilEndOfTurn = false)
    {
        AddContinuousEffect(new ContinuousEffectRuntime
        {
            layer = ContinuousEffectLayer.PowerToughness,
            powerToughnessSublayer = PowerToughnessSublayer.Modify,
            duration = untilEndOfTurn
                ? ContinuousEffectDuration.UntilEndOfTurn
                : ContinuousEffectDuration.Permanent,
            operation = ContinuousEffectOperation.ModifyPowerToughness,
            sourceKey = source ?? string.Empty,
            power = power,
            toughness = toughness
        });
    }

    public void SetPowerToughness(int power, int toughness, string source = null, bool untilEndOfTurn = false)
    {
        AddContinuousEffect(new ContinuousEffectRuntime
        {
            layer = ContinuousEffectLayer.PowerToughness,
            powerToughnessSublayer = PowerToughnessSublayer.Set,
            duration = untilEndOfTurn
                ? ContinuousEffectDuration.UntilEndOfTurn
                : ContinuousEffectDuration.Permanent,
            operation = ContinuousEffectOperation.SetPowerToughness,
            sourceKey = source ?? string.Empty,
            power = power,
            toughness = toughness
        });
    }

    public void SwitchPowerToughness(bool untilEndOfTurn)
    {
        AddContinuousEffect(new ContinuousEffectRuntime
        {
            layer = ContinuousEffectLayer.PowerToughness,
            powerToughnessSublayer = PowerToughnessSublayer.Switch,
            duration = untilEndOfTurn
                ? ContinuousEffectDuration.UntilEndOfTurn
                : ContinuousEffectDuration.Permanent,
            operation = ContinuousEffectOperation.SwitchPowerToughness
        });
    }

    public void ClearPowerToughnessModifiersFrom(string source)
    {
        if (string.IsNullOrEmpty(source)) return;

        for (int i = continuousEffects.Count - 1; i >= 0; i--)
        {
            if (continuousEffects[i].sourceKey == source
                && continuousEffects[i].layer == ContinuousEffectLayer.PowerToughness)
            {
                continuousEffects.RemoveAt(i);
            }
        }

        RecalculateContinuousEffects();
    }

    public void RecalculatePowerToughness()
    {
        RecalculateContinuousEffects();
    }

    public long AddContinuousEffect(ContinuousEffectRuntime effect)
    {
        if (effect == null) return 0;

        effect.effectId = nextContinuousEffectId++;
        if (effect.timestamp <= 0)
        {
            effect.timestamp = nextContinuousEffectTimestamp++;
        }
        else
        {
            nextContinuousEffectTimestamp =
                System.Math.Max(nextContinuousEffectTimestamp, effect.timestamp + 1);
        }
        if (effect.source != null)
        {
            effect.sourceZoneChangeVersion = effect.source.ZoneChangeVersion;
        }
        continuousEffects.Add(effect);
        RecalculateContinuousEffects();
        return effect.effectId;
    }

    public long AddCardTypeEffect(
        CardType cardTypes,
        bool remove,
        string sourceKey,
        bool untilEndOfTurn)
    {
        return AddContinuousEffect(new ContinuousEffectRuntime
        {
            layer = ContinuousEffectLayer.Type,
            duration = untilEndOfTurn
                ? ContinuousEffectDuration.UntilEndOfTurn
                : ContinuousEffectDuration.Permanent,
            operation = remove
                ? ContinuousEffectOperation.RemoveCardType
                : ContinuousEffectOperation.AddCardType,
            sourceKey = sourceKey ?? string.Empty,
            cardTypes = cardTypes
        });
    }

    public long SetColorEffect(
        ManaColorFlags colors,
        string sourceKey,
        bool untilEndOfTurn)
    {
        return AddContinuousEffect(new ContinuousEffectRuntime
        {
            layer = ContinuousEffectLayer.Color,
            duration = untilEndOfTurn
                ? ContinuousEffectDuration.UntilEndOfTurn
                : ContinuousEffectDuration.Permanent,
            operation = ContinuousEffectOperation.SetColors,
            sourceKey = sourceKey ?? string.Empty,
            colors = colors
        });
    }

    public long AddKeywordEffect(
        string keyword,
        bool remove,
        string sourceKey,
        bool untilEndOfTurn)
    {
        if (string.IsNullOrWhiteSpace(keyword)) return 0;
        return AddContinuousEffect(new ContinuousEffectRuntime
        {
            layer = ContinuousEffectLayer.Ability,
            duration = untilEndOfTurn
                ? ContinuousEffectDuration.UntilEndOfTurn
                : ContinuousEffectDuration.Permanent,
            operation = remove
                ? ContinuousEffectOperation.RemoveKeyword
                : ContinuousEffectOperation.AddKeyword,
            sourceKey = sourceKey ?? string.Empty,
            keyword = keyword.Trim()
        });
    }

    public bool RemoveContinuousEffect(long effectId)
    {
        for (int i = 0; i < continuousEffects.Count; i++)
        {
            if (continuousEffects[i].effectId != effectId) continue;
            continuousEffects.RemoveAt(i);
            RecalculateContinuousEffects();
            return true;
        }

        return false;
    }

    public void RecalculateContinuousEffects()
    {
        for (int i = continuousEffects.Count - 1; i >= 0; i--)
        {
            if (!continuousEffects[i].IsActive)
            {
                continuousEffects.RemoveAt(i);
            }
        }
        SortContinuousEffects();

        calculatedCardTypes =
            Characteristics != null ? Characteristics.CardTypes : Data?.cardTypes ?? CardType.None;
        calculatedSupertypes =
            Characteristics != null ? Characteristics.Supertypes : Data?.supertypes ?? CardSupertype.None;
        calculatedSubtypes =
            Characteristics != null ? Characteristics.Subtypes : Data?.subtypes ?? string.Empty;
        calculatedColors = GetPrintedColors();
        calculatedRulesText =
            Characteristics != null ? Characteristics.RulesText : Data?.description ?? string.Empty;
        calculatedCardName =
            Characteristics != null ? Characteristics.CardName : Data?.cardName ?? string.Empty;
        calculatedManaCost =
            Characteristics != null ? Characteristics.ManaCost : Data?.GetManaCost() ?? default;
        calculatedKeywords.Clear();
        calculatedKeywords.UnionWith(baseKeywords);
        ControllerPlayerID = baseControllerPlayerID;

        int power = Characteristics != null ? Characteristics.Power : Data != null ? Data.attack : 0;
        int toughness = Characteristics != null ? Characteristics.Toughness : Data != null ? Data.defence : 0;
        CaptureCopiableValues(power, toughness);
        for (int i = 0; i < continuousEffects.Count; i++)
        {
            ContinuousEffectRuntime effect = continuousEffects[i];
            switch (effect.operation)
            {
                case ContinuousEffectOperation.CopyCharacteristics:
                    ApplyCopyCharacteristics(
                        effect.copySource,
                        ref calculatedCardTypes,
                        ref calculatedSupertypes,
                        ref calculatedSubtypes,
                        ref calculatedColors,
                        ref calculatedRulesText,
                        ref calculatedCardName,
                        ref calculatedManaCost,
                        ref power,
                        ref toughness,
                        calculatedKeywords);
                    CaptureCopiableValues(power, toughness);
                    break;
                case ContinuousEffectOperation.SetFaceDownCharacteristics:
                    calculatedCardTypes = effect.cardTypes == CardType.None
                        ? CardType.Creature
                        : effect.cardTypes;
                    calculatedSupertypes = effect.supertypes;
                    calculatedSubtypes = effect.subtypes ?? string.Empty;
                    calculatedColors = effect.colors;
                    calculatedRulesText = effect.rulesText ?? string.Empty;
                    calculatedCardName = effect.cardName ?? string.Empty;
                    calculatedManaCost = default;
                    calculatedKeywords.Clear();
                    power = effect.power;
                    toughness = effect.toughness;
                    CaptureCopiableValues(power, toughness);
                    break;
                case ContinuousEffectOperation.SetController:
                    if (effect.controllerPlayerId == 1 || effect.controllerPlayerId == 2)
                    {
                        ControllerPlayerID = effect.controllerPlayerId;
                    }
                    break;
                case ContinuousEffectOperation.ReplaceRulesText:
                    calculatedRulesText = effect.rulesText ?? string.Empty;
                    break;
                case ContinuousEffectOperation.SetCardTypes:
                    calculatedCardTypes = effect.cardTypes;
                    break;
                case ContinuousEffectOperation.AddCardType:
                    calculatedCardTypes |= effect.cardTypes;
                    break;
                case ContinuousEffectOperation.RemoveCardType:
                    calculatedCardTypes &= ~effect.cardTypes;
                    break;
                case ContinuousEffectOperation.SetSupertypes:
                    calculatedSupertypes = effect.supertypes;
                    break;
                case ContinuousEffectOperation.AddSupertype:
                    calculatedSupertypes |= effect.supertypes;
                    break;
                case ContinuousEffectOperation.RemoveSupertype:
                    calculatedSupertypes &= ~effect.supertypes;
                    break;
                case ContinuousEffectOperation.SetSubtypes:
                    calculatedSubtypes = effect.subtypes ?? string.Empty;
                    break;
                case ContinuousEffectOperation.SetColors:
                    calculatedColors = effect.colors;
                    break;
                case ContinuousEffectOperation.SetName:
                    calculatedCardName = effect.cardName ?? string.Empty;
                    break;
                case ContinuousEffectOperation.AddKeyword:
                    if (!string.IsNullOrWhiteSpace(effect.keyword))
                        calculatedKeywords.Add(effect.keyword.Trim());
                    break;
                case ContinuousEffectOperation.RemoveKeyword:
                    if (!string.IsNullOrWhiteSpace(effect.keyword))
                        calculatedKeywords.Remove(effect.keyword.Trim());
                    break;
                case ContinuousEffectOperation.RemoveAllKeywords:
                    calculatedKeywords.Clear();
                    break;
                case ContinuousEffectOperation.SetPowerToughness:
                    power = effect.power;
                    toughness = effect.toughness;
                    break;
                case ContinuousEffectOperation.ModifyPowerToughness:
                    power += effect.power;
                    toughness += effect.toughness;
                    break;
                case ContinuousEffectOperation.SwitchPowerToughness:
                    break;
            }
        }

        power += GetCounters("+1/+1") - GetCounters("-1/-1");
        toughness += GetCounters("+1/+1") - GetCounters("-1/-1");

        // Counters and other modifiers share layer 7c. Switch effects are layer 7d,
        // so replay them after counters regardless of when they were created.
        int switchCount = 0;
        for (int i = 0; i < continuousEffects.Count; i++)
        {
            if (continuousEffects[i].operation == ContinuousEffectOperation.SwitchPowerToughness)
                switchCount++;
        }
        if ((switchCount & 1) != 0)
        {
            int previousPower = power;
            power = toughness;
            toughness = previousPower;
        }

        CurrentAttack = power;
        CurrentDefence = toughness;
    }

    private static void ApplyCopyCharacteristics(
        RuntimeCard copySource,
        ref CardType cardTypes,
        ref CardSupertype supertypes,
        ref string subtypes,
        ref ManaColorFlags colors,
        ref string rulesText,
        ref string cardName,
        ref ManaCost manaCost,
        ref int power,
        ref int toughness,
        HashSet<string> keywords)
    {
        if (copySource == null) return;

        cardTypes = copySource.copiableCardTypes;
        supertypes = copySource.copiableSupertypes;
        subtypes = copySource.copiableSubtypes;
        colors = copySource.copiableColors;
        rulesText = copySource.copiableRulesText;
        cardName = copySource.copiableCardName;
        manaCost = copySource.copiableManaCost;
        power = copySource.copiablePower;
        toughness = copySource.copiableToughness;
        keywords?.Clear();
        if (keywords != null) keywords.UnionWith(copySource.copiableKeywords);
    }

    private void CaptureCopiableValues(int power, int toughness)
    {
        copiableCardTypes = calculatedCardTypes;
        copiableSupertypes = calculatedSupertypes;
        copiableSubtypes = calculatedSubtypes;
        copiableColors = calculatedColors;
        copiableRulesText = calculatedRulesText;
        copiableCardName = calculatedCardName;
        copiableManaCost = calculatedManaCost;
        copiablePower = power;
        copiableToughness = toughness;
        copiableKeywords.Clear();
        copiableKeywords.UnionWith(calculatedKeywords);
    }

    private void SortContinuousEffects()
    {
        continuousEffects.Sort(CompareContinuousEffectPosition);
        if (continuousEffects.Count < 2) return;

        var ordered = new List<ContinuousEffectRuntime>(continuousEffects.Count);
        int groupStart = 0;
        while (groupStart < continuousEffects.Count)
        {
            ContinuousEffectRuntime first = continuousEffects[groupStart];
            int groupEnd = groupStart + 1;
            while (groupEnd < continuousEffects.Count
                && IsSameContinuousEffectGroup(first, continuousEffects[groupEnd]))
            {
                groupEnd++;
            }

            var remaining = new List<ContinuousEffectRuntime>(groupEnd - groupStart);
            for (int i = groupStart; i < groupEnd; i++)
            {
                remaining.Add(continuousEffects[i]);
            }

            while (remaining.Count > 0)
            {
                int readyIndex = -1;
                for (int candidateIndex = 0; candidateIndex < remaining.Count; candidateIndex++)
                {
                    bool hasUnresolvedDependency = false;
                    for (int dependencyIndex = 0; dependencyIndex < remaining.Count; dependencyIndex++)
                    {
                        if (candidateIndex == dependencyIndex) continue;
                        if (!remaining[candidateIndex].DependsOn(remaining[dependencyIndex])
                            && !remaining[candidateIndex].StructurallyDependsOn(
                                remaining[dependencyIndex]))
                        {
                            continue;
                        }
                        hasUnresolvedDependency = true;
                        break;
                    }

                    if (!hasUnresolvedDependency)
                    {
                        readyIndex = candidateIndex;
                        break;
                    }
                }

                // Cyclic declarations are invalid. Timestamp/effect ID order keeps the
                // resulting state deterministic while allowing the match to continue.
                if (readyIndex < 0) readyIndex = 0;
                ordered.Add(remaining[readyIndex]);
                remaining.RemoveAt(readyIndex);
            }

            groupStart = groupEnd;
        }

        continuousEffects.Clear();
        continuousEffects.AddRange(ordered);
    }

    private static bool IsSameContinuousEffectGroup(
        ContinuousEffectRuntime left,
        ContinuousEffectRuntime right)
    {
        if (left.layer != right.layer) return false;
        if (left.layer == ContinuousEffectLayer.PowerToughness)
        {
            return left.powerToughnessSublayer == right.powerToughnessSublayer;
        }
        return left.layer != ContinuousEffectLayer.Copy
            || left.copyEffectSublayer == right.copyEffectSublayer;
    }

    private static int CompareContinuousEffectPosition(
        ContinuousEffectRuntime left,
        ContinuousEffectRuntime right)
    {
        int layerOrder = left.layer.CompareTo(right.layer);
        if (layerOrder != 0) return layerOrder;

        if (left.layer == ContinuousEffectLayer.PowerToughness)
        {
            int sublayerOrder = left.powerToughnessSublayer.CompareTo(right.powerToughnessSublayer);
            if (sublayerOrder != 0) return sublayerOrder;
        }
        else if (left.layer == ContinuousEffectLayer.Copy)
        {
            int sublayerOrder = left.copyEffectSublayer.CompareTo(right.copyEffectSublayer);
            if (sublayerOrder != 0) return sublayerOrder;
        }

        if (left.layer >= ContinuousEffectLayer.Control
            && left.layer <= ContinuousEffectLayer.Ability
            && left.characteristicDefining != right.characteristicDefining)
        {
            return left.characteristicDefining ? -1 : 1;
        }

        int timestampOrder = left.timestamp.CompareTo(right.timestamp);
        return timestampOrder != 0
            ? timestampOrder
            : left.effectId.CompareTo(right.effectId);
    }

    private ManaColorFlags GetPrintedColors()
    {
        if (Data == null) return ManaColorFlags.None;
        if (Characteristics == null || ReferenceEquals(Characteristics, PrimaryFeature))
        {
            return Data.GetColors();
        }

        return Characteristics.GetColors();
    }

    private static string NormalizeCounterName(string counterName)
    {
        return string.IsNullOrWhiteSpace(counterName)
            ? string.Empty
            : counterName.Trim().ToLowerInvariant();
    }

    public void AddDamagePrevention(int amount)
    {
        AddPrevention(ref damagePreventionRemaining, amount);
    }

    public void AddPreventAllDamage()
    {
        damagePreventionRemaining = PreventAllDamage;
    }

    public int PreventIncomingDamage(int damage)
    {
        return PreventDamage(ref damagePreventionRemaining, damage);
    }

    public bool HasIncomingDamagePrevention =>
        damagePreventionRemaining != 0;

    public void AddSourceDamagePrevention(int amount)
    {
        AddPrevention(ref sourceDamagePreventionRemaining, amount);
    }

    public void AddPreventAllDamageByThisSource()
    {
        sourceDamagePreventionRemaining = PreventAllDamage;
    }

    public int PreventOutgoingDamage(int damage)
    {
        return PreventDamage(ref sourceDamagePreventionRemaining, damage);
    }

    public bool HasOutgoingDamagePrevention =>
        sourceDamagePreventionRemaining != 0;

    public void AddRegenerationShield()
    {
        regenerationShields++;
    }

    public bool Destroy(bool allowRegeneration = true)
    {
        if (IsDead) return false;
        if (HasKeyword("Indestructible")) return false;
        if (allowRegeneration && TryUseRegenerationShield()) return false;

        Die();
        return true;
    }

    public void ClearEndOfTurnEffects()
    {
        damagePreventionRemaining = 0;
        sourceDamagePreventionRemaining = 0;
        regenerationShields = 0;
        for (int i = continuousEffects.Count - 1; i >= 0; i--)
        {
            if (continuousEffects[i].duration == ContinuousEffectDuration.UntilEndOfTurn)
            {
                continuousEffects.RemoveAt(i);
            }
        }
        RecalculateContinuousEffects();
    }

    public void SetSpellTarget(RuntimeCard target)
    {
        spellTargets.Clear();
        AddSpellTarget(target);
    }

    public void SetSpellTargetPlayer(int playerID)
    {
        spellTargets.Clear();
        AddSpellTargetPlayer(playerID);
    }

    public bool AddSpellTarget(RuntimeCard target)
    {
        SpellTargetReference reference = SpellTargetReference.ForCard(target);
        if (reference == null) return false;
        spellTargets.Add(reference);
        return true;
    }

    public bool AddSpellTargetPlayer(int playerID)
    {
        SpellTargetReference reference = SpellTargetReference.ForPlayer(playerID);
        if (reference == null) return false;
        spellTargets.Add(reference);
        return true;
    }

    public void ClearSpellTarget()
    {
        spellTargets.Clear();
    }

    public void SetCurrentCastDeclaration(SpellCastDeclaration declaration)
    {
        CurrentCastDeclaration = declaration;
    }

    public void ClearCurrentCastDeclaration()
    {
        CurrentCastDeclaration = null;
    }

    public void SetSpellTargets(IEnumerable<SpellTargetReference> targets)
    {
        spellTargets.Clear();
        if (targets == null) return;

        foreach (SpellTargetReference target in targets)
        {
            if (target != null) spellTargets.Add(target);
        }
    }

    public bool SetController(int playerID)
    {
        if ((playerID != 1 && playerID != 2) || baseControllerPlayerID == playerID)
        {
            return false;
        }

        baseControllerPlayerID = playerID;
        ControllerPlayerID = playerID;
        return true;
    }

    public void MarkAsToken()
    {
        IsToken = true;
    }

    public void MarkAsSpellCopy()
    {
        IsSpellCopy = true;
    }

    public void MarkAsEmblem()
    {
        IsEmblem = true;
    }

    public void MarkZoneChange()
    {
        Detach();
        for (int i = attachedCards.Count - 1; i >= 0; i--)
        {
            RuntimeCard attachment = attachedCards[i];
            if (attachment != null) attachment.AttachedTo = null;
        }
        attachedCards.Clear();

        unchecked
        {
            ZoneChangeVersion++;
        }

        ControllerPlayerID = OwnerPlayerID;
        baseControllerPlayerID = OwnerPlayerID;
        DamageTaken = 0;
        IsDead = false;
        IsTapped = false;
        hasDeathtouchDamageSinceStateCheck = false;
        damagePreventionRemaining = 0;
        sourceDamagePreventionRemaining = 0;
        regenerationShields = 0;
        counters.Clear();
        continuousEffects.Clear();
        CurrentLoyalty = (Characteristics != null
                ? Characteristics.HasType(CardType.Planeswalker)
                : Data != null && Data.HasType(CardType.Planeswalker))
            ? Mathf.Max(0, Characteristics != null ? Characteristics.Loyalty : Data?.loyalty ?? 0)
            : 0;
        CurrentBattleDefense = (Characteristics != null
                ? Characteristics.HasType(CardType.Battle)
                : Data != null && Data.HasType(CardType.Battle))
            ? Mathf.Max(
                0,
                Characteristics != null
                    ? Characteristics.BattleDefense
                    : Data?.battleDefense ?? 0)
            : 0;
        BattleProtectorPlayerID = 0;
        ClearSpellTarget();
        ClearCurrentCastDeclaration();
        RemoveFromCombat();
        RecalculateContinuousEffects();
    }

    public bool AttachTo(RuntimeCard target)
    {
        if (target == null || ReferenceEquals(this, target)) return false;
        if (ReferenceEquals(AttachedTo, target)) return true;

        Detach();
        AttachedTo = target;
        if (!target.attachedCards.Contains(this))
        {
            target.attachedCards.Add(this);
        }
        return true;
    }

    public bool Detach()
    {
        if (AttachedTo == null) return false;

        RuntimeCard previousTarget = AttachedTo;
        AttachedTo = null;
        previousTarget.attachedCards.Remove(this);
        return true;
    }

    public CardFeatureRuntime FindFeature(int faceIndex, int featureIndex)
    {
        for (int i = 0; i < features.Count; i++)
        {
            CardFeatureRuntime feature = features[i];
            if (feature.FaceIndex == faceIndex && feature.FeatureIndex == featureIndex)
            {
                return feature;
            }
        }

        return null;
    }

    public CardFeatureRuntime FindPrimaryFeature(int faceIndex)
    {
        for (int i = 0; i < features.Count; i++)
        {
            CardFeatureRuntime feature = features[i];
            if (feature.FaceIndex == faceIndex && feature.IsPrimary)
            {
                return feature;
            }
        }

        return null;
    }

    public bool HasFeatureRole(CardFeatureRole role)
    {
        for (int i = 0; i < features.Count; i++)
        {
            if (features[i].Role == role) return true;
        }

        return false;
    }

    public void SetSpellFeature(CardFeatureRuntime feature)
    {
        SpellFeature = feature ?? PrimaryFeature;
        RecalculatePowerToughness();
    }

    public void ClearSpellFeature()
    {
        SpellFeature = null;
        RecalculatePowerToughness();
    }

    public bool SetCurrentFace(int faceIndex)
    {
        CardFeatureRuntime primary = FindPrimaryFeature(faceIndex);
        if (primary == null) return false;

        CurrentFaceIndex = faceIndex;
        PrimaryFeature = primary;
        ClearSpellFeature();
        return true;
    }

    public bool HasType(CardType type)
    {
        return calculatedCardTypes.Has(type);
    }

    public bool HasSupertype(CardSupertype type)
    {
        return calculatedSupertypes.Has(type);
    }

    public CardTargetMode GetTargetMode()
    {
        if (Characteristics != null && Characteristics.TargetMode != CardTargetMode.None)
        {
            return Characteristics.TargetMode;
        }

        return SpellFeature == null && Data != null
            ? Data.targetMode
            : CardTargetMode.None;
    }

    public ManaCost GetManaCost()
    {
        return calculatedManaCost;
    }

    public string GetCardName()
    {
        return calculatedCardName ?? string.Empty;
    }

    public string GetRulesText()
    {
        return calculatedRulesText ?? string.Empty;
    }

    public CardType GetCardTypes()
    {
        return calculatedCardTypes;
    }

    public CardSupertype GetSupertypes()
    {
        return calculatedSupertypes;
    }

    public string GetSubtypes()
    {
        return calculatedSubtypes ?? string.Empty;
    }

    public ManaColorFlags GetColors()
    {
        return calculatedColors;
    }

    public void MarkAdventureResolved()
    {
        CanCastPrimaryFromAdventureExile = true;
    }

    public void ConsumeAdventurePermission()
    {
        CanCastPrimaryFromAdventureExile = false;
    }

    public void MarkPrepared(bool prepared)
    {
        IsPrepared = prepared;
        if (!prepared)
        {
            PreparedCopy = null;
        }
    }

    public void MarkAsPreparedCopy(RuntimeCard source)
    {
        IsSpellCopy = true;
        PreparedSource = source;
        if (source != null)
        {
            source.IsPrepared = true;
            source.PreparedCopy = this;
        }
    }

    public void ClearPreparedLink()
    {
        RuntimeCard source = PreparedSource;
        RuntimeCard copy = PreparedCopy;
        PreparedSource = null;
        PreparedCopy = null;
        IsPrepared = false;

        if (source != null && ReferenceEquals(source.PreparedCopy, this))
        {
            source.PreparedCopy = null;
            source.IsPrepared = false;
        }

        if (copy != null && ReferenceEquals(copy.PreparedSource, this))
        {
            copy.PreparedSource = null;
        }
    }

    public bool UnlockRoomFeature(CardFeatureRuntime feature)
    {
        return feature != null
            && IsRoomDoorFeature(feature)
            && unlockedRoomFeatures.Add(feature.FeatureIndex);
    }

    public bool IsRoomFeatureUnlocked(CardFeatureRuntime feature)
    {
        return feature != null
            && IsRoomDoorFeature(feature)
            && unlockedRoomFeatures.Contains(feature.FeatureIndex);
    }

    public bool IsFeatureActive(CardFeatureRuntime feature)
    {
        if (feature == null) return false;
        switch (feature.Role)
        {
            case CardFeatureRole.RoomDoor:
                return IsRoomFeatureUnlocked(feature);
            case CardFeatureRole.ClassLevel:
                return feature.ClassLevel > 0 && ClassLevel >= feature.ClassLevel;
            case CardFeatureRole.CaseStage:
                return IsSolvedCaseStage(feature)
                    ? IsCaseSolved
                    : !IsCaseSolved;
            case CardFeatureRole.Prepare:
                return IsPrepared;
            default:
                return feature.IsPrimary
                    && (!HasFeatureRole(CardFeatureRole.RoomDoor)
                        || IsRoomFeatureUnlocked(feature));
        }
    }

    private bool IsRoomDoorFeature(CardFeatureRuntime feature)
    {
        return feature != null
            && (feature.Role == CardFeatureRole.RoomDoor
                || (feature.IsPrimary
                    && HasFeatureRole(CardFeatureRole.RoomDoor)
                    && feature.HasSubtype("Room", "\u623f\u95f4")));
    }

    public bool IsSolvedCaseStage(CardFeatureRuntime feature)
    {
        if (feature == null || feature.Role != CardFeatureRole.CaseStage) return false;
        if (feature.IsCaseStageNamed("\u5df2\u89e3\u660e", "Solved", "Solved Case")) return true;
        if (feature.IsCaseStageNamed("\u89e3\u660e\u6761\u4ef6", "To Solve", "Solve Condition")) return false;

        for (int i = 0; i < features.Count; i++)
        {
            CardFeatureRuntime candidate = features[i];
            if (candidate == null || candidate.Role != CardFeatureRole.CaseStage) continue;
            return !ReferenceEquals(candidate, feature);
        }

        return false;
    }

    public void ClearRoomUnlocks()
    {
        unlockedRoomFeatures.Clear();
    }

    public bool SetClassLevel(int level)
    {
        if (level <= ClassLevel) return false;
        ClassLevel = level;
        return true;
    }

    public void SolveCase()
    {
        IsCaseSolved = true;
    }

    public void SetResolvingAbilityContext(
        CardFeatureRuntime feature,
        CardRuleEntryDefinition ruleEntry,
        int chapter)
    {
        ResolvingFeature = feature;
        ResolvingRuleEntry = ruleEntry;
        ResolvingChapter = chapter;
    }

    public void ClearResolvingAbilityContext()
    {
        ResolvingFeature = null;
        ResolvingRuleEntry = null;
        ResolvingChapter = 0;
    }

    public void ResetPermanentDesignations()
    {
        ClearRoomUnlocks();
        ClassLevel = 1;
        IsCaseSolved = false;
        ClearPreparedLink();
        ClearResolvingAbilityContext();
        BattleProtectorPlayerID = 0;
    }

    public void EnsureScriptInstance()
    {
        if (ScriptInstance != null) return;
        if (Data == null || string.IsNullOrEmpty(Data.luaScript)) return;

        var manager = CardScriptRuntimeManager.EnsureExists();
        if (manager == null) return;

        IsInitializingScript = true;
        try { ScriptInstance = manager.CreateCardInstance(this, Data.luaScript); }
        finally { IsInitializingScript = false; }
    }

    public void OnDestroy()
    {
        if (ScriptInstance != null)
        {
            ScriptInstance.Dispose();
            ScriptInstance = null;
        }
    }

    public void Tap()
    {
        if (!IsTapped)
        {
            IsTapped = true;
            GameEvents.TriggerCardTapped(this);
        }
    }

    public void Untap()
    {
        if (IsTapped)
        {
            IsTapped = false;
            GameEvents.TriggerCardUntapped(this);
        }
    }
    
    public void TakeDamage(int damage)
    {
        if (damage <= 0) return;

        DamageTaken += damage;
        GameEvents.TriggerDamageDealt(this, damage);
    }

    public void MarkDeathtouchDamage()
    {
        hasDeathtouchDamageSinceStateCheck = true;
    }

    public void Die()
    {
        if (IsDead) return;
        IsDead = true;
        if (Data != null && HasType(CardType.Creature))
        {
            GameEvents.TriggerCreatureDied(this);
        }
    }

    public void ClearDamage()
    {
        DamageTaken = 0;
    }

    public void ClearDeathtouchDamageState()
    {
        hasDeathtouchDamageSinceStateCheck = false;
    }

    private bool TryUseRegenerationShield()
    {
        if (regenerationShields <= 0) return false;

        regenerationShields--;
        ClearDamage();
        ClearDeathtouchDamageState();
        Tap();
        RemoveFromCombat();
        GameEvents.TriggerCreatureRegenerated(this);
        return true;
    }

    private static void AddPrevention(ref int remaining, int amount)
    {
        if (remaining == PreventAllDamage) return;
        if (amount <= 0)
        {
            remaining = PreventAllDamage;
            return;
        }

        remaining += amount;
    }

    private static int PreventDamage(ref int remaining, int damage)
    {
        if (damage <= 0 || remaining == 0) return 0;
        if (remaining == PreventAllDamage) return damage;

        int prevented = Mathf.Min(remaining, damage);
        remaining -= prevented;
        return prevented;
    }

    // Combat State Management
    public void DeclareAttack(object target)
    {
        IsAttacking = true;
        AttackTarget = target;
        // Tapping logic is handled by GameRules based on Vigilance
    }

    public void DeclareBlock(RuntimeCard targetAttacker)
    {
        IsBlocking = true;
        BlockingTarget = targetAttacker;
        targetAttacker.IsBlocked = true;
        targetAttacker.WasBlockedThisCombat = true;
        targetAttacker.Blockers.Add(this);
    }

    public void RemoveFromCombat()
    {
        if (IsBlocking && BlockingTarget != null)
        {
            RuntimeCard target = BlockingTarget;
            target.Blockers.Remove(this);
            if (target.Blockers.Count == 0)
            {
                target.IsBlocked = false;
            }
        }

        if (IsAttacking && Blockers.Count > 0)
        {
            foreach (var blocker in Blockers)
            {
                blocker.IsBlocking = false;
                blocker.BlockingTarget = null;
            }
        }

        IsAttacking = false;
        IsBlocked = false;
        WasBlockedThisCombat = false;
        IsBlocking = false;
        AttackTarget = null;
        BlockingTarget = null;
        Blockers.Clear();
    }

    public bool HasActivatableAbility(GameRules rules)
    {
        foreach (var a in Abilities)
        {
            if (a.IsActivatable(rules, this)) return true;
        }
        return false;
    }

    public ActivatedAbility GetFirstActivatableAbility(GameRules rules)
    {
        foreach (var a in Abilities)
        {
            if (a.IsActivatable(rules, this)) return a;
        }
        return null;
    }

    public bool HasActivatableManaAbility(GameRules rules)
    {
        foreach (var a in Abilities)
        {
            if (a.IsManaAbility && a.IsActivatable(rules, this)) return true;
        }
        return false;
    }

    public ActivatedAbility GetFirstActivatableManaAbility(GameRules rules)
    {
        foreach (var a in Abilities)
        {
            if (a.IsManaAbility && a.IsActivatable(rules, this)) return a;
        }
        return null;
    }
}
