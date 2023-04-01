using Helpers;
using Nito.StructuredConcurrency;
using System.Net;
using System.Net.Sockets;
using System.Text;

var applicationExit = ConsoleEx.HookCtrlCCancellation();

var groupTask = TaskGroup.RunGroupAsync(applicationExit, async group =>
{
    // Create and connect the socket, registering it as a resource owned by the group.
    var clientSocket = new GracefulCloseSocket { Socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp) };
    await group.AddResourceAsync(clientSocket);
    await clientSocket.Socket.ConnectAsync(new IPEndPoint(IPAddress.Loopback, 5000), group.CancellationToken);

    // Start the reader work: Everything that comes in the socket is dumped to the console.
    group.Run(async ct =>
    {
        var buffer = new byte[1024];
        while (true)
        {
            var bytesRead = await clientSocket.Socket.ReceiveAsync(buffer, SocketFlags.None, ct);
            if (bytesRead == 0)
                break;
            Console.WriteLine($"Read: {Encoding.UTF8.GetString(buffer, 0, bytesRead)}");
        }
    });

    // Start the writer work: Everything read from the console is dumped to the socket.
    group.Run(async ct =>
    {
        while (!ct.IsCancellationRequested)
        {
            var input = ConsoleEx.ReadLine(ct);
            if (input == null)
                break;
            var buffer = Encoding.UTF8.GetBytes(input);
            await clientSocket.Socket.SendAsync(buffer, SocketFlags.None, ct);
        }
    });
});

Console.WriteLine("Press Ctrl-C to cancel...");

await groupTask.IgnoreCancellation();