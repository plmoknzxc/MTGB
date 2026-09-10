using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 战场排（spec 第 5 节）：世界空间卡牌，透视相机渲染。
/// 排序（从各自桌沿向中线）：地排（远）→ 非地永久物 → 生物排（近中线），双方镜像；
/// 卡牌基准比例 92×128（0.72），间距 7px；一行超过可用宽度先加大重叠（参考 −16px/张）、
/// 仍超则整行等比缩小，每侧每类最多两排。横置 = 桌面内旋转 90°，散布在排中原位。
/// 有图卡用真实卡图（CardImageLibrary），无图卡用结构化生成模板卡（名牌/法力费/色框/
/// 类型行/规则文本行/攻防角标），中线为红→银→蓝渐变 + 中央菱形 + 辉光。
/// </summary>
public class BattlefieldBoardController : MonoBehaviour
{
    private const float CardThickness = 0.012f;
    private const float GapPx = 7f;
    private const float OverlapExtraPx = 16f;
    private const float BoardLeftPx = 330f;
    private const float BoardRightPx = 1670f;
    private const float MinRowScale = 0.55f;
    private const float SecondRowOffset = 0.72f;

    [SerializeField] private GameRules gameRules;
    [SerializeField] private new Camera camera;
    [SerializeField] private InspectPanelController inspectPanel;

    private BattlefieldCameraRig.Solution solution;
    private float cardWidth;
    private float cardLength;
    private float boardMaxWidthPx;

    private readonly Dictionary<long, BoardCard> visuals = new Dictionary<long, BoardCard>();
    private readonly List<RuntimeCard> scratchCards = new List<RuntimeCard>(64);

    private Transform hoverRoot;
    private float glowPulseTime;

    private sealed class BoardCard
    {
        public RuntimeCard Card;
        public int Owner;
        public Transform Root;
        public Transform Glow;
        public TextMesh NameText;
        public TextMesh CostText;
        public TextMesh TypeText;
        public TextMesh PtText;
        public bool IsTemplate;
    }

    public void Initialize(GameRules rules, Camera worldCamera, in BattlefieldCameraRig.Solution rig,
        InspectPanelController inspect)
    {
        gameRules = rules;
        camera = worldCamera;
        solution = rig;
        inspectPanel = inspect;
        cardWidth = rig.CardWidth;
        cardLength = rig.CardLength;
        boardMaxWidthPx = BoardRightPx - BoardLeftPx;
        // 板区中心与 HTML 一致落在屏幕 x≈1000（左 330 / 右 250 非对称），整体偏移桌面区
        float boardCenterShift = ((BoardLeftPx - (IntegratedUiUtility.ReferenceWidth - BoardRightPx)) * 0.5f)
            / BattlefieldCameraRig.PixelsPerWorldX(rig, 0f);
        Transform arenaRoot = transform.parent != null ? transform.parent : transform;
        arenaRoot.position += new Vector3(boardCenterShift, 0f, 0f);
        CreateMidline();
    }

    private void Update()
    {
        if (gameRules == null || camera == null || !gameRules.IsInitialized()) return;
        if (cardWidth <= 0f)
        {
            // 场景重载后 solution/CardWidth 不可序列化，重算（与工厂一致，确定性）
            solution = BattlefieldCameraRig.Solve();
            cardWidth = solution.CardWidth;
            cardLength = solution.CardLength;
            boardMaxWidthPx = BoardRightPx - BoardLeftPx;
        }
        RefreshBoard();
        UpdateHover();
    }

    // ---------- 状态同步与布局 ----------

    private void RefreshBoard()
    {
        CollectMissing(OnlinePlayerContext.LocalPlayerId);
        CollectMissing(OnlinePlayerContext.OpponentPlayerId);
        RemoveStale();

        LayoutSide(OnlinePlayerContext.LocalPlayerId);
        LayoutSide(OnlinePlayerContext.OpponentPlayerId);
    }

    private void CollectMissing(int owner)
    {
        IReadOnlyList<RuntimeCard> cards = gameRules.GetBattlefieldCards(owner);
        for (int i = 0; i < cards.Count; i++)
        {
            RuntimeCard card = cards[i];
            if (card == null || visuals.ContainsKey(card.ObjectId)) continue;
            visuals[card.ObjectId] = CreateBoardCard(card, owner);
        }
    }

