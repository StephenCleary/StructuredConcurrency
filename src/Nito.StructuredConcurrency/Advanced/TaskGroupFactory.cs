namespace Nito.StructuredConcurrency.Advanced;

/// <summary>
/// Provides advanced methods for creating task groups with non-standard lifetimes.
/// </summary>
public static class TaskGroupFactory
{
    /// <inheritdoc cref="TaskGroup.TaskGroup"/>
    public static TaskGroup CreateTaskGroup(CancellationToken cancellationToken) => new(cancellationToken);

    /// <inheritdoc cref="RacingTaskGroup{TResult}.RacingTaskGroup"/>
    public static RacingTaskGroup<TResult> CreateRacingTaskGroup<TResult>(TaskGroup taskGroup, RaceResult<TResult> raceResult) => new(taskGroup, raceResult);
}
