using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

[Serializable]
public sealed class OnlinePlayerViewState
{
    public int viewerSeat;
    public long appliedSequence;
    public int startingPlayer;
    public bool openingMulligan;
    public int player1MulliganCount;
    public int player2MulliganCount;
    public int currentPlayer;
    public TurnPhase currentPhase;
    public int turnNumber;
    public bool awaitingPriority;
    public int priorityPlayer;
    public bool fullControl;
    public int player1Life;
    public int player2Life;
    public int player1Poison;
    public int player2Poison;
    public int player1MaxHandSize;
    public int player2MaxHandSize;
    public ManaPool player1Mana;
    public ManaPool player2Mana;
    public MatchOutcome outcome;
    public bool hasActiveChoice;
    public bool canConfirmActiveChoice;
    public GameChoiceRequest activeChoice;
    public List<OnlineVisibleCard> player1Library = new List<OnlineVisibleCard>();
    public List<OnlineVisibleCard> player2Library = new List<OnlineVisibleCard>();
    public List<OnlineVisibleCard> player1Hand = new List<OnlineVisibleCard>();
    public List<OnlineVisibleCard> player2Hand = new List<OnlineVisibleCard>();
    public List<OnlineVisibleCard> player1Battlefield = new List<OnlineVisibleCard>();
    public List<OnlineVisibleCard> player2Battlefield = new List<OnlineVisibleCard>();
    public List<OnlineVisibleCard> player1Graveyard = new List<OnlineVisibleCard>();
    public List<OnlineVisibleCard> player2Graveyard = new List<OnlineVisibleCard>();
    public List<OnlineVisibleCard> player1Exile = new List<OnlineVisibleCard>();
    public List<OnlineVisibleCard> player2Exile = new List<OnlineVisibleCard>();
    public List<OnlineVisibleCard> player1Command = new List<OnlineVisibleCard>();
    public List<OnlineVisibleCard> player2Command = new List<OnlineVisibleCard>();
    public List<OnlineVisibleCard> stack = new List<OnlineVisibleCard>();
    public string viewHash;

    public void SealHash()
    {
        viewHash = string.Empty;
        using SHA256 sha = SHA256.Create();
        byte[] bytes = Encoding.UTF8.GetBytes(JsonUtility.ToJson(this));
        viewHash = BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", string.Empty);
    }
}

[Serializable]
public sealed class OnlineVisibleCard
{
    public bool hidden;
    public bool publicObject;
    public long objectId;
    public int zoneChangeVersion;
    public string setCode;
    public string collectorNumber;
    public int cardId;
    public int ownerPlayerId;
    public int controllerPlayerId;
    public GameZone zone;
    public int currentFaceIndex;
    public int currentAttack;
    public int currentDefence;
    public int currentLoyalty;
    public int currentBattleDefense;
    public int battleProtectorPlayerId;
    public int damageTaken;
    public string visibleCardName;
    public string visibleRulesText;
    public string visibleManaCost;
    public CardType visibleCardTypes;
    public CardSupertype visibleSupertypes;
    public string visibleSubtypes;
    public ManaColorFlags visibleColors;
    public List<string> visibleKeywords = new List<string>();
    public int classLevel;
    public bool isCaseSolved;
    public bool canCastPrimaryFromAdventureExile;
    public bool isPrepared;
    public bool isSpellCopy;
    public bool isToken;
    public bool isEmblem;
    public bool canPlayFromHand;
    public bool canCastFromExile;
    public bool canActivateAbility;
    public bool canActivateManaAbility;
    public bool canDeclareAttacker;
    public bool pendingAttacker;
    public int pendingAttackTargetPlayerId;
    public long pendingAttackTargetObjectId;
    public int legalAttackTargetPlayerId;
    public List<long> legalAttackTargetObjectIds = new List<long>();
    public bool canCancelBlocker;
    public List<long> legalBlockTargetObjectIds = new List<long>();
    public long attachedToObjectId;
    public List<int> unlockedRoomFeatureIndices = new List<int>();
    public bool tapped;
    public bool attacking;
    public bool blocked;
    public bool blocking;
    public int attackTargetPlayerId;
    public long attackTargetObjectId;
    public long blockingTargetObjectId;
    public List<long> blockerObjectIds = new List<long>();
    public List<OnlineCounterValue> counters = new List<OnlineCounterValue>();
}

[Serializable]
public sealed class OnlineCounterValue
{
    public string name;
    public int value;
}

public static class OnlinePlayerViewSecurity
{
    public static bool ContainsHiddenIdentity(OnlinePlayerViewState view, int hiddenOwner)
    {
        if (view == null) return false;
        return ContainsIdentity(view.viewerSeat, hiddenOwner, view.player1Library)
            || ContainsIdentity(view.viewerSeat, hiddenOwner, view.player2Library)
            || ContainsIdentity(view.viewerSeat, hiddenOwner, view.player1Hand)
            || ContainsIdentity(view.viewerSeat, hiddenOwner, view.player2Hand);
    }

    private static bool ContainsIdentity(int viewer, int owner, List<OnlineVisibleCard> cards)
    {
        if (cards == null) return false;
        for (int i = 0; i < cards.Count; i++)
        {
            OnlineVisibleCard card = cards[i];
            if (card == null || card.ownerPlayerId != owner) continue;
            bool privateZone = card.zone == GameZone.Library || (card.zone == GameZone.Hand && owner != viewer);
            if (privateZone && (!card.hidden || card.cardId != 0
                || !string.IsNullOrEmpty(card.setCode) || !string.IsNullOrEmpty(card.collectorNumber)))
            {
                return true;
            }
            if (card.hidden && (!string.IsNullOrEmpty(card.setCode)
                || !string.IsNullOrEmpty(card.collectorNumber) || card.cardId != 0)) return true;
        }
        return false;
    }
}
