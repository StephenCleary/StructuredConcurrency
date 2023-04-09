using Nito.StructuredConcurrency.Advanced;

namespace Nito.StructuredConcurrency.Internals;

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
#pragma warning disable CA1062 // Validate arguments of public methods

public static class TaskGroupExtensions
{
    public static void Run(this TaskGroupCore group, Func<CancellationToken, ValueTask> work) => _ = RunAsync(group, work.WithResult());

    public static Task<T> RunAsync<T>(this TaskGroupCore group, Func<CancellationToken, ValueTask<T>> work)
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

    public static void Race<T>(this TaskGroupCore group, RaceResult<T> raceResult, Func<CancellationToken, ValueTask<T>> work)
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

    public static Task<T> RunAsync<T>(this RunTaskGroup group, Func<CancellationToken, ValueTask<T>> work) => group.DoRunAsync(work);
}
