using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 定稿战场 UI 场景的构建与验收工具。
/// - Build Integrated Battlefield Scene：新建空场景 → BattlefieldSceneFactory 组装 → 保存。
/// - Capture Integrated Battlefield Screenshot：进 Play、通过真实 GameAction 摆出中盘局面
///   （双方逐回合下地并横置部分地，全程走 ApplyAction，不改规则引擎），双相机合成 1920×1080 截图。
/// - RunBatch：命令行无人值守入口（-batchmode -executeMethod IntegratedBattlefieldSceneBuilder.RunBatch），
///   建场景 → 进 Play → 摆场 → 截图 → 退出编辑器。
/// </summary>
[InitializeOnLoad]
public static class IntegratedBattlefieldSceneBuilder
{
    private const string ScenePath = "Assets/Scenes/IntegratedBattlefield.unity";
    private const string CapturePath = "Captures/IntegratedBattlefield_08.png";
    private const int CaptureWidth = 1920;
    private const int CaptureHeight = 1080;

    [MenuItem("Tools/MTGB/Build Integrated Battlefield Scene")]
    public static void BuildScene()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        BattlefieldSceneFactory.Build(replaceExisting: true);
        EnsureSceneSaved();
        Debug.Log($"[MTGB] Integrated battlefield scene built → {ScenePath}");
    }

    [MenuItem("Tools/MTGB/Open Integrated Battlefield Scene")]
    public static void OpenScene()
    {
        if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }
    }

    [MenuItem("Tools/MTGB/Capture Integrated Battlefield Screenshot")]
    public static void CaptureScreenshot()
    {
        if (Application.isPlaying)
        {
            Debug.LogWarning("[MTGB] Already in play mode; exit play mode first.");
            return;
        }

        if (!File.Exists(ScenePath))
        {
            BuildScene();
        }
        else if (!Application.isBatchMode && EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        StartRunner(batch: false);
    }

    /// <summary>命令行入口：Unity -batchmode -projectPath … -executeMethod IntegratedBattlefieldSceneBuilder.RunBatch</summary>
    public static void RunBatch()
    {
        Debug.Log("[MTGB] RunBatch: building scene");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        BattlefieldSceneFactory.Build(replaceExisting: true);
        EnsureSceneSaved();
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        Debug.Log("[MTGB] RunBatch: entering play for staged capture");
        StartRunner(batch: true);
    }

    /// <summary>验收截图用演示牌组：海岛为主（对应预览图全海岛摆场），仅截图会话设置。</summary>
    internal static void ApplyDemoDecks()
    {
        var deck = new DeckDefinition
        {
            name = "IntegratedArena Demo",
            cards = new List<DeckEntry>
            {
                new DeckEntry { setCode = "MTGB", collectorNumber = "1", cardId = 1001, count = 16 },
                new DeckEntry { setCode = "MTGB", collectorNumber = "8", cardId = 1002, count = 2 },
                new DeckEntry { setCode = "MTGB", collectorNumber = "9", cardId = 1004, count = 1 },
                new DeckEntry { setCode = "MTGB", collectorNumber = "7", cardId = 1012, count = 1 }
            }
        };
        MatchStartConfig.SetDecks(deck, deck);
    }

    private static void StartRunner(bool batch)
    {
        ApplyDemoDecks();
        // 进 Play 会触发域重载，update 订阅会丢失：用 SessionState 跨越重载，
        // 由 [InitializeOnLoad] 静态构造在 EnteredPlayMode 后重新拉起 runner。
        SessionState.SetBool(PendingKey, true);
        SessionState.SetBool(PendingBatchKey, batch);
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        if (!EditorApplication.isPlaying) EditorApplication.isPlaying = true;
    }

    private const string PendingKey = "MTGB_IntegratedCapture_Pending";
    private const string PendingBatchKey = "MTGB_IntegratedCapture_Batch";

    static IntegratedBattlefieldSceneBuilder()
    {
        if (SessionState.GetBool(PendingKey, false))
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredPlayMode) return;
        if (!SessionState.GetBool(PendingKey, false)) return;
        SessionState.SetBool(PendingKey, false);
        bool batch = SessionState.GetBool(PendingBatchKey, false);
        var runner = new CaptureRunner(batch);
        EditorApplication.update += runner.Tick;
    }

    private static void EnsureSceneSaved()
    {
        var scene = EditorSceneManager.GetActiveScene();
        EditorSceneManager.SaveScene(scene, ScenePath);
        EditorBuildSettingsScene[] settings = EditorBuildSettings.scenes;
        foreach (EditorBuildSettingsScene entry in settings)
        {
            if (entry.path == ScenePath) return;
        }

        var list = new List<EditorBuildSettingsScene>(settings) { new EditorBuildSettingsScene(ScenePath, true) };
        EditorBuildSettings.scenes = list.ToArray();
    }

    /// <summary>进 Play → 等待初始化 → 确认调度 → 双方下地若干回合 → 合成截图 → 退 Play。</summary>
    private sealed class CaptureRunner
    {
        private enum Stage
        {
            EnteringPlay,
            WaitingRules,
            Staging,
            Capturing,
            Done
        }

        private readonly bool batch;
        private Stage stage = Stage.EnteringPlay;
        private double nextActionTime;
        private double startedAt;
        private readonly HashSet<int> playedTurnKeys = new HashSet<int>();
        private bool decksReloaded;
        private bool decksAppliedButWaiting;
        private bool mulliganToggled;
        private double stuckSince = -1.0;
        private const double TimeoutSeconds = 240.0;
        private const int TargetLandsPerSide = 8;

        public CaptureRunner(bool batchMode)
        {
            batch = batchMode;
            startedAt = EditorApplication.timeSinceStartup;
        }

        public void Tick()
        {
            if (stage != Stage.Done && EditorApplication.timeSinceStartup - startedAt > TimeoutSeconds)
            {
                Debug.LogWarning($"[MTGB] Capture timeout at stage={stage}; forcing capture");
                stage = Stage.Capturing;
                nextActionTime = 0;
            }

            switch (stage)
            {
                case Stage.EnteringPlay:
                    if (!EditorApplication.isPlaying)
                    {
                        // 域重载后 runner 由 OnPlayModeStateChanged 重新拉起，此分支兜底
                        if (!EditorApplication.isPlayingOrWillChangePlaymode)
                        {
                            EditorApplication.update -= Tick;
                            OnPlayModeStateChanged(PlayModeStateChange.EnteredPlayMode);
                        }
                        return;
                    }
                    stage = Stage.WaitingRules;
                    nextActionTime = EditorApplication.timeSinceStartup + 0.5;
                    Debug.Log("[MTGB] play mode entered; waiting for rules init");
                    break;

                case Stage.WaitingRules:
                    if (EditorApplication.timeSinceStartup < nextActionTime) return;
                    // 进 Play 的域重载会清掉静态 MatchStartConfig：补设演示牌组并重载场景，
                    // 让 GameRules.Start 以演示牌组重新初始化（仅执行一次）
                    if (!decksReloaded)
                    {
                        decksReloaded = true;
                        ApplyDemoDecks();
                        var activeScene = EditorSceneManager.GetActiveScene();
                        if (!string.IsNullOrEmpty(activeScene.path))
                        {
                            // Play 模式下不能用 EditorSceneManager.OpenScene，改运行时加载
                            UnityEngine.SceneManagement.SceneManager.LoadScene(activeScene.path);
                            Debug.Log("[MTGB] demo decks applied; scene reloaded in play mode");
                        }
                        decksAppliedButWaiting = true;
                        return;
                    }
                    if (decksAppliedButWaiting)
                    {
                        // 场景重载后立即给 P2 全控（其先手时的调度/落牌才能被确认）
                        decksAppliedButWaiting = false;
                        GameRules fresh = Object.FindFirstObjectByType<GameRules>();
                        if (fresh != null)
                        {
                            fresh.ApplyAction(new GameAction { type = GameActionType.SetFullControl, playerId = 2, value = 1 });
                        }
                        nextActionTime = EditorApplication.timeSinceStartup + 0.3;
                        return;
                    }

                    GameRules rules = Object.FindFirstObjectByType<GameRules>();
                    if (rules == null || !rules.IsInitialized()) return;

                    if (rules.IsOpeningMulligan())
                    {
                        GameChoiceRequest choice = rules.GetActiveChoice();
                        if (choice == null) return;
                        if (!mulliganToggled)
                        {
                            // MulliganDecision 需先选「保留这手牌」选项（optionId=1）再确认
                            mulliganToggled = true;
                            rules.ApplyAction(new GameAction
                            {
                                type = GameActionType.ToggleChoiceOption,
                                playerId = choice.choosingPlayerId,
                                choiceRequestId = choice.requestId,
                                optionId = 1
                            });
                            return;
                        }
                        if (rules.CanConfirmActiveChoice())
                        {
                            rules.ApplyAction(new GameAction
                            {
                                type = GameActionType.ConfirmChoice,
                                playerId = choice.choosingPlayerId,
                                choiceRequestId = choice.requestId
                            });
                            Debug.Log($"[MTGB] mulligan confirmed for P{choice.choosingPlayerId}");
                            mulliganToggled = false;
                        }
                        return;
                    }

                    stage = Stage.Staging;
                    nextActionTime = EditorApplication.timeSinceStartup + 0.3;
                    // 本地模式引擎固定把优先权交给 P1；给 P2 开全控后其回合才会等待落牌操作
                    rules.ApplyAction(new GameAction { type = GameActionType.SetFullControl, playerId = 2, value = 1 });
                    Debug.Log($"[MTGB] rules ready: turn={rules.GetTurnNumber()}, P1 deck={rules.GetDeckCount(1)}, P2 deck={rules.GetDeckCount(2)}");
                    break;

                case Stage.Staging:
                    if (EditorApplication.timeSinceStartup < nextActionTime) return;
                    nextActionTime = EditorApplication.timeSinceStartup + 0.15;
                    if (!AdvanceStaging()) return;
                    stage = Stage.Capturing;
                    nextActionTime = EditorApplication.timeSinceStartup + 1.0;
                    break;

                case Stage.Capturing:
                    if (captureRt == null)
                    {
                        SetupRenderTargets();
                        nextActionTime = EditorApplication.timeSinceStartup + 0.3;
                        return;
                    }
                    if (EditorApplication.timeSinceStartup < nextActionTime) return;
                    ReadbackCapture();
                    stage = Stage.Done;
                    nextActionTime = EditorApplication.timeSinceStartup + 0.8;
                    break;

                case Stage.Done:
                    EditorApplication.update -= Tick;
                    if (batch)
                    {
                        Debug.Log($"[MTGB] Battlefield capture saved → {CapturePath}; exiting");
                        EditorApplication.Exit(0);
                    }
                    else
                    {
                        EditorApplication.isPlaying = false;
                        Debug.Log($"[MTGB] Battlefield capture saved → {CapturePath}");
                    }
                    break;
            }
        }

        /// <summary>返回 true 表示摆场完成：双方各落 TargetLandsPerSide 张地，随后横置几张。</summary>
        private bool AdvanceStaging()
        {
            GameRules rules = Object.FindFirstObjectByType<GameRules>();
            if (rules == null || rules.IsGameOver()) return true;

            if (rules.HasPendingStack() || rules.HasActiveChoice() || rules.IsOpeningMulligan())
            {
                if (rules.HasActiveChoice() && rules.CanConfirmActiveChoice())
                {
                    GameChoiceRequest choice = rules.GetActiveChoice();
                    rules.ApplyAction(new GameAction
                    {
                        type = GameActionType.ConfirmChoice,
                        playerId = choice.choosingPlayerId,
                        choiceRequestId = choice.requestId
                    });
                }
                return false;
            }

            int current = rules.GetCurrentPlayer();
            int key = rules.GetTurnNumber() * 10 + current;

            if (!SideNeedsLands(rules, current))
            {
                playedTurnKeys.Add(key);
            }

            if (!playedTurnKeys.Contains(key))
            {
                if (TryPlayLand(rules, current))
                {
                    playedTurnKeys.Add(key);
                    Debug.Log($"[MTGB] land played: P{current}, turn {rules.GetTurnNumber()}");
                    return false;
                }

                // 该玩家本回合还没落下地：若是主阶段且因优先权/阶段时序失败则等待重试；
                // 停滞过久才让过（防死循环）
                if (stuckSince < 0) stuckSince = EditorApplication.timeSinceStartup;
                if (EditorApplication.timeSinceStartup - stuckSince > 12.0)
                {
                    stuckSince = -1.0;
                    playedTurnKeys.Add(key);
                    GameActionType primary = rules.GetPrimaryAdvanceActionType(current);
                    rules.ApplyAction(new GameAction { type = GameActionType.EndTurn, playerId = current });
                    Debug.Log($"[MTGB] force end turn P{current}");
                }
                return false;
            }

            playedTurnKeys.Add(key);
            stuckSince = -1.0;
            if (!SideNeedsLands(1) && !SideNeedsLands(2))
            {
                Debug.Log("[MTGB] staging complete; tapping lands");
                TapSomeLands(rules);
                rules.ApplyAction(new GameAction { type = GameActionType.SetFullControl, playerId = 2, value = 0 });
                return true;
            }

            GameActionResult endResult = rules.ApplyAction(new GameAction { type = GameActionType.EndTurn, playerId = current });
            Debug.Log($"[MTGB] end turn P{current}: accepted={endResult?.accepted}, turn={rules.GetTurnNumber()}, now P{rules.GetCurrentPlayer()} {rules.GetCurrentPhaseLabel()}");
            return false;
        }

        private bool SideNeedsLands(GameRules rules, int player)
        {
            return LandsOnBoard(rules, player) < TargetLandsPerSide;
        }

        private bool SideNeedsLands(int player)
        {
            GameRules rules = Object.FindFirstObjectByType<GameRules>();
            return rules != null && LandsOnBoard(rules, player) < TargetLandsPerSide;
        }

        private static int LandsOnBoard(GameRules rules, int player)
        {
            int count = 0;
            IReadOnlyList<RuntimeCard> cards = rules.GetBattlefieldCards(player);
            for (int i = 0; i < cards.Count; i++)
            {
                if (cards[i] != null && cards[i].Data.cardTypes.Has(CardType.Land)) count++;
            }
            return count;
        }


        private static bool TryPlayLand(GameRules rules, int player)
        {
            IReadOnlyList<RuntimeCard> hand = rules.GetHandCards(player);
            string lastIssue = "no land in hand";
            for (int i = 0; i < hand.Count; i++)
            {
                if (hand[i] == null || !hand[i].Data.cardTypes.Has(CardType.Land)) continue;
                if (!rules.CanPlayCard(player, i))
                {
                    rules.TryGetCanPlayCardReason(player, i, out string reason);
                    lastIssue = $"{hand[i].Data.cardName}: {reason}";
                    continue;
                }
                rules.ApplyAction(new GameAction
                {
                    type = GameActionType.PlayHandCard,
                    playerId = player,
                    zoneIndex = i
                });
                return true;
            }
            Debug.Log($"[MTGB] play land blocked for P{player}: {lastIssue}");
            return false;
        }

        private static void TapSomeLands(GameRules rules)
        {
            IReadOnlyList<RuntimeCard> battlefield = rules.GetBattlefieldCards(OnlinePlayerContext.LocalPlayerId);
            int tapped = 0;
            for (int i = 0; i < battlefield.Count && tapped < 3; i++)
            {
                RuntimeCard card = battlefield[i];
                if (card == null || !card.Data.cardTypes.Has(CardType.Land) || card.IsTapped) continue;
                if (!rules.CanActivateManaAbility(card) && !rules.CanActivateAnyAbility(card)) continue;
                rules.ApplyAction(new GameAction
                {
                    type = GameActionType.ActivateAbility,
                    playerId = card.OwnerPlayerID,
                    sourceObjectId = card.ObjectId,
                    value = 1
                });
                tapped++;
            }
        }

        private static RenderTexture captureRt;
        private static Camera captureWorldCamera;
        private static Canvas captureCanvas;
        private static RenderMode capturePreviousMode;
        private static Camera capturePreviousWorldCamera;
        private static bool readbackPending;
        private static bool readbackDone;

        /// <summary>
        /// 双相机合成 1920×1080 截图（URP 下不能用 Camera.Render 手动渲，需要管线驱动）：
        /// 世界相机与临时 UI 相机（Canvas 切 ScreenSpaceCamera）都挂同一 RenderTexture，
        /// 管线渲染数帧后 ReadPixels 落盘。不依赖 Game view 尺寸。
        /// </summary>
        /// <summary>反射设置 Game view 自定义分辨率（编辑器内部 API，失败则保持当前尺寸）。</summary>
        private static void TrySetGameViewSize(int width, int height)
        {
            try
            {
                var assembly = typeof(Editor).Assembly;
                var gameViewSizesType = assembly.GetType("UnityEditor.GameViewSizes");
                var instance = gameViewSizesType.GetProperty("instance").GetValue(null);
                var currentType = gameViewSizesType.GetProperty("currentType").GetValue(instance);
                var group = gameViewSizesType.GetMethod("GetGroup").Invoke(instance, new[] { currentType });
                var customSizes = group.GetType().GetProperty("customSizes").GetValue(group);
                var sizeType = assembly.GetType("UnityEditor.GameViewSize");
                var sizeTypeType = assembly.GetType("UnityEditor.GameViewSizeType");
                var existing = (System.Collections.IList)customSizes;
                int index = -1;
                for (int i = 0; i < existing.Count; i++)
                {
                    var entry = existing[i];
                    if ((int)entry.GetType().GetProperty("width").GetValue(entry) == width
                        && (int)entry.GetType().GetProperty("height").GetValue(entry) == height)
                    {
                        index = i;
                        break;
                    }
                }
                if (index < 0)
                {
                    var newSize = System.Activator.CreateInstance(sizeType,
                        System.Enum.Parse(sizeTypeType, "AspectRatio"), width, height, "MTGB Capture 1080p");
                    customSizes.GetType().GetMethod("Add").Invoke(customSizes, new[] { newSize });
                    index = existing.Count - 1;
                }

                var gameViewType = assembly.GetType("UnityEditor.GameView");
                var gameView = EditorWindow.GetWindow(gameViewType);
                var sizeCountField = gameViewType.GetField("m_sizeCount", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    ?? gameViewType.GetField("selectedSizeIndex", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                var selectedSize = gameViewType.GetProperty("selectedSize", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                if (selectedSize != null)
                {
                    var enumValue = gameViewType.GetProperty("selectedSize").GetIndexParameters();
                    selectedSize.SetValue(gameView, index + (existing.Count > 0 ? 1 : 0), null);
                }
                gameView.Repaint();
                Debug.Log($"[MTGB] game view size set to {width}x{height}");
            }
            catch (System.Exception exception)
            {
                Debug.LogWarning($"[MTGB] Could not set game view size: {exception.Message}");
            }
        }

        private static void SetupRenderTargets()
        {
            Debug.Log("[MTGB] capturing composite screenshot (setup)");
            TrySetGameViewSize(CaptureWidth, CaptureHeight);
            captureWorldCamera = Camera.main;
            captureCanvas = Object.FindFirstObjectByType<Canvas>();
            if (captureWorldCamera == null || captureCanvas == null)
            {
                Debug.LogError("[MTGB] Capture failed: camera/canvas not found");
                captureRt = new RenderTexture(4, 4, 0); // 触发 readback 失败路径
                captureRt.name = "CaptureFailed";
                return;
            }

            GameObject hudCameraObject = new GameObject("CaptureHudCamera");
            Camera hudCamera = hudCameraObject.AddComponent<Camera>();
            hudCamera.orthographic = true;
            hudCamera.clearFlags = CameraClearFlags.Depth;
            hudCamera.cullingMask = 1 << LayerMask.NameToLayer("UI");
            hudCamera.depth = 10f;

            capturePreviousMode = captureCanvas.renderMode;
            capturePreviousWorldCamera = captureCanvas.worldCamera;
            captureCanvas.renderMode = RenderMode.ScreenSpaceCamera;
            captureCanvas.worldCamera = hudCamera;
            captureCanvas.planeDistance = 1f;

            captureRt = new RenderTexture(CaptureWidth, CaptureHeight, 24, RenderTextureFormat.ARGB32)
            {
                name = "CaptureRT"
            };
            captureWorldCamera.targetTexture = captureRt;
            hudCamera.targetTexture = captureRt;

            // URP 在帧末会丢弃 render target 内容，必须在 endFrameRendering 里同步回读
            RenderPipelineManager.endFrameRendering -= OnEndFrameRendering;
            RenderPipelineManager.endFrameRendering += OnEndFrameRendering;
            readbackPending = true;
            readbackDone = false;
        }

        private static void OnEndFrameRendering(ScriptableRenderContext context, Camera[] cameras)
        {
            if (!readbackPending || readbackDone || captureRt == null) return;
            readbackPending = false;
            readbackDone = true;
            RenderPipelineManager.endFrameRendering -= OnEndFrameRendering;

            var previousActive = RenderTexture.active;
            RenderTexture.active = captureRt;
            var texture = new Texture2D(CaptureWidth, CaptureHeight, TextureFormat.RGBA32, false);
            texture.ReadPixels(new Rect(0f, 0f, CaptureWidth, CaptureHeight), 0, 0);
            texture.Apply();
            RenderTexture.active = previousActive;
            byte[] png = texture.EncodeToPNG();
            string path = Path.GetFullPath(CapturePath);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, png);
            Object.Destroy(texture);
            Debug.Log($"[MTGB] composite capture bytes: {png.Length}");
        }

        private static void ReadbackCapture()
        {
            Debug.Log("[MTGB] capturing composite screenshot (readback)");
            try
            {
                // 复合截图已在 OnEndFrameRendering 落盘；此处仅兜底存一张 Game view 截图
                ScreenCapture.CaptureScreenshot(CapturePath.Replace(".png", "_gameview.png"));
                if (System.IO.File.Exists(Path.GetFullPath(CapturePath)))
                {
                    Debug.Log($"[MTGB] capture written: {Path.GetFullPath(CapturePath)}");
                }
                else
                {
                    Debug.LogWarning("[MTGB] composite capture missing; check endFrameRendering readback");
                }
            }
            finally
            {
                if (captureWorldCamera != null) captureWorldCamera.targetTexture = null;
                captureCanvas.renderMode = capturePreviousMode;
                captureCanvas.worldCamera = capturePreviousWorldCamera;
                GameObject hudCameraObject = GameObject.Find("CaptureHudCamera");
                if (hudCameraObject != null) Object.Destroy(hudCameraObject);
                captureRt.Release();
                Object.Destroy(captureRt);
                captureRt = null;
            }
        }
    }
}
