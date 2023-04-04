namespace Nito.StructuredConcurrency.Advanced;

/// <summary>
/// Provides advanced methods for creating task groups with non-standard lifetimes.
/// </summary>
public static class TaskGroupFactory
{
    /// <inheritdoc cref="WorkTaskGroup.WorkTaskGroup"/>
    public static WorkTaskGroup CreateWorkTaskGroup(CancellationToken cancellationToken) => new(cancellationToken);

    /// <inheritdoc cref="TaskGroup.TaskGroup"/>
    public static TaskGroup CreateTaskGroup(WorkTaskGroup group) => new(group);

    /// <inheritdoc cref="RacingTaskGroup{TResult}.RacingTaskGroup"/>
    public static RacingTaskGroup<TResult> CreateRacingTaskGroup<TResult>(WorkTaskGroup group, RaceResult<TResult> raceResult) => new(group, raceResult);
}
