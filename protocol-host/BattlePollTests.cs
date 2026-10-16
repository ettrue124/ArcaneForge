using System.Net;
using System.Net.Sockets;
using System.Text;

static class BattlePollTests
{
    public static async Task Run()
    {
        using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(8));
        var socket=new TcpListener(IPAddress.Loopback,0);socket.Start();
        int port=((IPEndPoint)socket.LocalEndpoint).Port;socket.Stop();
        using var backend=new HttpListener();string url=$"http://127.0.0.1:{port}";
        backend.Prefixes.Add(url+"/");backend.Start();int polls=0,actions=0;
        var responder=Task.Run(async()=> {
            try {
                while(!timeout.IsCancellationRequested) {
                    var req=await backend.GetContextAsync().WaitAsync(timeout.Token);
                    string path=req.Request.Url!.AbsolutePath,body="{}";
                    if(path.EndsWith("poll")) {
                        if(++polls==1)req.Response.StatusCode=503;
                        else body="{\"events\":[],\"pending\":[],\"closed\":false,\"buttonEventId\":1}";
                    } else if(path.EndsWith("snapshot")) body="""
                        {"viewerSeat":1,"turn":{"number":5,"phase":"MAIN1","activeSeat":1,"prioritySeat":1},"players":[{"seat":1,"life":40},{"seat":2,"life":40}],"cards":[],"stack":[],"input":{"type":"InputPassPriority"},"legalActions":[],"buttonEventId":1}
                        """;
                    else if(path.EndsWith("action"))actions++;
                    byte[] bytes=Encoding.UTF8.GetBytes(body);await req.Response.OutputStream.WriteAsync(bytes);req.Response.Close();
                    if(actions>0)timeout.Cancel();
                }
            } catch(OperationCanceledException) {} catch(HttpListenerException) when(timeout.IsCancellationRequested) {}
        });
        using var wire=new MemoryStream();
        try {
            await MatchDoorHost.PollBattle(wire,url,null,"poll-recovery",4,new MatchSessionState(),timeout.Token);
            if(polls<2 || actions!=1 || wire.Length==0)throw new Exception("Temporary poll error stranded the board or automatic priority pass");
        } finally {timeout.Cancel();backend.Stop();await responder;}
        Console.WriteLine("PASS: battle updates recover from HTTP 503 and resume automatic passing even after drained events.");
    }
}
