using System.Reflection;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>
/// 定稿战场 UI 场景工厂（曜石竞技场 · 一体环境版）。
/// 结构：透视世界相机（约束求解）+ 环境层（可换肤）+ 战场排 + Screen Space HUD
/// （检视面板 / 身份牌 / 堆叠列 / 区域堆 / 右下角操作簇 / 双手牌）。
/// 规则引擎零改动：Systems 只挂 GameRules/CardManager/CardScriptRuntimeManager，
/// UI 只读 getter、只发 GameAction（spec 第 11 节）。
/// 编辑器与运行时均可调用（沿用 ArenaPrototypeSceneFactory 模式）。
/// </summary>
public static class BattlefieldSceneFactory
{
    public const string ArenaRootName = "IntegratedArena";

    public static void Build(bool replaceExisting)
    {
        if (replaceExisting) ClearGenerated();
        else if (GameObject.Find(ArenaRootName) != null) return;

        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.30f, 0.34f, 0.42f);

        // ① 相机：由桌沿/行锚点/0.875 行宽比约束求解
        BattlefieldCameraRig.Solution rig = BattlefieldCameraRig.Solve();
        Camera camera = CreateCamera(rig);

        GameObject root = new GameObject(ArenaRootName);

        // ① 环境层（含桌面地板世界侧 + 桌沿/压暗/暗角屏幕侧，可整体换肤）
        EnvironmentSkinController environment = root.AddComponent<EnvironmentSkinController>();

        // ② 战场排
        BattlefieldBoardController board = root.AddComponent<BattlefieldBoardController>();

        // 规则系统（原型假牌组，与现 Arena 场景一致）
        GameObject luaSystem = new GameObject("CardScriptRuntimeManager");
        luaSystem.SetActive(false);
        luaSystem.AddComponent<CardScriptRuntimeManager>();

        GameObject systems = new GameObject("Systems");
        systems.SetActive(false);
        GameRules rules = systems.AddComponent<GameRules>();
        // 演示场景：对手开局带一张地与一只生物（沿用原型种子，仅影响本场景展示）
        Assign(rules, "seedPrototypeOpponentBattlefield", true);
        systems.AddComponent<CardManager>();

        // ③–⑦ HUD（画布子物体按自下而上顺序创建，手牌最上）
        Canvas canvas = CreateCanvas();
        CreateEventSystem();

        environment.Initialize(camera, canvas.transform);
        board.Initialize(rules, camera, rig, null);

        InspectPanelController inspect = canvas.gameObject.AddComponent<InspectPanelController>();
        inspect.Initialize(canvas.transform);

        StackColumnController stack = canvas.gameObject.AddComponent<StackColumnController>();
        stack.Initialize(rules, canvas.transform, inspect);

        ZonePilesController opponentPiles = canvas.gameObject.AddComponent<ZonePilesController>();
        opponentPiles.Initialize(rules, canvas.transform, ZonePilesController.Corner.OpponentTopRight,
            OnlinePlayerContext.OpponentPlayerId);
        ZonePilesController playerPiles = canvas.gameObject.AddComponent<ZonePilesController>();
        playerPiles.Initialize(rules, canvas.transform, ZonePilesController.Corner.PlayerBottomLeft,
            OnlinePlayerContext.LocalPlayerId);

        IntegratedArenaHudController hud = canvas.gameObject.AddComponent<IntegratedArenaHudController>();
        hud.Initialize(rules, canvas.transform);

        IntegratedHandController hands = canvas.gameObject.AddComponent<IntegratedHandController>();
        hands.Initialize(rules, canvas.transform, inspect);

        // 战场悬停 → 检视联动（检视面板已就绪后回填）
        Assign(board, "inspectPanel", inspect);

        luaSystem.SetActive(true);
        systems.SetActive(true);
    }

    private static void ClearGenerated()
    {
        DestroyIfExists(ArenaRootName);
        DestroyIfExists("Canvas");
        DestroyIfExists("EventSystem");
        DestroyIfExists("Systems");
        DestroyIfExists("CardScriptRuntimeManager");
        DestroyIfExists("Main Camera");
    }

    private static void DestroyIfExists(string objectName)
    {
        GameObject existing = GameObject.Find(objectName);
        if (existing == null) return;
        if (Application.isPlaying) Object.Destroy(existing);
        else Object.DestroyImmediate(existing);
    }

    private static Camera CreateCamera(in BattlefieldCameraRig.Solution rig)
    {
        GameObject go = new GameObject("Main Camera");
        go.tag = "MainCamera";
        Camera camera = go.AddComponent<Camera>();
        go.AddComponent<AudioListener>();
        camera.orthographic = false;
        camera.fieldOfView = rig.FieldOfView;
        camera.nearClipPlane = 0.1f;
        camera.farClipPlane = 120f;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = IntegratedArenaPalette.BodyBack;
        camera.transform.SetPositionAndRotation(rig.CameraPosition, Quaternion.Euler(rig.CameraEuler));
        return camera;
    }

    private static Canvas CreateCanvas()
    {
        GameObject go = new GameObject("Canvas");
        Canvas canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        go.AddComponent<GraphicRaycaster>();
        return canvas;
    }

    private static void CreateEventSystem()
    {
        if (Object.FindFirstObjectByType<EventSystem>() != null) return;
        GameObject eventSystem = new GameObject("EventSystem");
        eventSystem.AddComponent<EventSystem>();
        eventSystem.AddComponent<StandaloneInputModule>();
    }

    private static void Assign(Object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        field?.SetValue(target, value);
    }
}
