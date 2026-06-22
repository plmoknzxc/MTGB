using System;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

public class CardEditor : MonoBehaviour
{
    [Header("UI References")]
    public InputField cardNameInput;
    public InputField cardIDInput; // Added ID input
    public InputField setCodeInput;
    public InputField collectorNumberInput;
    public InputField descriptionInput;
    public InputField costInput; // Mana symbols, for example {2}{U}{U}
    public InputField attackInput;
    public InputField defenceInput;
    public InputField cardTypeInput; // Flags such as "Kindred, Sorcery"
    public InputField luaScriptInput;
    public Image artworkPreview;
    public Button saveButton;

    [Header("Settings")]
    public string artworkPath = ""; // Store the selected path

    private Sprite selectedArtwork;
    private bool selectedArtworkRuntime;

    void Start()
    {
        if (saveButton != null)
            saveButton.onClick.AddListener(SaveCard);
    }

    public void SelectArtwork(Sprite artwork)
    {
        if (selectedArtworkRuntime && selectedArtwork != null)
        {
            var oldTexture = selectedArtwork.texture;
            if (oldTexture != null) Destroy(oldTexture);
            Destroy(selectedArtwork);
            selectedArtwork = null;
        }

        selectedArtwork = artwork;
        selectedArtworkRuntime = false;
        if (artworkPreview != null)
            artworkPreview.sprite = artwork;
    }

    // Call this from a UI button or file picker
    public void SetArtworkPath(string path)
    {
        artworkPath = path;
        if (selectedArtworkRuntime && selectedArtwork != null)
        {
            var oldTexture = selectedArtwork.texture;
            if (oldTexture != null) Destroy(oldTexture);
            Destroy(selectedArtwork);
            selectedArtwork = null;
        }
        // Load preview if possible
        if (File.Exists(path))
        {
            byte[] fileData = File.ReadAllBytes(path);
            Texture2D texture = new Texture2D(2, 2);
            if (texture.LoadImage(fileData))
            {
                selectedArtwork = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
                selectedArtworkRuntime = true;
                if (artworkPreview != null)
                    artworkPreview.sprite = selectedArtwork;
            }
        }
    }

    public void SaveCard()
    {
        int attack = 0;
        int defence = 0;
        int id = 0;
        int collectorNumber = 0;
        CardType cardTypes = CardType.Creature;

        string cardName = cardNameInput != null ? cardNameInput.text : string.Empty;
        string description = descriptionInput != null ? descriptionInput.text : string.Empty;

        if (attackInput != null) int.TryParse(attackInput.text, out attack);
        if (defenceInput != null) int.TryParse(defenceInput.text, out defence);
        if (cardIDInput != null) int.TryParse(cardIDInput.text, out id);
        if (collectorNumberInput != null) int.TryParse(collectorNumberInput.text, out collectorNumber);

        if (cardTypeInput != null && !string.IsNullOrWhiteSpace(cardTypeInput.text))
        {
            var raw = cardTypeInput.text.Trim();
            if (int.TryParse(raw, out int typeValue))
            {
                if (Enum.IsDefined(typeof(CardType), typeValue))
                {
                    cardTypes = (CardType)typeValue;
                }
            }
            else if (Enum.TryParse<CardType>(raw, true, out var parsed))
            {
                cardTypes = parsed;
            }
        }

        CardDataModel newCard = new CardDataModel
        {
            cardName = cardName,
            cardID = id,
            setCode = setCodeInput != null ? setCodeInput.text.Trim() : string.Empty,
            collectorNumber = collectorNumber,
            cardTypes = cardTypes,
            description = description,
            manaCost = costInput != null ? costInput.text.Trim() : string.Empty,
            attack = attack,
            defence = defence,
            artworkPath = artworkPath,
            luaScript = luaScriptInput != null ? luaScriptInput.text : string.Empty
        };

        // Save to persistent data path for runtime
        string folderPath = Path.Combine(Application.persistentDataPath, "CustomCards");
        if (!Directory.Exists(folderPath))
        {
            Directory.CreateDirectory(folderPath);
        }

        string printing = string.IsNullOrWhiteSpace(newCard.setCode) || newCard.collectorNumber <= 0
            ? newCard.cardID.ToString()
            : $"{newCard.setCode}_{newCard.collectorNumber}";
        string fileName = $"Card_{printing}_{newCard.cardName}.json";
        // Sanitize filename
        foreach (char c in Path.GetInvalidFileNameChars())
        {
            fileName = fileName.Replace(c, '_');
        }
        
        string fullPath = Path.Combine(folderPath, fileName);
        
        string json = JsonUtility.ToJson(newCard, true);
        File.WriteAllText(fullPath, json);

        Debug.Log($"Card saved to: {fullPath}");
    }
}
