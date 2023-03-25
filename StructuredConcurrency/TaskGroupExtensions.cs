using Nito.StructuredConcurrency.Internals;
using System.Threading.Channels;

namespace Nito.StructuredConcurrency;

public static class TaskGroupExtensions
{
    public static IAsyncEnumerable<T> RunSequence<T>(this TaskGroup group, Func<CancellationToken, IAsyncEnumerable<T>> work) => RunSequence(group, 1, work);

    public static IAsyncEnumerable<T> RunSequence<T>(this TaskGroup group, int capacity, Func<CancellationToken, IAsyncEnumerable<T>> work)
    {
        var channel = Channel.CreateBounded<T>(capacity);
        group.Run(async ct =>
        {
            try
            {
                await foreach (var item in work(ct).WithCancellation(ct).ConfigureAwait(false))
                {
                    await group.AddResourceAsync(DisposeUtility.TryWrapStandalone(item)).ConfigureAwait(false);
                    await channel.Writer.WriteAsync(item, ct).ConfigureAwait(false);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                channel.Writer.Complete(ex);
                throw;
            }
            finally
            {
                channel.Writer.Complete();
            }
        });
        return channel.Reader.ReadAllAsync();
    }

#pragma warning disable CS1998
    public static Task RunChildGroup(this TaskGroup parentGroup, Action<TaskGroup> work) => RunChildGroup(parentGroup, async g => work(g));
#pragma warning restore CS1998
    public static Task RunChildGroup(this TaskGroup parentGroup, Func<TaskGroup, Task> work)
    {
        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        parentGroup.Run(async ct =>
        {
            try
            {
                var childGroup = new TaskGroup(ct);
                await using (childGroup.ConfigureAwait(false))
                {
                    await work(childGroup).ConfigureAwait(false);
                }
                tcs.TrySetResult();
            }
            catch (Exception ex) // Including OperationCanceledException
            {
                tcs.TrySetException(ex);
                // Child group exceptions do not propagate to the parent.
            }
        });
        return tcs.Task;
    }

    public static void Race<TResult>(this TaskGroup group, RaceResult<TResult> raceResult, Func<CancellationToken, Task<TResult>> work)
    {
        group.Run(async ct =>
        {
            try
            {
                var result = await work(ct).ConfigureAwait(false);
                await raceResult.ReportResultAsync(result).ConfigureAwait(false);
                group.Cancel();
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                raceResult.ReportException(ex);
            }
        });
    }

#pragma warning disable CS1998
    public static Task<TResult> RaceChildGroup<TResult>(this TaskGroup parentGroup, Action<TaskGroup, RaceResult<TResult>> work) => RaceChildGroup<TResult>(parentGroup, async (g, r) => work(g, r));
#pragma warning restore CS1998
    public static Task<TResult> RaceChildGroup<TResult>(this TaskGroup parentGroup, Func<TaskGroup, RaceResult<TResult>, Task> work)
    {
        var tcs = new TaskCompletionSource<TResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        parentGroup.Run(async ct =>
        {
            try
            {
                var raceResult = new RaceResult<TResult>();
                var childGroup = new TaskGroup(ct);
                await using (childGroup.ConfigureAwait(false))
                {
                    await work(childGroup, raceResult).ConfigureAwait(false);
                }

                tcs.TrySetResult(raceResult.GetResult());
            }
            catch (Exception ex) // Including OperationCanceledException
            {
                tcs.TrySetException(ex);
                // Child group exceptions do not propagate to the parent.
            }
        });
        return tcs.Task;
    }
}
