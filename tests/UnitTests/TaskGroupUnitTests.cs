using Nito.Disposables;
using Nito.StructuredConcurrency;

namespace UnitTests;

public class TaskGroupUnitTests
{
    [Fact]
    public async Task WaitsForAllChildrenToComplete()
    {
        var task1Signal = new TaskCompletionSource();
        var task2Signal = new TaskCompletionSource();

        Task? task1 = null;
        Task? task2 = null;

        var groupTask = UseTaskGroup();

        await Assert.ThrowsAnyAsync<TimeoutException>(() => task1!.WaitAsync(TimeSpan.FromMilliseconds(100)));
        await Assert.ThrowsAnyAsync<TimeoutException>(() => task2!.WaitAsync(TimeSpan.FromMilliseconds(100)));
        await Assert.ThrowsAnyAsync<TimeoutException>(() => groupTask.WaitAsync(TimeSpan.FromMilliseconds(100)));

        task1Signal.TrySetResult();

        await task1!;
        await Assert.ThrowsAnyAsync<TimeoutException>(() => task2!.WaitAsync(TimeSpan.FromMilliseconds(100)));
        await Assert.ThrowsAnyAsync<TimeoutException>(() => groupTask.WaitAsync(TimeSpan.FromMilliseconds(100)));

        task2Signal.TrySetResult();

        await task1;
        await task2!;
        await groupTask;

        async Task UseTaskGroup()
        {
            await using var group = new TaskGroup();
            task1 = group.Run(async _ => { await task1Signal.Task; return 0; });
            task2 = group.Run(async _ => { await task2Signal.Task; return 0; });
        }
    }

    [Fact]
    public async Task FaultedTask_CancelsOtherTasks()
    {
        var task1Signal = new TaskCompletionSource();

        Task? task1 = null;
        Task? task2 = null;

        var groupTask = UseTaskGroup();

        task1Signal.TrySetResult();

        await Assert.ThrowsAsync<InvalidOperationException>(() => task1!);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task2!);
        await Assert.ThrowsAnyAsync<InvalidOperationException>(() => groupTask);

        async Task UseTaskGroup()
        {
            await using var group = new TaskGroup();
            task1 = group.Run(async _ => { await task1Signal.Task; throw new InvalidOperationException("1"); return 0; });
            task2 = group.Run(async ct => { await Task.Delay(Timeout.InfiniteTimeSpan, ct); return 0; });
        }
    }

    [Fact]
    public async Task ExternalCancellation_Ignored()
    {
        var task1Signal = new TaskCompletionSource();
        var cts = new CancellationTokenSource();

        Task? task1 = null;
        Task? task2 = null;

        var groupTask = UseTaskGroup();

        task1Signal.TrySetResult();

        await task1!;
        await Assert.ThrowsAnyAsync<TimeoutException>(() => task2!.WaitAsync(TimeSpan.FromMilliseconds(100)));
        await Assert.ThrowsAnyAsync<TimeoutException>(() => groupTask.WaitAsync(TimeSpan.FromMilliseconds(100)));

        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task2!);
        await groupTask;

        async Task UseTaskGroup()
        {
            await using var group = new TaskGroup();
            task1 = group.Run(async _ => { await task1Signal.Task; return 0; });
            task2 = group.Run(async _ => { await Task.Delay(Timeout.InfiniteTimeSpan, cts.Token); return 0; });
        }
    }

    [Fact]
    public async Task EmptyGroup_NoDeadlock()
    {
        await using var group = new TaskGroup();
    }

    [Fact]
    public async Task Resource_DisposedAtEndOfTaskGroup()
    {
        int wasdisposed = 0;

        await UseTaskGroup();
        var result = Interlocked.CompareExchange(ref wasdisposed, 0, 0);
        Assert.Equal(1, wasdisposed);

        async Task UseTaskGroup()
        {
            await using var group = new TaskGroup();
            await group.AddResourceAsync(Disposable.Create(() => Interlocked.Exchange(ref wasdisposed, 1)));
        }
    }

    [Fact]
    public async Task Resource_ThrowsException_Ignored()
    {
        await UseTaskGroup();

        async Task UseTaskGroup()
        {
            await using var group = new TaskGroup();
            await group.AddResourceAsync(Disposable.Create(() => throw new InvalidOperationException("nope")));
        }
    }

    [Fact]
    public async Task ReturnValue_NotAResource()
    {
        int wasdisposed = 0;

        await UseTaskGroup();
        var result = Interlocked.CompareExchange(ref wasdisposed, 0, 0);
        Assert.Equal(0, wasdisposed);

        async Task UseTaskGroup()
        {
            await using var group = new TaskGroup();
            var resource = await group.Run(async ct => Disposable.Create(() => Interlocked.Exchange(ref wasdisposed, 1)));
        }
    }

    [Fact]
    public async Task SequenceValue_IsAResource()
    {
        int wasdisposed = 0;

        await UseTaskGroup();
        var result = Interlocked.CompareExchange(ref wasdisposed, 0, 0);
        Assert.Equal(1, wasdisposed);

        async Task UseTaskGroup()
        {
            await using var group = new TaskGroup();
            _ = group.RunSequence(ct =>
            {
                return Impl();
                async IAsyncEnumerable<Disposable> Impl()
                {
                    yield return Disposable.Create(() => Interlocked.Exchange(ref wasdisposed, 1));
                }
            });
        }
    }

    [Fact]
    public async Task FaultingSequence_CompletesSequenceWithFault()
    {
        int exceptionWasObserved = 0;

        try { await UseTaskGroup(); } catch { }
        var result = Interlocked.CompareExchange(ref exceptionWasObserved, 0, 0);
        Assert.Equal(1, exceptionWasObserved);

        async Task UseTaskGroup()
        {
            await using var group = new TaskGroup();
            var sequence = group.RunSequence(ct =>
            {
                return Impl();
                async IAsyncEnumerable<int> Impl()
                {
                    yield return 0;
                    throw new InvalidOperationException();
                }
            });

            try
            {
                await foreach (var item in sequence)
                    ;
            }
            catch (InvalidOperationException)
            {
                Interlocked.Exchange(ref exceptionWasObserved, 1);
            }
            catch (OperationCanceledException)
            {
                Interlocked.Exchange(ref exceptionWasObserved, 2); // (should not happen)
            }
        }
    }

    [Fact]
    public async Task CancelledSequence_CompletesSequenceWithCancellation()
    {
        int exceptionWasObserved = 0;

        try { await UseTaskGroup(); } catch { }
        var result = Interlocked.CompareExchange(ref exceptionWasObserved, 0, 0);
        Assert.Equal(1, exceptionWasObserved);

        async Task UseTaskGroup()
        {
            await using var group = new TaskGroup();
            var sequence = group.RunSequence(ct =>
            {
                return Impl();
                async IAsyncEnumerable<int> Impl()
                {
                    yield return 0;
                    await Task.Delay(Timeout.InfiniteTimeSpan, ct);
                }
            });

            group.Run(ct =>
            {
                throw new InvalidOperationException();
            });

            try
            {
                await foreach (var item in sequence)
                    ;
            }
            catch (OperationCanceledException)
            {
                Interlocked.Exchange(ref exceptionWasObserved, 1);
            }
        }
    }
}