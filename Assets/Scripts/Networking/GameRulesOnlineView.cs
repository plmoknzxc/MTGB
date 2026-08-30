using System.Collections.Generic;
using UnityEngine;

public partial class GameRules
{
    private bool onlineViewOnly;
    private bool onlineAuthoritySession;
    private bool onlineCanConfirmActiveChoice;
    private string onlineViewHash = string.Empty;

    public void InitializeForOnlineSession(
        DeckDefinition first,
        DeckDefinition second,
        int seed,
        int startingLife = 20,
        int firstPlayer = 1)
    {
        onlineViewOnly = false;
        onlineAuthoritySession = true;
        onlineViewHash = string.Empty;
        MatchStartConfig.SetDecks(first, second);
        MatchStartConfig.SetDeterministicSeed(seed);
        MatchStartConfig.SetOnlineRules(startingLife, firstPlayer);
        InitializeGame();
    }

    public GameReplayResult RestoreOnlineSession(
        DeckDefinition first,
        DeckDefinition second,
        int seed,
        GameReplay sourceReplay,
        int startingLife = 20,
        int firstPlayer = 1)
    {
        onlineViewOnly = false;
        onlineAuthoritySession = true;
        onlineViewHash = string.Empty;
        MatchStartConfig.SetDecks(first, second);
        MatchStartConfig.SetDeterministicSeed(seed);
        MatchStartConfig.SetOnlineRules(startingLife, firstPlayer);
        if (sourceReplay == null || sourceReplay.actions == null || sourceReplay.actions.Count == 0)
        {
            InitializeGame();
            return new GameReplayResult { finalStateHash = ComputeDeterministicStateHash() };
        }
        return ReplayFromInitialState(sourceReplay);
    }

    public OnlinePlayerViewState CreateOnlinePlayerView(int viewerSeat)
    {
        if (viewerSeat < 0 || viewerSeat > 2) return null;
        bool revealViewerLibrary = activeChoice != null
            && activeChoice.kind == GameChoiceKind.LibrarySearch
            && activeChoice.choosingPlayerId == viewerSeat;
        var view = new OnlinePlayerViewState
        {
            viewerSeat = viewerSeat,
            appliedSequence = GetAppliedActionSequence(),
            startingPlayer = startingPlayer,
            openingMulligan = openingMulligan,
            player1MulliganCount = openingMulliganCounts[1],
            player2MulliganCount = openingMulliganCounts[2],
            currentPlayer = currentPlayer,
            currentPhase = currentPhase,
            turnNumber = turnNumber,
            awaitingPriority = priorityState.IsOpen,
            priorityPlayer = priorityState.PlayerWithPriority,
            fullControl = IsFullControl(viewerSeat),
            player1Life = player1Life,
            player2Life = player2Life,
            player1Poison = player1PoisonCounters,
            player2Poison = player2PoisonCounters,
            player1MaxHandSize = player1MaxHandSize,
            player2MaxHandSize = player2MaxHandSize,
            player1Mana = player1ManaPool,
            player2Mana = player2ManaPool,
            outcome = matchOutcome,
            hasActiveChoice = activeChoice != null,
            canConfirmActiveChoice = activeChoice != null
                && activeChoice.choosingPlayerId == viewerSeat
                && CanConfirmActiveChoice(),
            activeChoice = CreateChoiceView(viewerSeat)
        };

        CopyZone(player1Library, view.player1Library, viewerSeat, revealViewerLibrary && viewerSeat == 1);
        CopyZone(player2Library, view.player2Library, viewerSeat, revealViewerLibrary && viewerSeat == 2);
        CopyZone(player1Hand, view.player1Hand, viewerSeat, viewerSeat == 1);
        CopyZone(player2Hand, view.player2Hand, viewerSeat, viewerSeat == 2);
        CopyZone(player1Battlefield, view.player1Battlefield, viewerSeat, true);
        CopyZone(player2Battlefield, view.player2Battlefield, viewerSeat, true);
        CopyZone(player1Graveyard, view.player1Graveyard, viewerSeat, true);
        CopyZone(player2Graveyard, view.player2Graveyard, viewerSeat, true);
        CopyZone(player1Exile, view.player1Exile, viewerSeat, true);
        CopyZone(player2Exile, view.player2Exile, viewerSeat, true);
        CopyZone(player1Command, view.player1Command, viewerSeat, true);
        CopyZone(player2Command, view.player2Command, viewerSeat, true);
        CopyZone(stackCardView, view.stack, viewerSeat, true);
        view.SealHash();
        return view;
    }

