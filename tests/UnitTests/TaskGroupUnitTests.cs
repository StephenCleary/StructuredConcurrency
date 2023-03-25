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
}