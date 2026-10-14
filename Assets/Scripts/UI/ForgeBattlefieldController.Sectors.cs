using UnityEngine;

public sealed partial class ForgeBattlefieldController
{
    private static string SectorSymbol(string sector) => sector switch {
        "Alpha" => "α", "Beta" => "β", "Gamma" => "γ", _ => ""
    };

    private static string SectorName(string sector) => sector switch {
        "Alpha" => TapPaymentText("α 分区", "Alpha sector", "アルファ・セクター"),
        "Beta" => TapPaymentText("β 分区", "Beta sector", "ベータ・セクター"),
        "Gamma" => TapPaymentText("γ 分区", "Gamma sector", "ガンマ・セクター"),
        _ => ""
    };

    private void PresentSectorBadge(RectTransform face, Vector2 size, ForgeCardView card)
    {
        if (card.zone != "Battlefield") return;
        string symbol = SectorSymbol(card.sector);
        if (symbol.Length == 0) return;
        float diameter = Mathf.Clamp(size.x * .25f, 20, 26);
        var badge = Panel(face, "SectorBadge", new Vector2(size.x*.5f-diameter*.5f-3,
            size.y*.5f-diameter*.5f-3), Vector2.one*diameter, Ink);
        badge.GetComponent<UnityEngine.UI.Image>().sprite = IntegratedUiUtility.RoundedRect(28,28,14);
        var label = Label(badge, "Sector", symbol, Vector2.zero, Vector2.one*diameter, 16, Gold);
        label.alignment = TextAnchor.MiddleCenter;
        label.resizeTextForBestFit = false;
        label.horizontalOverflow = HorizontalWrapMode.Overflow;
        label.verticalOverflow = VerticalWrapMode.Overflow;
        label.raycastTarget = false;
        badge.GetComponent<UnityEngine.UI.Image>().raycastTarget = false;
    }

    private static string SectorDetail(ForgeCardView card)
    {
        string assigned = card.zone == "Battlefield" ? SectorName(card.sector) : "";
        string chosen = card.faceDown ? "" : SectorName(card.chosenSector);
        string text = assigned.Length == 0 ? "" : TapPaymentText("所属分区：", "Assigned sector: ", "所属セクター：") + assigned;
        if (chosen.Length > 0) text += (text.Length == 0 ? "" : "\n")
            + TapPaymentText("已选分区：", "Chosen sector: ", "選んだセクター：") + chosen;
        return text;
    }
}