    public bool ApplyOnlinePlayerView(OnlinePlayerViewState view, out string error)
    {
        error = string.Empty;
        if (view == null || view.viewerSeat < 0 || view.viewerSeat > 2)
        {
            error = "Player view is missing or has an invalid seat.";
            return false;
        }
        string expectedHash = view.viewHash;
        view.SealHash();
        if (!string.Equals(expectedHash, view.viewHash, System.StringComparison.Ordinal))
        {
            error = "Player view hash mismatch.";
            return false;
        }

        DisposeRuntimeCards();
        ClearOnlineViewZones();
        BuildOnlineViewZone(view.player1Library, player1Library);
        BuildOnlineViewZone(view.player2Library, player2Library);
        BuildOnlineViewZone(view.player1Hand, player1Hand);
        BuildOnlineViewZone(view.player2Hand, player2Hand);
        BuildOnlineViewZone(view.player1Battlefield, player1Battlefield);
        BuildOnlineViewZone(view.player2Battlefield, player2Battlefield);
        BuildOnlineViewZone(view.player1Graveyard, player1Graveyard);
        BuildOnlineViewZone(view.player2Graveyard, player2Graveyard);
        BuildOnlineViewZone(view.player1Exile, player1Exile);
        BuildOnlineViewZone(view.player2Exile, player2Exile);
        BuildOnlineViewZone(view.player1Command, player1Command);
        BuildOnlineViewZone(view.player2Command, player2Command);
        BuildOnlineViewZone(view.stack, stackCardView);
        ResolveOnlineCombatReferences(view);

        startingPlayer = view.startingPlayer;
        openingMulligan = view.openingMulligan;
        openingMulliganCounts[1] = view.player1MulliganCount;
        openingMulliganCounts[2] = view.player2MulliganCount;
        currentPlayer = view.currentPlayer;
        currentPhase = view.currentPhase;
        turnNumber = view.turnNumber;
        priorityState.Restore(view.awaitingPriority, view.priorityPlayer);
        player1FullControl = view.viewerSeat == 1 && view.fullControl;
        player2FullControl = view.viewerSeat == 2 && view.fullControl;
        player1Life = view.player1Life;
        player2Life = view.player2Life;
        player1PoisonCounters = view.player1Poison;
        player2PoisonCounters = view.player2Poison;
        player1MaxHandSize = view.player1MaxHandSize;
        player2MaxHandSize = view.player2MaxHandSize;
        player1ManaPool = view.player1Mana;
        player2ManaPool = view.player2Mana;
        matchOutcome = view.outcome;
        activeChoice = view.hasActiveChoice ? CloneChoice(view.activeChoice) : null;
        onlineCanConfirmActiveChoice = view.canConfirmActiveChoice;
        nextActionSequence = view.appliedSequence + 1;
        initialized = true;
        onlineViewOnly = true;
        onlineAuthoritySession = false;
        onlineViewHash = expectedHash;
        return true;
    }

    private GameChoiceRequest CreateChoiceView(int viewerSeat)
    {
        if (activeChoice == null) return null;
        if (activeChoice.choosingPlayerId == viewerSeat) return CloneChoice(activeChoice);
        return new GameChoiceRequest
        {
            requestId = activeChoice.requestId,
            revision = activeChoice.revision,
            kind = activeChoice.kind,
            choosingPlayerId = activeChoice.choosingPlayerId,
            prompt = $"Waiting for Player {activeChoice.choosingPlayerId} to choose.",
            minimumSelections = 0,
            maximumSelections = 0
        };
    }

    private void CopyZone(
        IReadOnlyList<RuntimeCard> source,
        List<OnlineVisibleCard> destination,
        int viewerSeat,
        bool reveal)
    {
        if (source == null) return;
        for (int i = 0; i < source.Count; i++)
        {
            RuntimeCard card = source[i];
            if (card == null) continue;
            destination.Add(CreateCardView(card, reveal, i, viewerSeat));
        }
    }

