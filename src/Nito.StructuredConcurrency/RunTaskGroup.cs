using Nito.StructuredConcurrency.Internals;

namespace Nito.StructuredConcurrency;

/// <summary>
/// A task group represents a list of tasks along with a <see cref="CancellationTokenSource"/>. Semantics:
/// <list type="bullet">
/// <item>When the task group is asynchronously disposed, it will asynchronously wait for all its child tasks to complete. I.e., there's an implicit `Task.WhenAll` at the end of the task group scope.</item>
/// <item>Each child task is provided a <see cref="CancellationToken"/> from this task group.</item>
/// <item><see cref="OperationCanceledException"/>s from child tasks are ignored. This is true regardless of the source of the cancellation; exceptions of this type are <i>always</i> ignored.</item>
/// <item>If any child task faults (with any exception except <see cref="OperationCanceledException"/>), the cancellation token is cancelled. The task group's asynchronous disposal will throw the first of its child exceptions.</item>
/// <item>Disposing the task group does not cancel the task group; it just waits for the child tasks. You can explicitly cancel the task group before disposing, if desired.</item>
/// </list>
/// </summary>
public sealed class RunTaskGroup : IAsyncDisposable
{
    private readonly WorkTaskGroup _group;

    /// <summary>
    /// Creates a task group.
    /// </summary>
    internal RunTaskGroup(WorkTaskGroup group)
    {
        _group = group;
    }

    /// <summary>
    /// The cancellation token for this group.
    /// </summary>
    public CancellationToken CancellationToken => _group.CancellationToken;

    /// <summary>
    /// The cancellation token source for this task group; this can be used to manually initiate cancellation of the task group.
    /// </summary>
    public CancellationTokenSource CancellationTokenSource => _group.CancellationTokenSource;

    /// <summary>
    /// Adds a resource to this task group. Resources are disposed (in reverse order) after all the tasks in the task group complete.
    /// </summary>
    /// <param name="resource">The resource to add.</param>
    public ValueTask AddResourceAsync(object? resource) => _group.AddResourceAsync(resource);

    /// <summary>
    /// Runs a child task (<paramref name="work"/>) as part of this task group.
    /// If <paramref name="work"/> throws an <see cref="OperationCanceledException"/>, it will be ignored by the task group.
    /// If <paramref name="work"/> throws any other exception, then this task group will be canceled.
    /// If the task group is canceled, then it is possible for an already-canceled token to be passed to <paramref name="work"/>.
    /// If the task group has already completed disposing, this method will throw an <see cref="InvalidOperationException"/>.
    /// </summary>
    /// <param name="work">The child work to be done soon. This delegate is passed a <see cref="CancellationToken"/> that is canceled when the task group is canceled. This delegate will be scheduled onto the current context.</param>
    public void Run(Func<CancellationToken, ValueTask> work) => _group.Run(work);

    /// <summary>
    /// Runs a child task (<paramref name="work"/>) as part of this task group.
    /// If <paramref name="work"/> throws an <see cref="OperationCanceledException"/>, it will be ignored by the task group. The returned task will still be canceled/faulted with that exception.
    /// If <paramref name="work"/> throws any other exception, then this task group will be canceled.
    /// If the task group is canceled, then it is possible for an already-canceled token to be passed to <paramref name="work"/>.
    /// If the task group has already completed disposing, this method will throw an <see cref="InvalidOperationException"/>.
    /// </summary>
    /// <param name="work">The child work to be done soon. This delegate is passed a <see cref="CancellationToken"/> that is canceled when the task group is canceled. This delegate will be scheduled onto the current context.</param>
    public Task<T> RunAsync<T>(Func<CancellationToken, ValueTask<T>> work) => _group.RunAsync(work);

    /// <summary>
    /// Asynchronously waits for all tasks in this task group to complete, disposes any resources owned by the task group, and then raises any exceptions observed by tasks in this task group.
    /// </summary>
    public ValueTask DisposeAsync() => _group.DisposeAsync();
}
