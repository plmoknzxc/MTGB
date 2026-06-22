// 2025/11/24 AI-Tag
// This was created with the help of Assistant, a Unity Artificial Intelligence product.

using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class CardDataConverter : MonoBehaviour
{
    public List<CardData> loadedCards = new List<CardData>();

    public void LoadCustomCards(string filePath)
    {
        if (File.Exists(filePath))
        {
            string json = File.ReadAllText(filePath);
            var cardModels = CardDataModelJson.ParseList(json);
            if (cardModels.Count == 0)
            {
                Debug.LogWarning($"No valid card data found in: {filePath}");
                return;
            }

            foreach (var model in cardModels)
            {
                CardData newCard = ScriptableObject.CreateInstance<CardData>();
                newCard.cardName = model.cardName;
                newCard.cardID = model.cardID;
                newCard.setCode = model.setCode;
                newCard.collectorNumber = model.collectorNumber;
                newCard.cardTypes = model.GetCardTypes();
                newCard.description = model.description;
                newCard.manaCost = model.GetManaCostString();
                newCard.attack = model.attack;
                newCard.defence = model.defence;
                newCard.luaScript = model.luaScript;

                // 加载图片
                if (!string.IsNullOrEmpty(model.artworkPath) && File.Exists(model.artworkPath))
                {
                    byte[] fileData = File.ReadAllBytes(model.artworkPath);
                    Texture2D texture = new Texture2D(2, 2);
                    if (texture.LoadImage(fileData))
                    {
                        newCard.artwork = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
                        newCard.artworkRuntime = true;
                    }
                }

                loadedCards.Add(newCard);
                Debug.Log($"Custom card loaded: {newCard.cardName} (ID: {newCard.cardID})");
            }
        }
        else
        {
            Debug.LogError("Custom card file not found: " + filePath);
        }
    }
}
