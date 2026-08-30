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
    public List<GameEventRecord> events = new List<GameEventRecord>();

    public static GameActionResult Rejected(string error, long sequence)
    {
        return new GameActionResult
        {
            accepted = false,
            error = error ?? "Action rejected.",
            appliedSequence = sequence,
            stateHash = string.Empty
        };
    }

    public static GameActionResult Accepted(
        long sequence,
        string stateHash,
        IReadOnlyList<GameEventRecord> events = null)
    {
        var result = new GameActionResult
        {
            accepted = true,
            error = string.Empty,
            appliedSequence = sequence,
            stateHash = stateHash ?? string.Empty
        };
        if (events != null)
        {
            for (int i = 0; i < events.Count; i++)
            {
                result.events.Add(events[i]?.Clone());
            }
        }
        return result;
    }
}

[Serializable]
public sealed class GameEventRecord
{
    public long eventId;
    public GameEventType type;
    public long sourceObjectId;
    public long cardObjectId;
    public int playerId;
    public int controllerId;
    public GameZone fromZone;
    public GameZone toZone;
    public int amount;
    public int originalAmount;
    public string detail;
    public DamageEventKind damageKind;
    public bool prevented;
    public bool replaced;

    public static GameEventRecord FromContext(GameEventContext context)
    {
        if (context == null) return null;
        return new GameEventRecord
        {
            eventId = context.eventId,
            type = context.type,
            sourceObjectId = context.sourceObjectId > 0
                ? context.sourceObjectId
                : context.source?.ObjectId ?? 0,
            cardObjectId = context.cardObjectId > 0
                ? context.cardObjectId
                : context.card?.ObjectId ?? 0,
            playerId = context.playerId,
            controllerId = context.controllerId,
            fromZone = context.fromZone,
            toZone = context.toZone,
            amount = context.amount,
            originalAmount = context.originalAmount,
            detail = context.detail,
            damageKind = context.damageKind,
            prevented = context.prevented,
            replaced = context.replaced
        };
    }

    public GameEventRecord Clone()
    {
        return (GameEventRecord)MemberwiseClone();
    }
}

[Serializable]
public sealed class GameReplay
{
    public const int CurrentVersion = 6;

    public int version = CurrentVersion;
    public int randomSeed;
    public int startingPlayer;
    public string initialStateHash;
    public List<GameAction> actions = new List<GameAction>();
    public List<string> stateHashes = new List<string>();
    public List<GameEventBatch> eventBatches = new List<GameEventBatch>();

    public void Record(
        GameAction action,
        string stateHash,
        IReadOnlyList<GameEventRecord> events)
    {
        if (action == null) return;
        actions.Add(action.Clone());
        stateHashes.Add(stateHash ?? string.Empty);
        eventBatches.Add(GameEventBatch.Create(action.sequence, events));
    }

    public GameReplay Clone()
    {
        var clone = new GameReplay
        {
            version = version,
            randomSeed = randomSeed,
            startingPlayer = startingPlayer,
            initialStateHash = initialStateHash
        };
        for (int i = 0; i < actions.Count; i++)
        {
            clone.actions.Add(actions[i]?.Clone());
        }
        clone.stateHashes.AddRange(stateHashes);
        for (int i = 0; i < eventBatches.Count; i++)
        {
            GameEventBatch batch = eventBatches[i];
            clone.eventBatches.Add(
                GameEventBatch.Create(batch?.actionSequence ?? 0, batch?.events));
        }
        return clone;
    }
}

[Serializable]
public sealed class GameEventBatch
{
    public long actionSequence;
    public List<GameEventRecord> events = new List<GameEventRecord>();

    public static GameEventBatch Create(
        long actionSequence,
        IReadOnlyList<GameEventRecord> source)
    {
        var batch = new GameEventBatch { actionSequence = actionSequence };
        if (source == null) return batch;
        for (int i = 0; i < source.Count; i++)
        {
            batch.events.Add(source[i]?.Clone());
        }
        return batch;
    }
}

[Serializable]
public sealed class GameReplayResult
{
    public bool succeeded;
    public int appliedActionCount;
    public int failedActionIndex = -1;
    public string error;
    public string finalStateHash;
}
