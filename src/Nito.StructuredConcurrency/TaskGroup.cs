using Nito.Disposables;
using Nito.StructuredConcurrency.Internals;
using System.Runtime.ExceptionServices;
using System.Threading.Channels;

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
public sealed partial class TaskGroup : IAsyncDisposable
{
    private readonly DynamicTaskWhenAll _tasks;
    private readonly TaskCompletionSource _groupScope;
    private readonly CollectionAsyncDisposable _resources;

    /// <summary>
    /// Creates a task group, optionally linking it to an upstream cancellation source.
    /// </summary>
    /// <param name="cancellationToken">The upstream cancellation token.</param>
    internal TaskGroup(CancellationToken cancellationToken = default)
    {
        CancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _tasks = new();
        _groupScope = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _tasks.Add(_groupScope.Task);
        _resources = new();
    }

    /// <summary>
    /// The cancellation token for this group.
    /// </summary>
    public CancellationToken CancellationToken => CancellationTokenSource.Token;

    /// <summary>
    /// The cancellation token source for this task group; this can be used to manually initiate cancellation of the task group.
    /// </summary>
    public CancellationTokenSource CancellationTokenSource { get; }

    /// <summary>
    /// Adds a resource to this task group. Resources are disposed (in reverse order) after all the tasks in the task group complete.
    /// </summary>
    /// <param name="resource">The resource to add.</param>
#pragma warning disable CA2000 // Dispose objects before losing scope
    public ValueTask AddResourceAsync(object? resource) => _resources.AddAsync(DisposeUtility.TryWrap(resource));
#pragma warning restore CA2000 // Dispose objects before losing scope

    /// <summary>
    /// Runs a child task (<paramref name="work"/>) as part of this task group.
    /// If <paramref name="work"/> throws an <see cref="OperationCanceledException"/>, it will be ignored by the task group.
    /// If <paramref name="work"/> throws any other exception, then this task group will be canceled.
    /// If the task group is canceled, then it is possible for an already-canceled token to be passed to <paramref name="work"/>.
    /// If the task group has already completed disposing, this method will throw an <see cref="InvalidOperationException"/>.
    /// </summary>
    /// <param name="work">The child work to be done soon. This delegate is passed a <see cref="CancellationToken"/> that is canceled when the task group is canceled. This delegate will be scheduled onto the current context.</param>
    public void Run(Func<CancellationToken, ValueTask> work) => _ = RunAsync(work.WithResult());

    /// <summary>
    /// Runs a child task (<paramref name="work"/>) as part of this task group.
    /// If <paramref name="work"/> throws an <see cref="OperationCanceledException"/>, it will be ignored by the task group. The returned task will still be canceled/faulted with that exception.
    /// If <paramref name="work"/> throws any other exception, then this task group will be canceled.
    /// If the task group is canceled, then it is possible for an already-canceled token to be passed to <paramref name="work"/>.
    /// If the task group has already completed disposing, this method will throw an <see cref="InvalidOperationException"/>.
    /// </summary>
    /// <param name="work">The child work to be done soon. This delegate is passed a <see cref="CancellationToken"/> that is canceled when the task group is canceled. This delegate will be scheduled onto the current context.</param>
    public Task<T> RunAsync<T>(Func<CancellationToken, ValueTask<T>> work)
    {
        var startSignal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var result = CancelOnException(CancellationTokenSource, DelayStart(startSignal.Task, work))(CancellationToken);
        var childTask = result.IgnoreCancellation();
        _tasks.Add(childTask);

        startSignal.TrySetResult();

        return result;

        static Func<CancellationToken, ValueTask<T>> DelayStart(Task startSignal, Func<CancellationToken, ValueTask<T>> work) => async cancellationToken =>
        {
            // Wait until we're in the child task collection before executing the work delegate.
#pragma warning disable CA2007 // Consider calling ConfigureAwait on the awaited task
            await startSignal;
#pragma warning restore CA2007 // Consider calling ConfigureAwait on the awaited task

            return await work(cancellationToken).ConfigureAwait(false);
        };

        static Func<CancellationToken, Task<T>> CancelOnException(CancellationTokenSource cancellationTokenSource, Func<CancellationToken, ValueTask<T>> work) => async cancellationToken =>
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

    /// <summary>
    /// Executes work that produces a sequence.
    /// This sequence cannot be consumed outside the scope of the task group.
    /// Each item produced by this sequence is a resource owned by this task group.
    /// </summary>
    /// <typeparam name="T">The type of items produced.</typeparam>
    /// <param name="work">The work to perform, producing a sequence of items.</param>
    public IAsyncEnumerable<T> RunSequence<T>(Func<CancellationToken, IAsyncEnumerable<T>> work) => RunSequence(1, work);

    /// <summary>
    /// Executes work that produces a sequence.
    /// This sequence cannot be consumed outside the scope of the task group.
    /// Each item produced by this sequence is a resource owned by this task group.
    /// </summary>
    /// <typeparam name="T">The type of items produced.</typeparam>
    /// <param name="capacity">The capacity of the channel containing the results of this work. Defaults to <c>1</c>.</param>
    /// <param name="work">The work to perform, producing a sequence of items.</param>
    public IAsyncEnumerable<T> RunSequence<T>(int capacity, Func<CancellationToken, IAsyncEnumerable<T>> work)
    {
        var channel = Channel.CreateBounded<T>(capacity);
        Run(async ct =>
        {
            try
            {
                // Note: No `WithCancellation(ct)`, because the work must decide when it is canceled.
                // If the worker method produces an item, we must flow it through.
                await foreach (var item in work(ct).ConfigureAwait(false))
                {
                    // If cancellation happens after the worker produced an item, then just dispose of it.
                    try
                    {
                        await channel.Writer.WriteAsync(item, ct).ConfigureAwait(false);
                    }
                    catch
                    {
                        await DisposeUtility.Wrap(item).DisposeAsync().ConfigureAwait(false);
                        throw;
                    }
                }
            }
            catch (Exception ex)
            {
                channel.Writer.TryComplete(ex);
                throw;
            }
            finally
            {
                channel.Writer.TryComplete();
            }
        });

        // Note: `CancellationToken.None`, for the same reason as above: the worker method produced an item, and we must flow it through.
        return channel.Reader.ReadAllAsync(CancellationToken.None);
    }

    /// <summary>
    /// Asynchronously waits for all tasks in this task group to complete, disposes any resources owned by the task group, and then raises any exceptions observed by tasks in this task group.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        _groupScope.TrySetResult();
        var compositeTask = _tasks.Task;
#pragma warning disable CA1031 // Do not catch general exception types
        try
        {
            await compositeTask.ConfigureAwait(false);
        }
        catch
        {
        }
#pragma warning restore CA1031 // Do not catch general exception types

        await _resources.DisposeAsync().ConfigureAwait(false);
        CancellationTokenSource.Dispose();

        if (compositeTask.Exception != null)
            ExceptionDispatchInfo.Capture(compositeTask.Exception.InnerException!).Throw();
    }
}
