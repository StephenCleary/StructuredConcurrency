using Nito.StructuredConcurrency;
using System.Net;
using System.Net.Sockets;

namespace UnitTests;

// TODO: incomplete.

public sealed class HappyEyeballs
{
    public async Task<Socket> ConnectAsync(string hostname, CancellationToken cancellationToken = default)
    {
        return await TaskGroup.RunGroupAsync(cancellationToken, async group =>
        {
            var ipAddresses = await GetHostAddressesAsync(hostname, group.CancellationToken);
            return await TaskGroup.RaceGroupAsync<Socket>(group.CancellationToken, async raceGroup =>
            {
                foreach (var ipAddress in ipAddresses)
                {
                    // Attempt
                    raceGroup.Race(async token => await TryConnectAsync(ipAddress, token));
                    await Delay(TimeSpan.FromMilliseconds(300), raceGroup.CancellationTokenSource.Token);
                }
            });
        });
    }

    Func<string, CancellationToken, Task<IPAddress[]>> GetHostAddressesAsync { get; set; } = Dns.GetHostAddressesAsync;
    Func<TimeSpan, CancellationToken, Task> Delay { get; set; } = Task.Delay;
    Func<IPAddress, CancellationToken, Task<Socket>> TryConnectAsync { get; set; } = null!;
}
