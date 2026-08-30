using System;
using System.Collections.Generic;
using UnityEngine;

public class CardScriptApi
{
    private static readonly Stack<GameRules> RulesStack = new Stack<GameRules>();

    private GameRules Rules
    {
        get
        {
            if (RulesStack.Count > 0 && RulesStack.Peek() != null) return RulesStack.Peek();
            return UnityEngine.Object.FindFirstObjectByType<GameRules>();
        }
    }

    public static IDisposable UseRules(GameRules rules)
    {
        RulesStack.Push(rules);
        return new RulesScope();
    }

    private sealed class RulesScope : IDisposable
    {
        private bool disposed;

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            if (RulesStack.Count > 0) RulesStack.Pop();
        }
    }

    public void DealDamage(int targetPlayerID, int amount)
    {
        Rules?.DealDamageToPlayer(targetPlayerID, amount);
    }

    public void DrawCards(int playerID, int count)
    {
        Rules?.ForceDrawCard(playerID, count);
    }

    public void DrawForOwner(RuntimeCard source, int count)
    {
        if (Rules != null && source != null)
        {
            Rules.ForceDrawCard(source.OwnerPlayerID, count);
        }
    }

    public void PreventNextDamage(RuntimeCard card, int amount)
    {
        Rules?.PreventNextDamageToCard(card, amount);
    }

    public void PreventAllDamage(RuntimeCard card)
    {
        Rules?.PreventAllDamageToCard(card);
    }

    public void PreventNextDamageToPlayer(int playerID, int amount)
    {
        Rules?.PreventNextDamageToPlayer(playerID, amount);
    }

    public void PreventAllDamageToPlayer(int playerID)
    {
        Rules?.PreventAllDamageToPlayer(playerID);
    }

    public void PreventNextDamageBySource(RuntimeCard source, int amount)
    {
        Rules?.PreventNextDamageBySource(source, amount);
    }

    public void PreventAllDamageBySource(RuntimeCard source)
    {
        Rules?.PreventAllDamageBySource(source);
    }

    public long ModifyOutgoingDamage(
        RuntimeCard effectSource,
        RuntimeCard damageSource,
        string operation,
        int value,
        bool combatOnly,
        bool untilEndOfTurn)
    {
        return RegisterDamageReplacement(
            effectSource,
            DamageReplacementScope.SourceDealsDamage,
            operation,
            damageSource,
            0,
            value,
            combatOnly,
            untilEndOfTurn);
    }

    public long ModifyIncomingDamage(
        RuntimeCard effectSource,
        RuntimeCard target,
        string operation,
        int value,
        bool combatOnly,
        bool untilEndOfTurn)
    {
        return RegisterDamageReplacement(
            effectSource,
            DamageReplacementScope.CardIsDealtDamage,
            operation,
            target,
            0,
            value,
            combatOnly,
            untilEndOfTurn);
    }

    public long ModifyPlayerDamage(
        RuntimeCard effectSource,
        int targetPlayerID,
        string operation,
        int value,
        bool combatOnly,
        bool untilEndOfTurn)
    {
        return RegisterDamageReplacement(
            effectSource,
            DamageReplacementScope.PlayerIsDealtDamage,
            operation,
            null,
            targetPlayerID,
            value,
            combatOnly,
            untilEndOfTurn);
    }

    public bool RemoveDamageModifier(long modifierId)
    {
        return Rules != null && Rules.RemoveDamageReplacement(modifierId);
    }

    private long RegisterDamageReplacement(
        RuntimeCard effectSource,
        DamageReplacementScope scope,
        string operation,
        RuntimeCard scopeCard,
        int scopePlayer,
        int value,
        bool combatOnly,
        bool untilEndOfTurn)
    {
        if (Rules == null
            || !Enum.TryParse(
                operation,
                true,
                out DamageReplacementOperation parsedOperation))
        {
            return 0;
        }

        return Rules.RegisterDamageReplacement(
            effectSource,
            scope,
            parsedOperation,
            scopeCard,
            scopePlayer,
            value,
            combatOnly,
            untilEndOfTurn);
    }

    public void Regenerate(RuntimeCard card)
    {
        Rules?.AddRegenerationShield(card);
    }

    public bool CanTargetCard(RuntimeCard source, RuntimeCard target)
    {
        return Rules != null && Rules.CanTargetCard(source, target);
    }

    public bool CanAttachToCard(RuntimeCard source, RuntimeCard target)
    {
        return Rules != null && Rules.CanAttachToCard(source, target);
    }

    public bool AttachToCard(RuntimeCard source, RuntimeCard target)
    {
        return Rules != null && Rules.AttachCard(source, target);
    }

    public bool DetachCard(RuntimeCard source)
    {
        return Rules != null && Rules.DetachCard(source);
    }

    public RuntimeCard GetTargetCard(RuntimeCard source)
    {
        return Rules != null ? Rules.GetTargetCard(source) : null;
    }

    public int GetTargetPlayer(RuntimeCard source)
    {
        return Rules != null ? Rules.GetTargetPlayer(source) : 0;
    }

    public int GetTargetCount(RuntimeCard source)
    {
        return Rules != null ? Rules.GetTargetCount(source) : 0;
    }

    public RuntimeCard GetTargetCardAt(RuntimeCard source, int oneBasedIndex)
    {
        return Rules != null ? Rules.GetTargetCardAt(source, oneBasedIndex) : null;
    }

    public int GetTargetPlayerAt(RuntimeCard source, int oneBasedIndex)
    {
        return Rules != null ? Rules.GetTargetPlayerAt(source, oneBasedIndex) : 0;
    }

    public bool AddTargetCard(RuntimeCard source, RuntimeCard target)
    {
        return Rules != null && Rules.AddSpellTarget(source, target);
    }

    public bool AddTargetPlayer(RuntimeCard source, int targetPlayerID)
    {
        return Rules != null && Rules.AddSpellTargetPlayer(source, targetPlayerID);
    }

    public bool ConfigureCastModes(RuntimeCard card, int minimum, int maximum)
    {
        return card != null && card.CastProfile.ConfigureModes(minimum, maximum);
    }

    public bool AddCastMode(RuntimeCard card, string id, string label)
    {
        return card != null && card.CastProfile.AddMode(id, label);
    }

    public bool ConfigureCastX(RuntimeCard card, int minimum, int maximum)
    {
        return card != null && card.CastProfile.ConfigureX(minimum, maximum);
    }

    public bool AddCastCost(
        RuntimeCard card,
        string id,
        string label,
        string kind,
        string costText,
        bool optional)
    {
        if (card == null
            || !Enum.TryParse(kind, true, out SpellCastCostKind parsedKind)
            || !ScriptAbilityCost.TryParse(costText, out ScriptAbilityCost cost, out _))
        {
            return false;
        }

        return card.CastProfile.AddCost(new SpellCastCostOption(
            id,
            label,
            parsedKind,
            cost,
            optional));
    }

    public bool AddCastTargetGroup(
        RuntimeCard card,
        string id,
        string prompt,
        string targetMode,
        int minimumTargets,
        int maximumTargets,
        string filterHookName,
        bool distinctWithinGroup)
    {
        if (card == null
            || !Enum.TryParse(targetMode, true, out CardTargetMode parsedMode))
        {
            return false;
        }

        return card.CastProfile.AddTargetGroup(new SpellTargetGroupDefinition(
            id,
            prompt,
            parsedMode,
            minimumTargets,
            maximumTargets,
            filterHookName,
            distinctWithinGroup));
    }

    public bool AddCastTargetGroupAdvanced(
        RuntimeCard card,
        string id,
        string prompt,
        int targetKinds,
        int allowedZones,
        int controllerRelation,
        int requiredCardTypes,
        int requiredColors,
        int minimumTargets,
        int maximumTargets,
        string filterHookName,
        bool distinctWithinGroup,
        int relations,
        string relationGroupId)
    {
        if (card == null
            || !Enum.IsDefined(
                typeof(SpellTargetControllerRelation),
                controllerRelation))
        {
            return false;
        }

        return card.CastProfile.AddTargetGroup(new SpellTargetGroupDefinition(
            id,
            prompt,
            (SpellTargetKind)targetKinds,
            (SpellTargetZone)allowedZones,
            (SpellTargetControllerRelation)controllerRelation,
            (CardType)requiredCardTypes,
            (ManaColorFlags)requiredColors,
            minimumTargets,
            maximumTargets,
            filterHookName,
            distinctWithinGroup,
            (SpellTargetRelation)relations,
            relationGroupId));
    }

    public int GetCastX(RuntimeCard card)
    {
        return card?.CurrentCastDeclaration?.XValue ?? 0;
    }

    public bool IsCastModeSelected(RuntimeCard card, string modeId)
    {
        IReadOnlyList<string> selected = card?.CurrentCastDeclaration?.SelectedModeIds;
        if (selected == null || string.IsNullOrWhiteSpace(modeId)) return false;
        for (int i = 0; i < selected.Count; i++)
        {
            if (string.Equals(selected[i], modeId.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }

    public bool WasAdditionalCostPaid(RuntimeCard card, string costId)
    {
        IReadOnlyList<string> selected =
            card?.CurrentCastDeclaration?.SelectedAdditionalCostIds;
        if (selected == null || string.IsNullOrWhiteSpace(costId)) return false;
        for (int i = 0; i < selected.Count; i++)
        {
            if (string.Equals(selected[i], costId.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }

    public int GetTargetGroupCount(RuntimeCard card, string groupId)
    {
        return card?.CurrentCastDeclaration?.GetTargets(groupId).Count ?? 0;
    }

    public RuntimeCard GetTargetCardInGroup(
        RuntimeCard card,
        string groupId,
        int oneBasedIndex)
    {
        IReadOnlyList<SpellTargetReference> targets =
            card?.CurrentCastDeclaration?.GetTargets(groupId);
        int index = oneBasedIndex - 1;
        if (targets == null || index < 0 || index >= targets.Count) return null;
        SpellTargetReference target = targets[index];
        if (target == null) return null;
        if (target.IsStackTarget) return target.StackItem?.SourceCard;
        return target.HasCurrentCardIdentity ? target.Card : null;
    }

    public int GetTargetPlayerInGroup(
        RuntimeCard card,
        string groupId,
        int oneBasedIndex)
    {
        IReadOnlyList<SpellTargetReference> targets =
            card?.CurrentCastDeclaration?.GetTargets(groupId);
        int index = oneBasedIndex - 1;
        if (targets == null || index < 0 || index >= targets.Count) return 0;
        return targets[index]?.PlayerID ?? 0;
    }

    public void ClearTargets(RuntimeCard source)
    {
        source?.ClearSpellTarget();
    }

    public bool ChooseOptions(
        RuntimeCard source,
        string prompt,
        IReadOnlyList<string> labels,
        int minimumSelections,
        int maximumSelections,
        bool mayCancel,
        string completedHook,
        string cancelledHook)
    {
        return BeginOptionChoice(
            source,
            GameChoiceKind.SelectOptions,
            prompt,
            labels,
            minimumSelections,
            maximumSelections,
            mayCancel,
            completedHook,
            cancelledHook);
    }

    public bool ChooseMode(
        RuntimeCard source,
        string prompt,
        IReadOnlyList<string> labels,
        bool mayCancel,
        string completedHook,
        string cancelledHook)
    {
        return BeginOptionChoice(
            source,
            GameChoiceKind.ChooseMode,
            prompt,
            labels,
            1,
            1,
            mayCancel,
            completedHook,
            cancelledHook);
    }

    public bool OrderOptions(
        RuntimeCard source,
        string prompt,
        IReadOnlyList<string> labels,
        string completedHook)
    {
        int optionCount = labels?.Count ?? 0;
        return BeginOptionChoice(
            source,
            GameChoiceKind.OrderCards,
            prompt,
            labels,
            optionCount,
            optionCount,
            false,
            completedHook,
            string.Empty);
    }

    public bool ChooseNumber(
        RuntimeCard source,
        string prompt,
        int minimumValue,
        int maximumValue,
        bool mayCancel,
        string completedHook,
        string cancelledHook)
    {
        GameRules rules = Rules;
        if (rules == null || source == null) return false;

        return rules.BeginNumberChoice(
            source.OwnerPlayerID,
            prompt,
            minimumValue,
            maximumValue,
            mayCancel,
            _ => InvokeChoiceHook(rules, source, completedHook),
            () => InvokeChoiceHook(rules, source, cancelledHook)) != null;
    }

    public bool ChooseRepeatedOptions(
        RuntimeCard source,
        string prompt,
        IReadOnlyList<string> labels,
        int minimumSelections,
        int maximumSelections,
        int maximumPerOption,
        bool mayCancel,
        string completedHook,
        string cancelledHook)
    {
        GameRules rules = Rules;
        if (rules == null
            || source == null
            || minimumSelections < 0
            || maximumSelections < minimumSelections
            || maximumPerOption <= 0)
        {
            return false;
        }
        List<GameChoiceOption> options =
            CreateTextChoiceOptions(labels, maximumPerOption);
        return rules.BeginRepeatedChoice(
            source.OwnerPlayerID,
            prompt,
            minimumSelections,
            maximumSelections,
            mayCancel,
            options,
            _ => InvokeChoiceHook(rules, source, completedHook),
            () => InvokeChoiceHook(rules, source, cancelledHook)) != null;
    }

    public bool DistributeAmount(
        RuntimeCard source,
        string prompt,
        IReadOnlyList<string> labels,
        int totalAmount,
        int minimumTargets,
        int maximumTargets,
        bool mayCancel,
        string completedHook,
        string cancelledHook)
    {
        GameRules rules = Rules;
        if (rules == null || source == null) return false;

        List<GameChoiceOption> options = CreateTextChoiceOptions(labels, totalAmount);
        return rules.BeginDistributionChoice(
            source.OwnerPlayerID,
            prompt,
            totalAmount,
            minimumTargets,
            maximumTargets,
            mayCancel,
            options,
            _ => InvokeChoiceHook(rules, source, completedHook),
            () => InvokeChoiceHook(rules, source, cancelledHook)) != null;
    }

    public int GetLastChoiceValue()
    {
        return Rules?.GetLastCompletedChoice()?.selectedValue ?? 0;
    }

    public int GetLastChoiceCount()
    {
        return Rules?.GetLastCompletedChoice()?.selectedOptionIds.Count ?? 0;
    }

    public int GetLastChoiceOption(int oneBasedIndex)
    {
        GameChoiceResult result = Rules?.GetLastCompletedChoice();
        int index = oneBasedIndex - 1;
        return result != null && index >= 0 && index < result.selectedOptionIds.Count
            ? result.selectedOptionIds[index]
            : 0;
    }

    public int GetLastChoiceAllocation(int optionId)
    {
        GameChoiceResult result = Rules?.GetLastCompletedChoice();
        if (result == null) return 0;

        for (int i = 0; i < result.allocations.Count; i++)
        {
            GameChoiceAllocation allocation = result.allocations[i];
            if (allocation != null && allocation.optionId == optionId) return allocation.amount;
        }

        return 0;
    }

    private bool BeginOptionChoice(
        RuntimeCard source,
        GameChoiceKind kind,
        string prompt,
        IReadOnlyList<string> labels,
        int minimumSelections,
        int maximumSelections,
        bool mayCancel,
        string completedHook,
        string cancelledHook)
    {
        GameRules rules = Rules;
        if (rules == null || source == null) return false;

        List<GameChoiceOption> options = CreateTextChoiceOptions(labels, 1);
        return rules.BeginOptionChoice(
            kind,
            source.OwnerPlayerID,
            prompt,
            minimumSelections,
            maximumSelections,
            mayCancel,
            options,
            _ => InvokeChoiceHook(rules, source, completedHook),
            () => InvokeChoiceHook(rules, source, cancelledHook)) != null;
    }

    private static List<GameChoiceOption> CreateTextChoiceOptions(
        IReadOnlyList<string> labels,
        int maximumAmount)
    {
        var options = new List<GameChoiceOption>();
        if (labels == null) return options;

        for (int i = 0; i < labels.Count; i++)
        {
            options.Add(new GameChoiceOption(
                i + 1,
                GameChoiceZone.None,
                0,
                -1,
                true,
                labels[i],
                0,
                Mathf.Max(1, maximumAmount)));
        }

        return options;
    }

    private static void InvokeChoiceHook(GameRules rules, RuntimeCard source, string hookName)
    {
        if (rules == null || source == null || string.IsNullOrWhiteSpace(hookName)) return;
        CardEffectExecutor.ExecuteTriggeredHook(rules, source, hookName.Trim());
    }

    public bool DealDamageToCard(RuntimeCard source, RuntimeCard target, int amount)
    {
        return Rules != null && Rules.DealDamageToCard(source, target, amount);
    }

    public bool DealDamageToTarget(RuntimeCard source, int amount)
    {
        return Rules != null && Rules.DealDamageToSpellTarget(source, amount);
    }

    public bool DealDamageToPlayer(RuntimeCard source, int playerID, int amount)
    {
        return Rules != null && Rules.DealDamageToPlayer(source, playerID, amount);
    }

    public void GainLife(int playerID, int amount)
    {
        Rules?.GainLife(playerID, amount);
    }

    public void LoseLife(int playerID, int amount)
    {
        Rules?.LoseLife(playerID, amount);
    }

    public void GainLifeForOwner(RuntimeCard source, int amount)
    {
        if (Rules != null && source != null)
        {
            Rules.GainLife(source.OwnerPlayerID, amount);
        }
    }

    public void LoseLifeForOwner(RuntimeCard source, int amount)
    {
        if (Rules != null && source != null)
        {
            Rules.LoseLife(source.OwnerPlayerID, amount);
        }
    }

    public void AddPoisonCounters(int playerID, int amount)
    {
        Rules?.AddPoisonCounters(playerID, amount);
    }

    public int GetPoisonCounters(int playerID)
    {
        return Rules != null ? Rules.GetPoisonCounters(playerID) : 0;
    }

    public int GetLoyaltyCounters(RuntimeCard card)
    {
        return Rules != null ? Rules.GetLoyaltyCounters(card) : 0;
    }

    public bool AddLoyaltyCounters(RuntimeCard card, int amount)
    {
        return Rules != null && Rules.AddLoyaltyCounters(card, amount);
    }

    public int GetCounters(RuntimeCard card, string counterName)
    {
        return Rules != null ? Rules.GetCounters(card, counterName) : 0;
    }

    public bool AddCounters(RuntimeCard card, string counterName, int amount)
    {
        return Rules != null && Rules.AddCounters(card, counterName, amount);
    }

    public int GetOwner(RuntimeCard card)
    {
        return card != null ? card.OwnerPlayerID : 0;
    }

    public int GetController(RuntimeCard card)
    {
        return Rules != null ? Rules.GetController(card) : card?.ControllerPlayerID ?? 0;
    }

    public bool ChangeController(RuntimeCard card, int playerID)
    {
        return Rules != null && Rules.ChangeController(card, playerID);
    }

    public int GetOpponent(int playerID)
    {
        if (playerID == 1) return 2;
        if (playerID == 2) return 1;
        return 0;
    }

    public string GetCardName(RuntimeCard card)
    {
        return card?.GetCardName() ?? string.Empty;
    }

    public int GetFeatureRole(RuntimeCard card)
    {
        return (int)(GetContextFeature(card)?.Role ?? CardFeatureRole.Primary);
    }

    public int GetFeatureIndex(RuntimeCard card)
    {
        return GetContextFeature(card)?.FeatureIndex ?? 0;
    }

    public int GetFeatureFace(RuntimeCard card)
    {
        return GetContextFeature(card)?.FaceIndex ?? 0;
    }

    public string GetFeatureName(RuntimeCard card)
    {
        return GetContextFeature(card)?.CardName ?? card?.Data?.cardName ?? string.Empty;
    }

    public string GetFeatureRoleValue(RuntimeCard card)
    {
        return GetContextFeature(card)?.RoleValue ?? string.Empty;
    }

    public int GetResolvingChapter(RuntimeCard card)
    {
        return card?.ResolvingChapter ?? 0;
    }

    public int GetClassLevel(RuntimeCard card)
    {
        return card?.ClassLevel ?? 0;
    }

    public bool IsCaseSolved(RuntimeCard card)
    {
        return card != null && card.IsCaseSolved;
    }

    public bool IsFeatureActive(RuntimeCard card, int faceIndex, int featureIndex)
    {
        CardFeatureRuntime feature = card?.FindFeature(faceIndex, featureIndex);
        return card != null && card.IsFeatureActive(feature);
    }

    public bool IsRoomUnlocked(RuntimeCard card, int faceIndex, int featureIndex)
    {
        CardFeatureRuntime feature = card?.FindFeature(faceIndex, featureIndex);
        return card != null && card.IsRoomFeatureUnlocked(feature);
    }

    public bool Prepare(RuntimeCard card, int featureIndex)
    {
        return Rules != null && Rules.PrepareCard(card, featureIndex);
    }

    public bool StopPreparing(RuntimeCard card)
    {
        return Rules != null && Rules.StopPreparing(card);
    }

    public bool SolveCase(RuntimeCard card)
    {
        return Rules != null && Rules.SolveCase(card);
    }

    private static CardFeatureRuntime GetContextFeature(RuntimeCard card)
    {
        return card?.ResolvingFeature ?? card?.SpellFeature ?? card?.PrimaryFeature;
    }

    public bool IsOnBattlefield(RuntimeCard card)
    {
        return Rules != null && Rules.IsCardOnBattlefield(card);
    }

    public bool Destroy(RuntimeCard card)
    {
        return Rules != null && Rules.DestroyCard(card);
    }

    public bool DestroyTarget(RuntimeCard source)
    {
        RuntimeCard target = GetTargetCard(source);
        return target != null && Rules != null && Rules.DestroyCard(source, target);
    }

    public bool Exile(RuntimeCard card)
    {
        return Rules != null && Rules.ExileCard(card);
    }

    public bool ExileTarget(RuntimeCard source)
    {
        RuntimeCard target = GetTargetCard(source);
        return target != null && Rules != null && Rules.ExileCard(target);
    }

    public bool PutIntoGraveyard(RuntimeCard card)
    {
        return Rules != null && Rules.MoveCardToGraveyard(card);
    }

    public int CreateTokens(
        string setCode,
        string collectorNumber,
        int controllerPlayerID,
        int count)
    {
        return Rules != null
            ? Rules.CreateTokens(setCode, collectorNumber, controllerPlayerID, count)
            : 0;
    }

    public RuntimeCard CreateEmblem(
        string setCode,
        string collectorNumber,
        int controllerPlayerID)
    {
        return Rules?.CreateEmblem(setCode, collectorNumber, controllerPlayerID);
    }

    public RuntimeCard CopySpell(RuntimeCard sourceSpell, int controllerPlayerID)
    {
        return Rules?.CopySpell(sourceSpell, controllerPlayerID);
    }

    public bool CopyStackAbility(int oneBasedStackIndex, int controllerPlayerID)
    {
        return Rules != null
            && Rules.CopyStackAbility(oneBasedStackIndex - 1, controllerPlayerID);
    }

    public bool Tap(RuntimeCard card)
    {
        return Rules != null && Rules.TapCard(card);
    }

    public bool Untap(RuntimeCard card)
    {
        return Rules != null && Rules.UntapCard(card);
    }

    public bool RegisterFetchBasicLandAbility(RuntimeCard card)
    {
        return Rules != null && Rules.RegisterFetchBasicLandAbility(card);
    }

    public bool RegisterManaAbility(RuntimeCard card, string producedSymbols)
    {
        return Rules != null && Rules.RegisterManaAbility(card, producedSymbols);
    }

    public long RegisterTriggeredAbility(
        RuntimeCard card,
        string eventType,
        string hookName,
        string conditionHookName,
        string duration,
        bool otherCardsOnly,
        string linkId = null)
    {
        if (Rules == null
            || card == null
            || !Enum.TryParse(eventType, true, out GameEventType parsedEvent)
            || !TryParseDuration(duration, out RuntimeEffectDuration parsedDuration))
        {
            return 0;
        }

        return Rules.RegisterScriptTrigger(
            card,
            parsedEvent,
            hookName,
            conditionHookName,
            parsedDuration,
            otherCardsOnly,
            linkId);
    }

    public long RegisterDelayedTriggeredAbility(
        RuntimeCard card,
        string eventType,
        string hookName,
        string conditionHookName = null,
        bool otherCardsOnly = false,
        string linkId = null)
    {
        return RegisterTriggeredAbility(
            card,
            eventType,
            hookName,
            conditionHookName,
            RuntimeEffectDuration.Once.ToString(),
            otherCardsOnly,
            linkId);
    }

    public long RegisterReplacementEffect(
        RuntimeCard card,
        string eventType,
        string operation,
        string hookName,
        int value,
        string duration,
        string conditionHookName = null,
        string linkId = null,
        string category = nameof(ReplacementEffectCategory.General))
    {
        ReplacementEffectCategory parsedCategory = ReplacementEffectCategory.General;
        if (Rules == null
            || card == null
            || !Enum.TryParse(eventType, true, out GameEventType parsedEvent)
            || !Enum.TryParse(
                operation,
                true,
                out GameEventReplacementOperation parsedOperation)
            || (!string.IsNullOrWhiteSpace(category)
                && !Enum.TryParse(category, true, out parsedCategory))
            || !TryParseDuration(duration, out RuntimeEffectDuration parsedDuration))
        {
            return 0;
        }

        return Rules.RegisterScriptReplacement(
            card,
            parsedEvent,
            parsedOperation,
            hookName,
            value,
            parsedDuration,
            conditionHookName,
            linkId,
            parsedCategory);
    }

    public long RegisterContinuousEffect(
        RuntimeCard card,
        string operation,
        string filterHookName,
        string dependencyKey,
        int cardTypes,
        int colors,
        string textValue,
        int valueA,
        int valueB,
        string duration,
        string linkId,
        bool characteristicDefining = false,
        int reads = 0,
        int writes = 0,
        RuntimeCard copySource = null,
        int supertypes = 0)
    {
        if (Rules == null
            || card == null
            || !Enum.TryParse(
                operation,
                true,
                out ContinuousEffectOperation parsedOperation)
            || !TryParseDuration(duration, out RuntimeEffectDuration parsedDuration))
        {
            return 0;
        }

        return Rules.RegisterScriptContinuousEffect(
            card,
            parsedOperation,
            filterHookName,
            dependencyKey,
            (CardType)cardTypes,
            (ManaColorFlags)colors,
            textValue,
            valueA,
            valueB,
            parsedDuration,
            linkId,
            characteristicDefining,
            (ContinuousEffectCharacteristic)reads,
            (ContinuousEffectCharacteristic)writes,
            copySource,
            (CardSupertype)supertypes);
    }

    public long RegisterRuleEffect(
        RuntimeCard card,
        string ruleKey,
        string operation,
        int value,
        int affectedPlayerId,
        string filterHookName,
        string duration,
        string linkId)
    {
        if (Rules == null
            || card == null
            || !Enum.TryParse(operation, true, out RuleEffectOperation parsedOperation)
            || !TryParseDuration(duration, out RuntimeEffectDuration parsedDuration))
        {
            return 0;
        }

        return Rules.RegisterScriptRuleEffect(
            card,
            ruleKey,
            parsedOperation,
            value,
            affectedPlayerId,
            filterHookName,
            parsedDuration,
            linkId);
    }

    public int GetRuleValue(
        string ruleKey,
        int playerId,
        RuntimeCard subject,
        int baseValue)
    {
        return Rules != null
            ? Rules.EvaluateScriptRuleValue(ruleKey, playerId, subject, baseValue)
            : baseValue;
    }

    public bool GetRulePermission(
        string ruleKey,
        int playerId,
        RuntimeCard subject,
        bool baseValue)
    {
        return Rules != null
            ? Rules.EvaluateScriptRulePermission(ruleKey, playerId, subject, baseValue)
            : baseValue;
    }

    public bool RemoveRuntimeEffect(long effectId)
    {
        return Rules != null && Rules.RemoveScriptEffect(effectId);
    }

    public int RemoveLinkedEffects(RuntimeCard card, string linkId)
    {
        return Rules != null
            ? Rules.RemoveScriptEffectsByLink(card, linkId)
            : 0;
    }

    public bool RegisterActivatedAbility(
        RuntimeCard card,
        string abilityName,
        string description,
        string cost,
        string resolveHook,
        string canActivateHook,
        string timing,
        int ruleTraits)
    {
        const ActivatedAbilityRuleTrait supportedTraits =
            ActivatedAbilityRuleTrait.CouldAddMana
            | ActivatedAbilityRuleTrait.RequiresTarget
            | ActivatedAbilityRuleTrait.LoyaltyAbility
            | ActivatedAbilityRuleTrait.MovesCardIntoOrOutOfLibrary;
        var parsedTraits = (ActivatedAbilityRuleTrait)ruleTraits;
        if (Rules == null
            || (parsedTraits & ~supportedTraits) != 0
            || !Enum.TryParse(
                string.IsNullOrWhiteSpace(timing) ? "AnyPriority" : timing,
                true,
                out ScriptAbilityTiming parsedTiming))
        {
            return false;
        }

        return Rules.RegisterScriptActivatedAbility(
            card,
            abilityName,
            description,
            cost,
            resolveHook,
            canActivateHook,
            parsedTiming,
            parsedTraits);
    }

    public int GetEventType(RuntimeCard card)
    {
        return (int)(card?.ResolvingGameEvent?.type ?? GameEventType.None);
    }

    public int GetEventAmount(RuntimeCard card)
    {
        return card?.ResolvingGameEvent?.amount ?? 0;
    }

    public int GetEventPlayer(RuntimeCard card)
    {
        return card?.ResolvingGameEvent?.playerId ?? 0;
    }

    public RuntimeCard GetEventSource(RuntimeCard card)
    {
        return card?.ResolvingGameEvent?.source;
    }

    public RuntimeCard GetEventCard(RuntimeCard card)
    {
        return card?.ResolvingGameEvent?.card;
    }

    public int GetEventFromZone(RuntimeCard card)
    {
        return (int)(card?.ResolvingGameEvent?.fromZone ?? GameZone.None);
    }

    public int GetEventToZone(RuntimeCard card)
    {
        return (int)(card?.ResolvingGameEvent?.toZone ?? GameZone.None);
    }

    public bool SetEventAmount(RuntimeCard card, int amount)
    {
        GameEventContext gameEvent = card?.ResolvingGameEvent;
        if (gameEvent == null) return false;
        gameEvent.amount = Mathf.Max(0, amount);
        gameEvent.replaced = true;
        return true;
    }

    public bool SetEventDamageTarget(RuntimeCard card, RuntimeCard target)
    {
        return Rules != null && Rules.RedirectCurrentDamageToCard(card, target);
    }

    public bool SetEventDamagePlayer(RuntimeCard card, int playerID)
    {
        return Rules != null && Rules.RedirectCurrentDamageToPlayer(card, playerID);
    }

    public bool PreventEvent(RuntimeCard card)
    {
        GameEventContext gameEvent = card?.ResolvingGameEvent;
        if (gameEvent == null) return false;
        gameEvent.amount = 0;
        gameEvent.prevented = true;
        gameEvent.replaced = true;
        return true;
    }

    public bool SetEventDestination(RuntimeCard card, string destination)
    {
        GameEventContext gameEvent = card?.ResolvingGameEvent;
        if (gameEvent == null
            || !Enum.TryParse(destination, true, out GameZone parsedZone))
        {
            return false;
        }

        gameEvent.toZone = parsedZone;
        gameEvent.replaced = true;
        return true;
    }

    public int GetZoneCount(int playerID, string zone)
    {
        return TryParseZone(zone, out GameZone parsedZone)
            ? Rules?.GetZoneCards(playerID, parsedZone).Count ?? 0
            : 0;
    }

    public RuntimeCard GetZoneCard(int playerID, string zone, int oneBasedIndex)
    {
        return Rules != null
            && TryParseZone(zone, out GameZone parsedZone)
            && parsedZone != GameZone.Library
            && parsedZone != GameZone.Hand
            ? Rules.GetZoneCard(playerID, parsedZone, oneBasedIndex - 1)
            : null;
    }

    public List<RuntimeCard> GetMatchingZoneCards(
        RuntimeCard source,
        int playerID,
        string zone,
        string filterHookName)
    {
        var result = new List<RuntimeCard>();
        if (Rules == null
            || source == null
            || !TryParseZone(zone, out GameZone parsedZone)
            || !Rules.CanScriptInspectZone(source, playerID, parsedZone))
        {
            return result;
        }

        IReadOnlyList<RuntimeCard> cards = Rules.GetZoneCards(playerID, parsedZone);
        if (cards == null) return result;

        for (int i = 0; i < cards.Count; i++)
        {
            RuntimeCard candidate = cards[i];
            if (candidate == null) continue;
            if (string.IsNullOrWhiteSpace(filterHookName))
            {
                result.Add(candidate);
                continue;
            }

            GameEventContext previous = source.ResolvingGameEvent;
            source.SetResolvingGameEvent(new GameEventContext
            {
                type = GameEventType.None,
                source = source,
                card = candidate,
                playerId = playerID,
                controllerId = candidate.ControllerPlayerID,
                fromZone = parsedZone,
                toZone = parsedZone
            });
            bool matches = CardEffectExecutor.EvaluateCardScriptPredicate(
                Rules,
                source,
                filterHookName,
                false);
            source.SetResolvingGameEvent(previous);
            if (matches) result.Add(candidate);
        }

        return result;
    }

    public bool MoveToZone(RuntimeCard card, string destination, bool toTop)
    {
        return Rules != null
            && TryParseZone(destination, out GameZone parsedZone)
            && Rules.MoveCardToZone(card, parsedZone, toTop);
    }

    public bool MoveCardsToZone(
        IReadOnlyList<RuntimeCard> cards,
        string destination,
        bool toTop)
    {
        return Rules != null
            && TryParseZone(destination, out GameZone parsedZone)
            && Rules.MoveCardsToZone(cards, parsedZone, toTop);
    }

    public bool ShuffleLibrary(int playerID)
    {
        return Rules != null && Rules.ShuffleLibrary(playerID);
    }

    public int Mill(int playerID, int count)
    {
        return Rules?.MillCards(playerID, count) ?? 0;
    }

    public bool Discard(RuntimeCard card)
    {
        return Rules != null && Rules.DiscardCard(card);
    }

    public bool ReturnToHand(RuntimeCard card)
    {
        return Rules != null && Rules.ReturnCardToHand(card);
    }

    public bool CounterSpell(RuntimeCard card)
    {
        return Rules != null && Rules.CounterSpell(card);
    }

    public bool CastWithoutPayingMana(RuntimeCard card, int featureIndex)
    {
        return Rules != null && Rules.CastCardWithoutPayingMana(card, featureIndex);
    }

    public long GetObjectId(RuntimeCard card)
    {
        return card?.ObjectId ?? 0;
    }

    public int GetZone(RuntimeCard card)
    {
        return (int)(card?.CurrentZone ?? GameZone.None);
    }

    public bool HasType(RuntimeCard card, int cardTypes)
    {
        return card != null && card.HasType((CardType)cardTypes);
    }

    public bool HasSubtype(RuntimeCard card, string subtype)
    {
        return card != null && card.HasSubtype(subtype);
    }

    public bool HasSupertype(RuntimeCard card, int supertype)
    {
        return card?.Characteristics != null
            && card.Characteristics.HasSupertype((CardSupertype)supertype);
    }

    public bool HasKeyword(RuntimeCard card, string keyword)
    {
        return card != null && card.HasKeyword(keyword);
    }

    public int GetCardTypes(RuntimeCard card)
    {
        return (int)(card?.Characteristics?.CardTypes ?? CardType.None);
    }

    public int GetColors(RuntimeCard card)
    {
        return (int)(card?.Characteristics?.GetColors() ?? ManaColorFlags.None);
    }

    public bool IsTapped(RuntimeCard card)
    {
        return card != null && card.IsTapped;
    }

    public int GetManaValue(RuntimeCard card)
    {
        return card?.Characteristics?.ManaCost.Total ?? 0;
    }

    private static bool TryParseZone(string value, out GameZone zone)
    {
        return Enum.TryParse(value, true, out zone) && zone != GameZone.None;
    }

    private static bool TryParseDuration(
        string value,
        out RuntimeEffectDuration duration)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            duration = RuntimeEffectDuration.WhileSourceRemains;
            return true;
        }
        return Enum.TryParse(value, true, out duration);
    }

    public void SetMaxHandSize(int playerID, int value)
    {
        Rules?.SetMaxHandSize(playerID, value);
    }

    public void AddKeyword(RuntimeCard card, string keyword)
    {
        card?.AddKeyword(keyword);
    }

    public void RemoveKeyword(RuntimeCard card, string keyword)
    {
        card?.RemoveKeyword(keyword);
    }

    public bool Buff(RuntimeCard card, int power, int toughness, string source)
    {
        return Rules != null && Rules.AddPowerToughnessModifier(card, power, toughness, source, false);
    }

    public bool BuffUntilEndOfTurn(RuntimeCard card, int power, int toughness, string source)
    {
        return Rules != null && Rules.AddPowerToughnessModifier(card, power, toughness, source, true);
    }

    public bool SetPowerToughness(RuntimeCard card, int power, int toughness, string source)
    {
        return Rules != null && Rules.SetPowerToughness(card, power, toughness, source, false);
    }

    public bool SetPowerToughnessUntilEndOfTurn(RuntimeCard card, int power, int toughness, string source)
    {
        return Rules != null && Rules.SetPowerToughness(card, power, toughness, source, true);
    }

    public bool SwitchPowerToughness(RuntimeCard card)
    {
        return Rules != null && Rules.SwitchPowerToughness(card, false);
    }

    public bool SwitchPowerToughnessUntilEndOfTurn(RuntimeCard card)
    {
        return Rules != null && Rules.SwitchPowerToughness(card, true);
    }

    public long AddCardType(RuntimeCard card, int cardTypes, string source, bool untilEndOfTurn)
    {
        return Rules != null
            ? Rules.AddCardTypeEffect(card, (CardType)cardTypes, false, source, untilEndOfTurn)
            : 0;
    }

    public long RemoveCardType(RuntimeCard card, int cardTypes, string source, bool untilEndOfTurn)
    {
        return Rules != null
            ? Rules.AddCardTypeEffect(card, (CardType)cardTypes, true, source, untilEndOfTurn)
            : 0;
    }

    public long SetColors(RuntimeCard card, int colors, string source, bool untilEndOfTurn)
    {
        return Rules != null
            ? Rules.SetColorEffect(card, (ManaColorFlags)colors, source, untilEndOfTurn)
            : 0;
    }

    public long GrantKeyword(RuntimeCard card, string keyword, string source, bool untilEndOfTurn)
    {
        return Rules != null
            ? Rules.AddKeywordEffect(card, keyword, false, source, untilEndOfTurn)
            : 0;
    }

    public long SuppressKeyword(RuntimeCard card, string keyword, string source, bool untilEndOfTurn)
    {
        return Rules != null
            ? Rules.AddKeywordEffect(card, keyword, true, source, untilEndOfTurn)
            : 0;
    }

    public void Log(string message)
    {
        Debug.Log($"[Lua]: {message}");
    }
}