    private OnlineVisibleCard CreateCardView(RuntimeCard card, bool reveal, int index, int viewerSeat)
    {
        bool revealIdentity = reveal && !card.IsOnlineIdentityHiddenFrom(viewerSeat);
        var result = new OnlineVisibleCard
        {
            hidden = !revealIdentity,
            publicObject = reveal,
            objectId = reveal
                ? card.ObjectId
                : -((GetAppliedActionSequence() + 1) * 100000L + card.OwnerPlayerID * 10000L + index + 1),
            zoneChangeVersion = reveal ? card.ZoneChangeVersion : 0,
            ownerPlayerId = card.OwnerPlayerID,
            controllerPlayerId = reveal ? card.ControllerPlayerID : card.OwnerPlayerID,
            zone = card.CurrentZone
        };
        if (!reveal) return result;

        if (revealIdentity)
        {
            result.setCode = card.Data?.setCode ?? string.Empty;
            result.collectorNumber = card.Data?.CollectorNumber ?? string.Empty;
            result.cardId = card.Data?.cardID ?? 0;
        }
        result.currentFaceIndex = card.CurrentFaceIndex;
        result.currentAttack = card.CurrentAttack;
        result.currentDefence = card.CurrentDefence;
        result.currentLoyalty = card.CurrentLoyalty;
        result.currentBattleDefense = card.CurrentBattleDefense;
        result.battleProtectorPlayerId = card.BattleProtectorPlayerID;
        result.damageTaken = card.DamageTaken;
        result.visibleCardName = card.GetCardName();
        result.visibleRulesText = card.GetRulesText();
        result.visibleManaCost = card.GetManaCost().ToSymbolString();
        result.visibleCardTypes = card.GetCardTypes();
        result.visibleSupertypes = card.GetSupertypes();
        result.visibleSubtypes = card.GetSubtypes();
        result.visibleColors = card.GetColors();
        result.classLevel = card.ClassLevel;
        result.isCaseSolved = card.IsCaseSolved;
        result.canCastPrimaryFromAdventureExile = card.CanCastPrimaryFromAdventureExile;
        result.isPrepared = card.IsPrepared;
        result.isSpellCopy = card.IsSpellCopy;
        result.isToken = card.IsToken;
        result.isEmblem = card.IsEmblem;
        bool viewerControlsCard = viewerSeat > 0
            && revealIdentity
            && card.ControllerPlayerID == viewerSeat;
        if (viewerControlsCard)
        {
            result.canPlayFromHand = card.CurrentZone == GameZone.Hand
                && CanPlayCard(viewerSeat, index);
            if (card.CurrentZone == GameZone.Exile)
            {
                CardFeatureRuntime previousFeature = card.SpellFeature;
                result.canCastFromExile = CanCastExileCard(viewerSeat, index);
                if (previousFeature != null) card.SetSpellFeature(previousFeature);
                else card.ClearSpellFeature();
            }
            result.canActivateAbility = card.CurrentZone == GameZone.Battlefield
                && CanActivateAnyAbility(card);
            result.canActivateManaAbility = card.CurrentZone == GameZone.Battlefield
                && CanActivateManaAbility(card);
            if (card.CurrentZone == GameZone.Battlefield)
            {
                result.canDeclareAttacker = CanDeclareAttacker(viewerSeat, index);
                result.pendingAttacker = pendingAttackers.Contains(card);
                if (result.pendingAttacker)
                {
                    pendingAttackTargets.TryGetValue(card, out object pendingTarget);
                    if (pendingTarget is int pendingPlayer)
                        result.pendingAttackTargetPlayerId = pendingPlayer;
                    else if (pendingTarget is RuntimeCard pendingPermanent)
                        result.pendingAttackTargetObjectId = pendingPermanent.ObjectId;

                    int defendingPlayer = GetDefendingPlayer(card.ControllerPlayerID);
                    if (CanAttackTarget(card, defendingPlayer))
                        result.legalAttackTargetPlayerId = defendingPlayer;
                    AddLegalAttackTargets(card, player1Battlefield, result.legalAttackTargetObjectIds);
                    AddLegalAttackTargets(card, player2Battlefield, result.legalAttackTargetObjectIds);
                }

                result.canCancelBlocker = CanCancelBlocker(viewerSeat, index);
                IReadOnlyList<RuntimeCard> attackers = GetBattlefieldCards(currentPlayer);
                for (int attackerIndex = 0; attackerIndex < attackers.Count; attackerIndex++)
                {
                    RuntimeCard attacker = attackers[attackerIndex];
                    if (attacker != null && CanDeclareBlocker(viewerSeat, index, attacker))
                        result.legalBlockTargetObjectIds.Add(attacker.ObjectId);
                }
            }
        }
        result.attachedToObjectId = card.AttachedTo?.ObjectId ?? 0;
        foreach (string keyword in card.GetKeywords()) result.visibleKeywords.Add(keyword);
        result.visibleKeywords.Sort(System.StringComparer.OrdinalIgnoreCase);
        card.CopyOnlineRoomUnlocks(result.unlockedRoomFeatureIndices);
        result.tapped = card.IsTapped;
        result.attacking = card.IsAttacking;
        result.blocked = card.IsBlocked;
        result.blocking = card.IsBlocking;
        if (card.AttackTarget is int playerTarget) result.attackTargetPlayerId = playerTarget;
        else if (card.AttackTarget is RuntimeCard cardTarget) result.attackTargetObjectId = cardTarget.ObjectId;
        result.blockingTargetObjectId = card.BlockingTarget?.ObjectId ?? 0;
        for (int i = 0; i < card.Blockers.Count; i++)
        {
            if (card.Blockers[i] != null) result.blockerObjectIds.Add(card.Blockers[i].ObjectId);
        }
        card.CopyOnlineCounters(result.counters);
        return result;
    }

