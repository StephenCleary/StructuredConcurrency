using Nito.StructuredConcurrency.Internals;
using System.Threading.Channels;

namespace Nito.StructuredConcurrency;

/// <summary>
/// Provides additional methods for <see cref="TaskGroup"/>.
/// </summary>
public static class TaskGroupExtensions
{
    /// <summary>
    /// Executes work that produces a sequence.
    /// This sequence cannot be consumed outside the scope of the task group.
    /// Each item produced by this sequence is a resource owned by this task group.
    /// </summary>
    /// <typeparam name="T">The type of items produced.</typeparam>
    /// <param name="group">The task group.</param>
    /// <param name="work">The work to perform, producing a sequence of items.</param>
    public static IAsyncEnumerable<T> RunSequence<T>(this TaskGroup group, Func<CancellationToken, IAsyncEnumerable<T>> work) => RunSequence(group, 1, work);

    /// <summary>
    /// Executes work that produces a sequence.
    /// This sequence cannot be consumed outside the scope of the task group.
    /// Each item produced by this sequence is a resource owned by this task group.
    /// </summary>
    /// <typeparam name="T">The type of items produced.</typeparam>
    /// <param name="group">The task group.</param>
    /// <param name="capacity">The capacity of the channel containing the results of this work. Defaults to <c>1</c>.</param>
    /// <param name="work">The work to perform, producing a sequence of items.</param>
    public static IAsyncEnumerable<T> RunSequence<T>(this TaskGroup group, int capacity, Func<CancellationToken, IAsyncEnumerable<T>> work)
    {
        _ = group ?? throw new ArgumentNullException(nameof(group));

        var channel = Channel.CreateBounded<T>(capacity);
        group.Run(async ct =>
        {
            try
            {
                await foreach (var item in work(ct).WithCancellation(ct).ConfigureAwait(false))
                {
                    await group.AddResourceAsync(DisposeUtility.TryWrap(item)).ConfigureAwait(false);
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

    /// <summary>
    /// Starts a child task group. This is an advanced operation.
    /// Child task groups honor cancellation from their parent task group, but they do not pass exceptions up to their parent.
    /// </summary>
    /// <param name="parentGroup">The parent task group.</param>
    /// <param name="work">The work do be done, using the child task group. There is no need to place the child task group in an <c>await using</c> block.</param>
    /// <returns>A task that completes when the child task group has completed. This task will be faulted if the child task group faults.</returns>
#pragma warning disable CS1998
    public static Task RunChildGroup(this TaskGroup parentGroup, Action<TaskGroup> work) => RunChildGroup(parentGroup, async g => work(g));
#pragma warning restore CS1998

    /// <summary>
    /// Starts a child task group. This is an advanced operation.
    /// Child task groups honor cancellation from their parent task group, but they do not pass exceptions up to their parent.
    /// </summary>
    /// <param name="parentGroup">The parent task group.</param>
    /// <param name="work">The work do be done, using the child task group. There is no need to place the child task group in an <c>await using</c> block.</param>
    /// <returns>A task that completes when the child task group has completed. This task will be faulted if the child task group faults.</returns>
    public static Task RunChildGroup(this TaskGroup parentGroup, Func<TaskGroup, Task> work)
    {
        _ = parentGroup ?? throw new ArgumentNullException(nameof(parentGroup));

        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        parentGroup.Run(async ct =>
        {
#pragma warning disable CA1031 // Do not catch general exception types
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
#pragma warning restore CA1031 // Do not catch general exception types
        });
        return tcs.Task;
    }

    /// <summary>
    /// Adds race work to this task group.
    /// Races cancel their task group on success instead of on fault.
    /// Faulting races are ignored.
    /// Results of successful races that do not "win" (i.e., are not the first result) are treated as resources and are immediately disposed.
    /// </summary>
    /// <typeparam name="TResult">The result type of the race work.</typeparam>
    /// <param name="group">The task group.</param>
    /// <param name="raceResult">The race result for that task group.</param>
    /// <param name="work">The race work to do.</param>
    public static void Race<TResult>(this TaskGroup group, RaceResult<TResult> raceResult, Func<CancellationToken, Task<TResult>> work)
    {
        _ = group ?? throw new ArgumentNullException(nameof(group));

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

    /// <summary>
    /// Starts a racing child task group.
    /// Child task groups honor cancellation from their parent task group, but they do not pass exceptions up to their parent.
    /// </summary>
    /// <typeparam name="TResult">The result type of the race work.</typeparam>
    /// <param name="parentGroup">The parent task group.</param>
    /// <param name="work">The work do be done, using the racing child task group. There is no need to place the racing child task group in an <c>await using</c> block.</param>
    /// <returns>The result of the race. This task will be faulted if the all races fault.</returns>
#pragma warning disable CS1998
    public static Task<TResult> RaceChildGroup<TResult>(this TaskGroup parentGroup, Action<TaskGroup, RaceResult<TResult>> work) => RaceChildGroup<TResult>(parentGroup, async (g, r) => work(g, r));
#pragma warning restore CS1998

    /// <summary>
    /// Starts a racing child task group.
    /// Child task groups honor cancellation from their parent task group, but they do not pass exceptions up to their parent.
    /// </summary>
    /// <typeparam name="TResult">The result type of the race work.</typeparam>
    /// <param name="parentGroup">The parent task group.</param>
    /// <param name="work">The work do be done, using the racing child task group. There is no need to place the racing child task group in an <c>await using</c> block.</param>
    /// <returns>The result of the race. This task will be faulted if the all races fault.</returns>
    public static Task<TResult> RaceChildGroup<TResult>(this TaskGroup parentGroup, Func<TaskGroup, RaceResult<TResult>, Task> work)
    {
        _ = parentGroup ?? throw new ArgumentNullException(nameof(parentGroup));

        var tcs = new TaskCompletionSource<TResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        parentGroup.Run(async ct =>
        {
#pragma warning disable CA1031 // Do not catch general exception types
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
#pragma warning restore CA1031 // Do not catch general exception types
        });
        return tcs.Task;
    }
}