    private void RemoveStale()
    {
        List<long> stale = null;
        foreach (KeyValuePair<long, BoardCard> pair in visuals)
        {
            if (pair.Value.Card != null && pair.Value.Card.CurrentZone == GameZone.Battlefield) continue;
            (stale ??= new List<long>()).Add(pair.Key);
        }

        if (stale == null) return;
        for (int i = 0; i < stale.Count; i++)
        {
            BoardCard visual = visuals[stale[i]];
            if (visual != null && visual.Root != null) Destroy(visual.Root.gameObject);
            visuals.Remove(stale[i]);
            if (hoverRoot == visual?.Root) hoverRoot = null;
        }
    }

    private void LayoutSide(int owner)
    {
        bool player = owner == OnlinePlayerContext.LocalPlayerId;
        float sign = player ? -1f : 1f;
        scratchCards.Clear();
        IReadOnlyList<RuntimeCard> cards = gameRules.GetBattlefieldCards(owner);
        for (int i = 0; i < cards.Count; i++)
        {
            if (cards[i] != null && visuals.TryGetValue(cards[i].ObjectId, out BoardCard visual) && visual != null)
                scratchCards.Add(cards[i]);
        }

        LayoutClass(player, sign, CardType.Land, solution.LandRowZ);
        LayoutClass(player, sign, 0, solution.PermanentRowZ); // 0 = 非地非生物永久物
        LayoutClass(player, sign, CardType.Creature, solution.CreatureRowZ);
    }

    private void LayoutClass(bool player, float sign, CardType filter, float rowZ)
    {
        List<BoardCard> row = new List<BoardCard>(scratchCards.Count);
        List<BoardCard> overflow = new List<BoardCard>(scratchCards.Count);
        foreach (RuntimeCard card in scratchCards)
        {
            if (!visuals.TryGetValue(card.ObjectId, out BoardCard visual) || visual == null) continue;
            bool matches = filter == 0
                ? !card.Data.cardTypes.Has(CardType.Land) && !card.Data.cardTypes.Has(CardType.Creature)
                : card.Data.cardTypes.Has(filter);
            if (!matches) continue;
            (row.Count <= RowCapacity(rowZ, false) ? row : overflow).Add(visual);
        }

        if (row.Count == 0) return;

        float baseZ = sign * rowZ;
        if (overflow.Count == 0)
        {
            LayoutRow(row, baseZ, player, 0f);
            return;
        }

        // 一行放不下：起第二排（向各自桌沿方向），仍超出的排内压缩
        float secondZ = sign * (rowZ + SecondRowOffset);
        if (overflow.Count > RowCapacity(rowZ, false))
        {
            // 超过两排承载量：全部压回一排等比缩小（spec：每侧每类最多两排，再多继续压缩）
            List<BoardCard> merged = new List<BoardCard>(row);
            merged.AddRange(overflow);
            LayoutRow(merged, baseZ, player, Mathf.Epsilon);
            return;
        }

        LayoutRow(row, baseZ, player, 0f);
        LayoutRow(overflow, secondZ, player, 0.004f);
    }

    private int RowCapacity(float rowZ, bool compressed)
    {
        float maxWidth = boardMaxWidthPx / BattlefieldCameraRig.PixelsPerWorldX(solution, rowZ);
        float pxToWorld = 1f / BattlefieldCameraRig.PixelsPerWorldX(solution, rowZ);
        float step = CardFootprint(false) + (compressed ? GapPx - OverlapExtraPx : GapPx) * pxToWorld;
        return Mathf.Max(1, Mathf.FloorToInt((maxWidth - CardFootprint(false)) / step) + 1);
    }

    private float CardFootprint(bool tapped) => tapped ? cardLength : cardWidth;

