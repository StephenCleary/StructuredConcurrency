using System.Collections.Immutable;

namespace Nito.StructuredConcurrency.Internals;

// TODO: at least one must be added!

public sealed class DynamicTaskWhenAll
{
    private readonly TaskCompletionSource _taskCompletionSource = new();
    private State _state = new(ImmutableQueue<Exception>.Empty, false, 0);

    public void Add(Task task)
    {
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

    public Task Task => _taskCompletionSource.Task;

    private record class State(ImmutableQueue<Exception> Exceptions, bool Done, uint Count);
}

public sealed class DynamicTaskWhenAll<TResult>
{
    private readonly TaskCompletionSource<IReadOnlyList<TResult>> _taskCompletionSource = new();
    private State _state = new(ImmutableQueue<Exception>.Empty, ImmutableList<TResult>.Empty, false, 0);

    public void Add(Task<TResult> task)
    {
        var localState = InterlockedEx.Apply(ref _state, x => x switch
        {
            { Done: true } => x,
            _ => x with { Count = x.Count + 1, Results = x.Results.Add(default!) },
        });
        if (localState.Done)
            throw new InvalidOperationException($"{nameof(DynamicTaskWhenAll<TResult>)} has already completed.");
        var index = localState.Results.Count - 1;
        Handle(task);

        async void Handle(Task<TResult> task)
        {
            try
            {
                var result = await task.ConfigureAwait(false);
                var localState = InterlockedEx.Apply(ref _state, x => x switch
                {
                    { Done: true } => x,
                    { Count: 1 } => x with { Done = true, Count = 0, Results = x.Results.SetItem(index, result) },
                    _ => x with { Count = x.Count - 1, Results = x.Results.SetItem(index, result) },
                });
                Complete(localState);
            }
            catch (Exception ex)
            {
                var localState = InterlockedEx.Apply(ref _state, x => x switch
                {
                    { Done: true } => x,
                    { Count: 1 } => x with { Done = true, Count = 0, Results = ImmutableList<TResult>.Empty, Exceptions = x.Exceptions.Enqueue(ex) },
                    _ => x with { Count = x.Count - 1, Results = ImmutableList<TResult>.Empty, Exceptions = x.Exceptions.Enqueue(ex) },
                });
                Complete(localState);
            }

            void Complete(State localState)
            {
                if (!localState.Done)
                    return;
                if (localState.Exceptions.IsEmpty)
                    _taskCompletionSource.TrySetResult(localState.Results!);
                else
                    _taskCompletionSource.TrySetException(localState.Exceptions);
            }
        }
    }

    public Task<IReadOnlyList<TResult>> Task => _taskCompletionSource.Task;

    private record class State(ImmutableQueue<Exception> Exceptions, ImmutableList<TResult> Results, bool Done, uint Count);
}
