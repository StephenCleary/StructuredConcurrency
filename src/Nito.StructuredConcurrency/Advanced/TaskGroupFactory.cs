namespace Nito.StructuredConcurrency.Advanced;

/// <summary>
/// Provides advanced methods for creating task groups with non-standard lifetimes.
/// </summary>
public static class TaskGroupFactory
{
    /// <inheritdoc cref="TaskGroupCore.TaskGroupCore"/>
    public static TaskGroupCore CreateTaskGroupCore(CancellationToken cancellationToken) => new(cancellationToken);

    /// <inheritdoc cref="RunTaskGroup.RunTaskGroup"/>
#pragma warning disable CA2000 // Dispose objects before losing scope
    public static RunTaskGroup CreateRunTaskGroup(CancellationToken cancellationToken) => new(new(cancellationToken));
#pragma warning restore CA2000 // Dispose objects before losing scope
}
