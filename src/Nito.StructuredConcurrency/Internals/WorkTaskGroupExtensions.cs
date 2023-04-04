using Nito.StructuredConcurrency.Advanced;

namespace Nito.StructuredConcurrency.Internals;

internal static class WorkTaskGroupExtensions
{
    public static void Run(this WorkTaskGroup group, Func<CancellationToken, ValueTask> work) => _ = RunAsync(group, work.WithResult());

    public static Task<T> RunAsync<T>(this WorkTaskGroup group, Func<CancellationToken, ValueTask<T>> work)
    {
        return group.WorkAsync(CancelOnException(group.CancellationTokenSource, work));

        static Func<CancellationToken, ValueTask<T>> CancelOnException(CancellationTokenSource cancellationTokenSource, Func< CancellationToken, ValueTask<T>> work) =>
            async cancellationToken =>
            {
                try
                {
                    return await work(cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    cancellationTokenSource.Cancel();
                    throw;
                }
            };
    }

    public static void Race<T>(this WorkTaskGroup group, RaceResult<T> raceResult, Func<CancellationToken, ValueTask<T>> work)
    {
        _ = group.WorkAsync(async ct =>
        {
            try
            {
                var result = await work(ct).ConfigureAwait(false);
                await raceResult.ReportResultAsync(result).ConfigureAwait(false);
                group.CancellationTokenSource.Cancel();
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                raceResult.ReportException(ex);
            }
        });
    }
}
