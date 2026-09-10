using UnityEngine;

/// <summary>
/// 定稿战场 UI（曜石竞技场 · 一体环境版）色板。
/// 规格：design-demos/mtgb-battleground/IMPLEMENTATION_SPEC.zh-CN.md 第 1 节。
/// 与 ObsidianArenaTheme 重叠的 token 直接复用旧值，其余在此补充，禁止出现色板外主色。
/// </summary>
public static class IntegratedArenaPalette
{
    // 全局背景 / 面板
    public static readonly Color Void = ObsidianArenaTheme.Void;            // #070A0F
    public static readonly Color BodyBack = Hex("#05070BFF");               // html body 背景
    public static readonly Color Panel = Hex("#0F1219E6");                  // rgba(15,18,25,.9) 身份牌/圆钮
    public static readonly Color PanelStrong = Hex("#0F1219F0");            // rgba(15,18,25,.94) 检视面板
    public static readonly Color Surface = ObsidianArenaTheme.Surface;      // #11141B
    public static readonly Color StackItem = Hex("#141821F0");              // rgba(20,24,33,.94)

    // 文字
    public static readonly Color Text = ObsidianArenaTheme.Text;            // #EDF2F8
    public static readonly Color MutedText = ObsidianArenaTheme.MutedText;  // #8995A9
    public static readonly Color SoftText = Hex("#C7D0DCFF");
    public static readonly Color FaintText = Hex("#5E6A7CFF");
    public static readonly Color GhostText = Hex("#4A5464FF");

    // 线
    public static readonly Color Line = Hex("#232936FF");
    public static readonly Color LineStrong = Hex("#2C3442FF");
    public static readonly Color CardBorder = Hex("#3A4351FF");
    public static readonly Color Silver = Hex("#8995A9FF");                 // 桌沿银（= MutedText）

    // 玩家蓝
    public static readonly Color Blue = ObsidianArenaTheme.Blue;            // #4285F4
    public static readonly Color BlueDeep = ObsidianArenaTheme.BlueDeep;    // #244987
    public static readonly Color BlueBright = Hex("#7AA8FFFF");
    public static readonly Color BlueSoft = Hex("#9FC2FFFF");
    public static readonly Color BluePlateLife = Hex("#BFD6FFFF");
    public static readonly Color BlueText = Hex("#CFE0FAFF");
    public static readonly Color BlueGlow = Hex("#A8C8FFFF");

    // 对手红
    public static readonly Color Red = ObsidianArenaTheme.Red;              // #DC3C5A
    public static readonly Color RedDeep = ObsidianArenaTheme.RedDeep;      // #5B1D2B
    public static readonly Color RedBright = Hex("#F0798FFF");
    public static readonly Color RedSoft = Hex("#FFB3C0FF");

    // 法力
    public static readonly Color ManaBlueBack = Hex("#31699CFF");
    public static readonly Color ManaColorlessBack = Hex("#4A5464FF");
    public static readonly Color ManaColorlessText = Hex("#DDE4ECFF");

    // 生成模板卡
    public static readonly Color GenBack = Hex("#0D1017FF");
    public static readonly Color GenNameBand = Hex("#1B212CFF");
    public static readonly Color GenNameText = Hex("#DFE6EEFF");
    public static readonly Color GenCostText = Hex("#8FA0B8FF");
    public static readonly Color GenArtBorder = Hex("#333B49FF");
    public static readonly Color GenTypeText = Hex("#93A0B4FF");
    public static readonly Color GenRuleLine = Hex("#2E3644FF");
    public static readonly Color GenPtBack = Hex("#161B24FF");
    public static readonly Color GenPtBorder = Hex("#303743FF");

    // 模板卡色系（.u / .w / .c 及通用五色）
    public static readonly Color GenUName = Hex("#15263AFF");
    public static readonly Color GenUArtA = Hex("#17304AFF");
    public static readonly Color GenUArtB = Hex("#10202FFF");
    public static readonly Color GenUArtMark = Hex("#4B7DB5FF");
    public static readonly Color GenWName = Hex("#2A2416FF");
    public static readonly Color GenWArtA = Hex("#362D17FF");
    public static readonly Color GenWArtB = Hex("#241D0CFF");
    public static readonly Color GenWArtMark = Hex("#B3944FFF");
    public static readonly Color GenCName = Hex("#1E2128FF");
    public static readonly Color GenCArtA = Hex("#242833FF");
    public static readonly Color GenCArtB = Hex("#171A20FF");
    public static readonly Color GenCArtMark = Hex("#6B7889FF");
    public static readonly Color GenBName = Hex("#221A20FF");
    public static readonly Color GenBArtA = Hex("#251C24FF");
    public static readonly Color GenBArtB = Hex("#171017FF");
    public static readonly Color GenBArtMark = Hex("#6D5580FF");
    public static readonly Color GenRName = Hex("#2B1616FF");
    public static readonly Color GenRArtA = Hex("#361C16FF");
    public static readonly Color GenRArtB = Hex("#22110DFF");
    public static readonly Color GenRArtMark = Hex("#B05F43FF");
    public static readonly Color GenGName = Hex("#16231AFF");
    public static readonly Color GenGArtA = Hex("#1B2E1CFF");
    public static readonly Color GenGArtB = Hex("#101E11FF");
    public static readonly Color GenGArtMark = Hex("#4F8A52FF");

    // 空区域堆斜纹
    public static readonly Color PileStripeA = Hex("#171C26FF");
    public static readonly Color PileStripeB = Hex("#12161FFF");

    public static Color Hex(string value)
    {
        return ColorUtility.TryParseHtmlString(value, out Color color) ? color : Color.magenta;
    }

    /// <summary>构造带透明度的颜色（保留原 RGB）。</summary>
    public static Color WithAlpha(Color color, float alpha)
    {
        color.a = Mathf.Clamp01(alpha);
        return color;
    }
}
