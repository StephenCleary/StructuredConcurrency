// See https://aka.ms/new-console-template for more information
using ChatApi;
using ChatApi.Messages;
using ChatServer;
using System.Buffers;
using System.IO.Pipelines;
using System.Net;
using System.Net.Sockets;
using System.Text;

Console.WriteLine("Starting server...");

var connections = new ConnectionCollection();

var listeningSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
listeningSocket.Bind(new IPEndPoint(IPAddress.Any, 33333));
listeningSocket.Listen();

Console.WriteLine("Listening...");

while (true)
{
    var connectedSocket = await listeningSocket.AcceptAsync();
    Console.WriteLine($"Got a connection from {connectedSocket.RemoteEndPoint} to {connectedSocket.LocalEndPoint}.");

    ProcessSocketMainLoop(connectedSocket);
}

async void ProcessSocketMainLoop(Socket socket)
{
    var chatConnection = new ChatConnection(new PipelineSocket(socket));
    var clientConnection = connections.Add(chatConnection);

    try
    {
        await foreach (var message in chatConnection.InputMessages)
        {
            if (message is ChatMessage chatMessage)
            {
                Console.WriteLine($"Got message from {chatConnection.RemoteEndPoint}: {chatMessage.Text}");

                var currentConnections = connections.CurrentConnections;
                var from = clientConnection.Nickname ?? chatConnection.RemoteEndPoint.ToString();
                var broadcastMessage = new BroadcastMessage(from, chatMessage.Text);
                var tasks = currentConnections
                    .Select(x => x.ChatConnection)
                    .Where(x => x != chatConnection)
                    .Select(connection => connection.SendMessageAsync(broadcastMessage));

                try
                {
                    await Task.WhenAll(tasks);
                }
                catch
                {
                    // ignore
                }
            }
            else if (message is SetNicknameRequestMessage setNicknameRequestMessage)
            {
                Console.WriteLine($"Got nickname request message from {chatConnection.RemoteEndPoint}: {setNicknameRequestMessage.Nickname}");

                if (connections.TrySetNickname(chatConnection, setNicknameRequestMessage.Nickname))
                {
                    await chatConnection.SendMessageAsync(new AckResponseMessage(setNicknameRequestMessage.RequestId));
                }
                else
                {
                    await chatConnection.SendMessageAsync(new NakResponseMessage(setNicknameRequestMessage.RequestId, "Nickname already taken."));
                }
            }
            else
                Console.WriteLine($"Got unknown message from {chatConnection.RemoteEndPoint}.");
        }

        Console.WriteLine($"Connection at {chatConnection.RemoteEndPoint} was disconnected.");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Exception from {chatConnection.RemoteEndPoint}: [{ex.GetType().Name}] {ex.Message}");
    }
    finally
    {
        connections.Remove(chatConnection);
    }
}
