#if NO
using Nito.StructuredConcurrency;
using System.Net;

namespace UnitTests;

// TODO: incomplete.

public sealed class HappyEyeballs
{
    public async Task<IPAddress> ConnectAsync(string hostname)
    {
        await using var group = new TaskGroup();

        var ipAddresses = await group.Run(async ct => await Dns.GetHostAddressesAsync(hostname, ct));

        return await group.RaceChildGroup<IPAddress>(async raceGroup =>
        {
            foreach (var ipAddress in ipAddresses)
            {
                // Attempt
                raceGroup.Race(async token => await TryConnectAsync(ipAddress, token));
                await Task.Delay(TimeSpan.FromMilliseconds(300), raceGroup.CancellationTaskSource.Token);
            }
        });

        static async Task<IPAddress> TryConnectAsync(IPAddress ipAddress, CancellationToken token)
        {
            await Task.Delay(1000, token);
            return ipAddress;
        }
    }
}
#endif