using System.Net.WebSockets;
using System.Collections.Concurrent;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

// Enable WebSockets
app.UseWebSockets();

var webSockets = new ConcurrentBag<WebSocket>(); // Stores all connections

app.Use(async (context, next) =>
{
    if (context.Request.Path == "/ws")
    {
        if (context.WebSockets.IsWebSocketRequest)
        {
            var webSocket = await context.WebSockets.AcceptWebSocketAsync();
            webSockets.Add(webSocket);
            await Broadcast(webSocket);
        }
        else
        {
            context.Response.StatusCode = 400;
        }
    }
    else
    {
        await next();
    }
});

app.UseDefaultFiles();
app.UseStaticFiles();

app.Run();

async Task Broadcast(WebSocket webSocket)
{
    var buffer = new byte[1024 * 4];

    try
    {
        while (true)
        {
            var result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);

            if (result.CloseStatus.HasValue)
                break;

            // Broadcast to all open sockets
            foreach (var socket in webSockets)
            {
                if (socket.State == WebSocketState.Open)
                {
                    await socket.SendAsync(new ArraySegment<byte>(buffer, 0, result.Count), result.MessageType, result.EndOfMessage, CancellationToken.None);
                }
            }
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"WebSocket error: {ex.Message}");
    }
    finally
    {
        if (webSocket.State != WebSocketState.Closed)
        {
            await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
        }
    }
}
