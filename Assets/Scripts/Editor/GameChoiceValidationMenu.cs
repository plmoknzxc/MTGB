#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class GameChoiceValidationMenu
{
    [MenuItem("MTGB/Validate Game Choice Model")]
    public static void ValidateFromMenu()
    {
        ValidateForAutomation();
        Debug.Log("MTGB game choice validation passed.");
    }

    public static void ValidateFromCommandLine()
    {
        try
        {
            ValidateForAutomation();
            Debug.Log("MTGB game choice validation passed.");
            EditorApplication.Exit(0);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorApplication.Exit(1);
        }
    }

    public static void ValidateForAutomation()
    {
        var single = new GameChoiceRequest
        {
            requestId = 1,
            kind = GameChoiceKind.LibrarySearch,
            choosingPlayerId = 1,
            minimumSelections = 0,
            maximumSelections = 1
        };
        single.options.Add(new GameChoiceOption(10, GameChoiceZone.Library, 1, 10, true, "A"));
        single.options.Add(new GameChoiceOption(11, GameChoiceZone.Library, 1, 11, true, "B"));
        single.options.Add(new GameChoiceOption(12, GameChoiceZone.Library, 1, 12, false, "C"));

        Require(single.CanConfirm, "An optional search must be confirmable with no selection.");
        Require(single.Toggle(10), "A legal single-choice option must be selectable.");
        Require(single.IsSelected(10), "The first single-choice option was not selected.");
        Require(single.Toggle(11), "Selecting a replacement single-choice option failed.");
        Require(!single.IsSelected(10) && single.IsSelected(11), "Single choice did not replace the previous selection.");
        Require(!single.Toggle(12), "An illegal option was selectable.");
        Require(single.Toggle(11) && single.selectedOptionIds.Count == 0, "Clicking the selected option did not deselect it.");

        var multiple = new GameChoiceRequest
        {
            requestId = 2,
            kind = GameChoiceKind.DiscardToHandSize,
            choosingPlayerId = 1,
            minimumSelections = 2,
            maximumSelections = 2
        };
        multiple.options.Add(new GameChoiceOption(0, GameChoiceZone.Hand, 1, 0, true, "A"));
        multiple.options.Add(new GameChoiceOption(1, GameChoiceZone.Hand, 1, 1, true, "B"));
        multiple.options.Add(new GameChoiceOption(2, GameChoiceZone.Hand, 1, 2, true, "C"));

        Require(!multiple.CanConfirm, "A mandatory multi-choice request confirmed too early.");
        Require(multiple.Toggle(0) && multiple.Toggle(1), "Legal multi-choice selections failed.");
        Require(multiple.CanConfirm, "A complete mandatory selection was not confirmable.");
        Require(!multiple.Toggle(2), "A multi-choice request exceeded its maximum.");
        Require(multiple.Toggle(0) && !multiple.CanConfirm, "Deselecting did not make the mandatory request incomplete.");

        var ordered = new GameChoiceRequest
        {
            requestId = 3,
            kind = GameChoiceKind.TriggerOrder,
            selectionMode = GameChoiceSelectionMode.Ordered,
            choosingPlayerId = 1,
            minimumSelections = 3,
            maximumSelections = 3
        };
        ordered.options.Add(new GameChoiceOption(20, GameChoiceZone.Stack, 1, 0, true, "First"));
        ordered.options.Add(new GameChoiceOption(21, GameChoiceZone.Stack, 1, 1, true, "Second"));
        ordered.options.Add(new GameChoiceOption(22, GameChoiceZone.Stack, 1, 2, true, "Third"));

        Require(ordered.Toggle(22) && ordered.Toggle(20) && ordered.Toggle(21), "Trigger ordering selections failed.");
        Require(ordered.CanConfirm, "A complete trigger order was not confirmable.");
        Require(
            ordered.selectedOptionIds[0] == 22
            && ordered.selectedOptionIds[1] == 20
            && ordered.selectedOptionIds[2] == 21,
            "Trigger ordering did not retain selection order.");
        Require(ordered.MoveSelectedOption(21, 0), "An ordered choice could not move a selected option.");
        Require(
            ordered.selectedOptionIds[0] == 21
            && ordered.selectedOptionIds[1] == 22
            && ordered.selectedOptionIds[2] == 20,
            "Ordered choice movement produced the wrong order.");

        var number = new GameChoiceRequest
        {
            requestId = 4,
            kind = GameChoiceKind.ChooseNumber,
            selectionMode = GameChoiceSelectionMode.Number,
            choosingPlayerId = 1,
            minimumValue = 0,
            maximumValue = 5
        };
        Require(!number.CanConfirm, "An unset number choice was confirmable.");
        Require(number.SetNumber(3) && number.CanConfirm, "A legal number choice was not confirmable.");
        Require(!number.SetNumber(6), "An out-of-range number choice was accepted.");

        var distribution = new GameChoiceRequest
        {
            requestId = 5,
            kind = GameChoiceKind.DistributeAmount,
            selectionMode = GameChoiceSelectionMode.Distribution,
            choosingPlayerId = 1,
            minimumSelections = 1,
            maximumSelections = 2,
            requiredTotalAmount = 3
        };
        distribution.options.Add(new GameChoiceOption(
            30,
            GameChoiceZone.Battlefield,
            1,
            0,
            true,
            "First target",
            1,
            3));
        distribution.options.Add(new GameChoiceOption(
            31,
            GameChoiceZone.Player,
            2,
            -1,
            true,
            "Second target",
            1,
            3));
        Require(distribution.SetAllocatedAmount(30, 2), "A legal distribution amount was rejected.");
        Require(!distribution.CanConfirm, "A partial distribution was confirmable.");
        Require(distribution.SetAllocatedAmount(31, 1), "The remaining distribution amount was rejected.");
        Require(distribution.CanConfirm, "A complete distribution was not confirmable.");
        Require(distribution.CreateResult()?.allocations.Count == 2, "Distribution result snapshot was incomplete.");

        var repeated = new GameChoiceRequest
        {
            requestId = 6,
            kind = GameChoiceKind.RepeatOptions,
            selectionMode = GameChoiceSelectionMode.Repeated,
            choosingPlayerId = 1,
            minimumSelections = 2,
            maximumSelections = 4
        };
        repeated.options.Add(new GameChoiceOption(
            40,
            GameChoiceZone.None,
            0,
            -1,
            true,
            "Repeatable",
            0,
            3));
        repeated.options.Add(new GameChoiceOption(
            41,
            GameChoiceZone.None,
            0,
            -1,
            true,
            "Other",
            0,
            2));
        Require(repeated.Toggle(40), "A repeatable option could not be selected.");
        Require(repeated.Toggle(40), "A repeatable option could not be selected twice.");
        Require(
            repeated.GetSelectionCount(40) == 2 && repeated.CanConfirm,
            "Repeated selection count or confirmation was incorrect.");
        Require(repeated.Toggle(40), "A repeatable option could not reach its per-option maximum.");
        Require(
            repeated.Toggle(40)
            && repeated.GetSelectionCount(40) == 0
            && !repeated.CanConfirm,
            "Cycling a repeatable option did not clear its repetitions.");
        Require(
            repeated.Toggle(40)
            && repeated.Toggle(40)
            && repeated.Toggle(41)
            && repeated.Toggle(41)
            && repeated.selectedOptionIds.Count == repeated.maximumSelections,
            "Repeated selection could not reach its global maximum.");
        Require(
            repeated.Toggle(40)
            && repeated.GetSelectionCount(40) == 0,
            "A repeated selection could not be cleared at the global maximum.");

        var advancedTargetGroup = new SpellTargetGroupDefinition(
            "related",
            "Choose up to two other blue creatures.",
            SpellTargetKind.Permanent | SpellTargetKind.Card,
            SpellTargetZone.Battlefield | SpellTargetZone.Graveyard,
            SpellTargetControllerRelation.Opponent,
            CardType.Creature,
            ManaColorFlags.Blue,
            0,
            2,
            "CanChooseRelated",
            true,
            SpellTargetRelation.Another
                | SpellTargetRelation.DistinctAcrossGroups
                | SpellTargetRelation.SharesCardType,
            "first");
        Require(
            advancedTargetGroup.MinimumTargets == 0
            && advancedTargetGroup.MaximumTargets == 2
            && advancedTargetGroup.Allows(SpellTargetKind.Permanent)
            && advancedTargetGroup.Allows(GameZone.Graveyard)
            && advancedTargetGroup.ControllerRelation
                == SpellTargetControllerRelation.Opponent
            && advancedTargetGroup.RequiredCardTypes.Has(CardType.Creature)
            && advancedTargetGroup.RequiredColors == ManaColorFlags.Blue,
            "Advanced target-group constraints were not retained.");

        GameObject rulesObject = new GameObject("Choice Validation Rules");
        try
        {
            GameRules rules = rulesObject.AddComponent<GameRules>();
            bool choiceCompleted = false;
            GameChoiceRequest runtimeChoice = rules.BeginOptionChoice(
                GameChoiceKind.ChooseMode,
                1,
                "Choose one",
                1,
                1,
                true,
                new[]
                {
                    new GameChoiceOption(1, GameChoiceZone.None, 0, -1, true, "A"),
                    new GameChoiceOption(2, GameChoiceZone.None, 0, -1, true, "B")
                },
                result => choiceCompleted = result.selectedOptionIds.Count == 1
                    && result.selectedOptionIds[0] == 2);
            Require(runtimeChoice != null, "A runtime generic choice could not start.");
            Require(
                !rules.ApplyAction(new GameAction
                {
                    type = GameActionType.PassPriority,
                    playerId = 1
                }).accepted,
                "An unrelated gameplay action was accepted during an active choice.");
            Require(
                rules.ApplyAction(new GameAction
                {
                    type = GameActionType.SetFullControl,
                    playerId = 1,
                    value = 1
                }).accepted,
                "A non-choice full-control toggle was blocked during an active choice.");
            Require(
                rules.ApplyAction(new GameAction
                {
                    type = GameActionType.ToggleChoiceOption,
                    playerId = 1,
                    choiceRequestId = runtimeChoice.requestId,
                    optionId = 2
                }).accepted,
                "A runtime generic choice option could not be selected.");
            Require(
                rules.TryConfirmActiveChoice(1) && choiceCompleted,
                "A runtime generic choice did not invoke its completion.");
            Require(
                rules.GetLastCompletedChoice()?.selectedOptionIds[0] == 2,
                "A completed runtime choice did not retain its result.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(rulesObject);
        }

        var creatureData = new CardDefinition
        {
            cardName = "Layer Test",
            cardTypes = CardType.Creature,
            attack = 2,
            defence = 3
        };
        var creature = new RuntimeCard(creatureData, 1, false);
        creature.AssignObjectId(9001);
        creature.SetCurrentZone(GameZone.Battlefield);
        SpellTargetReference stableTarget = SpellTargetReference.ForCard(creature);
        Require(
            stableTarget != null
            && stableTarget.HasCurrentCardIdentity
            && stableTarget.DeclaredZone == GameZone.Battlefield,
            "A card target did not capture stable object identity and declared zone.");
        creature.MarkZoneChange();
        Require(
            !stableTarget.HasCurrentCardIdentity,
            "A target reference survived a zone change as the same object.");

        GameStackItem stackItem = GameStackItem.ActivatedAbility(creature, "OnResolve");
        Require(
            stackItem.AssignStackObjectId(77)
            && !stackItem.AssignStackObjectId(78),
            "A stack object did not receive exactly one stable identity.");
        SpellTargetReference stackTarget = SpellTargetReference.ForStackItem(stackItem);
        Require(
            stackTarget != null
            && stackTarget.IsStackTarget
            && stackTarget.StackObjectId == 77,
            "A stack target did not capture stable stack identity.");

        var optionalTargetDeclaration = new SpellCastDeclaration(
            creature,
            1,
            1,
            GameChoiceZone.Hand,
            0,
            null);
        Require(
            !optionalTargetDeclaration.HasTargets,
            "A declaration with no chosen optional target reported a target.");
        optionalTargetDeclaration.ReplaceTargets(
            "optional",
            new[] { stackTarget });
        Require(
            optionalTargetDeclaration.HasTargets,
            "A declaration did not report its chosen target.");

        creature.SetPowerToughness(4, 5, "set", true);
        creature.AddPowerToughnessModifier(1, -1, "modify", true);
        creature.AddCounters("+1/+1", 2);
        creature.AddCounters("-1/-1", 1);
        Require(creature.CurrentAttack == 6 && creature.CurrentDefence == 5, "Power/toughness layers were applied out of order.");
        Require(creature.CancelOpposingPowerToughnessCounters(), "Opposing power/toughness counters did not cancel.");
        Require(
            creature.GetCounters("+1/+1") == 1 && creature.GetCounters("-1/-1") == 0,
            "Power/toughness counter cancellation produced the wrong totals.");
        creature.SwitchPowerToughness(true);
        Require(creature.CurrentAttack == 5 && creature.CurrentDefence == 6, "Power/toughness switching was not applied last.");
        creature.ClearEndOfTurnEffects();
        Require(creature.CurrentAttack == 3 && creature.CurrentDefence == 4, "End-of-turn effects were not removed.");

        var layeredPermanentData = new CardDefinition
        {
            cardName = "Characteristic Layer Test",
            cardTypes = CardType.Enchantment,
            colorIndicator = ManaColorFlags.White
        };
        var layeredPermanent = new RuntimeCard(layeredPermanentData, 1, false);
        layeredPermanent.AddKeyword("Flying");
        layeredPermanent.AddCardTypeEffect(CardType.Creature, false, "animate", true);
        layeredPermanent.SetColorEffect(ManaColorFlags.Blue, "first color", true);
        layeredPermanent.SetColorEffect(ManaColorFlags.Red, "later color", true);
        layeredPermanent.AddKeywordEffect("Flying", true, "lose flying", true);
        layeredPermanent.AddKeywordEffect("Haste", false, "gain haste", true);
        Require(
            layeredPermanent.HasType(CardType.Enchantment)
            && layeredPermanent.HasType(CardType.Creature),
            "Layer 4 type-changing effects were not combined with printed types.");
        Require(
            layeredPermanent.GetColors() == ManaColorFlags.Red,
            "Layer 5 effects did not use timestamp order.");
        Require(
            !layeredPermanent.HasKeyword("Flying") && layeredPermanent.HasKeyword("Haste"),
            "Layer 6 ability adding/removing effects were applied incorrectly.");
        layeredPermanent.ClearEndOfTurnEffects();
        Require(
            layeredPermanent.GetCardTypes() == CardType.Enchantment
            && layeredPermanent.GetColors() == ManaColorFlags.White
            && layeredPermanent.HasKeyword("Flying")
            && !layeredPermanent.HasKeyword("Haste"),
            "Until-end-of-turn continuous effects did not expire cleanly.");

        var battlefield = new List<RuntimeCard>();
        var graveyard = new List<RuntimeCard>();
        var firstDyingCreature = new RuntimeCard(creatureData, 1, false);
        var secondDyingCreature = new RuntimeCard(creatureData, 1, false);
        battlefield.Add(firstDyingCreature);
        battlefield.Add(secondDyingCreature);
        firstDyingCreature.TakeDamage(3);
        secondDyingCreature.TakeDamage(3);
        int deathEvents = 0;
        bool observedWholeBatch = true;
        Action<RuntimeCard> deathListener = _ =>
        {
            deathEvents++;
            observedWholeBatch &= battlefield.Count == 2;
        };
        GameEvents.OnCreatureDied += deathListener;
        try
        {
            Require(
                StateBasedActionProcessor.ProcessCreatureStateBasedActions(battlefield, graveyard),
                "Lethal damage did not perform a state-based action.");
        }
        finally
        {
            GameEvents.OnCreatureDied -= deathListener;
        }

        Require(deathEvents == 2, "Simultaneous lethal damage did not produce both death events.");
        Require(observedWholeBatch, "A dying creature left the battlefield before the simultaneous death batch completed.");
        Require(battlefield.Count == 0 && graveyard.Count == 2, "The simultaneous death batch moved cards incorrectly.");

        var targetSource = new RuntimeCard(creatureData, 1, false);
        var originalTarget = new RuntimeCard(creatureData, 2, false);
        var secondTarget = new RuntimeCard(creatureData, 2, false);
        targetSource.SetSpellTarget(originalTarget);
        targetSource.AddSpellTarget(secondTarget);
        Require(targetSource.HasCurrentTargetCardIdentity, "A newly selected target identity was stale.");
        originalTarget.MarkZoneChange();
        Require(
            !targetSource.HasCurrentTargetCardIdentity,
            "A target that changed zones retained its old object identity.");
        Require(
            targetSource.SpellTargets.Count == 2
            && targetSource.SpellTargets[1].HasCurrentCardIdentity
            && targetSource.HasSpellTarget,
            "One illegal target incorrectly invalidated every target of a multi-target spell.");
        targetSource.MarkZoneChange();
        Require(
            targetSource.SpellTargets.Count == 0,
            "A spell retained target identities across its own zone change.");

        var controlledTarget = new RuntimeCard(creatureData, 1, false);
        controlledTarget.AddKeyword("Hexproof");
        Require(
            controlledTarget.SetController(2),
            "A permanent could not change controller.");
        Require(
            !TargetingRules.CanTargetCard(targetSource, controlledTarget, true),
            "Hexproof used ownership instead of control when checking an opposing source.");
        GameStackItem controlledTrigger = GameStackItem.TriggeredAbility(controlledTarget, "OnTest");
        Require(
            controlledTrigger.ControllerPlayerId == 2,
            "A triggered ability was not controlled by its source's controller.");
        controlledTarget.MarkZoneChange();
        Require(
            controlledTarget.ControllerPlayerID == controlledTarget.OwnerPlayerID,
            "A zone change did not reset an object's controller to its owner.");

        var damageEvent = new DamageEventRuntime(
            targetSource,
            secondTarget,
            0,
            3,
            DamageEventKind.Noncombat);
        var doubleDamage = new DamageReplacementRuntime(
            1,
            1,
            targetSource,
            DamageReplacementScope.SourceDealsDamage,
            DamageReplacementOperation.Multiply,
            targetSource,
            0,
            2,
            false,
            true);
        Require(
            doubleDamage.AppliesTo(damageEvent, _ => true),
            "An outgoing damage replacement did not match its source.");
        damageEvent.Apply(doubleDamage);
        Require(damageEvent.Amount == 6, "A damage multiplier produced the wrong result.");
        var preventDamage = new DamageReplacementRuntime(
            2,
            2,
            targetSource,
            DamageReplacementScope.CardIsDealtDamage,
            DamageReplacementOperation.Prevent,
            secondTarget,
            0,
            4,
            false,
            true);
        damageEvent.Apply(preventDamage);
        Require(damageEvent.Amount == 2, "A partial damage prevention produced the wrong result.");

        var structuredData = new CardDefinition
        {
            cardName = "Structured V7 Test",
            cardTypes = CardType.Creature,
            features = new List<CardFeatureDefinition>
            {
                new CardFeatureDefinition
                {
                    faceIndex = 0,
                    featureIndex = 0,
                    role = CardFeatureRole.Primary,
                    cardName = "Primary Half",
                    cardTypes = CardType.Creature,
                    subtypes = "Room",
                    attack = 2,
                    defence = 2
                },
                new CardFeatureDefinition
                {
                    faceIndex = 0,
                    featureIndex = 1,
                    role = CardFeatureRole.Adventure,
                    cardName = "Adventure Half",
                    cardTypes = CardType.Sorcery,
                    subtypes = "Adventure"
                },
                new CardFeatureDefinition
                {
                    faceIndex = 0,
                    featureIndex = 2,
                    role = CardFeatureRole.Omen,
                    cardName = "Omen Half",
                    cardTypes = CardType.Sorcery,
                    subtypes = "Omen"
                },
                new CardFeatureDefinition
                {
                    faceIndex = 0,
                    featureIndex = 3,
                    role = CardFeatureRole.Prepare,
                    cardName = "Prepare Half",
                    cardTypes = CardType.Sorcery,
                    subtypes = "Prepare"
                },
                new CardFeatureDefinition
                {
                    faceIndex = 0,
                    featureIndex = 4,
                    role = CardFeatureRole.RoomDoor,
                    roleValue = "Right Door",
                    cardName = "Right Door",
                    cardTypes = CardType.Enchantment,
                    subtypes = "Room"
                },
                new CardFeatureDefinition
                {
                    faceIndex = 0,
                    featureIndex = 5,
                    role = CardFeatureRole.SplitPart,
                    cardName = "Second Half",
                    cardTypes = CardType.Instant,
                    subtypes = "Split"
                },
                new CardFeatureDefinition
                {
                    faceIndex = 0,
                    featureIndex = 6,
                    role = CardFeatureRole.ClassLevel,
                    roleValue = "2",
                    cardName = "Level Two",
                    cardTypes = CardType.Enchantment,
                    subtypes = "Class"
                },
                new CardFeatureDefinition
                {
                    faceIndex = 0,
                    featureIndex = 7,
                    role = CardFeatureRole.CaseStage,
                    roleValue = "To Solve",
                    cardName = "Case Condition",
                    cardTypes = CardType.Enchantment,
                    subtypes = "Case"
                },
                new CardFeatureDefinition
                {
                    faceIndex = 0,
                    featureIndex = 8,
                    role = CardFeatureRole.CaseStage,
                    roleValue = "Solved",
                    cardName = "Solved Case",
                    cardTypes = CardType.Enchantment,
                    subtypes = "Case"
                }
            }
        };
        var structuredCard = new RuntimeCard(structuredData, 1, false);
        CardFeatureRuntime primaryFeature = structuredCard.FindFeature(0, 0);
        CardFeatureRuntime adventureFeature = structuredCard.FindFeature(0, 1);
        CardFeatureRuntime omenFeature = structuredCard.FindFeature(0, 2);
        CardFeatureRuntime prepareFeature = structuredCard.FindFeature(0, 3);
        CardFeatureRuntime roomFeature = structuredCard.FindFeature(0, 4);
        CardFeatureRuntime splitFeature = structuredCard.FindFeature(0, 5);
        CardFeatureRuntime classFeature = structuredCard.FindFeature(0, 6);
        CardFeatureRuntime caseCondition = structuredCard.FindFeature(0, 7);
        CardFeatureRuntime solvedCase = structuredCard.FindFeature(0, 8);
        Require(
            primaryFeature.CanCastFromHand(true, true)
            && adventureFeature.CanCastFromHand(true, true)
            && omenFeature.CanCastFromHand(true, true)
            && roomFeature.CanCastFromHand(true, true)
            && splitFeature.CanCastFromHand(true, true),
            "A V7 castable feature region was incorrectly hidden.");
        Require(
            !prepareFeature.CanCastFromHand(true, true)
            && !classFeature.CanCastFromHand(true, true)
            && !caseCondition.CanCastFromHand(true, true),
            "A V7 non-spell feature region was incorrectly castable.");
        Require(
            !structuredCard.IsFeatureActive(primaryFeature)
            && structuredCard.UnlockRoomFeature(primaryFeature)
            && structuredCard.IsFeatureActive(primaryFeature),
            "The V7 primary room door did not unlock as the first door.");
        Require(
            structuredCard.UnlockRoomFeature(roomFeature)
            && structuredCard.IsFeatureActive(roomFeature),
            "An additional V7 room door did not unlock.");
        Require(
            structuredCard.SetClassLevel(2)
            && structuredCard.IsFeatureActive(classFeature),
            "A V7 class level did not become active.");
        Require(
            structuredCard.IsFeatureActive(caseCondition)
            && !structuredCard.IsFeatureActive(solvedCase),
            "The unsolved V7 case stage was incorrect.");
        structuredCard.SolveCase();
        Require(
            !structuredCard.IsFeatureActive(caseCondition)
            && structuredCard.IsFeatureActive(solvedCase),
            "The solved V7 case stage was incorrect.");
        structuredCard.MarkAdventureResolved();
        Require(
            structuredCard.CanCastPrimaryFromAdventureExile
            && primaryFeature.CanCastFromAdventureExile(),
            "Adventure resolution did not enable the primary feature from exile.");

        var preparedSource = new RuntimeCard(structuredData, 1, false);
        var preparedCopy = new RuntimeCard(structuredData, 1, false);
        preparedCopy.MarkAsPreparedCopy(preparedSource);
        Require(
            preparedSource.IsPrepared
            && ReferenceEquals(preparedSource.PreparedCopy, preparedCopy)
            && ReferenceEquals(preparedCopy.PreparedSource, preparedSource)
            && prepareFeature.CanCastAsPreparedCopy(),
            "A V7 prepare source/copy link was not established.");
        preparedCopy.ClearPreparedLink();
        Require(
            !preparedSource.IsPrepared
            && preparedSource.PreparedCopy == null
            && preparedCopy.PreparedSource == null,
            "A V7 prepare source/copy link was not cleared.");

        var sagaDefinition = new CardDefinition
        {
            cardName = "Saga Test",
            cardTypes = CardType.Enchantment,
            features = new List<CardFeatureDefinition>
            {
                new CardFeatureDefinition
                {
                    faceIndex = 0,
                    featureIndex = 0,
                    role = CardFeatureRole.Primary,
                    cardName = "Saga Test",
                    cardTypes = CardType.Enchantment,
                    subtypes = "Saga",
                    ruleEntries = new List<CardRuleEntryDefinition>
                    {
                        new CardRuleEntryDefinition
                        {
                            order = 0,
                            kind = CardRuleEntryKind.Chapter,
                            chapterNumbers = "1, 3",
                            text = "First and third chapter."
                        },
                        new CardRuleEntryDefinition
                        {
                            order = 1,
                            kind = CardRuleEntryKind.Chapter,
                            chapterNumbers = "2",
                            text = "Second chapter."
                        }
                    }
                }
            }
        };
        var saga = new RuntimeCard(sagaDefinition, 1, false);
        Require(
            saga.PrimaryFeature.IsSaga
            && saga.PrimaryFeature.GetFinalChapterNumber() == 3,
            "V7 Saga chapter entries were not parsed correctly.");

        var token = new RuntimeCard(creatureData, 1, false);
        token.MarkAsToken();
        int tokenZoneVersion = token.ZoneChangeVersion;
        token.MarkZoneChange();
        Require(
            token.IsToken && token.ZoneChangeVersion == tokenZoneVersion + 1,
            "A token lost its token identity during a zone change.");

        var returningCreature = new RuntimeCard(creatureData, 1, false);
        returningCreature.AddCounters("+1/+1", 2);
        returningCreature.AddPowerToughnessModifier(3, 3, "old object");
        returningCreature.TakeDamage(1);
        returningCreature.Tap();
        returningCreature.Die();
        returningCreature.MarkZoneChange();
        Require(
            !returningCreature.IsDead
            && !returningCreature.IsTapped
            && returningCreature.DamageTaken == 0
            && returningCreature.GetCounters("+1/+1") == 0
            && returningCreature.CurrentAttack == 2
            && returningCreature.CurrentDefence == 3,
            "A zone change retained state from the object's previous identity.");

        var auraData = new CardDefinition
        {
            cardName = "Aura Test",
            cardTypes = CardType.Enchantment,
            subtypes = "Aura",
            targetMode = CardTargetMode.AnyCreature
        };
        var aura = new RuntimeCard(auraData, 1, false);
        var enchantedCreature = new RuntimeCard(creatureData, 2, false);
        var firstBattlefield = new List<RuntimeCard> { aura };
        var secondBattlefield = new List<RuntimeCard> { enchantedCreature };
        var firstGraveyard = new List<RuntimeCard>();
        var secondGraveyard = new List<RuntimeCard>();
        Require(aura.AttachTo(enchantedCreature), "A legal Aura could not attach.");
        secondBattlefield.Remove(enchantedCreature);
        enchantedCreature.MarkZoneChange();
        Require(
            StateBasedActionProcessor.ProcessAttachmentStateBasedActions(
                firstBattlefield,
                secondBattlefield,
                firstGraveyard,
                secondGraveyard),
            "An unattached Aura did not perform a state-based action.");
        Require(
            firstBattlefield.Count == 0 && firstGraveyard.Count == 1,
            "An unattached Aura did not move to its owner's graveyard.");

        var planeswalker = new RuntimeCard(
            new CardDefinition
            {
                cardName = "Zero Loyalty",
                cardTypes = CardType.Planeswalker,
                loyalty = 0
            },
            1,
            false);
        var battle = new RuntimeCard(
            new CardDefinition
            {
                cardName = "Defeated Battle",
                cardTypes = CardType.Battle,
                battleDefense = 0
            },
            2,
            false);
        var unattachedAura = new RuntimeCard(auraData, 1, false);
        var equipment = new RuntimeCard(
            new CardDefinition
            {
                cardName = "Loose Equipment",
                cardTypes = CardType.Artifact,
                subtypes = "Equipment",
                targetMode = CardTargetMode.AnyCreature
            },
            1,
            false);
        var missingEquipmentTarget = new RuntimeCard(creatureData, 1, false);
        Require(
            equipment.AttachTo(missingEquipmentTarget),
            "Equipment setup failed before the state-based action test.");

        var p1Battlefield = new List<RuntimeCard>
        {
            planeswalker,
            unattachedAura,
            equipment
        };
        var p2Battlefield = new List<RuntimeCard> { battle };
        var p1Graveyard = new List<RuntimeCard>();
        var p2Graveyard = new List<RuntimeCard>();
        Require(
            StateBasedActionProcessor.ProcessBattlefieldStateBasedActions(
                p1Battlefield,
                p2Battlefield,
                p1Graveyard,
                p2Graveyard,
                _ => false),
            "Special-permanent state-based actions did not produce a batch.");
        Require(
            p1Graveyard.Contains(planeswalker)
            && p1Graveyard.Contains(unattachedAura)
            && p2Graveyard.Contains(battle),
            "Planeswalker, Aura, or Battle state-based actions moved cards incorrectly.");
        Require(
            p1Battlefield.Contains(equipment) && equipment.AttachedTo == null,
            "Equipment was put into a graveyard instead of becoming unattached.");

        GameObject boundaryRulesObject = new GameObject("Choice Boundary Validation Rules");
        try
        {
            GameRules boundaryRules = boundaryRulesObject.AddComponent<GameRules>();
            Require(
                boundaryRules.CanPlayerInspectZone(1, 1, GameZone.Hand)
                && !boundaryRules.CanPlayerInspectZone(1, 2, GameZone.Hand)
                && !boundaryRules.CanPlayerInspectZone(1, 1, GameZone.Library)
                && boundaryRules.CanPlayerInspectZone(1, 2, GameZone.Graveyard),
                "Hidden and public zones did not enforce viewer permissions.");

            var attacker = new RuntimeCard(
                new CardDefinition
                {
                    cardName = "Trample Attacker",
                    cardTypes = CardType.Creature,
                    attack = 7,
                    defence = 7
                },
                1,
                false);
            attacker.ReassignObjectId(9701);
            attacker.SetCurrentZone(GameZone.Battlefield);
            attacker.AddKeyword("Trample");
            attacker.DeclareAttack(2);
            var firstBlocker = new RuntimeCard(
                new CardDefinition
                {
                    cardName = "First Blocker",
                    cardTypes = CardType.Creature,
                    attack = 1,
                    defence = 2
                },
                2,
                false);
            firstBlocker.ReassignObjectId(9702);
            firstBlocker.SetCurrentZone(GameZone.Battlefield);
            var secondBlocker = new RuntimeCard(
                new CardDefinition
                {
                    cardName = "Second Blocker",
                    cardTypes = CardType.Creature,
                    attack = 1,
                    defence = 3
                },
                2,
                false);
            secondBlocker.ReassignObjectId(9703);
            secondBlocker.SetCurrentZone(GameZone.Battlefield);

            var p1 = (List<RuntimeCard>)typeof(GameRules).GetField(
                "player1Battlefield",
                BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(boundaryRules);
            var p2 = (List<RuntimeCard>)typeof(GameRules).GetField(
                "player2Battlefield",
                BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(boundaryRules);
            Require(p1 != null && p2 != null, "Combat validation battlefields were unavailable.");
            p1.Add(attacker);
            p2.Add(firstBlocker);
            p2.Add(secondBlocker);

            MethodInfo beginDamageChoice = typeof(GameRules).GetMethod(
                "BeginCombatDamageAssignmentChoice",
                BindingFlags.Instance | BindingFlags.NonPublic);
            bool began = beginDamageChoice != null
                && (bool)beginDamageChoice.Invoke(
                    boundaryRules,
                    new object[]
                    {
                        attacker,
                        new List<RuntimeCard> { firstBlocker, secondBlocker },
                        new Action<GameChoiceResult>(_ => { })
                    });
            GameChoiceRequest damageChoice = boundaryRules.GetActiveChoice();
            Require(
                began
                && damageChoice != null
                && damageChoice.kind == GameChoiceKind.CombatDamageAssignment,
                "Manual combat-damage assignment did not create a choice.");
            Require(
                damageChoice.SetAllocatedAmount(9299999, 7)
                && damageChoice.CanConfirm
                && !boundaryRules.CanConfirmActiveChoice(),
                "Trample damage was allowed to pass blockers before lethal assignment.");
            Require(damageChoice.SetAllocatedAmount(9299999, 0), "The invalid trample allocation could not be reset.");
            Require(
                damageChoice.SetAllocatedAmount(9200000, 2)
                && damageChoice.SetAllocatedAmount(9200001, 3)
                && damageChoice.SetAllocatedAmount(9299999, 2)
                && boundaryRules.CanConfirmActiveChoice(),
                "A legal manual trample assignment was not confirmable.");
            Require(
                boundaryRules.TryConfirmActiveChoice(1),
                "A legal manual combat-damage assignment did not complete.");

            ValidateBattleDefeatAndFreeBackFaceCast(boundaryRules, p1);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(boundaryRulesObject);
        }
    }

    private static void ValidateBattleDefeatAndFreeBackFaceCast(
        GameRules rules,
        List<RuntimeCard> controllerBattlefield)
    {
        typeof(GameRules).GetField(
            "player1Life",
            BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(rules, 20);
        typeof(GameRules).GetField(
            "player2Life",
            BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(rules, 20);
        typeof(GameRules).GetField(
            "currentPlayer",
            BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(rules, 1);

        var battle = new RuntimeCard(
            new CardDefinition
            {
                cardName = "Captured Siege",
                cardTypes = CardType.Battle,
                battleDefense = 3,
                features = new List<CardFeatureDefinition>
                {
                    new CardFeatureDefinition
                    {
                        faceIndex = 0,
                        featureIndex = 0,
                        role = CardFeatureRole.Primary,
                        cardName = "Captured Siege",
                        cardTypes = CardType.Battle,
                        battleDefense = 3
                    },
                    new CardFeatureDefinition
                    {
                        faceIndex = 1,
                        featureIndex = 0,
                        role = CardFeatureRole.Primary,
                        cardName = "Liberated Victor",
                        cardTypes = CardType.Creature,
                        manaCost = "{8}{U}",
                        attack = 5,
                        defence = 5
                    }
                }
            },
            2,
            false);
        battle.ReassignObjectId(9710);
        battle.SetController(1);
        battle.SetCurrentZone(GameZone.Battlefield);
        controllerBattlefield.Add(battle);
        Require(
            battle.CurrentBattleDefense == 3,
            $"Battle setup started with {battle.CurrentBattleDefense} defense instead of 3.");

        Require(
            rules.AddCounters(battle, "defense", -3)
            && battle.CurrentBattleDefense == 0,
            "Removing the final defense counter did not defeat a battle.");

        MethodInfo checkpoint = typeof(GameRules).GetMethod(
            "RunPriorityCheckpoint",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Require(
            checkpoint != null && (bool)checkpoint.Invoke(rules, null),
            "The battle defeat trigger was not processed at the priority checkpoint.");
        Require(
            controllerBattlefield.Contains(battle)
            && rules.GetStackCount() == 1,
            "A defeated battle left the battlefield before its intrinsic trigger resolved.");
        Require(
            rules.ResolveTopOfStack(),
            "The intrinsic battle defeat ability did not resolve.");

        GameChoiceRequest castBackFaceChoice = rules.GetActiveChoice();
        Require(
            castBackFaceChoice != null
            && castBackFaceChoice.kind == GameChoiceKind.SelectOptions
            && castBackFaceChoice.choosingPlayerId == 1
            && rules.GetExileCards(2).Count == 1
            && ReferenceEquals(rules.GetExileCards(2)[0], battle),
            "A stolen battle was not exiled to its owner's zone with its controller choosing the free cast.");
        Require(
            rules.TryToggleChoiceOption(1, castBackFaceChoice.options[0].optionId)
            && rules.TryConfirmActiveChoice(1),
            "The controller could not choose to cast the defeated battle's back face.");

        FieldInfo pendingCastField = typeof(GameRules).GetField(
            "pendingCast",
            BindingFlags.Instance | BindingFlags.NonPublic);
        var pendingCast = pendingCastField?.GetValue(rules) as SpellCastDeclaration;
        Require(
            pendingCast != null
            && pendingCast.PlayerId == 1
            && pendingCast.SourcePlayerId == 2
            && pendingCast.WithoutPayingManaCost
            && battle.CurrentFaceIndex == 1
            && rules.GetActiveChoice()?.kind == GameChoiceKind.CastConfirmation,
            "The back face did not enter the normal free-cast declaration flow.");
        Require(
            rules.TryConfirmActiveChoice(1)
            && rules.GetTopStackCard() == battle
            && battle.ControllerPlayerID == 1
            && battle.CurrentZone == GameZone.Stack,
            "The defeated battle's back face was not cast by its trigger controller.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
#endif
