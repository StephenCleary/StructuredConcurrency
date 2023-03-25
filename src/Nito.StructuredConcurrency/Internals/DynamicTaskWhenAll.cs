using System.Collections.Immutable;

namespace Nito.StructuredConcurrency.Internals;

/// <summary>
/// Similar to <see cref="Task.WhenAll(Task[])"/>, but allowing any number of tasks to be added, even after waiting has begun.
/// At least one task must be added, or else the <see cref="Task"/> will never complete.
/// </summary>
public sealed class DynamicTaskWhenAll
{
    private readonly TaskCompletionSource _taskCompletionSource = new();
    private State _state = new(ImmutableQueue<Exception>.Empty, false, 0);

    /// <summary>
    /// Adds a task to this dynamic waiter.
    /// Throws an exception if the wait has already completed.
    /// </summary>
    /// <param name="task">The task to add.</param>
    /// <exception cref="InvalidOperationException">The dynamic waiter has already completed.</exception>
    public void Add(Task task)
    {
        _ = task ?? throw new ArgumentNullException(nameof(task));

        var localState = InterlockedEx.Apply(ref _state, x => x switch
        {
            { Done: true } => x,
            _ => x with { Count = x.Count + 1 },
        });
        if (localState.Done)
            throw new InvalidOperationException($"{nameof(DynamicTaskWhenAll)} has already completed.");
        Handle(task);

        async void Handle(Task task)
        {
#pragma warning disable CA1031 // Do not catch general exception types
            try
            {
                await task.ConfigureAwait(false);
                var localState = InterlockedEx.Apply(ref _state, x => x switch
                {
                    { Done: true } => x,
                    { Count: 1 } => x with { Done = true, Count = 0 },
                    _ => x with { Count = x.Count - 1 },
                });
                Complete(localState);
            }
            catch (Exception ex)
            {
                var localState = InterlockedEx.Apply(ref _state, x => x switch
                {
                    { Done: true } => x,
                    { Count: 1 } => x with { Done = true, Count = 0, Exceptions = x.Exceptions.Enqueue(ex) },
                    _ => x with { Count = x.Count - 1, Exceptions = x.Exceptions.Enqueue(ex) },
                });
                Complete(localState);
            }
#pragma warning restore CA1031 // Do not catch general exception types

            void Complete(State localState)
            {
                if (!localState.Done)
                    return;
                if (localState.Exceptions.IsEmpty)
                    _taskCompletionSource.TrySetResult();
                else
                    _taskCompletionSource.TrySetException(localState.Exceptions);
            }
        }
    }

    /// <summary>
    /// Gets a task which is completed when all tasks added to this dynamic awaiter have completed.
    /// </summary>
    public Task Task => _taskCompletionSource.Task;

    private record class State(ImmutableQueue<Exception> Exceptions, bool Done, uint Count);
}
