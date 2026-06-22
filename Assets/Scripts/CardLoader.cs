using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class CardLoader : MonoBehaviour
{
    // Uses global CardDataModel class

    public List<CardData> loadedCards = new List<CardData>();

    public void LoadCustomCards(string filePath)
    {
        if (Directory.Exists(filePath))
        {
            LoadCustomCardsFromDirectory(filePath);
            return;
        }

        if (File.Exists(filePath))
        {
            string json = File.ReadAllText(filePath);
            var cardModels = CardDataModelJson.ParseList(json);
            if (cardModels.Count > 0)
            {
                foreach (var model in cardModels)
                {
                    CreateCardFromModel(model);
                }
            }
            else
            {
                Debug.LogWarning($"No valid card data found in: {filePath}");
            }
        }
        else
        {
            Debug.LogError("Custom card file not found: " + filePath);
        }
    }

    private void LoadCustomCardsFromDirectory(string folderPath)
    {
        var files = Directory.GetFiles(folderPath, "*.json", SearchOption.TopDirectoryOnly);
        if (files.Length == 0)
        {
            Debug.LogWarning($"No card JSON files found in folder: {folderPath}");
            return;
        }

        foreach (var file in files)
        {
            string json = File.ReadAllText(file);
            var cardModels = CardDataModelJson.ParseList(json);
            if (cardModels.Count == 0)
            {
                Debug.LogWarning($"No valid card data found in: {file}");
                continue;
            }

            foreach (var model in cardModels)
            {
                CreateCardFromModel(model);
            }
        }
    }

    private void CreateCardFromModel(CardDataModel model)
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

        // Load Artwork
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
        Debug.Log($"Loaded card: {newCard.cardName}");
    }
}