    /// <summary>摆一排：居中、横置原位旋转、超宽先重叠后整行等比缩小（spec 第 5 节密度规则）。</summary>
    private void LayoutRow(List<BoardCard> row, float baseZ, bool player, float forcedScale)
    {
        if (row.Count == 0) return;
        float pxToWorld = 1f / BattlefieldCameraRig.PixelsPerWorldX(solution, baseZ);
        float maxWidth = boardMaxWidthPx * pxToWorld;

        float normalStep = cardWidth + GapPx * pxToWorld;
        float compressedStep = cardWidth + (GapPx - OverlapExtraPx) * pxToWorld;

        float NaturalWidth(List<BoardCard> cards, float step)
        {
            float total = 0f;
            for (int i = 0; i < cards.Count; i++)
            {
                total += cards[i].Card.IsTapped ? cardLength : cardWidth;
                if (i < cards.Count - 1) total += step - cardWidth;
            }
            return total;
        }

        float step = NaturalWidth(row, normalStep) <= maxWidth ? normalStep : compressedStep;
        float scale = forcedScale > 0f ? forcedScale : 1f;
        float width = NaturalWidth(row, step);
        if (width > maxWidth)
        {
            scale = Mathf.Max(MinRowScale, maxWidth / width);
        }

        // 先算总占宽再从左缘起排（居中）
        float total = 0f;
        for (int i = 0; i < row.Count; i++)
        {
            total += (row[i].Card.IsTapped ? cardLength : cardWidth) * scale;
            if (i < row.Count - 1) total += (step - cardWidth) * scale;
        }

        float cursor = -total * 0.5f;
        for (int i = 0; i < row.Count; i++)
        {
            BoardCard visual = row[i];
            bool tapped = visual.Card.IsTapped;
            float footprint = (tapped ? cardLength : cardWidth) * scale;
            float x = cursor + footprint * 0.5f;
            cursor += footprint + (i < row.Count - 1 ? (step - cardWidth) * scale : 0f);

            // Quad 旋转 X +90° 后，贴图 V+（卡面“上”）指向世界 +Z；玩家侧需再转 180° 让卡顶朝远端
            float baseYaw = player ? 180f : 0f;
            float tapYaw = tapped ? 90f : 0f;
            visual.Root.localRotation = Quaternion.Euler(0f, baseYaw + tapYaw, 0f);
            visual.Root.localPosition = new Vector3(x, CardThickness * 0.5f + i * 0.0004f, baseZ);
            visual.Root.localScale = new Vector3(scale, 1f, scale);
            UpdateFace(visual);
        }
    }

    // ---------- 卡牌视觉 ----------

    private BoardCard CreateBoardCard(RuntimeCard card, int owner)
    {
        var visual = new BoardCard { Card = card, Owner = owner };
        GameObject root = new GameObject($"BoardCard_{(owner == 1 ? "P1" : "P2")}_{card.ObjectId}");
        root.transform.SetParent(transform, false);
        visual.Root = root.transform;

        // 卡体（深色卡侧）
        GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cube);
        body.name = "Body";
        body.transform.SetParent(root.transform, false);
        body.transform.localScale = new Vector3(cardWidth, CardThickness, cardLength);
        body.GetComponent<Renderer>().sharedMaterial = GetBodyMaterial();
        if (Application.isPlaying) Object.Destroy(body.GetComponent<BoxCollider>());
        else Object.DestroyImmediate(body.GetComponent<BoxCollider>());

        // 碰撞体放在根上（悬停/点击检测目标）
        BoxCollider collider = root.AddComponent<BoxCollider>();
        collider.size = new Vector3(cardWidth, CardThickness + 0.02f, cardLength);
        collider.center = new Vector3(0f, 0f, 0f);

        // 卡面
        GameObject face = GameObject.CreatePrimitive(PrimitiveType.Quad);
        face.name = "Face";
        face.transform.SetParent(root.transform, false);
        face.transform.localPosition = new Vector3(0f, CardThickness * 0.5f + 0.0006f, 0f);
        face.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        face.transform.localScale = new Vector3(cardWidth, cardLength, 1f);
        Renderer faceRenderer = face.GetComponent<Renderer>();

        Sprite sprite = CardImageLibrary.TryLoadFullCard(card.Data, out Sprite loaded) ? loaded : null;
        if (sprite != null)
        {
            visual.IsTemplate = false;
            faceRenderer.sharedMaterial = GetTextureMaterial(sprite.texture, $"{card.Data.cardName}_Face");
        }
        else
        {
            visual.IsTemplate = true;
            faceRenderer.sharedMaterial = GetTextureMaterial(
                TemplateCardTexture.Build(card.Data, (int)(cardWidth * 220), (int)(cardLength * 220)),
                $"{card.Data.cardName}_Template");
            (visual.NameText, visual.CostText, visual.TypeText, visual.PtText) =
                CreateTemplateTexts(face.transform);
        }

        if (Application.isPlaying) Object.Destroy(face.GetComponent<Collider>());
        else Object.DestroyImmediate(face.GetComponent<Collider>());

