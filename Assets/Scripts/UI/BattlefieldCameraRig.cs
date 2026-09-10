using UnityEngine;

/// <summary>
/// 定稿战场透视相机与桌面世界坐标的约束求解。
///
/// 验收硬指标（spec 第 4 节）：对手排牌的屏占宽度 ≈ 己方同排的 0.85–0.90。
/// CSS 预览的 perspective(1700px) rotateX(19deg) 是一支长焦：rotateX 19° 表示桌面相对屏幕
/// 倾斜 19°，对应 Unity 俯角 90°−19° ≈ 71°（并非低角度 30–35°——该俯角在数学上只能给出
/// ≈0.53 的行宽比，无法满足硬指标）。因此这里以「桌沿屏幕位置 + 行锚点 + 行宽比」为约束
/// 直接解出相机：俯角 ≈70°、FOV ≈44°（与 spec 建议的 FOV 45–50 吻合）。
/// </summary>
public static class BattlefieldCameraRig
{
    public struct Solution
    {
        public Vector3 CameraPosition;
        public Vector3 CameraEuler;
        public float FieldOfView;
        public float FarEdgeZ;      // 远端桌沿（对手侧）
        public float NearEdgeZ;     // 近端桌沿（己方侧，负值）
        public float LandRowZ;      // 双方地排（镜像 ±）
        public float CreatureRowZ;  // 双方生物排（镜像 ±）
        public float PermanentRowZ; // 双方非地永久物排（镜像 ±）
        public float CardWidth;     // 战场卡世界宽（卡面横边）
        public float CardLength;    // 战场卡世界长（纵边，比例 0.72）
    }

    // —— 桌面/屏幕约束（来源：08-battlefield-integrated.html 标注） ——
    private const float ScreenWidth = 1920f;
    private const float ScreenHeight = 1080f;
    private const float BandTopPx = 202f;    // 远端桌沿屏幕 y
    private const float BandBottomPx = 878f; // 近端桌沿屏幕 y
    private const float WorldDepth = 6.6f;   // 可见桌面纵深（±3.3）
    private const float PitchDeg = 70f;      // 90° − CSS rotateX(19°)
    private const float LandRowVPx = 282f;   // 对手地排中心屏幕 y（对称用于己方）
    private const float TargetRowRatio = 0.875f; // 对手排/己方排 屏占宽
    private const float TargetCardPx = 84f;  // 己方地排单卡屏占宽（实测验收图 84-85px）

    /// <summary>求解相机与桌面布局；参数均可由外部覆盖（0 表示用默认）。</summary>
    public static Solution Solve(float pitchDeg = 0f, float targetRatio = 0f, float targetCardPx = 0f)
    {
        float pitch = pitchDeg > 0f ? pitchDeg : PitchDeg;
        float ratio = targetRatio > 0f ? targetRatio : TargetRowRatio;
        float cardPx = targetCardPx > 0f ? targetCardPx : TargetCardPx;

        float k = Mathf.Tan(pitch * Mathf.Deg2Rad);
        float vTop = 1f - 2f * BandTopPx / ScreenHeight;    // 远端桌沿 NDC y
        float vLand = 1f - 2f * LandRowVPx / ScreenHeight;  // 地排 NDC y

        // f(t) = ((1+r)/(1−r))·(g(vLand)−ḡ) − ḡ − k，对 t 单调递增，二分求根
        float low = 0.05f, high = 1.2f;
        float equationScale = (1f + ratio) / (1f - ratio);
        for (int i = 0; i < 64; i++)
        {
            float mid = 0.5f * (low + high);
            float gTop = G(vTop, k, mid);
            float gBottom = G(-vTop, k, mid);
            float gMean = 0.5f * (gTop + gBottom);
            float value = equationScale * (G(vLand, k, mid) - gMean) - gMean - k;
            if (value > 0f) high = mid;
            else low = mid;
        }

        float t = 0.5f * (low + high);
        float gFar = G(vTop, k, t);
        float gNear = G(-vTop, k, t);
        float gMid = 0.5f * (gFar + gNear);

        float height = WorldDepth / (gFar - gNear);
        float zc = -height * gMid;
        float landRowZ = height * (G(vLand, k, t) - gMid);

        var solution = new Solution
        {
            CameraPosition = new Vector3(0f, height, zc),
            CameraEuler = new Vector3(pitch, 0f, 0f),
            FieldOfView = 2f * Mathf.Atan(t) * Mathf.Rad2Deg,
            FarEdgeZ = WorldDepth * 0.5f,
            NearEdgeZ = -WorldDepth * 0.5f,
            LandRowZ = landRowZ,
            // 生物排贴中线：位置由半卡长 + 少量余量推导，保证不跨过中线
            CreatureRowZ = 0f,
            PermanentRowZ = 0f,
        };

        // 己方地排处 1 世界单位横对应的屏幕像素 → 反解卡的世界尺寸（卡比例 92:128 = 0.72）
        float playerLandDistance = ForwardDistance(solution, -landRowZ);
        float tanHorizontal = t * (ScreenWidth / ScreenHeight);
        float pixelsPerWorld = ScreenWidth / (2f * playerLandDistance * tanHorizontal);
        solution.CardWidth = cardPx / pixelsPerWorld;
        solution.CardLength = solution.CardWidth / 0.72f;
        solution.CreatureRowZ = solution.CardLength * 0.5f + 0.10f;
        solution.PermanentRowZ = 0.5f * (landRowZ + solution.CreatureRowZ);
        return solution;
    }

    /// <summary>相机前向轴到桌面某 z 处的距离（用于像素换算与密度规则）。</summary>
    public static float ForwardDistance(in Solution solution, float z)
    {
        float pitchRad = solution.CameraEuler.x * Mathf.Deg2Rad;
        return solution.CameraPosition.y * Mathf.Sin(pitchRad) + (z - solution.CameraPosition.z) * Mathf.Cos(pitchRad);
    }

    /// <summary>桌面 z 处一个世界单位横对应的屏幕像素（按该行深度）。</summary>
    public static float PixelsPerWorldX(in Solution solution, float z)
    {
        float distance = ForwardDistance(solution, z);
        float tanHorizontal = Mathf.Tan(solution.FieldOfView * Mathf.Deg2Rad * 0.5f) * (ScreenWidth / ScreenHeight);
        return ScreenWidth / (2f * distance * tanHorizontal);
    }

    /// <summary>世界 z 坐标 → 屏幕 NDC y（调试/校验用）。</summary>
    public static float WorldZToNdcY(in Solution solution, float z)
    {
        float pitchRad = solution.CameraEuler.x * Mathf.Deg2Rad;
        float k = Mathf.Tan(pitchRad);
        float t = Mathf.Tan(solution.FieldOfView * Mathf.Deg2Rad * 0.5f);
        float ratio = (z - solution.CameraPosition.z) / solution.CameraPosition.y;
        return (ratio * k - 1f) / (t * (k + ratio));
    }

    // 屏幕参数 v 处的视线与桌面交点：z = h·(cosθ + sinθ·t·v)/(sinθ − cosθ·t·v)（z 以相机为原点）
    private static float G(float v, float k, float t)
    {
        return (1f + k * t * v) / (k - t * v);
    }
}
