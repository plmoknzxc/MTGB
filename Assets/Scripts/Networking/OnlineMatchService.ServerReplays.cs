using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

public sealed partial class OnlineMatchService
{
    private readonly List<Task<string>> serverReplayWrites = new List<Task<string>>();
    private string serverReplayDirectory;

    private void InitializeServerReplayArchive()
    {
        serverReplayDirectory=ReplayRepository.Root;
        try { Directory.CreateDirectory(serverReplayDirectory); }
        catch(Exception error) { Debug.LogError("无法创建服务器录像目录："+error.Message); }
    }

    private void QueueServerReplayArchive(MatchReplay replay,Task<byte[]> encoded)
    {
        if(!IsServer)return;
        string directory=serverReplayDirectory??ReplayRepository.Root;
        string id=new string((replay.matchId??"").Where(c=>char.IsLetterOrDigit(c)||c=='-'||c=='_').Take(40).ToArray());
        string name=ReplayRepository.DefaultName(replay)+" G"+replay.gameNumber+" "+id;
        serverReplayWrites.Add(Task.Run(async()=>{
            byte[] bytes=await encoded.ConfigureAwait(false);
            return ReplayRepository.SaveEncodedArchive(bytes,name,directory);
        }));
    }

    private void TickServerReplayArchives()
    {
        for(int i=serverReplayWrites.Count-1;i>=0;i--){
            var task=serverReplayWrites[i];if(!task.IsCompleted)continue;
            if(task.IsFaulted)Debug.LogError("服务器录像保存失败："+task.Exception.GetBaseException().Message);
            serverReplayWrites.RemoveAt(i);
        }
    }

    private void ArchiveOpenServerMatches()
    {
        if(!IsServer)return;
        foreach(var room in roomRegistry.Rooms.Where(r=>r.match!=null)){
            try {
                string[] players=Enumerable.Range(1,2).Select(seat=>room.members.TryGetValue(seat,out var member)?member.displayName:"Player "+seat).ToArray();
                var replay=room.match.CompleteReplay(players,room.series?.gameNumber??1,room.series?.bestOf??1,0,"服务器停止，保存未完成对局。");
                if(replay.frames.Count>0)QueueServerReplayArchive(replay,Task.Run(()=>ReplayRepository.Encode(replay)));
            } catch(Exception error) { Debug.LogError("服务器停止时无法生成录像："+error.Message); }
        }
    }

    private void FlushServerReplayArchives()
    {
        // Archives use only worker threads. Finish writes before a normal stop
        // disposes the rooms or exits the process, independently of network sends.
        foreach(var task in serverReplayWrites){
            try { task.GetAwaiter().GetResult(); }
            catch(Exception error) { Debug.LogError("服务器录像保存失败："+error.Message); }
        }
        serverReplayWrites.Clear();
    }
}
