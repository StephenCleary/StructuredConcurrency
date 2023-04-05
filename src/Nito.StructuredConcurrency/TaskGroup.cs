using Nito.StructuredConcurrency.Advanced;
using Nito.StructuredConcurrency.Internals;

namespace Nito.StructuredConcurrency;

#pragma warning disable CA1068 // CancellationToken parameters must come last

/// <summary>
/// Provides methods for creating and running different types of task groups.
/// </summary>
public static class TaskGroup
{
    /// <summary>
    /// Creates a new <see cref="RunTaskGroup"/> and runs the specified work as the first work task.
    /// </summary>
    /// <typeparam name="T">The type of the result of the task.</typeparam>
    /// <param name="cancellationToken">An upstream cancellation token for the task group.</param>
    /// <param name="work">The first work task of the task group.</param>
    public static async Task<T> RunGroupAsync<T>(CancellationToken cancellationToken, Func<RunTaskGroup, ValueTask<T>> work)
    {
#pragma warning disable CA2000 // Dispose objects before losing scope
        var group = new RunTaskGroup(new TaskGroupCore(cancellationToken));
#pragma warning restore CA2000 // Dispose objects before losing scope
        await using (group.ConfigureAwait(false))
            return await group.RunAsync(_ => work(group)).ConfigureAwait(false);
    }

    /// <summary>
    /// Creates a new <see cref="RunTaskGroup"/> and runs the specified work as the first work task.
    /// </summary>
    /// <typeparam name="T">The type of the result of the task.</typeparam>
    /// <param name="cancellationToken">An upstream cancellation token for the task group.</param>
    /// <param name="work">The first work task of the task group.</param>
    public static Task<T> RunGroupAsync<T>(CancellationToken cancellationToken, Func<RunTaskGroup, T> work) =>
        RunGroupAsync(cancellationToken, work.AsAsync());

    /// <summary>
    /// Creates a new <see cref="RunTaskGroup"/> and runs the specified work as the first work task.
    /// </summary>
    /// <param name="cancellationToken">An upstream cancellation token for the task group.</param>
    /// <param name="work">The first work task of the task group.</param>
    public static Task RunGroupAsync(CancellationToken cancellationToken, Func<RunTaskGroup, ValueTask> work) =>
        RunGroupAsync(cancellationToken, work.WithResult());

    /// <summary>
    /// Creates a new <see cref="RunTaskGroup"/> and runs the specified work as the first work task.
    /// </summary>
    /// <param name="cancellationToken">An upstream cancellation token for the task group.</param>
    /// <param name="work">The first work task of the task group.</param>
    public static Task RunGroupAsync(CancellationToken cancellationToken, Action<RunTaskGroup> work) =>
        RunGroupAsync(cancellationToken, work.AsAsync().WithResult());

    /// <summary>
    /// Creates a new <see cref="RaceTaskGroup{TResult}"/> and runs the specified work as the first run task.
    /// </summary>
    /// <param name="cancellationToken">An upstream cancellation token for the task group.</param>
    /// <param name="work">The first run task of the task group.</param>
    public static Task<T> RaceGroupAsync<T>(CancellationToken cancellationToken, Func<RaceTaskGroup<T>, ValueTask> work) =>
        RaceTaskGroup<T>.RaceGroupAsync(cancellationToken, work);

    /// <summary>
    /// Creates a new <see cref="RaceTaskGroup{TResult}"/> and runs the specified work as the first run task.
    /// </summary>
    /// <param name="cancellationToken">An upstream cancellation token for the task group.</param>
    /// <param name="work">The first run task of the task group.</param>
    public static Task<T> RaceGroupAsync<T>(CancellationToken cancellationToken, Action<RaceTaskGroup<T>> work) =>
        RaceTaskGroup<T>.RaceGroupAsync(cancellationToken, work.AsAsync());
}
