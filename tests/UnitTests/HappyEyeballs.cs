using Nito.StructuredConcurrency;
using System.Net;

namespace UnitTests;

// TODO: incomplete.

public sealed class HappyEyeballs
{
    public async Task<IPAddress> ConnectAsync(string hostname)
    {
        await using var group = new TaskGroup();

        return await group.RaceChildGroup<IPAddress>(raceGroup =>
        {
            raceGroup.Run(async ct =>
            {
                var ipAddresses = await Dns.GetHostAddressesAsync(hostname, ct);

                foreach (var ipAddress in ipAddresses)
                {
                    // Attempt
                    raceGroup.Race(raceResult, token => TryConnectAsync(ipAddress, token));
                    await Task.Delay(TimeSpan.FromMilliseconds(300), ct);
                }
            });
        });

        static async Task<IPAddress> TryConnectAsync(IPAddress ipAddress, CancellationToken token)
        {
            await Task.Delay(1000, token);
            return ipAddress;
        }
    }
}
