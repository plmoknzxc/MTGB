#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class GameDeterminismValidationMenu
{
    [MenuItem("MTGB/Validate Deterministic Rules")]
    public static void ValidateFromMenu()
    {
        ValidateForAutomation();
        Debug.Log("MTGB deterministic rules validation passed.");
    }

    public static void ValidateForAutomation()
    {
        ValidateRandomPlayOrderBeginsMulligan();
        ValidateLondonMulliganFlow();
        ValidateStackSnapshotCopy();
        ValidateEventTypeHierarchy();
        ValidateLuaRuntimeBasics();
        ValidateLuaStructuredFeatureHooks();
        ValidateReplayWithoutBattleUi();
        ValidateZeroAttackerDeclarationAdvancesCombat();
        ValidateChoiceActionsWithoutBattleUi();
        ValidateZoneReplacementBeforeMove();
        ValidateManaCostDeclarations();
        ValidateManaAbilityQualification();
        ValidateReplacementOrderingAndLinkedCleanup();
        ValidateContinuousEffectDependencies();
        ValidateRuleBoundaryExtensions();
        ValidateAtomicMultiCardZoneMove();
        ValidateLuaResourceQuotas();
    }

    private static void ValidateLondonMulliganFlow()
    {
        GameObject rulesObject = new GameObject("London Mulligan Validation Rules");
        try
        {
            ConfigureValidationMatch(FindStartingPlayerSeed(1));
            GameRules rules = rulesObject.AddComponent<GameRules>();
            MethodInfo initialize = typeof(GameRules).GetMethod(
                "InitializeGame",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Require(initialize != null, "GameRules.InitializeGame was not found.");
            initialize.Invoke(rules, null);
            string openingState = rules.BuildDeterministicStateString();

            SubmitOpeningChoice(rules, 2);
            Require(rules.GetOpeningMulliganCount(1) == 1
                && rules.GetHandCount(1) == rules.startingHandSize,
                "A London mulligan did not redraw seven cards and record its count.");
            Require(rules.GetDeckCount(1) + rules.GetHandCount(1) == 20,
                "A London mulligan changed the player's total card count.");

            SubmitOpeningChoice(rules, 1);
            GameChoiceRequest bottomChoice = rules.GetActiveChoice();
            Require(bottomChoice?.kind == GameChoiceKind.MulliganBottom
                && bottomChoice.minimumSelections == 1
                && bottomChoice.maximumSelections == 1,
                "Keeping after one mulligan did not request one card for the library bottom.");
            Require(bottomChoice.options.Count == rules.startingHandSize,
                "The bottoming choice did not expose the current opening hand.");
            SubmitOpeningChoice(rules, bottomChoice.options[0].optionId);

            Require(!rules.IsOpeningMulligan()
                && rules.GetHandCount(1) == rules.startingHandSize - 1,
                $"The opening mulligan did not finish with the correct hand size " +
                $"(opening={rules.IsOpeningMulligan()}, hand={rules.GetHandCount(1)}, " +
                $"library={rules.GetDeckCount(1)}, first={rules.GetStartingPlayer()}, " +
                $"choice={rules.GetActiveChoice()?.kind.ToString() ?? "none"}).");
            Require(rules.GetCurrentPlayer() == rules.GetStartingPlayer()
                && rules.GetCurrentPhase() == TurnPhase.Main1
                && rules.IsAwaitingPriority(),
                "The randomly selected starting player did not begin the first turn.");

            GameReplay replay = rules.GetReplay().Clone();
            string expectedHash = rules.ComputeDeterministicStateHash();
            MatchStartConfig.SetOnlineRules(20, 3 - replay.startingPlayer);
            MatchStartConfig.SetDeterministicSeed(unchecked(replay.randomSeed ^ 0x13579BDF));
            GameReplayResult replayResult = rules.ReplayFromInitialState(replay);
            Require(replayResult.succeeded && replayResult.finalStateHash == expectedHash,
                (replayResult.error ?? "The opening mulligan replay was not deterministic.")
                + BuildFirstDifference(openingState, rules.BuildDeterministicStateString()));
        }
        finally
        {
            MatchStartConfig.ClearOnlineSession();
            UnityEngine.Object.DestroyImmediate(rulesObject);
        }
    }

    private static string BuildFirstDifference(string expected, string actual)
    {
        int count = Math.Min(expected?.Length ?? 0, actual?.Length ?? 0);
        int index = 0;
        while (index < count && expected[index] == actual[index]) index++;
        int start = Math.Max(0, index - 40);
        int expectedLength = Math.Min(120, (expected?.Length ?? 0) - start);
        int actualLength = Math.Min(120, (actual?.Length ?? 0) - start);
        return $" First difference at {index}. Expected '{expected?.Substring(start, expectedLength)}', actual '{actual?.Substring(start, actualLength)}'.";
    }

    private static void SubmitOpeningChoice(GameRules rules, int optionId)
    {
        GameChoiceRequest choice = rules.GetActiveChoice();
        Require(choice != null && choice.choosingPlayerId == 1,
            "The local player did not receive the expected opening choice.");
        GameActionResult selected = rules.ApplyAction(new GameAction
        {
            type = GameActionType.ToggleChoiceOption,
            playerId = 1,
            choiceRequestId = choice.requestId,
            optionId = optionId,
            expectedStateSequence = rules.GetAppliedActionSequence()
        });
        Require(selected.accepted, selected.error);
        GameActionResult confirmed = rules.ApplyAction(new GameAction
        {
            type = GameActionType.ConfirmChoice,
            playerId = 1,
            choiceRequestId = choice.requestId,
            expectedStateSequence = rules.GetAppliedActionSequence()
        });
        Require(confirmed.accepted, confirmed.error);
    }

    private static void ValidateRandomPlayOrderBeginsMulligan()
    {
        GameObject rulesObject = new GameObject("Opening Mulligan Validation Rules");
        try
        {
            MethodInfo chooseStartingPlayer = typeof(GameRules).GetMethod(
                "ChooseStartingPlayer",
                BindingFlags.Static | BindingFlags.NonPublic);
            Require(chooseStartingPlayer != null, "GameRules.ChooseStartingPlayer was not found.");
            bool chosePlayer1 = false;
            bool chosePlayer2 = false;
            int player2Seed = 0;
            for (int seed = 1; seed <= 32; seed++)
            {
                int chosen = (int)chooseStartingPlayer.Invoke(null, new object[] { seed });
                chosePlayer1 |= chosen == 1;
                chosePlayer2 |= chosen == 2;
                if (chosen == 2 && player2Seed == 0) player2Seed = seed;
            }
            Require(chosePlayer1 && chosePlayer2,
                "Random play order did not produce both starting-player seats across validation seeds.");

            ConfigureValidationMatch(player2Seed);
            GameRules rules = rulesObject.AddComponent<GameRules>();
            MethodInfo initialize = typeof(GameRules).GetMethod(
                "InitializeGame",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Require(initialize != null, "GameRules.InitializeGame was not found.");
            initialize.Invoke(rules, null);

            Require(rules.GetStartingPlayer() == 2,
                "A configured local match did not apply the randomly selected second seat.");
            Require(rules.IsOpeningMulligan() && !rules.IsAwaitingPriority(),
                "A configured match entered the first turn before mulligans completed.");
            Require(rules.GetActiveChoice()?.kind == GameChoiceKind.MulliganDecision,
                "The opening hand did not request a keep-or-mulligan decision.");
            Require(rules.GetActiveChoice()?.choosingPlayerId == 1,
                "The offline opponent did not auto-keep before the local player's mulligan choice.");
            Require(rules.GetCurrentActionPrompt(1).Contains("先手"),
                "The opening prompt did not announce who plays first and second.");
        }
        finally
        {
            MatchStartConfig.ClearOnlineSession();
            UnityEngine.Object.DestroyImmediate(rulesObject);
        }
    }

    private static void ConfigureValidationMatch(int seed)
    {
        IReadOnlyList<CardDefinition> cards = CardDatabaseRepository.LoadAll();
        CardDefinition card = null;
        for (int i = 0; i < cards.Count && card == null; i++)
        {
            if (cards[i] != null && !string.IsNullOrEmpty(cards[i].PrintingKey)) card = cards[i];
        }
        Require(card != null, "The validation database contains no printable card.");
        var deck = new DeckDefinition { name = "Opening Validation" };
        deck.cards.Add(new DeckEntry
        {
            setCode = card.setCode,
            collectorNumber = card.CollectorNumber,
            cardId = card.cardID,
            count = 20
        });
        MatchStartConfig.SetDecks(deck, deck);
        MatchStartConfig.SetDeterministicSeed(seed);
    }

    private static int FindStartingPlayerSeed(int targetPlayer)
    {
        MethodInfo chooseStartingPlayer = typeof(GameRules).GetMethod(
            "ChooseStartingPlayer",
            BindingFlags.Static | BindingFlags.NonPublic);
        Require(chooseStartingPlayer != null, "GameRules.ChooseStartingPlayer was not found.");
        for (int seed = 1; seed <= 1024; seed++)
        {
            if ((int)chooseStartingPlayer.Invoke(null, new object[] { seed }) == targetPlayer)
                return seed;
        }
        throw new InvalidOperationException($"Could not find a validation seed for Player {targetPlayer}.");
    }

    private static void ValidateZeroAttackerDeclarationAdvancesCombat()
    {
        GameObject rulesObject = new GameObject("Zero Attacker Validation Rules");
        try
        {
            GameRules rules = rulesObject.AddComponent<GameRules>();
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            typeof(GameRules).GetField("player1Life", flags)?.SetValue(rules, 20);
            typeof(GameRules).GetField("player2Life", flags)?.SetValue(rules, 20);
            typeof(GameRules).GetField("currentPlayer", flags)?.SetValue(rules, 1);
            typeof(GameRules).GetField("currentPhase", flags)?.SetValue(
                rules,
                TurnPhase.DeclareAttackers);
            var priorityState = typeof(GameRules).GetField("priorityState", flags)?.GetValue(rules) as PriorityState;
            Require(priorityState != null, "GameRules priority state module was not found.");
            priorityState.Close();

            GameActionResult result = rules.ApplyAction(new GameAction
            {
                type = GameActionType.CommitAttackers,
                playerId = 1
            });

            Require(result.accepted, "Declaring zero attackers was rejected.");
            Require(
                rules.GetCurrentPhase() != TurnPhase.DeclareAttackers,
                "Declaring zero attackers did not advance out of the declare attackers step.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(rulesObject);
        }
    }

    private static void ValidateEventTypeHierarchy()
    {
        var source = new RuntimeCard(
            new CardDefinition
            {
                cardName = "Event Source",
                cardTypes = CardType.Creature
            },
            1,
            false);
        var pipeline = new GameEventPipeline();
        int zoneChangedCount = 0;
        int permanentLeftCount = 0;
        int diedCount = 0;

        pipeline.RegisterTrigger(
            source,
            GameEventType.ZoneChanged,
            "OnZoneChanged",
            null,
            RuntimeEffectDuration.Permanent,
            false);
        pipeline.RegisterTrigger(
            source,
            GameEventType.PermanentLeft,
            "OnPermanentLeft",
            null,
            RuntimeEffectDuration.Permanent,
            false);
        pipeline.RegisterTrigger(
            source,
            GameEventType.CardDied,
            "OnDied",
            null,
            RuntimeEffectDuration.Permanent,
            false);

        pipeline.Publish(
            new GameEventContext
            {
                type = GameEventType.CardDied,
                source = source,
                card = source,
                fromZone = GameZone.Battlefield,
                toZone = GameZone.Graveyard
            },
            card => true,
            null,
            (trigger, gameEvent) => true,
            (trigger, gameEvent) =>
            {
                if (trigger.EventType == GameEventType.ZoneChanged) zoneChangedCount++;
                if (trigger.EventType == GameEventType.PermanentLeft) permanentLeftCount++;
                if (trigger.EventType == GameEventType.CardDied) diedCount++;
            });

        Require(zoneChangedCount == 1, "A death did not match ZoneChanged exactly once.");
        Require(permanentLeftCount == 1, "A death did not match PermanentLeft exactly once.");
        Require(diedCount == 1, "A death did not match CardDied exactly once.");

        int supplementalZoneChangedCount = 0;
        pipeline.Publish(
            new GameEventContext
            {
                type = GameEventType.CardMilled,
                source = source,
                card = source,
                fromZone = GameZone.Library,
                toZone = GameZone.Graveyard
            },
            card => true,
            null,
            (trigger, gameEvent) => true,
            (trigger, gameEvent) =>
            {
                if (trigger.EventType == GameEventType.ZoneChanged)
                {
                    supplementalZoneChangedCount++;
                }
            });
        Require(
            supplementalZoneChangedCount == 0,
            "The supplemental mill event duplicated the primary zone-change event.");

        source.ReassignObjectId(91);
        source.SetCurrentZone(GameZone.Hand);
        var moved = new GameEventContext
        {
            type = GameEventType.CardDiscarded,
            source = source,
            card = source,
            fromZone = GameZone.Hand,
            toZone = GameZone.Graveyard
        };
        pipeline.Prepare(moved, card => true, null);
        source.MarkZoneChange();
        source.ReassignObjectId(92);
        source.SetCurrentZone(GameZone.Graveyard);
        pipeline.Complete(moved, card => true, null, null);
        GameEventRecord record = GameEventRecord.FromContext(moved);
        Require(
            record.sourceObjectId == 91 && record.cardObjectId == 91,
            "A zone-change event did not preserve the pre-move object identity.");

        moved.MarkReplacementApplied(77);
        GameEventContext cloned = moved.Clone();
        Require(
            cloned.HasAppliedReplacement(77),
            "Cloning an event lost its applied-replacement history.");

        var advancedPipeline = new GameEventPipeline();
        ScriptTriggerRegistration temporary = advancedPipeline.RegisterTrigger(
            source,
            GameEventType.TokenCreated,
            "Temporary",
            null,
            RuntimeEffectDuration.Permanent,
            false);
        Require(temporary != null, "A temporary effect could not be registered.");
        Require(
            advancedPipeline.Remove(temporary.Id),
            "A temporary effect could not be removed.");
        var untouchedPipeline = new GameEventPipeline();
        var advancedState = new System.Text.StringBuilder();
        var untouchedState = new System.Text.StringBuilder();
        advancedPipeline.AppendDeterministicState(advancedState);
        untouchedPipeline.AppendDeterministicState(untouchedState);
        Require(
            advancedState.ToString() != untouchedState.ToString(),
            "Event pipeline counters were missing from deterministic state.");
    }

    private static void ValidateLuaRuntimeBasics()
    {
        GameObject rulesObject = new GameObject("Lua Runtime Validation Rules");
        try
        {
            GameRules rules = rulesObject.AddComponent<GameRules>();
            typeof(GameRules).GetField(
                "player1Life",
                BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(rules, 20);
            typeof(GameRules).GetField(
                "player2Life",
                BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(rules, 20);
            var card = new RuntimeCard(
                new CardDefinition
                {
                    cardName = "Lua Smoke",
                    setCode = "TEST",
                    collectorNumberText = "1",
                    cardTypes = CardType.Creature
                },
                1,
                false);
            card.ReassignObjectId(501);
            card.SetCurrentZone(GameZone.Hand);
            var hand = typeof(GameRules).GetField(
                "player1Hand",
                BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(rules)
                as List<RuntimeCard>;
            Require(hand != null, "The Lua smoke hand could not be accessed.");
            hand.Add(card);

            using var runtime = new LuaCSharpCardScriptRuntime();
            using (CardScriptApi.UseRules(rules))
            using (ICardScriptInstance instance = runtime.CreateCardInstance(
                card,
                "return {\n" +
                "  ApiVersion = Game.ApiVersion,\n" +
                "  OnInit = function(self)\n" +
                "    self.TriggerId = Game.RegisterTriggeredAbility(self.Card, Game.Event.TokenCreated, 'OnToken', '', Game.Duration.Permanent, false)\n" +
                "    self.ReplacementId = Game.RegisterReplacementEffect(self.Card, Game.Event.CardDrawn, Game.Replacement.Prevent, '', 0, Game.Duration.Permanent)\n" +
                "    self.ContinuousId = Game.RegisterContinuousEffect(self.Card, Game.Continuous.AddKeyword, '', '', 0, 0, 'Flying', 0, 0, Game.Duration.WhileSourceRemains, 'lua-smoke')\n" +
                "    self.RuleId = Game.RegisterRuleEffect(self.Card, Game.Rule.MaxHandSize, Game.RuleOperation.Add, 1, 1, '', Game.Duration.Permanent, 'lua-smoke')\n" +
                "    self.Activated = Game.RegisterActivatedAbility(self.Card, 'Smoke', '', '{1};Tap', 'OnResolve', '', Game.Timing.AnyPriority, Game.AbilityTrait.None)\n" +
                "  end,\n" +
                "  Predicate = function(self)\n" +
                "    return Game.Event.TokenCreated == 22\n" +
                "      and self.Card.Name == 'Lua Smoke'\n" +
                "      and self.TriggerId > 0\n" +
                "      and self.ReplacementId > 0\n" +
                "      and self.ContinuousId > 0\n" +
                "      and self.RuleId > 0\n" +
                "      and self.Activated\n" +
                "  end,\n" +
                "  MoveAtomic = function(self)\n" +
                "    return Game.MoveCardsToZone({self.Card}, Game.Zone.Graveyard, false)\n" +
                "  end,\n" +
                "  OnToken = function(self) end,\n" +
                "  OnResolve = function(self) end\n" +
                "}"))
            {
                Require(instance != null, "The Lua-CSharp smoke script did not load.");
                Require(instance.HasFunction("Predicate"), "The Lua smoke hook was not exposed.");
                Require(
                    runtime.InvokeBoolean(instance, "Predicate", false),
                    "Lua constants, proxies, or runtime effect registrations failed.");
                Require(
                    runtime.InvokeBoolean(instance, "MoveAtomic", false)
                    && card.CurrentZone == GameZone.Graveyard,
                    "The Lua multi-card zone move binding failed.");
            }
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(rulesObject);
        }
    }

    private static void ValidateManaAbilityQualification()
    {
        Require(
            new ActivatedAbility
            {
                RuleTraits = ActivatedAbilityRuleTrait.CouldAddMana
            }.IsManaAbility,
            "A normal mana-producing activated ability was not a mana ability.");
        Require(
            !new ActivatedAbility
            {
                RuleTraits = ActivatedAbilityRuleTrait.CouldAddMana
                    | ActivatedAbilityRuleTrait.RequiresTarget
            }.IsManaAbility,
            "A targeted activated ability was incorrectly treated as a mana ability.");
        Require(
            !new ActivatedAbility
            {
                RuleTraits = ActivatedAbilityRuleTrait.CouldAddMana
                    | ActivatedAbilityRuleTrait.LoyaltyAbility
            }.IsManaAbility,
            "A loyalty ability was incorrectly treated as a mana ability.");
        Require(
            !new ActivatedAbility
            {
                RuleTraits = ActivatedAbilityRuleTrait.CouldAddMana
                    | ActivatedAbilityRuleTrait.MovesCardIntoOrOutOfLibrary
            }.IsManaAbility,
            "An ability that moves a card into or out of a library was incorrectly treated as a mana ability.");

        GameObject rulesObject = new GameObject("Mana Ability CR 605.1a Validation");
        try
        {
            GameRules rules = rulesObject.AddComponent<GameRules>();
            var card = new RuntimeCard(
                new CardDefinition
                {
                    cardName = "Mana Ability Validation",
                    cardTypes = CardType.Creature
                },
                1,
                false);

            Require(
                rules.RegisterScriptActivatedAbility(
                    card,
                    "Timing Is Not Classification",
                    string.Empty,
                    string.Empty,
                    "ResolveTimingOnly",
                    string.Empty,
                    ScriptAbilityTiming.Mana,
                    ActivatedAbilityRuleTrait.None),
                "A timing-only scripted ability could not be registered.");
            Require(
                !card.Abilities[0].IsManaAbility,
                "Game.Timing.Mana incorrectly classified an ability as a mana ability.");

            Require(
                rules.RegisterScriptActivatedAbility(
                    card,
                    "Library Mana",
                    string.Empty,
                    string.Empty,
                    "ResolveLibraryMana",
                    string.Empty,
                    ScriptAbilityTiming.AnyPriority,
                    ActivatedAbilityRuleTrait.CouldAddMana
                        | ActivatedAbilityRuleTrait.MovesCardIntoOrOutOfLibrary),
                "A library-moving mana-producing ability could not be registered.");
            Require(
                !card.Abilities[1].IsManaAbility,
                "CR 605.1a library movement did not force the ability onto the stack.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(rulesObject);
        }
    }

    private static void ValidateStackSnapshotCopy()
    {
        var sourceDefinition = new CardDefinition
        {
            cardName = "Snapshot Source",
            cardTypes = CardType.Creature
        };
        var targetDefinition = new CardDefinition
        {
            cardName = "Snapshot Target",
            cardTypes = CardType.Creature
        };
        var source = new RuntimeCard(sourceDefinition, 1, false);
        var target = new RuntimeCard(targetDefinition, 2, false);
        source.ReassignObjectId(17);
        target.ReassignObjectId(29);
        source.SetSpellTargets(new[] { SpellTargetReference.ForCard(target) });

        GameStackItem original = GameStackItem.ActivatedAbility(source, "OnResolve");
        source.MarkZoneChange();
        source.ReassignObjectId(18);
        GameStackItem copy = original.CopyForController(2);

        Require(copy != null, "A stack ability copy was not created.");
        Require(copy.SourceObjectId == 17, "A stack copy lost the original source identity.");
        Require(copy.SourceCard.ObjectId == 18, "A stack copy did not retain its source reference.");
        Require(copy.Targets.Count == 1, "A stack copy lost its captured targets.");
        Require(copy.Targets[0].Card == target, "A stack copy captured the wrong target.");
    }

    private static void ValidateLuaStructuredFeatureHooks()
    {
        var sagaDefinition = new CardDefinition
        {
            cardName = "Lua Saga",
            cardTypes = CardType.Enchantment,
            features = new List<CardFeatureDefinition>
            {
                new CardFeatureDefinition
                {
                    faceIndex = 0,
                    featureIndex = 0,
                    role = CardFeatureRole.Primary,
                    cardName = "Lua Saga",
                    cardTypes = CardType.Enchantment,
                    subtypes = "Saga",
                    ruleEntries = new List<CardRuleEntryDefinition>
                    {
                        new CardRuleEntryDefinition
                        {
                            order = 0,
                            kind = CardRuleEntryKind.Chapter,
                            chapterNumbers = "1",
                            text = "Validation chapter."
                        }
                    }
                }
            }
        };
        var adventureDefinition = new CardDefinition
        {
            cardName = "Lua Adventure",
            cardTypes = CardType.Creature,
            features = new List<CardFeatureDefinition>
            {
                new CardFeatureDefinition
                {
                    faceIndex = 0,
                    featureIndex = 0,
                    role = CardFeatureRole.Primary,
                    cardName = "Lua Adventure",
                    cardTypes = CardType.Creature
                },
                new CardFeatureDefinition
                {
                    faceIndex = 0,
                    featureIndex = 1,
                    role = CardFeatureRole.Adventure,
                    cardName = "Lua Adventure Spell",
                    cardTypes = CardType.Sorcery,
                    subtypes = "Adventure"
                }
            }
        };

        var saga = new RuntimeCard(sagaDefinition, 1, false);
        CardRuleEntryDefinition chapter = saga.PrimaryFeature.RuleEntries[0];
        saga.SetResolvingAbilityContext(saga.PrimaryFeature, chapter, 1);
        var adventure = new RuntimeCard(adventureDefinition, 1, false);
        adventure.SetSpellFeature(adventure.FindFeature(0, 1));

        const string script =
            "return {\n" +
            "  ApiVersion = Game.ApiVersion,\n" +
            "  IsSagaChapter = function(self)\n" +
            "    return self.Card.FeatureRole == Game.FeatureRole.Primary\n" +
            "      and Game.GetResolvingChapter(self.Card) == 1\n" +
            "  end,\n" +
            "  IsAdventureSpell = function(self)\n" +
            "    return self.Card.FeatureRole == Game.FeatureRole.Adventure\n" +
            "      and self.Card.FeatureIndex == 1\n" +
            "  end\n" +
            "}";

        using var runtime = new LuaCSharpCardScriptRuntime();
        using ICardScriptInstance sagaInstance = runtime.CreateCardInstance(saga, script);
        using ICardScriptInstance adventureInstance =
            runtime.CreateCardInstance(adventure, script);
        Require(
            sagaInstance != null
            && runtime.InvokeBoolean(sagaInstance, "IsSagaChapter", false),
            "A pure Lua Saga chapter hook did not receive its V7 feature context.");
        Require(
            adventureInstance != null
            && runtime.InvokeBoolean(
                adventureInstance,
                "IsAdventureSpell",
                false),
            "A pure Lua Adventure hook did not receive its V7 feature context.");
    }

    private static void ValidateReplayWithoutBattleUi()
    {
        GameObject rulesObject = new GameObject("Determinism Validation Rules");
        try
        {
            GameRules rules = rulesObject.AddComponent<GameRules>();
            MethodInfo initialize = typeof(GameRules).GetMethod(
                "InitializeGame",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Require(initialize != null, "GameRules.InitializeGame was not found.");
            initialize.Invoke(rules, null);

            string initialHash = rules.ComputeDeterministicStateHash();
            Require(!string.IsNullOrEmpty(initialHash), "The initial state hash was empty.");

            GameActionResult enabled = rules.ApplyAction(new GameAction
            {
                type = GameActionType.SetFullControl,
                playerId = 1,
                value = 1,
                expectedStateSequence = rules.GetAppliedActionSequence()
            });
            Require(enabled.accepted, "A legal deterministic action was rejected.");

            GameActionResult stale = rules.ApplyAction(new GameAction
            {
                type = GameActionType.SetFullControl,
                playerId = 1,
                value = 0,
                expectedStateSequence = enabled.appliedSequence - 1
            });
            Require(!stale.accepted, "A stale deterministic action was accepted.");
            Require(
                rules.ComputeDeterministicStateHash() == enabled.stateHash,
                "A stale action changed deterministic state.");
            Require(
                rules.GetAppliedActionSequence() == enabled.appliedSequence,
                "A stale action advanced the action sequence.");
            Require(
                rules.GetReplay().actions.Count == 1,
                "A stale action was recorded in the replay.");

            GameActionResult disabled = rules.ApplyAction(new GameAction
            {
                type = GameActionType.SetFullControl,
                playerId = 1,
                value = 0,
                expectedStateSequence = rules.GetAppliedActionSequence()
            });
            Require(disabled.accepted, "The second deterministic action was rejected.");

            GameReplay replay = rules.GetReplay().Clone();
            string expectedFinalHash = disabled.stateHash;
            GameReplayResult replayResult = rules.ReplayFromInitialState(replay);
            Require(replayResult.succeeded, replayResult.error ?? "Replay failed.");
            Require(
                replayResult.appliedActionCount == replay.actions.Count,
                "Replay applied the wrong number of actions.");
            Require(
                replayResult.finalStateHash == expectedFinalHash,
                "Replay ended with a different deterministic state hash.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(rulesObject);
        }
    }

    private static void ValidateChoiceActionsWithoutBattleUi()
    {
        GameObject rulesObject = new GameObject("Choice Action Validation Rules");
        try
        {
            GameRules rules = rulesObject.AddComponent<GameRules>();
            MethodInfo initialize = typeof(GameRules).GetMethod(
                "InitializeGame",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Require(initialize != null, "GameRules.InitializeGame was not found.");
            initialize.Invoke(rules, null);

            int completedOptionId = 0;
            GameChoiceRequest request = rules.BeginOptionChoice(
                GameChoiceKind.SelectOptions,
                1,
                "Choose one validation option.",
                1,
                1,
                false,
                new[]
                {
                    new GameChoiceOption(101, GameChoiceZone.None, 0, -1, true, "First"),
                    new GameChoiceOption(202, GameChoiceZone.None, 0, -1, true, "Second")
                },
                result =>
                {
                    if (result != null && result.selectedOptionIds.Count == 1)
                    {
                        completedOptionId = result.selectedOptionIds[0];
                    }
                });
            Require(request != null, "A generic choice could not be created.");

            string beforeRejectedHash = rules.ComputeDeterministicStateHash();
            long beforeRejectedSequence = rules.GetAppliedActionSequence();
            int beforeRejectedReplayCount = rules.GetReplay().actions.Count;
            GameActionResult wrongRequest = rules.ApplyAction(new GameAction
            {
                type = GameActionType.ToggleChoiceOption,
                playerId = 1,
                choiceRequestId = request.requestId + 1,
                optionId = 101,
                expectedStateSequence = beforeRejectedSequence
            });
            Require(!wrongRequest.accepted, "An action for the wrong choice request was accepted.");
            Require(
                rules.ComputeDeterministicStateHash() == beforeRejectedHash,
                "A rejected choice action changed deterministic state.");
            Require(
                rules.GetAppliedActionSequence() == beforeRejectedSequence,
                "A rejected choice action advanced the action sequence.");
            Require(
                rules.GetReplay().actions.Count == beforeRejectedReplayCount,
                "A rejected choice action was recorded in the replay.");

            GameActionResult selectFirst = rules.ApplyAction(new GameAction
            {
                type = GameActionType.ToggleChoiceOption,
                playerId = 1,
                choiceRequestId = request.requestId,
                optionId = 101,
                expectedStateSequence = rules.GetAppliedActionSequence()
            });
            Require(selectFirst.accepted, "The first generic choice selection was rejected.");
            Require(request.IsSelected(101), "The first generic choice option was not selected.");

            GameActionResult replaceWithSecond = rules.ApplyAction(new GameAction
            {
                type = GameActionType.ToggleChoiceOption,
                playerId = 1,
                choiceRequestId = request.requestId,
                optionId = 202,
                expectedStateSequence = rules.GetAppliedActionSequence()
            });
            Require(replaceWithSecond.accepted, "A legal single-choice replacement was rejected.");
            Require(
                !request.IsSelected(101) && request.IsSelected(202),
                "Selecting a second single-choice option did not replace the first.");

            GameActionResult confirm = rules.ApplyAction(new GameAction
            {
                type = GameActionType.ConfirmChoice,
                playerId = 1,
                choiceRequestId = request.requestId,
                expectedStateSequence = rules.GetAppliedActionSequence()
            });
            Require(confirm.accepted, "A valid generic choice confirmation was rejected.");
            Require(completedOptionId == 202, "The generic choice completed with the wrong option.");
            Require(!rules.HasActiveChoice(), "The confirmed generic choice remained active.");

            string afterConfirmationHash = rules.ComputeDeterministicStateHash();
            long afterConfirmationSequence = rules.GetAppliedActionSequence();
            int afterConfirmationReplayCount = rules.GetReplay().actions.Count;
            GameActionResult repeatedConfirmation = rules.ApplyAction(new GameAction
            {
                type = GameActionType.ConfirmChoice,
                playerId = 1,
                choiceRequestId = request.requestId,
                expectedStateSequence = afterConfirmationSequence
            });
            Require(!repeatedConfirmation.accepted, "A completed choice was confirmed twice.");
            Require(
                rules.ComputeDeterministicStateHash() == afterConfirmationHash,
                "A repeated confirmation changed deterministic state.");
            Require(
                rules.GetAppliedActionSequence() == afterConfirmationSequence,
                "A repeated confirmation advanced the action sequence.");
            Require(
                rules.GetReplay().actions.Count == afterConfirmationReplayCount,
                "A repeated confirmation was recorded in the replay.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(rulesObject);
        }
    }

    private static void ValidateZoneReplacementBeforeMove()
    {
        GameObject rulesObject = new GameObject("Zone Replacement Validation Rules");
        try
        {
            GameRules rules = rulesObject.AddComponent<GameRules>();
            MethodInfo initialize = typeof(GameRules).GetMethod(
                "InitializeGame",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Require(initialize != null, "GameRules.InitializeGame was not found.");
            initialize.Invoke(rules, null);

            RuntimeCard card = rules.GetHandCard(1, 0);
            Require(card != null, "The validation hand did not contain a card.");
            long objectId = card.ObjectId;
            int handCount = rules.GetHandCards(1).Count;
            int graveyardCount = rules.GetGraveyardCards(1).Count;
            long replacementId = rules.RegisterScriptReplacement(
                card,
                GameEventType.CardDiscarded,
                GameEventReplacementOperation.Prevent,
                string.Empty,
                0,
                RuntimeEffectDuration.Permanent);
            Require(replacementId > 0, "The discard replacement could not be registered.");

            bool moved = rules.MoveCardToZone(card, GameZone.Graveyard);
            Require(!moved, "A prevented discard still moved the card.");
            Require(card.CurrentZone == GameZone.Hand, "A prevented discard changed the card zone.");
            Require(card.ObjectId == objectId, "A prevented discard changed object identity.");
            Require(
                rules.GetHandCards(1).Count == handCount,
                "A prevented discard changed hand size.");
            Require(
                rules.GetGraveyardCards(1).Count == graveyardCount,
                "A prevented discard changed graveyard size.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(rulesObject);
        }
    }

    private static void ValidateManaCostDeclarations()
    {
        ManaCost baseCost = ManaCost.Parse("{X}{X}{U}");
        ManaCost additionalCost = ManaCost.Parse("{1}{X}");
        ManaCost combined = ManaCostMath.Add(baseCost, additionalCost);
        Require(combined.XSymbols == 3, "Combining costs lost one or more X symbols.");

        ManaCost resolved = ManaCostMath.ResolveX(combined, 2);
        Require(
            resolved.XSymbols == 0
            && resolved.Generic == 7
            && resolved.Blue == 1
            && resolved.Total == 8,
            "A compound X cost resolved to the wrong mana cost.");

        ManaCost adjusted = ManaCostMath.EnforceMinimumTotal(
            ManaCostMath.ReduceGeneric(
                ManaCostMath.AddGeneric(resolved, 2),
                4),
            6);
        Require(
            adjusted.Generic == 5 && adjusted.Blue == 1,
            "Cost increases, reductions, and minimum costs were applied out of order.");

        var source = new RuntimeCard(
            new CardDefinition
            {
                cardName = "Cast Declaration Source",
                cardTypes = CardType.Instant,
                manaCost = "{X}{U}"
            },
            1,
            false);
        source.ReassignObjectId(601);
        var target = new RuntimeCard(
            new CardDefinition
            {
                cardName = "Cast Declaration Target",
                cardTypes = CardType.Creature,
                attack = 2,
                defence = 2
            },
            2,
            false);
        target.ReassignObjectId(602);

        Require(source.CastProfile.ConfigureModes(1, 2), "Cast modes could not be configured.");
        Require(source.CastProfile.AddMode("damage", "Damage"), "A cast mode could not be added.");
        Require(source.CastProfile.AddMode("draw", "Draw"), "A second cast mode could not be added.");
        Require(source.CastProfile.ConfigureX(0, 99), "Cast X bounds could not be configured.");
        Require(
            source.CastProfile.AddCost(new SpellCastCostOption(
                "kicker",
                "Kicker",
                SpellCastCostKind.Additional,
                new ScriptAbilityCost
                {
                    Mana = ManaCost.Parse("{1}{U}"),
                    DiscardCount = 1
                },
                true)),
            "An additional cast cost could not be registered.");
        Require(
            source.CastProfile.AddTargetGroup(new SpellTargetGroupDefinition(
                "damage-targets",
                "Choose up to two targets.",
                CardTargetMode.AnyCreatureOrPlayer,
                1,
                2,
                string.Empty,
                true)),
            "A target group could not be registered.");

        var declaration = new SpellCastDeclaration(
            source,
            1,
            1,
            GameChoiceZone.Hand,
            0,
            source.PrimaryFeature)
        {
            XValue = 4,
            Stage = SpellCastDeclarationStage.Confirmation,
            TargetGroupIndex = 1
        };
        declaration.SetModes(new[] { "damage", "draw", "damage" });
        declaration.SetAdditionalCosts(new[] { "kicker", "kicker" });
        declaration.ReplaceTargets(
            "damage-targets",
            new[]
            {
                SpellTargetReference.ForCard(target),
                SpellTargetReference.ForPlayer(2)
            });
        declaration.LockCost(new ScriptAbilityCost
        {
            Mana = ManaCost.Parse("{5}{U}{U}"),
            DiscardCount = 1
        });
        source.SetCurrentCastDeclaration(declaration);

        GameStackItem stackItem = GameStackItem.Spell(source, source.PrimaryFeature);
        declaration.XValue = 1;
        declaration.SetModes(new[] { "draw" });
        declaration.ReplaceTargets("damage-targets", Array.Empty<SpellTargetReference>());

        Require(
            stackItem.CastDeclaration != null
            && stackItem.CastDeclaration.XValue == 4
            && stackItem.CastDeclaration.SelectedModeIds.Count == 2
            && stackItem.CastDeclaration.SelectedAdditionalCostIds.Count == 1
            && stackItem.CastDeclaration.GetTargets("damage-targets").Count == 2
            && stackItem.CastDeclaration.LockedCost.DiscardCount == 1,
            "A spell stack item did not snapshot its complete cast declaration.");

        var declarationState = new StringBuilder();
        stackItem.CastDeclaration.AppendDeterministicState(declarationState);
        Require(
            declarationState.ToString().Contains("damage-targets", StringComparison.Ordinal),
            "The deterministic cast state omitted grouped targets.");
    }

    private static void ValidateReplacementOrderingAndLinkedCleanup()
    {
        var source = new RuntimeCard(
            new CardDefinition
            {
                cardName = "Replacement Source",
                cardTypes = CardType.Enchantment
            },
            1,
            false);
        var pipeline = new GameEventPipeline();
        ScriptReplacementRegistration multiplier = pipeline.RegisterReplacement(
            source,
            GameEventType.CardDrawn,
            GameEventReplacementOperation.MultiplyAmount,
            string.Empty,
            2,
            RuntimeEffectDuration.Permanent,
            "CanMultiply",
            "replacement-chain");
        ScriptReplacementRegistration addition = pipeline.RegisterReplacement(
            source,
            GameEventType.CardDrawn,
            GameEventReplacementOperation.AddAmount,
            string.Empty,
            1,
            RuntimeEffectDuration.Once,
            "CanAdd",
            "replacement-chain");
        pipeline.RegisterTrigger(
            source,
            GameEventType.CardDrawn,
            "AfterDraw",
            string.Empty,
            RuntimeEffectDuration.Permanent,
            false,
            "replacement-chain");

        var drawEvent = new GameEventContext
        {
            type = GameEventType.CardDrawn,
            source = source,
            card = source,
            amount = 1
        };
        pipeline.Prepare(
            drawEvent,
            _ => true,
            null,
            (registration, context) =>
                registration == addition
                    ? context.amount < 2
                    : registration == multiplier && context.amount >= 2,
            (context, applicable) =>
            {
                for (int i = 0; i < applicable.Count; i++)
                {
                    if (applicable[i] == addition) return addition;
                }
                return applicable.Count > 0 ? applicable[0] : null;
            });

        Require(
            drawEvent.amount == 4
            && drawEvent.HasAppliedReplacement(addition.Id)
            && drawEvent.HasAppliedReplacement(multiplier.Id),
            "Replacement effects did not re-evaluate after each application.");
        Require(
            pipeline.Replacements.Count == 1
            && pipeline.Replacements[0] == multiplier,
            "A once-only replacement effect did not expire.");
        Require(
            pipeline.RemoveLinked(source, "replacement-chain") == 2
            && pipeline.Replacements.Count == 0
            && pipeline.Triggers.Count == 0,
            "Linked runtime effects were not removed as one logical effect.");

        var categoryPipeline = new GameEventPipeline();
        ScriptReplacementRegistration general = categoryPipeline.RegisterReplacement(
            source,
            GameEventType.CardDrawn,
            GameEventReplacementOperation.MultiplyAmount,
            string.Empty,
            2,
            RuntimeEffectDuration.Permanent,
            category: ReplacementEffectCategory.General);
        ScriptReplacementRegistration self = categoryPipeline.RegisterReplacement(
            source,
            GameEventType.CardDrawn,
            GameEventReplacementOperation.AddAmount,
            string.Empty,
            1,
            RuntimeEffectDuration.Permanent,
            category: ReplacementEffectCategory.SelfReplacement);
        var categorizedEvent = new GameEventContext
        {
            type = GameEventType.CardDrawn,
            source = source,
            card = source,
            amount = 1
        };
        IReadOnlyList<ScriptReplacementRegistration> firstCategory =
            categoryPipeline.GetApplicableReplacements(
                categorizedEvent,
                _ => true,
                null);
        Require(
            firstCategory.Count == 1 && firstCategory[0] == self,
            "A general replacement was offered before a self-replacement effect.");
        categoryPipeline.Prepare(categorizedEvent, _ => true, null);
        Require(
            categorizedEvent.amount == 4
            && categorizedEvent.HasAppliedReplacement(self.Id)
            && categorizedEvent.HasAppliedReplacement(general.Id),
            "Replacement categories did not recheck lower categories after application.");
    }

    private static void ValidateContinuousEffectDependencies()
    {
        var card = new RuntimeCard(
            new CardDefinition
            {
                cardName = "Continuous Effect Target",
                cardTypes = CardType.Creature,
                description = "Printed text",
                attack = 1,
                defence = 2
            },
            1,
            false);

        long dependentText = card.AddContinuousEffect(new ContinuousEffectRuntime
        {
            timestamp = 10,
            layer = ContinuousEffectLayer.Text,
            operation = ContinuousEffectOperation.ReplaceRulesText,
            duration = ContinuousEffectDuration.Permanent,
            linkId = "dependent",
            dependencyKeys = "provider",
            rulesText = "Dependent text"
        });
        card.AddContinuousEffect(new ContinuousEffectRuntime
        {
            timestamp = 20,
            layer = ContinuousEffectLayer.Text,
            operation = ContinuousEffectOperation.ReplaceRulesText,
            duration = ContinuousEffectDuration.Permanent,
            linkId = "provider",
            rulesText = "Provider text"
        });
        Require(
            card.GetRulesText() == "Dependent text",
            "A same-layer dependency was not applied after its provider.");

        card.AddContinuousEffect(new ContinuousEffectRuntime
        {
            timestamp = 30,
            layer = ContinuousEffectLayer.PowerToughness,
            powerToughnessSublayer = PowerToughnessSublayer.Modify,
            operation = ContinuousEffectOperation.ModifyPowerToughness,
            duration = ContinuousEffectDuration.Permanent,
            power = 2,
            toughness = 3
        });
        card.AddContinuousEffect(new ContinuousEffectRuntime
        {
            timestamp = 40,
            layer = ContinuousEffectLayer.PowerToughness,
            powerToughnessSublayer = PowerToughnessSublayer.Set,
            operation = ContinuousEffectOperation.SetPowerToughness,
            duration = ContinuousEffectDuration.Permanent,
            power = 5,
            toughness = 5
        });
        Require(
            card.CurrentAttack == 7 && card.CurrentDefence == 8,
            "Power/toughness sublayers were ordered by timestamp instead of sublayer.");

        long controlEffect = card.AddContinuousEffect(new ContinuousEffectRuntime
        {
            timestamp = 50,
            layer = ContinuousEffectLayer.Control,
            operation = ContinuousEffectOperation.SetController,
            duration = ContinuousEffectDuration.Permanent,
            controllerPlayerId = 2
        });
        Require(card.ControllerPlayerID == 2, "A control-changing effect was not applied.");
        Require(
            card.RemoveContinuousEffect(controlEffect) && card.ControllerPlayerID == 1,
            "Removing a control-changing effect did not restore base control.");
        Require(
            card.RemoveContinuousEffect(dependentText)
            && card.GetRulesText() == "Provider text",
            "Removing a dependent text effect did not reveal the prior effect.");

        var structuralDependencyCard = new RuntimeCard(
            new CardDefinition
            {
                cardName = "Structural Dependency Target",
                cardTypes = CardType.Creature,
                colorIndicator = ManaColorFlags.White,
                attack = 2,
                defence = 2
            },
            1,
            false);
        structuralDependencyCard.AddContinuousEffect(new ContinuousEffectRuntime
        {
            timestamp = 5,
            layer = ContinuousEffectLayer.Color,
            operation = ContinuousEffectOperation.SetColors,
            duration = ContinuousEffectDuration.Permanent,
            reads = ContinuousEffectCharacteristic.Color,
            writes = ContinuousEffectCharacteristic.Color,
            colors = ManaColorFlags.Red
        });
        structuralDependencyCard.AddContinuousEffect(new ContinuousEffectRuntime
        {
            timestamp = 10,
            layer = ContinuousEffectLayer.Color,
            operation = ContinuousEffectOperation.SetColors,
            duration = ContinuousEffectDuration.Permanent,
            writes = ContinuousEffectCharacteristic.Color,
            colors = ManaColorFlags.Blue
        });
        Require(
            structuralDependencyCard.GetColors() == ManaColorFlags.Red,
            "A structural dependency did not override timestamp order.");

        var copySource = new RuntimeCard(
            new CardDefinition
            {
                cardName = "Copy Source",
                cardTypes = CardType.Creature,
                colorIndicator = ManaColorFlags.Green,
                attack = 4,
                defence = 5
            },
            1,
            false);
        copySource.AddKeyword("Flying");
        var faceDownCopy = new RuntimeCard(
            new CardDefinition
            {
                cardName = "Copy Target",
                cardTypes = CardType.Artifact,
                attack = 0,
                defence = 0
            },
            1,
            false);
        faceDownCopy.AddContinuousEffect(new ContinuousEffectRuntime
        {
            timestamp = 20,
            layer = ContinuousEffectLayer.Copy,
            copyEffectSublayer = CopyEffectSublayer.Copy,
            operation = ContinuousEffectOperation.CopyCharacteristics,
            duration = ContinuousEffectDuration.Permanent,
            copySource = copySource,
            copySourceZoneChangeVersion = copySource.ZoneChangeVersion
        });
        faceDownCopy.AddContinuousEffect(new ContinuousEffectRuntime
        {
            timestamp = 10,
            layer = ContinuousEffectLayer.Copy,
            copyEffectSublayer = CopyEffectSublayer.FaceDown,
            operation = ContinuousEffectOperation.SetFaceDownCharacteristics,
            duration = ContinuousEffectDuration.Permanent,
            cardTypes = CardType.Creature,
            power = 2,
            toughness = 2
        });
        Require(
            faceDownCopy.GetCardTypes() == CardType.Creature
            && faceDownCopy.CurrentAttack == 2
            && faceDownCopy.CurrentDefence == 2
            && !faceDownCopy.HasKeyword("Flying")
            && string.IsNullOrEmpty(faceDownCopy.GetCardName()),
            "Face-down characteristics were not applied after copy effects.");
    }

    private static void ValidateRuleBoundaryExtensions()
    {
        GameObject rulesObject = new GameObject("Rule Boundary Validation Rules");
        try
        {
            GameRules rules = rulesObject.AddComponent<GameRules>();
            typeof(GameRules).GetField(
                "player1Life",
                BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(rules, 20);
            typeof(GameRules).GetField(
                "player2Life",
                BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(rules, 20);
            var source = new RuntimeCard(
                new CardDefinition
                {
                    cardName = "Boundary Source",
                    cardTypes = CardType.Creature,
                    colorIndicator = ManaColorFlags.White,
                    attack = 2,
                    defence = 2
                },
                1,
                false);
            source.ReassignObjectId(9801);
            source.SetCurrentZone(GameZone.Hand);
            var hand = (List<RuntimeCard>)typeof(GameRules).GetField(
                "player1Hand",
                BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(rules);
            Require(hand != null, "The hidden-zone validation hand was unavailable.");
            hand.Add(source);

            long cda = rules.RegisterScriptContinuousEffect(
                source,
                ContinuousEffectOperation.SetColors,
                string.Empty,
                string.Empty,
                CardType.None,
                ManaColorFlags.Blue,
                string.Empty,
                0,
                0,
                RuntimeEffectDuration.WhileSourceRemains,
                "hidden-zone-cda",
                true,
                ContinuousEffectCharacteristic.Color,
                ContinuousEffectCharacteristic.Color);
            Require(
                cda > 0 && source.GetColors() == ManaColorFlags.Blue,
                "A characteristic-defining ability was not applied in hand.");

            hand.Remove(source);
            var battlefield = (List<RuntimeCard>)typeof(GameRules).GetField(
                "player1Battlefield",
                BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(rules);
            Require(battlefield != null, "The rule-boundary battlefield was unavailable.");
            source.SetCurrentZone(GameZone.Battlefield);
            battlefield.Add(source);

            var target = new RuntimeCard(
                new CardDefinition
                {
                    cardName = "Counter Target",
                    cardTypes = CardType.Creature,
                    attack = 2,
                    defence = 2
                },
                1,
                false);
            target.ReassignObjectId(9802);
            target.SetCurrentZone(GameZone.Battlefield);
            battlefield.Add(target);

            rules.RegisterScriptReplacement(
                source,
                GameEventType.CounterChanged,
                GameEventReplacementOperation.SetAmount,
                string.Empty,
                2,
                RuntimeEffectDuration.Once);
            Require(
                rules.AddCounters(target, "+1/+1", 1)
                && target.GetCounters("+1/+1") == 2,
                "A counter-placement replacement did not affect the committed change.");

            rules.RegisterScriptReplacement(
                source,
                GameEventType.CounterChanged,
                GameEventReplacementOperation.SetAmount,
                string.Empty,
                -1,
                RuntimeEffectDuration.Once);
            Require(
                rules.AddCounters(target, "+1/+1", -2)
                && target.GetCounters("+1/+1") == 1,
                "A counter-removal replacement did not affect the committed change.");

            int lifeBefore = rules.GetLife(1);
            rules.RegisterScriptReplacement(
                source,
                GameEventType.LifeChanged,
                GameEventReplacementOperation.SetAmount,
                string.Empty,
                -1,
                RuntimeEffectDuration.Once);
            rules.LoseLife(1, 3);
            Require(
                rules.GetLife(1) == lifeBefore - 1,
                "A life-loss replacement did not affect the committed life change.");

            var planeswalker = new RuntimeCard(
                new CardDefinition
                {
                    cardName = "Attack Target",
                    cardTypes = CardType.Planeswalker
                },
                2,
                false);
            planeswalker.ReassignObjectId(9803);
            planeswalker.SetCurrentZone(GameZone.Battlefield);
            source.DeclareAttack(planeswalker);
            var state = new StringBuilder();
            source.AppendDeterministicState(state);
            Require(
                state.ToString().Contains("C9803/"),
                "A permanent attack target was omitted from deterministic state.");

            ValidateDestructionProtectionAndDamageRedirection(rules, battlefield);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(rulesObject);
        }
    }

    private static void ValidateDestructionProtectionAndDamageRedirection(
        GameRules rules,
        List<RuntimeCard> battlefield)
    {
        var regenerating = new RuntimeCard(
            new CardDefinition
            {
                cardName = "Regenerating Combatant",
                cardTypes = CardType.Creature,
                attack = 3,
                defence = 3
            },
            1,
            false);
        regenerating.ReassignObjectId(9810);
        regenerating.SetCurrentZone(GameZone.Battlefield);
        battlefield.Add(regenerating);
        regenerating.DeclareAttack(2);
        rules.AddRegenerationShield(regenerating);
        Require(
            !rules.DestroyCard(regenerating)
            && battlefield.Contains(regenerating)
            && regenerating.IsTapped
            && !regenerating.IsAttacking
            && regenerating.RegenerationShields == 0,
            "Regeneration did not replace destruction, tap, clear damage, and remove the creature from combat.");

        var indestructible = new RuntimeCard(
            new CardDefinition
            {
                cardName = "Indestructible Permanent",
                cardTypes = CardType.Creature,
                attack = 2,
                defence = 2
            },
            1,
            false);
        indestructible.ReassignObjectId(9811);
        indestructible.SetCurrentZone(GameZone.Battlefield);
        indestructible.AddKeyword("Indestructible");
        battlefield.Add(indestructible);
        Require(
            !rules.DestroyCard(indestructible) && battlefield.Contains(indestructible),
            "Indestructible did not stop a destroy effect.");

        var redSource = new RuntimeCard(
            new CardDefinition
            {
                cardName = "Red Damage Source",
                cardTypes = CardType.Creature,
                colorIndicator = ManaColorFlags.Red,
                attack = 2,
                defence = 2
            },
            1,
            false);
        redSource.ReassignObjectId(9812);
        redSource.SetCurrentZone(GameZone.Battlefield);
        battlefield.Add(redSource);
        var protectedTarget = new RuntimeCard(
            new CardDefinition
            {
                cardName = "Protected Target",
                cardTypes = CardType.Creature,
                attack = 4,
                defence = 4
            },
            1,
            false);
        protectedTarget.ReassignObjectId(9813);
        protectedTarget.SetCurrentZone(GameZone.Battlefield);
        protectedTarget.AddKeyword("ProtectionFromRed");
        battlefield.Add(protectedTarget);
        Require(
            rules.DealDamageToCard(redSource, protectedTarget, 2)
            && protectedTarget.DamageTaken == 0,
            "Protection did not prevent damage from the protected quality.");

        var redirectedTarget = new RuntimeCard(
            new CardDefinition
            {
                cardName = "Original Damage Target",
                cardTypes = CardType.Creature,
                attack = 5,
                defence = 5
            },
            1,
            false);
        redirectedTarget.ReassignObjectId(9814);
        redirectedTarget.SetCurrentZone(GameZone.Battlefield);
        battlefield.Add(redirectedTarget);
        var redirector = new RuntimeCard(
            new CardDefinition
            {
                cardName = "Damage Redirector",
                cardTypes = CardType.Enchantment,
                luaScript =
                    "local Card = { ApiVersion = 1 }\n" +
                    "function Card.OnInit(self)\n" +
                    "  Game.RegisterReplacementEffect(self.Card, Game.Event.Damage, Game.Replacement.InvokeHook, 'RedirectDamage', 0, Game.Duration.WhileSourceRemains)\n" +
                    "end\n" +
                    "function Card.RedirectDamage(self)\n" +
                    "  Game.SetEventDamagePlayer(self.Card, 2)\n" +
                    "end\n" +
                    "return Card"
            },
            1,
            false);
        redirector.ReassignObjectId(9815);
        redirector.SetCurrentZone(GameZone.Battlefield);
        battlefield.Add(redirector);
        using (CardScriptRuntimeManager.UseRules(rules))
        {
            redirector.EnsureScriptInstance();
        }
        Require(redirector.ScriptInstance != null, "The damage redirect replacement script did not initialize.");
        int opponentLifeBefore = rules.GetLife(2);
        Require(
            rules.DealDamageToCard(redSource, redirectedTarget, 2)
            && redirectedTarget.DamageTaken == 0
            && rules.GetLife(2) == opponentLifeBefore - 2,
            "A damage replacement could not redirect damage from a permanent to a player.");
    }

    private static void ValidateLuaResourceQuotas()
    {
        var card = new RuntimeCard(
            new CardDefinition
            {
                cardName = "Lua Quota Validation",
                setCode = "TEST",
                collectorNumberText = "quota",
                cardTypes = CardType.Instant
            },
            1,
            false);
        using var runtime = new LuaCSharpCardScriptRuntime();
        Require(
            runtime.ValidateRetainedStateQuotaForAutomation(card),
            "Lua retained-state quota did not reject an oversized script state.");
        Require(
            runtime.ValidateInstructionQuotaForAutomation(),
            "Lua instruction quota did not stop an unbounded script.");

        using ICardScriptInstance instance = runtime.CreateCardInstance(
            card,
            "return { ApiVersion = Game.ApiVersion, Ping = function(self) return true end }");
        Require(
            instance != null && runtime.InvokeBoolean(instance, "Ping", false),
            "A Lua quota violation poisoned subsequent script execution.");
    }

    private static void ValidateAtomicMultiCardZoneMove()
    {
        GameObject rulesObject = new GameObject("Atomic Zone Move Validation Rules");
        try
        {
            GameRules rules = rulesObject.AddComponent<GameRules>();
            FieldInfo handField = typeof(GameRules).GetField(
                "player1Hand",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Require(handField != null, "The player hand field was not found.");
            var hand = handField.GetValue(rules) as List<RuntimeCard>;
            Require(hand != null, "The player hand could not be accessed.");

            var first = new RuntimeCard(
                new CardDefinition { cardName = "First", cardTypes = CardType.Sorcery },
                1,
                false);
            var second = new RuntimeCard(
                new CardDefinition { cardName = "Second", cardTypes = CardType.Sorcery },
                1,
                false);
            var unknown = new RuntimeCard(
                new CardDefinition { cardName = "Unknown", cardTypes = CardType.Sorcery },
                1,
                false);
            first.ReassignObjectId(701);
            second.ReassignObjectId(702);
            unknown.ReassignObjectId(703);
            first.SetCurrentZone(GameZone.Hand);
            second.SetCurrentZone(GameZone.Hand);
            unknown.SetCurrentZone(GameZone.Hand);
            hand.Add(first);
            hand.Add(second);

            Require(
                !rules.MoveCardsToZone(
                    new[] { first, unknown },
                    GameZone.Graveyard,
                    false,
                    false),
                "An invalid multi-card move was accepted.");
            Require(
                first.CurrentZone == GameZone.Hand
                && second.CurrentZone == GameZone.Hand
                && rules.GetGraveyardCards(1).Count == 0,
                "A failed multi-card move partially changed zones.");

            Require(
                rules.MoveCardsToZone(
                    new[] { first, second },
                    GameZone.Library,
                    true,
                    false),
                "A valid multi-card move was rejected.");
            IReadOnlyList<RuntimeCard> library = rules.GetZoneCards(1, GameZone.Library);
            Require(
                library.Count == 2
                && ReferenceEquals(library[0], first)
                && ReferenceEquals(library[1], second),
                "A multi-card move did not preserve the selected top-to-bottom order.");
            Require(
                first.ObjectId != 701 && second.ObjectId != 702,
                "A multi-card zone change did not create new object identities.");

            var prevented = new RuntimeCard(
                new CardDefinition { cardName = "Prevented", cardTypes = CardType.Sorcery },
                1,
                false);
            var moved = new RuntimeCard(
                new CardDefinition { cardName = "Moved", cardTypes = CardType.Sorcery },
                1,
                false);
            prevented.ReassignObjectId(704);
            moved.ReassignObjectId(705);
            prevented.SetCurrentZone(GameZone.Hand);
            moved.SetCurrentZone(GameZone.Hand);
            hand.Add(prevented);
            hand.Add(moved);
            Require(
                rules.RegisterScriptReplacement(
                    prevented,
                    GameEventType.CardDiscarded,
                    GameEventReplacementOperation.Prevent,
                    string.Empty,
                    0,
                    RuntimeEffectDuration.Once) > 0,
                "The one-shot zone replacement could not be registered.");
            Require(
                rules.MoveCardsToZone(
                    new[] { prevented, moved },
                    GameZone.Graveyard,
                    false,
                    false),
                "A replacement-aware multi-card move was rejected.");
            Require(
                prevented.CurrentZone == GameZone.Hand
                && moved.CurrentZone == GameZone.Graveyard
                && hand.Contains(prevented)
                && !hand.Contains(moved),
                "Preventing one object incorrectly cancelled the rest of a simultaneous move.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(rulesObject);
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
#endif
