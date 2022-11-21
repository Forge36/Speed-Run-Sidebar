using System.Net.WebSockets;
using System.Text;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

WebApplication app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

WebSocketOptions webSocketOptions = new()
{
    KeepAliveInterval = TimeSpan.FromMinutes(2),
};

app.UseWebSockets(webSocketOptions);

WebSocket? Listener = null;
app.Use(async (context, next) =>
{
    switch (context.Request.Path)
    {
        case "/display":
            if (context.WebSockets.IsWebSocketRequest)
            {
                using WebSocket webSocket = await context.WebSockets.AcceptWebSocketAsync();
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
                using WebSocket webSocket = await context.WebSockets.AcceptWebSocketAsync();
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
    byte[] buffer = new byte[1024 * 4];

    WebSocketReceiveResult receiveResult;

    do
    {
        receiveResult = await webSocket.ReceiveAsync(
            new ArraySegment<byte>(buffer), CancellationToken.None);
        ArraySegment<byte> message = new(buffer, 0, receiveResult.Count);
        string asString = Encoding.Default.GetString(message);

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

            using StreamReader sr = new("CachedResponse.json");
            string response = await sr.ReadToEndAsync();
            await webSocket.SendAsync(
                    Encoding.Default.GetBytes(response),
                    receiveResult.MessageType,
                    receiveResult.EndOfMessage,
                    CancellationToken.None);
        }
        else if (asString.StartsWith("updatedata/"))
        {
            int id = int.Parse(asString.Substring(asString.IndexOf('/') + 1));
            // In practice this can probably be cached to once per week/month
            // and a manual "update time" button could avoid any extra calls
            using HttpClient client = new();
            using HttpResponseMessage ranks = await client.GetAsync($"https://api.mariokart64.com/players/{id}/ranks");
            using StreamReader data = new(await ranks.Content.ReadAsStreamAsync());
            string json = await data.ReadToEndAsync();
            using StreamWriter sr = new("CachedResponse.json");
            await sr.WriteAsync(json);
            await webSocket.SendAsync(
                    Encoding.Default.GetBytes(json),
                    receiveResult.MessageType,
                    receiveResult.EndOfMessage,
                    CancellationToken.None);
        }
    }
    while (!receiveResult.CloseStatus.HasValue);

    await webSocket.CloseAsync(
        receiveResult.CloseStatus.Value,
        receiveResult.CloseStatusDescription,
        CancellationToken.None);
}

async Task ReceiveEcho(WebSocket webSocket)
{
    byte[] buffer = new byte[1024 * 4];
    Listener = webSocket;
    WebSocketReceiveResult receiveResult;

    do
    {
        receiveResult = await webSocket.ReceiveAsync(
            new ArraySegment<byte>(buffer), CancellationToken.None);
    }
    while (!receiveResult.CloseStatus.HasValue);

    await webSocket.CloseAsync(
        receiveResult.CloseStatus.Value,
        receiveResult.CloseStatusDescription,
        CancellationToken.None);
}
