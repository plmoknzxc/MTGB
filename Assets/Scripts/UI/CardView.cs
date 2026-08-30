using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class CardView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private CardDefinition cardData;
    [SerializeField] private Text cardNameText;
    [SerializeField] private Text manaCostText;
    [SerializeField] private Image artworkImage;
    [SerializeField] private Text artworkPlaceholderText;
    [SerializeField] private Text typeLineText;
    [SerializeField] private Text rulesText;
    [SerializeField] private Text powerToughnessText;
    [SerializeField] private Text collectorInfoText;
    [SerializeField] private GameObject detailPanel;

    [Header("Future Data Hooks")]
    [SerializeField] private string manaCostOverride;
    [SerializeField] private string typeLineOverride;
    [SerializeField] private string collectorInfoOverride;
    [SerializeField] private string rarityOverride;
    [SerializeField] private string setCodeOverride;

    private void Awake()
    {
        SetDetailVisible(false);
        Refresh();
    }

    private void OnValidate()
    {
        Refresh();
    }

    public void SetCardDefinition(CardDefinition data)
    {
        cardData = data;
        Refresh();
    }

    public void Refresh()
    {
        if (cardData == null)
        {
            SetText(cardNameText, "Card Name");
            SetText(manaCostText, string.IsNullOrEmpty(manaCostOverride) ? "—" : manaCostOverride);
            SetText(typeLineText, string.IsNullOrEmpty(typeLineOverride) ? "Card Type" : typeLineOverride);
            SetText(rulesText, "Rules text / abilities.");
            SetText(powerToughnessText, "— / —");
            SetText(collectorInfoText, BuildCollectorInfo());
            SetArtwork(null);
            return;
        }

        SetText(cardNameText, string.IsNullOrEmpty(cardData.cardName) ? cardData.name : cardData.cardName);
        SetText(manaCostText, string.IsNullOrEmpty(manaCostOverride) ? cardData.GetManaCost().ToSymbolString() : manaCostOverride);
        SetText(typeLineText, string.IsNullOrEmpty(typeLineOverride) ? cardData.GetTypeLine() : typeLineOverride);
        SetText(rulesText, cardData.description);
        SetText(powerToughnessText, BuildStatsText(cardData));
        SetText(collectorInfoText, BuildCollectorInfo());
        SetArtwork(cardData.artwork);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        SetDetailVisible(true);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        SetDetailVisible(false);
    }

    private string BuildCollectorInfo()
    {
        if (!string.IsNullOrEmpty(collectorInfoOverride)) return collectorInfoOverride;

        var setCode = string.IsNullOrEmpty(setCodeOverride)
            ? (cardData == null || string.IsNullOrWhiteSpace(cardData.setCode) ? "SET" : cardData.setCode)
            : setCodeOverride;
        var rarity = string.IsNullOrEmpty(rarityOverride) ? "C" : rarityOverride;
        var id = cardData == null || string.IsNullOrEmpty(cardData.CollectorNumber)
            ? "000"
            : cardData.CollectorNumber;
        return $"{id}/000 · {setCode} · {rarity}";
    }

    private void SetArtwork(Sprite sprite)
    {
        if (artworkImage != null)
        {
            artworkImage.sprite = sprite;
            artworkImage.color = sprite == null ? new Color(0.30f, 0.42f, 0.55f, 1f) : Color.white;
            artworkImage.preserveAspect = true;
        }

        if (artworkPlaceholderText != null)
        {
            artworkPlaceholderText.gameObject.SetActive(sprite == null);
        }
    }

    private void SetDetailVisible(bool visible)
    {
        if (detailPanel != null)
        {
            detailPanel.SetActive(visible);
        }
    }

    private static void SetText(Text text, string value)
    {
        if (text != null)
        {
            text.text = value;
        }
    }

    private static string BuildStatsText(CardDefinition card)
    {
        if (card == null) return string.Empty;
        if (card.HasType(CardType.Creature)) return $"{card.attack} / {card.defence}";
        if (card.HasType(CardType.Planeswalker)) return card.loyalty.ToString();
        if (card.HasType(CardType.Battle)) return card.battleDefense.ToString();
        return string.Empty;
    }
}
