using System;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace WebSocketsClient
{
    class Program
    {
        static async Task Main()
        {
            while (true)
            {
                await ConnectToServerAsync();
            }
        }

        static async Task ConnectToServerAsync()
        {
            var serverUri = new Uri("ws://127.0.0.1:8080/");

            while (true)
            {
                var clientWebSocket = new ClientWebSocket();

                try
                {
                    // 連線至伺服器
                    await clientWebSocket.ConnectAsync(serverUri, CancellationToken.None);
                    Console.WriteLine($"WebSocket connected to server: {serverUri}");

                    // 傳送一個 "Hi" 訊息
                    string initialMessage = "Hi";
                    byte[] initialMessageBytes = Encoding.UTF8.GetBytes(initialMessage);
                    await clientWebSocket.SendAsync(new ArraySegment<byte>(initialMessageBytes), WebSocketMessageType.Text, true, CancellationToken.None);

                    // 主通信迴圈
                    while (clientWebSocket.State == WebSocketState.Open)
                    {
                        // 接收伺服器發送的訊息
                        var buffer = new byte[1024];
                        var result = await clientWebSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);

                        if (result.MessageType == WebSocketMessageType.Text)
                        {
                            var receivedMessage = Encoding.UTF8.GetString(buffer, 0, result.Count);
                            Console.WriteLine($"Received message from server: {receivedMessage}");
                        }
                    }
                }
                catch (WebSocketException ex)
                {
                    Console.WriteLine($"WebSocket error: {ex.Message}");

                    // 等待 5 秒再重新嘗試連線
                    await Task.Delay(5000);
                }
                finally
                {
                    // 關閉 WebSocket 連線
                    if (clientWebSocket.State == WebSocketState.Open)
                    {
                        await clientWebSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
                    }
                }
            }
        }
    }

}