    private void AddLegalAttackTargets(
        RuntimeCard attacker,
        IReadOnlyList<RuntimeCard> candidates,
        List<long> destination)
    {
        if (attacker == null || candidates == null || destination == null) return;
        for (int i = 0; i < candidates.Count; i++)
        {
            RuntimeCard target = candidates[i];
            if (target != null && CanAttackTarget(attacker, target))
                destination.Add(target.ObjectId);
        }
    }

    private static GameChoiceRequest CloneChoice(GameChoiceRequest source)
    {
        return source == null
            ? null
            : JsonUtility.FromJson<GameChoiceRequest>(JsonUtility.ToJson(source));
    }

    private void ClearOnlineViewZones()
    {
        player1Library.Clear();
        player2Library.Clear();
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
        stack.Clear();
        stackCardView.Clear();
        pendingAttackers.Clear();
        pendingAttackTargets.Clear();
        player1Deck?.Clear();
        player2Deck?.Clear();
    }

    private static void BuildOnlineViewZone(
        IReadOnlyList<OnlineVisibleCard> source,
        List<RuntimeCard> destination)
    {
        if (source == null) return;
        for (int i = 0; i < source.Count; i++)
        {
            OnlineVisibleCard snapshot = source[i];
            if (snapshot == null) continue;
            CardDefinition definition = snapshot.hidden
                ? OnlineHiddenCardDefinition.Value
                : CardDatabaseRepository.FindByPrintingKey(snapshot.setCode, snapshot.collectorNumber)
                    ?? CardDatabaseRepository.FindById(snapshot.cardId)
                    ?? OnlineHiddenCardDefinition.Value;
            var card = new RuntimeCard(definition, snapshot.ownerPlayerId, initializeScript: false);
            card.ApplyOnlineSnapshot(snapshot);
            destination.Add(card);
        }
    }

    private void ResolveOnlineCombatReferences(OnlinePlayerViewState view)
    {
        var byId = new Dictionary<long, RuntimeCard>();
        AddCardsById(player1Battlefield, byId);
        AddCardsById(player2Battlefield, byId);
        ApplyCombatReferences(view.player1Battlefield, player1Battlefield, byId);
        ApplyCombatReferences(view.player2Battlefield, player2Battlefield, byId);
        ApplyAttachmentReferences(view.player1Battlefield, player1Battlefield, byId);
        ApplyAttachmentReferences(view.player2Battlefield, player2Battlefield, byId);
        AddPendingAttackers(player1Battlefield, byId);
        AddPendingAttackers(player2Battlefield, byId);
    }

    private static void ApplyAttachmentReferences(
        IReadOnlyList<OnlineVisibleCard> snapshots,
        IReadOnlyList<RuntimeCard> cards,
        Dictionary<long, RuntimeCard> byId)
    {
        int count = Mathf.Min(snapshots?.Count ?? 0, cards?.Count ?? 0);
        for (int i = 0; i < count; i++)
        {
            if (byId.TryGetValue(snapshots[i].attachedToObjectId, out RuntimeCard target))
                cards[i].AttachTo(target);
        }
    }

