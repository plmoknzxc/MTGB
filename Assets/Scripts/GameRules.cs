using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

public partial class GameRules : MonoBehaviour
{
    private const int PreventAllDamage = -1;
    private const string BattleDefeatedHook = "OnBattleDefeated";
    private const int CastDefeatedBattleOptionId = 4200000;
    private const int KeepOpeningHandOptionId = 1;
    private const int MulliganOpeningHandOptionId = 2;

    public int startingLife = 20; 
    public int startingHandSize = 7; 
    public int maxHandSize = 7; 

    [Header("Development")]
    [SerializeField] private bool usePrototypeSetupWhenNoDeckConfigured = true;
    [SerializeField] private bool seedPrototypeOpponentBattlefield;
    [SerializeField] private int deterministicSeed = 19780522;

    [System.NonSerialized] public List<CardDefinition> player1Deck; 
    [System.NonSerialized] public List<CardDefinition> player2Deck; 

    // Runtime State using RuntimeCard wrapper
    private List<RuntimeCard> player1Hand = new List<RuntimeCard>(); 
    private List<RuntimeCard> player2Hand = new List<RuntimeCard>(); 

    private List<RuntimeCard> player1Battlefield = new List<RuntimeCard>(); 
    private List<RuntimeCard> player2Battlefield = new List<RuntimeCard>(); 
    
    private List<RuntimeCard> player1Graveyard = new List<RuntimeCard>();
    private List<RuntimeCard> player2Graveyard = new List<RuntimeCard>();
    private List<RuntimeCard> player1Exile = new List<RuntimeCard>();
    private List<RuntimeCard> player2Exile = new List<RuntimeCard>();
    private readonly List<RuntimeCard> player1Command = new List<RuntimeCard>();
    private readonly List<RuntimeCard> player2Command = new List<RuntimeCard>();
    private readonly List<RuntimeCard> player1Library = new List<RuntimeCard>();
    private readonly List<RuntimeCard> player2Library = new List<RuntimeCard>();
    private readonly List<GameStackItem> stack = new List<GameStackItem>();
    private readonly List<RuntimeCard> stackCardView = new List<RuntimeCard>();
    private readonly List<GameStackItem> pendingTriggeredAbilities = new List<GameStackItem>();
    private readonly List<GameStackItem> pendingTriggerOrderItems = new List<GameStackItem>();
    private readonly List<GameStackItem> deferredTriggerOrderItems = new List<GameStackItem>();
    private readonly List<RuntimeCard> pendingAttackers = new List<RuntimeCard>();
    private readonly Dictionary<RuntimeCard, object> pendingAttackTargets =
        new Dictionary<RuntimeCard, object>();
    private readonly List<RuntimeCard> pendingLegendRuleCards = new List<RuntimeCard>();
    private readonly Dictionary<RuntimeCard, ManaPool> unspentSourceMana = new Dictionary<RuntimeCard, ManaPool>();
    private readonly List<DamageReplacementRuntime> damageReplacements = new List<DamageReplacementRuntime>();
    private readonly GameEventPipeline eventPipeline = new GameEventPipeline();
    private readonly GameReplay replay = new GameReplay();
    private readonly List<GameEventRecord> currentActionEvents = new List<GameEventRecord>();
    private readonly HashSet<long> recordedActionEventIds = new HashSet<long>();
    private DeterministicRandom deterministicRandom;
    private bool applyingGameAction;
    private long nextActionSequence = 1;
    private long nextObjectId = 1;
    private long nextStackObjectId = 1;
    private long nextDamageReplacementId = 1;
    private long nextDamageReplacementTimestamp = 1;

    private int player1Life;
    private int player2Life;
    private int player1PoisonCounters;
    private int player2PoisonCounters;
    private int player1DamagePreventionRemaining;
    private int player2DamagePreventionRemaining;
    private int player1MaxHandSize;
    private int player2MaxHandSize;
    private bool player1AttemptedDrawFromEmptyLibrary;
    private bool player2AttemptedDrawFromEmptyLibrary;
    private MatchOutcome matchOutcome;
    private GameChoiceRequest activeChoice;
    private int nextChoiceRequestId = 1;
    private System.Action<GameChoiceResult> pendingGenericChoiceCompletion;
    private System.Action pendingGenericChoiceCancellation;
    private System.Func<GameChoiceRequest, bool> pendingGenericChoiceValidator;
    private GameChoiceResult lastCompletedChoice;
    private System.Action pendingStackResolutionCompletion;
    private SpellCastDeclaration pendingCast;
    private ScriptAbilityPayment pendingCastPayment;

    // Mana System
    private ManaPool player1ManaPool;
    private ManaPool player2ManaPool;
    private int landsPlayedThisTurn;
    private const int maxLandsPerTurn = 1;

    private int currentPlayer = 1; 
    private TurnPhase currentPhase = TurnPhase.Untap; 
    private int turnNumber = 1;
    private long nextBattlefieldEntrySequence;
    private readonly List<RuntimeCard> emptyCards = new List<RuntimeCard>(0);
    private bool initialized;
    private bool player1FullControl;
    private bool player2FullControl;
    private readonly PriorityState priorityState = new PriorityState();
    private bool resolvingAutomaticPriority;
    private bool combatAnimationInProgress;
    private RuntimeCard pendingLibrarySearchSource;
    private bool firstCombatDamageStepSnapshotActive;
    private readonly HashSet<RuntimeCard> firstCombatDamageStepSpecialStrikers = new HashSet<RuntimeCard>();
    private bool processingStateBasedActions;
    private bool stateBasedActionRecheckRequested;
    private int startingPlayer = 1;
    private bool hasReplaySeedOverride;
    private int replaySeedOverride;
    private int replayStartingPlayerOverride;
    private bool openingMulligan;
    private readonly int[] openingMulliganCounts = new int[3];
    private readonly bool[] openingMulliganKept = new bool[3];
    private readonly bool[] openingMulliganDecidedThisRound = new bool[3];
    private readonly bool[] openingMulliganBottomed = new bool[3];

    private struct CombatDamageAssignment
    {
        public RuntimeCard Source;
        public RuntimeCard CardTarget;
        public int PlayerTarget;
        public int Damage;

        public CombatDamageAssignment(RuntimeCard source, RuntimeCard cardTarget, int playerTarget, int damage)
        {
            Source = source;
            CardTarget = cardTarget;
            PlayerTarget = playerTarget;
            Damage = damage;
        }
    }

    private sealed class PreparedCombatDamage
    {
        public CombatDamageAssignment Assignment;
        public GameEventContext Event;
    }

    private enum ReplacementCandidateKind
    {
        Script = 0,
        RegisteredDamage = 1,
        ProtectionPrevention = 2,
        SourcePrevention = 3,
        TargetPrevention = 4
    }

    private sealed class ReplacementCandidate
    {
        public ReplacementCandidateKind Kind;
        public ScriptReplacementRegistration Script;
        public DamageReplacementRuntime Damage;
        public long Timestamp;
        public string Label;
    }

    private sealed class ScriptAbilityPayment
    {
        public readonly List<RuntimeCard> DiscardCards = new List<RuntimeCard>();
        public readonly List<RuntimeCard> SacrificePermanents = new List<RuntimeCard>();
        public readonly List<RuntimeCard> ExileHandCards = new List<RuntimeCard>();
        public readonly List<RuntimeCard> RevealHandCards = new List<RuntimeCard>();
    }

    private sealed class PreparedZoneMove
    {
        public RuntimeCard Card;
        public GameZone FromZone;
        public GameZone Destination;
        public bool WasOnBattlefield;
        public int EnteringController;
        public bool ToTop;
        public GameEventContext Event;
    }

    private sealed class PreparedCostPayment
    {
        public readonly List<PreparedZoneMove> ZoneMoves =
            new List<PreparedZoneMove>();
        public readonly HashSet<RuntimeCard> SacrificedCards =
            new HashSet<RuntimeCard>();
    }

    void Start()
    {
        InitializeGame();
    }

    private void Update()
    {
        // Cleanup can be reached after an automatic priority pass; keep the mandatory
        // hand-size check in the rules engine rather than relying on a UI button click.
        if (initialized
            && !IsGameOver()
            && currentPhase == TurnPhase.Cleanup
            && !HasActiveChoice()
            && GetHandCount(currentPlayer) > GetMaxHandSize(currentPlayer))
        {
            BeginDiscardToHandSize(currentPlayer);
        }
    }

    private void InitializeGame()
    {
        EnsureCardScriptRuntime();

        if (hasReplaySeedOverride)
        {
            deterministicSeed = replaySeedOverride;
            hasReplaySeedOverride = false;
        }
        else if (MatchStartConfig.TryGetDeterministicSeed(out int configuredSeed))
        {
            deterministicSeed = configuredSeed;
        }

        initialized = true;
        GameEvents.OnCardPlayed -= HandleCardPlayedLua;
        GameEvents.OnCreatureDied -= HandleCreatureDiedLua;
        GameEvents.OnCardTapped -= HandleCardTappedEvent;
        GameEvents.OnCardUntapped -= HandleCardUntappedEvent;
        GameEvents.OnPhaseChanged -= HandlePhaseChangedEvent;
        GameEvents.OnAttackDeclared -= HandleAttackDeclaredEvent;
        GameEvents.OnBlockDeclared -= HandleBlockDeclaredEvent;

        int matchStartingLife = startingLife;
        int matchFirstPlayer = replayStartingPlayerOverride;
        replayStartingPlayerOverride = 0;
        if ((matchFirstPlayer != 1 && matchFirstPlayer != 2)
            && MatchStartConfig.TryGetOnlineRules(out int configuredLife, out int configuredFirstPlayer))
        {
            matchStartingLife = configuredLife;
            matchFirstPlayer = configuredFirstPlayer;
        }
        player1Life = matchStartingLife;
        player2Life = matchStartingLife;
        player1PoisonCounters = 0;
        player2PoisonCounters = 0;
        player1DamagePreventionRemaining = 0;
        player2DamagePreventionRemaining = 0;
        player1MaxHandSize = maxHandSize;
        player2MaxHandSize = maxHandSize;
        player1AttemptedDrawFromEmptyLibrary = false;
        player2AttemptedDrawFromEmptyLibrary = false;
        matchOutcome = MatchOutcome.InProgress();
        player1ManaPool.Clear();
        player2ManaPool.Clear();
        player1Hand.Clear();
        player2Hand.Clear();
        player1Battlefield.Clear();
        player2Battlefield.Clear();
        player1Graveyard.Clear();
        player2Graveyard.Clear();
        player1Exile.Clear();
        player2Exile.Clear();
        player1Command.Clear();
        player2Command.Clear();
        player1Library.Clear();
        player2Library.Clear();
        stack.Clear();
        stackCardView.Clear();
        pendingTriggeredAbilities.Clear();
        pendingTriggerOrderItems.Clear();
        deferredTriggerOrderItems.Clear();
        pendingAttackers.Clear();
        pendingAttackTargets.Clear();
        pendingLegendRuleCards.Clear();
        unspentSourceMana.Clear();
        eventPipeline.Clear();
        deterministicRandom = new DeterministicRandom(deterministicSeed);
        replay.version = GameReplay.CurrentVersion;
        replay.randomSeed = deterministicSeed;
        replay.initialStateHash = string.Empty;
        replay.actions.Clear();
        replay.stateHashes.Clear();
        replay.eventBatches.Clear();
        currentActionEvents.Clear();
        recordedActionEventIds.Clear();
        applyingGameAction = false;
        nextActionSequence = 1;
        nextObjectId = 1;
        nextStackObjectId = 1;
        currentPlayer = 1;
        currentPhase = TurnPhase.Untap;
        turnNumber = 1;
        nextBattlefieldEntrySequence = 0;
        landsPlayedThisTurn = 0;
        priorityState.Reset();
        activeChoice = null;
        pendingGenericChoiceCompletion = null;
        pendingGenericChoiceCancellation = null;
        pendingGenericChoiceValidator = null;
        lastCompletedChoice = null;
        pendingCast = null;
        pendingCastPayment = null;
        nextChoiceRequestId = 1;
        combatAnimationInProgress = false;
        ClearPendingLibrarySearch();
        ClearCombatDamageStepSnapshot();
        player1FullControl = false;
        player2FullControl = false;
        startingPlayer = 1;
        openingMulligan = false;
        System.Array.Clear(openingMulliganCounts, 0, openingMulliganCounts.Length);
        System.Array.Clear(openingMulliganKept, 0, openingMulliganKept.Length);
        System.Array.Clear(openingMulliganDecidedThisRound, 0, openingMulliganDecidedThisRound.Length);
        System.Array.Clear(openingMulliganBottomed, 0, openingMulliganBottomed.Length);

        if (player1Deck == null) player1Deck = new List<CardDefinition>();
        if (player2Deck == null) player2Deck = new List<CardDefinition>();
        bool configuredDecks = MatchStartConfig.ApplyTo(player1Deck, player2Deck, out string deckWarning);
        if (!string.IsNullOrEmpty(deckWarning))
        {
            Debug.LogWarning(deckWarning);
        }
        bool usingPrototypeSetup = !configuredDecks && usePrototypeSetupWhenNoDeckConfigured;
        if (usingPrototypeSetup)
        {
            PrototypeMatchSetup.PrepareDecks(player1Deck, player2Deck);
        }
        else if (!configuredDecks)
        {
            Debug.LogError("No match decks were configured and prototype setup is disabled.");
        }
        if (matchFirstPlayer != 1 && matchFirstPlayer != 2)
        {
            matchFirstPlayer = configuredDecks
                ? ChooseStartingPlayer(deterministicSeed)
                : 1;
        }
        startingPlayer = matchFirstPlayer;
        currentPlayer = matchFirstPlayer;
        priorityState.Reset(matchFirstPlayer);
        replay.startingPlayer = matchFirstPlayer;

        // Register Lua listeners for game phases
        GameEvents.OnCardPlayed += HandleCardPlayedLua;
        GameEvents.OnCreatureDied += HandleCreatureDiedLua;
        GameEvents.OnCardTapped += HandleCardTappedEvent;
        GameEvents.OnCardUntapped += HandleCardUntappedEvent;
        GameEvents.OnPhaseChanged += HandlePhaseChangedEvent;
        GameEvents.OnAttackDeclared += HandleAttackDeclaredEvent;
        GameEvents.OnBlockDeclared += HandleBlockDeclaredEvent;

        var player1StartingDeck = new List<CardDefinition>(player1Deck);
        var player2StartingDeck = new List<CardDefinition>(player2Deck);
        ShuffleDeck(player1StartingDeck);
        ShuffleDeck(player2StartingDeck);
        if (usingPrototypeSetup)
        {
            PrototypeMatchSetup.MoveKnownCardsToTop(player1StartingDeck);
        }

        BuildRuntimeLibrary(player1StartingDeck, player1Library, 1);
        BuildRuntimeLibrary(player2StartingDeck, player2Library, 2);
        DrawCards(player1Library, player1Hand, startingHandSize, 1);
        DrawCards(player2Library, player2Hand, startingHandSize, 2);
        if (usingPrototypeSetup && seedPrototypeOpponentBattlefield)
        {
            PrototypeMatchSetup.SeedOpponentStartingBattlefield(player2Battlefield);
            for (int i = 0; i < player2Battlefield.Count; i++)
            {
                RegisterRuntimeCard(player2Battlefield[i], GameZone.Battlefield);
                MarkCardEnteredBattlefield(player2Battlefield[i]);
                InitializeStructuredCardOnBattlefield(player2Battlefield[i], null);
            }
        }

        if (configuredDecks) BeginOpeningMulligan();
        else BeginFirstTurnAfterMulligan();
        replay.initialStateHash = ComputeDeterministicStateHash();

        Debug.Log($"Game Initialized! P1 hand: {player1Hand.Count}, P2 hand: {player2Hand.Count}, current phase: P{currentPlayer} {currentPhase}.");
    }

    private static int ChooseStartingPlayer(int seed)
    {
        var random = new DeterministicRandom(unchecked(seed ^ 0x5EED1234));
        return random.Range(1, 3);
    }

    private void BeginOpeningMulligan()
    {
        openingMulligan = true;
        priorityState.Close();
        BeginOpeningMulliganRound();
    }

    private void BeginOpeningMulliganRound()
    {
        for (int player = 1; player <= 2; player++)
            openingMulliganDecidedThisRound[player] = openingMulliganKept[player];
        AdvanceOpeningMulliganDecision();
    }

    private void AdvanceOpeningMulliganDecision()
    {
        int secondPlayer = 3 - startingPlayer;
        int[] order = { startingPlayer, secondPlayer };
        for (int i = 0; i < order.Length; i++)
        {
            int player = order[i];
            if (openingMulliganKept[player] || openingMulliganDecidedThisRound[player]) continue;
            if (!IsOnlineMatch() && player == 2)
            {
                openingMulliganKept[player] = true;
                openingMulliganDecidedThisRound[player] = true;
                continue;
            }
            BeginOpeningMulliganDecision(player);
            return;
        }

        if (openingMulliganKept[1] && openingMulliganKept[2])
        {
            AdvanceOpeningMulliganBottoming();
            return;
        }
        BeginOpeningMulliganRound();
    }

    private void BeginOpeningMulliganDecision(int player)
    {
        int maximumMulligans = Mathf.Min(startingHandSize, GetHandCount(player));
        bool mayMulligan = openingMulliganCounts[player] < maximumMulligans;
        var options = new List<GameChoiceOption>
        {
            new GameChoiceOption(
                KeepOpeningHandOptionId,
                GameChoiceZone.None,
                player,
                -1,
                true,
                "保留这手牌")
        };
        if (mayMulligan)
        {
            options.Add(new GameChoiceOption(
                MulliganOpeningHandOptionId,
                GameChoiceZone.None,
                player,
                -1,
                true,
                $"重新调度（已调度 {openingMulliganCounts[player]} 次）"));
        }

        BeginOptionChoice(
            GameChoiceKind.MulliganDecision,
            player,
            BuildOpeningMulliganPrompt(player),
            1,
            1,
            false,
            options,
            result => CompleteOpeningMulliganDecision(player, result));
    }

    private void CompleteOpeningMulliganDecision(int player, GameChoiceResult result)
    {
        int option = result?.selectedOptionIds?.Count == 1
            ? result.selectedOptionIds[0]
            : 0;
        if (option == MulliganOpeningHandOptionId
            && openingMulliganCounts[player] < Mathf.Min(startingHandSize, GetHandCount(player)))
        {
            if (!RedrawOpeningHand(player))
                throw new System.InvalidOperationException("Could not redraw an opening hand.");
            openingMulliganCounts[player]++;
            if (openingMulliganCounts[player] >= Mathf.Min(startingHandSize, GetHandCount(player)))
                openingMulliganKept[player] = true;
        }
        else
        {
            openingMulliganKept[player] = true;
        }
        openingMulliganDecidedThisRound[player] = true;
        AdvanceOpeningMulliganDecision();
    }

    private bool RedrawOpeningHand(int player)
    {
        List<RuntimeCard> hand = GetHandList(player);
        List<RuntimeCard> library = GetLibraryList(player);
        if (hand == null || library == null) return false;
        var returning = new List<RuntimeCard>(hand);
        if (returning.Count > 0
            && !MoveCardsToZone(returning, GameZone.Library, false, false)) return false;
        ShuffleDeck(library);
        DrawCards(library, hand, startingHandSize, player);
        return hand.Count == Mathf.Min(startingHandSize, hand.Count + library.Count);
    }

    private void AdvanceOpeningMulliganBottoming()
    {
        int[] order = { startingPlayer, 3 - startingPlayer };
        for (int i = 0; i < order.Length; i++)
        {
            int player = order[i];
            int count = openingMulliganCounts[player];
            if (openingMulliganBottomed[player]) continue;
            if (count <= 0)
            {
                openingMulliganBottomed[player] = true;
                continue;
            }
            if (!IsOnlineMatch() && player == 2)
            {
                List<RuntimeCard> hand = GetHandList(player);
                if (hand == null || hand.Count < count)
                    throw new System.InvalidOperationException("The opponent does not have enough cards to finish mulliganing.");
                List<RuntimeCard> automaticCards = hand.GetRange(0, count);
                if (!PutOpeningCardsOnBottom(player, automaticCards))
                    throw new System.InvalidOperationException("Could not bottom the opponent's opening cards.");
                openingMulliganBottomed[player] = true;
                continue;
            }
            BeginOpeningMulliganBottomChoice(player, count);
            return;
        }
        CompleteOpeningMulligan();
    }

    private void BeginOpeningMulliganBottomChoice(int player, int count)
    {
        List<RuntimeCard> hand = GetHandList(player);
        var snapshot = new List<RuntimeCard>(hand);
        var options = new List<GameChoiceOption>(snapshot.Count);
        for (int i = 0; i < snapshot.Count; i++)
        {
            RuntimeCard card = snapshot[i];
            options.Add(new GameChoiceOption(
                i,
                GameChoiceZone.Hand,
                player,
                i,
                true,
                card?.GetCardName() ?? "牌",
                objectId: card?.ObjectId ?? 0,
                objectZoneChangeVersion: card?.ZoneChangeVersion ?? -1));
        }
        BeginOptionChoice(
            GameChoiceKind.MulliganBottom,
            player,
            $"选择 {count} 张牌，按点击顺序置于牌库底。",
            count,
            count,
            false,
            options,
            result =>
            {
                var selected = new List<RuntimeCard>(count);
                if (result?.selectedOptionIds != null)
                {
                    for (int i = 0; i < result.selectedOptionIds.Count; i++)
                    {
                        int index = result.selectedOptionIds[i];
                        if (index >= 0 && index < snapshot.Count) selected.Add(snapshot[index]);
                    }
                }
                if (selected.Count != count || !PutOpeningCardsOnBottom(player, selected))
                    throw new System.InvalidOperationException("Could not put the selected opening cards on the bottom.");
                openingMulliganBottomed[player] = true;
                AdvanceOpeningMulliganBottoming();
            });
    }

    private bool PutOpeningCardsOnBottom(int player, IReadOnlyList<RuntimeCard> cards)
    {
        if (cards == null || cards.Count == 0) return false;
        for (int i = 0; i < cards.Count; i++)
        {
            if (cards[i] == null || cards[i].OwnerPlayerID != player
                || cards[i].CurrentZone != GameZone.Hand) return false;
        }
        return MoveCardsToZone(cards, GameZone.Library, false, false);
    }

    private void CompleteOpeningMulligan()
    {
        openingMulligan = false;
        BeginFirstTurnAfterMulligan();
        Debug.Log($"Player {startingPlayer} plays first; Player {3 - startingPlayer} draws second.");
    }

    private void BeginFirstTurnAfterMulligan()
    {
        SetCurrentPhase(TurnPhase.Main1);
        MainPhase();
        BeginPriority(currentPlayer);
    }

    private string BuildOpeningMulliganPrompt(int player)
    {
        string order = startingPlayer == player
            ? $"你是先手；玩家 {3 - startingPlayer} 是后手。"
            : $"你是后手；玩家 {startingPlayer} 是先手。";
        return $"{order} 查看起手牌并选择保留或重新调度。";
    }

    private static void EnsureCardScriptRuntime()
    {
        CardScriptRuntimeManager.EnsureExists();
    }

    private void HandleCardPlayedLua(RuntimeCard card)
    {
        if (card == null) return;

        var pendingTriggers = new List<GameStackItem>();
        if (card.GetCardTypes().IsInstantOrSorcery())
        {
            CardEffectExecutor.ExecuteOnPlayEffect(this, card);
        }
        else if (CardEffectExecutor.HasOnPlayEffect(card))
        {
            pendingTriggers.Add(GameStackItem.TriggeredAbility(card, "OnPlay"));
        }

        CollectBattlefieldTriggers("OnOtherCardPlayed", card, pendingTriggers);
        CollectPendingTriggeredAbilities(pendingTriggers);
    }
    
    private void HandleCreatureDiedLua(RuntimeCard card)
    {
        if (card == null) return;

        var pendingTriggers = new List<GameStackItem>();
        if (CardEffectExecutor.HasCardScriptHook(card, "OnDie"))
        {
            pendingTriggers.Add(GameStackItem.TriggeredAbility(card, "OnDie"));
        }

        CollectBattlefieldTriggers("OnOtherCreatureDied", card, pendingTriggers);
        CollectPendingTriggeredAbilities(pendingTriggers);
    }

    private void HandleCardTappedEvent(RuntimeCard card)
    {
        PublishGameEvent(new GameEventContext
        {
            type = GameEventType.CardTapped,
            source = card,
            card = card,
            playerId = card?.ControllerPlayerID ?? 0,
            controllerId = card?.ControllerPlayerID ?? 0
        });
    }

    private void HandleCardUntappedEvent(RuntimeCard card)
    {
        PublishGameEvent(new GameEventContext
        {
            type = GameEventType.CardUntapped,
            source = card,
            card = card,
            playerId = card?.ControllerPlayerID ?? 0,
            controllerId = card?.ControllerPlayerID ?? 0
        });
    }

    private void HandlePhaseChangedEvent(TurnPhase phase)
    {
        PublishGameEvent(new GameEventContext
        {
            type = GameEventType.PhaseChanged,
            playerId = currentPlayer,
            controllerId = currentPlayer,
            amount = (int)phase
        });
    }

    private void HandleAttackDeclaredEvent(RuntimeCard attacker, object target)
    {
        PublishGameEvent(new GameEventContext
        {
            type = GameEventType.AttackDeclared,
            source = attacker,
            card = attacker,
            playerId = target is int player ? player : 0,
            controllerId = attacker?.ControllerPlayerID ?? 0
        });
    }

    private void HandleBlockDeclaredEvent(RuntimeCard blocker, RuntimeCard attacker)
    {
        PublishGameEvent(new GameEventContext
        {
            type = GameEventType.BlockDeclared,
            source = blocker,
            card = attacker,
            playerId = blocker?.ControllerPlayerID ?? 0,
            controllerId = blocker?.ControllerPlayerID ?? 0
        });
    }

    private void CollectBattlefieldTriggers(
        string hookName,
        RuntimeCard excludedCard,
        List<GameStackItem> destination)
    {
        var sources = new List<RuntimeCard>();
        CardEffectExecutor.CollectHookSources(player1Battlefield, hookName, excludedCard, sources);
        CardEffectExecutor.CollectHookSources(player2Battlefield, hookName, excludedCard, sources);
        for (int i = 0; i < sources.Count; i++)
        {
            destination.Add(GameStackItem.TriggeredAbility(sources[i], hookName));
        }
    }

    private void CollectPendingTriggeredAbilities(List<GameStackItem> pendingTriggers)
    {
        if (pendingTriggers == null || pendingTriggers.Count == 0) return;

        pendingTriggeredAbilities.AddRange(pendingTriggers);
    }

    private bool PutPendingTriggeredAbilitiesOnStack()
    {
        if (activeChoice != null || pendingTriggeredAbilities.Count == 0) return false;

        int nonActivePlayer = currentPlayer == 1 ? 2 : 1;
        List<GameStackItem> activePlayerTriggers = GetControlledTriggers(pendingTriggeredAbilities, currentPlayer);
        List<GameStackItem> nonActivePlayerTriggers = GetControlledTriggers(pendingTriggeredAbilities, nonActivePlayer);
        pendingTriggeredAbilities.Clear();

        if ((IsOnlineMatch() || currentPlayer == 1)
            && activePlayerTriggers.Count > 1)
        {
            BeginTriggerOrderChoice(
                activePlayerTriggers,
                nonActivePlayerTriggers,
                currentPlayer);
            return true;
        }

        PushControlledTriggers(activePlayerTriggers, currentPlayer);

        if ((IsOnlineMatch() || nonActivePlayer == 1)
            && nonActivePlayerTriggers.Count > 1)
        {
            BeginTriggerOrderChoice(
                nonActivePlayerTriggers,
                null,
                nonActivePlayer);
            NotifyStackChanged();
            return true;
        }

        PushControlledTriggers(nonActivePlayerTriggers, nonActivePlayer);
        NotifyStackChanged();
        return true;
    }

    private static List<GameStackItem> GetControlledTriggers(
        List<GameStackItem> pendingTriggers,
        int controllerPlayerId)
    {
        var result = new List<GameStackItem>();
        for (int i = 0; i < pendingTriggers.Count; i++)
        {
            GameStackItem item = pendingTriggers[i];
            if (item != null && item.ControllerPlayerId == controllerPlayerId)
            {
                result.Add(item);
            }
        }

        return result;
    }

    private void PushControlledTriggers(List<GameStackItem> pendingTriggers, int controllerPlayerId)
    {
        for (int i = 0; i < pendingTriggers.Count; i++)
        {
            GameStackItem item = pendingTriggers[i];
            if (item == null || item.ControllerPlayerId != controllerPlayerId) continue;
            PushStackItem(item);
            Debug.Log($"Player {controllerPlayerId} puts {item.SourceCard?.Data?.cardName ?? "an ability"} [{item.HookName}] on the stack.");
        }
    }

    private void PushTriggerItemsInOrder(List<GameStackItem> triggers)
    {
        if (triggers == null) return;

        for (int i = 0; i < triggers.Count; i++)
        {
            GameStackItem item = triggers[i];
            if (item == null) continue;
            PushStackItem(item);
            Debug.Log(
                $"Player {item.ControllerPlayerId} puts {item.SourceCard?.Data?.cardName ?? "an ability"} [{item.HookName}] on the stack.");
        }
    }

    private void BeginTriggerOrderChoice(
        List<GameStackItem> triggersToOrder,
        List<GameStackItem> triggersToPushAfterChoice,
        int choosingPlayerId)
    {
        pendingTriggerOrderItems.Clear();
        deferredTriggerOrderItems.Clear();
        if (triggersToOrder != null) pendingTriggerOrderItems.AddRange(triggersToOrder);
        if (triggersToPushAfterChoice != null) deferredTriggerOrderItems.AddRange(triggersToPushAfterChoice);

        GameChoiceRequest request = BeginChoice(
            GameChoiceKind.TriggerOrder,
            choosingPlayerId,
            "Choose the order for your triggered abilities, from bottom to top of the stack.",
            pendingTriggerOrderItems.Count,
            pendingTriggerOrderItems.Count,
            false);

        for (int i = 0; i < pendingTriggerOrderItems.Count; i++)
        {
            GameStackItem item = pendingTriggerOrderItems[i];
            string sourceName = item?.SourceCard?.Data?.cardName ?? "Triggered ability";
            string hookName = item?.HookName ?? string.Empty;
            request.options.Add(new GameChoiceOption(
                3000000 + i,
                GameChoiceZone.Stack,
                choosingPlayerId,
                i,
                true,
                $"{sourceName} [{hookName}]"));
        }

        priorityState.Close();
        Debug.Log($"Player {choosingPlayerId} must order simultaneous triggered abilities.");
    }

    private void ShuffleDeck<T>(IList<T> deck)
    {
        deterministicRandom ??= new DeterministicRandom(deterministicSeed);
        deterministicRandom.Shuffle(deck);
    }

    private void BuildRuntimeLibrary(
        IReadOnlyList<CardDefinition> definitions,
        List<RuntimeCard> library,
        int ownerID)
    {
        library.Clear();
        if (definitions == null) return;
        for (int i = 0; i < definitions.Count; i++)
        {
            CardDefinition definition = definitions[i];
            if (definition == null) continue;
            RuntimeCard card = new RuntimeCard(definition, ownerID);
            RegisterRuntimeCard(card, GameZone.Library);
            library.Add(card);
        }
    }

    private void DrawCards(List<RuntimeCard> deck, List<RuntimeCard> hand, int count, int ownerID)
    {
        for (int i = 0; i < count; i++)
        {
            if (deck.Count > 0)
            {
                RuntimeCard newCard = deck[0];
                if (!MoveCardToZone(newCard, GameZone.Hand)) break;
            }
            else
            {
                if (ownerID == 1) player1AttemptedDrawFromEmptyLibrary = true;
                else if (ownerID == 2) player2AttemptedDrawFromEmptyLibrary = true;
                Debug.LogWarning($"Player {ownerID} attempted to draw from an empty library.");
                break;
            }
        }
    }

    public long GetAppliedActionSequence()
    {
        return nextActionSequence - 1;
    }

    public GameReplay GetReplay()
    {
        return replay;
    }

    public GameActionResult ApplyAction(GameAction action)
    {
        if (action == null) return GameActionResult.Rejected("Action is null.", 0);

        if (!onlineAuthoritySession
            && OnlineMatchService.TryRouteAction(this, action, out GameActionResult routedResult))
        {
            return routedResult;
        }

        long sequence = action.sequence <= 0 ? nextActionSequence : action.sequence;
        if (sequence != nextActionSequence)
        {
            return GameActionResult.Rejected(
                $"Expected action sequence {nextActionSequence}, received {sequence}.",
                sequence);
        }

        if (action.expectedStateSequence >= 0
            && action.expectedStateSequence != GetAppliedActionSequence())
        {
            return GameActionResult.Rejected(
                $"Expected state sequence {GetAppliedActionSequence()}, " +
                $"received {action.expectedStateSequence}.",
                sequence);
        }

        action.sequence = sequence;
        currentActionEvents.Clear();
        recordedActionEventIds.Clear();
        applyingGameAction = true;
        bool accepted;
        try
        {
            accepted = ApplyActionInternal(action);
        }
        finally
        {
            applyingGameAction = false;
        }
        if (!accepted)
        {
            currentActionEvents.Clear();
            recordedActionEventIds.Clear();
            return GameActionResult.Rejected(
                $"Illegal or unavailable action: {action.type}.",
                sequence);
        }

        ContinueAutomaticPriority();
        nextActionSequence++;
        string stateHash = ComputeDeterministicStateHash();
        replay.Record(action, stateHash, currentActionEvents);
        GameActionResult result = GameActionResult.Accepted(
            sequence,
            stateHash,
            currentActionEvents);
        currentActionEvents.Clear();
        recordedActionEventIds.Clear();
        return result;
    }

    public GameReplayResult ReplayFromInitialState(GameReplay sourceReplay)
    {
        if (sourceReplay == null)
        {
            return new GameReplayResult { error = "Replay is null." };
        }
        if (sourceReplay.version != GameReplay.CurrentVersion)
        {
            return new GameReplayResult
            {
                error = $"Unsupported replay version {sourceReplay.version}."
            };
        }

        GameReplay input = sourceReplay.Clone();
        replaySeedOverride = input.randomSeed;
        hasReplaySeedOverride = true;
        replayStartingPlayerOverride = input.startingPlayer;
        InitializeGame();
        string initialHash = ComputeDeterministicStateHash();
        if (!string.IsNullOrEmpty(input.initialStateHash)
            && input.initialStateHash != initialHash)
        {
            return new GameReplayResult
            {
                failedActionIndex = -1,
                error = "Initial state hash mismatch.",
                finalStateHash = initialHash
            };
        }

        for (int i = 0; i < input.actions.Count; i++)
        {
            GameAction action = input.actions[i]?.Clone();
            GameActionResult actionResult = ApplyAction(action);
            if (!actionResult.accepted)
            {
                return new GameReplayResult
                {
                    appliedActionCount = i,
                    failedActionIndex = i,
                    error = actionResult.error,
                    finalStateHash = ComputeDeterministicStateHash()
                };
            }

            if (i < input.stateHashes.Count
                && !string.IsNullOrEmpty(input.stateHashes[i])
                && input.stateHashes[i] != actionResult.stateHash)
            {
                return new GameReplayResult
                {
                    appliedActionCount = i + 1,
                    failedActionIndex = i,
                    error = $"State hash mismatch after action {i + 1}.",
                    finalStateHash = actionResult.stateHash
                };
            }
        }

        return new GameReplayResult
        {
            succeeded = true,
            appliedActionCount = input.actions.Count,
            finalStateHash = ComputeDeterministicStateHash()
        };
    }

    private bool ApplyActionInternal(GameAction action)
    {
        RuntimeCard source;
        RuntimeCard target;
        if (!IsActionAllowedDuringActiveChoice(action)) return false;

        switch (action.type)
        {
            case GameActionType.PassPriority:
                return PassPriority(action.playerId);
            case GameActionType.AdvancePhase:
                if (!CanPlayerAdvance(action.playerId)) return false;
                NextPhase();
                return true;
            case GameActionType.Concede:
                return Concede(action.playerId);
            case GameActionType.EndTurn:
                if ((!IsOnlineMatch() && action.playerId != 1)
                    || (IsOnlineMatch() && action.playerId != currentPlayer)
                    || HasActiveChoice()
                    || HasPendingStack()
                    || IsGameOver())
                {
                    return false;
                }
                EndTurn();
                return true;
            case GameActionType.SetFullControl:
                if (!IsOnlineMatch() && action.playerId != 1) return false;
                SetFullControl(action.playerId, action.value != 0);
                return true;
            case GameActionType.PlayHandCard:
                return PlayCard(action.playerId, action.zoneIndex);
            case GameActionType.BeginHandCast:
                return BeginHandCastAction(action.playerId, action.zoneIndex);
            case GameActionType.SelectCastFeature:
                return SelectCastFeatureChoice(
                    action.playerId,
                    action.featureIndex,
                    out _,
                    out _);
            case GameActionType.BeginExileCast:
                RuntimeCard exileSource = GetExileCard(
                    action.playerId,
                    action.zoneIndex);
                if (exileSource == null
                    || !CanCastExileCard(action.playerId, action.zoneIndex))
                {
                    return false;
                }
                return BeginPendingCast(
                    action.playerId,
                    GameChoiceZone.Exile,
                    action.zoneIndex,
                    GetExileCastFeature(exileSource));
            case GameActionType.SelectCardTarget:
                target = FindRuntimeCard(action.targetObjectId);
                return target != null && ToggleSpellTargetCard(action.playerId, target);
            case GameActionType.SelectPlayerTarget:
                return ToggleSpellTargetPlayer(action.playerId, action.targetPlayerId);
            case GameActionType.ConfirmCast:
                if (!MatchesActiveChoice(action.choiceRequestId)) return false;
                return TryConfirmActiveChoice(action.playerId);
            case GameActionType.CancelCast:
                if (!MatchesActiveChoice(action.choiceRequestId)) return false;
                return CancelCastChoice(action.playerId)
                    || CancelCastFeatureChoice(action.playerId);
            case GameActionType.ActivateAbility:
                source = FindRuntimeCard(action.sourceObjectId);
                return source != null
                    && source.ControllerPlayerID == action.playerId
                    && BeginActivateAbilityChoice(source, action.value != 0);
            case GameActionType.ToggleAttacker:
                return TogglePendingAttacker(action.playerId, action.zoneIndex);
            case GameActionType.SetAttackerTarget:
                source = FindRuntimeCard(action.sourceObjectId);
                target = FindRuntimeCard(action.targetObjectId);
                return source != null
                    && SetPendingAttackerTarget(
                        action.playerId,
                        source,
                        target,
                        action.targetPlayerId);
            case GameActionType.CommitAttackers:
                if (action.playerId != currentPlayer
                    || currentPhase != TurnPhase.DeclareAttackers
                    || HasPendingStack()
                    || !CanPlayerAdvance(action.playerId)) return false;
                if (pendingAttackers.Count > 0) return CommitPendingAttackers();
                NextPhase();
                return true;
            case GameActionType.DeclareBlocker:
                target = FindRuntimeCard(action.targetObjectId);
                return target != null
                    && DeclareBlocker(action.playerId, action.zoneIndex, target);
            case GameActionType.CancelBlocker:
                return CancelBlocker(action.playerId, action.zoneIndex);
            case GameActionType.ToggleChoiceOption:
                return TryToggleChoiceOption(
                    action.playerId,
                    action.choiceRequestId,
                    action.optionId);
            case GameActionType.SetChoiceNumber:
                return TrySetChoiceNumber(
                    action.playerId,
                    action.choiceRequestId,
                    action.value);
            case GameActionType.SetChoiceAllocation:
                return TrySetChoiceAllocation(
                    action.playerId,
                    action.choiceRequestId,
                    action.optionId,
                    action.value);
            case GameActionType.MoveChoiceOption:
                return TryMoveChoiceOption(
                    action.playerId,
                    action.choiceRequestId,
                    action.optionId,
                    action.destinationIndex);
            case GameActionType.ConfirmChoice:
                if (!MatchesActiveChoice(action.choiceRequestId)) return false;
                return TryConfirmActiveChoice(action.playerId);
            case GameActionType.CancelChoice:
                if (!MatchesActiveChoice(action.choiceRequestId)) return false;
                return TryCancelActiveChoice(action.playerId);
            case GameActionType.ToggleLibraryCard:
                return ToggleLibrarySearchCard(action.playerId, action.zoneIndex);
            case GameActionType.ConfirmLibrarySearch:
                return ConfirmLibrarySearch(action.playerId);
            case GameActionType.ToggleDiscard:
                return ToggleDiscardHandCardToMaxHandSize(
                    action.playerId,
                    action.zoneIndex);
            case GameActionType.ConfirmDiscard:
                return ConfirmDiscardToMaxHandSize(action.playerId);
            default:
                return false;
        }
    }

    private bool IsActionAllowedDuringActiveChoice(GameAction action)
    {
        if (activeChoice == null || action == null) return true;

        if (action.type == GameActionType.Concede
            || action.type == GameActionType.SetFullControl)
        {
            return true;
        }

        if (action.playerId != activeChoice.choosingPlayerId) return false;

        switch (activeChoice.kind)
        {
            case GameChoiceKind.SpellTarget:
                return action.type == GameActionType.SelectCardTarget
                    || action.type == GameActionType.SelectPlayerTarget
                    || action.type == GameActionType.ToggleChoiceOption
                    || action.type == GameActionType.ConfirmCast
                    || action.type == GameActionType.ConfirmChoice
                    || action.type == GameActionType.CancelCast
                    || action.type == GameActionType.CancelChoice;
            case GameChoiceKind.CastConfirmation:
                return action.type == GameActionType.ConfirmCast
                    || action.type == GameActionType.ConfirmChoice
                    || action.type == GameActionType.CancelCast
                    || action.type == GameActionType.CancelChoice;
            case GameChoiceKind.CastFeature:
                return action.type == GameActionType.SelectCastFeature
                    || action.type == GameActionType.ToggleChoiceOption
                    || action.type == GameActionType.ConfirmChoice
                    || action.type == GameActionType.CancelCast
                    || action.type == GameActionType.CancelChoice;
            case GameChoiceKind.LibrarySearch:
                return action.type == GameActionType.ToggleLibraryCard
                    || action.type == GameActionType.ConfirmLibrarySearch
                    || action.type == GameActionType.ToggleChoiceOption
                    || action.type == GameActionType.ConfirmChoice
                    || action.type == GameActionType.CancelChoice;
            case GameChoiceKind.DiscardToHandSize:
                return action.type == GameActionType.ToggleDiscard
                    || action.type == GameActionType.ConfirmDiscard
                    || action.type == GameActionType.ToggleChoiceOption
                    || action.type == GameActionType.ConfirmChoice;
            default:
                return action.type == GameActionType.ToggleChoiceOption
                    || action.type == GameActionType.SetChoiceNumber
                    || action.type == GameActionType.SetChoiceAllocation
                    || action.type == GameActionType.MoveChoiceOption
                    || action.type == GameActionType.ConfirmChoice
                    || action.type == GameActionType.CancelChoice;
        }
    }

    private bool MatchesActiveChoice(int requestId)
    {
        return requestId <= 0
            || (activeChoice != null && activeChoice.requestId == requestId);
    }

    private bool BeginHandCastAction(int playerID, int handIndex)
    {
        if (activeChoice != null || !CanPlayCard(playerID, handIndex)) return false;

        IReadOnlyList<CardFeatureRuntime> features =
            GetPlayableHandFeatures(playerID, handIndex);
        if (features.Count <= 0) return false;
        if (features.Count > 1)
        {
            return BeginCastFeatureChoice(playerID, handIndex);
        }

        RuntimeCard card = GetHandCard(playerID, handIndex);
        if (card == null) return false;
        card.SetSpellFeature(features[0]);
        if (features[0].HasType(CardType.Land))
        {
            return PlayCard(playerID, handIndex);
        }

        return BeginPendingCast(
            playerID,
            GameChoiceZone.Hand,
            handIndex,
            features[0]);
    }

    public string ComputeDeterministicStateHash()
    {
        if (onlineViewOnly && !string.IsNullOrEmpty(onlineViewHash)) return onlineViewHash;
        string state = BuildDeterministicStateString();
        using SHA256 sha = SHA256.Create();
        byte[] digest = sha.ComputeHash(Encoding.UTF8.GetBytes(state));
        var result = new StringBuilder(digest.Length * 2);
        for (int i = 0; i < digest.Length; i++)
        {
            result.Append(digest[i].ToString("x2"));
        }
        return result.ToString();
    }

    public string BuildDeterministicStateString()
    {
        var builder = new StringBuilder(4096);
        builder.Append("v6|")
            .Append(deterministicSeed).Append('|')
            .Append(deterministicRandom?.State ?? 0).Append('|')
            .Append(nextActionSequence).Append('|')
            .Append(nextObjectId).Append('|')
            .Append(nextStackObjectId).Append('|')
            .Append(nextChoiceRequestId).Append('|')
            .Append(nextBattlefieldEntrySequence).Append('|')
            .Append(nextDamageReplacementId).Append('|')
            .Append(nextDamageReplacementTimestamp).Append('|')
            .Append(turnNumber).Append('|')
            .Append(startingPlayer).Append('|')
            .Append(openingMulligan ? 1 : 0).Append('|')
            .Append(openingMulliganCounts[1]).Append('|')
            .Append(openingMulliganCounts[2]).Append('|')
            .Append(openingMulliganKept[1] ? 1 : 0).Append('|')
            .Append(openingMulliganKept[2] ? 1 : 0).Append('|')
            .Append(openingMulliganDecidedThisRound[1] ? 1 : 0).Append('|')
            .Append(openingMulliganDecidedThisRound[2] ? 1 : 0).Append('|')
            .Append(openingMulliganBottomed[1] ? 1 : 0).Append('|')
            .Append(openingMulliganBottomed[2] ? 1 : 0).Append('|')
            .Append(currentPlayer).Append('|')
            .Append((int)currentPhase).Append('|');
        priorityState.AppendDeterministicState(builder);
        builder
            .Append(player1FullControl ? 1 : 0).Append('|')
            .Append(player2FullControl ? 1 : 0).Append('|')
            .Append(player1Life).Append('|')
            .Append(player2Life).Append('|')
            .Append(player1PoisonCounters).Append('|')
            .Append(player2PoisonCounters).Append('|')
            .Append(player1DamagePreventionRemaining).Append('|')
            .Append(player2DamagePreventionRemaining).Append('|')
            .Append(player1MaxHandSize).Append('|')
            .Append(player2MaxHandSize).Append('|')
            .Append(player1AttemptedDrawFromEmptyLibrary ? 1 : 0).Append('|')
            .Append(player2AttemptedDrawFromEmptyLibrary ? 1 : 0).Append('|')
            .Append(landsPlayedThisTurn).Append('|')
            .Append(combatAnimationInProgress ? 1 : 0).Append('|')
            .Append(pendingGenericChoiceCompletion != null ? 1 : 0).Append('|')
            .Append(pendingGenericChoiceCancellation != null ? 1 : 0).Append('|')
            .Append(pendingStackResolutionCompletion != null ? 1 : 0).Append('|')
            .Append(pendingLibrarySearchSource?.ObjectId ?? 0).Append('|');
        builder.Append("OUT[")
            .Append(matchOutcome.isFinished ? 1 : 0).Append(',')
            .Append(matchOutcome.winnerPlayerId).Append(',')
            .Append((int)matchOutcome.reason).Append("]|");
        AppendManaState(builder, player1ManaPool);
        AppendManaState(builder, player2ManaPool);
        AppendZoneState(builder, "P1L", player1Library);
        AppendZoneState(builder, "P1H", player1Hand);
        AppendZoneState(builder, "P1B", player1Battlefield);
        AppendZoneState(builder, "P1G", player1Graveyard);
        AppendZoneState(builder, "P1E", player1Exile);
        AppendZoneState(builder, "P1C", player1Command);
        AppendZoneState(builder, "P2L", player2Library);
        AppendZoneState(builder, "P2H", player2Hand);
        AppendZoneState(builder, "P2B", player2Battlefield);
        AppendZoneState(builder, "P2G", player2Graveyard);
        AppendZoneState(builder, "P2E", player2Exile);
        AppendZoneState(builder, "P2C", player2Command);

        builder.Append("STACK[");
        AppendStackItems(builder, stack);
        builder.Append("]|");

        if (activeChoice == null)
        {
            builder.Append("CHOICE[]|");
        }
        else
        {
            builder.Append("CHOICE[")
                .Append(activeChoice.requestId).Append(':')
                .Append(activeChoice.revision).Append(':')
                .Append((int)activeChoice.kind).Append(':')
                .Append((int)activeChoice.selectionMode).Append(':')
                .Append(activeChoice.choosingPlayerId).Append(':')
                .Append(activeChoice.minimumSelections).Append(':')
                .Append(activeChoice.maximumSelections).Append(':')
                .Append(activeChoice.minimumValue).Append(':')
                .Append(activeChoice.maximumValue).Append(':')
                .Append(activeChoice.selectedValue).Append(':')
                .Append(activeChoice.hasSelectedValue ? 1 : 0).Append(':')
                .Append(activeChoice.requiredTotalAmount).Append(':');
            for (int i = 0; i < activeChoice.selectedOptionIds.Count; i++)
            {
                builder.Append(activeChoice.selectedOptionIds[i]).Append(',');
            }
            builder.Append("|A:");
            for (int i = 0; i < activeChoice.allocations.Count; i++)
            {
                GameChoiceAllocation allocation = activeChoice.allocations[i];
                builder.Append(allocation?.optionId ?? 0).Append('=')
                    .Append(allocation?.amount ?? 0).Append(',');
            }
            builder.Append("|O:");
            for (int i = 0; i < activeChoice.options.Count; i++)
            {
                GameChoiceOption option = activeChoice.options[i];
                builder.Append(option?.optionId ?? 0).Append('/')
                    .Append(option?.legal == true ? 1 : 0).Append('/');
            }
            builder.Append("]|");
        }

        builder.Append("CAST[");
        pendingCast?.AppendDeterministicState(builder);
        builder.Append("]|CASTPAY[");
        AppendScriptAbilityPaymentState(builder, pendingCastPayment);
        builder.Append("]|");
        AppendChoiceResultState(builder, lastCompletedChoice);
        builder.Append("ATK[");
        for (int i = 0; i < pendingAttackers.Count; i++)
        {
            builder.Append(pendingAttackers[i]?.ObjectId ?? 0).Append(',');
        }
        builder.Append("]|ATT[");
        var pendingTargetCards = new List<RuntimeCard>(pendingAttackers);
        pendingTargetCards.Sort((left, right) =>
            (left?.ObjectId ?? 0).CompareTo(right?.ObjectId ?? 0));
        for (int i = 0; i < pendingTargetCards.Count; i++)
        {
            RuntimeCard attacker = pendingTargetCards[i];
            pendingAttackTargets.TryGetValue(attacker, out object attackTarget);
            builder.Append(attacker?.ObjectId ?? 0).Append('>');
            if (attackTarget is RuntimeCard permanent)
            {
                builder.Append('C').Append(permanent.ObjectId);
            }
            else
            {
                builder.Append('P').Append(attackTarget is int player ? player : 0);
            }
            builder.Append(',');
        }
        builder.Append("]|PTR[");
        AppendStackItems(builder, pendingTriggeredAbilities);
        builder.Append("]|PTO[");
        AppendStackItems(builder, pendingTriggerOrderItems);
        builder.Append("]|DTO[");
        AppendStackItems(builder, deferredTriggerOrderItems);
        builder.Append("]|LEG[");
        AppendCardIdentityList(builder, pendingLegendRuleCards);
        builder.Append("]|FC[")
            .Append(firstCombatDamageStepSnapshotActive ? 1 : 0)
            .Append(':');
        var specialStrikers = new List<RuntimeCard>(
            firstCombatDamageStepSpecialStrikers);
        specialStrikers.Sort((left, right) =>
            (left?.ObjectId ?? 0).CompareTo(right?.ObjectId ?? 0));
        AppendCardIdentityList(builder, specialStrikers);
        builder.Append("]|");
        AppendUnspentSourceManaState(builder);
        AppendDamageReplacementState(builder);
        eventPipeline.AppendDeterministicState(builder);
        return builder.ToString();
    }

    private static void AppendChoiceResultState(
        StringBuilder builder,
        GameChoiceResult result)
    {
        builder.Append("LAST[");
        if (result != null)
        {
            builder.Append(result.requestId).Append('/')
                .Append((int)result.kind).Append('/')
                .Append(result.choosingPlayerId).Append('/')
                .Append(result.selectedValue).Append('/');
            for (int i = 0; i < result.selectedOptionIds.Count; i++)
            {
                builder.Append(result.selectedOptionIds[i]).Append(',');
            }
            builder.Append('/');
            for (int i = 0; i < result.allocations.Count; i++)
            {
                GameChoiceAllocation allocation = result.allocations[i];
                builder.Append(allocation?.optionId ?? 0).Append('=')
                    .Append(allocation?.amount ?? 0).Append(',');
            }
        }
        builder.Append("]|");
    }

    private static void AppendCardIdentityList(
        StringBuilder builder,
        IReadOnlyList<RuntimeCard> cards)
    {
        if (cards == null) return;
        for (int i = 0; i < cards.Count; i++)
        {
            RuntimeCard card = cards[i];
            builder.Append(card?.ObjectId ?? 0).Append('/')
                .Append(card?.ZoneChangeVersion ?? -1).Append(',');
        }
    }

    private void AppendUnspentSourceManaState(StringBuilder builder)
    {
        builder.Append("USM[");
        var entries = new List<KeyValuePair<RuntimeCard, ManaPool>>(
            unspentSourceMana);
        entries.Sort((left, right) =>
            (left.Key?.ObjectId ?? 0).CompareTo(right.Key?.ObjectId ?? 0));
        for (int i = 0; i < entries.Count; i++)
        {
            KeyValuePair<RuntimeCard, ManaPool> entry = entries[i];
            builder.Append(entry.Key?.ObjectId ?? 0).Append('/');
            AppendManaState(builder, entry.Value);
        }
        builder.Append("]|");
    }

    private void AppendDamageReplacementState(StringBuilder builder)
    {
        builder.Append("DMGREP[")
            .Append(nextDamageReplacementId).Append('/')
            .Append(nextDamageReplacementTimestamp).Append(':');
        damageReplacements.Sort(
            (left, right) => left.Timestamp.CompareTo(right.Timestamp));
        for (int i = 0; i < damageReplacements.Count; i++)
        {
            DamageReplacementRuntime replacement = damageReplacements[i];
            builder.Append(replacement.Id).Append('/')
                .Append(replacement.Timestamp).Append('/')
                .Append(replacement.EffectSource?.ObjectId ?? 0).Append('/')
                .Append(replacement.EffectSourceZoneChangeVersion).Append('/')
                .Append((int)replacement.Scope).Append('/')
                .Append((int)replacement.Operation).Append('/')
                .Append(replacement.ScopeCard?.ObjectId ?? 0).Append('/')
                .Append(replacement.ScopeCardZoneChangeVersion).Append('/')
                .Append(replacement.ScopePlayer).Append('/')
                .Append(replacement.Value).Append('/')
                .Append(replacement.CombatOnly ? 1 : 0).Append('/')
                .Append(replacement.UntilEndOfTurn ? 1 : 0).Append(',');
        }
        builder.Append("]|");
    }

    private static void AppendStackItems(
        StringBuilder builder,
        IReadOnlyList<GameStackItem> items)
    {
        if (items == null) return;
        for (int i = 0; i < items.Count; i++)
        {
            GameStackItem item = items[i];
            if (item == null)
            {
                builder.Append("null;");
                continue;
            }

            builder.Append(item.StackObjectId).Append('/')
                .Append((int)item.Kind).Append('/')
                .Append(item.SourceObjectId).Append('/')
                .Append(item.SourceCard?.ObjectId ?? 0).Append('/')
                .Append(item.ControllerPlayerId).Append('/')
                .Append(item.HookName).Append('/')
                .Append(item.SpellFeature?.FaceIndex ?? -1).Append('/')
                .Append(item.SpellFeature?.FeatureIndex ?? -1).Append('/')
                .Append(item.RuleEntry?.order ?? -1).Append('/')
                .Append((int)(item.RuleEntry?.kind ?? CardRuleEntryKind.Text)).Append('/')
                .Append(item.AbilityValue).Append('/');

            GameEventContext eventContext = item.EventContext;
            if (eventContext != null)
            {
                builder.Append('E')
                    .Append(eventContext.eventId).Append(',')
                    .Append((int)eventContext.type).Append(',')
                    .Append(eventContext.sourceObjectId).Append(',')
                    .Append(eventContext.sourceZoneChangeVersion).Append(',')
                    .Append(eventContext.cardObjectId).Append(',')
                    .Append(eventContext.cardZoneChangeVersion).Append(',')
                    .Append(eventContext.playerId).Append(',')
                    .Append(eventContext.controllerId).Append(',')
                    .Append((int)eventContext.fromZone).Append(',')
                    .Append((int)eventContext.toZone).Append(',')
                    .Append(eventContext.amount).Append(',')
                    .Append(eventContext.originalAmount).Append(',')
                    .Append(eventContext.detail).Append(',')
                    .Append((int)eventContext.damageKind).Append(',')
                    .Append(eventContext.protectionPreventionApplied ? 1 : 0).Append(',')
                    .Append(eventContext.sourcePreventionApplied ? 1 : 0).Append(',')
                    .Append(eventContext.targetPreventionApplied ? 1 : 0).Append(',')
                    .Append(eventContext.prevented ? 1 : 0).Append(',')
                    .Append(eventContext.replaced ? 1 : 0);
            }

            builder.Append("/T[");
            IReadOnlyList<SpellTargetReference> targets = item.Targets;
            for (int targetIndex = 0; targetIndex < targets.Count; targetIndex++)
            {
                SpellTargetReference target = targets[targetIndex];
                builder.Append(target?.Card?.ObjectId ?? 0).Append(',')
                    .Append(target?.CardZoneChangeVersion ?? -1).Append(',')
                    .Append(target?.PlayerID ?? 0).Append(';');
            }
            builder.Append("]/C[");
            item.CastDeclaration?.AppendDeterministicState(builder);
            builder.Append("];");
        }
    }

    private static void AppendScriptAbilityPaymentState(
        StringBuilder builder,
        ScriptAbilityPayment payment)
    {
        if (builder == null || payment == null) return;
        AppendCardIdentityList(builder, payment.DiscardCards);
        builder.Append('/');
        AppendCardIdentityList(builder, payment.SacrificePermanents);
        builder.Append('/');
        AppendCardIdentityList(builder, payment.ExileHandCards);
        builder.Append('/');
        AppendCardIdentityList(builder, payment.RevealHandCards);
    }

    private static void AppendManaState(StringBuilder builder, ManaPool pool)
    {
        builder.Append("M[")
            .Append(pool.White).Append(',')
            .Append(pool.Blue).Append(',')
            .Append(pool.Black).Append(',')
            .Append(pool.Red).Append(',')
            .Append(pool.Green).Append(',')
            .Append(pool.Colorless).Append("]|");
    }

    private static void AppendZoneState(
        StringBuilder builder,
        string label,
        IReadOnlyList<RuntimeCard> cards)
    {
        builder.Append(label).Append('[');
        if (cards != null)
        {
            for (int i = 0; i < cards.Count; i++)
            {
                RuntimeCard card = cards[i];
                if (card == null)
                {
                    builder.Append("null;");
                    continue;
                }

                card.AppendDeterministicState(builder);
                builder.Append(';');
            }
        }
        builder.Append("]|");
    }

    public void NextPhase()
    {
        if (IsGameOver()) return;
        if (HasActiveChoice())
        {
            Debug.LogWarning("Finish the current choice before advancing.");
            return;
        }

        if (combatAnimationInProgress) return;

        if (currentPhase == TurnPhase.DeclareAttackers && pendingAttackers.Count > 0 && !HasPendingStack())
        {
            CommitPendingAttackers();
            return;
        }

        if (priorityState.IsOpen)
        {
            PassPriority(priorityState.PlayerWithPriority);
            return;
        }

        ProceedToNextPhase();
    }

    public void EndTurn()
    {
        if (IsGameOver()) return;
        if (HasActiveChoice())
        {
            Debug.LogWarning("Finish the current choice before ending the turn.");
            return;
        }

        if (HasPendingStack())
        {
            Debug.LogWarning("Resolve the stack before ending the turn.");
            return;
        }

        priorityState.Close();
        SetCurrentPhase(TurnPhase.Cleanup);
        CleanupStep();
    }

    public int GetCurrentPlayer()
    {
        return currentPlayer;
    }

    public int GetStartingPlayer()
    {
        return startingPlayer;
    }

    public bool IsOpeningMulligan()
    {
        return openingMulligan;
    }

    public int GetOpeningMulliganCount(int player)
    {
        return player == 1 || player == 2 ? openingMulliganCounts[player] : 0;
    }

    public bool IsInitialized()
    {
        return initialized;
    }

    public bool IsGameOver()
    {
        return matchOutcome.isFinished;
    }

    public MatchOutcome GetMatchOutcome()
    {
        return matchOutcome;
    }

    public TurnPhase GetCurrentPhase()
    {
        return currentPhase;
    }

    public string GetCurrentPhaseLabel()
    {
        return GetPhaseDisplayName(currentPhase);
    }

    public static string GetPhaseDisplayName(TurnPhase phase)
    {
        switch (phase)
        {
            case TurnPhase.Untap: return "\u91cd\u7f6e\u6b65\u9aa4";
            case TurnPhase.Upkeep: return "\u7ef4\u6301\u6b65\u9aa4";
            case TurnPhase.Draw: return "\u62bd\u724c\u6b65\u9aa4";
            case TurnPhase.Main1: return "\u7b2c\u4e00\u4e3b\u8981\u9636\u6bb5";
            case TurnPhase.BeginningOfCombat: return "\u6218\u6597\u5f00\u59cb\u6b65\u9aa4";
            case TurnPhase.DeclareAttackers: return "\u5ba3\u544a\u653b\u51fb\u8005\u6b65\u9aa4";
            case TurnPhase.DeclareBlockers: return "\u5ba3\u544a\u963b\u6321\u8005\u6b65\u9aa4";
            case TurnPhase.CombatDamage: return "\u6218\u6597\u4f24\u5bb3\u6b65\u9aa4";
            case TurnPhase.EndOfCombat: return "\u6218\u6597\u7ed3\u675f\u6b65\u9aa4";
            case TurnPhase.Main2: return "\u7b2c\u4e8c\u4e3b\u8981\u9636\u6bb5";
            case TurnPhase.End: return "\u7ed3\u675f\u6b65\u9aa4";
            case TurnPhase.Cleanup: return "\u6e05\u9664\u6b65\u9aa4";
            default: return phase.ToString();
        }
    }

    public int GetTurnNumber()
    {
        return turnNumber;
    }

    public bool IsFullControl()
    {
        return IsFullControl(OnlinePlayerContext.LocalPlayerId);
    }

    public void SetFullControl(bool enabled)
    {
        SetFullControl(OnlinePlayerContext.LocalPlayerId, enabled);
    }

    public bool IsFullControl(int player)
    {
        return player == 1
            ? player1FullControl
            : player == 2 && player2FullControl;
    }

    public void SetFullControl(int player, bool enabled)
    {
        if (player == 1) player1FullControl = enabled;
        else if (player == 2) player2FullControl = enabled;
        else return;
        ContinueAutomaticPriority();
    }

    public bool IsAwaitingPriority()
    {
        return priorityState.IsOpen;
    }

    public int GetPriorityPlayer()
    {
        return priorityState.PlayerWithPriority;
    }

    public bool HasPriority(int player)
    {
        return !IsGameOver() && priorityState.IsOpen && priorityState.PlayerWithPriority == player;
    }

    public bool CanHumanAdvance()
    {
        return CanPlayerAdvance(OnlinePlayerContext.LocalPlayerId);
    }

    public bool CanPlayerAdvance(int playerId)
    {
        if (playerId != 1 && playerId != 2) return false;
        if (IsGameOver()) return false;
        if (combatAnimationInProgress) return false;
        if (HasActiveChoice()) return false;
        if (priorityState.IsOpen) return priorityState.PlayerWithPriority == playerId;
        return !IsOnlineMatch() ? playerId == 1 : currentPlayer == playerId;
    }

    public GameActionType GetPrimaryAdvanceActionType(int playerId)
    {
        if (currentPhase == TurnPhase.DeclareAttackers && playerId == currentPlayer)
            return GameActionType.CommitAttackers;
        return priorityState.IsOpen && priorityState.PlayerWithPriority == playerId
            ? GameActionType.PassPriority
            : GameActionType.AdvancePhase;
    }

    public bool IsCombatAnimationInProgress()
    {
        return combatAnimationInProgress;
    }

    public string GetCurrentActionPrompt(int viewerPlayer = 1)
    {
        if (IsGameOver()) return matchOutcome.message ?? string.Empty;
        if (combatAnimationInProgress) return "Combat damage is resolving.";
        if (openingMulligan)
        {
            string order = startingPlayer == viewerPlayer
                ? $"你是先手；玩家 {3 - startingPlayer} 是后手。"
                : $"你是后手；玩家 {startingPlayer} 是先手。";
            if (activeChoice != null && activeChoice.choosingPlayerId != viewerPlayer)
                return $"{order} 等待玩家 {activeChoice.choosingPlayerId} 完成调度。";
            return activeChoice?.prompt ?? order;
        }

        if (IsChoosingLibraryCard())
        {
            if (GetLibrarySearchPlayer() == viewerPlayer)
            {
                return string.IsNullOrWhiteSpace(GetLibrarySearchPrompt())
                    ? "Choose a card from your library."
                    : GetLibrarySearchPrompt();
            }

            return $"Waiting for Player {GetLibrarySearchPlayer()} to search their library.";
        }

        if (priorityState.IsOpen)
        {
            if (priorityState.PlayerWithPriority != viewerPlayer)
            {
                return $"Waiting for Player {priorityState.PlayerWithPriority} to act or pass priority.";
            }

            if (HasPendingStack())
            {
                RuntimeCard top = GetTopStackCard();
                string topName = top != null && top.Data != null ? top.Data.cardName : "the stack";
                return $"You have priority. Cast an instant, activate an ability, or pass to resolve {topName}.";
            }

            if (currentPlayer == viewerPlayer)
            {
                switch (currentPhase)
                {
                    case TurnPhase.Main1:
                    case TurnPhase.Main2:
                        return "Your main phase: play a land, cast a spell, activate an ability, or end the step.";
                    case TurnPhase.DeclareAttackers:
                        return pendingAttackers.Count > 0
                            ? "Confirm attackers or choose more attackers."
                            : "Choose attackers or end the declare attackers step.";
                    case TurnPhase.DeclareBlockers:
                        return "Confirm blockers or end the declare blockers step.";
                    default:
                        return "You have priority. Cast an instant, activate an ability, or pass.";
                }
            }

            return "You have priority on the opponent turn. Cast an instant, activate an ability, or pass.";
        }

        if (currentPlayer == viewerPlayer)
        {
            return $"Your turn: {GetPhaseDisplayName(currentPhase)}.";
        }

        return $"Opponent turn: {GetPhaseDisplayName(currentPhase)}.";
    }

    public int GetDeckCount(int playerID)
    {
        if (playerID == 1) return player1Library.Count;
        if (playerID == 2) return player2Library.Count;
        return 0;
    }

    public int GetGraveyardCount(int playerID)
    {
        if (playerID == 1) return player1Graveyard.Count;
        if (playerID == 2) return player2Graveyard.Count;
        return 0;
    }

    public int GetExileCount(int playerID)
    {
        if (playerID == 1) return player1Exile.Count;
        if (playerID == 2) return player2Exile.Count;
        return 0;
    }

    public int GetHandCount(int playerID)
    {
        if (playerID == 1) return player1Hand.Count;
        if (playerID == 2) return player2Hand.Count;
        return 0;
    }

    public int GetMaxHandSize(int playerID)
    {
        int baseValue = playerID == 1
            ? player1MaxHandSize
            : playerID == 2
                ? player2MaxHandSize
                : maxHandSize;
        return Mathf.Max(0, EvaluateScriptRuleValue(
            "MaxHandSize",
            playerID,
            null,
            baseValue));
    }

    public void SetMaxHandSize(int playerID, int value)
    {
        int clamped = Mathf.Max(0, value);
        if (playerID == 1) player1MaxHandSize = clamped;
        else if (playerID == 2) player2MaxHandSize = clamped;
    }

    public bool IsDiscardingToHandSize()
    {
        return activeChoice != null && activeChoice.kind == GameChoiceKind.DiscardToHandSize;
    }

    public int GetDiscardToHandSizePlayer()
    {
        return IsDiscardingToHandSize() ? activeChoice.choosingPlayerId : 0;
    }

    public int GetDiscardToHandSizeRemaining()
    {
        if (!IsDiscardingToHandSize()) return 0;
        return Mathf.Max(0, activeChoice.minimumSelections - activeChoice.selectedOptionIds.Count);
    }

    public int GetDiscardToHandSizeRequiredCount()
    {
        return IsDiscardingToHandSize() ? activeChoice.minimumSelections : 0;
    }

    public int GetDiscardToHandSizeSelectedCount()
    {
        return IsDiscardingToHandSize() ? activeChoice.selectedOptionIds.Count : 0;
    }

    public bool CanConfirmDiscardToHandSize(int playerID)
    {
        return IsDiscardingToHandSize()
            && activeChoice.choosingPlayerId == playerID
            && activeChoice.minimumSelections > 0
            && activeChoice.CanConfirm;
    }

    public bool IsHandCardSelectedForDiscard(int playerID, int handIndex)
    {
        return IsDiscardingToHandSize()
            && activeChoice.choosingPlayerId == playerID
            && activeChoice.IsSelected(handIndex);
    }

    public int GetLife(int playerID)
    {
        if (playerID == 1) return player1Life;
        if (playerID == 2) return player2Life;
        return 0;
    }

    public int GetPoisonCounters(int playerID)
    {
        if (playerID == 1) return player1PoisonCounters;
        if (playerID == 2) return player2PoisonCounters;
        return 0;
    }

    public int GetStackCount()
    {
        return onlineViewOnly ? stackCardView.Count : stack.Count;
    }

    public RuntimeCard GetTopStackCard()
    {
        return onlineViewOnly
            ? (stackCardView.Count > 0 ? stackCardView[stackCardView.Count - 1] : null)
            : (stack.Count > 0 ? stack[stack.Count - 1].SourceCard : null);
    }

    public IReadOnlyList<RuntimeCard> GetStackCards()
    {
        return stackCardView;
    }

    public bool IsChoosingLibraryCard()
    {
        return activeChoice != null && activeChoice.kind == GameChoiceKind.LibrarySearch;
    }

    public int GetLibrarySearchPlayer()
    {
        return IsChoosingLibraryCard() ? activeChoice.choosingPlayerId : 0;
    }

    public string GetLibrarySearchPrompt()
    {
        return IsChoosingLibraryCard() ? activeChoice.prompt ?? string.Empty : string.Empty;
    }

    public int GetLibrarySearchSelectedDeckIndex()
    {
        if (!IsChoosingLibraryCard() || activeChoice.selectedOptionIds.Count == 0) return -1;
        GameChoiceOption option = activeChoice.FindOption(activeChoice.selectedOptionIds[0]);
        return option != null ? option.zoneIndex : -1;
    }

    public bool HasLibrarySearchSelection()
    {
        return GetLibrarySearchSelectedDeckIndex() >= 0;
    }

    public bool CanConfirmLibrarySearch(int playerID)
    {
        return IsChoosingLibraryCard()
            && activeChoice.choosingPlayerId == playerID
            && (!onlineViewOnly || playerID == OnlinePlayerContext.LocalPlayerId)
            && CanConfirmActiveChoice();
    }

    public bool CanSelectLibrarySearchCard(int playerID, int deckIndex)
    {
        if (!IsChoosingLibraryCard() || activeChoice.choosingPlayerId != playerID) return false;
        GameChoiceOption option = activeChoice.FindOption(deckIndex);
        return option != null
            && option.zone == GameChoiceZone.Library
            && option.zoneIndex == deckIndex
            && option.legal;
    }

    public bool SelectLibrarySearchCard(int playerID, int deckIndex)
    {
        return ToggleLibrarySearchCard(playerID, deckIndex);
    }

    public bool ToggleLibrarySearchCard(int playerID, int deckIndex)
    {
        return TryToggleChoiceOption(playerID, deckIndex);
    }

    public bool ConfirmLibrarySearch(int playerID)
    {
        if (!CanConfirmLibrarySearch(playerID)) return false;

        int selectedDeckIndex = GetLibrarySearchSelectedDeckIndex();
        if (selectedDeckIndex < 0)
        {
            return FailLibrarySearch(playerID);
        }

        List<RuntimeCard> deck = GetLibraryList(playerID);
        List<RuntimeCard> battlefield = playerID == 1 ? player1Battlefield : player2Battlefield;
        if (deck == null || battlefield == null) return false;

        if (!CanSelectLibrarySearchCard(playerID, selectedDeckIndex)) return false;

        RuntimeCard fetched = deck[selectedDeckIndex];
        deck.RemoveAt(selectedDeckIndex);

        fetched.Tap();
        AddToBattlefield(battlefield, fetched);

        string sourceName = pendingLibrarySearchSource?.Data != null ? pendingLibrarySearchSource.Data.cardName : "an ability";
        CompleteLibrarySearch(playerID, $"{fetched.Data.cardName} was put onto the battlefield tapped with {sourceName}.");
        return true;
    }

    public bool FailLibrarySearch(int playerID)
    {
        if (!IsChoosingLibraryCard()
            || activeChoice.choosingPlayerId != playerID
            || activeChoice.minimumSelections > 0)
        {
            return false;
        }

        string sourceName = pendingLibrarySearchSource?.Data != null ? pendingLibrarySearchSource.Data.cardName : "an ability";
        CompleteLibrarySearch(playerID, $"Player {playerID} found no card with {sourceName}.");
        return true;
    }

    public bool HasActiveChoice()
    {
        return activeChoice != null;
    }

    public GameChoiceRequest GetActiveChoice()
    {
        return activeChoice;
    }

    public bool TryToggleChoiceOption(int playerID, int optionId)
    {
        return activeChoice != null
            && activeChoice.choosingPlayerId == playerID
            && activeChoice.Toggle(optionId);
    }

    public bool TryToggleChoiceOption(int playerID, int requestId, int optionId)
    {
        return activeChoice != null
            && activeChoice.requestId == requestId
            && TryToggleChoiceOption(playerID, optionId);
    }

    public bool TrySetChoiceNumber(int playerID, int requestId, int value)
    {
        return activeChoice != null
            && activeChoice.requestId == requestId
            && activeChoice.choosingPlayerId == playerID
            && activeChoice.SetNumber(value);
    }

    public bool TrySetChoiceAllocation(int playerID, int requestId, int optionId, int amount)
    {
        return activeChoice != null
            && activeChoice.requestId == requestId
            && activeChoice.choosingPlayerId == playerID
            && activeChoice.SetAllocatedAmount(optionId, amount);
    }

    public bool TryMoveChoiceOption(int playerID, int requestId, int optionId, int destinationIndex)
    {
        return activeChoice != null
            && activeChoice.requestId == requestId
            && activeChoice.choosingPlayerId == playerID
            && activeChoice.MoveSelectedOption(optionId, destinationIndex);
    }

    public GameChoiceResult GetActiveChoiceResultPreview()
    {
        return activeChoice?.CreateResult();
    }

    public GameChoiceResult GetLastCompletedChoice()
    {
        return lastCompletedChoice;
    }

    public bool CanConfirmActiveChoice()
    {
        if (onlineViewOnly)
            return activeChoice != null && onlineCanConfirmActiveChoice;
        return activeChoice != null
            && activeChoice.CanConfirm
            && (pendingGenericChoiceValidator == null
                || pendingGenericChoiceValidator(activeChoice));
    }

    public bool IsResolvingGenericChoice()
    {
        return activeChoice != null && IsGenericChoiceKind(activeChoice.kind);
    }

    public GameChoiceRequest BeginOptionChoice(
        GameChoiceKind kind,
        int playerID,
        string prompt,
        int minimumSelections,
        int maximumSelections,
        bool mayCancel,
        IReadOnlyList<GameChoiceOption> options,
        System.Action<GameChoiceResult> onCompleted,
        System.Action onCancelled = null)
    {
        if (kind != GameChoiceKind.SelectOptions
            && kind != GameChoiceKind.ChooseMode
            && kind != GameChoiceKind.OrderCards
            && kind != GameChoiceKind.ManaPayment
            && kind != GameChoiceKind.ReplacementEffect
            && kind != GameChoiceKind.MulliganDecision
            && kind != GameChoiceKind.MulliganBottom)
        {
            return null;
        }

        GameChoiceRequest request = BeginChoice(
            kind,
            playerID,
            prompt,
            minimumSelections,
            maximumSelections,
            mayCancel);
        if (request == null) return null;

        if (kind == GameChoiceKind.OrderCards || kind == GameChoiceKind.MulliganBottom)
        {
            request.selectionMode = GameChoiceSelectionMode.Ordered;
        }

        if (options != null)
        {
            for (int i = 0; i < options.Count; i++)
            {
                GameChoiceOption option = options[i];
                if (option != null) request.options.Add(option);
            }
        }

        pendingGenericChoiceCompletion = onCompleted;
        pendingGenericChoiceCancellation = onCancelled;
        return request;
    }

    public GameChoiceRequest BeginNumberChoice(
        int playerID,
        string prompt,
        int minimumValue,
        int maximumValue,
        bool mayCancel,
        System.Action<GameChoiceResult> onCompleted,
        System.Action onCancelled = null)
    {
        if (minimumValue > maximumValue) return null;

        GameChoiceRequest request = BeginChoice(
            GameChoiceKind.ChooseNumber,
            playerID,
            prompt,
            0,
            0,
            mayCancel);
        if (request == null) return null;

        request.selectionMode = GameChoiceSelectionMode.Number;
        request.minimumValue = minimumValue;
        request.maximumValue = maximumValue;
        pendingGenericChoiceCompletion = onCompleted;
        pendingGenericChoiceCancellation = onCancelled;
        return request;
    }

    public GameChoiceRequest BeginDistributionChoice(
        int playerID,
        string prompt,
        int totalAmount,
        int minimumTargets,
        int maximumTargets,
        bool mayCancel,
        IReadOnlyList<GameChoiceOption> options,
        System.Action<GameChoiceResult> onCompleted,
        System.Action onCancelled = null)
    {
        if (totalAmount < 0) return null;

        GameChoiceRequest request = BeginChoice(
            GameChoiceKind.DistributeAmount,
            playerID,
            prompt,
            minimumTargets,
            maximumTargets,
            mayCancel);
        if (request == null) return null;

        request.selectionMode = GameChoiceSelectionMode.Distribution;
        request.requiredTotalAmount = totalAmount;
        if (options != null)
        {
            for (int i = 0; i < options.Count; i++)
            {
                GameChoiceOption option = options[i];
                if (option != null) request.options.Add(option);
            }
        }

        pendingGenericChoiceCompletion = onCompleted;
        pendingGenericChoiceCancellation = onCancelled;
        return request;
    }

    public GameChoiceRequest BeginRepeatedChoice(
        int playerID,
        string prompt,
        int minimumSelections,
        int maximumSelections,
        bool mayCancel,
        IReadOnlyList<GameChoiceOption> options,
        System.Action<GameChoiceResult> onCompleted,
        System.Action onCancelled = null)
    {
        GameChoiceRequest request = BeginChoice(
            GameChoiceKind.RepeatOptions,
            playerID,
            prompt,
            minimumSelections,
            maximumSelections,
            mayCancel);
        if (request == null) return null;

        request.selectionMode = GameChoiceSelectionMode.Repeated;
        if (options != null)
        {
            for (int i = 0; i < options.Count; i++)
            {
                GameChoiceOption option = options[i];
                if (option != null) request.options.Add(option);
            }
        }
        pendingGenericChoiceCompletion = onCompleted;
        pendingGenericChoiceCancellation = onCancelled;
        return request;
    }

    public bool TryConfirmActiveChoice(int playerID)
    {
        if (activeChoice == null
            || activeChoice.choosingPlayerId != playerID
            || !CanConfirmActiveChoice())
        {
            return false;
        }

        if (activeChoice.kind == GameChoiceKind.LibrarySearch) return ConfirmLibrarySearch(playerID);
        if (activeChoice.kind == GameChoiceKind.DiscardToHandSize) return ConfirmDiscardToMaxHandSize(playerID);
        if (activeChoice.kind == GameChoiceKind.SpellTarget) return ConfirmSpellTargetChoice(playerID);
        if (activeChoice.kind == GameChoiceKind.CastConfirmation) return ConfirmCastChoice(playerID);
        if (activeChoice.kind == GameChoiceKind.LegendRule) return ConfirmLegendRuleChoice(playerID);
        if (activeChoice.kind == GameChoiceKind.TriggerOrder) return ConfirmTriggerOrderChoice(playerID);
        if (IsGenericChoiceKind(activeChoice.kind)) return ConfirmGenericChoice(playerID);
        return false;
    }

    public bool TryCancelActiveChoice(int playerID)
    {
        if (activeChoice == null
            || activeChoice.choosingPlayerId != playerID
            || !activeChoice.mayCancel)
        {
            return false;
        }

        if (!IsGenericChoiceKind(activeChoice.kind)) return false;

        activeChoice = null;
        pendingGenericChoiceCompletion = null;
        pendingGenericChoiceValidator = null;
        System.Action cancellation = pendingGenericChoiceCancellation;
        pendingGenericChoiceCancellation = null;
        cancellation?.Invoke();
        TryResumeSuspendedStackResolution();
        return true;
    }

    private bool ConfirmGenericChoice(int playerID)
    {
        if (activeChoice == null
            || activeChoice.choosingPlayerId != playerID
            || !IsGenericChoiceKind(activeChoice.kind))
        {
            return false;
        }

        GameChoiceResult result = activeChoice.CreateResult();
        if (result == null) return false;

        lastCompletedChoice = result;
        activeChoice = null;
        System.Action<GameChoiceResult> completion = pendingGenericChoiceCompletion;
        pendingGenericChoiceCompletion = null;
        pendingGenericChoiceValidator = null;
        pendingGenericChoiceCancellation = null;
        completion?.Invoke(result);
        TryResumeSuspendedStackResolution();
        return true;
    }

    private bool SuspendStackResolutionForActiveChoice(System.Action completion)
    {
        if (!IsResolvingGenericChoice() || completion == null) return false;
        pendingStackResolutionCompletion = completion;
        return true;
    }

    private void TryResumeSuspendedStackResolution()
    {
        if (activeChoice != null || pendingStackResolutionCompletion == null) return;

        System.Action completion = pendingStackResolutionCompletion;
        pendingStackResolutionCompletion = null;
        completion();
    }

    private static bool IsGenericChoiceKind(GameChoiceKind kind)
    {
        return kind == GameChoiceKind.SelectOptions
            || kind == GameChoiceKind.ChooseMode
            || kind == GameChoiceKind.ChooseNumber
            || kind == GameChoiceKind.OrderCards
            || kind == GameChoiceKind.DistributeAmount
            || kind == GameChoiceKind.ManaPayment
            || kind == GameChoiceKind.RepeatOptions
            || kind == GameChoiceKind.ReplacementEffect
            || kind == GameChoiceKind.CombatDamageAssignment
            || kind == GameChoiceKind.MulliganDecision
            || kind == GameChoiceKind.MulliganBottom;
    }

    public bool IsOrderingTriggeredAbilities()
    {
        return activeChoice != null && activeChoice.kind == GameChoiceKind.TriggerOrder;
    }

    public bool ToggleTriggeredAbilityOrderItem(int playerID, int itemIndex)
    {
        if (!IsOrderingTriggeredAbilities()
            || activeChoice.choosingPlayerId != playerID
            || itemIndex < 0
            || itemIndex >= pendingTriggerOrderItems.Count)
        {
            return false;
        }

        return TryToggleChoiceOption(playerID, 3000000 + itemIndex);
    }

    public int GetTriggeredAbilityOrderRank(int itemIndex)
    {
        if (!IsOrderingTriggeredAbilities() || itemIndex < 0) return 0;
        int selectedIndex = activeChoice.selectedOptionIds.IndexOf(3000000 + itemIndex);
        return selectedIndex >= 0 ? selectedIndex + 1 : 0;
    }

    public bool ConfirmTriggerOrderChoice(int playerID)
    {
        if (!IsOrderingTriggeredAbilities()
            || activeChoice.choosingPlayerId != playerID
            || !activeChoice.CanConfirm)
        {
            return false;
        }

        var orderedTriggers = new List<GameStackItem>(pendingTriggerOrderItems.Count);
        for (int i = 0; i < activeChoice.selectedOptionIds.Count; i++)
        {
            GameChoiceOption option = activeChoice.FindOption(activeChoice.selectedOptionIds[i]);
            if (option == null
                || option.zoneIndex < 0
                || option.zoneIndex >= pendingTriggerOrderItems.Count)
            {
                return false;
            }

            orderedTriggers.Add(pendingTriggerOrderItems[option.zoneIndex]);
        }

        activeChoice = null;
        pendingTriggerOrderItems.Clear();
        PushTriggerItemsInOrder(orderedTriggers);
        var deferredTriggers = new List<GameStackItem>(deferredTriggerOrderItems);
        deferredTriggerOrderItems.Clear();

        int deferredController = deferredTriggers.Count > 0
            ? deferredTriggers[0]?.ControllerPlayerId ?? 0
            : 0;
        if (deferredTriggers.Count > 1
            && (IsOnlineMatch() || deferredController == 1))
        {
            BeginTriggerOrderChoice(
                deferredTriggers,
                null,
                deferredController);
            NotifyStackChanged();
            return true;
        }

        PushTriggerItemsInOrder(deferredTriggers);
        NotifyStackChanged();

        if (!IsGameOver() && !HasActiveChoice()) BeginPriority(currentPlayer);
        return true;
    }

    public bool IsChoosingLegendRule()
    {
        return activeChoice != null && activeChoice.kind == GameChoiceKind.LegendRule;
    }

    public int GetLegendRulePlayer()
    {
        return IsChoosingLegendRule() ? activeChoice.choosingPlayerId : 0;
    }

    public bool CanSelectLegendRuleCard(int playerID, RuntimeCard card)
    {
        if (!IsChoosingLegendRule()
            || activeChoice.choosingPlayerId != playerID
            || card == null
            || !TryGetBattlefieldLocation(card, out int ownerID, out int battlefieldIndex))
        {
            return false;
        }

        GameChoiceOption option = activeChoice.FindOption(
            GetBattlefieldChoiceOptionId(ownerID, battlefieldIndex));
        return option != null && option.legal;
    }

    public bool ToggleLegendRuleCard(int playerID, RuntimeCard card)
    {
        if (!CanSelectLegendRuleCard(playerID, card)) return false;
        TryGetBattlefieldLocation(card, out int ownerID, out int battlefieldIndex);
        return TryToggleChoiceOption(
            playerID,
            GetBattlefieldChoiceOptionId(ownerID, battlefieldIndex));
    }

    public bool IsLegendRuleCardSelected(RuntimeCard card)
    {
        if (!IsChoosingLegendRule()
            || card == null
            || !TryGetBattlefieldLocation(card, out int ownerID, out int battlefieldIndex))
        {
            return false;
        }

        return activeChoice.IsSelected(GetBattlefieldChoiceOptionId(ownerID, battlefieldIndex));
    }

    public bool ConfirmLegendRuleChoice(int playerID)
    {
        if (!IsChoosingLegendRule()
            || activeChoice.choosingPlayerId != playerID
            || !activeChoice.CanConfirm
            || !TryGetSelectedChoiceOption(GameChoiceKind.LegendRule, out GameChoiceOption option))
        {
            return false;
        }

        RuntimeCard keptCard = GetBattlefieldCard(option.ownerPlayerId, option.zoneIndex);
        if (keptCard == null || !pendingLegendRuleCards.Contains(keptCard)) return false;

        ResolveLegendRuleChoice(keptCard);
        if (!IsGameOver() && !HasActiveChoice()) BeginPriority(currentPlayer);
        return true;
    }

    public bool IsChoosingSpellTarget()
    {
        return activeChoice != null && activeChoice.kind == GameChoiceKind.SpellTarget;
    }

    public bool IsConfirmingCast()
    {
        return activeChoice != null && activeChoice.kind == GameChoiceKind.CastConfirmation;
    }

    public bool IsChoosingCastFeature()
    {
        return activeChoice != null && activeChoice.kind == GameChoiceKind.CastFeature;
    }

    public IReadOnlyList<CardFeatureRuntime> GetPlayableHandFeatures(int playerID, int handIndex)
    {
        RuntimeCard card = GetHandCard(playerID, handIndex);
        var result = new List<CardFeatureRuntime>();
        if (card == null
            || IsGameOver()
            || activeChoice != null
            || !HasPriority(playerID))
        {
            return result;
        }

        bool hasSplitParts = card.HasFeatureRole(CardFeatureRole.SplitPart);
        bool hasRoomDoors = card.HasFeatureRole(CardFeatureRole.RoomDoor);
        for (int i = 0; i < card.Features.Count; i++)
        {
            CardFeatureRuntime feature = card.Features[i];
            if (!feature.CanCastFromHand(hasSplitParts, hasRoomDoors)) continue;
            if (TryGetCanPlayFeatureReason(playerID, handIndex, feature, out _))
            {
                result.Add(feature);
            }
        }

        return result;
    }

    public bool BeginCastFeatureChoice(int playerID, int handIndex)
    {
        if (activeChoice != null) return false;
        if (!CanPlayCard(playerID, handIndex)) return false;

        IReadOnlyList<CardFeatureRuntime> features = GetPlayableHandFeatures(playerID, handIndex);
        if (features.Count <= 1) return false;

        RuntimeCard source = GetHandCard(playerID, handIndex);
        GameChoiceRequest request = BeginChoice(
            GameChoiceKind.CastFeature,
            playerID,
            $"Choose how to cast {source.Data.cardName}.",
            1,
            1,
            true);
        if (request == null) return false;

        request.sourceZone = GameChoiceZone.Hand;
        request.sourcePlayerId = playerID;
        request.sourceZoneIndex = handIndex;
        for (int i = 0; i < features.Count; i++)
        {
            CardFeatureRuntime feature = features[i];
            request.options.Add(new GameChoiceOption(
                GetCastFeatureChoiceOptionId(feature),
                GameChoiceZone.Hand,
                playerID,
                handIndex,
                true,
                $"{feature.CardName}  {feature.ManaCost.ToSymbolString()}  [{GetFeatureRoleLabel(feature.Role)}]"));
        }

        return true;
    }

    public bool SelectCastFeatureChoice(
        int playerID,
        int optionIndex,
        out bool requiresTarget,
        out string featureName)
    {
        requiresTarget = false;
        featureName = string.Empty;
        if (!IsChoosingCastFeature()
            || activeChoice.choosingPlayerId != playerID
            || optionIndex < 0
            || optionIndex >= activeChoice.options.Count)
        {
            return false;
        }

        GameChoiceOption option = activeChoice.options[optionIndex];
        RuntimeCard card = GetHandCard(playerID, activeChoice.sourceZoneIndex);
        CardFeatureRuntime feature = FindFeatureByChoiceOption(card, option);
        if (card == null
            || feature == null
            || !TryGetCanPlayFeatureReason(playerID, activeChoice.sourceZoneIndex, feature, out _))
        {
            return false;
        }

        int handIndex = activeChoice.sourceZoneIndex;
        activeChoice = null;
        card.SetSpellFeature(feature);
        requiresTarget = RequiresTarget(card);
        featureName = feature.CardName;
        return BeginPendingCast(
            playerID,
            GameChoiceZone.Hand,
            handIndex,
            feature);
    }

    public bool CancelCastFeatureChoice(int playerID)
    {
        if (!IsChoosingCastFeature()
            || activeChoice.choosingPlayerId != playerID
            || !activeChoice.mayCancel)
        {
            return false;
        }

        RuntimeCard card = GetHandCard(playerID, activeChoice.sourceZoneIndex);
        card?.ClearSpellFeature();
        activeChoice = null;
        return true;
    }

    private bool BeginPendingCast(
        int playerID,
        GameChoiceZone sourceZone,
        int sourceIndex,
        CardFeatureRuntime feature,
        bool withoutPayingManaCost = false,
        bool bypassSourcePermission = false,
        int sourcePlayerID = 0)
    {
        if (activeChoice != null || pendingCast != null || feature == null) return false;

        int resolvedSourcePlayerID = sourcePlayerID > 0 ? sourcePlayerID : playerID;
        RuntimeCard source = GetCastSourceCard(resolvedSourcePlayerID, sourceZone, sourceIndex);
        if (source == null || source.HasType(CardType.Land)) return false;
        if (sourceZone == GameChoiceZone.Hand)
        {
            if (!TryGetCanPlayFeatureReason(playerID, sourceIndex, feature, out _)) return false;
        }
        else if (!bypassSourcePermission
            && !CanCastSourceCard(playerID, sourceZone, sourceIndex))
        {
            return false;
        }

        source.SetSpellFeature(feature);
        source.ClearSpellTarget();
        pendingCast = new SpellCastDeclaration(
            source,
            playerID,
            resolvedSourcePlayerID,
            sourceZone,
            sourceIndex,
            feature,
            withoutPayingManaCost);
        pendingCastPayment = new ScriptAbilityPayment();
        return ContinuePendingCast();
    }

    private bool ContinuePendingCast()
    {
        while (pendingCast != null && activeChoice == null)
        {
            RuntimeCard source = pendingCast.Source;
            if (!IsPendingCastSourceAvailable())
            {
                CancelPendingCast();
                return false;
            }

            SpellCastProfile profile = source.CastProfile;
            switch (pendingCast.Stage)
            {
                case SpellCastDeclarationStage.Modes:
                    if (profile.Modes.Count > 0)
                    {
                        var modeOptions = new List<GameChoiceOption>(profile.Modes.Count);
                        for (int i = 0; i < profile.Modes.Count; i++)
                        {
                            SpellCastModeDefinition mode = profile.Modes[i];
                            modeOptions.Add(new GameChoiceOption(
                                i,
                                GameChoiceZone.None,
                                pendingCast.PlayerId,
                                -1,
                                true,
                                mode.Label));
                        }

                        GameChoiceRequest request = BeginOptionChoice(
                            GameChoiceKind.ChooseMode,
                            pendingCast.PlayerId,
                            $"Choose mode(s) for {source.GetCardName()}.",
                            profile.MinimumModes,
                            Mathf.Min(profile.MaximumModes, profile.Modes.Count),
                            true,
                            modeOptions,
                            result =>
                            {
                                var ids = new List<string>();
                                if (result != null)
                                {
                                    for (int i = 0; i < result.selectedOptionIds.Count; i++)
                                    {
                                        int option = result.selectedOptionIds[i];
                                        if (option >= 0 && option < profile.Modes.Count)
                                        {
                                            ids.Add(profile.Modes[option].Id);
                                        }
                                    }
                                }
                                pendingCast?.SetModes(ids);
                                if (pendingCast != null)
                                {
                                    pendingCast.Stage =
                                        SpellCastDeclarationStage.AlternativeCost;
                                    ContinuePendingCast();
                                }
                            },
                            CancelPendingCast);
                        return request != null;
                    }
                    pendingCast.Stage = SpellCastDeclarationStage.AlternativeCost;
                    break;

                case SpellCastDeclarationStage.AlternativeCost:
                    if (pendingCast.WithoutPayingManaCost)
                    {
                        pendingCast.AlternativeCostId = string.Empty;
                        pendingCast.Stage = SpellCastDeclarationStage.AdditionalCosts;
                        break;
                    }
                    if (profile.AlternativeCosts.Count > 0)
                    {
                        var costOptions = new List<GameChoiceOption>(
                            profile.AlternativeCosts.Count + 1)
                        {
                            new GameChoiceOption(
                                0,
                                GameChoiceZone.None,
                                pendingCast.PlayerId,
                                -1,
                                true,
                                $"Normal cost {pendingCast.Feature.ManaCost.ToSymbolString()}")
                        };
                        for (int i = 0; i < profile.AlternativeCosts.Count; i++)
                        {
                            SpellCastCostOption cost = profile.AlternativeCosts[i];
                            costOptions.Add(new GameChoiceOption(
                                i + 1,
                                GameChoiceZone.None,
                                pendingCast.PlayerId,
                                -1,
                                true,
                                cost.Label));
                        }

                        GameChoiceRequest request = BeginOptionChoice(
                            GameChoiceKind.SelectOptions,
                            pendingCast.PlayerId,
                            $"Choose a cost for {source.GetCardName()}.",
                            1,
                            1,
                            true,
                            costOptions,
                            result =>
                            {
                                int selected = result?.selectedOptionIds.Count == 1
                                    ? result.selectedOptionIds[0]
                                    : 0;
                                if (pendingCast != null)
                                {
                                    pendingCast.AlternativeCostId =
                                        selected > 0 && selected <= profile.AlternativeCosts.Count
                                            ? profile.AlternativeCosts[selected - 1].Id
                                            : string.Empty;
                                    pendingCast.Stage =
                                        SpellCastDeclarationStage.AdditionalCosts;
                                    ContinuePendingCast();
                                }
                            },
                            CancelPendingCast);
                        return request != null;
                    }
                    pendingCast.Stage = SpellCastDeclarationStage.AdditionalCosts;
                    break;

                case SpellCastDeclarationStage.AdditionalCosts:
                    var requiredCosts = new List<string>();
                    var optionalCosts = new List<SpellCastCostOption>();
                    for (int i = 0; i < profile.AdditionalCosts.Count; i++)
                    {
                        SpellCastCostOption cost = profile.AdditionalCosts[i];
                        if (cost.Optional) optionalCosts.Add(cost);
                        else requiredCosts.Add(cost.Id);
                    }
                    if (optionalCosts.Count > 0)
                    {
                        var options = new List<GameChoiceOption>(optionalCosts.Count);
                        for (int i = 0; i < optionalCosts.Count; i++)
                        {
                            options.Add(new GameChoiceOption(
                                i,
                                GameChoiceZone.None,
                                pendingCast.PlayerId,
                                -1,
                                true,
                                optionalCosts[i].Label));
                        }

                        GameChoiceRequest request = BeginOptionChoice(
                            GameChoiceKind.SelectOptions,
                            pendingCast.PlayerId,
                            $"Choose optional additional costs for {source.GetCardName()}.",
                            0,
                            optionalCosts.Count,
                            true,
                            options,
                            result =>
                            {
                                var selected = new List<string>(requiredCosts);
                                if (result != null)
                                {
                                    for (int i = 0; i < result.selectedOptionIds.Count; i++)
                                    {
                                        int option = result.selectedOptionIds[i];
                                        if (option >= 0 && option < optionalCosts.Count)
                                        {
                                            selected.Add(optionalCosts[option].Id);
                                        }
                                    }
                                }
                                pendingCast?.SetAdditionalCosts(selected);
                                if (pendingCast != null)
                                {
                                    pendingCast.Stage =
                                        SpellCastDeclarationStage.XValue;
                                    ContinuePendingCast();
                                }
                            },
                            CancelPendingCast);
                        return request != null;
                    }
                    pendingCast.SetAdditionalCosts(requiredCosts);
                    pendingCast.Stage = SpellCastDeclarationStage.XValue;
                    break;

                case SpellCastDeclarationStage.XValue:
                    if (profile.UsesX || PendingCastCostContainsX())
                    {
                        int minimumX = profile.UsesX ? profile.MinimumX : 0;
                        int configuredMaximum = profile.UsesX
                            ? profile.MaximumX
                            : GetPotentialManaTotal(pendingCast.PlayerId);
                        int maximumX = GetMaximumPayablePendingCastX(
                            minimumX,
                            configuredMaximum);
                        if (maximumX < minimumX)
                        {
                            CancelPendingCast();
                            return false;
                        }
                        GameChoiceRequest request = BeginNumberChoice(
                            pendingCast.PlayerId,
                            $"Choose X for {source.GetCardName()}.",
                            minimumX,
                            maximumX,
                            true,
                            result =>
                            {
                                if (pendingCast != null)
                                {
                                    pendingCast.XValue = result?.selectedValue ?? minimumX;
                                    pendingCast.Stage = SpellCastDeclarationStage.Targets;
                                    ContinuePendingCast();
                                }
                            },
                            CancelPendingCast);
                        return request != null;
                    }
                    pendingCast.Stage = SpellCastDeclarationStage.Targets;
                    break;

                case SpellCastDeclarationStage.Targets:
                    SpellTargetGroupDefinition group =
                        GetPendingTargetGroup(pendingCast.TargetGroupIndex);
                    if (group != null)
                    {
                        return BeginPendingTargetGroupChoice(group);
                    }
                    if (!TryLockPendingCastCost())
                    {
                        CancelPendingCast();
                        return false;
                    }
                    pendingCast.Stage = SpellCastDeclarationStage.DiscardCost;
                    break;

                case SpellCastDeclarationStage.DiscardCost:
                    return BeginPendingCastDiscardChoice();
                case SpellCastDeclarationStage.SacrificeCost:
                    return BeginPendingCastSacrificeChoice();
                case SpellCastDeclarationStage.ExileCost:
                    return BeginPendingCastExileChoice();
                case SpellCastDeclarationStage.RevealCost:
                    return BeginPendingCastRevealChoice();
                case SpellCastDeclarationStage.Confirmation:
                    return BeginPendingCastConfirmationChoice();
                case SpellCastDeclarationStage.Complete:
                    return true;
            }
        }

        return pendingCast != null;
    }

    private SpellTargetGroupDefinition GetPendingTargetGroup(int index)
    {
        if (pendingCast?.Source == null || index < 0) return null;
        IReadOnlyList<SpellTargetGroupDefinition> groups =
            pendingCast.Source.CastProfile.TargetGroups;
        if (groups.Count > 0)
        {
            return index < groups.Count ? groups[index] : null;
        }

        return index == 0 && RequiresTarget(pendingCast.Source)
            ? new SpellTargetGroupDefinition(
                "legacy",
                $"Choose a target for {pendingCast.Source.GetCardName()}.",
                pendingCast.Source.GetTargetMode(),
                1,
                1,
                string.Empty,
                true)
            : null;
    }

    private bool BeginPendingTargetGroupChoice(SpellTargetGroupDefinition group)
    {
        if (pendingCast == null || group == null || activeChoice != null) return false;
        GameChoiceRequest request = BeginChoice(
            GameChoiceKind.SpellTarget,
            pendingCast.PlayerId,
            group.Prompt,
            group.MinimumTargets,
            group.MaximumTargets,
            true);
        if (request == null) return false;

        request.sourceZone = pendingCast.SourceZone;
        request.sourcePlayerId = pendingCast.SourcePlayerId;
        request.sourceZoneIndex = pendingCast.SourceZoneIndex;
        AddSpellTargetOptions(request, pendingCast.Source, group);
        int legalCount = request.options.FindAll(option => option != null && option.legal).Count;
        if (legalCount >= group.MinimumTargets) return true;

        ClearActiveChoice(GameChoiceKind.SpellTarget);
        return false;
    }

    private bool TryLockPendingCastCost()
    {
        if (!TryBuildPendingCastCost(pendingCast?.XValue ?? 0, out ScriptAbilityCost total))
        {
            return false;
        }
        pendingCast.LockCost(total);
        return CanPayPendingCastCost(total);
    }

    private bool TryBuildPendingCastCost(
        int xValue,
        out ScriptAbilityCost total)
    {
        total = null;
        if (pendingCast == null || pendingCast.Feature == null) return false;
        SpellCastProfile profile = pendingCast.Source.CastProfile;
        if (!string.IsNullOrEmpty(pendingCast.AlternativeCostId))
        {
            SpellCastCostOption alternative =
                profile.FindCost(pendingCast.AlternativeCostId);
            if (alternative == null
                || alternative.Kind != SpellCastCostKind.Alternative)
            {
                return false;
            }
            total = alternative.Cost.Clone();
        }
        else
        {
            total = new ScriptAbilityCost
            {
                Mana = pendingCast.WithoutPayingManaCost
                    ? new ManaCost()
                    : pendingCast.Feature.ManaCost
            };
        }

        for (int i = 0; i < pendingCast.SelectedAdditionalCostIds.Count; i++)
        {
            SpellCastCostOption additional =
                profile.FindCost(pendingCast.SelectedAdditionalCostIds[i]);
            if (additional == null
                || additional.Kind != SpellCastCostKind.Additional)
            {
                return false;
            }
            total = ScriptAbilityCost.Combine(total, additional.Cost);
        }

        total.Mana = ManaCostMath.ResolveX(total.Mana, xValue);
        int increase = Mathf.Max(0, EvaluateScriptRuleValue(
            "SpellCostIncrease",
            pendingCast.PlayerId,
            pendingCast.Source,
            0));
        int reduction = Mathf.Max(0, EvaluateScriptRuleValue(
            "SpellCostReduction",
            pendingCast.PlayerId,
            pendingCast.Source,
            0));
        int minimum = Mathf.Max(0, EvaluateScriptRuleValue(
            "SpellCostMinimum",
            pendingCast.PlayerId,
            pendingCast.Source,
            0));
        total.Mana = ManaCostMath.AddGeneric(total.Mana, increase);
        total.Mana = ManaCostMath.ReduceGeneric(total.Mana, reduction);
        total.Mana = ManaCostMath.EnforceMinimumTotal(total.Mana, minimum);

        if (total.TapSource
            || total.SacrificeSource
            || total.ExileSource
            || total.CounterCount > 0)
        {
            return false;
        }

        return true;
    }

    private bool PendingCastCostContainsX()
    {
        if (pendingCast?.Feature == null) return false;
        SpellCastProfile profile = pendingCast.Source.CastProfile;
        ManaCost unresolved = pendingCast.WithoutPayingManaCost
            ? new ManaCost()
            : pendingCast.Feature.ManaCost;
        if (!string.IsNullOrEmpty(pendingCast.AlternativeCostId))
        {
            SpellCastCostOption alternative =
                profile.FindCost(pendingCast.AlternativeCostId);
            if (alternative != null) unresolved = alternative.Cost.Mana;
        }
        for (int i = 0; i < pendingCast.SelectedAdditionalCostIds.Count; i++)
        {
            SpellCastCostOption additional =
                profile.FindCost(pendingCast.SelectedAdditionalCostIds[i]);
            if (additional != null)
            {
                unresolved = ManaCostMath.Add(unresolved, additional.Cost.Mana);
            }
        }
        return unresolved.XSymbols > 0;
    }

    private int GetMaximumPayablePendingCastX(int minimumX, int configuredMaximum)
    {
        int maximum = Mathf.Max(minimumX, configuredMaximum);
        int result = minimumX - 1;
        for (int x = minimumX; x <= maximum; x++)
        {
            if (!TryBuildPendingCastCost(x, out ScriptAbilityCost cost)
                || !CanPayPendingCastCost(cost))
            {
                break;
            }
            result = x;
        }
        return result;
    }

    private int GetPotentialManaTotal(int playerId)
    {
        ManaPool pool = GetManaPool(playerId);
        int total = pool.Total;
        IReadOnlyList<RuntimeCard> battlefield = playerId == 1
            ? player1Battlefield
            : player2Battlefield;
        for (int i = 0; i < battlefield.Count; i++)
        {
            RuntimeCard card = battlefield[i];
            if (card == null || card.IsTapped) continue;
            for (int abilityIndex = 0; abilityIndex < card.Abilities.Count; abilityIndex++)
            {
                ActivatedAbility ability = card.Abilities[abilityIndex];
                if (ability != null
                    && ability.IsManaAbility
                    && ability.HasProducedMana
                    && ability.IsActivatable(this, card))
                {
                    total += ability.ProducedMana.Total;
                    break;
                }
            }
        }
        return Mathf.Max(0, total);
    }

    private bool CanPayPendingCastCost(ScriptAbilityCost cost)
    {
        if (pendingCast == null || cost == null) return false;
        return CanPayCastingCost(pendingCast.Source, pendingCast.PlayerId,
            pendingCast.SourceZone == GameChoiceZone.Hand, cost);
    }

    private bool CanPayCastingCost(RuntimeCard source, int playerId, bool sourceInHand, ScriptAbilityCost cost)
    {
        if (source == null || cost == null || cost.TapSource || cost.SacrificeSource
            || cost.ExileSource || cost.CounterCount > 0) return false;
        if (cost.Life > 0 && GetLife(playerId) <= cost.Life) return false;

        List<RuntimeCard> hand = GetHandList(playerId);
        int availableHandCards = hand != null ? hand.Count : 0;
        if (sourceInHand) availableHandCards--;
        if (cost.DiscardCount + cost.ExileHandCount > availableHandCards) return false;
        if (cost.RevealHandCount > (hand?.Count ?? 0)) return false;
        if (cost.SacrificePermanentCount
            > CountSacrificeCostCandidates(source, false))
        {
            return false;
        }
        return CanPayWithPoolOrAvailableMana(playerId, cost.Mana);
    }

    private bool BeginPendingCastDiscardChoice()
    {
        ScriptAbilityCost cost = pendingCast?.LockedCost;
        if (cost == null) return false;
        if (cost.DiscardCount <= 0)
        {
            pendingCast.Stage = SpellCastDeclarationStage.SacrificeCost;
            return ContinuePendingCast();
        }

        List<RuntimeCard> candidates = new List<RuntimeCard>(
            GetHandList(pendingCast.PlayerId));
        candidates.Remove(pendingCast.Source);
        if (pendingCast.PlayerId != 1 && !IsOnlineMatch())
        {
            AddAutomaticCards(candidates, cost.DiscardCount, pendingCastPayment.DiscardCards);
            pendingCast.Stage = SpellCastDeclarationStage.SacrificeCost;
            return ContinuePendingCast();
        }

        return BeginOptionChoice(
            GameChoiceKind.SelectOptions,
            pendingCast.PlayerId,
            $"Choose {cost.DiscardCount} card(s) to discard as a casting cost.",
            cost.DiscardCount,
            cost.DiscardCount,
            true,
            CreateCostChoiceOptions(candidates, GameChoiceZone.Hand, pendingCast.PlayerId),
            result =>
            {
                if (TryCollectSelectedCards(
                    candidates,
                    result?.selectedOptionIds,
                    cost.DiscardCount,
                    pendingCastPayment.DiscardCards))
                {
                    pendingCast.Stage = SpellCastDeclarationStage.SacrificeCost;
                    ContinuePendingCast();
                }
            },
            CancelPendingCast) != null;
    }

    private bool BeginPendingCastSacrificeChoice()
    {
        ScriptAbilityCost cost = pendingCast?.LockedCost;
        if (cost == null) return false;
        if (cost.SacrificePermanentCount <= 0)
        {
            pendingCast.Stage = SpellCastDeclarationStage.ExileCost;
            return ContinuePendingCast();
        }

        List<RuntimeCard> candidates =
            GetSacrificeCostCandidates(pendingCast.Source, false);
        if (pendingCast.PlayerId != 1 && !IsOnlineMatch())
        {
            AddAutomaticCards(
                candidates,
                cost.SacrificePermanentCount,
                pendingCastPayment.SacrificePermanents);
            pendingCast.Stage = SpellCastDeclarationStage.ExileCost;
            return ContinuePendingCast();
        }

        return BeginOptionChoice(
            GameChoiceKind.SelectOptions,
            pendingCast.PlayerId,
            $"Choose {cost.SacrificePermanentCount} permanent(s) to sacrifice as a casting cost.",
            cost.SacrificePermanentCount,
            cost.SacrificePermanentCount,
            true,
            CreateCostChoiceOptions(
                candidates,
                GameChoiceZone.Battlefield,
                pendingCast.PlayerId),
            result =>
            {
                if (TryCollectSelectedCards(
                    candidates,
                    result?.selectedOptionIds,
                    cost.SacrificePermanentCount,
                    pendingCastPayment.SacrificePermanents))
                {
                    pendingCast.Stage = SpellCastDeclarationStage.ExileCost;
                    ContinuePendingCast();
                }
            },
            CancelPendingCast) != null;
    }

    private bool BeginPendingCastExileChoice()
    {
        ScriptAbilityCost cost = pendingCast?.LockedCost;
        if (cost == null) return false;
        if (cost.ExileHandCount <= 0)
        {
            pendingCast.Stage = SpellCastDeclarationStage.RevealCost;
            return ContinuePendingCast();
        }

        List<RuntimeCard> candidates = new List<RuntimeCard>(
            GetHandList(pendingCast.PlayerId));
        candidates.Remove(pendingCast.Source);
        candidates.RemoveAll(card => pendingCastPayment.DiscardCards.Contains(card));
        if (pendingCast.PlayerId != 1 && !IsOnlineMatch())
        {
            AddAutomaticCards(candidates, cost.ExileHandCount, pendingCastPayment.ExileHandCards);
            pendingCast.Stage = SpellCastDeclarationStage.RevealCost;
            return ContinuePendingCast();
        }

        return BeginOptionChoice(
            GameChoiceKind.SelectOptions,
            pendingCast.PlayerId,
            $"Choose {cost.ExileHandCount} card(s) to exile as a casting cost.",
            cost.ExileHandCount,
            cost.ExileHandCount,
            true,
            CreateCostChoiceOptions(candidates, GameChoiceZone.Hand, pendingCast.PlayerId),
            result =>
            {
                if (TryCollectSelectedCards(
                    candidates,
                    result?.selectedOptionIds,
                    cost.ExileHandCount,
                    pendingCastPayment.ExileHandCards))
                {
                    pendingCast.Stage = SpellCastDeclarationStage.RevealCost;
                    ContinuePendingCast();
                }
            },
            CancelPendingCast) != null;
    }

    private bool BeginPendingCastRevealChoice()
    {
        ScriptAbilityCost cost = pendingCast?.LockedCost;
        if (cost == null) return false;
        if (cost.RevealHandCount <= 0)
        {
            pendingCast.Stage = SpellCastDeclarationStage.Confirmation;
            return ContinuePendingCast();
        }

        List<RuntimeCard> candidates = new List<RuntimeCard>(
            GetHandList(pendingCast.PlayerId));
        if (pendingCast.PlayerId != 1 && !IsOnlineMatch())
        {
            AddAutomaticCards(candidates, cost.RevealHandCount, pendingCastPayment.RevealHandCards);
            pendingCast.Stage = SpellCastDeclarationStage.Confirmation;
            return ContinuePendingCast();
        }

        return BeginOptionChoice(
            GameChoiceKind.SelectOptions,
            pendingCast.PlayerId,
            $"Choose {cost.RevealHandCount} card(s) to reveal as a casting cost.",
            cost.RevealHandCount,
            cost.RevealHandCount,
            true,
            CreateCostChoiceOptions(candidates, GameChoiceZone.Hand, pendingCast.PlayerId),
            result =>
            {
                if (TryCollectSelectedCards(
                    candidates,
                    result?.selectedOptionIds,
                    cost.RevealHandCount,
                    pendingCastPayment.RevealHandCards))
                {
                    pendingCast.Stage = SpellCastDeclarationStage.Confirmation;
                    ContinuePendingCast();
                }
            },
            CancelPendingCast) != null;
    }

    private bool BeginPendingCastConfirmationChoice()
    {
        if (pendingCast == null || activeChoice != null) return false;
        GameChoiceRequest request = BeginChoice(
            GameChoiceKind.CastConfirmation,
            pendingCast.PlayerId,
            $"Confirm casting {pendingCast.Source.GetCardName()}.",
            0,
            0,
            true);
        if (request == null) return false;
        request.sourceZone = pendingCast.SourceZone;
        request.sourcePlayerId = pendingCast.SourcePlayerId;
        request.sourceZoneIndex = pendingCast.SourceZoneIndex;
        return true;
    }

    private bool IsPendingCastSourceAvailable()
    {
        if (pendingCast == null || !pendingCast.HasCurrentSourceIdentity) return false;
        List<RuntimeCard> zone = pendingCast.SourceZone == GameChoiceZone.Hand
            ? GetHandList(pendingCast.SourcePlayerId)
            : pendingCast.SourceZone == GameChoiceZone.Exile
                ? GetExileList(pendingCast.SourcePlayerId)
                : null;
        return zone != null && zone.Contains(pendingCast.Source);
    }

    private void CancelPendingCast()
    {
        RuntimeCard source = pendingCast?.Source;
        activeChoice = null;
        pendingGenericChoiceCompletion = null;
        pendingGenericChoiceCancellation = null;
        pendingCast = null;
        pendingCastPayment = null;
        source?.ClearSpellTarget();
        source?.ClearCurrentCastDeclaration();
        source?.ClearSpellFeature();
        TryResumeSuspendedStackResolution();
    }

    public bool BeginSpellTargetChoice(int playerID, int handIndex)
    {
        RuntimeCard source = GetHandCard(playerID, handIndex);
        return BeginPendingCast(
            playerID,
            GameChoiceZone.Hand,
            handIndex,
            GetDefaultCastFeature(source));
    }

    public bool BeginExileSpellTargetChoice(int playerID, int exileIndex)
    {
        RuntimeCard source = GetExileCard(playerID, exileIndex);
        return BeginPendingCast(
            playerID,
            GameChoiceZone.Exile,
            exileIndex,
            GetExileCastFeature(source));
    }

    private bool BeginSpellTargetChoice(int playerID, GameChoiceZone sourceZone, int sourceIndex)
    {
        RuntimeCard source = GetCastSourceCard(playerID, sourceZone, sourceIndex);
        return BeginPendingCast(
            playerID,
            sourceZone,
            sourceIndex,
            sourceZone == GameChoiceZone.Exile
                ? GetExileCastFeature(source)
                : GetDefaultCastFeature(source));
    }

    public bool BeginCastConfirmationChoice(int playerID, int handIndex)
    {
        return BeginCastConfirmationChoice(playerID, GameChoiceZone.Hand, handIndex);
    }

    public bool BeginExileCastConfirmationChoice(int playerID, int exileIndex)
    {
        return BeginCastConfirmationChoice(playerID, GameChoiceZone.Exile, exileIndex);
    }

    private bool BeginCastConfirmationChoice(int playerID, GameChoiceZone sourceZone, int sourceIndex)
    {
        RuntimeCard source = GetCastSourceCard(playerID, sourceZone, sourceIndex);
        return BeginPendingCast(
            playerID,
            sourceZone,
            sourceIndex,
            sourceZone == GameChoiceZone.Exile
                ? GetExileCastFeature(source)
                : GetDefaultCastFeature(source));
    }

    public bool ToggleSpellTargetCard(int playerID, RuntimeCard target)
    {
        if (!IsChoosingSpellTarget() || target == null || activeChoice.choosingPlayerId != playerID) return false;
        for (int i = 0; i < activeChoice.options.Count; i++)
        {
            GameChoiceOption option = activeChoice.options[i];
            if (option != null
                && option.objectId == target.ObjectId
                && option.objectZoneChangeVersion == target.ZoneChangeVersion)
            {
                return TryToggleChoiceOption(playerID, option.optionId);
            }
        }
        return false;
    }

    public bool ToggleSpellTargetPlayer(int playerID, int targetPlayerID)
    {
        if (!IsChoosingSpellTarget() || activeChoice.choosingPlayerId != playerID) return false;
        for (int i = 0; i < activeChoice.options.Count; i++)
        {
            GameChoiceOption option = activeChoice.options[i];
            if (option != null
                && option.zone == GameChoiceZone.Player
                && option.ownerPlayerId == targetPlayerID)
            {
                return TryToggleChoiceOption(playerID, option.optionId);
            }
        }
        return false;
    }

    public RuntimeCard GetSelectedSpellTargetCard()
    {
        if (!IsChoosingSpellTarget()) return null;
        for (int i = 0; i < activeChoice.selectedOptionIds.Count; i++)
        {
            GameChoiceOption option =
                activeChoice.FindOption(activeChoice.selectedOptionIds[i]);
            if (option == null || option.objectId <= 0) continue;
            RuntimeCard card = FindRuntimeCard(option.objectId);
            if (card != null && card.ZoneChangeVersion == option.objectZoneChangeVersion)
            {
                return card;
            }
        }
        return null;
    }

    public int GetSelectedSpellTargetPlayer()
    {
        if (!IsChoosingSpellTarget()) return 0;
        for (int i = 0; i < activeChoice.selectedOptionIds.Count; i++)
        {
            GameChoiceOption option =
                activeChoice.FindOption(activeChoice.selectedOptionIds[i]);
            if (option != null && option.zone == GameChoiceZone.Player)
            {
                return option.ownerPlayerId;
            }
        }
        return 0;
    }

    public int GetSelectedSpellTargetCount()
    {
        return IsChoosingSpellTarget() ? activeChoice.selectedOptionIds.Count : 0;
    }

    public bool IsPendingSpellTargetSelected(RuntimeCard target)
    {
        if (!IsChoosingSpellTarget() || target == null) return false;

        for (int i = 0; i < activeChoice.selectedOptionIds.Count; i++)
        {
            GameChoiceOption option =
                activeChoice.FindOption(activeChoice.selectedOptionIds[i]);
            if (option != null
                && option.objectId == target.ObjectId
                && option.objectZoneChangeVersion == target.ZoneChangeVersion)
            {
                return true;
            }
        }

        return false;
    }

    public bool IsPendingSpellTargetPlayerSelected(int playerID)
    {
        if (!IsChoosingSpellTarget()) return false;

        for (int i = 0; i < activeChoice.selectedOptionIds.Count; i++)
        {
            GameChoiceOption option =
                activeChoice.FindOption(activeChoice.selectedOptionIds[i]);
            if (option != null
                && option.zone == GameChoiceZone.Player
                && option.ownerPlayerId == playerID)
            {
                return true;
            }
        }

        return false;
    }

    public bool ConfirmSpellTargetChoice(int playerID)
    {
        if (!IsChoosingSpellTarget()
            || activeChoice.choosingPlayerId != playerID
            || !activeChoice.CanConfirm
            || pendingCast == null)
        {
            return false;
        }

        SpellTargetGroupDefinition group =
            GetPendingTargetGroup(pendingCast.TargetGroupIndex);
        if (group == null) return false;
        var targets = new List<SpellTargetReference>();
        for (int i = 0; i < activeChoice.selectedOptionIds.Count; i++)
        {
            GameChoiceOption option =
                activeChoice.FindOption(activeChoice.selectedOptionIds[i]);
            if (option == null || !option.legal) return false;

            if (option.stackObjectId > 0)
            {
                GameStackItem stackTarget = FindStackItem(option.stackObjectId);
                SpellTargetReference target = SpellTargetReference.ForStackItem(stackTarget);
                if (target == null) return false;
                targets.Add(target);
            }
            else if (option.objectId > 0)
            {
                RuntimeCard card = FindRuntimeCard(option.objectId);
                if (card == null
                    || card.ZoneChangeVersion != option.objectZoneChangeVersion)
                {
                    return false;
                }
                SpellTargetReference target = SpellTargetReference.ForCard(card);
                if (target == null) return false;
                targets.Add(target);
            }
            else if (option.zone == GameChoiceZone.Player)
            {
                SpellTargetReference target =
                    SpellTargetReference.ForPlayer(option.ownerPlayerId);
                if (target == null) return false;
                targets.Add(target);
            }
            else
            {
                return false;
            }
        }

        pendingCast.ReplaceTargets(group.Id, targets);
        pendingCast.TargetGroupIndex++;
        pendingCast.Source.SetSpellTargets(GetPendingCastTargets());
        ClearActiveChoice(GameChoiceKind.SpellTarget);
        return ContinuePendingCast();
    }

    public bool ConfirmCastChoice(int playerID)
    {
        if (!IsConfirmingCast()
            || activeChoice.choosingPlayerId != playerID
            || !activeChoice.CanConfirm
            || pendingCast == null
            || pendingCast.PlayerId != playerID)
        {
            return false;
        }

        ClearActiveChoice(GameChoiceKind.CastConfirmation);
        if (CommitPendingCast()) return true;
        BeginPendingCastConfirmationChoice();
        return false;
    }

    public bool CancelCastChoice(int playerID)
    {
        if (activeChoice == null
            || activeChoice.choosingPlayerId != playerID
            || !activeChoice.mayCancel
            || (activeChoice.kind != GameChoiceKind.SpellTarget
                && activeChoice.kind != GameChoiceKind.CastConfirmation))
        {
            return false;
        }

        CancelPendingCast();
        return true;
    }

    public RuntimeCard GetPendingCastSourceCard()
    {
        if (pendingCast != null) return pendingCast.Source;
        if (!onlineViewOnly || activeChoice == null) return null;
        return GetCastSourceCard(
            activeChoice.sourcePlayerId,
            activeChoice.sourceZone,
            activeChoice.sourceZoneIndex);
    }

    public string GetPendingCastIssue(int playerID)
    {
        if (onlineViewOnly && activeChoice != null
            && activeChoice.choosingPlayerId == playerID
            && (activeChoice.kind == GameChoiceKind.SpellTarget
                || activeChoice.kind == GameChoiceKind.CastConfirmation))
        {
            return GetPendingCastSourceCard() == null
                ? "The projected spell source is unavailable."
                : string.Empty;
        }
        if (pendingCast == null || pendingCast.PlayerId != playerID)
        {
            return "There is no pending spell to cast.";
        }

        return GetCastSourceIssue(
            playerID,
            pendingCast.SourceZone,
            GetCurrentPendingCastSourceIndex());
    }

    public bool CanTargetPendingCastCard(int playerID, RuntimeCard target)
    {
        if (onlineViewOnly)
        {
            return target != null
                && activeChoice != null
                && activeChoice.kind == GameChoiceKind.SpellTarget
                && activeChoice.choosingPlayerId == playerID
                && activeChoice.options != null
                && activeChoice.options.Exists(option => option != null
                    && option.legal
                    && option.objectId == target.ObjectId
                    && option.objectZoneChangeVersion == target.ZoneChangeVersion);
        }
        RuntimeCard source = GetPendingCastSourceCard();
        return source != null
            && pendingCast != null
            && pendingCast.PlayerId == playerID
            && string.IsNullOrEmpty(GetPendingCastIssue(playerID))
            && CanSpellTargetCard(
                source,
                target,
                GetPendingTargetGroup(pendingCast.TargetGroupIndex));
    }

    public bool CanTargetPendingCastPlayer(int playerID, int targetPlayerID)
    {
        if (onlineViewOnly)
        {
            return activeChoice != null
                && activeChoice.kind == GameChoiceKind.SpellTarget
                && activeChoice.choosingPlayerId == playerID
                && activeChoice.options != null
                && activeChoice.options.Exists(option => option != null
                    && option.legal
                    && option.zone == GameChoiceZone.Player
                    && option.ownerPlayerId == targetPlayerID);
        }
        RuntimeCard source = GetPendingCastSourceCard();
        return source != null
            && pendingCast != null
            && pendingCast.PlayerId == playerID
            && string.IsNullOrEmpty(GetPendingCastIssue(playerID))
            && CanSpellTargetPlayer(
                source,
                targetPlayerID,
                GetPendingTargetGroup(pendingCast.TargetGroupIndex));
    }

    private RuntimeCard GetCastSourceCard(int playerID, GameChoiceZone sourceZone, int sourceIndex)
    {
        if (sourceZone == GameChoiceZone.Hand) return GetHandCard(playerID, sourceIndex);
        if (sourceZone == GameChoiceZone.Exile) return GetExileCard(playerID, sourceIndex);
        return null;
    }

    private bool CanCastSourceCard(int playerID, GameChoiceZone sourceZone, int sourceIndex)
    {
        return string.IsNullOrEmpty(GetCastSourceIssue(playerID, sourceZone, sourceIndex));
    }

    private int GetCurrentPendingCastSourceIndex()
    {
        if (pendingCast == null) return -1;
        List<RuntimeCard> zone = pendingCast.SourceZone == GameChoiceZone.Hand
            ? GetHandList(pendingCast.SourcePlayerId)
            : pendingCast.SourceZone == GameChoiceZone.Exile
                ? GetExileList(pendingCast.SourcePlayerId)
                : null;
        return zone?.IndexOf(pendingCast.Source) ?? -1;
    }

    private List<SpellTargetReference> GetPendingCastTargets()
    {
        var result = new List<SpellTargetReference>();
        if (pendingCast == null) return result;
        for (int i = 0; i < pendingCast.Targets.Count; i++)
        {
            SpellTargetReference target = pendingCast.Targets[i]?.Target;
            if (target != null) result.Add(target);
        }
        return result;
    }

    private bool CommitPendingCast()
    {
        if (pendingCast == null
            || pendingCast.LockedCost == null
            || !IsPendingCastSourceAvailable()
            || !ValidatePendingCastTargets()
            || !ValidateScriptAbilityPayment(
                pendingCast.Source,
                pendingCast.LockedCost,
                pendingCastPayment ?? new ScriptAbilityPayment())
            || !CanPayPendingCastCost(pendingCast.LockedCost))
        {
            return false;
        }

        SpellCastDeclaration declaration = pendingCast;
        ScriptAbilityCost cost = declaration.LockedCost;
        ScriptAbilityPayment payment = pendingCastPayment ?? new ScriptAbilityPayment();
        RuntimeCard source = declaration.Source;
        int playerId = declaration.PlayerId;
        List<SpellTargetReference> targets = GetPendingCastTargets();

        if (!TryPrepareCostPayment(source, cost, payment, out PreparedCostPayment prepared))
        {
            return false;
        }
        if (!TryPayMana(playerId, cost.Mana)) return false;
        if (cost.Life > 0)
        {
            if (playerId == 1) player1Life -= cost.Life;
            else player2Life -= cost.Life;
        }

        for (int i = 0; i < payment.RevealHandCards.Count; i++)
        {
            RuntimeCard revealed = payment.RevealHandCards[i];
            PublishGameEvent(new GameEventContext
            {
                type = GameEventType.CardRevealed,
                source = source,
                card = revealed,
                playerId = playerId,
                controllerId = revealed.ControllerPlayerID,
                fromZone = GameZone.Hand,
                toZone = GameZone.Hand
            });
        }
        CommitPreparedCostPayment(source, prepared);

        List<RuntimeCard> sourceZone = declaration.SourceZone == GameChoiceZone.Hand
            ? GetHandList(declaration.SourcePlayerId)
            : GetExileList(declaration.SourcePlayerId);
        int currentIndex = sourceZone?.IndexOf(source) ?? -1;
        if (currentIndex < 0) return false;
        sourceZone.RemoveAt(currentIndex);
        if (declaration.SourceZone == GameChoiceZone.Exile)
        {
            if (source.IsSpellCopy) source.ClearPreparedLink();
            else source.ConsumeAdventurePermission();
        }

        source.MarkZoneChange();
        source.ReassignObjectId(nextObjectId++);
        source.SetController(playerId);
        source.SetCurrentZone(GameZone.Stack);
        source.SetSpellTargets(targets);
        source.SetCurrentCastDeclaration(declaration);
        declaration.Stage = SpellCastDeclarationStage.Complete;

        PushStackItem(GameStackItem.Spell(source, declaration.Feature));
        PublishGameEvent(new GameEventContext
        {
            type = GameEventType.SpellCast,
            source = source,
            card = source,
            playerId = playerId,
            controllerId = playerId,
            fromZone = declaration.SourceZone == GameChoiceZone.Hand
                ? GameZone.Hand
                : GameZone.Exile,
            toZone = GameZone.Stack
        });

        pendingCast = null;
        pendingCastPayment = null;
        Debug.Log($"Player {playerId} puts {source.GetCardName()} on the stack.");
        NotifyStackChanged();
        BeginPriority(playerId);
        TryResumeSuspendedStackResolution();
        return true;
    }

    private bool ValidatePendingCastTargets()
    {
        if (pendingCast == null) return false;
        RuntimeCard source = pendingCast.Source;
        IReadOnlyList<SpellTargetGroupDefinition> groups =
            source.CastProfile.TargetGroups;
        if (groups.Count == 0)
        {
            if (!RequiresTarget(source)) return true;
            groups = new[]
            {
                new SpellTargetGroupDefinition(
                    "legacy",
                    string.Empty,
                    source.GetTargetMode(),
                    1,
                    1,
                    string.Empty,
                    true)
            };
        }

        for (int groupIndex = 0; groupIndex < groups.Count; groupIndex++)
        {
            SpellTargetGroupDefinition group = groups[groupIndex];
            IReadOnlyList<SpellTargetReference> targets =
                pendingCast.GetTargets(group.Id);
            if (targets.Count < group.MinimumTargets
                || targets.Count > group.MaximumTargets)
            {
                return false;
            }

            var distinctCards = new HashSet<long>();
            var distinctPlayers = new HashSet<int>();
            for (int i = 0; i < targets.Count; i++)
            {
                SpellTargetReference target = targets[i];
                bool legal = target != null
                    && ((target.IsCardTarget
                            && target.HasCurrentCardIdentity
                            && CanSpellTargetCard(source, target.Card, group)
                            && MatchesTargetGroupRelations(
                                source,
                                target.Card,
                                0,
                                null,
                                group)
                            && MatchesScriptTargetFilter(
                                source,
                                target.Card,
                                0,
                                group)
                            && (!group.DistinctWithinGroup
                                || distinctCards.Add(target.Card.ObjectId)))
                        || (target.IsPlayerTarget
                            && CanSpellTargetPlayer(source, target.PlayerID, group)
                            && MatchesTargetGroupRelations(
                                source,
                                null,
                                target.PlayerID,
                                null,
                                group)
                            && MatchesScriptTargetFilter(
                                source,
                                null,
                                target.PlayerID,
                                group)
                            && (!group.DistinctWithinGroup
                                || distinctPlayers.Add(target.PlayerID)))
                        || (target.IsStackTarget
                            && IsStackTargetLegal(source, target, group)
                            && (!group.DistinctWithinGroup
                                || distinctCards.Add(-target.StackObjectId))));
                if (!legal) return false;
            }
        }
        return true;
    }

    private string GetCastSourceIssue(int playerID, GameChoiceZone sourceZone, int sourceIndex)
    {
        if (sourceZone == GameChoiceZone.Hand) return GetHandCardPlayIssue(playerID, sourceIndex);
        if (sourceZone == GameChoiceZone.Exile) return GetExileCardCastIssue(playerID, sourceIndex);
        return "That zone cannot be used to cast a spell.";
    }

    private static int GetCastFeatureChoiceOptionId(CardFeatureRuntime feature)
    {
        return 4000000 + feature.FaceIndex * 10000 + feature.FeatureIndex;
    }

    private static CardFeatureRuntime FindFeatureByChoiceOption(
        RuntimeCard card,
        GameChoiceOption option)
    {
        if (card == null || option == null) return null;
        int value = option.optionId - 4000000;
        if (value < 0) return null;
        return card.FindFeature(value / 10000, value % 10000);
    }

    private static string GetFeatureRoleLabel(CardFeatureRole role)
    {
        switch (role)
        {
            case CardFeatureRole.Adventure: return "\u5386\u9669";
            case CardFeatureRole.Omen: return "Omen";
            case CardFeatureRole.Prepare: return "\u5907\u6cd5";
            case CardFeatureRole.RoomDoor: return "\u623f\u95f4";
            case CardFeatureRole.SplitPart: return "\u5206\u5272";
            case CardFeatureRole.ClassLevel: return "\u804c\u4e1a";
            case CardFeatureRole.CaseStage: return "\u6848\u4ef6";
            default: return "\u4e3b\u7279\u5f81";
        }
    }

    private void AddSpellTargetOptions(
        GameChoiceRequest request,
        RuntimeCard source,
        SpellTargetGroupDefinition group)
    {
        if (group.Allows(SpellTargetKind.Permanent)
            && group.Allows(GameZone.Battlefield))
        {
            AddCardTargetOptions(
                request,
                source,
                group,
                player1Battlefield,
                1,
                GameZone.Battlefield);
            AddCardTargetOptions(
                request,
                source,
                group,
                player2Battlefield,
                2,
                GameZone.Battlefield);
        }

        if (group.Allows(SpellTargetKind.Card))
        {
            AddPublicZoneTargetOptions(request, source, group, GameZone.Graveyard);
            AddPublicZoneTargetOptions(request, source, group, GameZone.Exile);
            AddPrivateZoneTargetOptions(request, source, group, GameZone.Hand);
            AddPrivateZoneTargetOptions(request, source, group, GameZone.Library);
        }

        if (group.Allows(SpellTargetKind.Spell)
            || group.Allows(SpellTargetKind.Ability))
        {
            AddStackTargetOptions(request, source, group);
        }

        if (!group.Allows(SpellTargetKind.Player)) return;
        for (int playerID = 1; playerID <= 2; playerID++)
        {
            request.options.Add(new GameChoiceOption(
                request.options.Count + 1,
                GameChoiceZone.Player,
                playerID,
                playerID,
                CanSpellTargetPlayer(source, playerID, group)
                    && MatchesTargetGroupRelations(source, null, playerID, null, group)
                    && MatchesScriptTargetFilter(source, null, playerID, group),
                $"Player {playerID}"));
        }
    }

    private void AddPublicZoneTargetOptions(
        GameChoiceRequest request,
        RuntimeCard source,
        SpellTargetGroupDefinition group,
        GameZone zone)
    {
        if (!group.Allows(zone)) return;
        AddCardTargetOptions(request, source, group, GetZoneCards(1, zone), 1, zone);
        AddCardTargetOptions(request, source, group, GetZoneCards(2, zone), 2, zone);
    }

    private void AddPrivateZoneTargetOptions(
        GameChoiceRequest request,
        RuntimeCard source,
        SpellTargetGroupDefinition group,
        GameZone zone)
    {
        if (!group.Allows(zone) || source == null) return;
        int playerId = source.ControllerPlayerID;
        AddCardTargetOptions(
            request,
            source,
            group,
            GetZoneCards(playerId, zone),
            playerId,
            zone);
    }

    private void AddCardTargetOptions(
        GameChoiceRequest request,
        RuntimeCard source,
        SpellTargetGroupDefinition group,
        IReadOnlyList<RuntimeCard> cards,
        int ownerID,
        GameZone zone)
    {
        for (int i = 0; i < cards.Count; i++)
        {
            RuntimeCard target = cards[i];
            request.options.Add(new GameChoiceOption(
                request.options.Count + 1,
                ToChoiceZone(zone),
                ownerID,
                i,
                CanSpellTargetCard(source, target, group)
                    && MatchesTargetGroupRelations(source, target, 0, null, group)
                    && MatchesScriptTargetFilter(source, target, 0, group),
                target != null && target.Data != null ? target.Data.cardName : string.Empty,
                0,
                1,
                target?.ObjectId ?? 0,
                target?.ZoneChangeVersion ?? -1));
        }
    }

    private void AddStackTargetOptions(
        GameChoiceRequest request,
        RuntimeCard source,
        SpellTargetGroupDefinition group)
    {
        if (!group.Allows(GameZone.Stack)) return;
        for (int i = 0; i < stack.Count; i++)
        {
            GameStackItem item = stack[i];
            if (item == null) continue;
            bool kindAllowed = item.Kind == GameStackItemKind.Spell
                ? group.Allows(SpellTargetKind.Spell)
                : group.Allows(SpellTargetKind.Ability);
            RuntimeCard targetCard = item.SourceCard;
            bool legal = kindAllowed
                && MatchesTargetCardConstraints(source, targetCard, item.ControllerPlayerId, group)
                && MatchesTargetGroupRelations(source, targetCard, 0, item, group)
                && MatchesScriptTargetFilter(source, targetCard, 0, group);
            request.options.Add(new GameChoiceOption(
                request.options.Count + 1,
                GameChoiceZone.Stack,
                item.ControllerPlayerId,
                i,
                legal,
                item.Kind == GameStackItemKind.Spell
                    ? targetCard?.GetCardName() ?? "Spell"
                    : $"{targetCard?.GetCardName() ?? "Ability"} [{item.HookName}]",
                0,
                1,
                0,
                -1,
                item.StackObjectId));
        }
    }

    private bool CanSpellTargetCard(
        RuntimeCard source,
        RuntimeCard target,
        SpellTargetGroupDefinition group)
    {
        if (source == null || target == null || group == null) return false;
        if (!group.Allows(target.CurrentZone)) return false;
        bool permanent = target.CurrentZone == GameZone.Battlefield;
        if (permanent && !group.Allows(SpellTargetKind.Permanent)) return false;
        if (!permanent && !group.Allows(SpellTargetKind.Card)) return false;
        if (!MatchesTargetCardConstraints(source, target, target.ControllerPlayerID, group))
        {
            return false;
        }
        return !permanent
            || TargetingRules.CanSpellTargetCard(
                source,
                target,
                true,
                group.TargetMode == CardTargetMode.None
                    ? CardTargetMode.AnyPermanent
                    : group.TargetMode);
    }

    private bool IsStackTargetLegal(
        RuntimeCard source,
        SpellTargetReference reference,
        SpellTargetGroupDefinition group)
    {
        if (source == null
            || reference == null
            || !reference.IsStackTarget
            || group == null
            || !group.Allows(GameZone.Stack))
        {
            return false;
        }
        GameStackItem item = FindStackItem(reference.StackObjectId);
        if (item == null || item != reference.StackItem) return false;
        bool kindAllowed = item.Kind == GameStackItemKind.Spell
            ? group.Allows(SpellTargetKind.Spell)
            : group.Allows(SpellTargetKind.Ability);
        RuntimeCard targetCard = item.SourceCard;
        return kindAllowed
            && MatchesTargetCardConstraints(
                source,
                targetCard,
                item.ControllerPlayerId,
                group)
            && MatchesTargetGroupRelations(source, targetCard, 0, item, group)
            && MatchesScriptTargetFilter(source, targetCard, 0, group);
    }

    private static bool CanSpellTargetPlayer(
        RuntimeCard source,
        int targetPlayerId,
        SpellTargetGroupDefinition group)
    {
        return group != null
            && group.Allows(SpellTargetKind.Player)
            && MatchesControllerRelation(
                source?.ControllerPlayerID ?? 0,
                targetPlayerId,
                group.ControllerRelation)
            && TargetingRules.CanSpellTargetPlayer(
                source,
                targetPlayerId,
                group.TargetMode == CardTargetMode.None
                    ? CardTargetMode.AnyPlayer
                    : group.TargetMode);
    }

    private bool MatchesTargetCardConstraints(
        RuntimeCard source,
        RuntimeCard target,
        int targetControllerId,
        SpellTargetGroupDefinition group)
    {
        if (source == null || target == null || group == null) return false;
        if (!MatchesControllerRelation(
                source.ControllerPlayerID,
                targetControllerId > 0 ? targetControllerId : target.ControllerPlayerID,
                group.ControllerRelation))
        {
            return false;
        }
        if (group.RequiredCardTypes != CardType.None
            && (target.GetCardTypes() & group.RequiredCardTypes)
                != group.RequiredCardTypes)
        {
            return false;
        }
        if (group.RequiredColors != ManaColorFlags.None
            && (target.GetColors() & group.RequiredColors)
                != group.RequiredColors)
        {
            return false;
        }
        return (group.Relations & SpellTargetRelation.Another) == 0
            || target.ObjectId != source.ObjectId
            || target.ZoneChangeVersion != source.ZoneChangeVersion;
    }

    private static bool MatchesControllerRelation(
        int sourceControllerId,
        int targetControllerId,
        SpellTargetControllerRelation relation)
    {
        if (sourceControllerId <= 0 || targetControllerId <= 0) return false;
        switch (relation)
        {
            case SpellTargetControllerRelation.You:
                return sourceControllerId == targetControllerId;
            case SpellTargetControllerRelation.Opponent:
                return sourceControllerId != targetControllerId;
            default:
                return true;
        }
    }

    private bool MatchesTargetGroupRelations(
        RuntimeCard source,
        RuntimeCard target,
        int targetPlayerId,
        GameStackItem stackItem,
        SpellTargetGroupDefinition group)
    {
        SpellCastDeclaration declaration =
            pendingCast ?? source?.CurrentCastDeclaration;
        if (declaration == null || group == null) return true;
        if ((group.Relations & SpellTargetRelation.DistinctAcrossGroups) != 0
            && DeclarationAlreadyTargets(
                declaration,
                group.Id,
                target,
                targetPlayerId,
                stackItem))
        {
            return false;
        }
        if (string.IsNullOrEmpty(group.RelationGroupId)) return true;

        IReadOnlyList<SpellTargetReference> related =
            declaration.GetTargets(group.RelationGroupId);
        if (related.Count == 0) return false;
        for (int i = 0; i < related.Count; i++)
        {
            SpellTargetReference reference = related[i];
            RuntimeCard relatedCard = GetReferenceCard(reference);
            int relatedController = GetReferenceController(reference);
            int targetController = stackItem != null
                ? stackItem.ControllerPlayerId
                : target != null ? target.ControllerPlayerID : targetPlayerId;

            if ((group.Relations & SpellTargetRelation.SharesCardType) != 0
                && (target == null
                    || relatedCard == null
                    || (target.GetCardTypes() & relatedCard.GetCardTypes()) == 0))
            {
                continue;
            }
            if ((group.Relations & SpellTargetRelation.SharesColor) != 0
                && (target == null
                    || relatedCard == null
                    || (target.GetColors() & relatedCard.GetColors()) == 0))
            {
                continue;
            }
            if ((group.Relations & SpellTargetRelation.SameController) != 0
                && targetController != relatedController)
            {
                continue;
            }
            if ((group.Relations & SpellTargetRelation.DifferentController) != 0
                && targetController == relatedController)
            {
                continue;
            }
            return true;
        }
        return false;
    }

    private static bool DeclarationAlreadyTargets(
        SpellCastDeclaration declaration,
        string currentGroupId,
        RuntimeCard card,
        int playerId,
        GameStackItem stackItem)
    {
        if (declaration == null) return false;
        IReadOnlyList<SpellTargetSelection> selections = declaration.Targets;
        for (int i = 0; i < selections.Count; i++)
        {
            SpellTargetSelection selection = selections[i];
            if (selection == null
                || string.Equals(
                    selection.GroupId,
                    currentGroupId,
                    System.StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            SpellTargetReference reference = selection.Target;
            if (reference == null) continue;
            if (card != null
                && reference.IsCardTarget
                && reference.Card == card
                && reference.CardZoneChangeVersion == card.ZoneChangeVersion)
            {
                return true;
            }
            if (playerId > 0 && reference.IsPlayerTarget && reference.PlayerID == playerId)
            {
                return true;
            }
            if (stackItem != null
                && reference.IsStackTarget
                && reference.StackObjectId == stackItem.StackObjectId)
            {
                return true;
            }
        }
        return false;
    }

    private static RuntimeCard GetReferenceCard(SpellTargetReference reference)
    {
        if (reference == null) return null;
        return reference.IsStackTarget ? reference.StackItem?.SourceCard : reference.Card;
    }

    private static int GetReferenceController(SpellTargetReference reference)
    {
        if (reference == null) return 0;
        if (reference.IsPlayerTarget) return reference.PlayerID;
        if (reference.IsStackTarget) return reference.StackItem?.ControllerPlayerId ?? 0;
        return reference.Card?.ControllerPlayerID ?? 0;
    }

    private static GameChoiceZone ToChoiceZone(GameZone zone)
    {
        switch (zone)
        {
            case GameZone.Library: return GameChoiceZone.Library;
            case GameZone.Hand: return GameChoiceZone.Hand;
            case GameZone.Battlefield: return GameChoiceZone.Battlefield;
            case GameZone.Graveyard: return GameChoiceZone.Graveyard;
            case GameZone.Exile: return GameChoiceZone.Exile;
            case GameZone.Stack: return GameChoiceZone.Stack;
            default: return GameChoiceZone.None;
        }
    }

    private bool MatchesScriptTargetFilter(
        RuntimeCard source,
        RuntimeCard target,
        int targetPlayerId,
        SpellTargetGroupDefinition group)
    {
        if (source == null
            || group == null
            || string.IsNullOrEmpty(group.FilterHookName))
        {
            return true;
        }

        GameEventContext previous = source.ResolvingGameEvent;
        source.SetResolvingGameEvent(new GameEventContext
        {
            source = source,
            card = target,
            playerId = targetPlayerId,
            controllerId = target?.ControllerPlayerID ?? targetPlayerId,
            fromZone = target?.CurrentZone ?? GameZone.None,
            toZone = target?.CurrentZone ?? GameZone.None
        });
        bool matches = CardEffectExecutor.EvaluateCardScriptPredicate(
            this,
            source,
            group.FilterHookName,
            false);
        source.SetResolvingGameEvent(previous);
        return matches;
    }

    private bool TryGetSelectedChoiceOption(GameChoiceKind kind, out GameChoiceOption option)
    {
        option = null;
        if (activeChoice == null
            || activeChoice.kind != kind
            || activeChoice.selectedOptionIds.Count != 1)
        {
            return false;
        }

        option = activeChoice.FindOption(activeChoice.selectedOptionIds[0]);
        return option != null && option.legal;
    }

    private bool TryGetBattlefieldLocation(RuntimeCard card, out int ownerID, out int battlefieldIndex)
    {
        ownerID = 1;
        battlefieldIndex = player1Battlefield.IndexOf(card);
        if (battlefieldIndex >= 0) return true;

        ownerID = 2;
        battlefieldIndex = player2Battlefield.IndexOf(card);
        return battlefieldIndex >= 0;
    }

    private static int GetBattlefieldChoiceOptionId(int ownerID, int battlefieldIndex)
    {
        return ownerID * 1000000 + battlefieldIndex;
    }

    private static int GetPlayerChoiceOptionId(int playerID)
    {
        return -playerID;
    }

    private GameChoiceRequest BeginChoice(
        GameChoiceKind kind,
        int playerID,
        string prompt,
        int minimumSelections,
        int maximumSelections,
        bool mayCancel)
    {
        if (activeChoice != null) return null;

        activeChoice = new GameChoiceRequest
        {
            requestId = nextChoiceRequestId++,
            kind = kind,
            selectionMode = kind == GameChoiceKind.TriggerOrder
                || kind == GameChoiceKind.OrderCards
                ? GameChoiceSelectionMode.Ordered
                : GameChoiceSelectionMode.Unordered,
            choosingPlayerId = playerID,
            prompt = prompt ?? string.Empty,
            minimumSelections = Mathf.Max(0, minimumSelections),
            maximumSelections = Mathf.Max(0, maximumSelections),
            mayCancel = mayCancel
        };
        return activeChoice;
    }

    private void ClearActiveChoice(GameChoiceKind expectedKind)
    {
        if (activeChoice != null && activeChoice.kind == expectedKind)
        {
            activeChoice = null;
        }
    }

    public IReadOnlyList<RuntimeCard> GetDeckCards(int playerID)
    {
        if (playerID == 1) return player1Library;
        if (playerID == 2) return player2Library;
        return emptyCards;
    }

    public bool CanPlayerInspectZone(int viewerPlayerID, int ownerPlayerID, GameZone zone)
    {
        if ((viewerPlayerID != 1 && viewerPlayerID != 2)
            || (ownerPlayerID != 1 && ownerPlayerID != 2))
        {
            return false;
        }

        switch (zone)
        {
            case GameZone.Hand:
                return viewerPlayerID == ownerPlayerID;
            case GameZone.Library:
                return IsChoosingLibraryCard()
                    && activeChoice.choosingPlayerId == viewerPlayerID
                    && viewerPlayerID == ownerPlayerID;
            case GameZone.Battlefield:
            case GameZone.Graveyard:
            case GameZone.Exile:
            case GameZone.Stack:
            case GameZone.Command:
                return true;
            default:
                return false;
        }
    }

    public IReadOnlyList<RuntimeCard> GetVisibleZoneCards(
        int viewerPlayerID,
        int ownerPlayerID,
        GameZone zone)
    {
        return CanPlayerInspectZone(viewerPlayerID, ownerPlayerID, zone)
            ? GetZoneCards(ownerPlayerID, zone)
            : emptyCards;
    }

    public bool CanScriptInspectZone(
        RuntimeCard source,
        int ownerPlayerID,
        GameZone zone)
    {
        if (source == null) return false;
        switch (zone)
        {
            case GameZone.Hand:
                return source.ControllerPlayerID == ownerPlayerID;
            case GameZone.Library:
                return source.ControllerPlayerID == ownerPlayerID
                    && source.ResolvingGameEvent != null;
            case GameZone.Battlefield:
            case GameZone.Graveyard:
            case GameZone.Exile:
            case GameZone.Stack:
            case GameZone.Command:
                return true;
            default:
                return false;
        }
    }

    public IReadOnlyList<RuntimeCard> GetGraveyardCards(int playerID)
    {
        if (playerID == 1) return player1Graveyard;
        if (playerID == 2) return player2Graveyard;
        return emptyCards;
    }

    public IReadOnlyList<RuntimeCard> GetExileCards(int playerID)
    {
        if (playerID == 1) return player1Exile;
        if (playerID == 2) return player2Exile;
        return emptyCards;
    }

    public RuntimeCard GetExileCard(int playerID, int exileIndex)
    {
        List<RuntimeCard> exile = GetExileList(playerID);
        if (exile == null || exileIndex < 0 || exileIndex >= exile.Count) return null;
        return exile[exileIndex];
    }

    public bool HasPendingStack()
    {
        return onlineViewOnly ? stackCardView.Count > 0 : stack.Count > 0;
    }

    private bool PushStackItem(GameStackItem item)
    {
        if (item == null) return false;
        if (item.StackObjectId <= 0)
        {
            item.AssignStackObjectId(nextStackObjectId++);
        }
        stack.Add(item);
        return true;
    }

    private GameStackItem FindStackItem(long stackObjectId)
    {
        if (stackObjectId <= 0) return null;
        for (int i = 0; i < stack.Count; i++)
        {
            GameStackItem item = stack[i];
            if (item != null && item.StackObjectId == stackObjectId) return item;
        }
        return null;
    }

    public bool CanPlayCard(int player, int handIndex)
    {
        return TryGetCanPlayCardReason(player, handIndex, out _);
    }

    public string GetHandCardPlayIssue(int player, int handIndex)
    {
        return TryGetCanPlayCardReason(player, handIndex, out string reason) ? string.Empty : reason;
    }

    public bool TryGetCanPlayCardReason(int player, int handIndex, out string reason)
    {
        List<RuntimeCard> hand = player == 1 ? player1Hand : player2Hand;
        reason = string.Empty;
        if (player != 1 && player != 2)
        {
            reason = "Invalid player.";
            return false;
        }

        if (IsGameOver())
        {
            reason = matchOutcome.message;
            return false;
        }

        if (activeChoice != null)
        {
            bool confirmingThisCard =
                (activeChoice.kind == GameChoiceKind.SpellTarget
                    || activeChoice.kind == GameChoiceKind.CastConfirmation)
                && activeChoice.sourcePlayerId == player
                && activeChoice.sourceZone == GameChoiceZone.Hand
                && activeChoice.sourceZoneIndex == handIndex;
            if (!confirmingThisCard)
            {
                reason = $"Player {activeChoice.choosingPlayerId} must finish the current choice first.";
                return false;
            }
        }

        if (IsDiscardingToHandSize())
        {
            reason = $"Player {GetDiscardToHandSizePlayer()} must discard down to hand size first.";
            return false;
        }

        if (IsChoosingLibraryCard())
        {
            reason = $"Player {GetLibrarySearchPlayer()} must finish searching their library first.";
            return false;
        }

        if (handIndex < 0 || handIndex >= hand.Count)
        {
            reason = "No card at that hand position.";
            return false;
        }

        if (!HasPriority(player))
        {
            reason = priorityState.IsOpen
                ? $"Player {priorityState.PlayerWithPriority} has priority now."
                : "No player has priority right now.";
            return false;
        }

        RuntimeCard card = hand[handIndex];
        if (onlineViewOnly)
        {
            if (card.OnlineCanPlayFromHand) return true;
            reason = "The authoritative match state does not allow this card to be played now.";
            return false;
        }
        if (card.SpellFeature != null)
        {
            return TryGetCanPlayFeatureReason(player, handIndex, card.SpellFeature, out reason);
        }

        IReadOnlyList<CardFeatureRuntime> playable = GetCastFeaturesFromHand(card);
        if (playable.Count == 0)
        {
            reason = $"{card.Data.cardName} has no feature that can be cast from hand.";
            return false;
        }

        string firstIssue = string.Empty;
        for (int i = 0; i < playable.Count; i++)
        {
            if (TryGetCanPlayFeatureReason(player, handIndex, playable[i], out string featureIssue))
            {
                return true;
            }

            if (string.IsNullOrEmpty(firstIssue)) firstIssue = featureIssue;
        }

        reason = firstIssue;
        return false;
    }

    private IReadOnlyList<CardFeatureRuntime> GetCastFeaturesFromHand(RuntimeCard card)
    {
        var result = new List<CardFeatureRuntime>();
        if (card == null) return result;

        bool hasSplitParts = card.HasFeatureRole(CardFeatureRole.SplitPart);
        bool hasRoomDoors = card.HasFeatureRole(CardFeatureRole.RoomDoor);
        for (int i = 0; i < card.Features.Count; i++)
        {
            CardFeatureRuntime feature = card.Features[i];
            if (feature.CanCastFromHand(hasSplitParts, hasRoomDoors))
            {
                result.Add(feature);
            }
        }

        return result;
    }

    private CardFeatureRuntime GetDefaultCastFeature(RuntimeCard card)
    {
        if (card == null) return null;
        if (card.SpellFeature != null) return card.SpellFeature;

        IReadOnlyList<CardFeatureRuntime> castFeatures = GetCastFeaturesFromHand(card);
        return castFeatures.Count > 0 ? castFeatures[0] : card.PrimaryFeature;
    }

    private bool TryGetCanPlayFeatureReason(
        int player,
        int handIndex,
        CardFeatureRuntime feature,
        out string reason)
    {
        reason = string.Empty;
        RuntimeCard card = GetHandCard(player, handIndex);
        if (card == null || feature == null || !ReferenceEquals(feature.Card, card.Data))
        {
            reason = "The selected card feature is unavailable.";
            return false;
        }

        CardFeatureRuntime previousFeature = card.SpellFeature;
        card.SetSpellFeature(feature);
        bool result = TryGetCanPlaySelectedFeatureReason(player, card, feature, out reason);
        if (previousFeature != null) card.SetSpellFeature(previousFeature);
        else card.ClearSpellFeature();
        return result;
    }

    private bool TryGetCanPlaySelectedFeatureReason(
        int player,
        RuntimeCard card,
        CardFeatureRuntime feature,
        out string reason)
    {
        reason = string.Empty;
        if (feature.HasType(CardType.Land))
        {
            if (!IsSorcerySpeedWindow(player))
            {
                reason = "Lands can only be played during your main phase while the stack is empty.";
                return false;
            }

            int landPlayLimit = Mathf.Max(0, EvaluateScriptRuleValue(
                "LandPlaysPerTurn",
                player,
                card,
                maxLandsPerTurn));
            if (landsPlayedThisTurn >= landPlayLimit)
            {
                reason = "You have already played a land this turn.";
                return false;
            }

            return true;
        }

        ManaCost manaCost = feature.ManaCost;
        if (!CanPayAnyDeclaredManaCost(player, card, feature))
        {
            reason = $"Not enough mana for {feature.CardName}. Need {manaCost.ToSymbolString()}, pool has {GetManaPool(player).ToDisplayString()}.";
            return false;
        }

        bool timingAllowed = EvaluateScriptRulePermission(
            "CastAsThoughFlash",
            player,
            card,
            feature.HasType(CardType.Instant) || IsSorcerySpeedWindow(player));
        if (!timingAllowed)
        {
            reason = $"{feature.CardName} can only be cast during your main phase while the stack is empty.";
            return false;
        }

        if (RequiresTarget(card) && !HasAnyLegalTarget(card))
        {
            reason = $"{feature.CardName} has no legal {GetTargetModeLabel(card.GetTargetMode())} target.";
            return false;
        }

        return true;
    }

    private bool CanPayAnyDeclaredManaCost(
        int playerId,
        RuntimeCard card,
        CardFeatureRuntime feature)
    {
        if (card == null || feature == null) return false;
        int x = card.CastProfile.UsesX ? card.CastProfile.MinimumX : 0;
        var candidates = new List<ScriptAbilityCost>
        {
            new ScriptAbilityCost { Mana = feature.ManaCost }
        };
        for (int i = 0; i < card.CastProfile.AlternativeCosts.Count; i++)
        {
            candidates.Add(card.CastProfile.AlternativeCosts[i].Cost.Clone());
        }

        for (int candidateIndex = 0; candidateIndex < candidates.Count; candidateIndex++)
        {
            ScriptAbilityCost total = candidates[candidateIndex];
            for (int i = 0; i < card.CastProfile.AdditionalCosts.Count; i++)
            {
                SpellCastCostOption additional = card.CastProfile.AdditionalCosts[i];
                if (!additional.Optional)
                {
                    total = ScriptAbilityCost.Combine(total, additional.Cost);
                }
            }

            int increase = Mathf.Max(0, EvaluateScriptRuleValue(
                "SpellCostIncrease",
                playerId,
                card,
                0));
            int reduction = Mathf.Max(0, EvaluateScriptRuleValue(
                "SpellCostReduction",
                playerId,
                card,
                0));
            int minimum = Mathf.Max(0, EvaluateScriptRuleValue(
                "SpellCostMinimum",
                playerId,
                card,
                0));
            ManaCost candidate = ManaCostMath.ResolveX(total.Mana, x);
            candidate = ManaCostMath.AddGeneric(candidate, increase);
            candidate = ManaCostMath.ReduceGeneric(candidate, reduction);
            candidate = ManaCostMath.EnforceMinimumTotal(candidate, minimum);
            total.Mana = candidate;
            if (CanPayCastingCost(card, playerId, card.CurrentZone == GameZone.Hand, total)) return true;
        }
        return false;
    }

    public RuntimeCard GetHandCard(int player, int handIndex)
    {
        List<RuntimeCard> hand = player == 1 ? player1Hand : player2Hand;
        if (handIndex < 0 || handIndex >= hand.Count) return null;
        return hand[handIndex];
    }

    public IReadOnlyList<RuntimeCard> GetHandCards(int player)
    {
        if (player == 1) return player1Hand;
        if (player == 2) return player2Hand;
        return emptyCards;
    }

    public IReadOnlyList<RuntimeCard> GetBattlefieldCards(int player)
    {
        if (player == 1) return player1Battlefield;
        if (player == 2) return player2Battlefield;
        return emptyCards;
    }

    public RuntimeCard GetBattlefieldCard(int player, int battlefieldIndex)
    {
        List<RuntimeCard> battlefield = player == 1 ? player1Battlefield : player2Battlefield;
        if (battlefieldIndex < 0 || battlefieldIndex >= battlefield.Count) return null;
        return battlefield[battlefieldIndex];
    }

    private int GetBattlefieldIndex(RuntimeCard card)
    {
        if (card == null) return -1;
        int index = player1Battlefield.IndexOf(card);
        return index >= 0 ? index : player2Battlefield.IndexOf(card);
    }

    public bool CanDeclareAttacker(int player, int battlefieldIndex)
    {
        if (player != currentPlayer
            || currentPhase != TurnPhase.DeclareAttackers
            || HasPendingStack()
            || !HasPriority(player)) return false;

        RuntimeCard card = GetBattlefieldCard(player, battlefieldIndex);
        if (onlineViewOnly) return card?.OnlineCanDeclareAttacker == true;
        return CanAttack(card);
    }

    public bool IsChoosingAttackers()
    {
        return currentPhase == TurnPhase.DeclareAttackers
            && !HasPendingStack()
            && (IsOnlineMatch() || currentPlayer == 1);
    }

    public bool IsPendingAttacker(RuntimeCard card)
    {
        return card != null
            && (onlineViewOnly ? card.OnlinePendingAttacker : pendingAttackers.Contains(card));
    }

    public int GetPendingAttackerCount()
    {
        if (onlineViewOnly)
        {
            int count = 0;
            for (int i = 0; i < player1Battlefield.Count; i++)
                if (player1Battlefield[i]?.OnlinePendingAttacker == true) count++;
            for (int i = 0; i < player2Battlefield.Count; i++)
                if (player2Battlefield[i]?.OnlinePendingAttacker == true) count++;
            return count;
        }
        return pendingAttackers.Count;
    }

    public bool HasDeclaredAttackers()
    {
        return HasAttackingCreatures();
    }

    public bool TogglePendingAttacker(int player, int battlefieldIndex)
    {
        if (player != currentPlayer
            || currentPhase != TurnPhase.DeclareAttackers
            || HasPendingStack()
            || !HasPriority(player)) return false;

        RuntimeCard card = GetBattlefieldCard(player, battlefieldIndex);
        if (card == null) return false;

        if (pendingAttackers.Remove(card))
        {
            pendingAttackTargets.Remove(card);
            return true;
        }
        if (!CanDeclareAttacker(player, battlefieldIndex)) return false;

        pendingAttackers.Add(card);
        pendingAttackTargets[card] = GetDefendingPlayer(card.ControllerPlayerID);
        return true;
    }

    public object GetPendingAttackerTarget(RuntimeCard attacker)
    {
        if (attacker == null) return null;
        if (onlineViewOnly)
        {
            if (attacker.OnlinePendingAttackTargetPlayerId > 0)
                return attacker.OnlinePendingAttackTargetPlayerId;
            if (attacker.OnlinePendingAttackTargetObjectId > 0)
                return FindRuntimeCard(attacker.OnlinePendingAttackTargetObjectId);
            return null;
        }
        return pendingAttackTargets.TryGetValue(attacker, out object target)
            ? target
            : null;
    }

    public bool SetPendingAttackerTarget(
        int player,
        RuntimeCard attacker,
        RuntimeCard permanentTarget,
        int playerTarget)
    {
        if (currentPhase != TurnPhase.DeclareAttackers
            || player != currentPlayer
            || attacker == null
            || attacker.ControllerPlayerID != player
            || !pendingAttackers.Contains(attacker)
            || HasPendingStack()
            || !HasPriority(player))
        {
            return false;
        }

        object target = permanentTarget != null
            ? permanentTarget
            : (object)playerTarget;
        if (!CanAttackTarget(attacker, target)) return false;

        pendingAttackTargets[attacker] = target;
        return true;
    }

    public bool CommitPendingAttackers()
    {
        if (currentPhase != TurnPhase.DeclareAttackers || HasPendingStack()) return false;

        bool declaredAny = false;
        for (int i = 0; i < pendingAttackers.Count; i++)
        {
            RuntimeCard attacker = pendingAttackers[i];
            if (!CanAttack(attacker)) continue;

            pendingAttackTargets.TryGetValue(attacker, out object attackTarget);
            DeclareAttackerCard(attacker, attackTarget);
            declaredAny = true;
        }

        pendingAttackers.Clear();
        pendingAttackTargets.Clear();
        return declaredAny;
    }

    public bool CanDeclareBlocker(int player, int battlefieldIndex, RuntimeCard targetAttacker)
    {
        if (player == currentPlayer
            || currentPhase != TurnPhase.DeclareBlockers
            || HasPendingStack()
            || !HasPriority(player)) return false;

        RuntimeCard blocker = GetBattlefieldCard(player, battlefieldIndex);
        if (onlineViewOnly)
            return blocker != null
                && targetAttacker != null
                && blocker.IsOnlineLegalBlockTarget(targetAttacker.ObjectId);
        return CanBlockAttacker(blocker, targetAttacker);
    }

    public bool CanCancelBlocker(int player, int battlefieldIndex)
    {
        if (!IsChoosingBlockers()) return false;
        if (player == currentPlayer) return false;
        if (!HasPriority(player)) return false;

        RuntimeCard blocker = GetBattlefieldCard(player, battlefieldIndex);
        if (onlineViewOnly) return blocker?.OnlineCanCancelBlocker == true;
        return blocker != null && blocker.IsBlocking && blocker.BlockingTarget != null;
    }

    public int GetCombatDamagePreviewToCard(RuntimeCard target)
    {
        if (target == null) return 0;
        if (currentPhase != TurnPhase.DeclareBlockers && currentPhase != TurnPhase.CombatDamage) return 0;

        int damage = 0;
        if (CombatHasFirstStrikeStep())
        {
            damage += SumAssignedDamageToCard(target, BuildCombatDamageAssignments(true));
        }

        damage += SumAssignedDamageToCard(target, BuildCombatDamageAssignments(false));
        return damage;
    }

    private static int SumAssignedDamageToCard(RuntimeCard target, List<CombatDamageAssignment> assignments)
    {
        int damage = 0;
        for (int i = 0; i < assignments.Count; i++)
        {
            CombatDamageAssignment assignment = assignments[i];
            if (ReferenceEquals(assignment.CardTarget, target)) damage += assignment.Damage;
        }

        return damage;
    }

    public bool IsChoosingBlockers()
    {
        return currentPhase == TurnPhase.DeclareBlockers
            && !HasPendingStack()
            && (IsOnlineMatch() || currentPlayer != 1);
    }

    public bool CanDeclareBlockerAgainstAnyAttacker(int player, int battlefieldIndex)
    {
        if (!IsChoosingBlockers() || player == currentPlayer) return false;

        RuntimeCard blocker = GetBattlefieldCard(player, battlefieldIndex);
        if (blocker == null) return false;
        if (onlineViewOnly) return blocker.HasOnlineLegalBlockTarget;

        IReadOnlyList<RuntimeCard> attackers = GetBattlefieldCards(currentPlayer);
        for (int i = 0; i < attackers.Count; i++)
        {
            if (CanBlockAttacker(blocker, attackers[i])) return true;
        }

        return false;
    }

    public bool CanAnyHumanBlockerBlock(RuntimeCard targetAttacker)
    {
        if (!IsChoosingBlockers()
            || targetAttacker == null
            || targetAttacker.ControllerPlayerID != currentPlayer)
        {
            return false;
        }

        IReadOnlyList<RuntimeCard> blockers = GetBattlefieldCards(OnlinePlayerContext.LocalPlayerId);
        for (int i = 0; i < blockers.Count; i++)
        {
            if (CanDeclareBlocker(OnlinePlayerContext.LocalPlayerId, i, targetAttacker)) return true;
        }

        return false;
    }

    private bool CanAttack(RuntimeCard card)
    {
        if (card == null || !card.HasType(CardType.Creature)) return false;
        if (card.IsTapped || card.IsAttacking || card.IsBlocking) return false;
        if (card.HasKeyword("Defender")) return false;
        if (card.TurnEnteredBattlefield == turnNumber && !card.HasKeyword("Haste")) return false;
        return true;
    }

    private static int GetDefendingPlayer(int attackingPlayer)
    {
        return attackingPlayer == 1 ? 2 : attackingPlayer == 2 ? 1 : 0;
    }

    public bool CanAttackTarget(RuntimeCard attacker, object target)
    {
        if (onlineViewOnly)
        {
            if (attacker == null
                || !attacker.OnlinePendingAttacker
                || !attacker.OnlineCanDeclareAttacker)
            {
                return false;
            }
            if (target is int onlinePlayerTarget)
                return onlinePlayerTarget == attacker.OnlineLegalAttackTargetPlayerId;
            return target is RuntimeCard permanentTarget
                && attacker.IsOnlineLegalAttackTarget(permanentTarget.ObjectId);
        }
        if (attacker == null || !CanAttack(attacker) && !pendingAttackers.Contains(attacker))
        {
            return false;
        }

        int defendingPlayer = GetDefendingPlayer(attacker.ControllerPlayerID);
        if (target is int playerTarget)
        {
            return playerTarget == defendingPlayer;
        }

        if (!(target is RuntimeCard permanent)
            || !IsOnBattlefield(permanent))
        {
            return false;
        }

        if (permanent.HasType(CardType.Planeswalker))
        {
            return permanent.ControllerPlayerID == defendingPlayer;
        }

        return permanent.HasType(CardType.Battle)
            && permanent.BattleProtectorPlayerID == defendingPlayer;
    }

    private static bool CanBlockAttacker(RuntimeCard blocker, RuntimeCard targetAttacker)
    {
        if (blocker == null || targetAttacker == null) return false;
        if (!blocker.HasType(CardType.Creature) || !targetAttacker.HasType(CardType.Creature)) return false;
        if (!targetAttacker.IsAttacking || blocker.IsTapped || blocker.IsBlocking || blocker.IsAttacking) return false;
        if (targetAttacker.HasKeyword("Unblockable")
            || targetAttacker.HasKeyword("CantBeBlocked")
            || targetAttacker.HasKeyword("Can't Be Blocked"))
        {
            return false;
        }

        bool attackerHasFlying = targetAttacker.HasKeyword("Flying");
        bool blockerCanBlockFlying = blocker.HasKeyword("Flying") || blocker.HasKeyword("Reach");
        if (attackerHasFlying && !blockerCanBlockFlying) return false;
        if (TargetingRules.IsProtectedFrom(targetAttacker, blocker)) return false;
        return true;
    }

    public bool CanTargetCard(RuntimeCard source, RuntimeCard target)
    {
        return TargetingRules.CanTargetCard(source, target, IsOnBattlefield(target));
    }

    public bool IsCardOnBattlefield(RuntimeCard card)
    {
        return IsOnBattlefield(card);
    }

    public bool CanAttachToCard(RuntimeCard source, RuntimeCard target)
    {
        return source != null
            && target != null
            && IsOnBattlefield(source)
            && IsOnBattlefield(target)
            && TargetingRules.CanAttachToCard(source, target);
    }

    public bool AttachCard(RuntimeCard source, RuntimeCard target)
    {
        return CanAttachToCard(source, target) && source.AttachTo(target);
    }

    public bool DetachCard(RuntimeCard source)
    {
        return source != null && IsOnBattlefield(source) && source.Detach();
    }

    public bool RequiresTarget(RuntimeCard card)
    {
        if (card == null || card.Data == null) return false;
        IReadOnlyList<SpellTargetGroupDefinition> groups = card.CastProfile.TargetGroups;
        for (int i = 0; i < groups.Count; i++)
        {
            if (groups[i] != null && groups[i].MaximumTargets > 0) return true;
        }
        return card.GetTargetMode() != CardTargetMode.None;
    }

    private static string GetTargetModeLabel(CardTargetMode mode)
    {
        switch (mode)
        {
            case CardTargetMode.AnyCreature: return "creature";
            case CardTargetMode.OpponentCreature: return "opponent creature";
            case CardTargetMode.FriendlyCreature: return "friendly creature";
            case CardTargetMode.AnyPermanent: return "permanent";
            case CardTargetMode.OpponentPermanent: return "opponent permanent";
            case CardTargetMode.FriendlyPermanent: return "friendly permanent";
            case CardTargetMode.AnyPlayer: return "player";
            case CardTargetMode.OpponentPlayer: return "opponent player";
            case CardTargetMode.AnyCreatureOrPlayer: return "creature or player";
            case CardTargetMode.OpponentCreatureOrPlayer: return "opponent creature or player";
            default: return "spell";
        }
    }

    public bool CanTargetHandCardCard(int player, int handIndex, RuntimeCard target)
    {
        RuntimeCard source = GetHandCard(player, handIndex);
        return source != null
            && CanPlayCard(player, handIndex)
            && CanSpellTargetCard(source, target);
    }

    public bool CanTargetHandCardPlayer(int player, int handIndex, int targetPlayerID)
    {
        RuntimeCard source = GetHandCard(player, handIndex);
        return source != null
            && CanPlayCard(player, handIndex)
            && CanSpellTargetPlayer(source, targetPlayerID);
    }

    public RuntimeCard GetTargetCard(RuntimeCard source)
    {
        return GetTargetCardAt(source, 1);
    }

    public int GetTargetPlayer(RuntimeCard source)
    {
        return GetTargetPlayerAt(source, 1);
    }

    public int GetTargetCount(RuntimeCard source)
    {
        return source?.SpellTargets.Count ?? 0;
    }

    public RuntimeCard GetTargetCardAt(RuntimeCard source, int oneBasedIndex)
    {
        int index = oneBasedIndex - 1;
        if (source == null || index < 0 || index >= source.SpellTargets.Count) return null;

        SpellTargetReference target = source.SpellTargets[index];
        return target != null && target.HasCurrentCardIdentity ? target.Card : null;
    }

    public int GetTargetPlayerAt(RuntimeCard source, int oneBasedIndex)
    {
        int index = oneBasedIndex - 1;
        if (source == null || index < 0 || index >= source.SpellTargets.Count) return 0;

        SpellTargetReference target = source.SpellTargets[index];
        return target != null && target.IsPlayerTarget ? target.PlayerID : 0;
    }

    public bool AddSpellTarget(RuntimeCard source, RuntimeCard target)
    {
        return source != null
            && CanSpellTargetCard(source, target)
            && source.AddSpellTarget(target);
    }

    public bool AddSpellTargetPlayer(RuntimeCard source, int targetPlayerID)
    {
        return source != null
            && CanSpellTargetPlayer(source, targetPlayerID)
            && source.AddSpellTargetPlayer(targetPlayerID);
    }

    public int GetController(RuntimeCard card)
    {
        return card?.ControllerPlayerID ?? 0;
    }

    public bool ChangeController(RuntimeCard card, int playerID)
    {
        if (card == null
            || (playerID != 1 && playerID != 2)
            || card.ControllerPlayerID == playerID
            || !IsOnBattlefield(card))
        {
            return false;
        }

        List<RuntimeCard> previousBattlefield = card.ControllerPlayerID == 1
            ? player1Battlefield
            : player2Battlefield;
        List<RuntimeCard> nextBattlefield = playerID == 1
            ? player1Battlefield
            : player2Battlefield;
        if (!previousBattlefield.Remove(card)) return false;

        card.RemoveFromCombat();
        card.SetController(playerID);
        nextBattlefield.Add(card);
        return true;
    }

    private bool HasAnyLegalTarget(RuntimeCard source)
    {
        if (source == null || source.Data == null) return false;
        if (!RequiresTarget(source)) return true;

        IReadOnlyList<SpellTargetGroupDefinition> groups =
            source.CastProfile.TargetGroups;
        if (groups.Count > 0)
        {
            for (int groupIndex = 0; groupIndex < groups.Count; groupIndex++)
            {
                SpellTargetGroupDefinition group = groups[groupIndex];
                int legalCount = CountLegalTargets(source, group);
                if (legalCount < group.MinimumTargets) return false;
            }
            return true;
        }

        if (CanSpellTargetPlayer(source, 1) || CanSpellTargetPlayer(source, 2)) return true;
        if (BattlefieldHasLegalTarget(source, player1Battlefield)) return true;
        if (BattlefieldHasLegalTarget(source, player2Battlefield)) return true;
        return false;
    }

    private int CountLegalTargets(
        RuntimeCard source,
        SpellTargetGroupDefinition group)
    {
        if (source == null || group == null) return 0;
        var request = new GameChoiceRequest();
        AddSpellTargetOptions(request, source, group);
        int count = 0;
        for (int i = 0; i < request.options.Count; i++)
        {
            if (request.options[i] != null && request.options[i].legal) count++;
        }
        return count;
    }

    private bool BattlefieldHasLegalTarget(RuntimeCard source, List<RuntimeCard> battlefield)
    {
        for (int i = 0; i < battlefield.Count; i++)
        {
            if (CanSpellTargetCard(source, battlefield[i])) return true;
        }

        return false;
    }

    private bool CanSpellTargetCard(RuntimeCard source, RuntimeCard target)
    {
        return TargetingRules.CanSpellTargetCard(source, target, IsOnBattlefield(target));
    }

    private bool CanSpellTargetPlayer(RuntimeCard source, int targetPlayerID)
    {
        return TargetingRules.CanSpellTargetPlayer(source, targetPlayerID);
    }

    private bool AreSpellTargetsStillLegal(RuntimeCard source)
    {
        if (!RequiresTarget(source)) return true;

        SpellCastDeclaration declaration = source?.CurrentCastDeclaration;
        if (declaration != null && source.CastProfile.TargetGroups.Count > 0)
        {
            if (!declaration.HasTargets) return true;

            for (int groupIndex = 0;
                 groupIndex < source.CastProfile.TargetGroups.Count;
                 groupIndex++)
            {
                SpellTargetGroupDefinition group =
                    source.CastProfile.TargetGroups[groupIndex];
                IReadOnlyList<SpellTargetReference> targets =
                    declaration.GetTargets(group.Id);
                var legalTargets = new List<SpellTargetReference>();
                for (int i = 0; i < targets.Count; i++)
                {
                    SpellTargetReference target = targets[i];
                    if (IsTargetReferenceLegal(source, target, group))
                    {
                        legalTargets.Add(target);
                    }
                }
                declaration.ReplaceTargets(group.Id, legalTargets);
            }

            List<SpellTargetReference> flattened = GetDeclarationTargets(declaration);
            source.SetSpellTargets(flattened);
            return flattened.Count > 0;
        }

        var legalLegacyTargets = new List<SpellTargetReference>();
        for (int i = 0; i < source.SpellTargets.Count; i++)
        {
            SpellTargetReference target = source.SpellTargets[i];
            if (target == null) continue;

            if (target.IsCardTarget
                && target.HasCurrentCardIdentity
                && CanSpellTargetCard(source, target.Card))
            {
                legalLegacyTargets.Add(target);
            }
            else if (target.IsPlayerTarget && CanSpellTargetPlayer(source, target.PlayerID))
            {
                legalLegacyTargets.Add(target);
            }
        }

        source.SetSpellTargets(legalLegacyTargets);
        return legalLegacyTargets.Count > 0;
    }

    private bool IsTargetReferenceLegal(
        RuntimeCard source,
        SpellTargetReference target,
        SpellTargetGroupDefinition group)
    {
        if (target == null || group == null) return false;
        if (target.IsCardTarget)
        {
            return target.HasCurrentCardIdentity
                && target.Card.CurrentZone == target.DeclaredZone
                && CanSpellTargetCard(source, target.Card, group)
                && MatchesTargetGroupRelations(source, target.Card, 0, null, group)
                && MatchesScriptTargetFilter(source, target.Card, 0, group);
        }
        if (target.IsPlayerTarget)
        {
            return CanSpellTargetPlayer(source, target.PlayerID, group)
                && MatchesTargetGroupRelations(source, null, target.PlayerID, null, group)
                && MatchesScriptTargetFilter(source, null, target.PlayerID, group);
        }
        return target.IsStackTarget && IsStackTargetLegal(source, target, group);
    }

    private static List<SpellTargetReference> GetDeclarationTargets(
        SpellCastDeclaration declaration)
    {
        var result = new List<SpellTargetReference>();
        if (declaration == null) return result;
        for (int i = 0; i < declaration.Targets.Count; i++)
        {
            SpellTargetReference target = declaration.Targets[i]?.Target;
            if (target != null) result.Add(target);
        }
        return result;
    }

    private bool IsOnBattlefield(RuntimeCard card)
    {
        return card != null && (player1Battlefield.Contains(card) || player2Battlefield.Contains(card));
    }

    private bool IsRuntimeEffectSourceActive(RuntimeCard card)
    {
        return IsOnBattlefield(card)
            || (card != null
                && card.IsEmblem
                && card.CurrentZone == GameZone.Command
                && (player1Command.Contains(card) || player2Command.Contains(card)));
    }

    private bool HasAttackingCreatures()
    {
        List<RuntimeCard> attackers = currentPlayer == 1 ? player1Battlefield : player2Battlefield;
        for (int i = 0; i < attackers.Count; i++)
        {
            if (attackers[i].IsAttacking) return true;
        }

        return false;
    }

    private bool HasIllegalMenaceBlocks()
    {
        List<RuntimeCard> attackers = currentPlayer == 1 ? player1Battlefield : player2Battlefield;
        for (int i = 0; i < attackers.Count; i++)
        {
            RuntimeCard attacker = attackers[i];
            if (attacker == null || !attacker.IsAttacking || !attacker.HasKeyword("Menace")) continue;
            if (attacker.Blockers.Count == 1) return true;
        }

        return false;
    }

    public string GetDeclareBlockersAdvanceBlockReason()
    {
        if (currentPhase != TurnPhase.DeclareBlockers) return string.Empty;

        List<RuntimeCard> attackers = currentPlayer == 1 ? player1Battlefield : player2Battlefield;
        for (int i = 0; i < attackers.Count; i++)
        {
            RuntimeCard attacker = attackers[i];
            if (attacker == null || !attacker.IsAttacking || !attacker.HasKeyword("Menace")) continue;
            if (attacker.Blockers.Count == 1)
            {
                return $"{attacker.Data.cardName} \u6709\u5a01\u6151\uff0c\u9700\u8981\u81f3\u5c11 2 \u4e2a\u751f\u7269\u963b\u6321\u3002";
            }
        }

        return string.Empty;
    }

    public bool ResolveTopOfStack()
    {
        if (IsGameOver()) return false;
        if (stack.Count == 0 || pendingStackResolutionCompletion != null) return false;

        GameStackItem item = stack[stack.Count - 1];
        stack.RemoveAt(stack.Count - 1);
        NotifyStackChanged();

        if (item == null || item.SourceCard == null) return true;

        RuntimeCard card = item.SourceCard;
        card.SetSpellTargets(item.Targets);
        card.SetCurrentCastDeclaration(
            item.CastDeclaration?.CopyForSource(
                card,
                item.ControllerPlayerId));
        if (item.Kind != GameStackItemKind.Spell)
        {
            Debug.Log($"Resolving {item.Kind}: {card.Data.cardName} [{item.HookName}]");
            card.SetResolvingAbilityContext(item.SpellFeature, item.RuleEntry, item.AbilityValue);
            card.SetResolvingGameEvent(item.EventContext);
            if (item.RuleEntry != null)
            {
                string hookName = CardEffectExecutor.HasCardScriptHook(card, item.HookName)
                    ? item.HookName
                    : "OnChapter";
                CardEffectExecutor.ExecuteTriggeredHook(this, card, hookName);
            }
            else if (item.HookName == "OnPlay")
            {
                CardEffectExecutor.ExecuteOnPlayEffect(this, card);
            }
            else if (item.HookName == BattleDefeatedHook)
            {
                if (CardEffectExecutor.HasCardScriptHook(card, item.HookName))
                {
                    CardEffectExecutor.ExecuteTriggeredHook(this, card, item.HookName);
                }

                if (SuspendStackResolutionForActiveChoice(
                        () => ResolveBattleDefeatAbility(card, item)))
                {
                    return true;
                }

                ResolveBattleDefeatAbility(card, item);
                return true;
            }
            else
            {
                CardEffectExecutor.ExecuteTriggeredHook(this, card, item.HookName);
            }

            if (SuspendStackResolutionForActiveChoice(
                    () => CompleteAbilityResolution(card, item)))
            {
                return true;
            }

            CompleteAbilityResolution(card, item);
            return true;
        }

        card.SetSpellFeature(item.SpellFeature);
        PublishGameEvent(new GameEventContext
        {
            type = GameEventType.SpellResolved,
            source = card,
            card = card,
            playerId = card.ControllerPlayerID,
            controllerId = card.ControllerPlayerID,
            fromZone = GameZone.Stack
        });
        List<RuntimeCard> battlefield = card.ControllerPlayerID == 1
            ? player1Battlefield
            : player2Battlefield;
        CardFeatureRuntime feature = item.SpellFeature ?? card.PrimaryFeature;

        if (RequiresTarget(card) && !AreSpellTargetsStillLegal(card))
        {
            Debug.Log($"{card.GetCardName()} has no legal target and does not resolve.");
            card.ClearSpellTarget();
            card.ClearSpellFeature();
            MoveDetachedCardToZone(card, GameZone.Graveyard);
            return true;
        }

        if (feature != null && feature.HasType(CardType.Land))
        {
            Debug.Log($"Resolving land: {feature.CardName}");
            card.ClearSpellFeature();
            AddToBattlefield(battlefield, card);
        }
        else if (feature != null && feature.IsInstantOrSorcery)
        {
            Debug.Log($"Resolving spell: {feature.CardName}");
            GameEvents.TriggerCardPlayed(card);

            if (SuspendStackResolutionForActiveChoice(
                    () => FinalizeResolvedInstantOrSorcery(card, feature)))
            {
                return true;
            }

            FinalizeResolvedInstantOrSorcery(card, feature);
        }
        else
        {
            Debug.Log($"Resolving permanent: {feature?.CardName ?? card.Data.cardName}");
            CardFeatureRuntime resolvedFeature = feature;
            RuntimeCard auraTarget = card.HasSubtype("Aura") ? GetTargetCard(card) : null;
            card.ClearSpellFeature();
            if (card.IsSpellCopy) card.MarkAsToken();
            AddToBattlefield(battlefield, card, resolvedFeature);
            if (card.HasSubtype("Aura") && !card.AttachTo(auraTarget))
            {
                MoveCardToGraveyard(card);
                card.ClearSpellTarget();
                return true;
            }
            GameEvents.TriggerCardPlayed(card);
            card.ClearSpellTarget();
        }

        return true;
    }

    private void CompleteAbilityResolution(RuntimeCard card, GameStackItem item)
    {
        if (card == null || item == null) return;

        PublishAbilityResolved(card, item.ControllerPlayerId);
        card.ClearResolvingAbilityContext();
        card.ClearResolvingGameEvent();
    }

    private void PublishAbilityActivated(RuntimeCard source)
    {
        if (source == null) return;
        PublishGameEvent(new GameEventContext
        {
            type = GameEventType.AbilityActivated,
            source = source,
            card = source,
            playerId = source.ControllerPlayerID,
            controllerId = source.ControllerPlayerID,
            fromZone = source.CurrentZone,
            toZone = GameZone.Stack
        });
    }

    private void PublishAbilityResolved(RuntimeCard source, int controllerPlayerId = 0)
    {
        if (source == null) return;
        int controller = controllerPlayerId > 0
            ? controllerPlayerId
            : source.ControllerPlayerID;
        PublishGameEvent(new GameEventContext
        {
            type = GameEventType.AbilityResolved,
            source = source,
            card = source,
            playerId = controller,
            controllerId = controller,
            fromZone = GameZone.Stack,
            toZone = source.CurrentZone
        });
    }

    private void FinalizeResolvedInstantOrSorcery(
        RuntimeCard card,
        CardFeatureRuntime feature)
    {
        if (card == null) return;

            card.ClearSpellTarget();

            if (feature.Role == CardFeatureRole.Adventure)
            {
                card.MarkAdventureResolved();
                card.ClearSpellFeature();
                MoveDetachedCardToZone(card, GameZone.Exile);
            }
            else if (feature.Role == CardFeatureRole.Omen)
            {
                card.ClearSpellFeature();
                if (MoveDetachedCardToZone(card, GameZone.Library))
                {
                    ShuffleDeck(GetLibraryList(card.OwnerPlayerID));
                }
            }
            else
            {
                card.ClearSpellFeature();
                if (!card.IsSpellCopy)
                {
                    MoveDetachedCardToZone(card, GameZone.Graveyard);
                }
                else card.OnDestroy();
            }
    }

    private void NotifyStackChanged()
    {
        stackCardView.Clear();
        for (int i = 0; i < stack.Count; i++)
        {
            RuntimeCard source = stack[i]?.SourceCard;
            if (source != null) stackCardView.Add(source);
        }

        GameEvents.TriggerStackChanged(stack.Count);
    }

    public void AddMana(int playerID, ManaColor color, int amount)
    {
        if (playerID == 1) player1ManaPool.Add(color, amount);
        else player2ManaPool.Add(color, amount);
        var pool = playerID == 1 ? player1ManaPool : player2ManaPool;
        Debug.Log($"Player {playerID} adds {color} x{amount}. Pool: {pool.ToDisplayString()}");
    }

    public ManaPool GetManaPool(int playerID)
    {
        return playerID == 1 ? player1ManaPool : player2ManaPool;
    }

    public int GetMana(int playerID)
    {
        return (playerID == 1 ? player1ManaPool : player2ManaPool).Total;
    }

    public bool GetPotentialMana(ManaCost cost, int playerID)
    {
        // Potential: current pool + explicitly registered mana abilities.
        ManaPool virtualPool = GetManaPool(playerID);
        List<RuntimeCard> battlefield = playerID == 1 ? player1Battlefield : player2Battlefield;
        foreach (var card in battlefield)
        {
            if (card == null || card.IsTapped) continue;
            for (int i = 0; i < card.Abilities.Count; i++)
            {
                ActivatedAbility ability = card.Abilities[i];
                if (ability == null || !ability.IsManaAbility || !ability.HasProducedMana) continue;
                if (!ability.IsActivatable(this, card)) continue;
                AddManaPoolValue(ref virtualPool, ability.ProducedMana);
            }
        }
        return virtualPool.CanPay(cost);
    }

    private bool SpendMana(int playerID, ManaCost cost)
    {
        if (cost.IsEmpty) return true;

        ManaPool before = GetManaPool(playerID);
        bool paid = playerID == 1 ? player1ManaPool.Spend(cost) : player2ManaPool.Spend(cost);
        if (!paid) return false;

        DeductTrackedLandMana(playerID, before, GetManaPool(playerID));
        return true;
    }

    private void ClearManaPools()
    {
        if (!player1ManaPool.IsEmpty || !player2ManaPool.IsEmpty || unspentSourceMana.Count > 0)
        {
            Debug.Log("Mana pools empty as phase ends.");
            player1ManaPool.Clear();
            player2ManaPool.Clear();
            unspentSourceMana.Clear();
        }
    }

    private void DeductTrackedLandMana(int playerID, ManaPool before, ManaPool after)
    {
        RemoveSpentTrackedMana(playerID, ManaColor.White, before.White - after.White);
        RemoveSpentTrackedMana(playerID, ManaColor.Blue, before.Blue - after.Blue);
        RemoveSpentTrackedMana(playerID, ManaColor.Black, before.Black - after.Black);
        RemoveSpentTrackedMana(playerID, ManaColor.Red, before.Red - after.Red);
        RemoveSpentTrackedMana(playerID, ManaColor.Green, before.Green - after.Green);
        RemoveSpentTrackedMana(playerID, ManaColor.Colorless, before.Colorless - after.Colorless);
    }

    private void RemoveSpentTrackedMana(int playerID, ManaColor color, int spent)
    {
        if (spent <= 0 || unspentSourceMana.Count == 0) return;

        var updates = new List<KeyValuePair<RuntimeCard, ManaPool>>();
        foreach (var entry in unspentSourceMana)
        {
            if (spent <= 0) break;
            RuntimeCard source = entry.Key;
            ManaPool produced = entry.Value;
            if (source == null
                || source.ControllerPlayerID != playerID
                || produced.GetAmount(color) <= 0)
            {
                continue;
            }

            while (spent > 0 && produced.GetAmount(color) > 0)
            {
                produced.TryRemove(color, 1);
                spent--;
            }
            updates.Add(new KeyValuePair<RuntimeCard, ManaPool>(source, produced));
        }

        for (int i = 0; i < updates.Count; i++)
        {
            RuntimeCard source = updates[i].Key;
            ManaPool remaining = updates[i].Value;
            if (remaining.IsEmpty)
            {
                unspentSourceMana.Remove(source);
            }
            else
            {
                unspentSourceMana[source] = remaining;
            }
        }
    }

    private static ManaPool ManaPoolFromSymbols(string symbols)
    {
        ManaCost cost = ManaCost.Parse(symbols);
        var pool = new ManaPool
        {
            White = cost.White,
            Blue = cost.Blue,
            Black = cost.Black,
            Red = cost.Red,
            Green = cost.Green,
            Colorless = cost.Colorless + cost.Generic
        };

        return pool;
    }

    private static string ManaPoolToSymbols(ManaPool pool)
    {
        var builder = new System.Text.StringBuilder();
        AppendManaSymbols(builder, "W", pool.White);
        AppendManaSymbols(builder, "U", pool.Blue);
        AppendManaSymbols(builder, "B", pool.Black);
        AppendManaSymbols(builder, "R", pool.Red);
        AppendManaSymbols(builder, "G", pool.Green);
        AppendManaSymbols(builder, "C", pool.Colorless);
        return builder.Length > 0 ? builder.ToString() : "{0}";
    }

    private static void AppendManaSymbols(System.Text.StringBuilder builder, string symbol, int count)
    {
        for (int i = 0; i < count; i++)
        {
            builder.Append('{').Append(symbol).Append('}');
        }
    }

    private static bool CanRemoveManaPool(ManaPool source, ManaPool amount)
    {
        return source.White >= amount.White
            && source.Blue >= amount.Blue
            && source.Black >= amount.Black
            && source.Red >= amount.Red
            && source.Green >= amount.Green
            && source.Colorless >= amount.Colorless;
    }

    private static bool TryRemoveManaPool(ref ManaPool source, ManaPool amount)
    {
        if (!CanRemoveManaPool(source, amount)) return false;

        source.White -= amount.White;
        source.Blue -= amount.Blue;
        source.Black -= amount.Black;
        source.Red -= amount.Red;
        source.Green -= amount.Green;
        source.Colorless -= amount.Colorless;
        return true;
    }

    private static void AddManaPoolValue(ref ManaPool target, ManaPool amount)
    {
        target.White += amount.White;
        target.Blue += amount.Blue;
        target.Black += amount.Black;
        target.Red += amount.Red;
        target.Green += amount.Green;
        target.Colorless += amount.Colorless;
    }

    private void AddManaFromSource(RuntimeCard source, ManaPool produced)
    {
        if (source == null || produced.IsEmpty) return;

        source.Tap();
        AddManaPool(source.ControllerPlayerID, produced);
        unspentSourceMana[source] = produced;
    }

    private void AddManaPool(int playerID, ManaPool produced)
    {
        AddManaIfPositive(playerID, ManaColor.White, produced.White);
        AddManaIfPositive(playerID, ManaColor.Blue, produced.Blue);
        AddManaIfPositive(playerID, ManaColor.Black, produced.Black);
        AddManaIfPositive(playerID, ManaColor.Red, produced.Red);
        AddManaIfPositive(playerID, ManaColor.Green, produced.Green);
        AddManaIfPositive(playerID, ManaColor.Colorless, produced.Colorless);
    }

    private void AddManaIfPositive(int playerID, ManaColor color, int amount)
    {
        if (amount > 0) AddMana(playerID, color, amount);
    }

    private bool CanUndoUnspentSourceMana(RuntimeCard source)
    {
        if (source == null || !source.IsTapped) return false;
        if (!unspentSourceMana.TryGetValue(source, out ManaPool produced) || produced.IsEmpty) return false;
        return CanRemoveManaPool(GetManaPool(source.ControllerPlayerID), produced);
    }

    private bool UndoUnspentSourceMana(RuntimeCard source)
    {
        if (!CanUndoUnspentSourceMana(source)) return false;

        ManaPool produced = unspentSourceMana[source];
        if (source.ControllerPlayerID == 1)
        {
            if (!TryRemoveManaPool(ref player1ManaPool, produced)) return false;
        }
        else
        {
            if (!TryRemoveManaPool(ref player2ManaPool, produced)) return false;
        }

        unspentSourceMana.Remove(source);
        source.Untap();
        Debug.Log($"Player {source.ControllerPlayerID} rewinds {source.Data.cardName}. Removed {produced.ToDisplayString()} from pool.");
        return true;
    }

    public bool RegisterManaAbility(RuntimeCard card, string producedSymbols)
    {
        if (card == null || card.Data == null || string.IsNullOrWhiteSpace(producedSymbols)) return false;

        ManaPool produced = ManaPoolFromSymbols(producedSymbols);
        if (produced.IsEmpty) return false;

        string symbolText = ManaPoolToSymbols(produced);
        string abilityName = $"Tap for {symbolText}";
        if (HasAbilityNamed(card, abilityName)) return true;

        card.Abilities.Add(new ActivatedAbility
        {
            Name = abilityName,
            Description = $"{{T}}: Add {symbolText}.",
            RuleTraits = ActivatedAbilityRuleTrait.CouldAddMana,
            ProducedMana = produced,
            CanActivate = (rules, c) => rules != null
                && c != null
                && !c.IsTapped
                && rules.IsCardOnBattlefield(c),
            Execute = (rules, c) =>
            {
                rules?.AddManaFromSource(c, produced);
            }
        });
        return true;
    }

    public bool RegisterFetchBasicLandAbility(RuntimeCard card)
    {
        if (card == null || card.Data == null || !card.HasType(CardType.Land)) return false;
        if (HasAbilityNamed(card, "Fetch basic land")) return true;

        card.Abilities.Add(new ActivatedAbility
        {
            Name = "Fetch basic land",
            Description = "{T}, Sacrifice this land: Search your library for a basic land card and put it onto the battlefield tapped. Then shuffle.",
            RuleTraits = ActivatedAbilityRuleTrait.MovesCardIntoOrOutOfLibrary,
            CanActivate = (rules, c) => rules != null
                && c != null
                && !c.IsTapped
                && rules.IsCardOnBattlefield(c)
                && !rules.HasActiveChoice()
                && rules.HasPriority(c.ControllerPlayerID),
            Execute = (rules, c) =>
            {
                rules?.ActivateFetchBasicLandAbility(c);
            }
        });
        return true;
    }

    public bool RegisterScriptActivatedAbility(
        RuntimeCard card,
        string abilityName,
        string description,
        string costText,
        string resolveHook,
        string canActivateHook,
        ScriptAbilityTiming timing,
        ActivatedAbilityRuleTrait ruleTraits)
    {
        if (card == null
            || string.IsNullOrWhiteSpace(abilityName)
            || string.IsNullOrWhiteSpace(resolveHook))
        {
            return false;
        }
        if (!ScriptAbilityCost.TryParse(
                costText,
                out ScriptAbilityCost cost,
                out string error))
        {
            Debug.LogError(error);
            return false;
        }

        if (HasAbilityNamed(card, abilityName.Trim())) return true;
        var ability = new ActivatedAbility
        {
            Name = abilityName.Trim(),
            Description = description ?? string.Empty,
            RuleTraits = ruleTraits,
            Cost = cost,
            Timing = timing,
            ResolveHook = resolveHook.Trim(),
            CanActivateHook = canActivateHook?.Trim()
        };
        ability.CanActivate = (rules, source) =>
            rules != null && rules.CanPayScriptAbility(source, ability);
        ability.Execute = (rules, source) =>
        {
            rules?.BeginScriptAbilityActivation(source, ability);
        };
        card.Abilities.Add(ability);
        return true;
    }

    private bool CanPayScriptAbility(RuntimeCard source, ActivatedAbility ability)
    {
        if (source == null
            || ability == null
            || !IsOnBattlefield(source)
            || !HasPriority(source.ControllerPlayerID))
        {
            return false;
        }

        if (ability.Timing == ScriptAbilityTiming.Sorcery
            && (currentPlayer != source.ControllerPlayerID
                || (currentPhase != TurnPhase.Main1 && currentPhase != TurnPhase.Main2)
                || stack.Count > 0))
        {
            return false;
        }

        ScriptAbilityCost cost = ability.Cost ?? new ScriptAbilityCost();
        if (cost.TapSource && source.IsTapped) return false;
        if (cost.Life > 0 && GetLife(source.ControllerPlayerID) <= cost.Life) return false;
        int handCount = GetHandCount(source.ControllerPlayerID);
        if (cost.DiscardCount + cost.ExileHandCount > handCount) return false;
        if (cost.RevealHandCount > handCount) return false;
        if (cost.SacrificePermanentCount
            > CountSacrificeCostCandidates(source, cost.SacrificeSource))
        {
            return false;
        }
        if (cost.CounterCount > source.GetCounters(cost.CounterName)) return false;
        if (!CanPayWithPoolOrAvailableMana(source.ControllerPlayerID, cost.Mana)) return false;

        return string.IsNullOrEmpty(ability.CanActivateHook)
            || CardEffectExecutor.EvaluateCardScriptPredicate(
                this,
                source,
                ability.CanActivateHook);
    }

    private void BeginScriptAbilityActivation(RuntimeCard source, ActivatedAbility ability)
    {
        if (!CanPayScriptAbility(source, ability)) return;
        var payment = new ScriptAbilityPayment();
        SelectScriptDiscardCost(source, ability, payment);
    }

    private void SelectScriptDiscardCost(
        RuntimeCard source,
        ActivatedAbility ability,
        ScriptAbilityPayment payment)
    {
        ScriptAbilityCost cost = ability.Cost ?? new ScriptAbilityCost();
        if (cost.DiscardCount <= 0)
        {
            SelectScriptSacrificeCost(source, ability, payment);
            return;
        }

        List<RuntimeCard> hand = GetHandList(source.ControllerPlayerID);
        if (source.ControllerPlayerID != 1 && !IsOnlineMatch())
        {
            AddAutomaticCards(hand, cost.DiscardCount, payment.DiscardCards);
            SelectScriptSacrificeCost(source, ability, payment);
            return;
        }

        BeginOptionChoice(
            GameChoiceKind.SelectOptions,
            source.ControllerPlayerID,
            $"Choose {cost.DiscardCount} card(s) to discard for {ability.Name}.",
            cost.DiscardCount,
            cost.DiscardCount,
            true,
            CreateCostChoiceOptions(hand, GameChoiceZone.Hand, source.ControllerPlayerID),
            result =>
            {
                if (TryCollectSelectedCards(
                    hand,
                    result?.selectedOptionIds,
                    cost.DiscardCount,
                    payment.DiscardCards))
                {
                    SelectScriptSacrificeCost(source, ability, payment);
                }
            });
    }

    private void SelectScriptSacrificeCost(
        RuntimeCard source,
        ActivatedAbility ability,
        ScriptAbilityPayment payment)
    {
        ScriptAbilityCost cost = ability.Cost ?? new ScriptAbilityCost();
        if (cost.SacrificePermanentCount <= 0)
        {
            SelectScriptExileHandCost(source, ability, payment);
            return;
        }

        List<RuntimeCard> candidates = GetSacrificeCostCandidates(source, cost.SacrificeSource);
        if (source.ControllerPlayerID != 1 && !IsOnlineMatch())
        {
            AddAutomaticCards(
                candidates,
                cost.SacrificePermanentCount,
                payment.SacrificePermanents);
            SelectScriptExileHandCost(source, ability, payment);
            return;
        }

        BeginOptionChoice(
            GameChoiceKind.SelectOptions,
            source.ControllerPlayerID,
            $"Choose {cost.SacrificePermanentCount} permanent(s) to sacrifice for {ability.Name}.",
            cost.SacrificePermanentCount,
            cost.SacrificePermanentCount,
            true,
            CreateCostChoiceOptions(
                candidates,
                GameChoiceZone.Battlefield,
                source.ControllerPlayerID),
            result =>
            {
                if (TryCollectSelectedCards(
                    candidates,
                    result?.selectedOptionIds,
                    cost.SacrificePermanentCount,
                    payment.SacrificePermanents))
                {
                    SelectScriptExileHandCost(source, ability, payment);
                }
            });
    }

    private void SelectScriptExileHandCost(
        RuntimeCard source,
        ActivatedAbility ability,
        ScriptAbilityPayment payment)
    {
        ScriptAbilityCost cost = ability.Cost ?? new ScriptAbilityCost();
        if (cost.ExileHandCount <= 0)
        {
            SelectScriptRevealHandCost(source, ability, payment);
            return;
        }

        List<RuntimeCard> candidates = new List<RuntimeCard>(GetHandList(source.ControllerPlayerID));
        candidates.RemoveAll(card => payment.DiscardCards.Contains(card));
        if (source.ControllerPlayerID != 1 && !IsOnlineMatch())
        {
            AddAutomaticCards(candidates, cost.ExileHandCount, payment.ExileHandCards);
            SelectScriptRevealHandCost(source, ability, payment);
            return;
        }

        BeginOptionChoice(
            GameChoiceKind.SelectOptions,
            source.ControllerPlayerID,
            $"Choose {cost.ExileHandCount} card(s) to exile from hand for {ability.Name}.",
            cost.ExileHandCount,
            cost.ExileHandCount,
            true,
            CreateCostChoiceOptions(
                candidates,
                GameChoiceZone.Hand,
                source.ControllerPlayerID),
            result =>
            {
                if (TryCollectSelectedCards(
                    candidates,
                    result?.selectedOptionIds,
                    cost.ExileHandCount,
                    payment.ExileHandCards))
                {
                    SelectScriptRevealHandCost(source, ability, payment);
                }
            });
    }

    private void SelectScriptRevealHandCost(
        RuntimeCard source,
        ActivatedAbility ability,
        ScriptAbilityPayment payment)
    {
        ScriptAbilityCost cost = ability.Cost ?? new ScriptAbilityCost();
        if (cost.RevealHandCount <= 0)
        {
            PayAndQueueScriptAbility(source, ability, payment);
            return;
        }

        List<RuntimeCard> hand = GetHandList(source.ControllerPlayerID);
        if (source.ControllerPlayerID != 1 && !IsOnlineMatch())
        {
            AddAutomaticCards(hand, cost.RevealHandCount, payment.RevealHandCards);
            PayAndQueueScriptAbility(source, ability, payment);
            return;
        }

        BeginOptionChoice(
            GameChoiceKind.SelectOptions,
            source.ControllerPlayerID,
            $"Choose {cost.RevealHandCount} card(s) to reveal for {ability.Name}.",
            cost.RevealHandCount,
            cost.RevealHandCount,
            true,
            CreateCostChoiceOptions(hand, GameChoiceZone.Hand, source.ControllerPlayerID),
            result =>
            {
                if (TryCollectSelectedCards(
                    hand,
                    result?.selectedOptionIds,
                    cost.RevealHandCount,
                    payment.RevealHandCards))
                {
                    PayAndQueueScriptAbility(source, ability, payment);
                }
            });
    }

    private void PayAndQueueScriptAbility(
        RuntimeCard source,
        ActivatedAbility ability,
        ScriptAbilityPayment payment)
    {
        if (!CanPayScriptAbility(source, ability)) return;
        ScriptAbilityCost cost = ability.Cost ?? new ScriptAbilityCost();
        payment ??= new ScriptAbilityPayment();
        if (!ValidateScriptAbilityPayment(source, cost, payment)) return;

        int controllerPlayerId = source.ControllerPlayerID;
        GameStackItem queuedAbility = ability.IsManaAbility
            ? null
            : GameStackItem.ActivatedAbility(source, ability.ResolveHook);
        if (!TryPrepareCostPayment(source, cost, payment, out PreparedCostPayment prepared))
        {
            return;
        }
        if (!TryPayMana(controllerPlayerId, cost.Mana)) return;
        if (cost.Life > 0)
        {
            if (controllerPlayerId == 1) player1Life -= cost.Life;
            else player2Life -= cost.Life;
        }
        if (cost.CounterCount > 0)
        {
            source.AddCounters(cost.CounterName, -cost.CounterCount);
        }
        if (cost.TapSource) source.Tap();

        for (int i = 0; i < payment.RevealHandCards.Count; i++)
        {
            RuntimeCard revealedCard = payment.RevealHandCards[i];
            Debug.Log(
                $"Player {controllerPlayerId} reveals " +
                $"{revealedCard.GetCardName()}.");
            PublishGameEvent(new GameEventContext
            {
                type = GameEventType.CardRevealed,
                source = source,
                card = revealedCard,
                playerId = controllerPlayerId,
                controllerId = revealedCard.ControllerPlayerID,
                fromZone = GameZone.Hand,
                toZone = GameZone.Hand
            });
        }
        CommitPreparedCostPayment(source, prepared);

        if (ability.IsManaAbility)
        {
            PublishAbilityActivated(source);
            CardEffectExecutor.ExecuteTriggeredHook(this, source, ability.ResolveHook);
            PublishAbilityResolved(source);
        }
        else
        {
            PushStackItem(queuedAbility);
            PublishAbilityActivated(source);
            NotifyStackChanged();
            BeginPriority(controllerPlayerId);
        }

        PerformStateBasedActions();
        Debug.Log(
            $"Player {controllerPlayerId} activates {ability.Name} on {source.GetCardName()}.");
    }

    private bool SacrificePermanentForCost(
        RuntimeCard abilitySource,
        RuntimeCard permanent)
    {
        if (permanent == null || !IsOnBattlefield(permanent)) return false;

        int owner = permanent.OwnerPlayerID;
        int controller = permanent.ControllerPlayerID;
        var sacrificeEvent = new GameEventContext
        {
            type = GameEventType.PermanentSacrificed,
            source = abilitySource ?? permanent,
            card = permanent,
            playerId = owner,
            controllerId = controller,
            fromZone = GameZone.Battlefield,
            toZone = GameZone.Graveyard
        };
        sacrificeEvent.CaptureObjectIdentities();
        if (!MoveCardToZone(permanent, GameZone.Graveyard, false, false))
        {
            return false;
        }

        sacrificeEvent.toZone = permanent.CurrentZone;
        PublishGameEvent(sacrificeEvent);
        return true;
    }

    private bool TryPrepareCostPayment(
        RuntimeCard source,
        ScriptAbilityCost cost,
        ScriptAbilityPayment payment,
        out PreparedCostPayment prepared)
    {
        prepared = null;
        if (source == null || cost == null || payment == null) return false;

        var result = new PreparedCostPayment();
        var cards = new HashSet<RuntimeCard>();
        bool AddMove(RuntimeCard card, GameZone destination, bool sacrificed)
        {
            if (card == null || !cards.Add(card)) return false;
            if (!TryPrepareZoneMove(
                    card,
                    destination,
                    false,
                    out PreparedZoneMove move))
            {
                return false;
            }
            result.ZoneMoves.Add(move);
            if (sacrificed) result.SacrificedCards.Add(card);
            return true;
        }

        for (int i = 0; i < payment.DiscardCards.Count; i++)
        {
            RuntimeCard card = payment.DiscardCards[i];
            if (card.CurrentZone != GameZone.Hand
                || !AddMove(card, GameZone.Graveyard, false))
            {
                return false;
            }
        }
        for (int i = 0; i < payment.ExileHandCards.Count; i++)
        {
            RuntimeCard card = payment.ExileHandCards[i];
            if (card.CurrentZone != GameZone.Hand
                || !AddMove(card, GameZone.Exile, false))
            {
                return false;
            }
        }
        for (int i = 0; i < payment.SacrificePermanents.Count; i++)
        {
            RuntimeCard card = payment.SacrificePermanents[i];
            if (!IsOnBattlefield(card)
                || !AddMove(card, GameZone.Graveyard, true))
            {
                return false;
            }
        }
        if (cost.SacrificeSource
            && (!IsOnBattlefield(source)
                || !AddMove(source, GameZone.Graveyard, true)))
        {
            return false;
        }
        if (cost.ExileSource
            && !AddMove(source, GameZone.Exile, false))
        {
            return false;
        }

        prepared = result;
        return true;
    }

    private void CommitPreparedCostPayment(
        RuntimeCard source,
        PreparedCostPayment payment)
    {
        if (payment == null) return;
        for (int i = 0; i < payment.ZoneMoves.Count; i++)
        {
            PreparedZoneMove move = payment.ZoneMoves[i];
            RuntimeCard card = move.Card;
            int owner = card.OwnerPlayerID;
            int controller = card.ControllerPlayerID;
            CommitPreparedZoneMove(move, false);
            if (!payment.SacrificedCards.Contains(card)) continue;

            PublishGameEvent(new GameEventContext
            {
                type = GameEventType.PermanentSacrificed,
                source = source ?? card,
                card = card,
                playerId = owner,
                controllerId = controller,
                fromZone = GameZone.Battlefield,
                toZone = card.CurrentZone
            });
        }
    }

    private int CountSacrificeCostCandidates(RuntimeCard source, bool excludeSource)
    {
        return GetSacrificeCostCandidates(source, excludeSource).Count;
    }

    private List<RuntimeCard> GetSacrificeCostCandidates(
        RuntimeCard source,
        bool excludeSource)
    {
        var result = new List<RuntimeCard>();
        if (source == null) return result;

        IReadOnlyList<RuntimeCard> battlefield = source.ControllerPlayerID == 1
            ? player1Battlefield
            : player2Battlefield;
        for (int i = 0; i < battlefield.Count; i++)
        {
            RuntimeCard candidate = battlefield[i];
            if (candidate == null
                || candidate.ControllerPlayerID != source.ControllerPlayerID
                || (excludeSource && ReferenceEquals(candidate, source)))
            {
                continue;
            }
            result.Add(candidate);
        }
        return result;
    }

    private List<GameChoiceOption> CreateCostChoiceOptions(
        IReadOnlyList<RuntimeCard> cards,
        GameChoiceZone zone,
        int playerId)
    {
        var options = new List<GameChoiceOption>(cards?.Count ?? 0);
        if (cards == null) return options;
        List<RuntimeCard> sourceZone = zone == GameChoiceZone.Hand
            ? GetHandList(playerId)
            : zone == GameChoiceZone.Battlefield
                ? playerId == 1 ? player1Battlefield : player2Battlefield
                : null;
        for (int i = 0; i < cards.Count; i++)
        {
            RuntimeCard card = cards[i];
            int zoneIndex = sourceZone != null ? sourceZone.IndexOf(card) : i;
            options.Add(new GameChoiceOption(
                i,
                zone,
                playerId,
                zoneIndex,
                card != null && zoneIndex >= 0,
                card?.GetCardName() ?? string.Empty));
        }
        return options;
    }

    private static bool TryCollectSelectedCards(
        IReadOnlyList<RuntimeCard> candidates,
        IReadOnlyList<int> selectedIndices,
        int requiredCount,
        List<RuntimeCard> destination)
    {
        if (candidates == null
            || destination == null
            || selectedIndices == null
            || selectedIndices.Count != requiredCount)
        {
            return false;
        }

        destination.Clear();
        var unique = new HashSet<int>();
        for (int i = 0; i < selectedIndices.Count; i++)
        {
            int index = selectedIndices[i];
            if (index < 0
                || index >= candidates.Count
                || !unique.Add(index)
                || candidates[index] == null)
            {
                destination.Clear();
                return false;
            }
            destination.Add(candidates[index]);
        }
        return true;
    }

    private static void AddAutomaticCards(
        IReadOnlyList<RuntimeCard> candidates,
        int count,
        List<RuntimeCard> destination)
    {
        destination.Clear();
        if (candidates == null || count <= 0) return;
        int start = Mathf.Max(0, candidates.Count - count);
        for (int i = start; i < candidates.Count; i++)
        {
            if (candidates[i] != null) destination.Add(candidates[i]);
        }
    }

    private bool ValidateScriptAbilityPayment(
        RuntimeCard source,
        ScriptAbilityCost cost,
        ScriptAbilityPayment payment)
    {
        if (source == null || cost == null || payment == null) return false;
        if (payment.DiscardCards.Count != cost.DiscardCount
            || payment.SacrificePermanents.Count != cost.SacrificePermanentCount
            || payment.ExileHandCards.Count != cost.ExileHandCount
            || payment.RevealHandCards.Count != cost.RevealHandCount)
        {
            return false;
        }

        List<RuntimeCard> hand = GetHandList(source.ControllerPlayerID);
        var paidHandCards = new HashSet<RuntimeCard>();
        for (int i = 0; i < payment.DiscardCards.Count; i++)
        {
            RuntimeCard card = payment.DiscardCards[i];
            if (card == null || !hand.Contains(card) || !paidHandCards.Add(card)) return false;
        }
        for (int i = 0; i < payment.ExileHandCards.Count; i++)
        {
            RuntimeCard card = payment.ExileHandCards[i];
            if (card == null || !hand.Contains(card) || !paidHandCards.Add(card)) return false;
        }
        for (int i = 0; i < payment.RevealHandCards.Count; i++)
        {
            RuntimeCard card = payment.RevealHandCards[i];
            if (card == null || !hand.Contains(card)) return false;
        }

        var sacrificed = new HashSet<RuntimeCard>();
        for (int i = 0; i < payment.SacrificePermanents.Count; i++)
        {
            RuntimeCard card = payment.SacrificePermanents[i];
            if (card == null
                || !IsOnBattlefield(card)
                || card.ControllerPlayerID != source.ControllerPlayerID
                || (cost.SacrificeSource && ReferenceEquals(card, source))
                || !sacrificed.Add(card))
            {
                return false;
            }
        }

        return true;
    }

    private static bool HasAbilityNamed(RuntimeCard card, string abilityName)
    {
        if (card == null || string.IsNullOrEmpty(abilityName)) return false;
        for (int i = 0; i < card.Abilities.Count; i++)
        {
            ActivatedAbility ability = card.Abilities[i];
            if (ability != null && ability.Name == abilityName) return true;
        }

        return false;
    }

    private bool ActivateFetchBasicLandAbility(RuntimeCard source)
    {
        if (source == null || source.IsTapped || !IsOnBattlefield(source)) return false;
        int playerID = source.ControllerPlayerID;

        source.Tap();
        if (!SacrificePermanentForCost(source, source)) return false;

        if ((IsOnlineMatch() || playerID == 1)
            && HasBasicLandInLibrary(playerID))
        {
            pendingLibrarySearchSource = source;
            GameChoiceRequest request = BeginChoice(
                GameChoiceKind.LibrarySearch,
                playerID,
                $"Search your library for a basic land card with {source.Data.cardName}. Put it onto the battlefield tapped, then shuffle.",
                0,
                1,
                false);
            if (request == null) return false;

            List<RuntimeCard> deck = GetLibraryList(playerID);
            for (int i = 0; i < deck.Count; i++)
            {
                RuntimeCard libraryCard = deck[i];
                request.options.Add(new GameChoiceOption(
                    i,
                    GameChoiceZone.Library,
                    playerID,
                    i,
                    IsBasicLandCard(libraryCard),
                    libraryCard?.Data != null ? libraryCard.Data.cardName : string.Empty));
            }
            Debug.Log($"Player {playerID} searches their library with {source.Data.cardName}.");
            return true;
        }

        bool found = PutFirstBasicLandFromLibraryOntoBattlefieldTapped(playerID);
        ShuffleDeck(GetLibraryList(playerID));
        Debug.Log(found
            ? $"Player {playerID} fetched a basic land with {source.Data.cardName}."
            : $"Player {playerID} searched with {source.Data.cardName}, but no basic land was found.");
        return true;
    }

    private bool PutFirstBasicLandFromLibraryOntoBattlefieldTapped(int playerID)
    {
        List<RuntimeCard> deck = GetLibraryList(playerID);
        List<RuntimeCard> battlefield = playerID == 1 ? player1Battlefield : player2Battlefield;
        if (deck == null || battlefield == null) return false;

        for (int i = 0; i < deck.Count; i++)
        {
            RuntimeCard fetched = deck[i];
            if (!IsBasicLandCard(fetched))
            {
                continue;
            }

            deck.RemoveAt(i);
            fetched.Tap();
            AddToBattlefield(battlefield, fetched);
            return true;
        }

        return false;
    }

    private bool HasBasicLandInLibrary(int playerID)
    {
        List<RuntimeCard> deck = GetLibraryList(playerID);
        if (deck == null) return false;

        for (int i = 0; i < deck.Count; i++)
        {
            if (IsBasicLandCard(deck[i])) return true;
        }

        return false;
    }

    private void CompleteLibrarySearch(int playerID, string logMessage)
    {
        RuntimeCard source = pendingLibrarySearchSource;
        ShuffleDeck(GetLibraryList(playerID));
        ClearPendingLibrarySearch();
        PublishAbilityResolved(source, playerID);
        if (!string.IsNullOrWhiteSpace(logMessage)) Debug.Log(logMessage);
        NotifyStackChanged();
    }

    private void ClearPendingLibrarySearch()
    {
        pendingLibrarySearchSource = null;
        ClearActiveChoice(GameChoiceKind.LibrarySearch);
    }

    private static bool IsBasicLandCard(RuntimeCard card)
    {
        return card?.Data != null
            && card.Data.HasType(CardType.Land)
            && card.Data.HasSupertype(CardSupertype.Basic);
    }

    public bool CanActivateAnyAbility(RuntimeCard card)
    {
        if (card == null || IsGameOver() || HasActiveChoice()) return false;
        if (!HasPriority(card.ControllerPlayerID)) return false;
        if (onlineViewOnly) return card.OnlineCanActivateAbility;
        return card.HasActivatableAbility(this) || CanUndoUnspentSourceMana(card);
    }

    public bool CanActivateManaAbility(RuntimeCard card)
    {
        if (card == null) return false;
        if (IsGameOver()) return false;
        if (HasActiveChoice()) return false;
        if (!HasPriority(card.ControllerPlayerID)) return false;
        if (onlineViewOnly) return card.OnlineCanActivateManaAbility;
        if (CanUndoUnspentSourceMana(card)) return true;
        return card.HasActivatableManaAbility(this);
    }

    public bool ActivateAbility(RuntimeCard card)
    {
        if (card == null) return false;
        if (IsGameOver()) return false;
        if (HasActiveChoice()) return false;
        if (!HasPriority(card.ControllerPlayerID)) return false;
        if (UndoUnspentSourceMana(card)) return true;

        var ability = card.GetFirstActivatableAbility(this);
        if (ability == null)
        {
            Debug.LogWarning("No activatable ability available.");
            return false;
        }

        ActivateRuntimeAbility(ability, card);
        Debug.Log($"Activated ability: {ability.Name} on {card.Data.cardName}");
        return true;
    }

    public bool BeginActivateAbilityChoice(RuntimeCard card, bool preferManaAbilities = false)
    {
        if (card == null || IsGameOver() || HasActiveChoice()) return false;
        if (UndoUnspentSourceMana(card)) return true;
        if (!HasPriority(card.ControllerPlayerID)) return false;

        var activatable = new List<KeyValuePair<int, ActivatedAbility>>();
        CollectActivatableAbilities(card, preferManaAbilities, activatable);
        if (activatable.Count == 0 && preferManaAbilities)
        {
            CollectActivatableAbilities(card, false, activatable);
        }

        if (activatable.Count == 0) return false;
        if (activatable.Count == 1)
        {
            return ActivateAbilityAtIndex(card, activatable[0].Key);
        }

        var options = new List<GameChoiceOption>(activatable.Count);
        for (int i = 0; i < activatable.Count; i++)
        {
            KeyValuePair<int, ActivatedAbility> entry = activatable[i];
            ActivatedAbility ability = entry.Value;
            string label = string.IsNullOrWhiteSpace(ability.Description)
                ? ability.Name
                : $"{ability.Name}\n{ability.Description}";
            options.Add(new GameChoiceOption(
                entry.Key,
                GameChoiceZone.Battlefield,
                card.ControllerPlayerID,
                entry.Key,
                true,
                label));
        }

        GameChoiceRequest request = BeginOptionChoice(
            GameChoiceKind.ChooseMode,
            card.ControllerPlayerID,
            $"Choose an ability of {card.GetCardName()}.",
            1,
            1,
            true,
            options,
            result =>
            {
                if (result != null && result.selectedOptionIds.Count == 1)
                {
                    ActivateAbilityAtIndex(card, result.selectedOptionIds[0]);
                }
            });
        return request != null;
    }

    private void CollectActivatableAbilities(
        RuntimeCard card,
        bool manaOnly,
        List<KeyValuePair<int, ActivatedAbility>> destination)
    {
        destination.Clear();
        if (card == null) return;

        for (int i = 0; i < card.Abilities.Count; i++)
        {
            ActivatedAbility ability = card.Abilities[i];
            if (ability == null
                || (manaOnly && !ability.IsManaAbility)
                || !ability.IsActivatable(this, card))
            {
                continue;
            }

            destination.Add(new KeyValuePair<int, ActivatedAbility>(i, ability));
        }
    }

    private bool ActivateAbilityAtIndex(RuntimeCard card, int abilityIndex)
    {
        if (card == null
            || IsGameOver()
            || HasActiveChoice()
            || !HasPriority(card.ControllerPlayerID)
            || abilityIndex < 0
            || abilityIndex >= card.Abilities.Count)
        {
            return false;
        }

        ActivatedAbility ability = card.Abilities[abilityIndex];
        if (ability == null || !ability.IsActivatable(this, card)) return false;

        ActivateRuntimeAbility(ability, card);
        Debug.Log($"Activated ability: {ability.Name} on {card.Data.cardName}");
        return true;
    }

    public bool ActivateManaAbility(RuntimeCard card)
    {
        if (card == null) return false;
        if (IsGameOver()) return false;
        if (HasActiveChoice()) return false;
        if (UndoUnspentSourceMana(card)) return true;
        if (!HasPriority(card.ControllerPlayerID)) return false;

        ActivatedAbility ability = card.GetFirstActivatableManaAbility(this);
        if (ability == null)
        {
            Debug.LogWarning("No mana ability available.");
            return false;
        }

        ActivateRuntimeAbility(ability, card);
        Debug.Log($"Activated mana ability: {ability.Name} on {card.Data.cardName}");
        return true;
    }

    private void ActivateRuntimeAbility(ActivatedAbility ability, RuntimeCard card)
    {
        if (ability == null || card == null) return;

        bool lifecycleHandledByScriptPayment =
            !string.IsNullOrWhiteSpace(ability.ResolveHook);
        if (!lifecycleHandledByScriptPayment)
        {
            PublishAbilityActivated(card);
        }

        ability.Activate(this, card);

        if (!lifecycleHandledByScriptPayment && !HasActiveChoice())
        {
            PublishAbilityResolved(card);
        }
    }

    private void SetCurrentPhase(TurnPhase phase)
    {
        if (phase != TurnPhase.DeclareAttackers)
        {
            pendingAttackers.Clear();
            pendingAttackTargets.Clear();
        }
        ClearManaPools();
        currentPhase = phase;
        GameEvents.TriggerPhaseChanged(currentPhase);
    }

    private bool IsMainPhase()
    {
        return currentPhase == TurnPhase.Main1 || currentPhase == TurnPhase.Main2;
    }

    private bool IsOnlineMatch()
    {
        return onlineAuthoritySession
            || onlineViewOnly
            || (OnlineMatchService.Instance != null && OnlineMatchService.Instance.IsInMatch);
    }

    private bool IsSorcerySpeedWindow(int player)
    {
        return priorityState.IsOpen
            && priorityState.PlayerWithPriority == player
            && player == currentPlayer
            && IsMainPhase()
            && !HasPendingStack();
    }

    private bool ShouldAutoPassPriority()
    {
        if (!priorityState.IsOpen) return false;
        int player = priorityState.PlayerWithPriority;
        return PriorityAutomationPolicy.ShouldAutoPass(
            IsOnlineMatch(),
            player,
            IsFullControl(player),
            player == currentPlayer,
            currentPhase,
            HasAvailablePriorityAction(player));
    }

    private bool HasAvailablePriorityAction(int player)
    {
        if (!HasPriority(player) || HasActiveChoice()) return false;

        if (currentPhase == TurnPhase.DeclareAttackers
            && player == currentPlayer
            && !HasDeclaredAttackers())
        {
            if (pendingAttackers.Count > 0) return true;
            IReadOnlyList<RuntimeCard> attackers = GetBattlefieldCards(player);
            for (int i = 0; i < attackers.Count; i++)
            {
                if (CanDeclareAttacker(player, i)) return true;
            }
        }

        if (currentPhase == TurnPhase.DeclareBlockers && player != currentPlayer)
        {
            IReadOnlyList<RuntimeCard> blockers = GetBattlefieldCards(player);
            IReadOnlyList<RuntimeCard> attackers = GetBattlefieldCards(currentPlayer);
            for (int blockerIndex = 0; blockerIndex < blockers.Count; blockerIndex++)
            {
                if (CanCancelBlocker(player, blockerIndex)) return true;
                for (int attackerIndex = 0; attackerIndex < attackers.Count; attackerIndex++)
                {
                    if (CanDeclareBlocker(player, blockerIndex, attackers[attackerIndex])) return true;
                }
            }
        }

        List<RuntimeCard> hand = GetHandList(player);
        for (int i = 0; i < hand.Count; i++)
        {
            if (GetPlayableHandFeatures(player, i).Count > 0) return true;
        }

        List<RuntimeCard> exile = GetExileList(player);
        for (int i = 0; i < exile.Count; i++)
        {
            if (CanCastExileCard(player, i)) return true;
        }

        IReadOnlyList<RuntimeCard> battlefield = GetBattlefieldCards(player);
        for (int cardIndex = 0; cardIndex < battlefield.Count; cardIndex++)
        {
            RuntimeCard card = battlefield[cardIndex];
            if (card == null) continue;
            for (int abilityIndex = 0; abilityIndex < card.Abilities.Count; abilityIndex++)
            {
                ActivatedAbility ability = card.Abilities[abilityIndex];
                if (ability != null && !ability.IsManaAbility && ability.IsActivatable(this, card))
                    return true;
            }
        }

        return false;
    }

    private void BeginPriority(int firstPlayer)
    {
        RunPriorityCheckpoint();
        if (IsGameOver() || HasActiveChoice()) return;
        priorityState.Begin(firstPlayer);
        ContinueAutomaticPriority();
    }

    public bool PassPriority(int player)
    {
        if (IsGameOver()) return false;
        if (HasActiveChoice()) return false;
        PriorityPassOutcome outcome = priorityState.TryPass(player, currentPlayer);
        if (outcome == PriorityPassOutcome.Rejected) return false;

        if (outcome == PriorityPassOutcome.WindowComplete)
        {
            if (HasPendingStack())
            {
                ResolveTopOfStack();
                BeginPriority(currentPlayer);
            }
            else
            {
                ProceedToNextPhase();
            }

            return true;
        }

        ContinueAutomaticPriority();
        return true;
    }

    private void ContinueAutomaticPriority()
    {
        if (resolvingAutomaticPriority) return;

        resolvingAutomaticPriority = true;
        int guard = 0;
        while (priorityState.IsOpen && guard++ < 64 && ShouldAutoPassPriority())
        {
            int playerToPass = priorityState.PlayerWithPriority;
            if (!PassPriority(playerToPass)) break;
        }
        resolvingAutomaticPriority = false;
    }

    private void ProceedToNextPhase()
    {
        if (IsGameOver()) return;
        if (HasActiveChoice())
        {
            Debug.LogWarning("Finish the current choice before changing phases.");
            return;
        }

        if (HasPendingStack())
        {
            Debug.LogWarning("Players must pass priority before the stack resolves.");
            BeginPriority(currentPlayer);
            return;
        }

        switch (currentPhase)
        {
            case TurnPhase.Untap:
                UntapPhase();
                SetCurrentPhase(TurnPhase.Upkeep);
                BeginPriority(currentPlayer);
                break;
            case TurnPhase.Upkeep:
                SetCurrentPhase(TurnPhase.Draw);
                DrawPhase();
                BeginPriority(currentPlayer);
                break;
            case TurnPhase.Draw:
                SetCurrentPhase(TurnPhase.Main1);
                MainPhase();
                AdvanceSagaLoreCounters(currentPlayer);
                BeginPriority(currentPlayer);
                break;
            case TurnPhase.Main1:
                SetCurrentPhase(TurnPhase.BeginningOfCombat);
                BeginPriority(currentPlayer);
                break;
            case TurnPhase.BeginningOfCombat:
                SetCurrentPhase(TurnPhase.DeclareAttackers);
                CombatPhase();
                AutoDeclareAttackers();
                BeginPriority(currentPlayer);
                break;
            case TurnPhase.DeclareAttackers:
                if (!HasAttackingCreatures())
                {
                    SetCurrentPhase(TurnPhase.EndOfCombat);
                    BeginPriority(currentPlayer);
                    break;
                }

                SetCurrentPhase(TurnPhase.DeclareBlockers);
                AutoDeclareBlockers();
                BeginPriority(currentPlayer);
                break;
            case TurnPhase.DeclareBlockers:
                if (HasIllegalMenaceBlocks())
                {
                    Debug.LogWarning("Menace attackers must be blocked by two or more creatures.");
                    BeginPriority(currentPlayer);
                    break;
                }

                SetCurrentPhase(TurnPhase.CombatDamage);
                if (!TryStartCombatAnimation())
                {
                    FinishCombatDamageAfterAnimation();
                }
                break;
            case TurnPhase.CombatDamage:
                SetCurrentPhase(TurnPhase.EndOfCombat);
                BeginPriority(currentPlayer);
                break;
            case TurnPhase.EndOfCombat:
                ClearCombatStates();
                SetCurrentPhase(TurnPhase.Main2);
                MainPhase();
                BeginPriority(currentPlayer);
                break;
            case TurnPhase.Main2:
                SetCurrentPhase(TurnPhase.End);
                EndPhase();
                BeginPriority(currentPlayer);
                break;
            case TurnPhase.End:
                SetCurrentPhase(TurnPhase.Cleanup);
                CleanupStep();
                break;
            case TurnPhase.Cleanup:
                CleanupStep();
                break;
        }
    }

    private bool TryStartCombatAnimation()
    {
        if (combatAnimationInProgress) return true;
        if (onlineAuthoritySession) return false;

        bool handled = GameEvents.TriggerCombatAnimationRequested(FinishCombatDamageAfterAnimation);
        if (!handled) return false;

        combatAnimationInProgress = true;
        priorityState.Close();
        return true;
    }

    private void FinishCombatDamageAfterAnimation()
    {
        if (currentPhase != TurnPhase.CombatDamage) return;

        combatAnimationInProgress = false;
        ResolveCombat(() => BeginPriority(currentPlayer));
    }

    private void UntapPhase()
    {
        Debug.Log($"Player {currentPlayer} is in Untap Phase.");
        
        List<RuntimeCard> currentBattlefield = currentPlayer == 1 ? player1Battlefield : player2Battlefield;
        foreach (var card in currentBattlefield)
        {
            card.Untap();
        }
    }

    private void DrawPhase()
    {
        Debug.Log($"Player {currentPlayer} is in Draw Phase.");
        if (currentPlayer == 1)
        {
            DrawCards(player1Library, player1Hand, 1, 1);
        }
        else
        {
            DrawCards(player2Library, player2Hand, 1, 2);
        }
    }

    private void StartTurn()
    {
        landsPlayedThisTurn = 0;
        SetCurrentPhase(TurnPhase.Untap);
        Debug.Log($"Player {currentPlayer}'s turn starts!");
        ProceedToNextPhase();
    }

    private void MainPhase()
    {
        Debug.Log($"Player {currentPlayer} is in Main Phase.");
    }

    private void CombatPhase()
    {
        Debug.Log($"Player {currentPlayer} is in Combat Phase. Declare Attackers!");
        // Note: Real game would wait for UI input.
    }

    private void EndPhase()
    {
        Debug.Log($"Player {currentPlayer} is in End Step.");
        QueueCaseCheckAbilities();
    }

    private void CleanupStep()
    {
        Debug.Log($"Player {currentPlayer} is in Cleanup Step.");

        ClearCombatStates();
        ClearDamageOnBattlefield();
        ClearEndOfTurnEffectsOnBattlefield();
        RemoveScriptContinuousEffectsByDuration(RuntimeEffectDuration.UntilEndOfTurn);
        eventPipeline.EndTurn();
        damageReplacements.RemoveAll(replacement =>
            replacement == null || replacement.UntilEndOfTurn);
        player1DamagePreventionRemaining = 0;
        player2DamagePreventionRemaining = 0;

        if (GetHandCount(currentPlayer) > GetMaxHandSize(currentPlayer))
        {
            BeginDiscardToHandSize(currentPlayer);
            return;
        }

        CompleteCleanupStep();
    }

    private void BeginDiscardToHandSize(int playerID)
    {
        int requiredCount = Mathf.Max(0, GetHandCount(playerID) - GetMaxHandSize(playerID));
        if (requiredCount <= 0)
        {
            CompleteCleanupStep();
            return;
        }

        GameChoiceRequest request = BeginChoice(
            GameChoiceKind.DiscardToHandSize,
            playerID,
            $"Choose {requiredCount} card(s) to discard.",
            requiredCount,
            requiredCount,
            false);
        if (request == null) return;

        List<RuntimeCard> hand = GetHandList(playerID);
        for (int i = 0; i < hand.Count; i++)
        {
            RuntimeCard card = hand[i];
            request.options.Add(new GameChoiceOption(
                i,
                GameChoiceZone.Hand,
                playerID,
                i,
                true,
                card != null && card.Data != null ? card.Data.cardName : string.Empty));
        }

        priorityState.Close();

        if (playerID != 1 && !IsOnlineMatch())
        {
            // Until opponent decisions are network-driven, discard the newest cards.
            for (int i = hand.Count - 1; i >= 0 && request.selectedOptionIds.Count < requiredCount; i--)
            {
                request.Toggle(i);
            }
            ConfirmDiscardToMaxHandSize(playerID);
            return;
        }

        Debug.Log($"Player {playerID} must discard {GetDiscardToHandSizeRemaining()} card(s) down to maximum hand size.");
    }

    private void CompleteCleanupStep()
    {
        ClearActiveChoice(GameChoiceKind.DiscardToHandSize);

        if (RunPriorityCheckpoint())
        {
            BeginPriority(currentPlayer);
            return;
        }

        currentPlayer = currentPlayer == 1 ? 2 : 1;
        turnNumber++;
        StartTurn();
    }

    public bool CanDiscardHandCardToMaxHandSize(int playerID, int handIndex)
    {
        if (!IsDiscardingToHandSize() || activeChoice.choosingPlayerId != playerID) return false;
        GameChoiceOption option = activeChoice.FindOption(handIndex);
        if (option == null || !option.legal || option.zone != GameChoiceZone.Hand) return false;
        return activeChoice.IsSelected(handIndex)
            || activeChoice.selectedOptionIds.Count < activeChoice.maximumSelections;
    }

    public bool ToggleDiscardHandCardToMaxHandSize(int playerID, int handIndex)
    {
        if (!CanDiscardHandCardToMaxHandSize(playerID, handIndex)) return false;

        return TryToggleChoiceOption(playerID, handIndex);
    }

    public bool ConfirmDiscardToMaxHandSize(int playerID)
    {
        if (!CanConfirmDiscardToHandSize(playerID)) return false;

        List<RuntimeCard> hand = GetHandList(playerID);
        if (hand == null || GetGraveyardList(playerID) == null) return false;

        var selectedCards = new List<RuntimeCard>(activeChoice.selectedOptionIds.Count);
        for (int i = 0; i < activeChoice.selectedOptionIds.Count; i++)
        {
            GameChoiceOption option = activeChoice.FindOption(activeChoice.selectedOptionIds[i]);
            if (option == null || option.zoneIndex < 0 || option.zoneIndex >= hand.Count) return false;
            RuntimeCard selectedCard = hand[option.zoneIndex];
            if (selectedCard == null || selectedCards.Contains(selectedCard)) return false;
            selectedCards.Add(selectedCard);
        }

        for (int i = 0; i < selectedCards.Count; i++)
        {
            RuntimeCard card = selectedCards[i];
            if (!MoveCardToZone(card, GameZone.Graveyard, false, false))
            {
                return false;
            }
            Debug.Log($"Player {playerID} discards {card.Data.cardName}.");
        }

        CompleteCleanupStep();
        return true;
    }

    public bool DiscardHandCardToMaxHandSize(int playerID, int handIndex)
    {
        if (!CanDiscardHandCardToMaxHandSize(playerID, handIndex)) return false;
        if (!ToggleDiscardHandCardToMaxHandSize(playerID, handIndex)) return false;
        return !CanConfirmDiscardToHandSize(playerID) || ConfirmDiscardToMaxHandSize(playerID);
    }

    private void ClearCombatStates()
    {
        ClearCombatDamageStepSnapshot();
        foreach (var card in player1Battlefield) card.RemoveFromCombat();
        foreach (var card in player2Battlefield) card.RemoveFromCombat();
    }

    private void ClearDamageOnBattlefield()
    {
        foreach (var card in player1Battlefield) card.ClearDamage();
        foreach (var card in player2Battlefield) card.ClearDamage();
    }

    private void ClearEndOfTurnEffectsOnBattlefield()
    {
        foreach (var card in player1Battlefield) card.ClearEndOfTurnEffects();
        foreach (var card in player2Battlefield) card.ClearEndOfTurnEffects();
    }

    public bool PlayCard(int player, int handIndex)
    {
        return PlayCardInternal(player, handIndex, null, 0, false);
    }

    public bool PlayCardWithTarget(int player, int handIndex, RuntimeCard target)
    {
        return PlayCardInternal(player, handIndex, target, 0, true);
    }

    public bool PlayCardWithPlayerTarget(int player, int handIndex, int targetPlayerID)
    {
        return PlayCardInternal(player, handIndex, null, targetPlayerID, true);
    }

    public bool CanCastExileCard(int player, int exileIndex)
    {
        return string.IsNullOrEmpty(GetExileCardCastIssue(player, exileIndex));
    }

    public string GetExileCardCastIssue(int player, int exileIndex)
    {
        List<RuntimeCard> exile = GetExileList(player);
        if (exile == null || exileIndex < 0 || exileIndex >= exile.Count)
        {
            return "That exiled card is unavailable.";
        }

        if (IsGameOver()) return "The match has ended.";
        if (activeChoice != null)
        {
            bool confirmingThisCard =
                (activeChoice.kind == GameChoiceKind.SpellTarget
                    || activeChoice.kind == GameChoiceKind.CastConfirmation)
                && activeChoice.sourcePlayerId == player
                && activeChoice.sourceZone == GameChoiceZone.Exile
                && activeChoice.sourceZoneIndex == exileIndex;
            if (!confirmingThisCard) return "Finish the current choice first.";
        }
        if (!HasPriority(player)) return "You do not have priority.";

        RuntimeCard card = exile[exileIndex];
        if (onlineViewOnly)
        {
            return card.OnlineCanCastFromExile
                ? string.Empty
                : "The authoritative match state does not allow this exiled card to be cast now.";
        }
        CardFeatureRuntime feature = GetExileCastFeature(card);
        if (feature == null) return "That card cannot be cast from exile.";
        card.SetSpellFeature(feature);

        if (!feature.IsInstantOrSorcery && !IsSorcerySpeedWindow(player))
        {
            return $"{feature.CardName} can only be cast during your main phase while the stack is empty.";
        }

        if (RequiresTarget(card) && !HasAnyLegalTarget(card))
        {
            return $"{feature.CardName} has no legal {GetTargetModeLabel(card.GetTargetMode())} target.";
        }

        if (!CanPayAnyDeclaredManaCost(player, card, feature))
        {
            return $"Not enough mana to cast {feature.CardName}.";
        }

        return string.Empty;
    }

    public bool CastExileCard(int player, int exileIndex)
    {
        return CastExileCardInternal(player, exileIndex, null, 0, false);
    }

    public bool CastExileCardWithTarget(
        int player,
        int exileIndex,
        RuntimeCard target)
    {
        return CastExileCardInternal(player, exileIndex, target, 0, true);
    }

    public bool CastExileCardWithPlayerTarget(
        int player,
        int exileIndex,
        int targetPlayerID)
    {
        return CastExileCardInternal(player, exileIndex, null, targetPlayerID, true);
    }

    private bool CastExileCardInternal(
        int player,
        int exileIndex,
        RuntimeCard targetCard,
        int targetPlayerID,
        bool targetProvided)
    {
        string issue = GetExileCardCastIssue(player, exileIndex);
        if (!string.IsNullOrEmpty(issue))
        {
            Debug.LogWarning(issue);
            return false;
        }

        List<RuntimeCard> exile = GetExileList(player);
        RuntimeCard card = exile[exileIndex];
        CardFeatureRuntime feature = GetExileCastFeature(card);
        card.SetSpellFeature(feature);

        if (RequiresTarget(card))
        {
            if (!targetProvided) return false;
            if (targetCard != null)
            {
                if (!CanSpellTargetCard(card, targetCard)) return false;
                card.SetSpellTarget(targetCard);
            }
            else if (targetPlayerID > 0)
            {
                if (!CanSpellTargetPlayer(card, targetPlayerID)) return false;
                card.SetSpellTargetPlayer(targetPlayerID);
            }
            else
            {
                return false;
            }
        }
        else
        {
            card.ClearSpellTarget();
        }

        if (!TryPayMana(player, feature.ManaCost))
        {
            card.ClearSpellTarget();
            return false;
        }

        exile.RemoveAt(exileIndex);
        if (card.IsSpellCopy)
        {
            card.ClearPreparedLink();
        }
        else
        {
            card.ConsumeAdventurePermission();
        }

        MarkSpellMovedToStack(card, targetCard, targetPlayerID, player);
        PushStackItem(GameStackItem.Spell(card, feature));
        Debug.Log($"Player {player} casts {feature.CardName} from exile.");
        NotifyStackChanged();
        BeginPriority(player);
        return true;
    }

    private CardFeatureRuntime GetExileCastFeature(RuntimeCard card)
    {
        if (card == null) return null;
        if (card.IsSpellCopy
            && card.PreparedSource != null
            && card.PreparedSource.IsPrepared
            && IsOnBattlefield(card.PreparedSource)
            && card.SpellFeature != null
            && card.SpellFeature.Role == CardFeatureRole.Prepare)
        {
            return card.SpellFeature;
        }

        return card.CanCastPrimaryFromAdventureExile
            ? card.PrimaryFeature
            : null;
    }

    private bool PlayCardInternal(int player, int handIndex, RuntimeCard targetCard, int targetPlayerID, bool targetProvided)
    {
        List<RuntimeCard> hand = player == 1 ? player1Hand : player2Hand;
        List<RuntimeCard> battlefield = player == 1 ? player1Battlefield : player2Battlefield;

        if (handIndex >= 0 && handIndex < hand.Count)
        {
            RuntimeCard card = hand[handIndex];
            CardFeatureRuntime feature = GetDefaultCastFeature(card);
            string playIssue = feature == null ? "No castable card feature is available." : string.Empty;

            if (feature == null || !TryGetCanPlayFeatureReason(player, handIndex, feature, out playIssue))
            {
                Debug.LogWarning(playIssue);
                return false;
            }
            card.SetSpellFeature(feature);

            if (RequiresTarget(card))
            {
                if (!targetProvided)
                {
                    Debug.LogWarning($"{feature.CardName} requires a target.");
                    return false;
                }

                if (targetCard != null)
                {
                    if (!CanSpellTargetCard(card, targetCard))
                    {
                        Debug.LogWarning($"{targetCard.Data.cardName} is not a legal target for {feature.CardName}.");
                        return false;
                    }

                    card.SetSpellTarget(targetCard);
                }
                else if (targetPlayerID > 0)
                {
                    if (!CanSpellTargetPlayer(card, targetPlayerID))
                    {
                        Debug.LogWarning($"Player {targetPlayerID} is not a legal target for {feature.CardName}.");
                        return false;
                    }

                    card.SetSpellTargetPlayer(targetPlayerID);
                }
                else
                {
                    Debug.LogWarning($"{feature.CardName} requires a target.");
                    return false;
                }
            }
            else
            {
                card.ClearSpellTarget();
            }

            // 1. Check Card Type & Costs
            if (feature.HasType(CardType.Land))
            {
                landsPlayedThisTurn++;
            }
            else // Spells (Creature, Sorcery, etc.)
            {
                ManaCost manaCost = feature.ManaCost;
                if (!TryPayMana(player, manaCost))
                {
                    card.ClearSpellTarget();
                    if (GetPotentialMana(manaCost, player))
                        Debug.LogWarning($"Not enough mana in pool. Tap your lands first! (Need {manaCost.ToSymbolString()}, pool has {GetMana(player)})");
                    else
                        Debug.LogWarning($"Not enough mana to play {feature.CardName}. Cost: {manaCost.ToSymbolString()}");
                    return false;
                }
            }

            // 2. Move Card
            hand.RemoveAt(handIndex);
            if (feature.HasType(CardType.Land))
            {
                card.ClearSpellFeature();
                AddToBattlefield(battlefield, card);
                Debug.Log($"Player {player} played land: {feature.CardName}");
                GameEvents.TriggerCardPlayed(card);
                NotifyStackChanged();
                BeginPriority(player);
                return true;
            }

            MarkSpellMovedToStack(card, targetCard, targetPlayerID, player);
            PushStackItem(GameStackItem.Spell(card, feature));
            PublishGameEvent(new GameEventContext
            {
                type = GameEventType.SpellCast,
                source = card,
                card = card,
                playerId = player,
                controllerId = player,
                fromZone = GameZone.Hand,
                toZone = GameZone.Stack
            });
            Debug.Log($"Player {player} puts {feature.CardName} on the stack.");
            NotifyStackChanged();
            BeginPriority(player);
            return true;
        }

        return false;
    }

    private void MarkSpellMovedToStack(
        RuntimeCard card,
        RuntimeCard targetCard,
        int targetPlayerID,
        int controllerPlayerID)
    {
        if (card == null) return;

        card.MarkZoneChange();
        card.ReassignObjectId(nextObjectId++);
        card.SetController(controllerPlayerID);
        card.SetCurrentZone(GameZone.Stack);
        if (targetCard != null)
        {
            card.SetSpellTarget(targetCard);
        }
        else if (targetPlayerID > 0)
        {
            card.SetSpellTargetPlayer(targetPlayerID);
        }
    }

    private bool TryPayMana(int playerID, ManaCost cost)
    {
        if (cost.IsEmpty) return true;
        if (SpendMana(playerID, cost)) return true;
        if (!ActivateManaAbilitiesForCost(playerID, cost)) return false;
        return SpendMana(playerID, cost);
    }

    private bool CanPayWithPoolOrAvailableMana(int playerID, ManaCost cost)
    {
        return cost.IsEmpty || GetManaPool(playerID).CanPay(cost) || GetPotentialMana(cost, playerID);
    }

    private bool ActivateManaAbilitiesForCost(int playerID, ManaCost cost)
    {
        List<RuntimeCard> battlefield = playerID == 1 ? player1Battlefield : player2Battlefield;
        int guard = 0;
        while (!GetManaPool(playerID).CanPay(cost) && guard++ < battlefield.Count)
        {
            ActivatedAbility ability = FindBestManaAbility(playerID, cost, out RuntimeCard source);
            if (source == null || ability == null) break;
            ActivateRuntimeAbility(ability, source);
        }

        return GetManaPool(playerID).CanPay(cost);
    }

    private ActivatedAbility FindBestManaAbility(int playerID, ManaCost cost, out RuntimeCard source)
    {
        ManaPool pool = GetManaPool(playerID);

        ActivatedAbility coloredNeed = FindUntappedManaAbilityProducing(playerID, cost.White - pool.White, ManaColor.White, out source);
        if (coloredNeed != null) return coloredNeed;
        coloredNeed = FindUntappedManaAbilityProducing(playerID, cost.Blue - pool.Blue, ManaColor.Blue, out source);
        if (coloredNeed != null) return coloredNeed;
        coloredNeed = FindUntappedManaAbilityProducing(playerID, cost.Black - pool.Black, ManaColor.Black, out source);
        if (coloredNeed != null) return coloredNeed;
        coloredNeed = FindUntappedManaAbilityProducing(playerID, cost.Red - pool.Red, ManaColor.Red, out source);
        if (coloredNeed != null) return coloredNeed;
        coloredNeed = FindUntappedManaAbilityProducing(playerID, cost.Green - pool.Green, ManaColor.Green, out source);
        if (coloredNeed != null) return coloredNeed;
        coloredNeed = FindUntappedManaAbilityProducing(playerID, cost.Colorless - pool.Colorless, ManaColor.Colorless, out source);
        if (coloredNeed != null) return coloredNeed;

        return FindFirstUntappedManaAbility(playerID, out source);
    }

    private ActivatedAbility FindUntappedManaAbilityProducing(int playerID, int needed, ManaColor color, out RuntimeCard source)
    {
        source = null;
        if (needed <= 0) return null;
        List<RuntimeCard> battlefield = playerID == 1 ? player1Battlefield : player2Battlefield;
        for (int i = 0; i < battlefield.Count; i++)
        {
            RuntimeCard card = battlefield[i];
            if (card == null || card.IsTapped) continue;
            for (int abilityIndex = 0; abilityIndex < card.Abilities.Count; abilityIndex++)
            {
                ActivatedAbility ability = card.Abilities[abilityIndex];
                if (ability == null || !ability.IsManaAbility || !ability.HasProducedMana) continue;
                if (ability.ProducedMana.GetAmount(color) <= 0) continue;
                if (!ability.IsActivatable(this, card)) continue;

                source = card;
                return ability;
            }
        }

        return null;
    }

    private ActivatedAbility FindFirstUntappedManaAbility(int playerID, out RuntimeCard source)
    {
        source = null;
        List<RuntimeCard> battlefield = playerID == 1 ? player1Battlefield : player2Battlefield;
        for (int i = 0; i < battlefield.Count; i++)
        {
            RuntimeCard card = battlefield[i];
            if (card == null || card.IsTapped) continue;
            ActivatedAbility ability = card.GetFirstActivatableManaAbility(this);
            if (ability == null || !ability.HasProducedMana) continue;

            source = card;
            return ability;
        }

        return null;
    }

    private void AutoDeclareBlockers()
    {
        if (IsOnlineMatch()) return;
        int defenderPlayerID = currentPlayer == 1 ? 2 : 1;
        if (defenderPlayerID == 1) return;

        List<RuntimeCard> attackers = currentPlayer == 1 ? player1Battlefield : player2Battlefield;
        List<RuntimeCard> blockers = defenderPlayerID == 1 ? player1Battlefield : player2Battlefield;

        for (int attackerIndex = 0; attackerIndex < attackers.Count; attackerIndex++)
        {
            RuntimeCard attacker = attackers[attackerIndex];
            if (!attacker.IsAttacking || attacker.Blockers.Count > 0) continue;

            int requiredBlockers = attacker.HasKeyword("Menace") ? 2 : 1;
            List<int> chosenBlockers = new List<int>(requiredBlockers);

            for (int blockerIndex = 0; blockerIndex < blockers.Count; blockerIndex++)
            {
                RuntimeCard blocker = blockers[blockerIndex];
                if (!CanBlockAttacker(blocker, attacker)) continue;

                chosenBlockers.Add(blockerIndex);
                if (chosenBlockers.Count >= requiredBlockers) break;
            }

            if (chosenBlockers.Count < requiredBlockers)
            {
                continue;
            }

            for (int i = 0; i < chosenBlockers.Count; i++)
            {
                DeclareBlocker(defenderPlayerID, chosenBlockers[i], attacker);
            }
        }
    }

    private void AutoDeclareAttackers()
    {
        if (IsOnlineMatch()) return;
        if (currentPlayer == 1) return;
        if (currentPhase != TurnPhase.DeclareAttackers || HasPendingStack()) return;

        List<RuntimeCard> attackers = player2Battlefield;
        for (int i = 0; i < attackers.Count; i++)
        {
            RuntimeCard attacker = attackers[i];
            if (!CanAttack(attacker)) continue;

            DeclareAttackerCard(attacker, GetDefendingPlayer(attacker.ControllerPlayerID));
        }
    }

    // New Combat System: Step 1 - Declare Attacker
    public void DeclareAttacker(int attackerPlayerID, int attackerIndex)
    {
        if (IsGameOver()) return;
        if (currentPhase != TurnPhase.DeclareAttackers)
        {
            Debug.LogWarning("Not in Declare Attackers Step!");
            return;
        }

        if (HasPendingStack())
        {
            Debug.LogWarning("Resolve the stack before declaring attackers!");
            return;
        }

        if (attackerPlayerID != currentPlayer)
        {
            Debug.LogWarning("Not your turn!");
            return;
        }

        List<RuntimeCard> attackerBattlefield = attackerPlayerID == 1 ? player1Battlefield : player2Battlefield;
        
        if (attackerIndex >= 0 && attackerIndex < attackerBattlefield.Count)
        {
            RuntimeCard attacker = attackerBattlefield[attackerIndex];

            if (!CanAttack(attacker))
            {
                Debug.LogWarning($"{attacker.Data.cardName} cannot attack now.");
                return;
            }

            DeclareAttackerCard(attacker, GetDefendingPlayer(attacker.ControllerPlayerID));
        }
    }

    private void DeclareAttackerCard(RuntimeCard attacker, object attackTarget)
    {
        if (attacker == null) return;

        if (!CanAttackTarget(attacker, attackTarget))
        {
            attackTarget = GetDefendingPlayer(attacker.ControllerPlayerID);
        }

        if (!attacker.HasKeyword("Vigilance"))
        {
            attacker.Tap();
        }

        int defenderPlayerID = GetDefendingPlayer(attacker.ControllerPlayerID);
        attacker.DeclareAttack(attackTarget);
        GameEvents.TriggerAttackDeclared(attacker, defenderPlayerID);
        string targetName = attackTarget is RuntimeCard permanent
            ? permanent.GetCardName()
            : $"Player {defenderPlayerID}";
        Debug.Log($"{attacker.Data.cardName} attacks {targetName}!");
    }

    // New Combat System: Step 2 - Declare Blocker
    public bool DeclareBlocker(int blockerPlayerID, int blockerIndex, RuntimeCard targetAttacker)
    {
        if (IsGameOver()) return false;
        if (currentPhase != TurnPhase.DeclareBlockers)
        {
            Debug.LogWarning("Not in Declare Blockers Step!");
            return false;
        }

        // Must be opponent's turn to block
        if (blockerPlayerID == currentPlayer)
        {
            Debug.LogWarning("Cannot block during your own turn!");
            return false;
        }

        if (!HasPriority(blockerPlayerID))
        {
            Debug.LogWarning("The blocking player does not have priority.");
            return false;
        }

        if (HasPendingStack())
        {
            Debug.LogWarning("Resolve the stack before declaring blockers!");
            return false;
        }

        List<RuntimeCard> blockerBattlefield = blockerPlayerID == 1 ? player1Battlefield : player2Battlefield;

        if (blockerIndex >= 0 && blockerIndex < blockerBattlefield.Count)
        {
            RuntimeCard blocker = blockerBattlefield[blockerIndex];

            if (!CanBlockAttacker(blocker, targetAttacker))
            {
                Debug.LogWarning($"{blocker.Data.cardName} cannot block {targetAttacker?.Data.cardName ?? "that attacker"}.");
                return false;
            }

            // Set State
            blocker.DeclareBlock(targetAttacker);
            GameEvents.TriggerBlockDeclared(blocker, targetAttacker);
            Debug.Log($"{blocker.Data.cardName} blocks {targetAttacker.Data.cardName}!");
            return true;
        }

        return false;
    }

    public bool CancelBlocker(int blockerPlayerID, int blockerIndex)
    {
        if (!CanCancelBlocker(blockerPlayerID, blockerIndex))
        {
            return false;
        }

        RuntimeCard blocker = GetBattlefieldCard(blockerPlayerID, blockerIndex);
        RuntimeCard targetAttacker = blocker.BlockingTarget;
        blocker.RemoveFromCombat();
        if (targetAttacker != null && targetAttacker.Blockers.Count == 0)
        {
            targetAttacker.WasBlockedThisCombat = false;
        }
        Debug.Log($"{blocker.Data.cardName} no longer blocks {targetAttacker?.Data.cardName ?? "that attacker"}.");
        return true;
    }

    // New Combat System: Step 3 - Resolve Combat Damage
    public void ResolveCombat()
    {
        ResolveCombat(null);
    }

    private void ResolveCombat(System.Action onCompleted)
    {
        if (IsGameOver())
        {
            onCompleted?.Invoke();
            return;
        }
        if (HasPendingStack())
        {
            Debug.LogWarning("Resolve the stack before combat damage!");
            onCompleted?.Invoke();
            return;
        }

        Debug.Log("Resolving Combat Damage...");

        CaptureCombatDamageStepSnapshot();
        bool hasFirstStrikeStep = firstCombatDamageStepSpecialStrikers.Count > 0;
        if (hasFirstStrikeStep)
        {
            ResolveCombatDamagePass(
                true,
                () => ResolveCombatDamagePass(
                    false,
                    () =>
                    {
                        ClearCombatDamageStepSnapshot();
                        onCompleted?.Invoke();
                    }));
            return;
        }

        ResolveCombatDamagePass(
            false,
            () =>
            {
                ClearCombatDamageStepSnapshot();
                onCompleted?.Invoke();
            });
    }

    private void ResolveCombatDamagePass(
        bool firstStrikePass,
        System.Action onCompleted)
    {
        BuildCombatDamageAssignmentsWithChoices(firstStrikePass, assignments =>
        {
            PrepareCombatDamageWithChoices(assignments, preparedDamage =>
            {
                for (int i = 0; i < preparedDamage.Count; i++)
                {
                    PreparedCombatDamage prepared = preparedDamage[i];
                    if (prepared.Event.prevented || prepared.Event.amount <= 0) continue;

                    CombatDamageAssignment original = prepared.Assignment;
                    RuntimeCard redirectedCard = prepared.Event.card;
                    DealCombatDamageAssignment(new CombatDamageAssignment(
                        original.Source,
                        redirectedCard,
                        redirectedCard == null ? prepared.Event.playerId : 0,
                        prepared.Event.amount));
                    CompleteGameEvent(prepared.Event);
                }

                PerformStateBasedActions();
                onCompleted?.Invoke();
            });
        });
    }

    private void BuildCombatDamageAssignmentsWithChoices(
        bool firstStrikePass,
        System.Action<List<CombatDamageAssignment>> onCompleted)
    {
        List<RuntimeCard> attackers = currentPlayer == 1
            ? player1Battlefield
            : player2Battlefield;
        var assignments = new List<CombatDamageAssignment>();

        void BuildAt(int attackerIndex)
        {
            while (attackerIndex < attackers.Count
                && (attackers[attackerIndex] == null
                    || !attackers[attackerIndex].IsAttacking))
            {
                attackerIndex++;
            }

            if (attackerIndex >= attackers.Count)
            {
                onCompleted?.Invoke(assignments);
                return;
            }

            RuntimeCard attacker = attackers[attackerIndex];
            bool attackerCanAssign = CanAssignCombatDamageInPass(
                attacker,
                firstStrikePass);
            var currentBlockers = new List<RuntimeCard>();
            for (int i = 0; i < attacker.Blockers.Count; i++)
            {
                RuntimeCard blocker = attacker.Blockers[i];
                if (blocker == null || blocker.BlockingTarget != attacker) continue;
                currentBlockers.Add(blocker);
                if (CanAssignCombatDamageInPass(blocker, firstStrikePass)
                    && blocker.CurrentAttack > 0)
                {
                    assignments.Add(new CombatDamageAssignment(
                        blocker,
                        attacker,
                        0,
                        blocker.CurrentAttack));
                }
            }

            if (!attackerCanAssign || attacker.CurrentAttack <= 0)
            {
                BuildAt(attackerIndex + 1);
                return;
            }

            if (!attacker.IsBlocked && !attacker.WasBlockedThisCombat)
            {
                AssignAttackTargetCombatDamage(
                    attacker,
                    attacker.CurrentAttack,
                    assignments);
                BuildAt(attackerIndex + 1);
                return;
            }

            if (currentBlockers.Count == 0)
            {
                if (attacker.HasKeyword("Trample"))
                {
                    AssignAttackTargetCombatDamage(
                        attacker,
                        attacker.CurrentAttack,
                        assignments);
                }
                BuildAt(attackerIndex + 1);
                return;
            }

            bool requiresHumanAssignment = (IsOnlineMatch()
                    || attacker.ControllerPlayerID == 1)
                && (currentBlockers.Count > 1 || attacker.HasKeyword("Trample"));
            if (!requiresHumanAssignment)
            {
                AssignAttackerDamageAutomatically(
                    attacker,
                    currentBlockers,
                    assignments);
                BuildAt(attackerIndex + 1);
                return;
            }

            if (!BeginCombatDamageAssignmentChoice(
                    attacker,
                    currentBlockers,
                    result =>
                    {
                        AddChosenCombatDamageAssignments(
                            attacker,
                            currentBlockers,
                            result,
                            assignments);
                        BuildAt(attackerIndex + 1);
                    }))
            {
                AssignAttackerDamageAutomatically(
                    attacker,
                    currentBlockers,
                    assignments);
                BuildAt(attackerIndex + 1);
            }
        }

        BuildAt(0);
    }

    private List<CombatDamageAssignment> BuildCombatDamageAssignments(bool firstStrikePass)
    {
        List<RuntimeCard> attackers = currentPlayer == 1 ? player1Battlefield : player2Battlefield;
        List<CombatDamageAssignment> assignments = new List<CombatDamageAssignment>();

        foreach (var attacker in attackers)
        {
            if (attacker == null || !attacker.IsAttacking) continue;

            bool attackerCanAssignDamage = CanAssignCombatDamageInPass(attacker, firstStrikePass);

            if (attacker.IsBlocked || attacker.WasBlockedThisCombat)
            {
                AssignBlockedCombatDamage(attacker, assignments, firstStrikePass, attackerCanAssignDamage);
            }
            else if (attackerCanAssignDamage)
            {
                AssignAttackTargetCombatDamage(attacker, attacker.CurrentAttack, assignments);
            }
        }

        return assignments;
    }

    private void AssignBlockedCombatDamage(RuntimeCard attacker, List<CombatDamageAssignment> assignments, bool firstStrikePass, bool attackerCanAssignDamage)
    {
        int remainingAttackerDamage = attackerCanAssignDamage ? Mathf.Max(0, attacker.CurrentAttack) : 0;
        List<RuntimeCard> currentBlockers = new List<RuntimeCard>(attacker.Blockers.Count);

        for (int i = 0; i < attacker.Blockers.Count; i++)
        {
            RuntimeCard blocker = attacker.Blockers[i];
            if (blocker == null || blocker.BlockingTarget != attacker) continue;
            currentBlockers.Add(blocker);

            if (CanAssignCombatDamageInPass(blocker, firstStrikePass) && blocker.CurrentAttack > 0)
            {
                assignments.Add(new CombatDamageAssignment(blocker, attacker, 0, blocker.CurrentAttack));
            }
        }

        if (!attackerCanAssignDamage || remainingAttackerDamage <= 0) return;
        if (currentBlockers.Count == 0)
        {
            if (attacker.HasKeyword("Trample"))
            {
                AssignAttackTargetCombatDamage(attacker, remainingAttackerDamage, assignments);
            }

            return;
        }

        bool trample = attacker.HasKeyword("Trample");
        for (int i = 0; i < currentBlockers.Count && remainingAttackerDamage > 0; i++)
        {
            RuntimeCard blocker = currentBlockers[i];
            int assignedDamage = trample
                ? Mathf.Min(remainingAttackerDamage, GetMinimumDamageBeforeTrample(attacker, blocker))
                : i == 0
                    ? remainingAttackerDamage
                    : 0;

            if (assignedDamage <= 0) continue;

            assignments.Add(new CombatDamageAssignment(attacker, blocker, 0, assignedDamage));
            remainingAttackerDamage -= assignedDamage;
        }

        if (trample && remainingAttackerDamage > 0)
        {
            AssignAttackTargetCombatDamage(attacker, remainingAttackerDamage, assignments);
        }
    }

    private void AssignAttackTargetCombatDamage(
        RuntimeCard attacker,
        int damage,
        List<CombatDamageAssignment> assignments)
    {
        if (attacker == null || damage <= 0) return;
        if (attacker.AttackTarget is RuntimeCard permanentTarget)
        {
            if (IsOnBattlefield(permanentTarget))
            {
                assignments.Add(new CombatDamageAssignment(
                    attacker,
                    permanentTarget,
                    0,
                    damage));
            }
            return;
        }

        if (attacker.AttackTarget is int targetPlayerID)
        {
            assignments.Add(new CombatDamageAssignment(
                attacker,
                null,
                targetPlayerID,
                damage));
        }
    }

    private void AssignAttackerDamageAutomatically(
        RuntimeCard attacker,
        IReadOnlyList<RuntimeCard> blockers,
        List<CombatDamageAssignment> assignments)
    {
        int remaining = Mathf.Max(0, attacker?.CurrentAttack ?? 0);
        bool trample = attacker != null && attacker.HasKeyword("Trample");
        for (int i = 0; i < blockers.Count && remaining > 0; i++)
        {
            RuntimeCard blocker = blockers[i];
            int amount = trample
                ? Mathf.Min(remaining, GetMinimumDamageBeforeTrample(attacker, blocker))
                : i == 0 ? remaining : 0;
            if (amount <= 0) continue;
            assignments.Add(new CombatDamageAssignment(attacker, blocker, 0, amount));
            remaining -= amount;
        }
        if (trample && remaining > 0)
        {
            AssignAttackTargetCombatDamage(attacker, remaining, assignments);
        }
    }

    private bool BeginCombatDamageAssignmentChoice(
        RuntimeCard attacker,
        IReadOnlyList<RuntimeCard> blockers,
        System.Action<GameChoiceResult> onCompleted)
    {
        if (attacker == null || blockers == null || blockers.Count == 0) return false;
        int totalDamage = Mathf.Max(0, attacker.CurrentAttack);
        GameChoiceRequest request = BeginChoice(
            GameChoiceKind.CombatDamageAssignment,
            attacker.ControllerPlayerID,
            $"分配 {attacker.GetCardName()} 的 {totalDamage} 点战斗伤害",
            1,
            blockers.Count + (attacker.HasKeyword("Trample") ? 1 : 0),
            false);
        if (request == null) return false;

        request.selectionMode = GameChoiceSelectionMode.Distribution;
        request.requiredTotalAmount = totalDamage;
        for (int i = 0; i < blockers.Count; i++)
        {
            RuntimeCard blocker = blockers[i];
            request.options.Add(new GameChoiceOption(
                9200000 + i,
                GameChoiceZone.Battlefield,
                blocker.ControllerPlayerID,
                GetBattlefieldIndex(blocker),
                true,
                $"{blocker.GetCardName()}（致死值 {GetMinimumDamageBeforeTrample(attacker, blocker)}）",
                0,
                totalDamage,
                blocker.ObjectId,
                blocker.ZoneChangeVersion));
        }

        int attackTargetOptionId = 0;
        if (attacker.HasKeyword("Trample"))
        {
            attackTargetOptionId = 9299999;
            RuntimeCard permanent = attacker.AttackTarget as RuntimeCard;
            int player = attacker.AttackTarget is int playerTarget ? playerTarget : 0;
            request.options.Add(new GameChoiceOption(
                attackTargetOptionId,
                permanent != null ? GameChoiceZone.Battlefield : GameChoiceZone.Player,
                permanent?.ControllerPlayerID ?? player,
                permanent != null ? GetBattlefieldIndex(permanent) : -1,
                permanent != null ? IsOnBattlefield(permanent) : player > 0,
                permanent != null ? permanent.GetCardName() : $"玩家 {player}",
                0,
                totalDamage,
                permanent?.ObjectId ?? 0,
                permanent?.ZoneChangeVersion ?? -1));
        }

        pendingGenericChoiceCompletion = onCompleted;
        pendingGenericChoiceCancellation = null;
        pendingGenericChoiceValidator = choice =>
        {
            if (attackTargetOptionId == 0
                || choice.GetAllocatedAmount(attackTargetOptionId) <= 0)
            {
                return true;
            }

            for (int i = 0; i < blockers.Count; i++)
            {
                if (choice.GetAllocatedAmount(9200000 + i)
                    < GetMinimumDamageBeforeTrample(attacker, blockers[i]))
                {
                    return false;
                }
            }
            return true;
        };
        return true;
    }

    private void AddChosenCombatDamageAssignments(
        RuntimeCard attacker,
        IReadOnlyList<RuntimeCard> blockers,
        GameChoiceResult result,
        List<CombatDamageAssignment> assignments)
    {
        if (result == null) return;
        for (int i = 0; i < blockers.Count; i++)
        {
            int amount = GetChoiceAllocation(result, 9200000 + i);
            if (amount > 0)
            {
                assignments.Add(new CombatDamageAssignment(
                    attacker,
                    blockers[i],
                    0,
                    amount));
            }
        }

        int targetAmount = GetChoiceAllocation(result, 9299999);
        if (targetAmount > 0)
        {
            AssignAttackTargetCombatDamage(attacker, targetAmount, assignments);
        }
    }

    private static int GetChoiceAllocation(GameChoiceResult result, int optionId)
    {
        if (result?.allocations == null) return 0;
        for (int i = 0; i < result.allocations.Count; i++)
        {
            GameChoiceAllocation allocation = result.allocations[i];
            if (allocation != null && allocation.optionId == optionId)
            {
                return allocation.amount;
            }
        }
        return 0;
    }

    private void PrepareCombatDamageWithChoices(
        IReadOnlyList<CombatDamageAssignment> assignments,
        System.Action<List<PreparedCombatDamage>> onPrepared)
    {
        var prepared = new List<PreparedCombatDamage>(
            assignments?.Count ?? 0);
        if (assignments == null || assignments.Count == 0)
        {
            onPrepared?.Invoke(prepared);
            return;
        }

        void PrepareAt(int index)
        {
            while (index < assignments.Count
                && (assignments[index].Source == null
                    || assignments[index].Damage <= 0))
            {
                index++;
            }
            if (index >= assignments.Count)
            {
                onPrepared?.Invoke(prepared);
                return;
            }

            CombatDamageAssignment assignment = assignments[index];
            var gameEvent = new GameEventContext
            {
                type = GameEventType.Damage,
                source = assignment.Source,
                card = assignment.CardTarget,
                playerId = assignment.CardTarget != null
                    ? assignment.CardTarget.ControllerPlayerID
                    : assignment.PlayerTarget,
                controllerId = assignment.Source.ControllerPlayerID,
                amount = assignment.Damage,
                damageKind = DamageEventKind.Combat
            };
            PrepareGameEventWithChoice(gameEvent, resolved =>
            {
                prepared.Add(new PreparedCombatDamage
                {
                    Assignment = assignment,
                    Event = resolved
                });
                PrepareAt(index + 1);
            });
        }

        PrepareAt(0);
    }

    private int ConsumePlayerDamagePrevention(int player, int damage)
    {
        if (damage <= 0) return 0;

        int remaining = player == 1
            ? player1DamagePreventionRemaining
            : player == 2
                ? player2DamagePreventionRemaining
                : 0;
        if (remaining == PreventAllDamage) return damage;
        if (remaining <= 0) return 0;

        int prevented = Mathf.Min(remaining, damage);
        if (player == 1) player1DamagePreventionRemaining -= prevented;
        else if (player == 2) player2DamagePreventionRemaining -= prevented;
        return prevented;
    }

    private void DealCombatDamageAssignment(CombatDamageAssignment assignment)
    {
        if (assignment.Source == null || assignment.Damage <= 0) return;
        int damage = assignment.Damage;

        if (assignment.CardTarget != null)
        {
            Debug.Log($"{assignment.Source.Data.cardName} deals {damage} combat damage to {assignment.CardTarget.Data.cardName}.");
            if (assignment.Source.HasKeyword("Deathtouch")
                && assignment.CardTarget.HasType(CardType.Creature))
            {
                assignment.CardTarget.MarkDeathtouchDamage();
            }

            ApplyDamageToPermanent(assignment.Source, assignment.CardTarget, damage);
        }
        else if (assignment.PlayerTarget > 0)
        {
            Debug.Log($"{assignment.Source.Data.cardName} deals {damage} combat damage to Player {assignment.PlayerTarget}.");
            ApplyDamageToPlayerLife(assignment.PlayerTarget, damage);
        }

        if (assignment.Source.HasKeyword("Lifelink"))
        {
            GainLife(assignment.Source.ControllerPlayerID, damage);
        }
    }

    private bool CanAssignCombatDamageInPass(RuntimeCard card, bool firstStrikePass)
    {
        if (card == null || card.IsDead || card.CurrentAttack <= 0) return false;

        if (!firstCombatDamageStepSnapshotActive) return true;

        return firstStrikePass
            ? firstCombatDamageStepSpecialStrikers.Contains(card)
            : !firstCombatDamageStepSpecialStrikers.Contains(card) || HasDoubleStrike(card);
    }

    private bool CombatHasFirstStrikeStep()
    {
        if (CombatSideHasFirstStrikeStep(player1Battlefield)) return true;
        if (CombatSideHasFirstStrikeStep(player2Battlefield)) return true;
        return false;
    }

    private void CaptureCombatDamageStepSnapshot()
    {
        ClearCombatDamageStepSnapshot();
        CaptureCombatDamageStepSnapshot(player1Battlefield);
        CaptureCombatDamageStepSnapshot(player2Battlefield);
        firstCombatDamageStepSnapshotActive = firstCombatDamageStepSpecialStrikers.Count > 0;
    }

    private void CaptureCombatDamageStepSnapshot(List<RuntimeCard> battlefield)
    {
        for (int i = 0; i < battlefield.Count; i++)
        {
            RuntimeCard card = battlefield[i];
            if (card == null || card.IsDead) continue;
            if (!card.IsAttacking && !card.IsBlocking) continue;
            if (HasFirstStrike(card) || HasDoubleStrike(card))
            {
                firstCombatDamageStepSpecialStrikers.Add(card);
            }
        }
    }

    private void ClearCombatDamageStepSnapshot()
    {
        firstCombatDamageStepSnapshotActive = false;
        firstCombatDamageStepSpecialStrikers.Clear();
    }

    private static bool CombatSideHasFirstStrikeStep(List<RuntimeCard> battlefield)
    {
        for (int i = 0; i < battlefield.Count; i++)
        {
            RuntimeCard card = battlefield[i];
            if (card == null || card.IsDead) continue;
            if (!card.IsAttacking && !card.IsBlocking) continue;
            if (HasFirstStrike(card) || HasDoubleStrike(card)) return true;
        }

        return false;
    }

    private static bool HasFirstStrike(RuntimeCard card)
    {
        return card != null && (card.HasKeyword("FirstStrike") || card.HasKeyword("First Strike"));
    }

    private static bool HasDoubleStrike(RuntimeCard card)
    {
        return card != null && (card.HasKeyword("DoubleStrike") || card.HasKeyword("Double Strike"));
    }

    private static int GetMinimumDamageBeforeTrample(RuntimeCard source, RuntimeCard target)
    {
        if (source != null && source.HasKeyword("Deathtouch")) return 1;
        return Mathf.Max(0, target.CurrentDefence - target.DamageTaken);
    }

    private bool PerformStateBasedActions()
    {
        // Script effects finish before an SBA checkpoint. Re-entering Lua here
        // (for example an anthem filter during a buff hook) violates its runtime guard.
        if (CardScriptRuntimeManager.Instance?.Runtime is LuaCSharpCardScriptRuntime lua && lua.IsExecutingHook)
        {
            stateBasedActionRecheckRequested = true;
            return false;
        }
        if (processingStateBasedActions)
        {
            stateBasedActionRecheckRequested = true;
            return false;
        }
        if (IsGameOver() || activeChoice != null) return false;

        bool performedAny = false;
        bool performedThisPass;
        int passCount = 0;
        processingStateBasedActions = true;
        try
        {
            do
            {
                stateBasedActionRecheckRequested = false;
                performedThisPass = false;
                RecalculateBattlefieldContinuousEffects();
                if (EvaluatePlayerLossState())
                {
                    return true;
                }
                performedThisPass |= CorrectBattleProtectors();
                performedThisPass |= StateBasedActionProcessor.ProcessBattlefieldStateBasedActions(
                    player1Battlefield,
                    player2Battlefield,
                    player1Graveyard,
                    player2Graveyard,
                    HasPendingBattleDefeatAbility,
                    HandleStateBasedZoneChange,
                    cards => MoveCardsToZone(
                        cards,
                        GameZone.Graveyard,
                        false,
                        true));
                performedAny |= performedThisPass;
                if (activeChoice != null) return true;

                performedThisPass |= CleanupOrphanedPreparedCopies();
                performedThisPass |= ProcessSagaStateBasedActions();
                if (activeChoice != null) return true;

                performedThisPass |= RemoveCeasedTokens(player1Hand);
                performedThisPass |= RemoveCeasedTokens(player2Hand);
                performedThisPass |= RemoveCeasedTokens(player1Graveyard);
                performedThisPass |= RemoveCeasedTokens(player2Graveyard);
                performedThisPass |= RemoveCeasedTokens(player1Exile);
                performedThisPass |= RemoveCeasedTokens(player2Exile);
                performedThisPass |= RemoveCeasedTokens(player1Library);
                performedThisPass |= RemoveCeasedTokens(player2Library);
                performedThisPass |= RemoveCeasedTokens(player1Command);
                performedThisPass |= RemoveCeasedTokens(player2Command);
                performedAny |= performedThisPass;
                if (TryApplyWorldRule())
                {
                    performedAny = true;
                    performedThisPass = true;
                }
                if (activeChoice != null) return true;

                if (!performedThisPass && TryBeginLegendRuleChoice())
                {
                    return true;
                }
                performedThisPass |= stateBasedActionRecheckRequested;
            }
            while (performedThisPass && ++passCount < 64);

            if (passCount >= 64)
            {
                Debug.LogError(
                    "State-based actions exceeded 64 checks without reaching a stable state.");
            }
        }
        finally
        {
            processingStateBasedActions = false;
        }

        ClearDeathtouchDamageStateOnBattlefield();
        return performedAny;
    }

    private void RecalculateBattlefieldContinuousEffects()
    {
        int maximumPasses = Mathf.Max(
            2,
            GetTrackedRuntimeCardCount()
                + eventPipeline.ContinuousEffects.Count
                + 1);
        for (int pass = 0; pass < maximumPasses; pass++)
        {
            bool changed = SynchronizeScriptContinuousEffects();
            RecalculateContinuousEffectsInAllZones();
            changed |= RebalanceBattlefieldControllers();
            if (!changed) break;
        }
    }

    private bool SynchronizeScriptContinuousEffects()
    {
        bool changed = false;
        IReadOnlyList<ScriptContinuousRegistration> registrations =
            eventPipeline.ContinuousEffects;
        for (int registrationIndex = 0;
             registrationIndex < registrations.Count;
             registrationIndex++)
        {
            ScriptContinuousRegistration registration = registrations[registrationIndex];
            if (registration == null) continue;

            var staleTargets = new List<RuntimeCard>();
            System.Func<RuntimeCard, bool> sourceActive =
                registration.CharacteristicDefining
                    ? IsRuntimeEffectSourceInTrackedZone
                    : IsRuntimeEffectSourceActive;
            foreach (KeyValuePair<RuntimeCard, long> applied in registration.AppliedEffectIds)
            {
                if (applied.Key == null
                    || !IsContinuousTargetZoneEligible(registration, applied.Key)
                    || !registration.IsActive(sourceActive)
                    || !IsScriptContinuousTarget(registration, applied.Key))
                {
                    changed |= applied.Key?.RemoveContinuousEffect(applied.Value) ?? false;
                    staleTargets.Add(applied.Key);
                }
            }
            for (int i = 0; i < staleTargets.Count; i++)
            {
                registration.AppliedEffectIds.Remove(staleTargets[i]);
            }

            if (!registration.IsActive(sourceActive)) continue;
            changed |= ApplyScriptContinuousEffectToZone(
                registration,
                player1Battlefield);
            changed |= ApplyScriptContinuousEffectToZone(
                registration,
                player2Battlefield);
            if (registration.CharacteristicDefining)
            {
                changed |= ApplyCharacteristicDefiningEffectToHiddenAndPublicZones(
                    registration);
            }
        }
        return changed;
    }

    private bool ApplyCharacteristicDefiningEffectToHiddenAndPublicZones(
        ScriptContinuousRegistration registration)
    {
        bool changed = false;
        changed |= ApplyScriptContinuousEffectToZone(registration, player1Library);
        changed |= ApplyScriptContinuousEffectToZone(registration, player2Library);
        changed |= ApplyScriptContinuousEffectToZone(registration, player1Hand);
        changed |= ApplyScriptContinuousEffectToZone(registration, player2Hand);
        changed |= ApplyScriptContinuousEffectToZone(registration, player1Graveyard);
        changed |= ApplyScriptContinuousEffectToZone(registration, player2Graveyard);
        changed |= ApplyScriptContinuousEffectToZone(registration, player1Exile);
        changed |= ApplyScriptContinuousEffectToZone(registration, player2Exile);
        changed |= ApplyScriptContinuousEffectToZone(registration, player1Command);
        changed |= ApplyScriptContinuousEffectToZone(registration, player2Command);
        changed |= ApplyScriptContinuousEffectToZone(registration, GetStackCards());
        return changed;
    }

    private bool IsContinuousTargetZoneEligible(
        ScriptContinuousRegistration registration,
        RuntimeCard card)
    {
        if (registration == null) return false;
        return registration.CharacteristicDefining
            ? IsRuntimeEffectSourceInTrackedZone(card)
            : IsOnBattlefield(card);
    }

    private bool IsRuntimeEffectSourceInTrackedZone(RuntimeCard card)
    {
        if (card == null) return false;
        return ContainsReference(player1Library, card)
            || ContainsReference(player2Library, card)
            || ContainsReference(player1Hand, card)
            || ContainsReference(player2Hand, card)
            || ContainsReference(player1Battlefield, card)
            || ContainsReference(player2Battlefield, card)
            || ContainsReference(player1Graveyard, card)
            || ContainsReference(player2Graveyard, card)
            || ContainsReference(player1Exile, card)
            || ContainsReference(player2Exile, card)
            || ContainsReference(player1Command, card)
            || ContainsReference(player2Command, card)
            || ContainsReference(GetStackCards(), card);
    }

    private static bool ContainsReference(
        IReadOnlyList<RuntimeCard> cards,
        RuntimeCard target)
    {
        if (cards == null || target == null) return false;
        for (int i = 0; i < cards.Count; i++)
        {
            if (ReferenceEquals(cards[i], target)) return true;
        }
        return false;
    }

    private int GetTrackedRuntimeCardCount()
    {
        return player1Library.Count + player2Library.Count
            + player1Hand.Count + player2Hand.Count
            + player1Battlefield.Count + player2Battlefield.Count
            + player1Graveyard.Count + player2Graveyard.Count
            + player1Exile.Count + player2Exile.Count
            + player1Command.Count + player2Command.Count
            + stack.Count;
    }

    private void RecalculateContinuousEffectsInAllZones()
    {
        RecalculateContinuousEffects(player1Library);
        RecalculateContinuousEffects(player2Library);
        RecalculateContinuousEffects(player1Hand);
        RecalculateContinuousEffects(player2Hand);
        RecalculateContinuousEffects(player1Battlefield);
        RecalculateContinuousEffects(player2Battlefield);
        RecalculateContinuousEffects(player1Graveyard);
        RecalculateContinuousEffects(player2Graveyard);
        RecalculateContinuousEffects(player1Exile);
        RecalculateContinuousEffects(player2Exile);
        RecalculateContinuousEffects(player1Command);
        RecalculateContinuousEffects(player2Command);
        RecalculateContinuousEffects(GetStackCards());
    }

    private static void RecalculateContinuousEffects(
        IReadOnlyList<RuntimeCard> cards)
    {
        if (cards == null) return;
        for (int i = 0; i < cards.Count; i++)
        {
            cards[i]?.RecalculateContinuousEffects();
        }
    }

    private bool ApplyScriptContinuousEffectToZone(
        ScriptContinuousRegistration registration,
        IReadOnlyList<RuntimeCard> cards)
    {
        bool changed = false;
        for (int i = 0; i < cards.Count; i++)
        {
            RuntimeCard target = cards[i];
            if (target == null
                || registration.AppliedEffectIds.ContainsKey(target)
                || !IsScriptContinuousTarget(registration, target))
            {
                continue;
            }

            long effectId = target.AddContinuousEffect(
                CreateContinuousEffectRuntime(registration));
            if (effectId > 0)
            {
                registration.AppliedEffectIds[target] = effectId;
                changed = true;
            }
        }
        return changed;
    }

    private bool RebalanceBattlefieldControllers()
    {
        var toPlayer1 = new List<RuntimeCard>();
        var toPlayer2 = new List<RuntimeCard>();
        for (int i = 0; i < player1Battlefield.Count; i++)
        {
            RuntimeCard card = player1Battlefield[i];
            if (card != null && card.ControllerPlayerID == 2) toPlayer2.Add(card);
        }
        for (int i = 0; i < player2Battlefield.Count; i++)
        {
            RuntimeCard card = player2Battlefield[i];
            if (card != null && card.ControllerPlayerID == 1) toPlayer1.Add(card);
        }
        for (int i = 0; i < toPlayer1.Count; i++)
        {
            player2Battlefield.Remove(toPlayer1[i]);
            player1Battlefield.Add(toPlayer1[i]);
        }
        for (int i = 0; i < toPlayer2.Count; i++)
        {
            player1Battlefield.Remove(toPlayer2[i]);
            player2Battlefield.Add(toPlayer2[i]);
        }
        return toPlayer1.Count > 0 || toPlayer2.Count > 0;
    }

    private bool IsScriptContinuousTarget(
        ScriptContinuousRegistration registration,
        RuntimeCard target)
    {
        if (registration?.Source == null || target == null) return false;
        if (string.IsNullOrEmpty(registration.FilterHookName))
        {
            return ReferenceEquals(registration.Source, target);
        }

        RuntimeCard source = registration.Source;
        GameEventContext previousContext = source.ResolvingGameEvent;
        source.SetResolvingGameEvent(new GameEventContext
        {
            source = source,
            card = target,
            playerId = target.ControllerPlayerID,
            controllerId = registration.ControllerId,
            fromZone = target.CurrentZone,
            toZone = target.CurrentZone
        });
        bool matches = CardEffectExecutor.EvaluateCardScriptPredicate(
            this,
            source,
            registration.FilterHookName);
        source.SetResolvingGameEvent(previousContext);
        return matches;
    }

    private static ContinuousEffectRuntime CreateContinuousEffectRuntime(
        ScriptContinuousRegistration registration)
    {
        ContinuousEffectLayer layer = ContinuousEffectLayer.PowerToughness;
        PowerToughnessSublayer sublayer = PowerToughnessSublayer.Modify;
        int power = registration.ValueA;
        int toughness = registration.ValueB;
        switch (registration.Operation)
        {
            case ContinuousEffectOperation.SetController:
                layer = ContinuousEffectLayer.Control;
                break;
            case ContinuousEffectOperation.ReplaceRulesText:
            case ContinuousEffectOperation.SetName:
                layer = ContinuousEffectLayer.Text;
                break;
            case ContinuousEffectOperation.CopyCharacteristics:
            case ContinuousEffectOperation.SetFaceDownCharacteristics:
                layer = ContinuousEffectLayer.Copy;
                break;
            case ContinuousEffectOperation.SetCardTypes:
            case ContinuousEffectOperation.AddCardType:
            case ContinuousEffectOperation.RemoveCardType:
            case ContinuousEffectOperation.SetSupertypes:
            case ContinuousEffectOperation.AddSupertype:
            case ContinuousEffectOperation.RemoveSupertype:
            case ContinuousEffectOperation.SetSubtypes:
                layer = ContinuousEffectLayer.Type;
                break;
            case ContinuousEffectOperation.SetColors:
                layer = ContinuousEffectLayer.Color;
                break;
            case ContinuousEffectOperation.AddKeyword:
            case ContinuousEffectOperation.RemoveKeyword:
            case ContinuousEffectOperation.RemoveAllKeywords:
                layer = ContinuousEffectLayer.Ability;
                break;
            case ContinuousEffectOperation.SetPowerToughness:
                sublayer = PowerToughnessSublayer.Set;
                break;
            case ContinuousEffectOperation.ModifyPowerToughness:
                sublayer = PowerToughnessSublayer.Modify;
                break;
            case ContinuousEffectOperation.SwitchPowerToughness:
                sublayer = PowerToughnessSublayer.Switch;
                break;
        }
        if (layer == ContinuousEffectLayer.PowerToughness
            && registration.CharacteristicDefining)
        {
            sublayer = PowerToughnessSublayer.CharacteristicDefining;
        }
        if (registration.Operation == ContinuousEffectOperation.SetFaceDownCharacteristics
            && power == 0
            && toughness == 0)
        {
            power = 2;
            toughness = 2;
        }

        return new ContinuousEffectRuntime
        {
            layer = layer,
            copyEffectSublayer =
                registration.Operation == ContinuousEffectOperation.SetFaceDownCharacteristics
                    ? CopyEffectSublayer.FaceDown
                    : CopyEffectSublayer.Copy,
            powerToughnessSublayer = sublayer,
            duration = ContinuousEffectDuration.Permanent,
            operation = registration.Operation,
            source = registration.Source,
            sourceZoneChangeVersion = registration.Source?.ZoneChangeVersion ?? -1,
            sourceKey = $"LuaEffect:{registration.Id}",
            linkId = registration.LinkId,
            dependencyKeys = registration.DependencyKey,
            characteristicDefining = registration.CharacteristicDefining,
            reads = registration.Reads,
            writes = registration.Writes,
            timestamp = registration.Timestamp,
            copySource = registration.CopySource,
            copySourceZoneChangeVersion =
                registration.CopySource?.ZoneChangeVersion ?? -1,
            cardTypes = registration.CardTypes,
            supertypes = registration.Supertypes,
            colors = registration.Colors,
            keyword = registration.TextValue,
            rulesText = registration.TextValue,
            subtypes = registration.TextValue,
            cardName = registration.TextValue,
            controllerPlayerId = registration.ValueA,
            power = power,
            toughness = toughness
        };
    }

    private void RemoveScriptContinuousEffectsByDuration(
        RuntimeEffectDuration duration)
    {
        var ids = new List<long>();
        IReadOnlyList<ScriptContinuousRegistration> registrations =
            eventPipeline.ContinuousEffects;
        for (int i = 0; i < registrations.Count; i++)
        {
            if (registrations[i] != null && registrations[i].Duration == duration)
            {
                ids.Add(registrations[i].Id);
            }
        }
        for (int i = 0; i < ids.Count; i++) RemoveScriptEffect(ids[i]);
    }

    private bool TryApplyWorldRule()
    {
        var worldPermanents = new List<RuntimeCard>();
        CollectWorldPermanents(player1Battlefield, worldPermanents);
        CollectWorldPermanents(player2Battlefield, worldPermanents);
        if (worldPermanents.Count <= 1) return false;

        long newestSequence = long.MinValue;
        for (int i = 0; i < worldPermanents.Count; i++)
        {
            newestSequence = System.Math.Max(newestSequence, worldPermanents[i].BattlefieldEntrySequence);
        }

        RuntimeCard cardToKeep = null;
        int newestCount = 0;
        for (int i = 0; i < worldPermanents.Count; i++)
        {
            RuntimeCard card = worldPermanents[i];
            if (card.BattlefieldEntrySequence != newestSequence) continue;
            newestCount++;
            cardToKeep = card;
        }
        if (newestCount != 1) cardToKeep = null;

        var cardsToMove = new List<RuntimeCard>();
        for (int i = 0; i < worldPermanents.Count; i++)
        {
            RuntimeCard card = worldPermanents[i];
            if (!ReferenceEquals(card, cardToKeep)) cardsToMove.Add(card);
        }

        bool accepted = MoveCardsToZone(
            cardsToMove,
            GameZone.Graveyard,
            false,
            true,
            moved =>
            {
                if (!moved) return;
                for (int i = 0; i < cardsToMove.Count; i++)
                {
                    RuntimeCard card = cardsToMove[i];
                    if (card != null && card.CurrentZone != GameZone.Battlefield)
                    {
                        Debug.Log(
                            $"{card.GetCardName()} is put into its owner's graveyard by the world rule.");
                    }
                }
            });
        return accepted;
    }

    private static void CollectWorldPermanents(List<RuntimeCard> battlefield, List<RuntimeCard> destination)
    {
        for (int i = 0; i < battlefield.Count; i++)
        {
            RuntimeCard card = battlefield[i];
            if (card != null
                && !card.IsDead
                && card.Data != null
                && card.HasSupertype(CardSupertype.World))
            {
                destination.Add(card);
            }
        }
    }

    private bool TryBeginLegendRuleChoice()
    {
        if (activeChoice != null) return false;

        int activePlayer = currentPlayer == 2 ? 2 : 1;
        int nonActivePlayer = activePlayer == 1 ? 2 : 1;
        return TryBeginLegendRuleChoiceForPlayer(activePlayer)
            || TryBeginLegendRuleChoiceForPlayer(nonActivePlayer);
    }

    private bool TryBeginLegendRuleChoiceForPlayer(int playerID)
    {
        List<RuntimeCard> battlefield = playerID == 1 ? player1Battlefield : player2Battlefield;
        Dictionary<string, List<RuntimeCard>> legendaryByName =
            new Dictionary<string, List<RuntimeCard>>(System.StringComparer.Ordinal);

        for (int i = 0; i < battlefield.Count; i++)
        {
            RuntimeCard card = battlefield[i];
            if (card == null
                || card.IsDead
                || !card.HasSupertype(CardSupertype.Legendary)
                || string.IsNullOrWhiteSpace(card.GetCardName()))
            {
                continue;
            }

            string cardName = card.GetCardName();
            if (!legendaryByName.TryGetValue(cardName, out List<RuntimeCard> copies))
            {
                copies = new List<RuntimeCard>();
                legendaryByName.Add(cardName, copies);
            }

            copies.Add(card);
        }

        foreach (KeyValuePair<string, List<RuntimeCard>> pair in legendaryByName)
        {
            List<RuntimeCard> copies = pair.Value;
            if (copies.Count <= 1) continue;

            pendingLegendRuleCards.Clear();
            pendingLegendRuleCards.AddRange(copies);
            GameChoiceRequest request = BeginChoice(
                GameChoiceKind.LegendRule,
                playerID,
                $"Choose one legendary permanent named {pair.Key} to keep.",
                1,
                1,
                false);

            for (int i = 0; i < copies.Count; i++)
            {
                RuntimeCard card = copies[i];
                int battlefieldIndex = battlefield.IndexOf(card);
                request.options.Add(new GameChoiceOption(
                    GetBattlefieldChoiceOptionId(playerID, battlefieldIndex),
                    GameChoiceZone.Battlefield,
                    playerID,
                    battlefieldIndex,
                    true,
                    card.GetCardName()));
            }

            priorityState.Close();

            if (playerID != 1 && !IsOnlineMatch())
            {
                ResolveLegendRuleChoice(copies[0]);
            }
            else
            {
                Debug.Log($"Player {playerID} must choose one {pair.Key} to keep.");
            }

            return true;
        }

        return false;
    }

    private void ResolveLegendRuleChoice(RuntimeCard keptCard)
    {
        List<RuntimeCard> cardsToMove = new List<RuntimeCard>();
        for (int i = 0; i < pendingLegendRuleCards.Count; i++)
        {
            RuntimeCard card = pendingLegendRuleCards[i];
            if (card != null && !ReferenceEquals(card, keptCard))
            {
                cardsToMove.Add(card);
            }
        }

        activeChoice = null;
        pendingLegendRuleCards.Clear();

        MoveCardsToZone(
            cardsToMove,
            GameZone.Graveyard,
            false,
            true,
            moved =>
            {
                if (!moved) return;
                for (int i = 0; i < cardsToMove.Count; i++)
                {
                    RuntimeCard card = cardsToMove[i];
                    if (card != null && card.CurrentZone != GameZone.Battlefield)
                    {
                        Debug.Log(
                            $"{card.GetCardName()} is put into Player {card.OwnerPlayerID}'s graveyard by the legend rule.");
                    }
                }
            });
    }

    private bool CorrectBattleProtectors()
    {
        bool changed = false;
        changed |= CorrectBattleProtectors(player1Battlefield);
        changed |= CorrectBattleProtectors(player2Battlefield);
        return changed;
    }

    private static bool CorrectBattleProtectors(IReadOnlyList<RuntimeCard> battlefield)
    {
        if (battlefield == null) return false;

        bool changed = false;
        for (int i = 0; i < battlefield.Count; i++)
        {
            RuntimeCard battle = battlefield[i];
            if (battle == null || !battle.HasType(CardType.Battle)) continue;

            int requiredProtector = battle.ControllerPlayerID == 1 ? 2 : 1;
            if (battle.BattleProtectorPlayerID != requiredProtector)
            {
                changed |= battle.SetBattleProtector(requiredProtector);
            }
        }
        return changed;
    }

    private bool EvaluatePlayerLossState()
    {
        bool player1Lost = player1Life <= 0
            || player1AttemptedDrawFromEmptyLibrary
            || player1PoisonCounters >= 10;
        bool player2Lost = player2Life <= 0
            || player2AttemptedDrawFromEmptyLibrary
            || player2PoisonCounters >= 10;
        if (!player1Lost && !player2Lost) return false;

        if (player1Lost && player2Lost)
        {
            EndMatch(0, MatchEndReason.SimultaneousLoss, "Both players lost the game simultaneously.");
            return true;
        }

        int losingPlayer = player1Lost ? 1 : 2;
        bool attemptedEmptyDraw = losingPlayer == 1
            ? player1AttemptedDrawFromEmptyLibrary
            : player2AttemptedDrawFromEmptyLibrary;
        bool hasLethalPoison = GetPoisonCounters(losingPlayer) >= 10;
        MatchEndReason reason = attemptedEmptyDraw
            ? MatchEndReason.EmptyLibrary
            : hasLethalPoison
                ? MatchEndReason.PoisonCounters
                : MatchEndReason.LifeTotal;
        int winner = losingPlayer == 1 ? 2 : 1;
        string detail;
        if (reason == MatchEndReason.EmptyLibrary)
        {
            detail = $"Player {losingPlayer} attempted to draw from an empty library.";
        }
        else if (reason == MatchEndReason.PoisonCounters)
        {
            detail = $"Player {losingPlayer} has {GetPoisonCounters(losingPlayer)} poison counters.";
        }
        else
        {
            detail = $"Player {losingPlayer} has {GetLife(losingPlayer)} life.";
        }
        EndMatch(winner, reason, $"{detail} Player {winner} wins.");
        return true;
    }

    private void EndMatch(int winnerPlayerId, MatchEndReason reason, string message)
    {
        if (matchOutcome.isFinished) return;

        matchOutcome = new MatchOutcome
        {
            isFinished = true,
            winnerPlayerId = winnerPlayerId,
            reason = reason,
            message = message ?? string.Empty
        };
        priorityState.Close();
        combatAnimationInProgress = false;
        activeChoice = null;
        pendingTriggeredAbilities.Clear();
        pendingTriggerOrderItems.Clear();
        deferredTriggerOrderItems.Clear();
        pendingLegendRuleCards.Clear();
        Debug.Log(matchOutcome.message);
        GameEvents.TriggerMatchEnded(matchOutcome);
    }

    public bool Concede(int playerID)
    {
        if (IsGameOver() || (playerID != 1 && playerID != 2)) return false;
        int winner = playerID == 1 ? 2 : 1;
        EndMatch(winner, MatchEndReason.Concession, $"Player {playerID} conceded. Player {winner} wins.");
        return true;
    }

    private bool RunPriorityCheckpoint()
    {
        bool performedAny = false;
        bool performedThisPass;
        int guard = 0;
        do
        {
            performedThisPass = PerformStateBasedActions();
            performedAny |= performedThisPass;
        }
        while (performedThisPass && guard++ < 16);

        if (!IsGameOver() && !HasActiveChoice())
        {
            performedAny |= PutPendingTriggeredAbilitiesOnStack();
        }

        return performedAny;
    }

    private void ClearDeathtouchDamageStateOnBattlefield()
    {
        for (int i = 0; i < player1Battlefield.Count; i++)
        {
            player1Battlefield[i].ClearDeathtouchDamageState();
        }

        for (int i = 0; i < player2Battlefield.Count; i++)
        {
            player2Battlefield[i].ClearDeathtouchDamageState();
        }
    }

    public void DealDamageToPlayer(int player, int damage)
    {
        if (damage <= 0) return;

        var gameEvent = new GameEventContext
        {
            type = GameEventType.Damage,
            playerId = player,
            amount = damage,
            damageKind = DamageEventKind.Noncombat
        };
        PrepareGameEventWithChoice(gameEvent, prepared =>
        {
            int resolvedDamage = prepared.amount;
            if (prepared.prevented || resolvedDamage <= 0) return;
            RuntimeCard resolvedTarget = prepared.card;
            if (resolvedTarget != null)
            {
                if (!IsOnBattlefield(resolvedTarget)) return;
                ApplyDamageToPermanent(null, resolvedTarget, resolvedDamage);
            }
            else
            {
                if (prepared.playerId != 1 && prepared.playerId != 2) return;
                ApplyDamageToPlayerLife(prepared.playerId, resolvedDamage);
            }
            CompleteGameEvent(prepared);
            PerformStateBasedActions();
        });
    }

    public bool RedirectCurrentDamageToCard(RuntimeCard contextCard, RuntimeCard target)
    {
        GameEventContext gameEvent = contextCard?.ResolvingGameEvent;
        if (gameEvent == null
            || gameEvent.type != GameEventType.Damage
            || target == null
            || !IsOnBattlefield(target))
        {
            return false;
        }

        gameEvent.card = target;
        gameEvent.cardObjectId = target.ObjectId;
        gameEvent.cardZoneChangeVersion = target.ZoneChangeVersion;
        gameEvent.playerId = target.ControllerPlayerID;
        gameEvent.protectionPreventionApplied = false;
        gameEvent.targetPreventionApplied = false;
        gameEvent.replaced = true;
        return true;
    }

    public bool RedirectCurrentDamageToPlayer(RuntimeCard contextCard, int playerID)
    {
        GameEventContext gameEvent = contextCard?.ResolvingGameEvent;
        if (gameEvent == null
            || gameEvent.type != GameEventType.Damage
            || (playerID != 1 && playerID != 2))
        {
            return false;
        }

        gameEvent.card = null;
        gameEvent.cardObjectId = 0;
        gameEvent.cardZoneChangeVersion = -1;
        gameEvent.playerId = playerID;
        gameEvent.protectionPreventionApplied = false;
        gameEvent.targetPreventionApplied = false;
        gameEvent.replaced = true;
        return true;
    }

    private void ApplyDamageToPlayerLife(int player, int damage)
    {
        if (damage <= 0) return;

        if (player == 1)
        {
            player1Life -= damage;
            Debug.Log($"Player 1 takes {damage} damage! Remaining life: {player1Life}");
            GameEvents.TriggerPlayerDamaged(1, damage, player1Life);
        }
        else if (player == 2)
        {
            player2Life -= damage;
            Debug.Log($"Player 2 takes {damage} damage! Remaining life: {player2Life}");
            GameEvents.TriggerPlayerDamaged(2, damage, player2Life);
        }
    }

    public bool DealDamageToSpellTarget(RuntimeCard source, int damage)
    {
        if (source == null || damage <= 0) return false;

        RuntimeCard targetCard = GetTargetCard(source);
        if (targetCard != null)
        {
            return DealDamageToCard(source, targetCard, damage);
        }

        if (source.TargetPlayerID > 0)
        {
            return DealDamageToPlayer(source, source.TargetPlayerID, damage);
        }

        return false;
    }

    public bool DealDamageToCard(RuntimeCard source, RuntimeCard target, int damage)
    {
        if (source == null || target == null || damage <= 0) return false;
        if (!IsOnBattlefield(target)) return false;

        var gameEvent = new GameEventContext
        {
            type = GameEventType.Damage,
            source = source,
            card = target,
            playerId = target.ControllerPlayerID,
            controllerId = source.ControllerPlayerID,
            amount = damage,
            damageKind = DamageEventKind.Noncombat
        };
        PrepareGameEventWithChoice(gameEvent, prepared =>
        {
            int resolvedDamage = prepared.amount;
            if (prepared.prevented || resolvedDamage <= 0) return;
            RuntimeCard resolvedTarget = prepared.card;
            if (resolvedTarget != null)
            {
                if (!IsOnBattlefield(resolvedTarget)) return;
                Debug.Log($"{source.Data.cardName} deals {resolvedDamage} damage to {resolvedTarget.Data.cardName}.");
                if (source.HasKeyword("Deathtouch") && resolvedTarget.HasType(CardType.Creature))
                {
                    resolvedTarget.MarkDeathtouchDamage();
                }
                ApplyDamageToPermanent(source, resolvedTarget, resolvedDamage);
            }
            else
            {
                if (prepared.playerId != 1 && prepared.playerId != 2) return;
                ApplyDamageToPlayerLife(prepared.playerId, resolvedDamage);
            }
            CompleteGameEvent(prepared);
            if (source.HasKeyword("Lifelink"))
            {
                GainLife(source.ControllerPlayerID, resolvedDamage);
            }
            PerformStateBasedActions();
        });
        return true;
    }

    private void ApplyDamageToPermanent(
        RuntimeCard source,
        RuntimeCard target,
        int damage)
    {
        if (target == null || damage <= 0) return;
        if (target.HasType(CardType.Planeswalker))
        {
            target.AddLoyaltyCounters(-damage);
            return;
        }

        if (!target.HasType(CardType.Battle))
        {
            target.TakeDamage(damage);
            return;
        }

        ChangeBattleDefenseCounters(target, -damage);
    }

    private void ChangeBattleDefenseCounters(RuntimeCard battle, int amount)
    {
        if (battle == null || !battle.HasType(CardType.Battle) || amount == 0) return;

        int previousDefense = battle.CurrentBattleDefense;
        battle.AddBattleDefenseCounters(amount);
        if (previousDefense <= 0
            || battle.CurrentBattleDefense != 0
            || HasPendingBattleDefeatAbility(battle))
        {
            return;
        }

        pendingTriggeredAbilities.Add(
            GameStackItem.FeatureAbility(
                battle,
                battle.PrimaryFeature,
                BattleDefeatedHook));
    }

    private void ResolveBattleDefeatAbility(RuntimeCard battle, GameStackItem item)
    {
        if (battle == null
            || item == null
            || !IsOnBattlefield(battle)
            || battle.ObjectId != item.SourceObjectId)
        {
            CompleteAbilityResolution(battle, item);
            return;
        }

        bool moveAccepted = MoveCardToZone(
            battle,
            GameZone.Exile,
            false,
            false,
            moved =>
            {
                if (moved && battle.CurrentZone == GameZone.Exile)
                {
                    BeginDefeatedBattleCastChoice(battle, item.ControllerPlayerId);
                }
            });

        if (moveAccepted
            && SuspendStackResolutionForActiveChoice(
                () => CompleteAbilityResolution(battle, item)))
        {
            return;
        }

        CompleteAbilityResolution(battle, item);
    }

    private bool BeginDefeatedBattleCastChoice(RuntimeCard battle, int playerId)
    {
        CardFeatureRuntime backFace = FindBattleBackFace(battle);
        if (battle == null
            || backFace == null
            || battle.CurrentZone != GameZone.Exile
            || (playerId != 1 && playerId != 2))
        {
            return false;
        }

        var options = new List<GameChoiceOption>
        {
            new GameChoiceOption(
                CastDefeatedBattleOptionId,
                GameChoiceZone.Exile,
                battle.OwnerPlayerID,
                GetExileList(battle.OwnerPlayerID)?.IndexOf(battle) ?? -1,
                true,
                $"Cast {backFace.CardName} transformed without paying its mana cost.",
                0,
                1,
                battle.ObjectId,
                battle.ZoneChangeVersion)
        };

        return BeginOptionChoice(
            GameChoiceKind.SelectOptions,
            playerId,
            $"You may cast {backFace.CardName} transformed without paying its mana cost.",
            0,
            1,
            false,
            options,
            result =>
            {
                if (result == null
                    || !result.selectedOptionIds.Contains(CastDefeatedBattleOptionId)
                    || battle.CurrentZone != GameZone.Exile)
                {
                    return;
                }

                List<RuntimeCard> exile = GetExileList(battle.OwnerPlayerID);
                int exileIndex = exile?.IndexOf(battle) ?? -1;
                int previousFace = battle.CurrentFaceIndex;
                if (exileIndex < 0
                    || !battle.SetCurrentFace(backFace.FaceIndex)
                    || !BeginPendingCast(
                        playerId,
                        GameChoiceZone.Exile,
                        exileIndex,
                        backFace,
                        true,
                        true,
                        battle.OwnerPlayerID))
                {
                    battle.SetCurrentFace(previousFace);
                }
            }) != null;
    }

    private static CardFeatureRuntime FindBattleBackFace(RuntimeCard battle)
    {
        if (battle == null) return null;
        for (int i = 0; i < battle.Features.Count; i++)
        {
            CardFeatureRuntime feature = battle.Features[i];
            if (feature != null
                && feature.IsPrimary
                && feature.FaceIndex != battle.CurrentFaceIndex)
            {
                return feature;
            }
        }

        return null;
    }

    public bool DealDamageToPlayer(RuntimeCard source, int player, int damage)
    {
        if (source == null || damage <= 0) return false;

        var gameEvent = new GameEventContext
        {
            type = GameEventType.Damage,
            source = source,
            playerId = player,
            controllerId = source.ControllerPlayerID,
            amount = damage,
            damageKind = DamageEventKind.Noncombat
        };
        PrepareGameEventWithChoice(gameEvent, prepared =>
        {
            int resolvedDamage = prepared.amount;
            if (prepared.prevented || resolvedDamage <= 0) return;
            RuntimeCard resolvedTarget = prepared.card;
            if (resolvedTarget != null)
            {
                if (!IsOnBattlefield(resolvedTarget)) return;
                if (source.HasKeyword("Deathtouch") && resolvedTarget.HasType(CardType.Creature))
                {
                    resolvedTarget.MarkDeathtouchDamage();
                }
                ApplyDamageToPermanent(source, resolvedTarget, resolvedDamage);
            }
            else
            {
                if (prepared.playerId != 1 && prepared.playerId != 2) return;
                ApplyDamageToPlayerLife(prepared.playerId, resolvedDamage);
            }
            CompleteGameEvent(prepared);
            if (source.HasKeyword("Lifelink"))
            {
                GainLife(source.ControllerPlayerID, resolvedDamage);
            }
            PerformStateBasedActions();
        });
        return true;
    }

    public bool DestroyCard(RuntimeCard target)
    {
        return DestroyCard(null, target, true);
    }

    public bool DestroyCard(RuntimeCard source, RuntimeCard target, bool allowRegeneration = true)
    {
        if (target == null || !IsOnBattlefield(target)) return false;

        bool destroyed = target.Destroy(allowRegeneration);
        if (!destroyed)
        {
            Debug.Log($"{target.Data.cardName} was not destroyed.");
            return false;
        }

        Debug.Log(source != null
            ? $"{source.Data.cardName} destroys {target.Data.cardName}."
            : $"{target.Data.cardName} is destroyed.");
        PerformStateBasedActions();
        return true;
    }

    public bool MoveCardToGraveyard(RuntimeCard card)
    {
        return MoveCardToZone(card, GameZone.Graveyard);
    }

    public bool ExileCard(RuntimeCard card)
    {
        return MoveCardToZone(card, GameZone.Exile);
    }

    public bool MoveCardToZone(
        RuntimeCard card,
        GameZone destination,
        bool toTop = false,
        bool performStateBasedActions = true)
    {
        return MoveCardToZone(
            card,
            destination,
            toTop,
            performStateBasedActions,
            null);
    }

    private bool MoveCardToZone(
        RuntimeCard card,
        GameZone destination,
        bool toTop,
        bool performStateBasedActions,
        System.Action<bool> onCompleted)
    {
        if (card == null || destination == GameZone.None || destination == GameZone.Stack) return false;

        if (destination == card.CurrentZone)
        {
            onCompleted?.Invoke(true);
            return true;
        }

        bool callbackCompleted = false;
        bool committed = false;
        bool accepted = TryPrepareZoneMoveWithChoice(
            card,
            destination,
            toTop,
            move =>
            {
                callbackCompleted = true;
                committed = move != null
                    && CommitPreparedZoneMove(move, false);
                onCompleted?.Invoke(committed);
                if (committed && performStateBasedActions)
                {
                    PerformStateBasedActions();
                }
            });
        return accepted && (!callbackCompleted || committed);
    }

    public bool MoveCardsToZone(
        IReadOnlyList<RuntimeCard> cards,
        GameZone destination,
        bool toTop = false,
        bool performStateBasedActions = true)
    {
        return MoveCardsToZone(
            cards,
            destination,
            toTop,
            performStateBasedActions,
            null);
    }

    private bool MoveCardsToZone(
        IReadOnlyList<RuntimeCard> cards,
        GameZone destination,
        bool toTop,
        bool performStateBasedActions,
        System.Action<bool> onCompleted)
    {
        if (cards == null
            || cards.Count == 0
            || !IsSupportedMoveDestination(destination))
        {
            return false;
        }

        var uniqueCards = new HashSet<RuntimeCard>();
        for (int i = 0; i < cards.Count; i++)
        {
            RuntimeCard card = cards[i];
            if (card == null
                || !uniqueCards.Add(card)
                || !IsRuntimeCardInKnownZone(card))
            {
                return false;
            }
        }

        var moves = new List<PreparedZoneMove>(cards.Count);
        bool callbackCompleted = false;
        bool committed = false;
        bool preparationAccepted = true;

        void CommitBatch()
        {
            callbackCompleted = true;

            // Replacement hooks run during preparation. Revalidate every source
            // before committing so the batch cannot partially move.
            for (int i = 0; i < moves.Count; i++)
            {
                PreparedZoneMove move = moves[i];
                if (!IsRuntimeCardInKnownZone(move.Card)
                    || move.Card.CurrentZone != move.FromZone)
                {
                    committed = false;
                    onCompleted?.Invoke(false);
                    return;
                }
            }

            bool reverseForLibraryOrder = destination == GameZone.Library && toTop;
            for (int offset = 0; offset < moves.Count; offset++)
            {
                int index = reverseForLibraryOrder
                    ? moves.Count - 1 - offset
                    : offset;
                if (!CommitPreparedZoneMoveState(moves[index]))
                {
                    throw new System.InvalidOperationException(
                        "A prepared multi-card zone move failed during commit.");
                }
            }

            // CR 704.3: the whole state-based-action batch changes zones before
            // triggered ability conditions are evaluated for any member of it.
            for (int offset = 0; offset < moves.Count; offset++)
            {
                int index = reverseForLibraryOrder
                    ? moves.Count - 1 - offset
                    : offset;
                CompletePreparedZoneMove(moves[index]);
            }

            committed = true;
            onCompleted?.Invoke(true);
            if (performStateBasedActions && moves.Count > 0)
            {
                PerformStateBasedActions();
            }
        }

        void PrepareAt(int cardIndex)
        {
            while (cardIndex < cards.Count
                && cards[cardIndex].CurrentZone == destination)
            {
                cardIndex++;
            }
            if (cardIndex >= cards.Count)
            {
                CommitBatch();
                return;
            }

            bool accepted = TryPrepareZoneMoveWithChoice(
                cards[cardIndex],
                destination,
                toTop,
                move =>
                {
                    if (move == null)
                    {
                        PrepareAt(cardIndex + 1);
                        return;
                    }
                    moves.Add(move);
                    PrepareAt(cardIndex + 1);
                });
            if (!accepted)
            {
                preparationAccepted = false;
                callbackCompleted = true;
                committed = false;
                onCompleted?.Invoke(false);
            }
        }

        PrepareAt(0);
        return preparationAccepted && (!callbackCompleted || committed);
    }

    private bool TryPrepareZoneMoveWithChoice(
        RuntimeCard card,
        GameZone destination,
        bool toTop,
        System.Action<PreparedZoneMove> onPrepared)
    {
        if (card == null
            || onPrepared == null
            || destination == GameZone.None
            || destination == GameZone.Stack
            || !IsRuntimeCardInKnownZone(card))
        {
            return false;
        }

        GameZone fromZone = card.CurrentZone;
        bool wasOnBattlefield = IsOnBattlefield(card);
        var gameEvent = new GameEventContext
        {
            type = ClassifyZoneChangeEvent(
                card,
                fromZone,
                destination,
                wasOnBattlefield),
            source = card,
            card = card,
            playerId = card.OwnerPlayerID,
            controllerId = card.ControllerPlayerID,
            fromZone = fromZone,
            toZone = destination
        };
        PrepareGameEventWithChoice(gameEvent, prepared =>
        {
            GameZone resolvedDestination = prepared.toZone;
            if (prepared.prevented
                || !IsSupportedMoveDestination(resolvedDestination)
                || !IsRuntimeCardInKnownZone(card)
                || card.CurrentZone != fromZone)
            {
                onPrepared(null);
                return;
            }

            prepared.type = ClassifyZoneChangeEvent(
                card,
                fromZone,
                resolvedDestination,
                wasOnBattlefield);
            onPrepared(new PreparedZoneMove
            {
                Card = card,
                FromZone = fromZone,
                Destination = resolvedDestination,
                WasOnBattlefield = wasOnBattlefield,
                EnteringController = prepared.controllerId == 1
                    || prepared.controllerId == 2
                        ? prepared.controllerId
                        : card.ControllerPlayerID,
                ToTop = toTop,
                Event = prepared
            });
        });
        return true;
    }

    private bool TryPrepareZoneMove(
        RuntimeCard card,
        GameZone destination,
        bool toTop,
        out PreparedZoneMove move)
    {
        move = null;
        if (card == null
            || destination == GameZone.None
            || destination == GameZone.Stack)
        {
            return false;
        }

        GameZone fromZone = card.CurrentZone;
        bool wasOnBattlefield = IsOnBattlefield(card);
        var gameEvent = new GameEventContext
        {
            type = ClassifyZoneChangeEvent(
                card,
                fromZone,
                destination,
                wasOnBattlefield),
            source = card,
            card = card,
            playerId = card.OwnerPlayerID,
            controllerId = card.ControllerPlayerID,
            fromZone = fromZone,
            toZone = destination
        };
        PrepareGameEvent(gameEvent);
        destination = gameEvent.toZone;
        if (gameEvent.prevented
            || !IsSupportedMoveDestination(destination)
            || !IsRuntimeCardInKnownZone(card))
        {
            return false;
        }
        gameEvent.type = ClassifyZoneChangeEvent(
            card,
            fromZone,
            destination,
            wasOnBattlefield);
        move = new PreparedZoneMove
        {
            Card = card,
            FromZone = fromZone,
            Destination = destination,
            WasOnBattlefield = wasOnBattlefield,
            EnteringController = card.ControllerPlayerID,
            ToTop = toTop,
            Event = gameEvent
        };
        return true;
    }

    private bool CommitPreparedZoneMove(
        PreparedZoneMove move,
        bool performStateBasedActions)
    {
        if (!CommitPreparedZoneMoveState(move)) return false;
        CompletePreparedZoneMove(move);
        if (performStateBasedActions) PerformStateBasedActions();
        return true;
    }

    private bool CommitPreparedZoneMoveState(PreparedZoneMove move)
    {
        if (move?.Card == null || !IsRuntimeCardInKnownZone(move.Card)) return false;

        RuntimeCard card = move.Card;
        GameZone fromZone = move.FromZone;
        GameZone destination = move.Destination;
        bool wasOnBattlefield = move.WasOnBattlefield;
        if (!RemoveRuntimeCardFromKnownZones(card)) return false;

        if (wasOnBattlefield)
        {
            card.RemoveFromCombat();
            RemovePreparedCopyForSource(card);
            card.ResetPermanentDesignations();
        }

        int enteringController = move.EnteringController;
        bool diesFromBattlefield = wasOnBattlefield
            && destination == GameZone.Graveyard
            && card.HasType(CardType.Creature);
        if (diesFromBattlefield && !card.IsDead)
        {
            card.Die();
        }

        card.MarkZoneChange();
        card.ReassignObjectId(nextObjectId++);
        if (destination == GameZone.Battlefield)
        {
            card.SetController(enteringController);
            card.SetCurrentZone(GameZone.Battlefield);
            List<RuntimeCard> battlefield = enteringController == 1
                ? player1Battlefield
                : player2Battlefield;
            MarkCardEnteredBattlefield(card);
            battlefield.Add(card);
            InitializeStructuredCardOnBattlefield(card, null);
        }
        else
        {
            card.SetController(card.OwnerPlayerID);
            card.SetCurrentZone(destination);
            List<RuntimeCard> destinationCards = destination switch
            {
                GameZone.Library => GetLibraryList(card.OwnerPlayerID),
                GameZone.Hand => GetHandList(card.OwnerPlayerID),
                GameZone.Graveyard => GetGraveyardList(card.OwnerPlayerID),
                GameZone.Exile => GetExileList(card.OwnerPlayerID),
                GameZone.Command => card.OwnerPlayerID == 1
                    ? player1Command
                    : player2Command,
                _ => null
            };
            if (destinationCards == null) return false;
            if (destination == GameZone.Library && move.ToTop) destinationCards.Insert(0, card);
            else destinationCards.Add(card);
        }

        return true;
    }

    private void CompletePreparedZoneMove(PreparedZoneMove move)
    {
        if (move?.Card == null || move.Event == null) return;

        RuntimeCard card = move.Card;
        GameEventContext gameEvent = move.Event;
        gameEvent.toZone = move.Destination;
        gameEvent.controllerId = card.ControllerPlayerID;
        CompleteGameEvent(gameEvent);
        Debug.Log(
            $"{card.Data?.cardName ?? "Card"} moved from {move.FromZone} to {move.Destination}.");
    }

    private static bool IsSupportedMoveDestination(GameZone destination)
    {
        return destination == GameZone.Library
            || destination == GameZone.Hand
            || destination == GameZone.Battlefield
            || destination == GameZone.Graveyard
            || destination == GameZone.Exile
            || destination == GameZone.Command;
    }

    private bool IsRuntimeCardInKnownZone(RuntimeCard card)
    {
        if (card == null) return false;
        return player1Hand.Contains(card)
            || player2Hand.Contains(card)
            || player1Battlefield.Contains(card)
            || player2Battlefield.Contains(card)
            || player1Graveyard.Contains(card)
            || player2Graveyard.Contains(card)
            || player1Exile.Contains(card)
            || player2Exile.Contains(card)
            || player1Library.Contains(card)
            || player2Library.Contains(card)
            || player1Command.Contains(card)
            || player2Command.Contains(card)
            || stack.Exists(item =>
                item != null
                && item.Kind == GameStackItemKind.Spell
                && ReferenceEquals(item.SourceCard, card));
    }

    public bool ShuffleLibrary(int playerID)
    {
        List<RuntimeCard> library = GetLibraryList(playerID);
        if (library == null) return false;
        ShuffleDeck(library);
        return true;
    }

    public int MillCards(int playerID, int count)
    {
        List<RuntimeCard> library = GetLibraryList(playerID);
        if (library == null || count <= 0) return 0;
        int moved = 0;
        while (moved < count && library.Count > 0)
        {
            RuntimeCard card = library[0];
            if (!MoveCardToGraveyard(card)) break;
            PublishGameEvent(new GameEventContext
            {
                type = GameEventType.CardMilled,
                source = card,
                card = card,
                playerId = playerID,
                controllerId = card.ControllerPlayerID,
                fromZone = GameZone.Library,
                toZone = GameZone.Graveyard
            });
            moved++;
        }
        return moved;
    }

    public bool DiscardCard(RuntimeCard card)
    {
        return card != null
            && card.CurrentZone == GameZone.Hand
            && MoveCardToGraveyard(card);
    }

    public bool ReturnCardToHand(RuntimeCard card)
    {
        return card != null && MoveCardToZone(card, GameZone.Hand);
    }

    public bool CounterSpell(RuntimeCard card)
    {
        if (card == null || card.CurrentZone != GameZone.Stack) return false;
        return MoveCardToGraveyard(card);
    }

    public bool CastCardWithoutPayingMana(RuntimeCard card, int featureIndex = -1)
    {
        if (card == null || card.CurrentZone == GameZone.Stack || IsOnBattlefield(card)) return false;
        CardFeatureRuntime feature = featureIndex >= 0
            ? card.FindFeature(card.CurrentFaceIndex, featureIndex)
            : GetDefaultCastFeature(card);
        if (feature == null || feature.HasType(CardType.Land)) return false;

        GameZone fromZone = card.CurrentZone;
        if (!RemoveRuntimeCardFromKnownZones(card)) return false;
        card.SetSpellFeature(feature);
        card.MarkZoneChange();
        card.ReassignObjectId(nextObjectId++);
        card.SetCurrentZone(GameZone.Stack);
        PushStackItem(GameStackItem.Spell(card, feature));
        PublishGameEvent(new GameEventContext
        {
            type = GameEventType.SpellCast,
            source = card,
            card = card,
            playerId = card.ControllerPlayerID,
            controllerId = card.ControllerPlayerID,
            fromZone = fromZone,
            toZone = GameZone.Stack
        });
        NotifyStackChanged();
        BeginPriority(card.ControllerPlayerID);
        return true;
    }

    public int CreateTokens(
        string setCode,
        string collectorNumber,
        int controllerPlayerID,
        int count)
    {
        if ((controllerPlayerID != 1 && controllerPlayerID != 2) || count <= 0) return 0;

        CardDefinition definition = CardDatabaseRepository.FindByPrintingKey(
            setCode,
            collectorNumber);
        if (definition == null) return 0;

        List<RuntimeCard> battlefield = controllerPlayerID == 1
            ? player1Battlefield
            : player2Battlefield;
        int created = 0;
        for (int i = 0; i < count; i++)
        {
            var token = new RuntimeCard(definition, controllerPlayerID);
            token.MarkAsToken();
            AddToBattlefield(battlefield, token);
            PublishGameEvent(new GameEventContext
            {
                type = GameEventType.TokenCreated,
                source = token,
                card = token,
                playerId = controllerPlayerID,
                controllerId = controllerPlayerID,
                fromZone = GameZone.None,
                toZone = GameZone.Battlefield
            });
            created++;
        }

        PerformStateBasedActions();
        return created;
    }

    public RuntimeCard CreateEmblem(
        string setCode,
        string collectorNumber,
        int controllerPlayerID)
    {
        if (controllerPlayerID != 1 && controllerPlayerID != 2) return null;

        CardDefinition definition = CardDatabaseRepository.FindByPrintingKey(
            setCode,
            collectorNumber);
        if (definition == null) return null;

        var emblem = new RuntimeCard(definition, controllerPlayerID);
        emblem.MarkAsEmblem();
        RegisterRuntimeCard(emblem, GameZone.Command);
        List<RuntimeCard> command = controllerPlayerID == 1
            ? player1Command
            : player2Command;
        command.Add(emblem);
        return emblem;
    }

    public RuntimeCard CopySpell(RuntimeCard sourceSpell, int controllerPlayerID)
    {
        if (sourceSpell == null
            || sourceSpell.CurrentZone != GameZone.Stack
            || (controllerPlayerID != 1 && controllerPlayerID != 2))
        {
            return null;
        }

        GameStackItem sourceItem = stack.FindLast(item =>
            item != null
            && item.Kind == GameStackItemKind.Spell
            && ReferenceEquals(item.SourceCard, sourceSpell));
        if (sourceItem == null || sourceSpell.Data == null) return null;

        var copy = new RuntimeCard(sourceSpell.Data, sourceSpell.OwnerPlayerID);
        copy.MarkAsSpellCopy();
        copy.SetController(controllerPlayerID);
        copy.SetSpellFeature(sourceItem.SpellFeature);
        copy.SetSpellTargets(sourceItem.Targets);
        RegisterRuntimeCard(copy, GameZone.Stack);
        copy.SetCurrentCastDeclaration(
            sourceItem.CastDeclaration?.CopyForSource(
                copy,
                controllerPlayerID));
        PushStackItem(GameStackItem.Spell(copy, sourceItem.SpellFeature));
        NotifyStackChanged();
        return copy;
    }

    public bool CopyStackAbility(int stackIndex, int controllerPlayerID)
    {
        if (stackIndex < 0
            || stackIndex >= stack.Count
            || (controllerPlayerID != 1 && controllerPlayerID != 2))
        {
            return false;
        }

        GameStackItem source = stack[stackIndex];
        if (source == null || source.Kind == GameStackItemKind.Spell) return false;

        GameStackItem copy = source.CopyForController(controllerPlayerID);
        if (copy == null) return false;
        PushStackItem(copy);
        NotifyStackChanged();
        return true;
    }

    public bool TapCard(RuntimeCard card)
    {
        if (card == null || !IsOnBattlefield(card)) return false;
        card.Tap();
        return true;
    }

    public bool UntapCard(RuntimeCard card)
    {
        if (card == null || !IsOnBattlefield(card)) return false;
        card.Untap();
        return true;
    }

    public bool AddPowerToughnessModifier(RuntimeCard card, int power, int toughness, string source, bool untilEndOfTurn)
    {
        if (card == null || !card.HasType(CardType.Creature) || !IsOnBattlefield(card)) return false;
        card.AddPowerToughnessModifier(power, toughness, source, untilEndOfTurn);
        PerformStateBasedActions();
        return true;
    }

    public bool SetPowerToughness(RuntimeCard card, int power, int toughness, string source, bool untilEndOfTurn)
    {
        if (card == null || !card.HasType(CardType.Creature) || !IsOnBattlefield(card)) return false;
        card.SetPowerToughness(power, toughness, source, untilEndOfTurn);
        PerformStateBasedActions();
        return true;
    }

    public bool SwitchPowerToughness(RuntimeCard card, bool untilEndOfTurn)
    {
        if (card == null || !card.HasType(CardType.Creature) || !IsOnBattlefield(card)) return false;
        card.SwitchPowerToughness(untilEndOfTurn);
        PerformStateBasedActions();
        return true;
    }

    public long AddCardTypeEffect(
        RuntimeCard card,
        CardType cardTypes,
        bool remove,
        string source,
        bool untilEndOfTurn)
    {
        if (card == null || cardTypes == CardType.None || !IsOnBattlefield(card)) return 0;
        long effectId = card.AddCardTypeEffect(cardTypes, remove, source, untilEndOfTurn);
        PerformStateBasedActions();
        return effectId;
    }

    public long SetColorEffect(
        RuntimeCard card,
        ManaColorFlags colors,
        string source,
        bool untilEndOfTurn)
    {
        if (card == null || !IsOnBattlefield(card)) return 0;
        long effectId = card.SetColorEffect(colors, source, untilEndOfTurn);
        PerformStateBasedActions();
        return effectId;
    }

    public long AddKeywordEffect(
        RuntimeCard card,
        string keyword,
        bool remove,
        string source,
        bool untilEndOfTurn)
    {
        if (card == null || !IsOnBattlefield(card)) return 0;
        long effectId = card.AddKeywordEffect(keyword, remove, source, untilEndOfTurn);
        PerformStateBasedActions();
        return effectId;
    }

    private void InitializeStructuredCardOnBattlefield(
        RuntimeCard card,
        CardFeatureRuntime enteringFeature)
    {
        if (card == null) return;

        RegisterStructuredFeatureAbilities(card);
        if (enteringFeature != null
            && (enteringFeature.Role == CardFeatureRole.RoomDoor
                || (enteringFeature.IsPrimary
                    && card.HasFeatureRole(CardFeatureRole.RoomDoor)
                    && enteringFeature.HasSubtype("Room", "\u623f\u95f4"))))
        {
            card.UnlockRoomFeature(enteringFeature);
        }

        for (int i = 0; i < card.Features.Count; i++)
        {
            CardFeatureRuntime feature = card.Features[i];
            if (feature != null && feature.IsSaga && feature.GetFinalChapterNumber() > 0)
            {
                RequestSagaLoreCounters(card, 1);
                break;
            }
        }

        if (card.HasType(CardType.Battle) && card.BattleProtectorPlayerID == 0)
        {
            card.SetBattleProtector(card.ControllerPlayerID == 1 ? 2 : 1);
        }
    }

    private void RegisterStructuredFeatureAbilities(RuntimeCard card)
    {
        if (card == null) return;

        for (int i = 0; i < card.Features.Count; i++)
        {
            CardFeatureRuntime feature = card.Features[i];
            if (feature == null) continue;

            if (feature.Role == CardFeatureRole.RoomDoor)
            {
                string abilityName = $"Unlock room: {feature.CardName} [{feature.FeatureIndex}]";
                if (HasAbilityNamed(card, abilityName)) continue;

                card.Abilities.Add(new ActivatedAbility
                {
                    Name = abilityName,
                    Description = $"Unlock {feature.CardName} for {feature.ManaCost}.",
                    CanActivate = (rules, source) =>
                        rules != null && rules.CanUnlockRoomDoor(source, feature),
                    Execute = (rules, source) =>
                    {
                        rules?.UnlockRoomDoor(source, feature);
                    }
                });
                continue;
            }

            if (feature.Role != CardFeatureRole.ClassLevel || feature.ClassLevel <= 1) continue;

            string classAbilityName = $"Advance class to level {feature.ClassLevel}";
            if (HasAbilityNamed(card, classAbilityName)) continue;
            card.Abilities.Add(new ActivatedAbility
            {
                Name = classAbilityName,
                Description = $"Advance to class level {feature.ClassLevel} for {feature.ManaCost}.",
                CanActivate = (rules, source) =>
                    rules != null && rules.CanAdvanceClass(source, feature),
                Execute = (rules, source) =>
                {
                    rules?.AdvanceClass(source, feature);
                }
            });
        }
    }

    public bool CanUnlockRoomDoor(RuntimeCard card, CardFeatureRuntime door)
    {
        return card != null
            && door != null
            && door.Role == CardFeatureRole.RoomDoor
            && IsOnBattlefield(card)
            && !card.IsRoomFeatureUnlocked(door)
            && IsSorcerySpeedWindow(card.ControllerPlayerID)
            && CanPayWithPoolOrAvailableMana(card.ControllerPlayerID, door.ManaCost);
    }

    public bool UnlockRoomDoor(RuntimeCard card, CardFeatureRuntime door)
    {
        if (!CanUnlockRoomDoor(card, door)
            || !TryPayMana(card.ControllerPlayerID, door.ManaCost)
            || !card.UnlockRoomFeature(door))
        {
            return false;
        }

        QueueStructuredFeatureHook(card, door, $"OnRoomUnlocked{door.FeatureIndex}", "OnRoomUnlocked");
        Debug.Log($"Player {card.ControllerPlayerID} unlocks {door.CardName}.");
        return true;
    }

    public bool CanAdvanceClass(RuntimeCard card, CardFeatureRuntime levelFeature)
    {
        return card != null
            && levelFeature != null
            && levelFeature.Role == CardFeatureRole.ClassLevel
            && levelFeature.ClassLevel == card.ClassLevel + 1
            && IsOnBattlefield(card)
            && IsSorcerySpeedWindow(card.ControllerPlayerID)
            && CanPayWithPoolOrAvailableMana(card.ControllerPlayerID, levelFeature.ManaCost);
    }

    public bool AdvanceClass(RuntimeCard card, CardFeatureRuntime levelFeature)
    {
        if (!CanAdvanceClass(card, levelFeature)
            || !TryPayMana(card.ControllerPlayerID, levelFeature.ManaCost)
            || !card.SetClassLevel(levelFeature.ClassLevel))
        {
            return false;
        }

        InvokeStructuredFeatureHook(
            card,
            levelFeature,
            $"OnClassLevel{levelFeature.ClassLevel}",
            "OnClassLevel");
        Debug.Log($"{card.Data.cardName} advances to class level {levelFeature.ClassLevel}.");
        return true;
    }

    public bool PrepareCard(RuntimeCard source, int featureIndex = -1)
    {
        if (source == null || !IsOnBattlefield(source) || source.IsPrepared) return false;

        CardFeatureRuntime prepareFeature = null;
        for (int i = 0; i < source.Features.Count; i++)
        {
            CardFeatureRuntime candidate = source.Features[i];
            if (candidate == null
                || candidate.Role != CardFeatureRole.Prepare
                || (featureIndex >= 0 && candidate.FeatureIndex != featureIndex))
            {
                continue;
            }

            prepareFeature = candidate;
            break;
        }

        if (prepareFeature == null) return false;
        List<RuntimeCard> exile = GetExileList(source.OwnerPlayerID);
        if (exile == null) return false;

        var preparedCopy = new RuntimeCard(source.Data, source.OwnerPlayerID);
        preparedCopy.SetSpellFeature(preparedCopy.FindFeature(
            prepareFeature.FaceIndex,
            prepareFeature.FeatureIndex));
        preparedCopy.MarkAsPreparedCopy(source);
        exile.Add(preparedCopy);
        Debug.Log($"{source.Data.cardName} becomes prepared with {prepareFeature.CardName}.");
        return true;
    }

    public bool StopPreparing(RuntimeCard source)
    {
        if (source == null || !source.IsPrepared) return false;
        RemovePreparedCopyForSource(source);
        return true;
    }

    public bool SolveCase(RuntimeCard card)
    {
        if (card == null || card.IsCaseSolved || !IsOnBattlefield(card)) return false;

        CardFeatureRuntime solvedStage = null;
        CardFeatureRuntime firstStage = null;
        for (int i = 0; i < card.Features.Count; i++)
        {
            CardFeatureRuntime feature = card.Features[i];
            if (feature == null || feature.Role != CardFeatureRole.CaseStage) continue;
            firstStage ??= feature;
            if (feature.IsCaseStageNamed("\u5df2\u89e3\u660e", "Solved", "Solved Case"))
            {
                solvedStage = feature;
                break;
            }

            if (solvedStage == null && !ReferenceEquals(feature, firstStage))
            {
                solvedStage = feature;
            }
        }

        card.SolveCase();
        QueueStructuredFeatureHook(card, solvedStage, "OnCaseSolved", string.Empty);
        Debug.Log($"{card.Data.cardName} is solved.");
        return true;
    }

    private void QueueCaseCheckAbilities()
    {
        QueueCaseCheckAbilities(player1Battlefield);
        QueueCaseCheckAbilities(player2Battlefield);
    }

    private void QueueCaseCheckAbilities(IReadOnlyList<RuntimeCard> battlefield)
    {
        if (battlefield == null) return;
        for (int cardIndex = 0; cardIndex < battlefield.Count; cardIndex++)
        {
            RuntimeCard card = battlefield[cardIndex];
            if (card == null || card.IsCaseSolved) continue;

            CardFeatureRuntime condition = null;
            for (int featureIndex = 0; featureIndex < card.Features.Count; featureIndex++)
            {
                CardFeatureRuntime feature = card.Features[featureIndex];
                if (feature == null || feature.Role != CardFeatureRole.CaseStage) continue;
                condition ??= feature;
                if (feature.IsCaseStageNamed("\u89e3\u660e\u6761\u4ef6", "To Solve", "Solve Condition"))
                {
                    condition = feature;
                    break;
                }
            }

            if (condition != null && CardEffectExecutor.HasCardScriptHook(card, "OnCaseCheck"))
            {
                pendingTriggeredAbilities.Add(
                    GameStackItem.FeatureAbility(card, condition, "OnCaseCheck"));
            }
        }
    }

    private void AdvanceSagaLoreCounters(int playerID)
    {
        IReadOnlyList<RuntimeCard> battlefield =
            playerID == 1 ? player1Battlefield : player2Battlefield;
        for (int i = 0; i < battlefield.Count; i++)
        {
            RuntimeCard card = battlefield[i];
            if (HasSagaChapters(card)) RequestSagaLoreCounters(card, 1);
        }
    }

    private void RequestSagaLoreCounters(RuntimeCard card, int amount)
    {
        if (card == null || amount <= 0 || !IsOnBattlefield(card)) return;
        PrepareCounterChange(
            card,
            "lore",
            amount,
            resolvedAmount => CommitSagaLoreCounters(card, resolvedAmount));
    }

    private void CommitSagaLoreCounters(RuntimeCard card, int amount)
    {
        if (card == null || amount <= 0 || !IsOnBattlefield(card)) return;

        int previous = card.GetCounters("lore");
        int current = card.AddCounters("lore", amount);
        for (int featureIndex = 0; featureIndex < card.Features.Count; featureIndex++)
        {
            CardFeatureRuntime feature = card.Features[featureIndex];
            if (feature == null || !feature.IsSaga) continue;

            for (int entryIndex = 0; entryIndex < feature.RuleEntries.Count; entryIndex++)
            {
                CardRuleEntryDefinition entry = feature.RuleEntries[entryIndex];
                IReadOnlyList<int> chapters = CardFeatureRuntime.GetChapterNumbers(entry);
                for (int chapterIndex = 0; chapterIndex < chapters.Count; chapterIndex++)
                {
                    int chapter = chapters[chapterIndex];
                    if (previous < chapter && current >= chapter)
                    {
                        pendingTriggeredAbilities.Add(GameStackItem.ChapterAbility(
                            card,
                            feature,
                            entry,
                            $"OnChapter{chapter}",
                            chapter));
                    }
                }
            }
        }

        Debug.Log($"{card.Data.cardName} now has {current} lore counter(s).");
    }

    private static bool HasSagaChapters(RuntimeCard card)
    {
        if (card == null) return false;
        for (int i = 0; i < card.Features.Count; i++)
        {
            CardFeatureRuntime feature = card.Features[i];
            if (feature != null && feature.IsSaga && feature.GetFinalChapterNumber() > 0)
            {
                return true;
            }
        }

        return false;
    }

    private bool ProcessSagaStateBasedActions()
    {
        var cardsToSacrifice = new List<RuntimeCard>();
        CollectSagasToSacrifice(player1Battlefield, cardsToSacrifice);
        CollectSagasToSacrifice(player2Battlefield, cardsToSacrifice);
        if (cardsToSacrifice.Count == 0) return false;

        var sacrificeEvents = new Dictionary<RuntimeCard, GameEventContext>();
        for (int i = 0; i < cardsToSacrifice.Count; i++)
        {
            RuntimeCard card = cardsToSacrifice[i];
            var sacrificeEvent = new GameEventContext
            {
                type = GameEventType.PermanentSacrificed,
                source = card,
                card = card,
                playerId = card.OwnerPlayerID,
                controllerId = card.ControllerPlayerID,
                fromZone = GameZone.Battlefield,
                toZone = GameZone.Graveyard
            };
            sacrificeEvent.CaptureObjectIdentities();
            sacrificeEvents.Add(card, sacrificeEvent);
        }

        return MoveCardsToZone(
            cardsToSacrifice,
            GameZone.Graveyard,
            false,
            true,
            moved =>
            {
                if (!moved) return;
                for (int i = 0; i < cardsToSacrifice.Count; i++)
                {
                    RuntimeCard card = cardsToSacrifice[i];
                    if (card == null || card.CurrentZone == GameZone.Battlefield)
                    {
                        continue;
                    }

                    GameEventContext sacrificeEvent = sacrificeEvents[card];
                    sacrificeEvent.toZone = card.CurrentZone;
                    PublishGameEvent(sacrificeEvent);
                    Debug.Log(
                        $"{card.GetCardName()} is sacrificed after its final chapter ability left the stack.");
                }
            });
    }

    private void CollectSagasToSacrifice(
        IReadOnlyList<RuntimeCard> battlefield,
        List<RuntimeCard> destination)
    {
        if (battlefield == null || destination == null) return;
        for (int cardIndex = 0; cardIndex < battlefield.Count; cardIndex++)
        {
            RuntimeCard card = battlefield[cardIndex];
            int finalChapter = GetFinalSagaChapter(card);
            if (finalChapter > 0
                && card.GetCounters("lore") >= finalChapter
                && !HasPendingChapterAbility(card))
            {
                destination.Add(card);
            }
        }
    }

    private static int GetFinalSagaChapter(RuntimeCard card)
    {
        int finalChapter = 0;
        if (card == null) return finalChapter;
        for (int i = 0; i < card.Features.Count; i++)
        {
            CardFeatureRuntime feature = card.Features[i];
            if (feature != null && feature.IsSaga)
            {
                finalChapter = Mathf.Max(finalChapter, feature.GetFinalChapterNumber());
            }
        }

        return finalChapter;
    }

    private bool HasPendingChapterAbility(RuntimeCard source)
    {
        return ContainsChapterAbility(stack, source)
            || ContainsChapterAbility(pendingTriggeredAbilities, source)
            || ContainsChapterAbility(pendingTriggerOrderItems, source)
            || ContainsChapterAbility(deferredTriggerOrderItems, source);
    }

    private bool HasPendingBattleDefeatAbility(RuntimeCard source)
    {
        return ContainsBattleDefeatAbility(stack, source)
            || ContainsBattleDefeatAbility(pendingTriggeredAbilities, source)
            || ContainsBattleDefeatAbility(pendingTriggerOrderItems, source)
            || ContainsBattleDefeatAbility(deferredTriggerOrderItems, source);
    }

    private static bool ContainsBattleDefeatAbility(
        IReadOnlyList<GameStackItem> items,
        RuntimeCard source)
    {
        if (items == null || source == null) return false;
        for (int i = 0; i < items.Count; i++)
        {
            GameStackItem item = items[i];
            if (item != null
                && ReferenceEquals(item.SourceCard, source)
                && item.SourceObjectId == source.ObjectId
                && string.Equals(
                    item.HookName,
                    "OnBattleDefeated",
                    System.StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ContainsChapterAbility(
        IReadOnlyList<GameStackItem> items,
        RuntimeCard source)
    {
        if (items == null || source == null) return false;
        for (int i = 0; i < items.Count; i++)
        {
            GameStackItem item = items[i];
            if (item != null
                && ReferenceEquals(item.SourceCard, source)
                && item.RuleEntry != null
                && item.RuleEntry.kind == CardRuleEntryKind.Chapter)
            {
                return true;
            }
        }

        return false;
    }

    private void QueueStructuredFeatureHook(
        RuntimeCard card,
        CardFeatureRuntime feature,
        string preferredHook,
        string fallbackHook)
    {
        if (card == null) return;
        string hook = CardEffectExecutor.HasCardScriptHook(card, preferredHook)
            ? preferredHook
            : fallbackHook;
        if (string.IsNullOrEmpty(hook) || !CardEffectExecutor.HasCardScriptHook(card, hook)) return;

        pendingTriggeredAbilities.Add(GameStackItem.FeatureAbility(card, feature, hook));
    }

    private void InvokeStructuredFeatureHook(
        RuntimeCard card,
        CardFeatureRuntime feature,
        string preferredHook,
        string fallbackHook)
    {
        if (card == null) return;
        string hook = CardEffectExecutor.HasCardScriptHook(card, preferredHook)
            ? preferredHook
            : fallbackHook;
        if (string.IsNullOrEmpty(hook) || !CardEffectExecutor.HasCardScriptHook(card, hook)) return;

        card.SetResolvingAbilityContext(feature, null, 0);
        CardEffectExecutor.ExecuteTriggeredHook(this, card, hook);
        card.ClearResolvingAbilityContext();
    }

    private void RemovePreparedCopyForSource(RuntimeCard source)
    {
        RuntimeCard copy = source?.PreparedCopy;
        if (copy == null)
        {
            source?.ClearPreparedLink();
            return;
        }

        GetExileList(copy.OwnerPlayerID)?.Remove(copy);
        copy.ClearPreparedLink();
        copy.OnDestroy();
    }

    private bool CleanupOrphanedPreparedCopies()
    {
        bool removed = false;
        removed |= CleanupOrphanedPreparedCopies(player1Graveyard);
        removed |= CleanupOrphanedPreparedCopies(player2Graveyard);
        removed |= CleanupOrphanedPreparedCopies(player1Exile);
        removed |= CleanupOrphanedPreparedCopies(player2Exile);
        ResetZonePermanentDesignations(player1Graveyard);
        ResetZonePermanentDesignations(player2Graveyard);
        ResetZonePermanentDesignations(player1Exile);
        ResetZonePermanentDesignations(player2Exile);
        return removed;
    }

    private bool CleanupOrphanedPreparedCopies(IReadOnlyList<RuntimeCard> cards)
    {
        bool removed = false;
        if (cards == null) return false;
        for (int i = 0; i < cards.Count; i++)
        {
            RuntimeCard source = cards[i];
            if (source?.PreparedCopy == null || IsOnBattlefield(source)) continue;
            RemovePreparedCopyForSource(source);
            source.ResetPermanentDesignations();
            removed = true;
        }

        return removed;
    }

    private static void ResetZonePermanentDesignations(IReadOnlyList<RuntimeCard> cards)
    {
        if (cards == null) return;
        for (int i = 0; i < cards.Count; i++)
        {
            RuntimeCard card = cards[i];
            if (card == null || card.IsSpellCopy) continue;
            card.ResetPermanentDesignations();
        }
    }

    private static GameEventType ClassifyZoneChangeEvent(
        RuntimeCard card,
        GameZone fromZone,
        GameZone destination,
        bool wasOnBattlefield)
    {
        if (destination == GameZone.Battlefield)
        {
            return GameEventType.PermanentEntered;
        }
        if (wasOnBattlefield
            && destination == GameZone.Graveyard
            && card != null
            && card.HasType(CardType.Creature))
        {
            return GameEventType.CardDied;
        }
        if (fromZone == GameZone.Hand && destination == GameZone.Graveyard)
        {
            return GameEventType.CardDiscarded;
        }
        if (wasOnBattlefield)
        {
            return GameEventType.PermanentLeft;
        }
        if (fromZone == GameZone.Library && destination == GameZone.Hand)
        {
            return GameEventType.CardDrawn;
        }
        return GameEventType.ZoneChanged;
    }

    private bool AddToBattlefield(
        List<RuntimeCard> battlefield,
        RuntimeCard card,
        CardFeatureRuntime enteringFeature = null)
    {
        if (battlefield == null || card == null) return false;
        return MoveDetachedCardToZone(
            card,
            GameZone.Battlefield,
            enteringFeature);
    }

    private bool MoveDetachedCardToZone(
        RuntimeCard card,
        GameZone destination,
        CardFeatureRuntime enteringFeature = null,
        bool toTop = false)
    {
        if (card == null
            || destination == GameZone.None
            || destination == GameZone.Stack)
        {
            return false;
        }

        GameZone fromZone = card.CurrentZone;
        int enteringController = card.ControllerPlayerID;
        var gameEvent = new GameEventContext
        {
            type = ClassifyZoneChangeEvent(
                card,
                fromZone,
                destination,
                false),
            source = card,
            card = card,
            playerId = card.OwnerPlayerID,
            controllerId = enteringController,
            fromZone = fromZone,
            toZone = destination
        };
        PrepareGameEvent(gameEvent);
        if (gameEvent.prevented
            || gameEvent.toZone == GameZone.None
            || gameEvent.toZone == GameZone.Stack)
        {
            return false;
        }

        destination = gameEvent.toZone;
        if (destination != GameZone.Library
            && destination != GameZone.Hand
            && destination != GameZone.Battlefield
            && destination != GameZone.Graveyard
            && destination != GameZone.Exile
            && destination != GameZone.Command)
        {
            return false;
        }
        gameEvent.type = ClassifyZoneChangeEvent(
            card,
            fromZone,
            destination,
            false);

        card.MarkZoneChange();
        card.ReassignObjectId(nextObjectId++);
        if (destination == GameZone.Battlefield)
        {
            card.SetController(enteringController);
            RegisterRuntimeCard(card, GameZone.Battlefield);
            MarkCardEnteredBattlefield(card);
            List<RuntimeCard> destinationBattlefield = enteringController == 1
                ? player1Battlefield
                : player2Battlefield;
            destinationBattlefield.Add(card);
            InitializeStructuredCardOnBattlefield(card, enteringFeature);
        }
        else
        {
            card.SetController(card.OwnerPlayerID);
            card.SetCurrentZone(destination);
            List<RuntimeCard> destinationCards = destination switch
            {
                GameZone.Library => GetLibraryList(card.OwnerPlayerID),
                GameZone.Hand => GetHandList(card.OwnerPlayerID),
                GameZone.Graveyard => GetGraveyardList(card.OwnerPlayerID),
                GameZone.Exile => GetExileList(card.OwnerPlayerID),
                GameZone.Command => card.OwnerPlayerID == 1
                    ? player1Command
                    : player2Command,
                _ => null
            };
            if (destinationCards == null) return false;
            if (destination == GameZone.Library && toTop)
            {
                destinationCards.Insert(0, card);
            }
            else
            {
                destinationCards.Add(card);
            }
        }

        gameEvent.toZone = destination;
        gameEvent.controllerId = card.ControllerPlayerID;
        CompleteGameEvent(gameEvent);
        return true;
    }

    private void MarkCardEnteredBattlefield(RuntimeCard card)
    {
        if (card == null) return;
        card.TurnEnteredBattlefield = turnNumber;
        card.BattlefieldEntrySequence = ++nextBattlefieldEntrySequence;
        eventPipeline.BindIntrinsicEffects(card);
    }

    private List<RuntimeCard> GetGraveyardList(int playerID)
    {
        if (playerID == 1) return player1Graveyard;
        if (playerID == 2) return player2Graveyard;
        return null;
    }

    private List<RuntimeCard> GetHandList(int playerID)
    {
        if (playerID == 1) return player1Hand;
        if (playerID == 2) return player2Hand;
        return null;
    }

    private List<RuntimeCard> GetLibraryList(int playerID)
    {
        if (playerID == 1) return player1Library;
        if (playerID == 2) return player2Library;
        return null;
    }

    public IReadOnlyList<RuntimeCard> GetZoneCards(int playerID, GameZone zone)
    {
        switch (zone)
        {
            case GameZone.Library:
                return GetLibraryList(playerID) ?? emptyCards;
            case GameZone.Hand:
                return GetHandList(playerID) ?? emptyCards;
            case GameZone.Battlefield:
                return playerID == 1
                    ? player1Battlefield
                    : playerID == 2 ? player2Battlefield : emptyCards;
            case GameZone.Graveyard:
                return GetGraveyardList(playerID) ?? emptyCards;
            case GameZone.Exile:
                return GetExileList(playerID) ?? emptyCards;
            case GameZone.Stack:
                return GetStackCards();
            case GameZone.Command:
                return playerID == 1
                    ? player1Command
                    : playerID == 2 ? player2Command : emptyCards;
            default:
                return emptyCards;
        }
    }

    public RuntimeCard GetZoneCard(int playerID, GameZone zone, int index)
    {
        IReadOnlyList<RuntimeCard> cards = GetZoneCards(playerID, zone);
        return index >= 0 && index < cards.Count ? cards[index] : null;
    }

    public RuntimeCard FindRuntimeCard(long objectId)
    {
        if (objectId <= 0) return null;
        GameZone[] zones =
        {
            GameZone.Library,
            GameZone.Hand,
            GameZone.Battlefield,
            GameZone.Graveyard,
            GameZone.Exile,
            GameZone.Stack,
            GameZone.Command
        };
        for (int player = 1; player <= 2; player++)
        {
            for (int zoneIndex = 0; zoneIndex < zones.Length; zoneIndex++)
            {
                IReadOnlyList<RuntimeCard> cards = GetZoneCards(player, zones[zoneIndex]);
                for (int i = 0; i < cards.Count; i++)
                {
                    if (cards[i] != null && cards[i].ObjectId == objectId) return cards[i];
                }
            }
        }

        return null;
    }

    private void RegisterRuntimeCard(RuntimeCard card, GameZone zone)
    {
        if (card == null) return;
        if (card.ObjectId <= 0) card.AssignObjectId(nextObjectId++);
        card.SetCurrentZone(zone);
    }

    private void MoveRuntimeCardToZone(RuntimeCard card, GameZone fromZone, GameZone toZone)
    {
        if (card == null) return;
        RegisterRuntimeCard(card, toZone);
        card.SetCurrentZone(toZone);
        PublishGameEvent(new GameEventContext
        {
            type = GameEventType.ZoneChanged,
            source = card,
            card = card,
            playerId = card.OwnerPlayerID,
            controllerId = card.ControllerPlayerID,
            fromZone = fromZone,
            toZone = toZone
        });
    }

    public long RegisterScriptTrigger(
        RuntimeCard source,
        GameEventType eventType,
        string hookName,
        string conditionHookName,
        RuntimeEffectDuration duration,
        bool otherCardsOnly,
        string linkId = null)
    {
        ScriptTriggerRegistration registration = eventPipeline.RegisterTrigger(
            source,
            eventType,
            hookName,
            conditionHookName,
            duration,
            otherCardsOnly,
            linkId);
        return registration?.Id ?? 0;
    }

    public long RegisterScriptReplacement(
        RuntimeCard source,
        GameEventType eventType,
        GameEventReplacementOperation operation,
        string hookName,
        int value,
        RuntimeEffectDuration duration,
        string conditionHookName = null,
        string linkId = null,
        ReplacementEffectCategory category = ReplacementEffectCategory.General)
    {
        ScriptReplacementRegistration registration = eventPipeline.RegisterReplacement(
            source,
            eventType,
            operation,
            hookName,
            value,
            duration,
            conditionHookName,
            linkId,
            category);
        return registration?.Id ?? 0;
    }

    public long RegisterScriptContinuousEffect(
        RuntimeCard source,
        ContinuousEffectOperation operation,
        string filterHookName,
        string dependencyKey,
        CardType cardTypes,
        ManaColorFlags colors,
        string textValue,
        int valueA,
        int valueB,
        RuntimeEffectDuration duration,
        string linkId,
        bool characteristicDefining = false,
        ContinuousEffectCharacteristic reads = ContinuousEffectCharacteristic.None,
        ContinuousEffectCharacteristic writes = ContinuousEffectCharacteristic.None,
        RuntimeCard copySource = null,
        CardSupertype supertypes = CardSupertype.None)
    {
        ScriptContinuousRegistration registration = eventPipeline.RegisterContinuous(
            source,
            operation,
            filterHookName,
            dependencyKey,
            cardTypes,
            colors,
            textValue,
            valueA,
            valueB,
            duration,
            linkId,
            characteristicDefining,
            reads,
            writes,
            copySource,
            supertypes);
        if (registration == null) return 0;
        RecalculateBattlefieldContinuousEffects();
        PerformStateBasedActions();
        return registration.Id;
    }

    public long RegisterScriptRuleEffect(
        RuntimeCard source,
        string ruleKey,
        RuleEffectOperation operation,
        int value,
        int affectedPlayerId,
        string filterHookName,
        RuntimeEffectDuration duration,
        string linkId)
    {
        if (source == null
            || string.IsNullOrWhiteSpace(ruleKey)
            || (affectedPlayerId != 0
                && affectedPlayerId != 1
                && affectedPlayerId != 2))
        {
            return 0;
        }

        ScriptRuleRegistration registration = eventPipeline.RegisterRuleEffect(
            source,
            ruleKey,
            operation,
            value,
            affectedPlayerId,
            filterHookName,
            duration,
            linkId);
        return registration?.Id ?? 0;
    }

    public int EvaluateScriptRuleValue(
        string ruleKey,
        int playerId,
        RuntimeCard subject,
        int baseValue)
    {
        int result = baseValue;
        IReadOnlyList<ScriptRuleRegistration> registrations = eventPipeline.RuleEffects;
        for (int i = 0; i < registrations.Count; i++)
        {
            ScriptRuleRegistration registration = registrations[i];
            if (!IsApplicableScriptRule(registration, ruleKey, playerId, subject)) continue;

            switch (registration.Operation)
            {
                case RuleEffectOperation.Add:
                    result += registration.Value;
                    break;
                case RuleEffectOperation.Set:
                    result = registration.Value;
                    break;
                case RuleEffectOperation.Minimum:
                    result = Mathf.Max(result, registration.Value);
                    break;
                case RuleEffectOperation.Maximum:
                    result = Mathf.Min(result, registration.Value);
                    break;
            }
        }
        return result;
    }

    public bool EvaluateScriptRulePermission(
        string ruleKey,
        int playerId,
        RuntimeCard subject,
        bool baseValue)
    {
        bool result = baseValue;
        IReadOnlyList<ScriptRuleRegistration> registrations = eventPipeline.RuleEffects;
        for (int i = 0; i < registrations.Count; i++)
        {
            ScriptRuleRegistration registration = registrations[i];
            if (!IsApplicableScriptRule(registration, ruleKey, playerId, subject)) continue;
            if (registration.Operation == RuleEffectOperation.Allow) result = true;
            else if (registration.Operation == RuleEffectOperation.Deny) result = false;
        }
        return result;
    }

    private bool IsApplicableScriptRule(
        ScriptRuleRegistration registration,
        string ruleKey,
        int playerId,
        RuntimeCard subject)
    {
        if (registration == null
            || !registration.IsActive(IsRuntimeEffectSourceActive)
            || !string.Equals(
                registration.RuleKey,
                ruleKey,
                System.StringComparison.OrdinalIgnoreCase)
            || (registration.AffectedPlayerId != 0
                && registration.AffectedPlayerId != playerId))
        {
            return false;
        }

        if (string.IsNullOrEmpty(registration.FilterHookName)) return true;
        if (registration.Source == null || subject == null) return false;

        RuntimeCard source = registration.Source;
        GameEventContext previousContext = source.ResolvingGameEvent;
        source.SetResolvingGameEvent(new GameEventContext
        {
            source = source,
            card = subject,
            playerId = playerId,
            controllerId = subject.ControllerPlayerID,
            fromZone = subject.CurrentZone,
            toZone = subject.CurrentZone
        });
        bool matches = CardEffectExecutor.EvaluateCardScriptPredicate(
            this,
            source,
            registration.FilterHookName,
            false);
        source.SetResolvingGameEvent(previousContext);
        return matches;
    }

    public bool RemoveScriptEffect(long effectId)
    {
        ScriptContinuousRegistration continuous =
            eventPipeline.FindContinuous(effectId);
        if (continuous != null)
        {
            foreach (KeyValuePair<RuntimeCard, long> applied in continuous.AppliedEffectIds)
            {
                applied.Key?.RemoveContinuousEffect(applied.Value);
            }
            continuous.AppliedEffectIds.Clear();
        }
        return eventPipeline.Remove(effectId);
    }

    public int RemoveScriptEffectsByLink(RuntimeCard source, string linkId)
    {
        if (source == null || string.IsNullOrWhiteSpace(linkId)) return 0;

        var appliedEffectIds = new List<long>();
        IReadOnlyList<ScriptContinuousRegistration> continuousEffects =
            eventPipeline.ContinuousEffects;
        for (int i = 0; i < continuousEffects.Count; i++)
        {
            ScriptContinuousRegistration effect = continuousEffects[i];
            if (!ReferenceEquals(effect.Source, source)
                || !string.Equals(
                    effect.LinkId,
                    linkId.Trim(),
                    System.StringComparison.Ordinal))
            {
                continue;
            }

            foreach (KeyValuePair<RuntimeCard, long> applied in effect.AppliedEffectIds)
            {
                applied.Key?.RemoveContinuousEffect(applied.Value);
            }
            effect.AppliedEffectIds.Clear();
            appliedEffectIds.Add(effect.Id);
        }

        int removed = eventPipeline.RemoveLinked(source, linkId);
        if (appliedEffectIds.Count > 0)
        {
            RecalculateBattlefieldContinuousEffects();
            PerformStateBasedActions();
        }
        return removed;
    }

    public GameEventContext PublishGameEvent(GameEventContext gameEvent)
    {
        GameEventContext completed = eventPipeline.Publish(
            gameEvent,
            IsRuntimeEffectSourceActive,
            InvokeScriptReplacement,
            ShouldApplyScriptReplacement,
            SelectScriptReplacement,
            ShouldQueueScriptTrigger,
            QueueScriptTrigger);
        RecordActionEvent(completed);
        return completed;
    }

    private GameEventContext PrepareGameEvent(GameEventContext gameEvent)
    {
        GameEventContext prepared = eventPipeline.Prepare(
            gameEvent,
            IsRuntimeEffectSourceActive,
            InvokeScriptReplacement,
            ShouldApplyScriptReplacement,
            SelectScriptReplacement);
        if (prepared?.prevented == true)
        {
            RecordActionEvent(prepared);
        }
        return prepared;
    }

    private void PrepareGameEventWithChoice(
        GameEventContext gameEvent,
        System.Action<GameEventContext> onPrepared)
    {
        if (gameEvent == null || onPrepared == null) return;
        eventPipeline.InitializeEvent(gameEvent);
        ContinueReplacementSelection(gameEvent, onPrepared);
    }

    private void ContinueReplacementSelection(
        GameEventContext gameEvent,
        System.Action<GameEventContext> onPrepared)
    {
        IReadOnlyList<ScriptReplacementRegistration> applicableScripts =
            eventPipeline.GetApplicableReplacements(
                gameEvent,
                IsRuntimeEffectSourceActive,
                ShouldApplyScriptReplacement);
        var candidates = new List<ReplacementCandidate>();
        for (int i = 0; i < applicableScripts.Count; i++)
        {
            ScriptReplacementRegistration replacement = applicableScripts[i];
            string sourceName = replacement.Source?.GetCardName();
            candidates.Add(new ReplacementCandidate
            {
                Kind = ReplacementCandidateKind.Script,
                Script = replacement,
                Timestamp = replacement.Timestamp,
                Label = string.IsNullOrWhiteSpace(sourceName)
                    ? replacement.Operation.ToString()
                    : $"{sourceName}: {replacement.Operation}"
            });
        }

        bool mayIncludeGeneralDamageReplacements =
            gameEvent.type == GameEventType.Damage
            && (applicableScripts.Count == 0
                || applicableScripts[0].Category == ReplacementEffectCategory.General);
        if (mayIncludeGeneralDamageReplacements)
        {
            AddDamageReplacementCandidates(gameEvent, candidates);
        }

        if (candidates.Count == 0)
        {
            if (gameEvent.prevented) RecordActionEvent(gameEvent);
            onPrepared(gameEvent);
            return;
        }

        candidates.Sort((left, right) =>
        {
            int timestampOrder = left.Timestamp.CompareTo(right.Timestamp);
            return timestampOrder != 0
                ? timestampOrder
                : left.Kind.CompareTo(right.Kind);
        });

        void ApplyAndRecheck(ReplacementCandidate candidate)
        {
            ApplyReplacementCandidate(candidate, gameEvent);
            ContinueReplacementSelection(gameEvent, onPrepared);
        }

        int choosingPlayer = GetReplacementChoosingPlayer(gameEvent);
        if (candidates.Count == 1
            || choosingPlayer != 1
            || activeChoice != null)
        {
            ApplyAndRecheck(candidates[0]);
            return;
        }

        var options = new List<GameChoiceOption>(candidates.Count);
        for (int i = 0; i < candidates.Count; i++)
        {
            options.Add(new GameChoiceOption(
                4100000 + i,
                GameChoiceZone.None,
                choosingPlayer,
                i,
                true,
                candidates[i].Label));
        }

        GameChoiceRequest request = BeginOptionChoice(
            GameChoiceKind.ReplacementEffect,
            choosingPlayer,
            "选择要先应用的替代效应",
            1,
            1,
            false,
            options,
            result =>
            {
                int selectedIndex = result.selectedOptionIds.Count > 0
                    ? result.selectedOptionIds[0] - 4100000
                    : 0;
                if (selectedIndex < 0 || selectedIndex >= candidates.Count)
                {
                    selectedIndex = 0;
                }
                ApplyAndRecheck(candidates[selectedIndex]);
            });
        if (request == null)
        {
            ApplyAndRecheck(candidates[0]);
        }
    }

    private void AddDamageReplacementCandidates(
        GameEventContext gameEvent,
        List<ReplacementCandidate> candidates)
    {
        if (gameEvent == null || candidates == null || gameEvent.amount <= 0) return;

        damageReplacements.RemoveAll(replacement =>
            replacement == null
            || (replacement.EffectSource != null
                && (replacement.EffectSource.ZoneChangeVersion
                    != replacement.EffectSourceZoneChangeVersion
                    || !IsOnBattlefield(replacement.EffectSource))));

        var damageEvent = new DamageEventRuntime(
            gameEvent.source,
            gameEvent.card,
            gameEvent.playerId,
            gameEvent.amount,
            gameEvent.damageKind);
        for (int i = 0; i < damageReplacements.Count; i++)
        {
            DamageReplacementRuntime replacement = damageReplacements[i];
            if (gameEvent.HasAppliedDamageReplacement(replacement.Id)
                || !replacement.AppliesTo(damageEvent, IsOnBattlefield))
            {
                continue;
            }

            string sourceName = replacement.EffectSource?.GetCardName();
            candidates.Add(new ReplacementCandidate
            {
                Kind = ReplacementCandidateKind.RegisteredDamage,
                Damage = replacement,
                Timestamp = replacement.Timestamp,
                Label = string.IsNullOrWhiteSpace(sourceName)
                    ? replacement.Operation.ToString()
                    : $"{sourceName}: {replacement.Operation}"
            });
        }

        if (!gameEvent.protectionPreventionApplied
            && gameEvent.card != null
            && TargetingRules.IsProtectedFrom(gameEvent.card, gameEvent.source))
        {
            candidates.Add(new ReplacementCandidate
            {
                Kind = ReplacementCandidateKind.ProtectionPrevention,
                Timestamp = long.MaxValue - 2,
                Label = $"{gameEvent.card.GetCardName()}: Protection"
            });
        }
        if (!gameEvent.sourcePreventionApplied
            && gameEvent.source != null
            && gameEvent.source.HasOutgoingDamagePrevention)
        {
            candidates.Add(new ReplacementCandidate
            {
                Kind = ReplacementCandidateKind.SourcePrevention,
                Timestamp = long.MaxValue - 1,
                Label = $"{gameEvent.source.GetCardName()}: Prevent outgoing damage"
            });
        }
        bool hasTargetPrevention = gameEvent.card != null
            ? gameEvent.card.HasIncomingDamagePrevention
            : HasPlayerDamagePrevention(gameEvent.playerId);
        if (!gameEvent.targetPreventionApplied && hasTargetPrevention)
        {
            candidates.Add(new ReplacementCandidate
            {
                Kind = ReplacementCandidateKind.TargetPrevention,
                Timestamp = long.MaxValue,
                Label = gameEvent.card != null
                    ? $"{gameEvent.card.GetCardName()}: Prevent incoming damage"
                    : $"Player {gameEvent.playerId}: Prevent damage"
            });
        }
    }

    private void ApplyReplacementCandidate(
        ReplacementCandidate candidate,
        GameEventContext gameEvent)
    {
        if (candidate == null || gameEvent == null) return;

        switch (candidate.Kind)
        {
            case ReplacementCandidateKind.Script:
                eventPipeline.ApplyReplacementToEvent(
                    candidate.Script,
                    gameEvent,
                    InvokeScriptReplacement);
                return;
            case ReplacementCandidateKind.RegisteredDamage:
                ApplyRegisteredDamageReplacement(candidate.Damage, gameEvent);
                return;
            case ReplacementCandidateKind.ProtectionPrevention:
            {
                gameEvent.protectionPreventionApplied = true;
                int prevented = gameEvent.amount;
                gameEvent.amount = 0;
                gameEvent.prevented = true;
                gameEvent.replaced = true;
                Debug.Log(
                    $"{gameEvent.card?.GetCardName() ?? "Protected object"}'s " +
                    $"protection prevents {prevented} damage.");
                GameEvents.TriggerDamagePrevented(
                    gameEvent.source,
                    gameEvent.card,
                    prevented);
                return;
            }
            case ReplacementCandidateKind.SourcePrevention:
            {
                gameEvent.sourcePreventionApplied = true;
                int prevented = gameEvent.source?.PreventOutgoingDamage(gameEvent.amount) ?? 0;
                gameEvent.amount = Mathf.Max(0, gameEvent.amount - prevented);
                gameEvent.prevented |= gameEvent.amount == 0;
                gameEvent.replaced |= prevented > 0;
                if (prevented > 0)
                {
                    GameEvents.TriggerDamagePrevented(
                        gameEvent.source,
                        gameEvent.card,
                        prevented);
                }
                return;
            }
            case ReplacementCandidateKind.TargetPrevention:
            {
                gameEvent.targetPreventionApplied = true;
                int prevented = gameEvent.card != null
                    ? gameEvent.card.PreventIncomingDamage(gameEvent.amount)
                    : ConsumePlayerDamagePrevention(
                        gameEvent.playerId,
                        gameEvent.amount);
                gameEvent.amount = Mathf.Max(0, gameEvent.amount - prevented);
                gameEvent.prevented |= gameEvent.amount == 0;
                gameEvent.replaced |= prevented > 0;
                if (prevented > 0)
                {
                    GameEvents.TriggerDamagePrevented(
                        gameEvent.source,
                        gameEvent.card,
                        prevented);
                }
                return;
            }
        }
    }

    private static void ApplyRegisteredDamageReplacement(
        DamageReplacementRuntime replacement,
        GameEventContext gameEvent)
    {
        if (replacement == null
            || gameEvent == null
            || gameEvent.HasAppliedDamageReplacement(replacement.Id))
        {
            return;
        }

        gameEvent.MarkDamageReplacementApplied(replacement.Id);
        switch (replacement.Operation)
        {
            case DamageReplacementOperation.Prevent:
                gameEvent.amount = replacement.Value < 0
                    ? 0
                    : Mathf.Max(0, gameEvent.amount - replacement.Value);
                break;
            case DamageReplacementOperation.Add:
                gameEvent.amount = Mathf.Max(0, gameEvent.amount + replacement.Value);
                break;
            case DamageReplacementOperation.Multiply:
                gameEvent.amount = Mathf.Max(
                    0,
                    gameEvent.amount * Mathf.Max(0, replacement.Value));
                break;
            case DamageReplacementOperation.Set:
                gameEvent.amount = Mathf.Max(0, replacement.Value);
                break;
        }
        gameEvent.prevented |= gameEvent.amount == 0;
        gameEvent.replaced = true;
    }

    private bool HasPlayerDamagePrevention(int player)
    {
        return player == 1
            ? player1DamagePreventionRemaining != 0
            : player == 2 && player2DamagePreventionRemaining != 0;
    }

    private static int GetReplacementChoosingPlayer(GameEventContext gameEvent)
    {
        if (gameEvent?.card != null
            && (gameEvent.card.ControllerPlayerID == 1
                || gameEvent.card.ControllerPlayerID == 2))
        {
            return gameEvent.card.ControllerPlayerID;
        }
        return gameEvent != null && gameEvent.playerId == 2 ? 2 : 1;
    }

    private GameEventContext CompleteGameEvent(GameEventContext gameEvent)
    {
        GameEventContext completed = eventPipeline.Complete(
            gameEvent,
            IsRuntimeEffectSourceActive,
            ShouldQueueScriptTrigger,
            QueueScriptTrigger);
        RecordActionEvent(completed);
        return completed;
    }

    private void RecordActionEvent(GameEventContext gameEvent)
    {
        if (!applyingGameAction
            || gameEvent == null
            || gameEvent.eventId <= 0
            || !recordedActionEventIds.Add(gameEvent.eventId))
        {
            return;
        }

        currentActionEvents.Add(GameEventRecord.FromContext(gameEvent));
    }

    private void InvokeScriptReplacement(
        ScriptReplacementRegistration replacement,
        GameEventContext gameEvent)
    {
        if (replacement?.Source == null || string.IsNullOrEmpty(replacement.HookName)) return;
        GameEventContext previousContext = replacement.Source.ResolvingGameEvent;
        replacement.Source.SetResolvingGameEvent(gameEvent);
        try { CardEffectExecutor.ExecuteTriggeredHook(this, replacement.Source, replacement.HookName); }
        finally { replacement.Source.SetResolvingGameEvent(previousContext); }
    }

    private bool ShouldApplyScriptReplacement(
        ScriptReplacementRegistration replacement,
        GameEventContext gameEvent)
    {
        if (replacement?.Source == null) return false;
        if (string.IsNullOrEmpty(replacement.ConditionHookName)) return true;

        RuntimeCard source = replacement.Source;
        GameEventContext previousContext = source.ResolvingGameEvent;
        source.SetResolvingGameEvent(gameEvent);
        try
        {
            return CardEffectExecutor.EvaluateCardScriptPredicate(this, source, replacement.ConditionHookName, false);
        }
        finally { source.SetResolvingGameEvent(previousContext); }
    }

    private static ScriptReplacementRegistration SelectScriptReplacement(
        GameEventContext gameEvent,
        IReadOnlyList<ScriptReplacementRegistration> applicable)
    {
        if (applicable == null || applicable.Count == 0) return null;

        // CR 616's player-facing ordering UI is layered over this policy hook.
        // Until a choice is requested, timestamp order is the deterministic default.
        ScriptReplacementRegistration selected = applicable[0];
        for (int i = 1; i < applicable.Count; i++)
        {
            if (applicable[i].Timestamp < selected.Timestamp)
            {
                selected = applicable[i];
            }
        }
        return selected;
    }

    private bool ShouldQueueScriptTrigger(
        ScriptTriggerRegistration trigger,
        GameEventContext gameEvent)
    {
        if (trigger?.Source == null) return false;
        GameEventContext previousContext = trigger.Source.ResolvingGameEvent;
        trigger.Source.SetResolvingGameEvent(gameEvent);
        try
        {
            return string.IsNullOrEmpty(trigger.ConditionHookName)
                || CardEffectExecutor.EvaluateCardScriptPredicate(this, trigger.Source, trigger.ConditionHookName);
        }
        finally { trigger.Source.SetResolvingGameEvent(previousContext); }
    }

    private void QueueScriptTrigger(
        ScriptTriggerRegistration trigger,
        GameEventContext gameEvent)
    {
        if (trigger?.Source == null) return;
        pendingTriggeredAbilities.Add(
            GameStackItem.TriggeredAbility(trigger.Source, trigger.HookName, gameEvent));
    }

    private void HandleStateBasedZoneChange(
        RuntimeCard card,
        GameZone fromZone,
        GameZone toZone)
    {
        if (card == null) return;
        card.SetCurrentZone(toZone);
        PublishGameEvent(new GameEventContext
        {
            type = card.HasType(CardType.Creature)
                ? GameEventType.CardDied
                : GameEventType.ZoneChanged,
            source = card,
            card = card,
            playerId = card.OwnerPlayerID,
            controllerId = card.ControllerPlayerID,
            fromZone = fromZone,
            toZone = toZone
        });
    }

    private List<RuntimeCard> GetExileList(int playerID)
    {
        if (playerID == 1) return player1Exile;
        if (playerID == 2) return player2Exile;
        return null;
    }

    private bool RemoveRuntimeCardFromKnownZones(RuntimeCard card)
    {
        bool removed = false;
        removed |= player1Hand.Remove(card);
        removed |= player2Hand.Remove(card);
        removed |= player1Battlefield.Remove(card);
        removed |= player2Battlefield.Remove(card);
        removed |= player1Graveyard.Remove(card);
        removed |= player2Graveyard.Remove(card);
        removed |= player1Exile.Remove(card);
        removed |= player2Exile.Remove(card);
        removed |= player1Library.Remove(card);
        removed |= player2Library.Remove(card);
        removed |= player1Command.Remove(card);
        removed |= player2Command.Remove(card);
        bool removedFromStack = false;
        for (int i = stack.Count - 1; i >= 0; i--)
        {
            GameStackItem item = stack[i];
            if (item == null
                || item.Kind != GameStackItemKind.Spell
                || !ReferenceEquals(item.SourceCard, card))
            {
                continue;
            }

            stack.RemoveAt(i);
            removedFromStack = true;
        }
        removed |= removedFromStack;
        if (removedFromStack) NotifyStackChanged();
        return removed;
    }

    private static bool RemoveCeasedTokens(List<RuntimeCard> zone)
    {
        if (zone == null) return false;

        bool removed = false;
        for (int i = zone.Count - 1; i >= 0; i--)
        {
            RuntimeCard card = zone[i];
            if (card == null || !card.IsToken) continue;

            zone.RemoveAt(i);
            card.OnDestroy();
            removed = true;
        }

        return removed;
    }

    public long RegisterDamageReplacement(
        RuntimeCard effectSource,
        DamageReplacementScope scope,
        DamageReplacementOperation operation,
        RuntimeCard scopeCard,
        int scopePlayer,
        int value,
        bool combatOnly,
        bool untilEndOfTurn)
    {
        if (effectSource == null || !IsOnBattlefield(effectSource)) return 0;
        if ((scope == DamageReplacementScope.SourceDealsDamage
                || scope == DamageReplacementScope.CardIsDealtDamage)
            && scopeCard == null)
        {
            return 0;
        }
        if (scope == DamageReplacementScope.PlayerIsDealtDamage
            && scopePlayer != 1
            && scopePlayer != 2)
        {
            return 0;
        }

        long id = nextDamageReplacementId++;
        damageReplacements.Add(new DamageReplacementRuntime(
            id,
            nextDamageReplacementTimestamp++,
            effectSource,
            scope,
            operation,
            scopeCard,
            scopePlayer,
            value,
            combatOnly,
            untilEndOfTurn));
        return id;
    }

    public bool RemoveDamageReplacement(long replacementId)
    {
        int index = damageReplacements.FindIndex(replacement =>
            replacement != null && replacement.Id == replacementId);
        if (index < 0) return false;
        damageReplacements.RemoveAt(index);
        return true;
    }

    public void GainLife(int player, int amount)
    {
        ChangeLife(player, amount, "gain");
    }

    public void LoseLife(int player, int amount)
    {
        if (amount <= 0) return;
        ChangeLife(player, -amount, "loss");
    }

    private void ChangeLife(int player, int amount, string detail)
    {
        if ((player != 1 && player != 2) || amount == 0) return;
        var gameEvent = new GameEventContext
        {
            type = GameEventType.LifeChanged,
            playerId = player,
            controllerId = player,
            amount = amount,
            detail = detail
        };
        PrepareGameEventWithChoice(gameEvent, prepared =>
        {
            int resolvedAmount = prepared.amount;
            if (prepared.prevented || resolvedAmount == 0) return;

            if (player == 1)
            {
                player1Life += resolvedAmount;
                Debug.Log($"Player 1 life changes by {resolvedAmount}. Life total: {player1Life}");
            }
            else if (player == 2)
            {
                player2Life += resolvedAmount;
                Debug.Log($"Player 2 life changes by {resolvedAmount}. Life total: {player2Life}");
            }
            CompleteGameEvent(prepared);
            PerformStateBasedActions();
        });
    }

    public void AddPoisonCounters(int player, int amount)
    {
        if (IsGameOver() || amount <= 0) return;
        if (player != 1 && player != 2) return;

        var gameEvent = new GameEventContext
        {
            type = GameEventType.CounterChanged,
            playerId = player,
            controllerId = player,
            amount = amount,
            detail = "poison"
        };
        PrepareGameEventWithChoice(gameEvent, prepared =>
        {
            if (prepared.prevented || prepared.amount <= 0) return;
            if (player == 1) player1PoisonCounters += prepared.amount;
            else player2PoisonCounters += prepared.amount;
            CompleteGameEvent(prepared);
            Debug.Log($"Player {player} gets {prepared.amount} poison counter(s). Total: {GetPoisonCounters(player)}");
            PerformStateBasedActions();
        });
    }

    public int GetLoyaltyCounters(RuntimeCard card)
    {
        return card != null ? card.CurrentLoyalty : 0;
    }

    public bool AddLoyaltyCounters(RuntimeCard card, int amount)
    {
        if (IsGameOver()
            || card == null
            || card.Data == null
            || !card.HasType(CardType.Planeswalker)
            || !IsCardOnBattlefield(card)
            || amount == 0)
        {
            return false;
        }

        PrepareCounterChange(card, "loyalty", amount, resolvedAmount =>
        {
            card.AddLoyaltyCounters(resolvedAmount);
            Debug.Log($"{card.Data.cardName} loyalty is now {card.CurrentLoyalty}.");
        });
        return true;
    }

    public int GetCounters(RuntimeCard card, string counterName)
    {
        return card != null ? card.GetCounters(counterName) : 0;
    }

    public bool AddCounters(RuntimeCard card, string counterName, int amount)
    {
        if (IsGameOver()
            || card == null
            || string.IsNullOrWhiteSpace(counterName)
            || !IsCardOnBattlefield(card)
            || amount == 0)
        {
            return false;
        }

        string normalizedName = counterName.Trim();
        PrepareCounterChange(card, normalizedName, amount, resolvedAmount =>
        {
            if (normalizedName.Equals("lore", System.StringComparison.OrdinalIgnoreCase)
                && HasSagaChapters(card))
            {
                CommitSagaLoreCounters(card, resolvedAmount);
            }
            else
            {
                int total;
                if (normalizedName.Equals("defense", System.StringComparison.OrdinalIgnoreCase)
                    || normalizedName.Equals("defence", System.StringComparison.OrdinalIgnoreCase))
                {
                    ChangeBattleDefenseCounters(card, resolvedAmount);
                    total = card.CurrentBattleDefense;
                }
                else
                {
                    total = card.AddCounters(normalizedName, resolvedAmount);
                }
                Debug.Log($"{card.Data.cardName} now has {total} {normalizedName} counter(s).");
            }
        });
        return true;
    }

    private void PrepareCounterChange(
        RuntimeCard card,
        string counterName,
        int amount,
        System.Action<int> apply)
    {
        var gameEvent = new GameEventContext
        {
            type = GameEventType.CounterChanged,
            source = card,
            card = card,
            playerId = card.ControllerPlayerID,
            controllerId = card.ControllerPlayerID,
            amount = amount,
            detail = counterName
        };
        PrepareGameEventWithChoice(gameEvent, prepared =>
        {
            if (prepared.prevented || prepared.amount == 0) return;
            if (!IsOnBattlefield(card)) return;
            apply?.Invoke(prepared.amount);
            CompleteGameEvent(prepared);
            PerformStateBasedActions();
        });
    }

    public void PreventNextDamageToCard(RuntimeCard card, int amount)
    {
        if (card == null) return;
        if (amount <= 0)
        {
            PreventAllDamageToCard(card);
            return;
        }

        card.AddDamagePrevention(amount);
        Debug.Log($"{card.Data.cardName} will prevent the next {amount} damage.");
    }

    public void PreventAllDamageToCard(RuntimeCard card)
    {
        if (card == null) return;

        card.AddPreventAllDamage();
        Debug.Log($"{card.Data.cardName} will prevent all damage this turn.");
    }

    public void PreventNextDamageToPlayer(int player, int amount)
    {
        if (player != 1 && player != 2) return;
        if (amount <= 0)
        {
            PreventAllDamageToPlayer(player);
            return;
        }

        if (player == 1 && player1DamagePreventionRemaining != PreventAllDamage)
        {
            player1DamagePreventionRemaining += amount;
        }
        else if (player == 2 && player2DamagePreventionRemaining != PreventAllDamage)
        {
            player2DamagePreventionRemaining += amount;
        }

        Debug.Log($"Player {player} will prevent the next {amount} damage.");
    }

    public void PreventAllDamageToPlayer(int player)
    {
        if (player == 1) player1DamagePreventionRemaining = PreventAllDamage;
        else if (player == 2) player2DamagePreventionRemaining = PreventAllDamage;
        else return;

        Debug.Log($"Player {player} will prevent all damage this turn.");
    }

    public void PreventNextDamageBySource(RuntimeCard source, int amount)
    {
        if (source == null) return;
        if (amount <= 0)
        {
            PreventAllDamageBySource(source);
            return;
        }

        source.AddSourceDamagePrevention(amount);
        Debug.Log($"{source.Data.cardName}'s next {amount} damage will be prevented.");
    }

    public void PreventAllDamageBySource(RuntimeCard source)
    {
        if (source == null) return;

        source.AddPreventAllDamageByThisSource();
        Debug.Log($"{source.Data.cardName}'s damage will be prevented this turn.");
    }

    public void AddRegenerationShield(RuntimeCard card)
    {
        if (card == null || !card.HasType(CardType.Creature)) return;

        card.AddRegenerationShield();
        Debug.Log($"{card.Data.cardName} gains a regeneration shield.");
    }

    public void ForceDrawCard(int playerID, int count)
    {
        if (playerID == 1)
        {
            DrawCards(player1Library, player1Hand, count, 1);
        }
        else if (playerID == 2)
        {
            DrawCards(player2Library, player2Hand, count, 2);
        }
        Debug.Log($"Player {playerID} drew {count} card(s).");
    }

    private void OnDestroy()
    {
        GameEvents.OnCardPlayed -= HandleCardPlayedLua;
        GameEvents.OnCreatureDied -= HandleCreatureDiedLua;
        GameEvents.OnCardTapped -= HandleCardTappedEvent;
        GameEvents.OnCardUntapped -= HandleCardUntappedEvent;
        GameEvents.OnPhaseChanged -= HandlePhaseChangedEvent;
        GameEvents.OnAttackDeclared -= HandleAttackDeclaredEvent;
        GameEvents.OnBlockDeclared -= HandleBlockDeclaredEvent;
        DisposeRuntimeCards();
    }

    private void DisposeRuntimeCards()
    {
        var disposed = new HashSet<RuntimeCard>();
        DisposeRuntimeCards(player1Hand, disposed);
        DisposeRuntimeCards(player2Hand, disposed);
        DisposeRuntimeCards(player1Battlefield, disposed);
        DisposeRuntimeCards(player2Battlefield, disposed);
        DisposeRuntimeCards(player1Graveyard, disposed);
        DisposeRuntimeCards(player2Graveyard, disposed);
        DisposeRuntimeCards(player1Exile, disposed);
        DisposeRuntimeCards(player2Exile, disposed);
        DisposeRuntimeCards(player1Library, disposed);
        DisposeRuntimeCards(player2Library, disposed);
        DisposeRuntimeCards(player1Command, disposed);
        DisposeRuntimeCards(player2Command, disposed);
        DisposeRuntimeCards(stackCardView, disposed);
        for (int i = 0; i < stack.Count; i++)
        {
            RuntimeCard card = stack[i]?.SourceCard;
            if (card != null && disposed.Add(card)) card.OnDestroy();
        }
    }

    private static void DisposeRuntimeCards(
        IReadOnlyList<RuntimeCard> cards,
        HashSet<RuntimeCard> disposed)
    {
        if (cards == null) return;
        for (int i = 0; i < cards.Count; i++)
        {
            RuntimeCard card = cards[i];
            if (card != null && disposed.Add(card)) card.OnDestroy();
        }
    }
}

public enum TurnPhase
{
    Untap,
    Upkeep,
    Draw,
    Main1,
    BeginningOfCombat,
    DeclareAttackers,
    DeclareBlockers,
    CombatDamage,
    EndOfCombat,
    Main2,
    End,
    Cleanup
}
