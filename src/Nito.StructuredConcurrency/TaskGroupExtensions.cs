using Nito.StructuredConcurrency.Internals;
using System.Threading.Channels;

namespace Nito.StructuredConcurrency;

/// <summary>
/// Provides additional methods for <see cref="TaskGroup"/>.
/// </summary>
public static class TaskGroupExtensions
{
    /// <summary>
    /// Starts a child task group.
    /// Child task groups honor cancellation from their parent task group, but they do not pass exceptions up to their parent.
    /// </summary>
    /// <param name="parentGroup">The parent task group.</param>
    /// <param name="work">The work do be done, using the child task group. There is no need to place the child task group in an <c>await using</c> block.</param>
    /// <returns>A task that completes when the child task group has completed. This task will be faulted if the child task group faults.</returns>
#pragma warning disable CS1998
    public static void SpawnChildGroup(this TaskGroup parentGroup, Action<TaskGroup> work) => SpawnChildGroup(parentGroup, async g => work(g));
#pragma warning restore CS1998

    /// <summary>
    /// Starts a child task group.
    /// Child task groups honor cancellation from their parent task group, but they do not pass exceptions up to their parent.
    /// </summary>
    /// <param name="parentGroup">The parent task group.</param>
    /// <param name="work">The work do be done, using the child task group. There is no need to place the child task group in an <c>await using</c> block.</param>
    /// <returns>A task that completes when the child task group has completed. This task will be faulted if the child task group faults.</returns>
    public static void SpawnChildGroup(this TaskGroup parentGroup, Func<TaskGroup, Task> work)
    {
        _ = parentGroup ?? throw new ArgumentNullException(nameof(parentGroup));

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
            }
            catch // Including OperationCanceledException
            {
                // Child group exceptions do not propagate to the parent.
            }
#pragma warning restore CA1031 // Do not catch general exception types
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
    public static Task<TResult> RaceChildGroup<TResult>(this TaskGroup parentGroup, Action<RacingTaskGroup<TResult>> work) => RaceChildGroup<TResult>(parentGroup, async (g) => work(g));
#pragma warning restore CS1998

    /// <summary>
    /// Starts a racing child task group.
    /// Child task groups honor cancellation from their parent task group, but they do not pass exceptions up to their parent.
    /// </summary>
    /// <typeparam name="TResult">The result type of the race work.</typeparam>
    /// <param name="parentGroup">The parent task group.</param>
    /// <param name="work">The work do be done, using the racing child task group. There is no need to place the racing child task group in an <c>await using</c> block.</param>
    /// <returns>The result of the race. This task will be faulted if the all races fault.</returns>
    public static Task<TResult> RaceChildGroup<TResult>(this TaskGroup parentGroup, Func<RacingTaskGroup<TResult>, Task> work)
    {
        _ = parentGroup ?? throw new ArgumentNullException(nameof(parentGroup));

        var tcs = new TaskCompletionSource<TResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        parentGroup.Run(async ct =>
        {
#pragma warning disable CA1031 // Do not catch general exception types
            try
            {
                var raceGroup = new RacingTaskGroup<TResult>(ct);
                await using (raceGroup.ConfigureAwait(false))
                {
                    await work(raceGroup).ConfigureAwait(false);
                }

                tcs.TrySetResult(raceGroup.GetResult());
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