    private void AddPendingAttackers(
        IReadOnlyList<RuntimeCard> cards,
        Dictionary<long, RuntimeCard> byId)
    {
        for (int i = 0; i < cards.Count; i++)
        {
            RuntimeCard card = cards[i];
            if (card == null || !card.OnlinePendingAttacker) continue;
            pendingAttackers.Add(card);
            if (card.OnlinePendingAttackTargetPlayerId > 0)
                pendingAttackTargets[card] = card.OnlinePendingAttackTargetPlayerId;
            else if (byId.TryGetValue(card.OnlinePendingAttackTargetObjectId, out RuntimeCard target))
                pendingAttackTargets[card] = target;
        }
    }

    private static void AddCardsById(IReadOnlyList<RuntimeCard> cards, Dictionary<long, RuntimeCard> byId)
    {
        for (int i = 0; i < cards.Count; i++)
        {
            RuntimeCard card = cards[i];
            if (card != null && card.ObjectId > 0) byId[card.ObjectId] = card;
        }
    }

    private static void ApplyCombatReferences(
        IReadOnlyList<OnlineVisibleCard> snapshots,
        IReadOnlyList<RuntimeCard> cards,
        Dictionary<long, RuntimeCard> byId)
    {
        int count = Mathf.Min(snapshots?.Count ?? 0, cards?.Count ?? 0);
        for (int i = 0; i < count; i++)
        {
            OnlineVisibleCard snapshot = snapshots[i];
            RuntimeCard card = cards[i];
            if (snapshot.attackTargetPlayerId > 0) card.AttackTarget = snapshot.attackTargetPlayerId;
            else if (byId.TryGetValue(snapshot.attackTargetObjectId, out RuntimeCard attackTarget)) card.AttackTarget = attackTarget;
            if (byId.TryGetValue(snapshot.blockingTargetObjectId, out RuntimeCard blockingTarget)) card.BlockingTarget = blockingTarget;
            for (int j = 0; j < snapshot.blockerObjectIds.Count; j++)
            {
                if (byId.TryGetValue(snapshot.blockerObjectIds[j], out RuntimeCard blocker)) card.Blockers.Add(blocker);
            }
        }
    }
}

public partial class RuntimeCard
{
    public bool IsOnlineIdentityHiddenFrom(int viewerSeat)
    {
        if (viewerSeat == ControllerPlayerID) return false;
        for (int i = 0; i < continuousEffects.Count; i++)
        {
            if (continuousEffects[i]?.operation == ContinuousEffectOperation.SetFaceDownCharacteristics)
                return true;
        }
        return false;
    }

    public void CopyOnlineCounters(List<OnlineCounterValue> destination)
    {
        if (destination == null) return;
        foreach (KeyValuePair<string, int> pair in counters)
        {
            if (pair.Value > 0) destination.Add(new OnlineCounterValue { name = pair.Key, value = pair.Value });
        }
    }

    public void CopyOnlineRoomUnlocks(List<int> destination)
    {
        if (destination == null) return;
        destination.AddRange(unlockedRoomFeatures);
        destination.Sort();
    }

