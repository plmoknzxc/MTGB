// 2025/11/24 AI-Tag
// This was created with the help of Assistant, a Unity Artificial Intelligence product.

using System.Collections.Generic;
using UnityEngine;

public class CardManager : MonoBehaviour
{
    [System.NonSerialized] public List<CardDefinition> allCards = new List<CardDefinition>();
    [SerializeField] private bool loadExternalDatabases = true;

    void Start()
    {
        LoadAllCards();
    }

    public void LoadAllCards()
    {
        allCards.Clear();

        if (loadExternalDatabases)
        {
            IReadOnlyList<CardDefinition> databaseCards = CardDatabaseRepository.LoadAll();
            for (int i = 0; i < databaseCards.Count; i++)
            {
                CardDefinition card = databaseCards[i];
                if (card == null) continue;
                allCards.Add(card);
            }
        }

        Debug.Log($"CardManager loaded {allCards.Count} database card definitions.");
    }
}
