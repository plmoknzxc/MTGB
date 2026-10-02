using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

[Serializable] public sealed class OnlineRoomListing
{
    public string roomId, hostName, format;
    public int players, bestOf;
    public bool inMatch, lanHosted, allowCustomContent;
    public int spectators;
    [NonSerialized] public string address, source, serverId;
    [NonSerialized] public int port;
}
[Serializable] public sealed class OnlineRoomDirectory
{
    public string protocol = OnlineRoomDiscovery.Protocol;
    public string instanceId;
    public OnlineRoomListing[] rooms = Array.Empty<OnlineRoomListing>();
}

// Browsing does not create a Netcode client, occupy a seat, or create rooms.
public static class OnlineRoomDiscovery
{
    public const string Protocol = "MTGB-ROOMS/1";
    public const int LanPort = 47777;
    public static async Task<OnlineRoomDirectory> Query(string address, int port)
    {
        using(var client=new TcpClient()) {
            var connect=client.ConnectAsync(address,port);
            if(await Task.WhenAny(connect,Task.Delay(2000))!=connect)throw new TimeoutException("服务器连接超时，请检查地址和端口。");
            await connect;
            using(var stream=client.GetStream()) {
                var request=Encoding.UTF8.GetBytes(Protocol+"\n");await stream.WriteAsync(request,0,request.Length);
                var result=ReadReply(stream);
                if(await Task.WhenAny(result,Task.Delay(2000))!=result)throw new TimeoutException("服务器没有返回房间列表。");
                var directory=JsonUtility.FromJson<OnlineRoomDirectory>(await result);
                if(directory==null||directory.protocol!=Protocol||directory.rooms==null||directory.rooms.Length>128)throw new IOException("不是兼容的 MTGB 房间服务器。");
                return directory;
            }
        }
    }
    static async Task<string> ReadReply(NetworkStream stream)
    {
        var bytes=new List<byte>();var buffer=new byte[2048];
        while(bytes.Count<131072) {
            int n=await stream.ReadAsync(buffer,0,buffer.Length);if(n==0)break;
            for(int i=0;i<n;i++){if(buffer[i]==10)return Encoding.UTF8.GetString(bytes.ToArray());bytes.Add(buffer[i]);}
        }
        throw new IOException("房间列表响应不完整。");
    }
    public static async Task<List<IPEndPoint>> ScanLan()
    {
        var found=new Dictionary<string,IPEndPoint>();
        using(var udp=new UdpClient(0)) {
            udp.EnableBroadcast=true;string nonce=Guid.NewGuid().ToString("N");
            var data=Encoding.UTF8.GetBytes(Protocol+" "+nonce);
            await udp.SendAsync(data,data.Length,new IPEndPoint(IPAddress.Broadcast,LanPort));
            await udp.SendAsync(data,data.Length,new IPEndPoint(IPAddress.Loopback,LanPort));
            var end=Task.Delay(1000);
            while(found.Count<32) {
                var receive=udp.ReceiveAsync();if(await Task.WhenAny(receive,end)!=receive)break;
                UdpReceiveResult packet;
                try {packet=await receive;}catch(SocketException){if(end.IsCompleted)break;continue;}
                string[] parts=Encoding.UTF8.GetString(packet.Buffer).Split(' ');
                if(parts.Length==3&&parts[0]==Protocol&&parts[1]==nonce&&int.TryParse(parts[2],out int port)&&port>0&&port<=65535) {
                    var endpoint=new IPEndPoint(packet.RemoteEndPoint.Address,port);found[endpoint.ToString()]=endpoint;
                }
            }
        }
        return found.Values.ToList();
    }
}

// Pumped on Unity's main thread: registry snapshots never race with room mutation.
public sealed class OnlineRoomDirectoryServer : IDisposable
{
    public readonly string InstanceId=Guid.NewGuid().ToString("N");
    readonly TcpListener tcp;
    readonly UdpClient udp;
    readonly int port;
    readonly HashSet<TcpClient> clients=new HashSet<TcpClient>();
    public OnlineRoomDirectoryServer(int gamePort)
    {
        port=gamePort;tcp=new TcpListener(IPAddress.Any,port);tcp.Start(16);
        try {
            udp=new UdpClient();udp.Client.SetSocketOption(SocketOptionLevel.Socket,SocketOptionName.ReuseAddress,true);
            udp.Client.Bind(new IPEndPoint(IPAddress.Any,OnlineRoomDiscovery.LanPort));
        } catch(SocketException){udp?.Dispose();udp=null;}
    }
    public void Pump(string snapshot)
    {
        for(int i=0;i<8&&tcp.Pending();i++) {
            var client=tcp.AcceptTcpClient();if(clients.Count>=16){client.Dispose();continue;}
            clients.Add(client);Serve(client,snapshot);
        }
        for(int i=0;udp!=null&&i<8&&udp.Available>0;i++) {
            var peer=new IPEndPoint(IPAddress.Any,0);var bytes=udp.Receive(ref peer);
            if(bytes.Length>80)continue;
            string query=Encoding.UTF8.GetString(bytes);
            if(!query.StartsWith(OnlineRoomDiscovery.Protocol+" ")||query.Length!=OnlineRoomDiscovery.Protocol.Length+33)continue;
            var reply=Encoding.UTF8.GetBytes(query+" "+port);udp.Send(reply,reply.Length,peer);
        }
    }
    async void Serve(TcpClient client,string snapshot)
    {
        try {
            using(client)using(var stream=client.GetStream()) {
                byte[] request=new byte[OnlineRoomDiscovery.Protocol.Length+1];int read=0;
                var timeout=Task.Delay(2000);
                while(read<request.Length) {
                    var task=stream.ReadAsync(request,read,request.Length-read);
                    if(await Task.WhenAny(task,timeout)!=task)return;
                    int n=await task;if(n==0)return;read+=n;
                }
                if(Encoding.UTF8.GetString(request)!=OnlineRoomDiscovery.Protocol+"\n")return;
                var bytes=Encoding.UTF8.GetBytes(snapshot+"\n");
                await Task.WhenAny(stream.WriteAsync(bytes,0,bytes.Length),timeout);
            }
        } catch(Exception e)when(e is IOException||e is SocketException||e is ObjectDisposedException) { }
        finally {clients.Remove(client);}
    }
    public void Dispose(){tcp.Stop();udp?.Dispose();foreach(var client in clients.ToArray())client.Dispose();clients.Clear();}
}