    public void ApplyOnlineSnapshot(OnlineVisibleCard source)
    {
        if (source == null) return;
        ObjectId = source.objectId;
        ZoneChangeVersion = source.zoneChangeVersion;
        CurrentZone = source.zone;
        OwnerPlayerID = source.ownerPlayerId;
        ControllerPlayerID = source.controllerPlayerId;
        baseControllerPlayerID = source.controllerPlayerId;
        CurrentFaceIndex = source.currentFaceIndex;
        CurrentAttack = source.currentAttack;
        CurrentDefence = source.currentDefence;
        CurrentLoyalty = source.currentLoyalty;
        CurrentBattleDefense = source.currentBattleDefense;
        BattleProtectorPlayerID = source.battleProtectorPlayerId;
        DamageTaken = source.damageTaken;
        calculatedCardName = source.visibleCardName ?? string.Empty;
        calculatedRulesText = source.visibleRulesText ?? string.Empty;
        calculatedManaCost = ManaCost.Parse(source.visibleManaCost);
        calculatedCardTypes = source.visibleCardTypes;
        calculatedSupertypes = source.visibleSupertypes;
        calculatedSubtypes = source.visibleSubtypes ?? string.Empty;
        calculatedColors = source.visibleColors;
        calculatedKeywords.Clear();
        if (source.visibleKeywords != null) calculatedKeywords.UnionWith(source.visibleKeywords);
        ClassLevel = source.classLevel;
        IsCaseSolved = source.isCaseSolved;
        CanCastPrimaryFromAdventureExile = source.canCastPrimaryFromAdventureExile;
        IsPrepared = source.isPrepared;
        IsSpellCopy = source.isSpellCopy;
        IsToken = source.isToken;
        IsEmblem = source.isEmblem;
        OnlineCanPlayFromHand = source.canPlayFromHand;
        OnlineCanCastFromExile = source.canCastFromExile;
        OnlineCanActivateAbility = source.canActivateAbility;
        OnlineCanActivateManaAbility = source.canActivateManaAbility;
        OnlineCanDeclareAttacker = source.canDeclareAttacker;
        OnlinePendingAttacker = source.pendingAttacker;
        OnlinePendingAttackTargetPlayerId = source.pendingAttackTargetPlayerId;
        OnlinePendingAttackTargetObjectId = source.pendingAttackTargetObjectId;
        OnlineLegalAttackTargetPlayerId = source.legalAttackTargetPlayerId;
        onlineLegalAttackTargetObjectIds.Clear();
        if (source.legalAttackTargetObjectIds != null)
            onlineLegalAttackTargetObjectIds.UnionWith(source.legalAttackTargetObjectIds);
        OnlineCanCancelBlocker = source.canCancelBlocker;
        onlineLegalBlockTargetObjectIds.Clear();
        if (source.legalBlockTargetObjectIds != null)
            onlineLegalBlockTargetObjectIds.UnionWith(source.legalBlockTargetObjectIds);
        unlockedRoomFeatures.Clear();
        if (source.unlockedRoomFeatureIndices != null)
            unlockedRoomFeatures.UnionWith(source.unlockedRoomFeatureIndices);
        IsTapped = source.tapped;
        IsAttacking = source.attacking;
        IsBlocked = source.blocked;
        IsBlocking = source.blocking;
        counters.Clear();
        for (int i = 0; i < source.counters.Count; i++)
        {
            OnlineCounterValue counter = source.counters[i];
            if (counter != null && !string.IsNullOrWhiteSpace(counter.name) && counter.value > 0)
            {
                counters[counter.name.Trim()] = counter.value;
            }
        }
    }

    public bool OnlineCanPlayFromHand { get; private set; }
    public bool OnlineCanCastFromExile { get; private set; }
    public bool OnlineCanActivateAbility { get; private set; }
    public bool OnlineCanActivateManaAbility { get; private set; }
    public bool OnlineCanDeclareAttacker { get; private set; }
    public bool OnlinePendingAttacker { get; private set; }
    public int OnlinePendingAttackTargetPlayerId { get; private set; }
    public long OnlinePendingAttackTargetObjectId { get; private set; }
    public int OnlineLegalAttackTargetPlayerId { get; private set; }
    public bool OnlineCanCancelBlocker { get; private set; }
    private readonly HashSet<long> onlineLegalAttackTargetObjectIds = new HashSet<long>();
    private readonly HashSet<long> onlineLegalBlockTargetObjectIds = new HashSet<long>();

    public bool IsOnlineLegalAttackTarget(long objectId) =>
        objectId > 0 && onlineLegalAttackTargetObjectIds.Contains(objectId);

    public bool IsOnlineLegalBlockTarget(long objectId) =>
        objectId > 0 && onlineLegalBlockTargetObjectIds.Contains(objectId);

    public bool HasOnlineLegalBlockTarget => onlineLegalBlockTargetObjectIds.Count > 0;
}

internal static class OnlineHiddenCardDefinition
{
    public static readonly CardDefinition Value = Create();

    private static CardDefinition Create()
    {
        var card = new CardDefinition
        {
            cardID = 0,
            cardName = "Hidden Card",
            setCode = string.Empty,
            collectorNumberText = string.Empty,
            description = string.Empty
        };
        card.Normalize();
        return card;
    }
}
