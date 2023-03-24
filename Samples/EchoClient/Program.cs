using Helpers;
using Nito.StructuredConcurrency;
using System.Net;
using System.Net.Sockets;
using System.Text;

var applicationExit = ConsoleEx.HookCtrlCCancellation();
using var clientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
await using var group = new TaskGroup(applicationExit);

group.Run(async ct =>
{
    await clientSocket.ConnectAsync(new IPEndPoint(IPAddress.Loopback, 5000), ct);
    
    group.Run(async ct =>
    {
        var buffer = new byte[1024];
        while (true)
        {
            var bytesRead = await clientSocket.ReceiveAsync(buffer, SocketFlags.None, ct);
            if (bytesRead == 0)
                break;
            Console.WriteLine($"Read: {Encoding.UTF8.GetString(buffer, 0, bytesRead)}");
        }
    });

    group.Run(async ct =>
    {
        while (!ct.IsCancellationRequested)
        {
            var input = ConsoleEx.ReadLine(ct);
            if (input == null)
                break;
            var buffer = Encoding.UTF8.GetBytes(input);
            await clientSocket.SendAsync(buffer, SocketFlags.None, ct);
        }
    });
});

Console.WriteLine("Press Ctrl-C to cancel...");
