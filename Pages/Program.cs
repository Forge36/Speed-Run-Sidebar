using System.Net.WebSockets;
using System.Text;

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
        var message = new ArraySegment<byte>(buffer, 0, receiveResult.Count);
        var asString = Encoding.Default.GetString(message);

        if (asString.StartsWith("{"))
        {
            if (Listener != null)
            {
                await Listener.SendAsync(
                    message,
                    receiveResult.MessageType,
                    receiveResult.EndOfMessage,
                    CancellationToken.None);
            }
        }
        else if (asString == "loadData")
        {
            // In practice this can probably be cached to once per week/month
            // and a manual "update time" button could avoid any extra calls

            using var sr = new StreamReader("SampleResponse.json");
            var response = await sr.ReadToEndAsync();
            await webSocket.SendAsync(
                    Encoding.Default.GetBytes(response),
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
