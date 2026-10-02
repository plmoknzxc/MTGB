using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;

[Serializable] public sealed class OnlineReplayChunk
{
    public string matchId, data;
    public int index, count;
}

// Full two-seat views are sent only after that game has ended, never during play.
public sealed partial class OnlineMatchService
{
    private readonly HashSet<string> expectedReplays = new HashSet<string>();
    private readonly Dictionary<string, MemoryStream> receivingReplays = new Dictionary<string, MemoryStream>();
    private const int ReplayChunkBytes = 12000;
    private IEnumerator SendCompletedReplay(MatchReplay replay, ulong[] recipients)
    {
        Task<byte[]> encode = Task.Run(() => ReplayRepository.Encode(replay));
        while (!encode.IsCompleted) yield return null;
        if (encode.IsFaulted) { Debug.LogWarning("录像生成失败：" + encode.Exception.GetBaseException().Message); yield break; }
        byte[] bytes = encode.Result;
        int count = (bytes.Length + ReplayChunkBytes - 1) / ReplayChunkBytes;
        for (int index = 0; index < count && IsOnline; index++)
        {
            var chunk = new OnlineReplayChunk { matchId = replay.matchId, index = index, count = count,
                data = Convert.ToBase64String(bytes, index * ReplayChunkBytes, Math.Min(ReplayChunkBytes, bytes.Length - index * ReplayChunkBytes)) };
            foreach (ulong clientId in recipients)
                if (networkManager.ConnectedClients.ContainsKey(clientId)) Send(clientId, OnlineProtocol.ReplayChunk, chunk);
            yield return null;
        }
    }
    private void ReceiveReplay(OnlineReplayChunk chunk)
    {
        if (chunk == null || string.IsNullOrEmpty(chunk.matchId) || !expectedReplays.Contains(chunk.matchId)) return;
        try
        {
            if (chunk.count < 1 || chunk.count > (ReplayRepository.MaxFileBytes + ReplayChunkBytes - 1) / ReplayChunkBytes
                || chunk.index < 0 || chunk.index >= chunk.count || chunk.data == null || chunk.data.Length > ReplayChunkBytes * 2)
                throw new InvalidDataException("录像分段无效。");
            if (!receivingReplays.TryGetValue(chunk.matchId, out var stream))
            {
                if (chunk.index != 0 || receivingReplays.Count >= 4) throw new InvalidDataException("录像分段缺失。");
                receivingReplays[chunk.matchId] = stream = new MemoryStream();
            }
            if (stream.Length != (long)chunk.index * ReplayChunkBytes) throw new InvalidDataException("录像分段顺序无效。");
            byte[] part = Convert.FromBase64String(chunk.data);
            if (part.Length > ReplayChunkBytes || stream.Length + part.Length > ReplayRepository.MaxFileBytes)
                throw new InvalidDataException("录像过大。");
            stream.Write(part, 0, part.Length);
            if (chunk.index != chunk.count - 1) return;
            var replay = ReplayRepository.Decode(stream.ToArray());
            if (replay.matchId != chunk.matchId) throw new InvalidDataException("录像对局编号不符。");
            stream.Dispose(); receivingReplays.Remove(chunk.matchId); expectedReplays.Remove(chunk.matchId);
            if (!ReceiveEndReviewReplay(replay)) ReplaySavePrompt.Enqueue(replay);
        }
        catch (Exception error)
        {
            if (receivingReplays.TryGetValue(chunk.matchId, out var stream)) stream.Dispose();
            receivingReplays.Remove(chunk.matchId); expectedReplays.Remove(chunk.matchId);
            if (EndReview?.matchId == chunk.matchId) { EndReplayStatus = "录像接收失败，可以继续查看或离开"; endReplayChoice = false; }
            SetStatus("无法接收录像：" + error.Message);
        }
    }
}
