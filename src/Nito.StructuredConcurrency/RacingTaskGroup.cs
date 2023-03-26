using Nito.StructuredConcurrency.Internals;

namespace Nito.StructuredConcurrency;

/// <summary>
/// A racing task group represents a list of tasks along with a <see cref="CancellationTokenSource"/>. Semantics:
/// <list type="bullet">
/// <item>Each child task is provided a <see cref="CancellationToken"/> from this racing task group.</item>
/// <item>All exceptions from child tasks are ignored.</item>
/// <item>If any child task completes successfully, the cancellation token is cancelled. If no child task completes successfully, the racing task group's asynchronous disposal will throw an <see cref="AggregateException"/> containing all of the child task exceptions.</item>
/// </list>
/// </summary>
public static class RacingTaskGroup
{
    /// <summary>
    /// Creates a new <see cref="RacingTaskGroup{TResult}"/> and runs the specified work as the first work task.
    /// </summary>
    /// <typeparam name="TResult">The type of the value that is the result of the race.</typeparam>
    /// <param name="cancellationToken">An upstream cancellation token for the task group.</param>
    /// <param name="work">The first work task of the task group.</param>
    public static async Task<TResult> RunAsync<TResult>(Func<RacingTaskGroup<TResult>, ValueTask> work, CancellationToken cancellationToken = default)
    {
        var tcs = new TaskCompletionSource<TResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var raceResult = new RaceResult<TResult>();
        await TaskGroup.RunAsync(async group =>
        {
            var raceGroup = new RacingTaskGroup<TResult>(group, raceResult);
            await work(raceGroup).ConfigureAwait(false);
        }, cancellationToken).ConfigureAwait(false);
        return raceResult.GetResult();
    }

    /// <summary>
    /// Creates a new <see cref="RacingTaskGroup{TResult}"/> and runs the specified work as the first work task.
    /// </summary>
    /// <typeparam name="TResult">The type of the value that is the result of the race.</typeparam>
    /// <param name="cancellationToken">An upstream cancellation token for the task group.</param>
    /// <param name="work">The first work task of the task group.</param>
#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
    public static Task<TResult> RunAsync<TResult>(Action<RacingTaskGroup<TResult>> work, CancellationToken cancellationToken = default) =>
        RunAsync<TResult>(async g => work(g), cancellationToken);
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
}
