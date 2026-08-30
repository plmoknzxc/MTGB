using System;

[Serializable]
public sealed class MTGAHandLayoutSettings
{
    public float playerRootY = 0f;
    public float playerRootHeight = 292f;
    public float playerBaseY = 26f;
    public float playerArcDepth = 38f;
    public float playerMaxSpacing = 132f;
    public float playerTotalWidth = 1540f;
    public float playerRotationPerOffset = -3.65f;
    public float playerCardWidth = 194f;
    public float playerCardHeight = 274f;
    public float playerMinScale = 0.60f;

    public float opponentRootY = 0f;
    public float opponentRootHeight = 86f;
    public float opponentBaseY = 76f;
    public float opponentArcDepth = 5f;
    public float opponentMaxSpacing = 70f;
    public float opponentTotalWidth = 780f;
    public float opponentRotationPerOffset = 1.65f;
    public float opponentCardWidth = 74f;
    public float opponentCardHeight = 104f;
    public float opponentMinScale = 0.62f;

    public int fullScaleCardCount = 7;

    public MTGAHandLayoutSettings Clone()
    {
        return (MTGAHandLayoutSettings)MemberwiseClone();
    }

    public void CopyFrom(MTGAHandLayoutSettings other)
    {
        if (other == null) return;

        playerRootY = other.playerRootY;
        playerRootHeight = other.playerRootHeight;
        playerBaseY = other.playerBaseY;
        playerArcDepth = other.playerArcDepth;
        playerMaxSpacing = other.playerMaxSpacing;
        playerTotalWidth = other.playerTotalWidth;
        playerRotationPerOffset = other.playerRotationPerOffset;
        playerCardWidth = other.playerCardWidth;
        playerCardHeight = other.playerCardHeight;
        playerMinScale = other.playerMinScale;

        opponentRootY = other.opponentRootY;
        opponentRootHeight = other.opponentRootHeight;
        opponentBaseY = other.opponentBaseY;
        opponentArcDepth = other.opponentArcDepth;
        opponentMaxSpacing = other.opponentMaxSpacing;
        opponentTotalWidth = other.opponentTotalWidth;
        opponentRotationPerOffset = other.opponentRotationPerOffset;
        opponentCardWidth = other.opponentCardWidth;
        opponentCardHeight = other.opponentCardHeight;
        opponentMinScale = other.opponentMinScale;

        fullScaleCardCount = other.fullScaleCardCount;
        Sanitize();
    }

    public void Sanitize()
    {
        playerRootHeight = Math.Max(1f, playerRootHeight);
        playerMaxSpacing = Math.Max(1f, playerMaxSpacing);
        playerTotalWidth = Math.Max(1f, playerTotalWidth);
        playerCardWidth = Math.Max(1f, playerCardWidth);
        playerCardHeight = Math.Max(1f, playerCardHeight);
        playerMinScale = Clamp(playerMinScale, 0.1f, 1f);

        opponentRootHeight = Math.Max(1f, opponentRootHeight);
        opponentMaxSpacing = Math.Max(1f, opponentMaxSpacing);
        opponentTotalWidth = Math.Max(1f, opponentTotalWidth);
        opponentCardWidth = Math.Max(1f, opponentCardWidth);
        opponentCardHeight = Math.Max(1f, opponentCardHeight);
        opponentMinScale = Clamp(opponentMinScale, 0.1f, 1f);

        fullScaleCardCount = Clamp(fullScaleCardCount, 1, 20);
    }

    private static float Clamp(float value, float min, float max)
    {
        if (value < min) return min;
        if (value > max) return max;
        return value;
    }

    private static int Clamp(int value, int min, int max)
    {
        if (value < min) return min;
        if (value > max) return max;
        return value;
    }
}
