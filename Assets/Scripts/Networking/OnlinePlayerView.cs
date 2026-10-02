using System;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
[Serializable]
public sealed class OnlinePlayerViewState
{
    public string matchId, viewHash;
    public ForgeSeatView forge;
    public int viewerSeat;
    public long appliedSequence;
    public MatchOutcome outcome;
    public void SealHash()
    {
        viewHash = string.Empty;
        using SHA256 sha = SHA256.Create();
        byte[] bytes = Encoding.UTF8.GetBytes(JsonUtility.ToJson(this));
        viewHash = BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", string.Empty);
    }
}
