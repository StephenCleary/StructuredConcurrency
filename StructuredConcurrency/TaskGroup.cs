using Nito.StructuredConcurrency.Internals;
using System.Runtime.ExceptionServices;

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
public sealed class TaskGroup : IAsyncDisposable
{
    private readonly CancellationTokenSource _cancellationTokenSource;
    private readonly DynamicTaskWhenAll _tasks;
    private readonly TaskCompletionSource _groupScope;

    /// <summary>
    /// Creates a task group, optionally linking it to an upstream cancellation source.
    /// </summary>
    public TaskGroup(CancellationToken cancellationToken = default)
    {
        _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _tasks = new();
        _groupScope = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _tasks.Add(_groupScope.Task);
    }

    /// <summary>
    /// Cancels this task group. This is sometimes done just before disposing the task group.
    /// </summary>
    public void Cancel() => _cancellationTokenSource.Cancel();

    /// <summary>
    /// Runs a child task (<paramref name="work"/>) as part of this task group.
    /// If <paramref name="work"/> throws an <see cref="OperationCanceledException"/>, it will be ignored by the task group. The returned task will still be canceled/faulted with that exception.
    /// If <paramref name="work"/> throws any other exception, then this task group will be canceled.
    /// If the task group is canceled, then it is possible for an already-canceled token to be passed to <paramref name="work"/>.
    /// If the task group has already completed disposing, this method will throw an <see cref="InvalidOperationException"/>.
    /// </summary>
    /// <param name="work">The child work to be done soon. This delegate is passed a <see cref="CancellationToken"/> that is canceled when the task group is canceled. This delegate will be scheduled onto the current context.</param>
    public void Run(Func<CancellationToken, Task> work) => _ = AddAndTrack(work);

    public Task<T> Run<T>(Func<CancellationToken, Task<T>> work) => AddAndTrack(work);

    /// <summary>
    /// Adds a child task (<paramref name="work"/>) to this task group.
    /// If <paramref name="work"/> throws an <see cref="OperationCanceledException"/>, it will be ignored by the task group. The returned task will still be canceled/faulted with that exception.
    /// If <paramref name="work"/> throws any other exception, then this task group will be canceled.
    /// If the task group is canceled, then it is possible for an already-canceled token to be passed to <paramref name="work"/>.
    /// If the task group has already completed disposing, this method will throw an <see cref="InvalidOperationException"/>.
    /// </summary>
    /// <param name="work">The child work to be done soon. This delegate is passed a <see cref="CancellationToken"/> that is canceled when the task group is canceled. This delegate will be scheduled onto the current context.</param>
    /// <returns>A task representing the work that was done. This task may be discarded or used to retrieve errors (or detect cancellation).</returns>
    public Task AddAndTrack(Func<CancellationToken, Task> work) => AddAndTrack(async ct => { await work(ct).ConfigureAwait(false); return 0; });

    /// <summary>
    /// Adds a child task (<paramref name="work"/>) to this task group.
    /// If <paramref name="work"/> throws an <see cref="OperationCanceledException"/>, it will be ignored by the task group. The returned task will still be canceled/faulted with that exception.
    /// If <paramref name="work"/> throws any other exception, then this task group will be canceled. The returned task will be faulted with that exception.
    /// If the task group is canceled, then it is possible for an already-canceled token to be passed to <paramref name="work"/>.
    /// If the task group has already completed disposing, this method will throw an <see cref="InvalidOperationException"/>.
    /// </summary>
    /// <param name="work">The child work to be done soon. This delegate is passed a <see cref="CancellationToken"/> that is canceled when the task group is canceled. This delegate will be scheduled onto the current context.</param>
    /// <returns>A task representing the work that was done. This is usually discarded but may be used to retrieve results or errors (or detect cancellation).</returns>
    public Task<T> AddAndTrack<T>(Func<CancellationToken, Task<T>> work)
    {
        var startSignal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var result = CancelOnException(_cancellationTokenSource, DelayStart(startSignal.Task, work))(_cancellationTokenSource.Token);
        var childTask = IgnoreCancellation(result);
        _tasks.Add(childTask);

        startSignal.TrySetResult();

        return result;

        static async Task IgnoreCancellation(Task<T> task)
        {
            try
            {
                _ = await task.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
        }

        static Func<CancellationToken, Task<T>> DelayStart(Task startSignal, Func<CancellationToken, Task<T>> work) => async cancellationToken =>
        {
            // Wait until we're in the child task collection before executing the work delegate.
            await startSignal;

            return await work(cancellationToken).ConfigureAwait(false);
        };

        static Func<CancellationToken, Task<T>> CancelOnException(CancellationTokenSource cancellationTokenSource, Func<CancellationToken, Task<T>> work) => async cancellationToken =>
        {
            try
            {
                return await work(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                cancellationTokenSource.Cancel();
                throw;
            }
        };
    }

    public async ValueTask DisposeAsync()
    {
        _groupScope.TrySetResult();
        var compositeTask = _tasks.Task;
        try
        {
            await compositeTask.ConfigureAwait(false);
        }
        catch
        {
        }

        _cancellationTokenSource.Dispose();

        if (compositeTask.Exception != null)
            ExceptionDispatchInfo.Capture(compositeTask.Exception.InnerException!).Throw();
    }
}
