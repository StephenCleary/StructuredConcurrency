using Nito.StructuredConcurrency.Internals;

namespace Nito.StructuredConcurrency;

/// <summary>
/// TODO
/// </summary>
/// <typeparam name="TResult"></typeparam>
public sealed class RacingTaskGroup<TResult> : IAsyncDisposable
{
    private readonly TaskGroup _group;
    private readonly RaceResult<TResult> _raceResult;

    /// <summary>
    /// TODO
    /// </summary>
    /// <param name="cancellationToken"></param>
    public RacingTaskGroup(CancellationToken cancellationToken = default)
    {
        _group = new TaskGroup(cancellationToken);
        _raceResult = new();
    }

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
    /// TODO
    /// </summary>
    /// <returns></returns>
    public ValueTask DisposeAsync() => _group.DisposeAsync();

    /// <summary>
    /// TODO
    /// </summary>
    /// <returns></returns>
    public TResult GetResult() => _raceResult.GetResult();
}
