using Helpers;
using Nito.StructuredConcurrency;
using System.Net;
using System.Net.Sockets;

var applicationExit = ConsoleEx.HookCtrlCCancellation();

var serverGroupTask = TaskGroup.RunAsync(async serverGroup =>
{
    // listener
    var sockets = serverGroup.RunSequence(ct =>
    {
        return Impl();
        async IAsyncEnumerable<GracefulCloseSocket> Impl()
        {
            using var listeningSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            listeningSocket.Bind(new IPEndPoint(IPAddress.Any, 5000));
            listeningSocket.Listen();
            while (true)
            {
                Socket? socket = null;
                try
                {
                    socket = await listeningSocket.AcceptAsync(ct);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    // Ignore accept failures.
                }

                if (socket != null)
                    yield return new() { Socket = socket };
            }
        }
    });

    // echoers
    await foreach (var socket in sockets)
    {
        serverGroup.SpawnChildAsync(async group =>
        {
            await group.AddResourceAsync(socket);
            group.Run(async ct =>
            {
                try
                {
                    var buffer = new byte[1024];
                    while (true)
                    {
                        var bytesRead = await socket.Socket.ReceiveAsync(buffer, SocketFlags.None, ct);
                        await socket.Socket.SendAsync(buffer.AsMemory()[..bytesRead], SocketFlags.None, ct);
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    Console.WriteLine(ex);
                }
            });
        });
    }
}, applicationExit);

Console.WriteLine("Listening on port 5000; press Ctrl-C to cancel...");

await serverGroupTask.IgnoreCancellation();