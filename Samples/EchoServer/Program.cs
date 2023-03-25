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
    async IAsyncEnumerable<Socket> Impl()
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
                yield return socket;
        }
    }
});

// echoers
serverGroup.Run(async token =>
{
    await foreach (var socket in sockets)
    {
        serverGroup.RunChildGroup(async connectionGroup =>
        {
            await connectionGroup.AddResourceAsync(socket);
            connectionGroup.Run(async ct =>
            {
                var buffer = new byte[1024];
                while (true)
                {
                    var bytesRead = await socket.ReceiveAsync(buffer, SocketFlags.None, ct);
                    await socket.SendAsync(buffer.AsMemory()[..bytesRead], SocketFlags.None, ct);
                }
            });
        });
    }
});

Console.WriteLine("Listening on port 5000; press Ctrl-C to cancel...");
