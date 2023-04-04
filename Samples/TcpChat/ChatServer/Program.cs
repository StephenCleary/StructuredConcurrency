// See https://aka.ms/new-console-template for more information
using ChatApi;
using ChatApi.Messages;
using ChatServer;
using Nito.StructuredConcurrency;
using System.Net;
using System.Net.Sockets;

Console.WriteLine("Starting server...");

var connections = new ConnectionCollection();

var applicationExit = ConsoleEx.HookCtrlCCancellation();

var serverGroupTask = TaskGroup.RunGroupAsync(applicationExit, async serverGroup =>
{
    // Define the "listener"; this work accepts new socket connections.
    async IAsyncEnumerable<Socket> ListenAsync()
    {
        using var listeningSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        listeningSocket.Bind(new IPEndPoint(IPAddress.Any, 33333));
        listeningSocket.Listen();

        Console.WriteLine("Listening...");

        while (true)
        {
            Socket? connectedSocket = null;
            try
            {
                connectedSocket = await listeningSocket.AcceptAsync(serverGroup.CancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Ignore accept failures.
                continue;
            }

            Console.WriteLine($"Got a connection from {connectedSocket.RemoteEndPoint} to {connectedSocket.LocalEndPoint}.");
            yield return connectedSocket;
        }
    }

    await foreach (var socket in ListenAsync())
    {
        serverGroup.Run(async ct =>
        {
            ChatConnection? chatConnection = null;
            try
            {
                await TaskGroup.RunGroupAsync(ct, async group =>
                {
                    chatConnection = new ChatConnection(group, new PipelineSocket(group, socket));
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
                    finally
                    {
                        group.CancellationTokenSource.Cancel();
                        connections.Remove(chatConnection);
                    }
                });
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Do not do a `throw;` here; that would cancel the server as soon as any client faulted.
                Console.WriteLine($"Exception from {chatConnection!.RemoteEndPoint}: [{ex.GetType().Name}] {ex.Message}");
            }
        });
    }
});

await serverGroupTask.IgnoreCancellation();