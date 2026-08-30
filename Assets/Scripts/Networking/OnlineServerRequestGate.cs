public sealed class OnlineServerRequestGate
{
    public long LastRequestId { get; private set; }

    public bool TryPrepare(long requestId, int authenticatedSeat, GameAction action, out string error)
    {
        if (authenticatedSeat != 1 && authenticatedSeat != 2)
        {
            error = "The authenticated player seat is invalid.";
            return false;
        }

        if (action == null)
        {
            error = "Action payload is missing.";
            return false;
        }

        if (requestId <= 0 || requestId <= LastRequestId)
        {
            error = "The action request ID is missing, duplicated, or out of order.";
            return false;
        }

        LastRequestId = requestId;
        action.playerId = authenticatedSeat;
        action.sequence = 0;
        error = string.Empty;
        return true;
    }
}
