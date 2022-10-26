using System.Net.WebSockets;

var builder = WebApplication.CreateBuilder(args);

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

var webSocketOptions = new WebSocketOptions
{
    KeepAliveInterval = TimeSpan.FromMinutes(2),
};
//webSocketOptions.AllowedOrigins.Add("localhost");

app.UseWebSockets(webSocketOptions);

WebSocket? Listener = null;
app.Use(async (context, next) =>
{
    switch (context.Request.Path)
    {
        case "/display":
            if (context.WebSockets.IsWebSocketRequest)
            {
                using var webSocket = await context.WebSockets.AcceptWebSocketAsync();
                await ReceiveEcho(webSocket);
            }
            else
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
            }
            break;
        case "/controller":
            if (context.WebSockets.IsWebSocketRequest)
            {
                using var webSocket = await context.WebSockets.AcceptWebSocketAsync();
                await SendEcho(webSocket);
            }
            else
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
            }
            break;
        default:
            await next(context);
            break;
    }

});

app.Run();
async Task SendEcho(WebSocket webSocket)
{
    var buffer = new byte[1024 * 4];

    WebSocketReceiveResult receiveResult;

    do
    {
        receiveResult = await webSocket.ReceiveAsync(
            new ArraySegment<byte>(buffer), CancellationToken.None);

        if (Listener != null)
        {
            await Listener.SendAsync(
                new ArraySegment<byte>(buffer, 0, receiveResult.Count),
                receiveResult.MessageType,
                receiveResult.EndOfMessage,
                CancellationToken.None);
        }
    } while (!receiveResult.CloseStatus.HasValue);

    await webSocket.CloseAsync(
        receiveResult.CloseStatus.Value,
        receiveResult.CloseStatusDescription,
        CancellationToken.None);
}
async Task ReceiveEcho(WebSocket webSocket)
{
    var buffer = new byte[1024 * 4];
    Listener = webSocket;
    WebSocketReceiveResult receiveResult;

    do
    {
        receiveResult = await webSocket.ReceiveAsync(
            new ArraySegment<byte>(buffer), CancellationToken.None);
    } while (!receiveResult.CloseStatus.HasValue);

    await webSocket.CloseAsync(
        receiveResult.CloseStatus.Value,
        receiveResult.CloseStatusDescription,
        CancellationToken.None);
}
