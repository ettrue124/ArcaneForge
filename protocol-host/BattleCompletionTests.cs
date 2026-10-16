using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Wotc.Mtgo.Gre.External.Messaging;

static class BattleCompletionTests
{
    public static async Task Run()
    {
        using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var reservation=new TcpListener(IPAddress.Loopback,0);reservation.Start();
        int port=((IPEndPoint)reservation.LocalEndpoint).Port;reservation.Stop();
        using var backend=new HttpListener();
        string url=$"http://127.0.0.1:{port}";backend.Prefixes.Add(url+"/");backend.Start();
        string snapshot="";
        var responder=Task.Run(async()=> {
            try {
                while(!timeout.IsCancellationRequested) {
                    var request=await backend.GetContextAsync().WaitAsync(timeout.Token);
                    string body=request.Request.Url!.AbsolutePath.EndsWith("snapshot") ? snapshot
                        : "{\"events\":[],\"pending\":[],\"closed\":false,\"buttonEventId\":0}";
                    byte[] bytes=Encoding.UTF8.GetBytes(body);request.Response.ContentType="application/json";
                    await request.Response.OutputStream.WriteAsync(bytes,timeout.Token);request.Response.Close();
                }
            } catch(OperationCanceledException) {} catch(HttpListenerException) when(timeout.IsCancellationRequested) {}
        });
        var push=typeof(MatchDoorHost).GetMethod("PushStateAndActions",BindingFlags.Static|BindingFlags.NonPublic)!;
        try {
            foreach(int winner in new[]{1,2,0}) {
                snapshot=JsonSerializer.Serialize(new {matchId="completion-wire",gameStateId=1,msgId=1,viewerSeat=1,
                    turn=new {number=8,phase="COMBAT_DAMAGE",activeSeat=1,prioritySeat=1},
                    players=new[]{new {seat=1,life=11},new {seat=2,life=-10}},cards=Array.Empty<object>(),
                    stage=3,winnerSeat=winner,draw=winner==0,lossReason=winner==2?"Conceded":"LifeReachedZero",
                    input=new {type="InputLockUI"},buttonEventId=0});
                var session=new MatchSessionState();using var wire=new MemoryStream();
                Task Publish()=>(Task)push.Invoke(null,new object?[]{wire,url,null,"completion-wire",(byte)4,session,timeout.Token,null})!;
                await Publish();
                long finalLength=wire.Length;await Publish();
                if(wire.Length!=finalLength || !session.CompletionSent)throw new Exception("Terminal match was republished/not committed");
                wire.Position=0;
                var boardFrame=await Frames.Read(wire,timeout.Token);
                var messages=MatchServiceToClientMessage.Parser.ParseFrom(boardFrame!.Body).GreToClientEvent.GreToClientMessages;
                var board=messages[0].GameStateMessage;
                var intermission=messages.Single(m=>m.Type==GREMessageType.IntermissionReq);
                if(board.GameInfo.Stage!=GameStage.GameOver || board.PendingMessageCount!=messages.Count-1
                    || intermission.IntermissionReq.Result.WinningTeamId!=winner
                    || messages.Any(m=>m.Type==GREMessageType.ActionsAvailableReq))
                    throw new Exception("Final battle batch omitted intermission or retained a playable request");
                var roomFrame=await Frames.Read(wire,timeout.Token);
                var room=MatchServiceToClientMessage.Parser.ParseFrom(roomFrame!.Body).MatchGameRoomStateChangedEvent.GameRoomInfo;
                if(room.StateType!=MatchGameRoomStateType.MatchCompleted || !room.FinalMatchResult.ResultList.SequenceEqual(board.GameInfo.Results)
                    || await Frames.Read(wire,timeout.Token)!=null)throw new Exception("Completion ordering/results/duplicates failed");
            }
        } finally {timeout.Cancel();await responder;backend.Stop();}
        Console.WriteLine("PASS: MatchDoor publishes terminal board/intermission before matching completion, exactly once, without gameplay requests.");
    }
}