        // 悬停辉光（卡面外扩的蓝色描边）
        GameObject glow = GameObject.CreatePrimitive(PrimitiveType.Quad);
        glow.name = "HoverGlow";
        glow.transform.SetParent(root.transform, false);
        glow.transform.localPosition = new Vector3(0f, CardThickness * 0.5f + 0.0004f, 0f);
        glow.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        glow.transform.localScale = new Vector3(cardWidth + 0.05f, cardLength + 0.05f, 1f);
        glow.GetComponent<Renderer>().sharedMaterial = GetGlowMaterial();
        glow.SetActive(false);
        if (Application.isPlaying) Object.Destroy(glow.GetComponent<Collider>());
        else Object.DestroyImmediate(glow.GetComponent<Collider>());
        visual.Glow = glow.transform;

        return visual;
    }

    private (TextMesh name, TextMesh cost, TextMesh type, TextMesh pt) CreateTemplateTexts(Transform face)
    {
        TextMesh Make(string label, TextAnchor anchor, float characterSize, Color color)
        {
            GameObject go = new GameObject($"Text_{label}");
            go.transform.SetParent(face, false);
            // Quad 正面朝局部 −Z（=世界上方）：文字绕 Y 转 180° 使其正面朝上，且沿 −Z 抬到面片上方
            go.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            go.transform.localPosition = new Vector3(0f, 0f, -0.0012f);
            TextMesh mesh = go.AddComponent<TextMesh>();
            mesh.font = IntegratedUiUtility.Font;
            mesh.fontSize = 48;
            mesh.characterSize = characterSize;
            mesh.anchor = anchor;
            mesh.alignment = TextAlignment.Left;
            mesh.color = color;
            return mesh;
        }

        // face 本地 +Y 指向卡面“上”（对方视角）；玩家侧由父级 yaw 180 翻转
        float unit = cardWidth * 0.11f;
        TextMesh name = Make("Name", TextAnchor.MiddleLeft, unit * 0.5f, IntegratedArenaPalette.GenNameText);
        name.transform.localPosition = new Vector3(-cardWidth * 0.44f, cardLength * 0.40f, 0.0012f);
        TextMesh cost = Make("Cost", TextAnchor.MiddleRight, unit * 0.5f, IntegratedArenaPalette.GenCostText);
        cost.transform.localPosition = new Vector3(cardWidth * 0.44f, cardLength * 0.40f, 0.0012f);
        TextMesh type = Make("Type", TextAnchor.MiddleLeft, unit * 0.36f, IntegratedArenaPalette.GenTypeText);
        type.transform.localPosition = new Vector3(-cardWidth * 0.40f, -cardLength * 0.08f, 0.0012f);
        TextMesh pt = Make("PT", TextAnchor.MiddleRight, unit * 0.5f, IntegratedArenaPalette.Text);
        pt.transform.localPosition = new Vector3(cardWidth * 0.38f, -cardLength * 0.42f, 0.0012f);
        return (name, cost, type, pt);
    }

    private static void UpdateFace(BoardCard visual)
    {
        if (!visual.IsTemplate) return;
        CardDefinition data = visual.Card.Data;
        ManaCost cost = data.GetManaCost();
        visual.NameText.text = data.cardName ?? string.Empty;
        visual.CostText.text = cost.IsEmpty ? string.Empty : data.manaCost;
        visual.TypeText.text = data.GetTypeLine();
        visual.PtText.text = data.cardTypes.Has(CardType.Creature)
            ? $"{visual.Card.CurrentAttack:0.#}/{visual.Card.CurrentDefence:0.#}"
            : string.Empty;
    }

    // ---------- 中线 ----------

    private void CreateMidline()
    {
        GameObject midline = GameObject.CreatePrimitive(PrimitiveType.Quad);
        midline.name = "Midline";
        midline.transform.SetParent(transform, false);
        midline.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        float pixelsPerWorld = BattlefieldCameraRig.PixelsPerWorldX(solution, 0f);
        midline.transform.localPosition = new Vector3(0f, 0.004f, 0f);
        midline.transform.localScale = new Vector3(12.4f, 0.28f, 1f); // ≈1340×31px（桌面区宽度）
        Renderer renderer = midline.GetComponent<Renderer>();
        renderer.sharedMaterial = GetTextureMaterial(MidlineTexture.Build(), "MidlineMaterial");
        if (Application.isPlaying) Object.Destroy(midline.GetComponent<Collider>());
        else Object.DestroyImmediate(midline.GetComponent<Collider>());
    }

    // ---------- 悬停 / 点击 ----------

    private void UpdateHover()
    {
        if (camera == null) return;
        Transform hitRoot = null;
        if (!Input.GetMouseButton(0) || !IsDraggingPointer())
        {
            Ray ray = camera.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit, 100f))
            {
                Transform node = hit.transform;
                while (node != null)
                {
                    if (node.name.StartsWith("BoardCard_")) { hitRoot = node; break; }
                    node = node.parent;
                }
            }
        }

        if (hoverRoot != hitRoot)
        {
            if (hoverRoot != null) SetGlow(hoverRoot, false);
            hoverRoot = hitRoot;
            if (hoverRoot != null)
            {
                SetGlow(hoverRoot, true);
                BoardCard visual = FindVisual(hoverRoot);
                if (visual != null && inspectPanel != null) inspectPanel.Show(visual.Card);
            }
        }

        glowPulseTime += Time.unscaledDeltaTime;
        if (hoverRoot != null && hoverRoot.gameObject.activeInHierarchy)
        {
            Renderer renderer = hoverRoot.Find("HoverGlow")?.GetComponent<Renderer>();
            if (renderer != null)
            {
                Color color = IntegratedArenaPalette.Blue;
                color.a = 0.55f + 0.25f * Mathf.Sin(glowPulseTime * 5f);
                renderer.material.color = color;
            }
        }

        if (hoverRoot != null && Input.GetMouseButtonDown(0)) HandleBoardClick(hoverRoot);
    }

    private static bool IsDraggingPointer()
    {
        // 预留：手牌拖拽期间禁用悬停（当前场景无拖拽，恒 false）
        return false;
    }

    private BoardCard FindVisual(Transform root)
    {
        foreach (KeyValuePair<long, BoardCard> pair in visuals)
        {
            if (pair.Value?.Root == root) return pair.Value;
        }
        return null;
    }

    private void SetGlow(Transform root, bool active)
    {
        Transform glow = root.Find("HoverGlow");
        if (glow != null) glow.gameObject.SetActive(active);
    }

    private void HandleBoardClick(Transform root)
    {
        BoardCard visual = FindVisual(root);
        if (visual == null || gameRules == null) return;
        if (visual.Owner != OnlinePlayerContext.LocalPlayerId) return;
        if (!visual.Card.Data.cardTypes.Has(CardType.Land)) return;
        if (OnlineMatchService.Instance != null && !OnlineMatchService.Instance.CanSubmitRuleAction) return;

        if ((gameRules.CanActivateManaAbility(visual.Card) || gameRules.CanActivateAnyAbility(visual.Card))
            && gameRules.ApplyAction(new GameAction
            {
                type = GameActionType.ActivateAbility,
                playerId = visual.Owner,
                sourceObjectId = visual.Card.ObjectId,
                value = 1
            }).accepted)
        {
            // 横置产法力；布局下一帧由 RefreshBoard 应用
        }
    }

    // ---------- 共享材质 ----------

    private Material bodyMaterial;
    private Material glowMaterial;
    private readonly Dictionary<Texture2D, Material> textureMaterials = new Dictionary<Texture2D, Material>();

    private Material GetBodyMaterial()
    {
        if (bodyMaterial == null)
        {
            bodyMaterial = new Material(UnlitShader()) { name = "BoardCardBody" };
            bodyMaterial.color = new Color(0.043f, 0.051f, 0.071f, 1f);
        }
        return bodyMaterial;
    }

    private Material GetGlowMaterial()
    {
        if (glowMaterial == null)
        {
            glowMaterial = new Material(UnlitShader()) { name = "BoardCardGlow" };
            SetUnlitTransparent(glowMaterial);
            glowMaterial.SetTexture("_BaseMap", IntegratedUiUtility.RoundedRectOutline(128, 178, 8, 3, "worldCardGlow").texture);
            glowMaterial.color = IntegratedArenaPalette.Blue;
        }
        return glowMaterial;
    }

    private Material GetTextureMaterial(Texture2D texture, string name)
    {
        if (textureMaterials.TryGetValue(texture, out Material cached) && cached != null) return cached;
        Shader shader = UnlitShader();
        if (shader == null) Debug.LogError($"[MTGB] Unlit shader not found for {name}");
        Material material = new Material(shader) { name = name };
        material.SetTexture("_BaseMap", texture);
        material.SetColor("_BaseColor", Color.white);
        textureMaterials[texture] = material;
        return material;
    }

    private static Shader UnlitShader()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        return shader != null ? shader : Shader.Find("Unlit/Texture");
    }

    private static void SetUnlitTransparent(Material material)
    {
        material.SetFloat("_Surface", 1f);
        material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        material.SetFloat("_ZWrite", 0f);
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
    }
}
