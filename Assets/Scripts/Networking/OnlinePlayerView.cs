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
    [NonSerialized] internal string receivedWireHash,receivedTypedHash;
    public void SealHash()
    {
        receivedWireHash=receivedTypedHash=null;
        viewHash=ServerSnapshotJson.HashView(this);
    }
    public bool HasValidHash()
    {
        string current=ServerSnapshotJson.HashView(this);
        return receivedWireHash==null?string.Equals(viewHash,current,StringComparison.Ordinal)
            :string.Equals(viewHash,receivedWireHash,StringComparison.Ordinal)
                &&string.Equals(current,receivedTypedHash,StringComparison.Ordinal);
    }
}
