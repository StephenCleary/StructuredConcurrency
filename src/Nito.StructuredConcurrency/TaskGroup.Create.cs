using Nito.StructuredConcurrency.Advanced;
using Nito.StructuredConcurrency.Internals;

namespace Nito.StructuredConcurrency;

public sealed partial class TaskGroup
{
    /// <summary>
    /// Creates a new <see cref="TaskGroup"/> and runs the specified work as the first work task.
    /// </summary>
    /// <typeparam name="T">The type of the result of the task.</typeparam>
    /// <param name="cancellationToken">An upstream cancellation token for the task group.</param>
    /// <param name="work">The first work task of the task group.</param>
    public static async Task<T> RunGroupAsync<T>(Func<TaskGroup, ValueTask<T>> work, CancellationToken cancellationToken)
    {
        await using var group = new TaskGroup(cancellationToken);
        return await group.RunAsync(_ => work(group)).ConfigureAwait(false);
    }

    /// <summary>
    /// Creates a new <see cref="TaskGroup"/> and runs the specified work as the first work task.
    /// </summary>
    /// <typeparam name="T">The type of the result of the task.</typeparam>
    /// <param name="cancellationToken">An upstream cancellation token for the task group.</param>
    /// <param name="work">The first work task of the task group.</param>
    public static Task<T> RunGroupAsync<T>(Func<TaskGroup, T> work, CancellationToken cancellationToken) =>
        RunGroupAsync(work.AsAsync(), cancellationToken);

    /// <summary>
    /// Creates a new <see cref="TaskGroup"/> and runs the specified work as the first work task.
    /// </summary>
    /// <param name="cancellationToken">An upstream cancellation token for the task group.</param>
    /// <param name="work">The first work task of the task group.</param>
    public static Task RunGroupAsync(Func<TaskGroup, ValueTask> work, CancellationToken cancellationToken) =>
        RunGroupAsync(work.WithResult(), cancellationToken);

    /// <summary>
    /// Creates a new <see cref="TaskGroup"/> and runs the specified work as the first work task.
    /// </summary>
    /// <param name="cancellationToken">An upstream cancellation token for the task group.</param>
    /// <param name="work">The first work task of the task group.</param>
    public static Task RunGroupAsync(Action<TaskGroup> work, CancellationToken cancellationToken) =>
        RunGroupAsync(work.AsAsync().WithResult(), cancellationToken);

    /// <summary>
    /// Creates a new <see cref="RacingTaskGroup{TResult}"/> and runs the specified work as the first work task.
    /// </summary>
    /// <param name="cancellationToken">An upstream cancellation token for the task group.</param>
    /// <param name="work">The first work task of the task group.</param>
    public static async Task<T> RaceGroupAsync<T>(Func<RacingTaskGroup<T>, ValueTask> work, CancellationToken cancellationToken = default)
    {
        var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        var raceResult = new RaceResult<T>();
        await RunGroupAsync(async group =>
        {
            var raceGroup = new RacingTaskGroup<T>(group, raceResult);
            await work(raceGroup).ConfigureAwait(false);
        }, cancellationToken).ConfigureAwait(false);
        return raceResult.GetResult();
    }

    /// <summary>
    /// Creates a new <see cref="RacingTaskGroup{TResult}"/> and runs the specified work as the first work task.
    /// </summary>
    /// <param name="cancellationToken">An upstream cancellation token for the task group.</param>
    /// <param name="work">The first work task of the task group.</param>
    public static Task<T> RaceGroupAsync<T>(Action<RacingTaskGroup<T>> work, CancellationToken cancellationToken = default) =>
        RaceGroupAsync<T>(work.AsAsync(), cancellationToken);
}
