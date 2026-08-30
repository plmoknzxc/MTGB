using System;
using UnityEngine;

public class InputManager : MonoBehaviour
{
    public GameRules gameRules;
    public Camera mainCamera;

    private void Update()
    {
        // Phase Control
        if (Input.GetKeyDown(KeyCode.Space))
        {
            if (gameRules != null)
            {
                gameRules.ApplyAction(new GameAction
                {
                    type = GameActionType.AdvancePhase,
                    playerId = OnlinePlayerContext.LocalPlayerId
                });
            }
        }

        // Mouse Interaction
        if (Input.GetMouseButtonDown(0))
        {
            HandleClick();
        }
    }

    private void HandleClick()
    {
        if (gameRules == null || mainCamera == null) return;

        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit))
        {
            // Assuming the visual object has a component that links back to the RuntimeCard or Index
            // Since we don't have the visual script yet, I'll create a dummy interface here.
            
            var cardVisual = hit.collider.GetComponent<CardVisualPlaceholder>();
            if (cardVisual != null)
            {
                if (string.Equals(cardVisual.Zone, "Hand", StringComparison.OrdinalIgnoreCase))
                {
                    gameRules.ApplyAction(new GameAction
                    {
                        type = GameActionType.BeginHandCast,
                        playerId = cardVisual.OwnerID,
                        zoneIndex = cardVisual.Index
                    });
                }
            }
        }
    }
}

// Placeholder for the Visual Component user needs to attach to Card GameObjects
public class CardVisualPlaceholder : MonoBehaviour
{
    public int OwnerID;
    public int Index;
    public string Zone; // "Hand", "Battlefield"
}
