using UnityEngine;
using UnityEngine.UI;

public class BattleUIController : MonoBehaviour
{
    public GameRules gameRules;
    public Text playerDeckCountText;
    public Text opponentDeckCountText;
    public Text playerHandCountText;
    public Text opponentHandCountText;
    public Text currentPlayerText;
    public Text currentPhaseText;
    public Button endTurnButton;

    void Start()
    {
        if (gameRules == null)
        {
            gameRules = Object.FindFirstObjectByType<GameRules>();
        }

        if (playerDeckCountText == null) playerDeckCountText = GameObject.Find("PlayerDeckCountText")?.GetComponent<Text>();
        if (opponentDeckCountText == null) opponentDeckCountText = GameObject.Find("OpponentDeckCountText")?.GetComponent<Text>();
        if (playerHandCountText == null) playerHandCountText = GameObject.Find("PlayerHandCountText")?.GetComponent<Text>();
        if (opponentHandCountText == null) opponentHandCountText = GameObject.Find("OpponentHandCountText")?.GetComponent<Text>();
        if (currentPlayerText == null) currentPlayerText = GameObject.Find("CurrentPlayerText")?.GetComponent<Text>();
        if (currentPhaseText == null) currentPhaseText = GameObject.Find("CurrentPhaseText")?.GetComponent<Text>();
        if (endTurnButton == null) endTurnButton = GameObject.Find("EndTurnButton")?.GetComponent<Button>();

        if (endTurnButton != null)
        {
            endTurnButton.onClick.AddListener(OnEndTurn);
        }
    }

    void Update()
    {
        if (gameRules == null) return;

        if (playerDeckCountText != null)
        {
            playerDeckCountText.text = $"Deck: {gameRules.GetDeckCount(OnlinePlayerContext.LocalPlayerId)}";
        }

        if (opponentDeckCountText != null)
        {
            opponentDeckCountText.text = $"Deck: {gameRules.GetDeckCount(OnlinePlayerContext.OpponentPlayerId)}";
        }

        if (playerHandCountText != null)
        {
            playerHandCountText.text = $"Hand: {gameRules.GetHandCount(OnlinePlayerContext.LocalPlayerId)}";
        }

        if (opponentHandCountText != null)
        {
            opponentHandCountText.text = $"Hand: {gameRules.GetHandCount(OnlinePlayerContext.OpponentPlayerId)}";
        }

        if (currentPlayerText != null)
        {
            currentPlayerText.text = $"Current Player: {gameRules.GetCurrentPlayer()}";
        }

        if (currentPhaseText != null)
        {
            currentPhaseText.text = $"Phase: {gameRules.GetCurrentPhaseLabel()}";
        }
    }

    private void OnEndTurn()
    {
        if (gameRules != null && OnlineMatchService.Instance?.CanSubmitRuleAction != false)
        {
            GameActionType actionType = gameRules.GetPrimaryAdvanceActionType(
                OnlinePlayerContext.LocalPlayerId);
            gameRules.ApplyAction(new GameAction
            {
                type = actionType,
                playerId = OnlinePlayerContext.LocalPlayerId
            });
        }
    }
}
