using Nito.StructuredConcurrency.Internals;
using System.ComponentModel;

namespace Nito.StructuredConcurrency;

/// <summary>
/// A racing task group represents a list of tasks along with a <see cref="CancellationTokenSource"/>. Semantics:
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
    [EditorBrowsable(EditorBrowsableState.Never)]
    public RacingTaskGroup(TaskGroup group, RaceResult<TResult> raceResult)
    {
        _group = group;
        _raceResult = raceResult;
    }

    /// <summary>
    /// Gets they underlying task group. This can be used to run other tasks or spawn child groups.
    /// </summary>
    public TaskGroup TaskGroup => _group;

    /// <inheritdoc cref="TaskGroup.CancellationTokenSource"/>
    public CancellationTokenSource CancellationTaskSource => _group.CancellationTokenSource;

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
}
