using System;

[Serializable]
public enum MatchEndReason
{
    None = 0,
    LifeTotal = 1,
    EmptyLibrary = 2,
    SimultaneousLoss = 3,
    Concession = 4,
    PoisonCounters = 5,
    CardEffect = 6
}

[Serializable]
public struct MatchOutcome
{
    public bool isFinished;
    public int winnerPlayerId;
    public MatchEndReason reason;
    public string message;

    public bool IsDraw => isFinished && winnerPlayerId == 0;

    public static MatchOutcome InProgress()
    {
        return new MatchOutcome
        {
            isFinished = false,
            winnerPlayerId = 0,
            reason = MatchEndReason.None,
            message = string.Empty
        };
    }
}
