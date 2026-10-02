using System;
using System.Collections.Generic;

[Serializable]
public enum GameActionType
{
    None = 0,
    PassPriority = 1,
    AdvancePhase = 2,
    Concede = 3,
    EndTurn = 4,
    SetFullControl = 5,
    PlayHandCard = 10,
    BeginHandCast = 11,
    SelectCardTarget = 12,
    SelectPlayerTarget = 13,
    ConfirmCast = 14,
    CancelCast = 15,
    SelectCastFeature = 16,
    BeginExileCast = 17,
    ActivateAbility = 20,
    ToggleAttacker = 30,
    CommitAttackers = 31,
    DeclareBlocker = 32,
    CancelBlocker = 33,
    SetAttackerTarget = 34,
    ToggleChoiceOption = 40,
    SetChoiceNumber = 41,
    SetChoiceAllocation = 42,
    MoveChoiceOption = 43,
    ConfirmChoice = 44,
    CancelChoice = 45,
    ToggleLibraryCard = 50,
    ConfirmLibrarySearch = 51,
    ToggleDiscard = 52,
    ConfirmDiscard = 53
}

[Serializable]
public sealed class GameAction
{
    public long sequence;
    public long expectedStateSequence = -1;
    public GameActionType type;
    public int playerId;
    public long sourceObjectId;
    public long targetObjectId;
    public int targetPlayerId;
    public int zoneIndex = -1;
    public int optionId;
    public int value;
    public int destinationIndex;
    public int featureIndex = -1;
    public int choiceRequestId;
    public List<int> optionIds = new List<int>();
    public List<int> values = new List<int>();

    public GameAction Clone()
    {
        return new GameAction
        {
            sequence = sequence,
            expectedStateSequence = expectedStateSequence,
            type = type,
            playerId = playerId,
            sourceObjectId = sourceObjectId,
            targetObjectId = targetObjectId,
            targetPlayerId = targetPlayerId,
            zoneIndex = zoneIndex,
            optionId = optionId,
            value = value,
            destinationIndex = destinationIndex,
            featureIndex = featureIndex,
            choiceRequestId = choiceRequestId,
            optionIds = optionIds != null ? new List<int>(optionIds) : new List<int>(),
            values = values != null ? new List<int>(values) : new List<int>()
        };
    }
}

[Serializable]
public sealed class GameActionResult
{
    public bool accepted;
    public string error;
    public long appliedSequence;
    public string stateHash;
    public static GameActionResult Rejected(string error, long sequence) => new GameActionResult {
        accepted = false, error = error ?? "Action rejected.", appliedSequence = sequence, stateHash = string.Empty
    };
    public static GameActionResult Accepted(long sequence, string stateHash) => new GameActionResult {
        accepted = true, error = string.Empty, appliedSequence = sequence, stateHash = stateHash ?? string.Empty
    };
}
