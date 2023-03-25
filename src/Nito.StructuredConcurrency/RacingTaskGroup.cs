using Nito.StructuredConcurrency.Internals;

namespace Nito.StructuredConcurrency;

/// <summary>
/// A racing task group represents a list of tasks along with a <see cref="CancellationTokenSource"/>. Semantics:
/// <list type="bullet">
/// <item>When the racing task group is asynchronously disposed, it will asynchronously wait for all its child tasks to complete. I.e., there's an implicit `Task.WhenAll` at the end of the racing task group scope.</item>
/// <item>Each child task is provided a <see cref="CancellationToken"/> from this racing task group.</item>
/// <item>All exceptions from child tasks are ignored.</item>
/// <item>If any child task completes successfully, the cancellation token is cancelled. If no child task completes successfully, the racing task group's asynchronous disposal will throw an <see cref="AggregateException"/> containing all of the child task exceptions.</item>
/// <item>Disposing the racing task group does not cancel the racing task group; it just waits for the child tasks.</item>
/// </list>
/// </summary>
/// <typeparam name="TResult">The type of the value that is the result of the race.</typeparam>
public sealed class RacingTaskGroup<TResult> : IAsyncDisposable
{
    private readonly TaskGroup _group;
    private readonly RaceResult<TResult> _raceResult;

    /// <summary>
    /// Creates a racing task group, optionally linking it with an upstream <see cref="CancellationToken"/>.
    /// </summary>
    /// <param name="cancellationToken">The upstream cancellation token.</param>
    public RacingTaskGroup(CancellationToken cancellationToken = default)
    {
        _group = new TaskGroup(cancellationToken);
        _raceResult = new();
    }

    /// <inheritdoc cref="TaskGroup.CancellationTokenSource"/>
    public CancellationTokenSource CancellationTaskSource => _group.CancellationTokenSource;

    /// <summary>
    /// Adds race work to this task group.
    /// Races cancel their task group on success instead of on fault.
    /// Faulting races are ignored.
    /// Results of successful races that do not "win" (i.e., are not the first result) are treated as resources and are immediately disposed.
    /// </summary>
    /// <param name="work">The race work to do.</param>
    public void Race(Func<CancellationToken, Task<TResult>> work)
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
    /// Asynchronously waits for all tasks in this task group to complete.
    /// </summary>
    public ValueTask DisposeAsync() => _group.DisposeAsync();

    /// <summary>
    /// Retrieves the results of this race. This may only be called after this racing task group has been disposed.
    /// If no racers participated at all, then this throws <see cref="OperationCanceledException"/>.
    /// If all racers failed, then the returned task contains all of the racer exceptions, in timeline order.
    /// </summary>
    public TResult GetResult() => _raceResult.GetResult();
}
