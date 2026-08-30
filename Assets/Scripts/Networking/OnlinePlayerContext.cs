public static class OnlinePlayerContext
{
    public static int LocalPlayerId { get; private set; } = 1;
    public static int OpponentPlayerId => LocalPlayerId == 1 ? 2 : 1;

    public static void SetLocalPlayer(int playerId)
    {
        LocalPlayerId = playerId == 2 ? 2 : 1;
    }

    public static void Reset()
    {
        LocalPlayerId = 1;
    }
}
