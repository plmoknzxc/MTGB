// 2025/11/24 AI-Tag
// This was created with the help of Assistant, a Unity Artificial Intelligence product.

using System.Collections.Generic;
using UnityEngine;

public class CardManager : MonoBehaviour
{
    public List<CardData> allCards = new List<CardData>();
    [SerializeField] private bool loadExternalDatabases = true;

    void Start()
    {
        LoadAllCards();
    }

    public void LoadAllCards()
    {
        allCards.Clear();
        var knownIds = new HashSet<int>();
        var cardAssets = Resources.LoadAll<CardData>(string.Empty);

        foreach (var card in cardAssets)
        {
            if (card == null || !knownIds.Add(card.cardID)) continue;
            allCards.Add(card);
        }

        if (loadExternalDatabases)
        {
            IReadOnlyList<CardData> databaseCards = CardDatabaseRepository.LoadAll();
            for (int i = 0; i < databaseCards.Count; i++)
            {
                CardData card = databaseCards[i];
                if (card == null || !knownIds.Add(card.cardID)) continue;
                allCards.Add(card);
            }
        }

        Debug.Log($"CardManager loaded {allCards.Count} card definitions.");
    }
}
