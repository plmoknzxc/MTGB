using System.Text;

public enum PriorityPassOutcome
{
    Rejected = 0,
    PassedToOpponent = 1,
    WindowComplete = 2
}

public sealed class PriorityState
{
    public bool IsOpen { get; private set; }
    public int PlayerWithPriority { get; private set; } = 1;
    public bool ActivePlayerPassed { get; private set; }
    public bool NonActivePlayerPassed { get; private set; }

    public void Reset(int firstPlayer = 1)
    {
        PlayerWithPriority = NormalizePlayer(firstPlayer);
        IsOpen = false;
        ActivePlayerPassed = false;
        NonActivePlayerPassed = false;
    }

    public void Begin(int firstPlayer)
    {
        PlayerWithPriority = NormalizePlayer(firstPlayer);
        IsOpen = true;
        ActivePlayerPassed = false;
        NonActivePlayerPassed = false;
    }

    public void Close()
    {
        IsOpen = false;
        ActivePlayerPassed = false;
        NonActivePlayerPassed = false;
    }

    public void Restore(
        bool isOpen,
        int playerWithPriority,
        bool activePlayerPassed = false,
        bool nonActivePlayerPassed = false)
    {
        IsOpen = isOpen;
        PlayerWithPriority = NormalizePlayer(playerWithPriority);
        ActivePlayerPassed = isOpen && activePlayerPassed;
        NonActivePlayerPassed = isOpen && nonActivePlayerPassed;
    }

    public PriorityPassOutcome TryPass(int player, int activePlayer)
    {
        if (!IsOpen || player != PlayerWithPriority)
            return PriorityPassOutcome.Rejected;

        if (player == activePlayer) ActivePlayerPassed = true;
        else NonActivePlayerPassed = true;

        if (ActivePlayerPassed && NonActivePlayerPassed)
        {
            Close();
            return PriorityPassOutcome.WindowComplete;
        }

        PlayerWithPriority = player == 1 ? 2 : 1;
        return PriorityPassOutcome.PassedToOpponent;
    }

    public void AppendDeterministicState(StringBuilder builder)
    {
        builder.Append(PlayerWithPriority).Append('|')
            .Append(IsOpen ? 1 : 0).Append('|')
            .Append(ActivePlayerPassed ? 1 : 0).Append('|')
            .Append(NonActivePlayerPassed ? 1 : 0).Append('|');
    }

    private static int NormalizePlayer(int player)
    {
        return player == 2 ? 2 : 1;
    }
}

public static class PriorityAutomationPolicy
{
    public static bool ShouldAutoPass(
        bool isOnlineMatch,
        int playerWithPriority,
        bool fullControl,
        bool isActivePlayer,
        TurnPhase phase,
        bool hasAvailableAction)
    {
        if (!isOnlineMatch && playerWithPriority != 1) return true;
        if (fullControl) return false;
        if (isActivePlayer && IsRequiredDecisionPhase(phase)) return false;
        return !hasAvailableAction;
    }

    private static bool IsRequiredDecisionPhase(TurnPhase phase)
    {
        return phase == TurnPhase.Main1
            || phase == TurnPhase.Main2
            || phase == TurnPhase.DeclareAttackers;
    }
}
