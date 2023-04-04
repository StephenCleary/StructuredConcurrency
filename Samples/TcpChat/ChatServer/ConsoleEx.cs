namespace ChatServer;

public static class ConsoleEx
{
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