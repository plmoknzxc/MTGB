using System;
using UnityEngine;

public static class CardArtPreferences
{
    private static string Key(CardDefinition card) => "CardArt.Preferred." +
        (string.IsNullOrEmpty(card.oracleID) ? "printing:" + card.PrintingKey : card.oracleID);

    public static string Get(CardDefinition card) => card == null ? string.Empty : PlayerPrefs.GetString(Key(card), "");

    public static void Set(CardDefinition card, CardDefinition version)
    {
        bool allowed = false;
        foreach (var candidate in CardArtVersions.GetVersions(card))
            if (candidate.PrintingKey == version?.PrintingKey) { allowed = true; break; }
        if (!allowed || !CardImageLibrary.TryLoadExact(version, 0, out _))
            throw new InvalidOperationException("请先下载这张牌的对应版本卡图。");
        PlayerPrefs.SetString(Key(card), version.PrintingKey);
        PlayerPrefs.Save();
    }

    public static void Clear(CardDefinition card)
    {
        PlayerPrefs.DeleteKey(Key(card));
        PlayerPrefs.Save();
    }
}
