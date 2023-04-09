using Nito.StructuredConcurrency;
using System.Threading.Channels;

namespace UnitTests;

public class Usage
{
    [Fact]
    public async Task ImplicitWhenAll()
    {
        await TaskGroup.RunGroupAsync(default, group =>
        {
            group.Run(async token => await Task.Delay(TimeSpan.FromMilliseconds(1), token));
            group.Run(async token => await Task.Delay(TimeSpan.FromMilliseconds(2), token));
        }); // implicit WhenAll
    }

    [Fact]
    public async Task ProducerConsumer()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await TaskGroup.RunGroupAsync(default, group =>
            {
                var channel = Channel.CreateBounded<int>(10);

                // Producer
                group.Run(async token =>
                {
                    try
                    {
                        foreach (var value in Enumerable.Range(1, 1_000))
                        {
                            token.ThrowIfCancellationRequested();
                            await channel.Writer.WriteAsync(value, token);
                        }

                        channel.Writer.Complete();
                    }
                    catch (Exception ex)
                    {
                        channel.Writer.Complete(ex);
                    }
                });

                // Consumer
                group.Run(async token =>
                {
                    await foreach (var value in channel.Reader.ReadAllAsync(token))
                    {
                        if (value == 13)
                            throw new InvalidOperationException("Oh, no!");
                    }
                });

                // If either the producer or consumer encounters an exception,
                // then both are cancelled, and the TaskGroup disposal waits for
                // both of them to completely cancel before re-raising the original
                // exception.
            });
        });
    }

    [Fact]
    public async Task ProducerMultipleConsumers()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await TaskGroup.RunGroupAsync(default, group =>
            {
                var channel = Channel.CreateBounded<int>(10);

                // Producer
                group.Run(async token =>
                {
                    try
                    {
                        foreach (var value in Enumerable.Range(1, 1_000))
                            await channel.Writer.WriteAsync(value, token);
                        channel.Writer.Complete();
                    }
                    catch (Exception ex)
                    {
                        channel.Writer.Complete(ex);
                    }
                });

                // Consumer
                group.Run(async token =>
                {
                    await foreach (var value in channel.Reader.ReadAllAsync(token))
                    {
                        if (value == 13)
                            throw new InvalidOperationException("Oh, no!");
                    }
                });

                // Consumer
                group.Run(async token =>
                {
                    await foreach (var value in channel.Reader.ReadAllAsync(token))
                    {
                        if (value == 13)
                            throw new InvalidOperationException("Oh, no!");
                    }
                });

                // If the producer or either consumer encounters an exception,
                // then all are cancelled, and the TaskGroup disposal waits for
                // all of them to completely cancel before re-raising the original
                // exception.
            });
        });
    }

    [Fact]
    public async Task Pipeline()
    {
        var result = await TaskGroup.RunGroupAsync(default, async group =>
        {
            // All the channels and transformation methods are asynchronously
            // scoped to this "CalculateUsingTemporaryPipelineAsync" method.
            // If there are any exceptions in any of them, all of them are
            // cancelled and all cancellation is completed before rethrowing the
            // original exception.

            var channel1 = Channel.CreateBounded<int>(1);

            // Producer
            group.Run(async token =>
            {
                try
                {
                    foreach (var value in Enumerable.Range(1, 1_000))
                    {
                        // Pretend to do asynchronous work that observes cancellation
                        await Task.Yield();
                        token.ThrowIfCancellationRequested();

                        await channel1.Writer.WriteAsync(value, token);
                    }

                    channel1.Writer.Complete();
                }
                catch (Exception ex)
                {
                    channel1.Writer.Complete(ex);
                }
            });

            var channel2 = Channel.CreateBounded<int>(1);

            // Transformer 1
            group.Run(async token =>
            {
                try
                {
                    await foreach (var value in channel1.Reader.ReadAllAsync(token))
                        await channel2.Writer.WriteAsync(value / 2, token);
                    channel2.Writer.Complete();
                }
                catch (Exception ex)
                {
                    channel2.Writer.Complete(ex);
                }
            });

            var channel3 = Channel.CreateBounded<double>(1);

            // Transformer 2
            group.Run(async token =>
            {
                try
                {
                    await foreach (var value in channel2.Reader.ReadAllAsync(token))
                        await channel3.Writer.WriteAsync(value * 3.0, token);
                    channel3.Writer.Complete();
                }
                catch (Exception ex)
                {
                    channel3.Writer.Complete(ex);
                }
            });

            var result = 0.0;
            await foreach (var value in channel3.Reader.ReadAllAsync(group.CancellationToken))
                result += value;
            return result;
        });

        Assert.Equal(750000, result);
    }

    [Fact]
    public async Task ExplicitCancel()
    {
        var result = await TaskGroup.RunGroupAsync(default, group =>
        {
            // All the channels and transformation methods are asynchronously
            // scoped to this "CalculateUsingTemporaryPipelineAsync" method.
            // If there are any exceptions in any of them, all of them are
            // cancelled and all cancellation is completed before rethrowing the
            // original exception.

            var channel1 = Channel.CreateBounded<int>(1);

            // Producer
            group.Run(async token =>
            {
                try
                {
                    foreach (var value in Enumerable.Range(1, 1_000))
                    {
                        // Pretend to do asynchronous work that observes cancellation
                        await Task.Yield();
                        token.ThrowIfCancellationRequested();

                        await channel1.Writer.WriteAsync(value, token);
                    }

                    channel1.Writer.Complete();
                }
                catch (Exception ex)
                {
                    channel1.Writer.Complete(ex);
                }
            });

            var channel2 = Channel.CreateBounded<int>(1);

            // Transformer 1
            group.Run(async token =>
            {
                try
                {
                    await foreach (var value in channel1.Reader.ReadAllAsync(token))
                        await channel2.Writer.WriteAsync(value / 2, token);
                    channel2.Writer.Complete();
                }
                catch (Exception ex)
                {
                    channel2.Writer.Complete(ex);
                }
            });

            var channel3 = Channel.CreateBounded<double>(1);

            // Transformer 2
            group.Run(async token =>
            {
                try
                {
                    await foreach (var value in channel2.Reader.ReadAllAsync(token))
                        await channel3.Writer.WriteAsync(value * 3.0, token);
                    channel3.Writer.Complete();
                }
                catch (Exception ex)
                {
                    channel3.Writer.Complete(ex);
                }
            });

            // Oh, hey, we don't need this pipeline after all.
            group.CancellationTokenSource.Cancel();
            return 42;
        });

        Assert.Equal(42, result);
    }

    [Fact]
    public async Task Timeouts()
    {
        var groupTask = TaskGroup.RunGroupAsync(default, async group =>
        {
            await TaskGroup.RunGroupAsync(group.CancellationToken, async childGroup =>
            {
                childGroup.CancellationTokenSource.CancelAfter(TimeSpan.FromMilliseconds(10));
                await Task.Delay(Timeout.InfiniteTimeSpan, childGroup.CancellationToken);
            });
        });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => groupTask);
    }
}
