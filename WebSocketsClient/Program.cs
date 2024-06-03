using System.Net.WebSockets;
using System.Text;

namespace WebSocketsClient
{
    class Program
    {
        private const string ServerUri = "ws://127.0.0.1:8080/"; // 替換成你的WebSocket伺服器地址
        private const int ReconnectIntervalInSeconds = 5;

        private static async Task Main(string[] args)
        {
            List<Task> clientTasks = new List<Task>();
            int clientConut = 15;

            for (int i = 0; i < clientConut; i++)
            {
                var count = i + 1;
                clientTasks.Add(StartClientAsync(count.ToString()));
            }

            await Task.WhenAll(clientTasks);
        }

        private static async Task StartClientAsync(string strNumber)
        {
            while (true)
            {
                using (var client = new ClientWebSocket())
                {
                    try
                    {
                        client.Options.SetRequestHeader("ClientID", strNumber);
                        client.Options.SetRequestHeader("ClientGroup", (int.Parse(strNumber) / 2).ToString());

                        await client.ConnectAsync(new Uri(ServerUri), CancellationToken.None);

                        Console.WriteLine($"{strNumber} Connected to the server: {ServerUri}");

                        var receiveTask = ReceiveMessage(client, strNumber);
                        //var pingTask = SendPingPeriodically(client, strNumber);
                        //var inputTask = Task.Run(() => HandleConsoleInputAsync(client, strNumber));

                        await Task.WhenAll(receiveTask);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"{strNumber} Error: {ex.Message}");
                    }

                    Console.WriteLine($"{strNumber} Disconnected. Reconnecting in {ReconnectIntervalInSeconds} seconds...");
                    await Task.Delay(TimeSpan.FromSeconds(ReconnectIntervalInSeconds));
                }
            }
        }

        private static async Task ReceiveMessage(ClientWebSocket client, string strNumber)
        {
            var buffer = new byte[1024];

            while (client.State == WebSocketState.Open)
            {
                try
                {
                    var result = await client.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);

                    if (result.MessageType == WebSocketMessageType.Text)
                    {
                        var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                        Console.WriteLine($"{strNumber} Received message: {message}");
                    }
                    else if (result.MessageType == WebSocketMessageType.Close)
                    {
                        await client.CloseAsync(WebSocketCloseStatus.NormalClosure, string.Empty, CancellationToken.None);
                    }
                }
                catch (WebSocketException ex)
                {
                    Console.WriteLine($"{strNumber} WebSocket error: {ex.Message}");
                    break;
                }
            }
        }

        private static async Task SendPingPeriodically(ClientWebSocket client, string strNumber)
        {
            var maxPingResponseTime = TimeSpan.FromSeconds(30);
            var lastPingTime = DateTime.Now;
            int sendCount = 0;

            while (client.State == WebSocketState.Open)
            {
                try
                {
                    lastPingTime = DateTime.Now;

                    // 發送ping
                    await client.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes("ping")), WebSocketMessageType.Text, true, CancellationToken.None);
                    Console.WriteLine($"{strNumber} Sent ping." + sendCount.ToString());
                    sendCount += 1;

                    // 檢查上次ping回來的時間
                    if ((DateTime.Now - lastPingTime) > maxPingResponseTime)
                    {
                        Console.WriteLine($"{strNumber} Ping response time exceeded {maxPingResponseTime.TotalSeconds} seconds. Closing connection.");
                        break;
                    }
                    else
                    {
                        Console.WriteLine($"{strNumber} Ping response time : {(DateTime.Now - lastPingTime).TotalMilliseconds} Milliseconds.");
                    }

                    await Task.Delay(TimeSpan.FromSeconds(5));
                }
                catch (WebSocketException ex)
                {
                    Console.WriteLine($"{strNumber} WebSocket error: {ex.Message}");
                    break;
                }
            }
        }

        private static async Task HandleConsoleInputAsync(ClientWebSocket client, string strNumber)
        {
            while (client.State == WebSocketState.Open)
            {
                try
                {
                    string message = Console.ReadLine();
                    var buffer = Encoding.UTF8.GetBytes(message);

                    // 發送訊息
                    await client.SendAsync(buffer, WebSocketMessageType.Text, true, CancellationToken.None);
                }
                catch (WebSocketException ex)
                {
                    Console.WriteLine($"{strNumber}  WebSocket error: {ex.Message}");
                    break;
                }
            }

        }
    }

}