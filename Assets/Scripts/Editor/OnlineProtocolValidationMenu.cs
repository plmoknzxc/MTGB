#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using Unity.Netcode;
using Unity.Pipeline.Commands;
using UnityEngine;
using UnityEngine.UI;

public static class OnlineProtocolValidationMenu
{
    [MenuItem("MTGB/Networking/Validate Protocol")]
    public static void ValidateFromMenu()
    {
        ValidateForAutomation();
        Debug.Log("MTGB online protocol validation passed.");
    }

    [CliCommand("mtgb_validate_p1", "Run MTGB P1 online protocol and room-isolation validation")]
    public static void ValidateFromCli()
    {
        ValidateFromMenu();
    }

    public static void ValidateForAutomation()
    {
        ValidateProtocolRoundTrip();
        ValidateSeriesState();
        ValidateInteractionGate();
        ValidateOnlinePriorityHudLabel();
        ValidatePriorityStateModule();
        ValidateOnlinePriorityAutomation();
        ValidateOnlinePriorityHandoff();
        ValidateAuthoritativeCombatAnimationIsolation();
        ValidateOnlineSeatSymmetry();
        ValidateOnlineLobbyUiContract();
        ValidateCompleteOnlineTurns();
        ValidateReconnectWindow();
        ValidateServerRequestGate();
        ValidateProfileIsolation();
        ValidateLocalDevelopmentAndLanHost();
        ValidatePlayerViewProjection();
        ValidateOnlineActionabilityProjection();
        ValidateOnlineSeatRelativeTargetButtons();
        ValidateOnlineCombatInteractionProjection();
        ValidateOnlineBlockInteractionProjection();
        ValidateOnlineChoiceConfirmationProjection();
        ValidateOnlineLibrarySearchPresentation();
        ValidateContentManifest();
        ValidateMessageGuard();
        ValidateRoomIsolation();
        ValidateAuthoritativeLoopback();
    }

