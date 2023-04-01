using Helpers;
using Nito.StructuredConcurrency;
using System.Net;
using System.Net.Sockets;

var applicationExit = ConsoleEx.HookCtrlCCancellation();

var serverGroupTask = TaskGroup.RunGroupAsync(applicationExit, async serverGroup =>
{
    // Start the "listener"; this work accepts new socket connections and pushes them to a channel.
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

    // As each new socket comes in, start an independent "echoer".
    await foreach (var socket in sockets)
    {
        serverGroup.Run(async ct =>
        {
            // The try/catch is used to prevent exceptions from bubbling up and canceling the group (and the listenener).
            // I.e., we want each socket connection to fail on its own without affecting the server.
            try
            {
                // Create a linked child group to handle the individual connection.
                // The child group is linked to the parent via the cancellation token, so cancellation will flow down.
                // Normally, exceptions would also flow up, but the try/catch prevents that.
                await TaskGroup.RunGroupAsync(ct, async group =>
                {
                    // Each child group owns its client socket connection.
                    await group.AddResourceAsync(socket);

                    // This simple "echoer" example just has a single bit of work: read followed by write.
                    // A more realistic TCP application would have two workers here: a reader and a writer.
                    var buffer = new byte[1024];
                    while (true)
                    {
                        var bytesRead = await socket.Socket.ReceiveAsync(buffer, SocketFlags.None, ct);
                        await socket.Socket.SendAsync(buffer.AsMemory()[..bytesRead], SocketFlags.None, ct);
                    }
                });
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Do not do a `throw;` here; that would cancel the server as soon as any client faulted.
                Console.WriteLine(ex);
            }
        });
    }
});

Console.WriteLine("Listening on port 5000; press Ctrl-C to cancel...");

await serverGroupTask.IgnoreCancellation();