using Nito.StructuredConcurrency.Advanced;

namespace Nito.StructuredConcurrency;

/// <summary>
/// A racing task group represents a list of tasks along with a <see cref="System.Threading.CancellationTokenSource"/>. Semantics:
/// <list type="bullet">
/// <item>Each child task is provided a <see cref="CancellationToken"/> from this racing task group.</item>
/// <item>All exceptions from child tasks are ignored.</item>
/// <item>If any child task completes successfully, the cancellation token is cancelled. If no child task completes successfully, the racing task group's asynchronous disposal will throw an <see cref="AggregateException"/> containing all of the child task exceptions.</item>
/// </list>
/// </summary>
/// <typeparam name="TResult">The type of the value that is the result of the race.</typeparam>
public sealed class RacingTaskGroup<TResult>
{
    private readonly TaskGroup _group;
    private readonly RaceResult<TResult> _raceResult;

    /// <summary>
    /// Creates a racing task group.
    /// </summary>
    internal RacingTaskGroup(TaskGroup group, RaceResult<TResult> raceResult)
    {
        _group = group;
        _raceResult = raceResult;
    }

    /// <summary>
    /// Gets they underlying task group. This can be used to run other tasks or spawn child groups.
    /// </summary>
    public TaskGroup TaskGroup => _group;

    /// <inheritdoc cref="TaskGroup.CancellationToken"/>
    public CancellationToken CancellationToken => CancellationTokenSource.Token;

    /// <inheritdoc cref="TaskGroup.CancellationTokenSource"/>
    public CancellationTokenSource CancellationTokenSource => _group.CancellationTokenSource;

    /// <inheritdoc cref="TaskGroup.AddResourceAsync"/>
    public ValueTask AddResourceAsync(object? resource) => _group.AddResourceAsync(resource);

    /// <summary>
    /// Adds race work to this task group.
    /// Races cancel their task group on success instead of on fault.
    /// Faulting races are ignored.
    /// Results of successful races that do not "win" (i.e., are not the first result) are treated as resources and are immediately disposed.
    /// </summary>
    /// <param name="work">The race work to do.</param>
    public void Race(Func<CancellationToken, ValueTask<TResult>> work)
    {
        _group.Run(async ct =>
        {
            try
            {
                var result = await work(ct).ConfigureAwait(false);
                await _raceResult.ReportResultAsync(result).ConfigureAwait(false);
                _group.CancellationTokenSource.Cancel();
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _raceResult.ReportException(ex);
            }
        });
    }

    /// <summary>
    /// Creates a new <see cref="RacingTaskGroup{TResult}"/> and runs the specified work as the first work task.
    /// </summary>
    /// <param name="cancellationToken">An upstream cancellation token for the task group.</param>
    /// <param name="work">The first work task of the task group.</param>
#pragma warning disable CA1000 // Do not declare static members on generic types
    public static async Task<TResult> RunAsync(Func<RacingTaskGroup<TResult>, ValueTask> work, CancellationToken cancellationToken = default)
#pragma warning restore CA1000 // Do not declare static members on generic types
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
    /// <param name="cancellationToken">An upstream cancellation token for the task group.</param>
    /// <param name="work">The first work task of the task group.</param>
#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
#pragma warning disable CA1000 // Do not declare static members on generic types
    public static Task<TResult> RunAsync(Action<RacingTaskGroup<TResult>> work, CancellationToken cancellationToken = default) =>
        RunAsync(async g => work(g), cancellationToken);
#pragma warning restore CA1000 // Do not declare static members on generic types
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously

}
