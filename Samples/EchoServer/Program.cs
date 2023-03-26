using Helpers;
using Nito.StructuredConcurrency;
using System.Net;
using System.Net.Sockets;

var applicationExit = ConsoleEx.HookCtrlCCancellation();
await using var serverGroup = new TaskGroup(applicationExit);

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
serverGroup.Run(async token =>
{
    await foreach (var socket in sockets)
    {
        serverGroup.Run(async parentToken =>
        {
            await using var group = new TaskGroup(parentToken);
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
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                }
            });
        });
    }
});

Console.WriteLine("Listening on port 5000; press Ctrl-C to cancel...");
