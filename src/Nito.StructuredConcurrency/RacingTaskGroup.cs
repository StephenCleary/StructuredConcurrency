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
public sealed class RacingTaskGroup<TResult> : IAsyncDisposable
{
    private readonly WorkTaskGroup _group;
    private readonly RaceResult<TResult> _raceResult;

    /// <summary>
    /// Creates a racing task group.
    /// </summary>
    internal RacingTaskGroup(WorkTaskGroup group, RaceResult<TResult> raceResult)
    {
        _group = group;
        _raceResult = raceResult;
    }

    /// <inheritdoc cref="TaskGroup.CancellationToken"/>
    public CancellationToken CancellationToken => CancellationTokenSource.Token;

    /// <inheritdoc cref="TaskGroup.CancellationTokenSource"/>
    public CancellationTokenSource CancellationTokenSource => _group.CancellationTokenSource;

    /// <inheritdoc cref="TaskGroup.AddResourceAsync"/>
    public ValueTask AddResourceAsync(object? resource) => _group.AddResourceAsync(resource);

    /// <inheritdoc cref="TaskGroup.DisposeAsync"/>
    public ValueTask DisposeAsync() => _group.DisposeAsync();

    /// <summary>
    /// Adds race work to this task group.
    /// Races cancel their task group on success instead of on fault.
    /// Faulting races are ignored.
    /// Results of successful races that do not "win" (i.e., are not the first result) are treated as resources and are immediately disposed.
    /// </summary>
    /// <param name="work">The race work to do.</param>
    public void Race(Func<CancellationToken, ValueTask<TResult>> work)
    {
        _ = _group.WorkAsync(async ct =>
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
}
