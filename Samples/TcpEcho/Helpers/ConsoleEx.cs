using System.Runtime.Versioning;
using Windows.Win32;
using Windows.Win32.System.Console;

namespace Helpers;

public static class ConsoleEx
{
    [SupportedOSPlatform("windows6.0.6000")]
    public static string? ReadLine(CancellationToken cancellationToken)
    {
        using var registration = cancellationToken.Register(() => PInvoke.CancelIoEx(PInvoke.GetStdHandle_SafeHandle(STD_HANDLE.STD_INPUT_HANDLE), null));
        return Console.ReadLine();
    }

    public static CancellationToken HookCtrlCCancellation()
    {
        var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
        };
        return cts.Token;
    }
}