    private static void ValidateOnlineLobbyUiContract()
    {
        GameObject canvasObject = new GameObject("OnlineLobbyValidationCanvas", typeof(RectTransform), typeof(Canvas));
        GameObject controllerObject = new GameObject("OnlineLobbyValidationController");
        try
        {
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            GameObject startObject = new GameObject("StartScreen", typeof(RectTransform), typeof(CanvasGroup));
            startObject.transform.SetParent(canvasObject.transform, false);
            CanvasGroup startGroup = startObject.GetComponent<CanvasGroup>();
            GameObject deckObject = new GameObject("DeckBuilderScreen", typeof(RectTransform), typeof(CanvasGroup));
            deckObject.transform.SetParent(canvasObject.transform, false);
            CanvasGroup deckGroup = deckObject.GetComponent<CanvasGroup>();

            MainMenuController controller = controllerObject.AddComponent<MainMenuController>();
            typeof(MainMenuController).GetField("startScreenGroup", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.SetValue(controller, startGroup);
            typeof(MainMenuController).GetField("deckBuilderGroup", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.SetValue(controller, deckGroup);

            MethodInfo setLobbyVisible = typeof(MainMenuController).GetMethod(
                "SetOnlineLobbyVisible", BindingFlags.Instance | BindingFlags.Public);
            Require(setLobbyVisible != null, "The online lobby has no dedicated main-menu screen transition.");
            setLobbyVisible.Invoke(controller, new object[] { true });
            Require(!startObject.activeSelf && !deckObject.activeSelf,
                "Opening the online lobby left a previous main-menu screen visible.");
            setLobbyVisible.Invoke(controller, new object[] { false });
            Require(startObject.activeSelf && !deckObject.activeSelf,
                "Closing the online lobby did not restore the start screen.");

            MethodInfo create = typeof(OnlineLobbyPanel).GetMethod(
                "Create", BindingFlags.Static | BindingFlags.NonPublic);
            Require(create != null, "OnlineLobbyPanel.Create was not found.");
            OnlineLobbyPanel panel = create.Invoke(null, new object[] { canvasObject.transform }) as OnlineLobbyPanel;
            Require(panel != null, "OnlineLobbyPanel.Create did not produce a lobby page.");

            RectTransform page = panel.GetComponent<RectTransform>();
            Require(page.anchorMin == Vector2.zero && page.anchorMax == Vector2.one
                && page.offsetMin == Vector2.zero && page.offsetMax == Vector2.zero,
                "The online lobby is not a full-parent page.");

            Transform connectionPage = panel.transform.Find("Content/ConnectionPage");
            Transform roomPage = panel.transform.Find("Content/RoomPage");
            Transform sideboardPage = panel.transform.Find("Content/SideboardPage");
            Require(connectionPage != null && roomPage != null && sideboardPage != null
                && connectionPage != roomPage && roomPage != sideboardPage,
                "LAN connection, room preparation, and between-game sideboarding are not separate pages.");

            FieldInfo deckDropdownField = typeof(OnlineLobbyPanel).GetField(
                "deckDropdown", BindingFlags.Instance | BindingFlags.NonPublic);
            Dropdown boundDeckDropdown = deckDropdownField?.GetValue(panel) as Dropdown;
            Require(boundDeckDropdown != null && boundDeckDropdown.transform.IsChildOf(roomPage),
                "The in-room preparation page has no saved-deck selector.");

            FieldInfo mainDeckContentField = typeof(OnlineLobbyPanel).GetField(
                "mainDeckContent", BindingFlags.Instance | BindingFlags.NonPublic);
            FieldInfo sideboardContentField = typeof(OnlineLobbyPanel).GetField(
                "sideboardContent", BindingFlags.Instance | BindingFlags.NonPublic);
            RectTransform boundMainDeckContent = mainDeckContentField?.GetValue(panel) as RectTransform;
            RectTransform boundSideboardContent = sideboardContentField?.GetValue(panel) as RectTransform;
            Require(boundMainDeckContent != null && boundSideboardContent != null
                && boundMainDeckContent.transform.IsChildOf(sideboardPage)
                && boundSideboardContent.transform.IsChildOf(sideboardPage),
                "The dedicated sideboard page has no draggable main-deck and sideboard card grids.");
            Require(!boundMainDeckContent.transform.IsChildOf(roomPage)
                && !boundSideboardContent.transform.IsChildOf(roomPage),
                "The room preparation page still contains sideboarding controls.");
            Require(typeof(UnityEngine.EventSystems.IBeginDragHandler).IsAssignableFrom(typeof(OnlineSideboardCardTile))
                && typeof(UnityEngine.EventSystems.IEndDragHandler).IsAssignableFrom(typeof(OnlineSideboardCardTile)),
                "The dedicated sideboard card tile does not implement direct dragging.");
            Require(sideboardPage.Find("MainDeckCard/MoveToSideboardButton") == null
                && sideboardPage.Find("SideboardCard/MoveToMainButton") == null,
                "The sideboard page still uses move buttons instead of direct dragging.");

            FieldInfo toggleField = typeof(OnlineLobbyPanel).GetField(
                "customContentToggle", BindingFlags.Instance | BindingFlags.NonPublic);
            Toggle diy = toggleField?.GetValue(panel) as Toggle;
            Require(diy != null && diy.targetGraphic != null && diy.graphic != null,
                "The DIY toggle is missing its visual state graphics.");
            Require(diy.targetGraphic != diy.graphic,
                "The DIY toggle uses the same image for its background and checked state.");
            Require(diy.transform.Find("Background/Checkmark") != null,
                "The DIY toggle is missing a distinct Background/Checkmark hierarchy.");

            MethodInfo readConfig = typeof(OnlineLobbyPanel).GetMethod(
                "ReadRoomConfig", BindingFlags.Instance | BindingFlags.NonPublic);
            Require(toggleField != null && readConfig != null, "The lobby room-config bindings were not found.");
            Toggle boundToggle = toggleField.GetValue(panel) as Toggle;
            boundToggle.SetIsOnWithoutNotify(true);
            OnlineRoomConfig enabled = readConfig.Invoke(panel, null) as OnlineRoomConfig;
            boundToggle.SetIsOnWithoutNotify(false);
            OnlineRoomConfig disabled = readConfig.Invoke(panel, null) as OnlineRoomConfig;
            Require(enabled?.allowCustomContent == true && disabled?.allowCustomContent == false,
                "The DIY toggle state is not carried into the room configuration.");

            MethodInfo applyAuthoritativeConfig = typeof(OnlineLobbyPanel).GetMethod(
                "ApplyAuthoritativeRoomConfig", BindingFlags.Instance | BindingFlags.NonPublic);
            FieldInfo lifeField = typeof(OnlineLobbyPanel).GetField(
                "lifeInput", BindingFlags.Instance | BindingFlags.NonPublic);
            FieldInfo setsField = typeof(OnlineLobbyPanel).GetField(
                "allowedSetsInput", BindingFlags.Instance | BindingFlags.NonPublic);
            Require(applyAuthoritativeConfig != null && lifeField != null && setsField != null,
                "The lobby cannot display the host's authoritative room settings.");
            applyAuthoritativeConfig.Invoke(panel, new object[]
            {
                new OnlineRoomConfig
                {
                    startingLife = 32,
                    bestOf = 3,
                    allowCustomContent = true,
                    allowedSetCodes = new List<string> { "TST", "LAN" }
                }
            });
            InputField boundLife = lifeField.GetValue(panel) as InputField;
            InputField boundSets = setsField.GetValue(panel) as InputField;
            FieldInfo formatField = typeof(OnlineLobbyPanel).GetField(
                "formatDropdown", BindingFlags.Instance | BindingFlags.NonPublic);
            Dropdown boundFormat = formatField?.GetValue(panel) as Dropdown;
            Require(boundToggle.isOn && boundLife?.text == "32" && boundSets?.text == "TST,LAN"
                && boundFormat?.value == 1,
                "The joined client did not reflect the host's authoritative room settings.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(controllerObject);
            UnityEngine.Object.DestroyImmediate(canvasObject);
        }
    }

    private static void ValidateSeriesState()
    {
        var bestOfOne = new OnlineSeriesState();
        bestOfOne.Reset(1);
        bestOfOne.BeginGame();
        bestOfOne.RecordGameResult(2);
        Require(bestOfOne.finished && bestOfOne.winnerSeat == 2 && !bestOfOne.betweenGames,
            "BO1 did not finish after its first game.");

        var bestOfThree = new OnlineSeriesState();
        bestOfThree.Reset(3);
        bestOfThree.BeginGame();
        bestOfThree.RecordGameResult(1);
        Require(!bestOfThree.finished && bestOfThree.betweenGames && bestOfThree.gameNumber == 2
            && bestOfThree.player1Wins == 1,
            "BO3 did not enter the game-two sideboarding window.");
        bestOfThree.BeginGame();
        bestOfThree.RecordGameResult(2);
        Require(bestOfThree.betweenGames && bestOfThree.gameNumber == 3
            && bestOfThree.player1Wins == 1 && bestOfThree.player2Wins == 1,
            "BO3 did not preserve its tied score before game three.");
        bestOfThree.BeginGame();
        bestOfThree.RecordGameResult(1);
        Require(bestOfThree.finished && bestOfThree.winnerSeat == 1 && !bestOfThree.betweenGames,
            "BO3 did not finish when a player reached two wins.");
    }

    private static void ValidateProtocolRoundTrip()
    {
        var deck = new DeckDefinition { name = "Network Test" };
        deck.cards.Add(new DeckEntry { setCode = "TST", collectorNumber = "1", cardId = 42, count = 4 });
        var action = new GameAction
        {
            sequence = 7,
            expectedStateSequence = 6,
            type = GameActionType.SelectPlayerTarget,
            playerId = 2,
            targetPlayerId = 1,
            optionIds = { 3, 5 },
            values = { 2, 4 }
        };
        var view = new OnlinePlayerViewState
        {
            viewerSeat = 2,
            appliedSequence = 7,
            startingPlayer = 2,
            openingMulligan = true,
            player1MulliganCount = 1,
            player2MulliganCount = 2
        };
        view.SealHash();
        var payload = new OnlineStateUpdateMessage
        {
            requestId = 91,
            result = GameActionResult.Accepted(7, view.viewHash),
            view = view
        };
        var envelope = new OnlineEnvelope
        {
            type = OnlineProtocol.StateUpdate,
            payload = JsonUtility.ToJson(payload)
        };

        OnlineEnvelope decodedEnvelope = JsonUtility.FromJson<OnlineEnvelope>(JsonUtility.ToJson(envelope));
        OnlineStateUpdateMessage decoded = JsonUtility.FromJson<OnlineStateUpdateMessage>(decodedEnvelope.payload);
        Require(decodedEnvelope.version == OnlineProtocol.Version, "Protocol version was not preserved.");
        Require(decoded.requestId == 91, "Action request ID was not preserved.");
        Require(decoded?.view != null && decoded.view.viewerSeat == 2
            && decoded.view.startingPlayer == 2
            && decoded.view.openingMulligan
            && decoded.view.player1MulliganCount == 1
            && decoded.view.player2MulliganCount == 2,
            "Player view or opening-mulligan state was not preserved.");
        Require(decoded.result != null && decoded.result.accepted
            && decoded.result.stateHash == view.viewHash, "State update result was not preserved.");

        OnlineActionMessage actionRoundTrip = JsonUtility.FromJson<OnlineActionMessage>(JsonUtility.ToJson(
            new OnlineActionMessage { requestId = 8, action = action }));
        Require(actionRoundTrip.action.playerId == 2
            && actionRoundTrip.action.optionIds.Count == 2
            && actionRoundTrip.action.values[1] == 4, "Submitted action fields were not preserved.");

        var reconnectError = new OnlineErrorMessage
        {
            code = OnlineProtocol.ReconnectRejected,
            message = "expired"
        };
        OnlineErrorMessage decodedError = JsonUtility.FromJson<OnlineErrorMessage>(JsonUtility.ToJson(reconnectError));
        Require(decodedError.code == OnlineProtocol.ReconnectRejected,
            "Reconnect rejection codes were not preserved.");

        OnlineWelcomeMessage decodedWelcome = JsonUtility.FromJson<OnlineWelcomeMessage>(JsonUtility.ToJson(
            new OnlineWelcomeMessage { seat = 2, reconnectToken = "resume", resumingMatch = true, roomId = "test" }));
        Require(decodedWelcome.seat == 2 && decodedWelcome.resumingMatch && decodedWelcome.roomId == "test",
            "The reconnecting-match welcome flag was not preserved.");

        string deckJson = JsonUtility.ToJson(new OnlineDeckMessage { deck = deck });
        OnlineDeckMessage decodedDeck = JsonUtility.FromJson<OnlineDeckMessage>(deckJson);
        Require(decodedDeck?.deck?.cards?.Count == 1 && decodedDeck.deck.cards[0].PrintingKey == "TST/1", "Deck payload was not preserved.");

        OnlinePlayerContext.SetLocalPlayer(2);
        Require(OnlinePlayerContext.LocalPlayerId == 2 && OnlinePlayerContext.OpponentPlayerId == 1, "Seat perspective mapping failed.");
        OnlinePlayerContext.Reset();

        Array actionTypes = Enum.GetValues(typeof(GameActionType));
        for (int i = 0; i < actionTypes.Length; i++)
        {
            var message = new OnlineActionMessage
            {
                requestId = i + 1,
                action = new GameAction { type = (GameActionType)actionTypes.GetValue(i) }
            };
            OnlineActionMessage roundTrip = JsonUtility.FromJson<OnlineActionMessage>(JsonUtility.ToJson(message));
            Require(roundTrip.requestId == i + 1 && roundTrip.action.type == message.action.type,
                $"Game action {message.action.type} did not survive protocol serialization.");
        }
    }

    private static void ValidateInteractionGate()
    {
        var gate = new OnlineClientActionGate();
        Require(!gate.CanSubmit, "A disconnected client accepted rule input.");
        gate.SetConnected(true);
        Require(gate.CanSubmit, "A connected idle client rejected rule input.");
        Require(gate.TryBeginSubmit(11, 5d, 10d), "The first action submission was rejected.");
        Require(!gate.TryBeginSubmit(12, 6d, 10d), "A duplicate action was accepted while submitting.");
        Require(!gate.CompleteSubmit(12), "Another request incorrectly unlocked rule input.");
        Require(gate.CompleteSubmit(11) && gate.CanSubmit, "The matching response did not unlock rule input.");
        Require(gate.TryBeginSubmit(13, 20d, 2d), "The timeout test action was rejected.");
        Require(!gate.Tick(21.9d), "The action timed out before its deadline.");
        Require(gate.Tick(22d) && gate.State == OnlineClientInteractionState.Resyncing,
            "A timed-out action did not enter resynchronization.");
        Require(gate.CompleteResync() && gate.CanSubmit, "A confirmed resync did not restore rule input.");
    }

    private static void ValidateOnlinePriorityHudLabel()
    {
        MethodInfo labelResolver = typeof(ArenaHudController).GetMethod(
            "GetPriorityActionLabel",
            BindingFlags.Static | BindingFlags.NonPublic);
        Require(labelResolver != null,
            "The online HUD does not distinguish passing priority from advancing the phase.");
        Require((string)labelResolver.Invoke(null, new object[] { true, 2, 2 }) == "让过\n优先权",
            "The player with priority was not prompted to pass priority.");
        Require((string)labelResolver.Invoke(null, new object[] { true, 2, 1 }) == "等待\n对手",
            "The player without priority was not told to wait for the opponent.");
        Require(labelResolver.Invoke(null, new object[] { false, 2, 1 }) == null,
            "The priority HUD label remained active outside a priority window.");
    }

    private static void ValidatePriorityStateModule()
    {
        var state = new PriorityState();
        state.Begin(1);
        Require(state.IsOpen && state.PlayerWithPriority == 1,
            "A priority window did not open for its requested player.");
        Require(state.TryPass(2, 1) == PriorityPassOutcome.Rejected,
            "A player passed priority they did not hold.");
        Require(state.TryPass(1, 1) == PriorityPassOutcome.PassedToOpponent
                && state.IsOpen
                && state.PlayerWithPriority == 2,
            "The first pass did not hand priority to the opponent.");
        Require(state.TryPass(2, 1) == PriorityPassOutcome.WindowComplete
                && !state.IsOpen,
            "Two consecutive passes did not close the priority window.");

        Require(PriorityAutomationPolicy.ShouldAutoPass(false, 2, false, false, TurnPhase.Upkeep, false),
            "Offline opponent automation no longer passes priority.");
        Require(!PriorityAutomationPolicy.ShouldAutoPass(true, 2, true, false, TurnPhase.Upkeep, false),
            "Full control no longer preserves a priority stop.");
        Require(!PriorityAutomationPolicy.ShouldAutoPass(true, 1, false, true, TurnPhase.Main1, false),
            "The active player's main-phase decision was not preserved.");
        Require(PriorityAutomationPolicy.ShouldAutoPass(true, 2, false, false, TurnPhase.Main1, false)
                && !PriorityAutomationPolicy.ShouldAutoPass(true, 2, false, false, TurnPhase.Main1, true),
            "Online automatic passing no longer distinguishes legal responses.");
    }

    private static void ValidateOnlinePriorityAutomation()
    {
        GameObject rulesObject = new GameObject("Online Priority Automation Rules");
        try
        {
            GameRules rules = rulesObject.AddComponent<GameRules>();
            const BindingFlags instanceFlags = BindingFlags.Instance
                | BindingFlags.Public
                | BindingFlags.NonPublic;
            typeof(GameRules).GetField("currentPlayer", instanceFlags)?.SetValue(rules, 1);
            typeof(GameRules).GetField("currentPhase", instanceFlags)?.SetValue(
                rules,
                TurnPhase.DeclareAttackers);
            RestorePriority(rules, true, 2);
            typeof(GameRules).GetField("onlineAuthoritySession", instanceFlags)?.SetValue(rules, true);

            MethodInfo actionResolver = typeof(GameRules).GetMethod(
                "GetPrimaryAdvanceActionType",
                instanceFlags,
                null,
                new[] { typeof(int) },
                null);
            Require(actionResolver != null,
                "The primary phase action is not resolved from the acting player's seat.");
            Require((GameActionType)actionResolver.Invoke(rules, new object[] { 1 })
                    == GameActionType.CommitAttackers,
                "The active player could not confirm attackers.");
            Require((GameActionType)actionResolver.Invoke(rules, new object[] { 2 })
                    == GameActionType.PassPriority,
                "The defending player tried to commit the opponent's attackers instead of passing priority.");

            MethodInfo setFullControl = typeof(GameRules).GetMethod(
                "SetFullControl",
                instanceFlags,
                null,
                new[] { typeof(int), typeof(bool) },
                null);
            MethodInfo isFullControl = typeof(GameRules).GetMethod(
                "IsFullControl",
                instanceFlags,
                null,
                new[] { typeof(int) },
                null);
            MethodInfo shouldAutoPass = typeof(GameRules).GetMethod(
                "ShouldAutoPassPriority",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Require(setFullControl != null && isFullControl != null && shouldAutoPass != null,
                "Online priority automation is not tracked independently per player.");

            GetPriorityState(rules).Close();
            setFullControl.Invoke(rules, new object[] { 1, true });
            Require((bool)isFullControl.Invoke(rules, new object[] { 1 })
                    && !(bool)isFullControl.Invoke(rules, new object[] { 2 }),
                "One player's full-control setting leaked to the opponent.");

            RestorePriority(rules, true, 2);
            Require((bool)shouldAutoPass.Invoke(rules, null),
                "An online player without a legal response did not automatically pass priority.");

            GetPriorityState(rules).Close();
            setFullControl.Invoke(rules, new object[] { 2, true });
            RestorePriority(rules, true, 2);
            Require(!(bool)shouldAutoPass.Invoke(rules, null),
                "Full control did not preserve the online player's priority stop.");

            setFullControl.Invoke(rules, new object[] { 1, false });
            setFullControl.Invoke(rules, new object[] { 2, false });
            typeof(GameRules).GetField("currentPhase", instanceFlags)?.SetValue(rules, TurnPhase.Main1);
            RestorePriority(rules, true, 1);
            Require(!(bool)shouldAutoPass.Invoke(rules, null),
                "The active player's main phase was skipped because no spell was currently playable.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(rulesObject);
        }
    }

    private static void ValidateAuthoritativeCombatAnimationIsolation()
    {
        GameObject rulesObject = new GameObject("Authoritative Combat Animation Isolation Rules");
        int animationRequestCount = 0;
        Func<Action, bool> animationHandler = onComplete =>
        {
            animationRequestCount++;
            return true;
        };
        GameEvents.OnCombatAnimationRequested += animationHandler;
        try
        {
            GameRules rules = rulesObject.AddComponent<GameRules>();
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            Type rulesType = typeof(GameRules);
            rulesType.GetField("currentPlayer", flags)?.SetValue(rules, 1);
            rulesType.GetField("currentPhase", flags)?.SetValue(rules, TurnPhase.DeclareBlockers);
            RestorePriority(rules, true, 1);
            rulesType.GetField("onlineAuthoritySession", flags)?.SetValue(rules, true);
            rulesType.GetField("player1FullControl", flags)?.SetValue(rules, true);
            rulesType.GetField("player2FullControl", flags)?.SetValue(rules, true);
            rulesType.GetField("player1Life", flags)?.SetValue(rules, 20);
            rulesType.GetField("player2Life", flags)?.SetValue(rules, 20);

            Require(rules.PassPriority(1) && rules.PassPriority(2),
                "The authoritative combat repro could not pass both players' priority.");
            Require(animationRequestCount == 0,
                "Authoritative rules incorrectly requested a client combat animation.");
            Require(rules.GetCurrentPhase() == TurnPhase.CombatDamage
                    && rules.IsAwaitingPriority()
                    && rules.GetPriorityPlayer() == 1,
                "Authoritative combat damage did not restore priority to the active player.");
        }
        finally
        {
            GameEvents.OnCombatAnimationRequested -= animationHandler;
            UnityEngine.Object.DestroyImmediate(rulesObject);
        }
    }

    private static void ValidateOnlinePriorityHandoff()
    {
        GameObject rulesObject = new GameObject("Online Priority Handoff Rules");
        try
        {
            GameRules rules = rulesObject.AddComponent<GameRules>();
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            Type rulesType = typeof(GameRules);
            rulesType.GetField("currentPlayer", flags)?.SetValue(rules, 1);
            rulesType.GetField("currentPhase", flags)?.SetValue(rules, TurnPhase.Main1);
            RestorePriority(rules, true, 1);
            rulesType.GetField("onlineAuthoritySession", flags)?.SetValue(rules, true);
            rulesType.GetField("player1Life", flags)?.SetValue(rules, 20);
            rulesType.GetField("player2Life", flags)?.SetValue(rules, 20);

            Require(rules.PassPriority(1), "The active player could not pass priority.");
            Require(rules.GetCurrentPlayer() == 1
                    && rules.GetCurrentPhase() == TurnPhase.DeclareAttackers
                    && rules.IsAwaitingPriority()
                    && rules.GetPriorityPlayer() == 1,
                "Automatic passes skipped the active player's next required decision or handed off the turn.");

            rulesType.GetField("currentPhase", flags)?.SetValue(rules, TurnPhase.Main1);
            RestorePriority(rules, true, 1);
            RuntimeCard response = new RuntimeCard(
                new CardDefinition
                {
                    cardName = "P2 Priority Response",
                    cardTypes = CardType.Instant,
                    manaCost = string.Empty
                },
                2,
                false);
            response.SetCurrentZone(GameZone.Hand);
            ((List<RuntimeCard>)rulesType.GetField("player2Hand", flags)?.GetValue(rules))?.Add(response);

            Require(rules.PassPriority(1), "P1 could not hand priority to P2's response window.");
            Require(rules.GetCurrentPlayer() == 1
                    && rules.GetCurrentPhase() == TurnPhase.Main1
                    && rules.IsAwaitingPriority()
                    && rules.GetPriorityPlayer() == 2,
                "P1's pass skipped P2 even though P2 had a legal instant response.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(rulesObject);
        }
    }

    private static void ValidateOnlineSeatSymmetry()
    {
        ValidatePlayerTwoTriggerOrdering();
        ValidatePlayerTwoScriptCostChoice();
        ValidatePlayerTwoLibrarySearchChoice();
        ValidatePlayerTwoCombatDamageChoice();
    }

    private static void ValidatePlayerTwoTriggerOrdering()
    {
        GameObject rulesObject = new GameObject("Online Seat Symmetry Rules");
        try
        {
            GameRules rules = rulesObject.AddComponent<GameRules>();
            const BindingFlags instanceFlags = BindingFlags.Instance
                | BindingFlags.Public
                | BindingFlags.NonPublic;
            typeof(GameRules).GetField("currentPlayer", instanceFlags)?.SetValue(rules, 2);
            typeof(GameRules).GetField("onlineAuthoritySession", instanceFlags)?.SetValue(rules, true);

            var triggerSourceA = new RuntimeCard(
                new CardDefinition { cardName = "P2 Trigger A", cardTypes = CardType.Creature },
                2,
                false);
            var triggerSourceB = new RuntimeCard(
                new CardDefinition { cardName = "P2 Trigger B", cardTypes = CardType.Creature },
                2,
                false);
            var triggerSourceC = new RuntimeCard(
                new CardDefinition { cardName = "P1 Trigger A", cardTypes = CardType.Creature },
                1,
                false);
            var triggerSourceD = new RuntimeCard(
                new CardDefinition { cardName = "P1 Trigger B", cardTypes = CardType.Creature },
                1,
                false);
            var pendingTriggers = (List<GameStackItem>)typeof(GameRules).GetField(
                "pendingTriggeredAbilities",
                instanceFlags)?.GetValue(rules);
            Require(pendingTriggers != null,
                "The pending-trigger queue was unavailable for online seat validation.");
            pendingTriggers.Add(GameStackItem.TriggeredAbility(triggerSourceA, "OnTest"));
            pendingTriggers.Add(GameStackItem.TriggeredAbility(triggerSourceB, "OnTest"));
            pendingTriggers.Add(GameStackItem.TriggeredAbility(triggerSourceC, "OnTest"));
            pendingTriggers.Add(GameStackItem.TriggeredAbility(triggerSourceD, "OnTest"));

            MethodInfo putTriggersOnStack = typeof(GameRules).GetMethod(
                "PutPendingTriggeredAbilitiesOnStack",
                instanceFlags);
            Require(putTriggersOnStack != null
                    && (bool)putTriggersOnStack.Invoke(rules, null),
                "Player 2 simultaneous triggers were not processed.");
            GameChoiceRequest triggerOrder = rules.GetActiveChoice();
            Require(triggerOrder != null
                    && triggerOrder.kind == GameChoiceKind.TriggerOrder
                    && triggerOrder.choosingPlayerId == 2,
                "Player 2 simultaneous triggers did not create a Player 2 ordering choice.");
            Require(rules.ToggleTriggeredAbilityOrderItem(2, 0)
                    && rules.ToggleTriggeredAbilityOrderItem(2, 1)
                    && rules.ConfirmTriggerOrderChoice(2),
                "Player 2 could not complete simultaneous-trigger ordering.");
            GameChoiceRequest opponentTriggerOrder = rules.GetActiveChoice();
            Require(opponentTriggerOrder != null
                    && opponentTriggerOrder.kind == GameChoiceKind.TriggerOrder
                    && opponentTriggerOrder.choosingPlayerId == 1,
                "The non-active online player was not asked to order their own simultaneous triggers.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(rulesObject);
        }
    }

    private static void ValidatePlayerTwoScriptCostChoice()
    {
        GameObject rulesObject = new GameObject("Online P2 Script Cost Choice Rules");
        try
        {
            GameRules rules = rulesObject.AddComponent<GameRules>();
            const BindingFlags instanceFlags = BindingFlags.Instance
                | BindingFlags.Public
                | BindingFlags.NonPublic;
            Type rulesType = typeof(GameRules);
            rulesType.GetField("currentPlayer", instanceFlags)?.SetValue(rules, 2);
            rulesType.GetField("currentPhase", instanceFlags)?.SetValue(rules, TurnPhase.Main1);
            RestorePriority(rules, true, 2);
            rulesType.GetField("onlineAuthoritySession", instanceFlags)?.SetValue(rules, true);

            var source = new RuntimeCard(
                new CardDefinition { cardName = "P2 Cost Source", cardTypes = CardType.Creature },
                2,
                false);
            source.SetCurrentZone(GameZone.Battlefield);
            var discarded = new RuntimeCard(
                new CardDefinition { cardName = "P2 Cost Card", cardTypes = CardType.Instant },
                2,
                false);
            discarded.SetCurrentZone(GameZone.Hand);
            ((List<RuntimeCard>)rulesType.GetField("player2Battlefield", instanceFlags)?.GetValue(rules))?.Add(source);
            ((List<RuntimeCard>)rulesType.GetField("player2Hand", instanceFlags)?.GetValue(rules))?.Add(discarded);

            var ability = new ActivatedAbility
            {
                Name = "P2 Discard Cost",
                Cost = new ScriptAbilityCost { DiscardCount = 1 },
                Timing = ScriptAbilityTiming.AnyPriority
            };
            MethodInfo beginActivation = rulesType.GetMethod(
                "BeginScriptAbilityActivation",
                instanceFlags);
            Require(beginActivation != null,
                "The script-ability payment path was unavailable for Player 2 validation.");
            beginActivation.Invoke(rules, new object[] { source, ability });
            GameChoiceRequest choice = rules.GetActiveChoice();
            Require(choice != null
                    && choice.kind == GameChoiceKind.SelectOptions
                    && choice.choosingPlayerId == 2,
                "Player 2's selectable script-ability cost was resolved automatically instead of asking Player 2.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(rulesObject);
        }
    }

    private static void ValidatePlayerTwoLibrarySearchChoice()
    {
        GameObject rulesObject = new GameObject("Online P2 Library Search Rules");
        try
        {
            GameRules rules = rulesObject.AddComponent<GameRules>();
            const BindingFlags instanceFlags = BindingFlags.Instance
                | BindingFlags.Public
                | BindingFlags.NonPublic;
            Type rulesType = typeof(GameRules);
            rulesType.GetField("currentPlayer", instanceFlags)?.SetValue(rules, 2);
            rulesType.GetField("onlineAuthoritySession", instanceFlags)?.SetValue(rules, true);

            var source = new RuntimeCard(
                new CardDefinition { cardName = "P2 Fetch Land", cardTypes = CardType.Land },
                2,
                false);
            source.SetCurrentZone(GameZone.Battlefield);
            var basicLand = new RuntimeCard(
                new CardDefinition
                {
                    cardName = "P2 Searchable Basic",
                    cardTypes = CardType.Land,
                    supertypes = CardSupertype.Basic
                },
                2,
                false);
            basicLand.SetCurrentZone(GameZone.Library);
            ((List<RuntimeCard>)rulesType.GetField("player2Battlefield", instanceFlags)?.GetValue(rules))?.Add(source);
            ((List<RuntimeCard>)rulesType.GetField("player2Library", instanceFlags)?.GetValue(rules))?.Add(basicLand);

            MethodInfo activateFetch = rulesType.GetMethod(
                "ActivateFetchBasicLandAbility",
                instanceFlags);
            Require(activateFetch != null
                    && (bool)activateFetch.Invoke(rules, new object[] { source }),
                "Player 2's library-search ability did not start.");
            GameChoiceRequest choice = rules.GetActiveChoice();
            Require(choice != null
                    && choice.kind == GameChoiceKind.LibrarySearch
                    && choice.choosingPlayerId == 2,
                "Player 2's online library search selected a card automatically instead of asking Player 2.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(rulesObject);
        }
    }

    private static void ValidatePlayerTwoCombatDamageChoice()
    {
        GameObject rulesObject = new GameObject("Online P2 Combat Damage Choice Rules");
        try
        {
            GameRules rules = rulesObject.AddComponent<GameRules>();
            const BindingFlags instanceFlags = BindingFlags.Instance
                | BindingFlags.Public
                | BindingFlags.NonPublic;
            Type rulesType = typeof(GameRules);
            rulesType.GetField("currentPlayer", instanceFlags)?.SetValue(rules, 2);
            rulesType.GetField("currentPhase", instanceFlags)?.SetValue(rules, TurnPhase.CombatDamage);
            rulesType.GetField("onlineAuthoritySession", instanceFlags)?.SetValue(rules, true);

            var attacker = new RuntimeCard(
                new CardDefinition
                {
                    cardName = "P2 Trample Attacker",
                    cardTypes = CardType.Creature,
                    attack = 7,
                    defence = 7
                },
                2,
                false);
            attacker.SetCurrentZone(GameZone.Battlefield);
            attacker.AddKeyword("Trample");
            attacker.DeclareAttack(1);
            var blockerA = new RuntimeCard(
                new CardDefinition
                {
                    cardName = "P1 Blocker A",
                    cardTypes = CardType.Creature,
                    attack = 1,
                    defence = 2
                },
                1,
                false);
            blockerA.SetCurrentZone(GameZone.Battlefield);
            blockerA.DeclareBlock(attacker);
            var blockerB = new RuntimeCard(
                new CardDefinition
                {
                    cardName = "P1 Blocker B",
                    cardTypes = CardType.Creature,
                    attack = 1,
                    defence = 3
                },
                1,
                false);
            blockerB.SetCurrentZone(GameZone.Battlefield);
            blockerB.DeclareBlock(attacker);
            ((List<RuntimeCard>)rulesType.GetField("player2Battlefield", instanceFlags)?.GetValue(rules))?.Add(attacker);
            var p1Battlefield = (List<RuntimeCard>)rulesType.GetField(
                "player1Battlefield",
                instanceFlags)?.GetValue(rules);
            p1Battlefield?.Add(blockerA);
            p1Battlefield?.Add(blockerB);

            rules.ResolveCombat();
            GameChoiceRequest choice = rules.GetActiveChoice();
            Require(choice != null
                    && choice.kind == GameChoiceKind.CombatDamageAssignment
                    && choice.choosingPlayerId == 2,
                "Player 2's complex combat damage was assigned automatically instead of asking Player 2.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(rulesObject);
        }
    }

    private static void ValidateCompleteOnlineTurns()
    {
        ValidateCompleteOnlineTurn(1);
        ValidateCompleteOnlineTurn(2);
    }

    private static void ValidateCompleteOnlineTurn(int activePlayer)
    {
        int defendingPlayer = activePlayer == 1 ? 2 : 1;
        GameObject rulesObject = new GameObject($"Online Full Turn P{activePlayer} Rules");
        GameObject player1ViewObject = new GameObject($"Online Full Turn P{activePlayer} P1 View");
        GameObject player2ViewObject = new GameObject($"Online Full Turn P{activePlayer} P2 View");
        try
        {
            GameRules rules = rulesObject.AddComponent<GameRules>();
            GameRules player1View = player1ViewObject.AddComponent<GameRules>();
            GameRules player2View = player2ViewObject.AddComponent<GameRules>();
            const BindingFlags instanceFlags = BindingFlags.Instance
                | BindingFlags.Public
                | BindingFlags.NonPublic;
            Type rulesType = typeof(GameRules);
            rulesType.GetField("currentPlayer", instanceFlags)?.SetValue(rules, activePlayer);
            rulesType.GetField("currentPhase", instanceFlags)?.SetValue(rules, TurnPhase.Upkeep);
            rulesType.GetField("turnNumber", instanceFlags)?.SetValue(rules, 2);
            RestorePriority(rules, true, activePlayer);
            rulesType.GetField("onlineAuthoritySession", instanceFlags)?.SetValue(rules, true);
            rulesType.GetField("player1FullControl", instanceFlags)?.SetValue(rules, true);
            rulesType.GetField("player2FullControl", instanceFlags)?.SetValue(rules, true);
            rulesType.GetField("player1Life", instanceFlags)?.SetValue(rules, 20);
            rulesType.GetField("player2Life", instanceFlags)?.SetValue(rules, 20);
            rulesType.GetField("player1MaxHandSize", instanceFlags)?.SetValue(rules, 7);
            rulesType.GetField("player2MaxHandSize", instanceFlags)?.SetValue(rules, 7);

            var drawCard = new RuntimeCard(
                new CardDefinition
                {
                    cardName = $"P{activePlayer} Full Turn Draw",
                    cardTypes = CardType.Creature,
                    manaCost = "{9}",
                    attack = 1,
                    defence = 1
                },
                activePlayer,
                false);
            drawCard.ReassignObjectId(70000 + activePlayer * 100);
            drawCard.SetCurrentZone(GameZone.Library);

            var attacker = new RuntimeCard(
                new CardDefinition
                {
                    cardName = $"P{activePlayer} Full Turn Attacker",
                    cardTypes = CardType.Creature,
                    attack = 2,
                    defence = 2
                },
                activePlayer,
                false);
            attacker.ReassignObjectId(70001 + activePlayer * 100);
            attacker.SetCurrentZone(GameZone.Battlefield);
            attacker.TurnEnteredBattlefield = 1;

            var blocker = new RuntimeCard(
                new CardDefinition
                {
                    cardName = $"P{defendingPlayer} Full Turn Blocker",
                    cardTypes = CardType.Creature,
                    attack = 1,
                    defence = 3
                },
                defendingPlayer,
                false);
            blocker.ReassignObjectId(70002 + activePlayer * 100);
            blocker.SetCurrentZone(GameZone.Battlefield);
            blocker.TurnEnteredBattlefield = 1;

            var activeLibrary = (List<RuntimeCard>)rulesType.GetField(
                activePlayer == 1 ? "player1Library" : "player2Library",
                instanceFlags)?.GetValue(rules);
            var activeBattlefield = (List<RuntimeCard>)rulesType.GetField(
                activePlayer == 1 ? "player1Battlefield" : "player2Battlefield",
                instanceFlags)?.GetValue(rules);
            var defendingBattlefield = (List<RuntimeCard>)rulesType.GetField(
                defendingPlayer == 1 ? "player1Battlefield" : "player2Battlefield",
                instanceFlags)?.GetValue(rules);
            Require(activeLibrary != null
                    && activeBattlefield != null
                    && defendingBattlefield != null,
                $"P{activePlayer} full-turn zones were unavailable.");
            activeLibrary.Add(drawCard);
            activeBattlefield.Add(attacker);
            defendingBattlefield.Add(blocker);

            var visitedPhases = new HashSet<TurnPhase>();
            bool attackerSelected = false;
            bool blockerDeclared = false;
            int actionCount = 0;
            while (rules.GetCurrentPlayer() == activePlayer && actionCount++ < 64)
            {
                TurnPhase phase = rules.GetCurrentPhase();
                visitedPhases.Add(phase);
                Require(!rules.HasActiveChoice(),
                    $"P{activePlayer} full turn stalled on an unexpected {rules.GetActiveChoice()?.kind} choice in {phase}.");
                Require(rules.IsAwaitingPriority(),
                    $"P{activePlayer} full turn lost its priority window in {phase}.");

                Require(player1View.ApplyOnlinePlayerView(rules.CreateOnlinePlayerView(1), out string p1ViewError),
                    p1ViewError);
                Require(player2View.ApplyOnlinePlayerView(rules.CreateOnlinePlayerView(2), out string p2ViewError),
                    p2ViewError);
                GameRules priorityView = rules.GetPriorityPlayer() == 1 ? player1View : player2View;
                GameRules waitingView = rules.GetPriorityPlayer() == 1 ? player2View : player1View;
                int prioritySeat = rules.GetPriorityPlayer();
                int waitingSeat = prioritySeat == 1 ? 2 : 1;
                Require(priorityView.GetCurrentPlayer() == rules.GetCurrentPlayer()
                        && priorityView.GetCurrentPhase() == phase
                        && priorityView.GetPriorityPlayer() == prioritySeat,
                    $"P{prioritySeat}'s player view diverged from the authoritative turn state in {phase}.");
                Require(priorityView.CanPlayerAdvance(prioritySeat)
                        && !waitingView.CanPlayerAdvance(waitingSeat)
                        && priorityView.GetPrimaryAdvanceActionType(prioritySeat)
                            == rules.GetPrimaryAdvanceActionType(prioritySeat),
                    $"The projected priority/advance interaction was asymmetric in {phase}.");

                if (phase == TurnPhase.DeclareAttackers && !attackerSelected)
                {
                    GameActionResult selectAttacker = rules.ApplyAction(new GameAction
                    {
                        type = GameActionType.ToggleAttacker,
                        playerId = activePlayer,
                        zoneIndex = 0
                    });
                    Require(selectAttacker.accepted,
                        $"P{activePlayer} could not select an attacker: {selectAttacker.error}");
                    attackerSelected = true;
                    continue;
                }

                if (phase == TurnPhase.DeclareBlockers && !blockerDeclared)
                {
                    if (rules.GetPriorityPlayer() != defendingPlayer)
                    {
                        GameActionResult handoff = rules.ApplyAction(new GameAction
                        {
                            type = GameActionType.PassPriority,
                            playerId = rules.GetPriorityPlayer()
                        });
                        Require(handoff.accepted,
                            $"P{activePlayer} could not hand blocker priority to P{defendingPlayer}: {handoff.error}");
                        continue;
                    }
                    GameActionResult declareBlocker = rules.ApplyAction(new GameAction
                    {
                        type = GameActionType.DeclareBlocker,
                        playerId = defendingPlayer,
                        zoneIndex = 0,
                        targetObjectId = attacker.ObjectId
                    });
                    Require(declareBlocker.accepted,
                        $"P{defendingPlayer} could not declare a blocker against P{activePlayer}: {declareBlocker.error}");
                    blockerDeclared = true;
                    continue;
                }

                int priorityPlayer = rules.GetPriorityPlayer();
                GameActionType actionType = rules.GetPrimaryAdvanceActionType(priorityPlayer);
                GameActionResult advance = rules.ApplyAction(new GameAction
                {
                    type = actionType,
                    playerId = priorityPlayer
                });
                Require(advance.accepted,
                    $"P{priorityPlayer} could not continue P{activePlayer}'s turn in {phase} with {actionType}: {advance.error}");
            }

            Require(actionCount < 64,
                $"P{activePlayer} full turn exceeded the action guard in {rules.GetCurrentPhase()}.");
            Require(rules.GetCurrentPlayer() == defendingPlayer
                    && rules.GetTurnNumber() == 3
                    && rules.GetCurrentPhase() == TurnPhase.Upkeep
                    && rules.IsAwaitingPriority()
                    && rules.GetPriorityPlayer() == defendingPlayer,
                $"P{activePlayer} full turn did not hand off to P{defendingPlayer}'s upkeep.");
            Require(attackerSelected && blockerDeclared,
                $"P{activePlayer} full turn did not exercise both attacker and blocker declarations.");

            TurnPhase[] requiredPhases =
            {
                TurnPhase.Upkeep,
                TurnPhase.Draw,
                TurnPhase.Main1,
                TurnPhase.BeginningOfCombat,
                TurnPhase.DeclareAttackers,
                TurnPhase.DeclareBlockers,
                TurnPhase.CombatDamage,
                TurnPhase.EndOfCombat,
                TurnPhase.Main2,
                TurnPhase.End
            };
            for (int i = 0; i < requiredPhases.Length; i++)
            {
                Require(visitedPhases.Contains(requiredPhases[i]),
                    $"P{activePlayer} full turn skipped {requiredPhases[i]}.");
            }
        }
        finally
        {
            OnlinePlayerContext.Reset();
            UnityEngine.Object.DestroyImmediate(player1ViewObject);
            UnityEngine.Object.DestroyImmediate(player2ViewObject);
            UnityEngine.Object.DestroyImmediate(rulesObject);
        }
    }

    private static void ValidateReconnectWindow()
    {
        var resumable = new OnlineReconnectWindow(30d);
        resumable.Begin(100d);
        Require(resumable.IsActive && resumable.CanResume(129.99d),
            "A disconnected seat was not resumable during its grace period.");
        Require(Math.Abs(resumable.RemainingSeconds(120d) - 10d) < 0.001d,
            "Reconnect grace remaining time was incorrect.");
        Require(resumable.TryComplete(120d) && !resumable.IsActive,
            "A reconnect inside the grace period did not complete.");

        var expired = new OnlineReconnectWindow(30d);
        expired.Begin(200d);
        Require(!expired.CanResume(230d) && expired.IsExpired(230d),
            "An expired reconnect window still accepted a client.");
        Require(!expired.TryComplete(231d), "An expired reconnect window completed unexpectedly.");
    }

    private static void ValidateServerRequestGate()
    {
        var gate = new OnlineServerRequestGate();
        var spoofed = new GameAction { type = GameActionType.SetFullControl, playerId = 2 };
        Require(gate.TryPrepare(1, 1, spoofed, out string error), error);
        Require(spoofed.playerId == 1 && spoofed.sequence == 0,
            "The authenticated seat did not replace the claimed action seat.");
        Require(!gate.TryPrepare(1, 1, spoofed.Clone(), out error),
            "A duplicate request ID was accepted.");
        Require(!gate.TryPrepare(0, 1, spoofed.Clone(), out error),
            "A non-positive request ID was accepted.");
        Require(gate.TryPrepare(3, 1, spoofed.Clone(), out error), error);
        Require(!gate.TryPrepare(2, 1, spoofed.Clone(), out error),
            "An out-of-order request ID was accepted.");
        Require(!gate.TryPrepare(4, 1, null, out error),
            "A request without an action payload was accepted.");

        var invalidSeatGate = new OnlineServerRequestGate();
        Require(!invalidSeatGate.TryPrepare(1, 3, spoofed.Clone(), out error),
            "An invalid authenticated seat was accepted.");
    }

    private static void ValidateProfileIsolation()
    {
        string suffix = Guid.NewGuid().ToString("N");
        string firstId = "validation-a-" + suffix;
        string secondId = "validation-b-" + suffix;
        var firstDeck = new DeckDefinition { name = "First Profile Deck" };
        var secondDeck = new DeckDefinition { name = "Second Profile Deck" };
        try
        {
            OnlineClientProfileStore.SaveConnection(firstId, "127.0.0.1", 7777, "Alice", "alpha");
            OnlineClientProfileStore.SaveDeck(firstId, firstDeck);
            OnlineClientProfileStore.SaveReconnectToken(firstId, "token-a", "alpha");
            OnlineClientProfileStore.SaveConnection(secondId, "127.0.0.2", 8888, "Bob", "beta");
            OnlineClientProfileStore.SaveDeck(secondId, secondDeck);
            OnlineClientProfileStore.SaveReconnectToken(secondId, "token-b", "beta");

            OnlineClientProfile first = OnlineClientProfileStore.Load(firstId);
            OnlineClientProfile second = OnlineClientProfileStore.Load(secondId);
            Require(first.displayName == "Alice" && second.displayName == "Bob", "Profile names were not isolated.");
            Require(first.port == 7777 && second.port == 8888, "Profile endpoints were not isolated.");
            Require(first.roomId == "alpha" && second.roomId == "beta", "Profile room IDs were not isolated.");
            Require(first.reconnectToken == "token-a" && second.reconnectToken == "token-b",
                "Reconnect tokens were not isolated.");
            Require(first.deck?.name == "First Profile Deck" && second.deck?.name == "Second Profile Deck",
                "Profile decks were not isolated.");

            DeleteValidationPlayerPrefs(firstId);
            DeleteValidationPlayerPrefs(secondId);
            first = OnlineClientProfileStore.Load(firstId);
            second = OnlineClientProfileStore.Load(secondId);
            Require(first.displayName == "Alice" && second.displayName == "Bob"
                && first.deck?.name == "First Profile Deck" && second.deck?.name == "Second Profile Deck",
                "Shared development profiles were not available outside the PlayerPrefs instance that created them.");
        }
        finally
        {
            DeleteValidationProfile(firstId);
            DeleteValidationProfile(secondId);
        }
    }

    private static void ValidateLocalDevelopmentAndLanHost()
    {
        string projectRoot = PrintingKeyUtility.ApplicationRoot;
        string virtualDataPath = Path.Combine(projectRoot, "Library", "VP", "Player2", "Assets");
        Require(string.Equals(
                PrintingKeyUtility.ResolveApplicationRoot(virtualDataPath),
                projectRoot,
                StringComparison.OrdinalIgnoreCase),
            "A Multiplayer Play Mode instance did not resolve the authoring project's card-data root.");

        MethodInfo direction = typeof(OnlineMatchService).GetMethod(
            "IsServerBoundMessage",
            BindingFlags.Static | BindingFlags.NonPublic);
        Require(direction != null
            && (bool)direction.Invoke(null, new object[] { OnlineProtocol.Hello })
            && !(bool)direction.Invoke(null, new object[] { OnlineProtocol.Welcome }),
            "Host-mode messages cannot distinguish local client requests from server responses.");

        string profileId = "validation-lan-" + Guid.NewGuid().ToString("N");
        OnlineClientProfileStore.SaveConnection(profileId, "127.0.0.1", 17777, "LAN Host", "validation-room");
        OnlineClientProfileStore.SaveReconnectToken(profileId, "stale-reconnect-token", "validation-room");

        GameObject serviceObject = new GameObject("Online LAN Validation Service");
        GameObject managerObject = null;
        try
        {
            OnlineMatchService service = serviceObject.AddComponent<OnlineMatchService>();
            typeof(OnlineMatchService).GetField("currentProfileId", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.SetValue(service, profileId);
            typeof(OnlineMatchService).GetField("currentRoomId", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.SetValue(service, "validation-room");
            FieldInfo reconnectingField = typeof(OnlineMatchService).GetField(
                "reconnectingMatch",
                BindingFlags.Instance | BindingFlags.NonPublic);
            MethodInfo buildHello = typeof(OnlineMatchService).GetMethod(
                "BuildHelloMessage",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Require(reconnectingField != null && buildHello != null,
                "The LAN hello construction path was not found.");
            OnlineHelloMessage freshHello = (OnlineHelloMessage)buildHello.Invoke(service, null);
            Require(string.IsNullOrEmpty(freshHello.reconnectToken),
                "A fresh LAN host/join attempt reused a stale reconnect token.");
            reconnectingField.SetValue(service, true);
            OnlineHelloMessage reconnectHello = (OnlineHelloMessage)buildHello.Invoke(service, null);
            Require(reconnectHello.reconnectToken == "stale-reconnect-token",
                "An automatic match reconnect did not retain its reconnect token.");

            MethodInfo create = typeof(OnlineMatchService).GetMethod(
                "CreateNetworkManager",
                BindingFlags.Instance | BindingFlags.NonPublic);
            FieldInfo managerField = typeof(OnlineMatchService).GetField(
                "networkManager",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Require(create != null && managerField != null
                && (bool)create.Invoke(service, new object[] { "127.0.0.1", (ushort)17777, false }),
                "The runtime NetworkManager could not be created for validation.");
            NetworkManager manager = managerField.GetValue(service) as NetworkManager;
            managerObject = manager != null ? manager.gameObject : null;
            Require(managerObject != null && managerObject.transform.parent == null,
                "NetworkManager must be a root GameObject for Netcode for GameObjects 2.x.");
            managerField.SetValue(service, null);
            typeof(OnlineMatchService).GetField("transport", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.SetValue(service, null);
        }
        finally
        {
            if (managerObject != null) UnityEngine.Object.DestroyImmediate(managerObject);
            UnityEngine.Object.DestroyImmediate(serviceObject);
            DeleteValidationProfile(profileId);
        }
    }

    private static void ValidatePlayerViewProjection()
    {
        GameObject serverObject = new GameObject("Online Player View Server");
        GameObject clientObject = new GameObject("Online Player View Client");
        try
        {
            GameRules server = serverObject.AddComponent<GameRules>();
            InitializeRules(server, 41027);
            OnlinePlayerViewState firstView = server.CreateOnlinePlayerView(1);
            OnlinePlayerViewState secondView = server.CreateOnlinePlayerView(2);
            OnlinePlayerViewState spectatorView = server.CreateOnlinePlayerView(0);

            Require(firstView != null && secondView != null && spectatorView != null,
                "The server did not create both player and spectator perspectives.");
            Require(!OnlinePlayerViewSecurity.ContainsHiddenIdentity(firstView, 2),
                "Player 1 received an opponent hidden-zone identity.");
            Require(!OnlinePlayerViewSecurity.ContainsHiddenIdentity(secondView, 1),
                "Player 2 received an opponent hidden-zone identity.");
            Require(!OnlinePlayerViewSecurity.ContainsHiddenIdentity(spectatorView, 1)
                && !OnlinePlayerViewSecurity.ContainsHiddenIdentity(spectatorView, 2),
                "The spectator projection received a hidden-zone identity.");
            Require(firstView.player1Hand.Count == server.GetHandCount(1)
                && firstView.player2Hand.Count == server.GetHandCount(2),
                "The projected hand counts do not match the authoritative state.");
            Require(firstView.player2Hand.TrueForAll(card => card != null && card.hidden && card.objectId < 0),
                "Opponent hand cards were not represented by temporary hidden objects.");
            Require(firstView.player2Library.TrueForAll(card => card != null && card.hidden && card.objectId < 0),
                "Opponent library cards were not represented by temporary hidden objects.");

            CardDefinition visibleDefinition = null;
            var definitions = CardDatabaseRepository.LoadAll();
            for (int i = 0; i < definitions.Count && visibleDefinition == null; i++)
            {
                if (definitions[i] != null && !string.IsNullOrEmpty(definitions[i].PrintingKey))
                    visibleDefinition = definitions[i];
            }
            Require(visibleDefinition != null, "The validation database contains no face-down test card.");
            var faceDown = new RuntimeCard(visibleDefinition, 1, initializeScript: false);
            faceDown.AssignObjectId(991);
            faceDown.AddContinuousEffect(new ContinuousEffectRuntime
            {
                operation = ContinuousEffectOperation.SetFaceDownCharacteristics,
                cardTypes = CardType.Creature,
                power = 2,
                toughness = 2
            });
            MethodInfo projector = typeof(GameRules).GetMethod(
                "CreateCardView",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Require(projector != null, "The online card projector was not found.");
            var controllerCard = (OnlineVisibleCard)projector.Invoke(server, new object[] { faceDown, true, 0, 1 });
            var opponentCard = (OnlineVisibleCard)projector.Invoke(server, new object[] { faceDown, true, 0, 2 });
            Require(!controllerCard.hidden && controllerCard.cardId == visibleDefinition.cardID,
                "The controller could not see their face-down permanent identity.");
            Require(opponentCard.hidden && opponentCard.publicObject && opponentCard.objectId == 991
                && opponentCard.cardId == 0 && opponentCard.visibleCardTypes == CardType.Creature,
                "A face-down public permanent leaked identity or lost its public characteristics.");
            faceDown.OnDestroy();

            GameRules client = clientObject.AddComponent<GameRules>();
            Require(client.ApplyOnlinePlayerView(firstView, out string error), error);
            Require(client.GetHandCount(1) == server.GetHandCount(1)
                && client.GetHandCount(2) == server.GetHandCount(2),
                "The client did not apply the projected zone counts.");
            Require(client.ComputeDeterministicStateHash() == firstView.viewHash,
                "The client did not retain the player-view hash.");

            OnlinePlayerViewState noChoiceView = JsonUtility.FromJson<OnlinePlayerViewState>(
                JsonUtility.ToJson(firstView));
            noChoiceView.hasActiveChoice = false;
            noChoiceView.activeChoice = null;
            noChoiceView.SealHash();
            OnlinePlayerViewState noChoiceWireView = JsonUtility.FromJson<OnlinePlayerViewState>(
                JsonUtility.ToJson(noChoiceView));
            Require(noChoiceWireView.activeChoice != null && !noChoiceWireView.hasActiveChoice,
                "The null-choice JsonUtility reproduction no longer exercises the wire-format edge case.");
            Require(client.ApplyOnlinePlayerView(noChoiceWireView, out error), error);
            Require(!client.HasActiveChoice(),
                "A wire view without an active choice created a phantom default choice on the client.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(serverObject);
            UnityEngine.Object.DestroyImmediate(clientObject);
        }
    }

    private static void ValidateOnlineActionabilityProjection()
    {
        GameObject serverObject = new GameObject("Online Actionability Server");
        GameObject clientObject = new GameObject("Online Actionability Client");
        try
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            Type rulesType = typeof(GameRules);
            GameRules server = serverObject.AddComponent<GameRules>();
            rulesType.GetField("currentPlayer", flags)?.SetValue(server, 2);
            rulesType.GetField("currentPhase", flags)?.SetValue(server, TurnPhase.Main1);
            rulesType.GetField("turnNumber", flags)?.SetValue(server, 6);
            rulesType.GetField("onlineAuthoritySession", flags)?.SetValue(server, true);
            rulesType.GetField("player1Life", flags)?.SetValue(server, 20);
            rulesType.GetField("player2Life", flags)?.SetValue(server, 20);
            RestorePriority(server, true, 1);

            var islandDefinition = new CardDefinition
            {
                cardID = 91001,
                setCode = "TST",
                collectorNumberText = "actionability-island",
                cardName = "Actionability Island",
                cardTypes = CardType.Land
            };
            var quickStudyDefinition = new CardDefinition
            {
                cardID = 91002,
                setCode = "TST",
                collectorNumberText = "actionability-quick-study",
                cardName = "Actionability Quick Study",
                cardTypes = CardType.Instant,
                manaCost = "{2}{U}"
            };

            var serverBattlefield = (List<RuntimeCard>)rulesType.GetField(
                "player1Battlefield", flags)?.GetValue(server);
            var serverHand = (List<RuntimeCard>)rulesType.GetField(
                "player1Hand", flags)?.GetValue(server);
            Require(serverBattlefield != null && serverHand != null,
                "The authoritative actionability zones were unavailable.");

            for (int i = 0; i < 3; i++)
            {
                var island = new RuntimeCard(islandDefinition, 1, initializeScript: false);
                island.ReassignObjectId(91010 + i);
                island.SetCurrentZone(GameZone.Battlefield);
                island.Abilities.Add(new ActivatedAbility
                {
                    Name = "Add U",
                    RuleTraits = ActivatedAbilityRuleTrait.CouldAddMana,
                    ProducedMana = new ManaPool { Blue = 1 },
                    CanActivate = (_, source) => source != null && !source.IsTapped
                });
                serverBattlefield.Add(island);
            }

            var quickStudy = new RuntimeCard(quickStudyDefinition, 1, initializeScript: false);
            quickStudy.ReassignObjectId(91020);
            quickStudy.SetCurrentZone(GameZone.Hand);
            serverHand.Add(quickStudy);

            Require(server.CanActivateManaAbility(serverBattlefield[0]),
                "The authoritative server did not recognize an untapped land mana ability.");
            Require(server.CanPlayCard(1, 0),
                "The authoritative server did not recognize the payable 2U instant.");

            OnlinePlayerViewState view = server.CreateOnlinePlayerView(1);
            GameRules client = clientObject.AddComponent<GameRules>();
            Require(client.ApplyOnlinePlayerView(view, out string error), error);
            RuntimeCard clientLand = client.GetBattlefieldCard(1, 0);
            Require(clientLand != null && client.CanActivateManaAbility(clientLand),
                "The online player view lost immediate land mana actionability.");
            Require(client.CanPlayCard(1, 0),
                "The online player view lost the payable 2U instant actionability.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(serverObject);
            UnityEngine.Object.DestroyImmediate(clientObject);
        }
    }

    private static void ValidateOnlineCombatInteractionProjection()
    {
        GameObject serverObject = new GameObject("Online Combat Interaction Server");
        GameObject clientObject = new GameObject("Online Combat Interaction Client");
        try
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            Type rulesType = typeof(GameRules);
            GameRules server = serverObject.AddComponent<GameRules>();
            rulesType.GetField("currentPlayer", flags)?.SetValue(server, 1);
            rulesType.GetField("currentPhase", flags)?.SetValue(server, TurnPhase.DeclareAttackers);
            rulesType.GetField("turnNumber", flags)?.SetValue(server, 6);
            rulesType.GetField("onlineAuthoritySession", flags)?.SetValue(server, true);
            rulesType.GetField("player1Life", flags)?.SetValue(server, 20);
            rulesType.GetField("player2Life", flags)?.SetValue(server, 20);
            RestorePriority(server, true, 1);

            var creatureDefinition = new CardDefinition
            {
                cardID = 92001,
                setCode = "TST",
                collectorNumberText = "combat-interaction-creature",
                cardName = "Combat Interaction Creature",
                cardTypes = CardType.Creature,
                attack = 2,
                defence = 2
            };
            var establishedCreature = new RuntimeCard(
                creatureDefinition, 1, initializeScript: false);
            establishedCreature.ReassignObjectId(92010);
            establishedCreature.SetCurrentZone(GameZone.Battlefield);
            establishedCreature.TurnEnteredBattlefield = 5;
            var summoningSickCreature = new RuntimeCard(
                creatureDefinition, 1, initializeScript: false);
            summoningSickCreature.ReassignObjectId(92011);
            summoningSickCreature.SetCurrentZone(GameZone.Battlefield);
            summoningSickCreature.TurnEnteredBattlefield = 6;

            var battlefield = (List<RuntimeCard>)rulesType.GetField(
                "player1Battlefield", flags)?.GetValue(server);
            Require(battlefield != null, "The authoritative combat battlefield was unavailable.");
            battlefield.Add(establishedCreature);
            battlefield.Add(summoningSickCreature);
            Require(server.CanDeclareAttacker(1, 0)
                    && !server.CanDeclareAttacker(1, 1),
                "The authoritative combat setup did not distinguish an established creature from summoning sickness.");
            Require(server.TogglePendingAttacker(1, 0),
                "The authoritative server could not select the established attacker.");
            Require(server.IsPendingAttacker(establishedCreature)
                    && !establishedCreature.IsAttacking
                    && server.GetPendingAttackerTarget(establishedCreature) is int playerTarget
                    && playerTarget == 2,
                "The authoritative pending-attacker state was not established.");

            OnlinePlayerViewState view = server.CreateOnlinePlayerView(1);
            GameRules client = clientObject.AddComponent<GameRules>();
            Require(client.ApplyOnlinePlayerView(view, out string error), error);
            RuntimeCard clientEstablished = client.GetBattlefieldCard(1, 0);
            Require(client.CanDeclareAttacker(1, 0)
                    && !client.CanDeclareAttacker(1, 1),
                "The online player view changed which creatures can legally attack.");
            Require(client.IsPendingAttacker(clientEstablished)
                    && !clientEstablished.IsAttacking,
                "The online player view lost or conflated pending and declared attackers.");
            Require(client.GetPendingAttackerTarget(clientEstablished) is int clientPlayerTarget
                    && clientPlayerTarget == 2,
                "The online player view lost the pending attack target.");

            RestorePriority(server, true, 2);
            Require(client.ApplyOnlinePlayerView(server.CreateOnlinePlayerView(1), out error), error);
            clientEstablished = client.GetBattlefieldCard(1, 0);
            Require(!client.CanDeclareAttacker(1, 0)
                    && !client.CanAttackTarget(clientEstablished, 2),
                "The online player view kept attacker interactions after priority moved to the opponent.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(serverObject);
            UnityEngine.Object.DestroyImmediate(clientObject);
        }
    }

    private static void ValidateOnlineSeatRelativeTargetButtons()
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Static
            | BindingFlags.Public | BindingFlags.NonPublic;
        FieldInfo serviceInstanceField = typeof(OnlineMatchService).GetField(
            "<Instance>k__BackingField", flags);
        object existingService = serviceInstanceField?.GetValue(null);
        GameObject rulesObject = new GameObject("Online P2 Target Button Rules");
        GameObject interactionObject = new GameObject("Online P2 Target Button Interaction");
        GameObject localButtonObject = new GameObject(
            "Local Player Target", typeof(RectTransform), typeof(Image), typeof(Button));
        GameObject opponentButtonObject = new GameObject(
            "Opponent Player Target", typeof(RectTransform), typeof(Image), typeof(Button));
        GameObject handRootObject = new GameObject("P2 Local Hand Root", typeof(RectTransform));
        try
        {
            serviceInstanceField?.SetValue(null, null);
            OnlinePlayerContext.SetLocalPlayer(2);
            GameRules rules = rulesObject.AddComponent<GameRules>();
            typeof(GameRules).GetField("onlineViewOnly", flags)?.SetValue(rules, true);
            typeof(GameRules).GetField("activeChoice", flags)?.SetValue(rules, new GameChoiceRequest
            {
                requestId = 1,
                kind = GameChoiceKind.SpellTarget,
                choosingPlayerId = 2,
                minimumSelections = 1,
                maximumSelections = 1,
                options = new List<GameChoiceOption>
                {
                    new GameChoiceOption(1, GameChoiceZone.Player, 1, -1, true, "Opponent")
                }
            });

            ArenaWhiteboxInteraction interaction = interactionObject.AddComponent<ArenaWhiteboxInteraction>();
            Button localButton = localButtonObject.GetComponent<Button>();
            Button opponentButton = opponentButtonObject.GetComponent<Button>();
            typeof(ArenaWhiteboxInteraction).GetField("gameRules", flags)?.SetValue(interaction, rules);
            typeof(ArenaWhiteboxInteraction).GetField("playerTargetButton", flags)?.SetValue(interaction, localButton);
            typeof(ArenaWhiteboxInteraction).GetField("opponentTargetButton", flags)?.SetValue(interaction, opponentButton);
            typeof(ArenaWhiteboxInteraction).GetField("choosingSpellTarget", flags)?.SetValue(interaction, true);
            typeof(ArenaWhiteboxInteraction).GetField("targetHandOwner", flags)?.SetValue(interaction, 2);
            MethodInfo refresh = typeof(ArenaWhiteboxInteraction).GetMethod(
                "RefreshTargetButtons", flags);
            Require(refresh != null, "The seat-relative target-button refresh path was not found.");
            refresh.Invoke(interaction, null);
            Require(!localButton.interactable && opponentButton.interactable,
                "Player 2 target buttons used fixed P1/P2 seats instead of the local/opponent perspective.");

            var handVisuals = new List<MTGAHandUiCard>();
            MethodInfo ensureHandCapacity = typeof(MTGAHandUiController).GetMethod(
                "EnsureHandCapacity", flags);
            Require(ensureHandCapacity != null,
                "The seat-relative hand visual creation path was not found.");
            ensureHandCapacity.Invoke(null, new object[]
            {
                handRootObject.GetComponent<RectTransform>(), handVisuals, 1, 2
            });
            Require(handVisuals.Count == 1
                    && handVisuals[0].Owner == 2
                    && handVisuals[0].name.StartsWith("PlayerHandCard_", StringComparison.Ordinal),
                "Player 2's local hand was created with opponent-facing names or ownership.");
        }
        finally
        {
            serviceInstanceField?.SetValue(null, existingService);
            OnlinePlayerContext.Reset();
            UnityEngine.Object.DestroyImmediate(localButtonObject);
            UnityEngine.Object.DestroyImmediate(opponentButtonObject);
            UnityEngine.Object.DestroyImmediate(handRootObject);
            UnityEngine.Object.DestroyImmediate(interactionObject);
            UnityEngine.Object.DestroyImmediate(rulesObject);
        }
    }

    private static void ValidateOnlineChoiceConfirmationProjection()
    {
        GameObject serverObject = new GameObject("Online Choice Confirmation Server");
        GameObject clientObject = new GameObject("Online Choice Confirmation Client");
        try
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            GameRules server = serverObject.AddComponent<GameRules>();
            typeof(GameRules).GetField("onlineAuthoritySession", flags)?.SetValue(server, true);
            typeof(GameRules).GetField("player1Life", flags)?.SetValue(server, 20);
            typeof(GameRules).GetField("player2Life", flags)?.SetValue(server, 20);
            GameChoiceRequest choice = server.BeginOptionChoice(
                GameChoiceKind.SelectOptions,
                1,
                "Choose only after the authoritative boundary is satisfied.",
                0,
                1,
                true,
                new[]
                {
                    new GameChoiceOption(1, GameChoiceZone.None, 1, -1, true, "Option")
                },
                _ => { });
            Require(choice != null && choice.CanConfirm,
                "The choice projection setup was not structurally confirmable.");
            typeof(GameRules).GetField("pendingGenericChoiceValidator", flags)?.SetValue(
                server,
                new Func<GameChoiceRequest, bool>(_ => false));
            Require(!server.CanConfirmActiveChoice(),
                "The authoritative choice validator did not block confirmation.");

            OnlinePlayerViewState view = server.CreateOnlinePlayerView(1);
            GameRules client = clientObject.AddComponent<GameRules>();
            Require(client.ApplyOnlinePlayerView(view, out string error), error);
            Require(!client.CanConfirmActiveChoice(),
                "The online player view lost the authoritative choice-confirmation boundary.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(serverObject);
            UnityEngine.Object.DestroyImmediate(clientObject);
        }
    }

    private static void ValidateOnlineLibrarySearchPresentation()
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        GameObject serverObject = new GameObject("Online Library Search Server");
        GameObject playerOneObject = new GameObject("Online Library Search Player 1");
        GameObject playerTwoObject = new GameObject("Online Library Search Player 2");
        GameObject playerOneInteractionObject = new GameObject("Online Library Search Player 1 Interaction");
        GameObject playerTwoInteractionObject = new GameObject("Online Library Search Player 2 Interaction");
        try
        {
            GameRules server = serverObject.AddComponent<GameRules>();
            typeof(GameRules).GetField("currentPlayer", flags)?.SetValue(server, 1);
            typeof(GameRules).GetField("onlineAuthoritySession", flags)?.SetValue(server, true);

            var source = new RuntimeCard(
                new CardDefinition { cardName = "P1 Fetch Land", cardTypes = CardType.Land },
                1,
                false);
            source.SetCurrentZone(GameZone.Battlefield);
            var basicLand = new RuntimeCard(
                new CardDefinition
                {
                    cardName = "P1 Searchable Basic",
                    cardTypes = CardType.Land,
                    supertypes = CardSupertype.Basic
                },
                1,
                false);
            basicLand.SetCurrentZone(GameZone.Library);
            var nonbasicLand = new RuntimeCard(
                new CardDefinition { cardName = "P1 Nonbasic Land", cardTypes = CardType.Land },
                1,
                false);
            nonbasicLand.SetCurrentZone(GameZone.Library);
            ((List<RuntimeCard>)typeof(GameRules).GetField("player1Battlefield", flags)?.GetValue(server))?.Add(source);
            ((List<RuntimeCard>)typeof(GameRules).GetField("player1Library", flags)?.GetValue(server))?.Add(basicLand);
            ((List<RuntimeCard>)typeof(GameRules).GetField("player1Library", flags)?.GetValue(server))?.Add(nonbasicLand);

            MethodInfo activateFetch = typeof(GameRules).GetMethod("ActivateFetchBasicLandAbility", flags);
            Require(activateFetch != null
                    && (bool)activateFetch.Invoke(server, new object[] { source }),
                "The online library-search presentation setup did not start a choice.");

            GameRules playerOne = playerOneObject.AddComponent<GameRules>();
            GameRules playerTwo = playerTwoObject.AddComponent<GameRules>();
            Require(playerOne.ApplyOnlinePlayerView(server.CreateOnlinePlayerView(1), out string playerOneError),
                playerOneError);
            Require(playerTwo.ApplyOnlinePlayerView(server.CreateOnlinePlayerView(2), out string playerTwoError),
                playerTwoError);

            MethodInfo syncBrowser = typeof(ArenaWhiteboxInteraction).GetMethod(
                "SyncLibrarySearchBrowserState", flags);
            MethodInfo buildBrowserCards = typeof(ArenaWhiteboxInteraction).GetMethod(
                "BuildBrowserSourceCards", flags);
            FieldInfo gameRulesField = typeof(ArenaWhiteboxInteraction).GetField("gameRules", flags);
            FieldInfo browserOpenField = typeof(ArenaWhiteboxInteraction).GetField("stackBrowserOpen", flags);
            FieldInfo browserCardsField = typeof(ArenaWhiteboxInteraction).GetField("stackBrowserSourceCards", flags);
            FieldInfo browserIndicesField = typeof(ArenaWhiteboxInteraction).GetField("stackBrowserDeckIndices", flags);
            Require(syncBrowser != null && buildBrowserCards != null && gameRulesField != null
                    && browserOpenField != null && browserCardsField != null && browserIndicesField != null,
                "The library-search browser validation seam was unavailable.");

            OnlinePlayerContext.SetLocalPlayer(2);
            ArenaWhiteboxInteraction playerTwoInteraction =
                playerTwoInteractionObject.AddComponent<ArenaWhiteboxInteraction>();
            gameRulesField.SetValue(playerTwoInteraction, playerTwo);
            syncBrowser.Invoke(playerTwoInteraction, null);
            Require(!(bool)browserOpenField.GetValue(playerTwoInteraction),
                "The non-choosing client opened the opponent's hidden library-search browser.");
            Require(!playerTwo.CanConfirmLibrarySearch(1),
                "The non-choosing client treated the opponent's redacted library search as confirmable.");

            OnlinePlayerContext.SetLocalPlayer(1);
            ArenaWhiteboxInteraction playerOneInteraction =
                playerOneInteractionObject.AddComponent<ArenaWhiteboxInteraction>();
            gameRulesField.SetValue(playerOneInteraction, playerOne);
            syncBrowser.Invoke(playerOneInteraction, null);
            Require((bool)browserOpenField.GetValue(playerOneInteraction),
                "The choosing client did not open its library-search browser.");
            buildBrowserCards.Invoke(playerOneInteraction, new object[] { null, 0, null });
            var browserCards = browserCardsField.GetValue(playerOneInteraction) as List<RuntimeCard>;
            var browserIndices = browserIndicesField.GetValue(playerOneInteraction) as List<int>;
            Require(browserCards != null && browserCards.Count == 1
                    && browserIndices != null && browserIndices.Count == 1
                    && playerOne.CanSelectLibrarySearchCard(1, browserIndices[0]),
                "The choosing client rendered illegal cards alongside the legal library-search choices.");
        }
        finally
        {
            OnlinePlayerContext.Reset();
            UnityEngine.Object.DestroyImmediate(playerTwoInteractionObject);
            UnityEngine.Object.DestroyImmediate(playerOneInteractionObject);
            UnityEngine.Object.DestroyImmediate(playerTwoObject);
            UnityEngine.Object.DestroyImmediate(playerOneObject);
            UnityEngine.Object.DestroyImmediate(serverObject);
        }
    }

    private static void ValidateOnlineBlockInteractionProjection()
    {
        GameObject serverObject = new GameObject("Online Block Interaction Server");
        GameObject defenderClientObject = new GameObject("Online Block Interaction Defender Client");
        GameObject attackerClientObject = new GameObject("Online Block Interaction Attacker Client");
        try
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            Type rulesType = typeof(GameRules);
            GameRules server = serverObject.AddComponent<GameRules>();
            rulesType.GetField("currentPlayer", flags)?.SetValue(server, 1);
            rulesType.GetField("currentPhase", flags)?.SetValue(server, TurnPhase.DeclareBlockers);
            rulesType.GetField("turnNumber", flags)?.SetValue(server, 6);
            rulesType.GetField("onlineAuthoritySession", flags)?.SetValue(server, true);
            rulesType.GetField("player1Life", flags)?.SetValue(server, 20);
            rulesType.GetField("player2Life", flags)?.SetValue(server, 20);

            var attacker = new RuntimeCard(
                new CardDefinition
                {
                    cardID = 92101,
                    setCode = "TST",
                    collectorNumberText = "block-interaction-attacker",
                    cardName = "Block Interaction Attacker",
                    cardTypes = CardType.Creature,
                    attack = 2,
                    defence = 2
                },
                1,
                initializeScript: false);
            attacker.ReassignObjectId(92110);
            attacker.SetCurrentZone(GameZone.Battlefield);
            attacker.IsAttacking = true;
            attacker.AttackTarget = 2;
            var blocker = new RuntimeCard(
                new CardDefinition
                {
                    cardID = 92102,
                    setCode = "TST",
                    collectorNumberText = "block-interaction-blocker",
                    cardName = "Block Interaction Blocker",
                    cardTypes = CardType.Creature,
                    attack = 1,
                    defence = 3
                },
                2,
                initializeScript: false);
            blocker.ReassignObjectId(92120);
            blocker.SetCurrentZone(GameZone.Battlefield);

            var attackers = (List<RuntimeCard>)rulesType.GetField(
                "player1Battlefield", flags)?.GetValue(server);
            var blockers = (List<RuntimeCard>)rulesType.GetField(
                "player2Battlefield", flags)?.GetValue(server);
            Require(attackers != null && blockers != null,
                "The authoritative blocker battlefields were unavailable.");
            attackers.Add(attacker);
            blockers.Add(blocker);

            RestorePriority(server, true, 1);
            Require(!server.CanDeclareBlocker(2, 0, attacker)
                    && !server.ApplyAction(new GameAction
                    {
                        type = GameActionType.DeclareBlocker,
                        playerId = 2,
                        zoneIndex = 0,
                        targetObjectId = attacker.ObjectId
                    }).accepted,
                "The defender declared blockers before receiving priority.");

            RestorePriority(server, true, 2);
            Require(server.CanDeclareBlocker(2, 0, attacker),
                "The authoritative defender could not block after receiving priority.");
            OnlinePlayerContext.SetLocalPlayer(2);
            GameRules defenderClient = defenderClientObject.AddComponent<GameRules>();
            Require(defenderClient.ApplyOnlinePlayerView(server.CreateOnlinePlayerView(2), out string error), error);
            RuntimeCard defenderViewAttacker = defenderClient.GetBattlefieldCard(1, 0);
            Require(defenderClient.CanDeclareBlocker(2, 0, defenderViewAttacker)
                    && defenderClient.CanDeclareBlockerAgainstAnyAttacker(2, 0)
                    && defenderClient.CanAnyHumanBlockerBlock(defenderViewAttacker),
                "The defender player view lost a legal block interaction.");

            GameRules attackerClient = attackerClientObject.AddComponent<GameRules>();
            Require(attackerClient.ApplyOnlinePlayerView(server.CreateOnlinePlayerView(1), out error), error);
            Require(!attackerClient.CanDeclareBlocker(2, 0, attackerClient.GetBattlefieldCard(1, 0)),
                "The attacker player view received the defender's private block permission.");

            Require(server.ApplyAction(new GameAction
                {
                    type = GameActionType.DeclareBlocker,
                    playerId = 2,
                    zoneIndex = 0,
                    targetObjectId = attacker.ObjectId
                }).accepted,
                "The authoritative defender could not submit a legal block.");
            Require(defenderClient.ApplyOnlinePlayerView(server.CreateOnlinePlayerView(2), out error), error);
            Require(defenderClient.CanCancelBlocker(2, 0)
                    && defenderClient.GetBattlefieldCard(2, 0).BlockingTarget?.ObjectId == attacker.ObjectId,
                "The defender player view lost the declared block or its cancel interaction.");
        }
        finally
        {
            OnlinePlayerContext.Reset();
            UnityEngine.Object.DestroyImmediate(serverObject);
            UnityEngine.Object.DestroyImmediate(defenderClientObject);
            UnityEngine.Object.DestroyImmediate(attackerClientObject);
        }
    }

    private static void ValidateContentManifest()
    {
        var card = new CardDefinition
        {
            cardID = 7,
            setCode = "TST",
            collectorNumberText = "1",
            cardName = "Manifest Card",
            manaCost = "{1}",
            description = "Test",
            luaScript = "return 1"
        };
        card.Normalize();
        OnlineContentManifest first = OnlineContentManifest.Build(new[] { card });
        OnlineContentManifest identical = OnlineContentManifest.Build(new[] { card });
        Require(first.IsCompatibleWith(identical, out string error), error);

        card.luaScript = "return 2";
        OnlineContentManifest changedScript = OnlineContentManifest.Build(new[] { card });
        Require(!first.IsCompatibleWith(changedScript, out error)
            && error.Contains("Lua"), "A changed Lua script was not identified by the content handshake.");

        card.luaScript = "return 1";
        card.description = "Changed rules text";
        OnlineContentManifest changedCardData = OnlineContentManifest.Build(new[] { card });
        Require(!first.IsCompatibleWith(changedCardData, out error)
            && error.Contains("卡牌数据库"), "Changed card data was not identified by the content handshake.");

        changedScript = OnlineContentManifest.Build(new[] { card });
        changedScript.protocolVersion++;
        Require(!first.IsCompatibleWith(changedScript, out error),
            "A changed protocol version passed the content handshake.");
    }

    private static void ValidateMessageGuard()
    {
        var manifest = new OnlineContentManifest
        {
            gameVersion = "test",
            protocolVersion = OnlineProtocol.Version,
            databaseSchemaVersion = CardDatabaseRepository.SupportedSchemaVersion,
            contentHash = "hash"
        };
        Require(OnlineMessageGuard.ValidateHello(new OnlineHelloMessage
        {
            displayName = "Alice",
            roomId = "room-1",
            manifest = manifest
        }, out string error), error);
        Require(!OnlineMessageGuard.ValidateHello(new OnlineHelloMessage
        {
            displayName = "Alice",
            roomId = "../bad",
            manifest = manifest
        }, out error), "A malformed room ID passed validation.");
        var oversizedRoomConfig = new OnlineRoomConfig();
        for (int i = 0; i < 65; i++) oversizedRoomConfig.allowedSetCodes.Add("T" + i);
        Require(!OnlineMessageGuard.ValidateHello(new OnlineHelloMessage
        {
            displayName = "Alice",
            roomId = "room-1",
            manifest = manifest,
            requestedRoomConfig = oversizedRoomConfig
        }, out error), "An oversized room set list passed validation.");

        var invalidAction = new GameAction { type = GameActionType.PlayHandCard };
        for (int i = 0; i < 65; i++) invalidAction.optionIds.Add(i);
        Require(!OnlineMessageGuard.ValidateActionShape(invalidAction, out error),
            "An oversized action option list passed validation.");
        Require(!OnlineMessageGuard.ValidateActionShape(new GameAction
        {
            type = GameActionType.ActivateAbility,
            sourceObjectId = long.MaxValue
        }, out error), "An unbounded object ID passed validation.");

        var guard = new OnlineMessageGuard();
        for (int i = 0; i < OnlineMessageGuard.MaxMessagesPerWindow; i++)
            Require(guard.TryAccept(9, 128, 1d, out error), error);
        Require(!guard.TryAccept(9, 128, 1d, out error),
            "The server rate limiter accepted too many messages.");
        guard.Touch(10, 0d);
        Require(guard.IsIdle(10, OnlineMessageGuard.IdleTimeoutSeconds + 0.01d),
            "An idle connection was not detected.");
        Require(!guard.TryAccept(11, OnlineMessageGuard.MaxEnvelopeCharacters + 1, 0d, out error),
            "An oversized envelope passed validation.");
    }

    private static void ValidateRoomIsolation()
    {
        var registry = new OnlineRoomRegistry();
        try
        {
            Require(registry.TryGetOrCreate("alpha", out OnlineRoomState alpha, out string error), error);
            Require(registry.TryGetOrCreate("beta", out OnlineRoomState beta, out error), error);
            Require(!ReferenceEquals(alpha, beta) && registry.Count == 2,
                "Distinct room IDs did not create isolated rooms.");
            alpha.config.startingLife = 30;
            alpha.config.firstPlayerSeat = 2;
            beta.config.firstPlayerSeat = 1;
            Require(beta.config.startingLife == 20, "Room configuration leaked between rooms.");

            DeckDefinition deck = CreateValidationDeck();
            alpha.match = new OnlineMatchSession("alpha-match", deck, deck, 101, alpha.config);
            beta.match = new OnlineMatchSession("beta-match", deck, deck, 202, beta.config);
            OnlinePlayerViewState configuredView = alpha.match.GetView(1);
            Require(configuredView.player1Life == 30 && configuredView.currentPlayer == 2,
                $"Room config was not applied (life={configuredView.player1Life}, first={configuredView.currentPlayer}).");
            GameActionResult applied = alpha.match.ApplyAction(new GameAction
            {
                type = GameActionType.SetFullControl,
                playerId = 2,
                value = 1,
                expectedStateSequence = 0
            });
            Require(applied.accepted, applied.error);
            Require(alpha.match.AppliedSequence == 1 && beta.match.AppliedSequence == 0,
                "An action in one room changed another room's match sequence.");
            Require(alpha.match.GetView(1).viewHash != beta.match.GetView(1).viewHash,
                "Matches with different seeds did not retain independent state.");
        }
        finally
        {
            registry.Dispose();
        }
    }

    private static void ValidateAuthoritativeLoopback()
    {
        OnlineMatchSession session = null;
        GameObject clientObject = new GameObject("Online Validation Player View Client");
        try
        {
            DeckDefinition deck = CreateValidationDeck();
            session = new OnlineMatchSession("loopback", deck, deck, 739391);
            OnlinePlayerViewState openingPlayer1 = session.GetView(1);
            OnlinePlayerViewState openingPlayer2 = session.GetView(2);
            Require(openingPlayer1.openingMulligan && openingPlayer2.openingMulligan
                && openingPlayer1.startingPlayer == 1 && openingPlayer2.startingPlayer == 1,
                "The authoritative opening state did not preserve the starting player.");
            Require(openingPlayer1.activeChoice?.kind == GameChoiceKind.MulliganDecision
                && openingPlayer1.activeChoice.choosingPlayerId == 1
                && openingPlayer1.activeChoice.options.Count == 2,
                "The starting player did not receive the opening mulligan decision.");
            Require(openingPlayer2.activeChoice?.kind == GameChoiceKind.MulliganDecision
                && openingPlayer2.activeChoice.choosingPlayerId == 1
                && openingPlayer2.activeChoice.options.Count == 0,
                "A waiting player received private mulligan options.");
            Require(!OnlinePlayerViewSecurity.ContainsHiddenIdentity(openingPlayer1, 2)
                && !OnlinePlayerViewSecurity.ContainsHiddenIdentity(openingPlayer2, 1),
                "Opening mulligan projection leaked a hidden card identity.");
            for (int i = 0; i < 3; i++)
            {
                OnlinePlayerViewState before = session.GetView(1);
                GameActionResult authoritative = session.ApplyAction(new GameAction
                {
                    type = GameActionType.SetFullControl,
                    playerId = 1,
                    value = i % 2,
                    expectedStateSequence = before.appliedSequence
                });
                Require(authoritative.accepted, "The loopback server rejected a legal action.");
            }
            OnlinePlayerViewState finalView = session.GetView(1);
            GameActionResult stale = session.ApplyAction(new GameAction
            {
                type = GameActionType.SetFullControl,
                playerId = 1,
                value = 1,
                expectedStateSequence = 0
            });
            Require(!stale.accepted && session.GetView(1).viewHash == finalView.viewHash,
                "A stale expected-state sequence changed authoritative state.");

            GameRules client = clientObject.AddComponent<GameRules>();
            Require(client.ApplyOnlinePlayerView(finalView, out string error), error);
            Require(client.ComputeDeterministicStateHash() == finalView.viewHash,
                "The client did not reach the authoritative player-view hash.");

            OnlinePlayerViewState tampered = session.GetView(1);
            tampered.player1Life--;
            Require(!client.ApplyOnlinePlayerView(tampered, out error),
                "A tampered authoritative player view passed hash validation.");
        }
        finally
        {
            session?.Dispose();
            UnityEngine.Object.DestroyImmediate(clientObject);
        }
    }

    private static DeckDefinition CreateValidationDeck()
    {
        var deck = new DeckDefinition { name = "Online Validation Deck" };
        System.Collections.Generic.IReadOnlyList<CardDefinition> cards = CardDatabaseRepository.LoadAll();
        CardDefinition card = null;
        for (int i = 0; i < cards.Count && card == null; i++)
        {
            if (cards[i] != null && !string.IsNullOrEmpty(cards[i].PrintingKey)) card = cards[i];
        }
        Require(card != null, "The validation database contains no printable card.");
        deck.cards.Add(new DeckEntry
        {
            setCode = card.setCode,
            collectorNumber = card.CollectorNumber,
            cardId = card.cardID,
            count = 20
        });
        return deck;
    }

    private static void InitializeRules(GameRules rules, int seed)
    {
        MatchStartConfig.SetDeterministicSeed(seed);
        MethodInfo initialize = typeof(GameRules).GetMethod("InitializeGame", BindingFlags.Instance | BindingFlags.NonPublic);
        Require(initialize != null, "GameRules.InitializeGame was not found.");
        initialize.Invoke(rules, null);
    }

    private static void DeleteValidationProfile(string profileId)
    {
        DeleteValidationPlayerPrefs(profileId);
        string sharedPath = Path.Combine(
            PrintingKeyUtility.ApplicationRoot,
            "Library",
            "MTGB",
            "OnlineProfiles",
            OnlineClientProfileStore.NormalizeId(profileId) + ".json");
        if (File.Exists(sharedPath)) File.Delete(sharedPath);
    }

    private static void DeleteValidationPlayerPrefs(string profileId)
    {
        string key = "MTGB.Online.Profile." + OnlineClientProfileStore.NormalizeId(profileId) + ".";
        PlayerPrefs.DeleteKey(key + "Address");
        PlayerPrefs.DeleteKey(key + "Port");
        PlayerPrefs.DeleteKey(key + "DisplayName");
        PlayerPrefs.DeleteKey(key + "RoomId");
        PlayerPrefs.DeleteKey(key + "ReconnectToken");
        PlayerPrefs.DeleteKey(key + "ReconnectToken.main");
        PlayerPrefs.DeleteKey(key + "ReconnectToken.alpha");
        PlayerPrefs.DeleteKey(key + "ReconnectToken.beta");
        PlayerPrefs.DeleteKey(key + "Deck");
    }

    private static PriorityState GetPriorityState(GameRules rules)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var state = typeof(GameRules).GetField("priorityState", flags)?.GetValue(rules) as PriorityState;
        Require(state != null, "GameRules priority state module was not found.");
        return state;
    }

    private static void RestorePriority(GameRules rules, bool isOpen, int player)
    {
        GetPriorityState(rules).Restore(isOpen, player);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
#endif
