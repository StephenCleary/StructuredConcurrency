using Nito.StructuredConcurrency;
using System.Net;

namespace UnitTests;

// TODO: incomplete.

public sealed class HappyEyeballs
{
    public async Task<IPAddress> ConnectAsync(string hostname)
    {
        return await TaskGroup.RunGroupAsync(async group =>
        {
            var ipAddresses = await Dns.GetHostAddressesAsync(hostname, group.CancellationToken);
            return await RacingTaskGroup<IPAddress>.RunGroupAsync(async raceGroup =>
            {
                foreach (var ipAddress in ipAddresses)
                {
                    // Attempt
                    raceGroup.Race(async token => await TryConnectAsync(ipAddress, token));
                    await Task.Delay(TimeSpan.FromMilliseconds(300), raceGroup.CancellationTokenSource.Token);
                }
            });
        }, default);

        static async Task<IPAddress> TryConnectAsync(IPAddress ipAddress, CancellationToken token)
        {
            await Task.Delay(1000, token);
            return ipAddress;
        }
    }
}
