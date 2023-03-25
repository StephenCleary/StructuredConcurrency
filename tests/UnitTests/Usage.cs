using Newtonsoft.Json.Linq;
using Nito.StructuredConcurrency;
using System.Net;
using System.Net.Sockets;
using System.Threading.Channels;

namespace UnitTests;

public class Usage
{
    [Fact]
    public async Task ImplicitWhenAll()
    {
        await using var group = new TaskGroup();
        group.Run(async token => await Task.Delay(TimeSpan.FromMilliseconds(1), token));
        group.Run(async token => await Task.Delay(TimeSpan.FromMilliseconds(2), token));
    } // implicit WhenAll

    [Fact]
    public async Task ProducerConsumer_Explicit()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await using var group = new TaskGroup();
            var channel = Channel.CreateBounded<int>(10);

            // Producer
            group.Run(async token =>
            {
                foreach (var value in Enumerable.Range(1, 1_000))
                {
                    token.ThrowIfCancellationRequested();
                    await channel.Writer.WriteAsync(value, token);
                }

                channel.Writer.Complete();
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
    }

    [Fact]
    public async Task ProducerConsumer()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await using var group = new TaskGroup();

            // Producer
            var integers = group.RunSequence(token =>
            {
                return Impl();

                async IAsyncEnumerable<int> Impl()
                {
                    foreach (var value in Enumerable.Range(1, 1_000))
                    {
                        // Pretend to do asynchronous work that observes cancellation
                        await Task.Yield();
                        token.ThrowIfCancellationRequested();

                        yield return value;
                    }
                }
            });

            // Consumer
            group.Run(async token =>
            {
                await foreach (var value in integers.WithCancellation(token))
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
    }

    [Fact]
    public async Task ProducerMultipleConsumers_Explicit()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await using var group = new TaskGroup();
            var channel = Channel.CreateBounded<int>(10);

            // Producer
            group.Run(async token =>
            {
                foreach (var value in Enumerable.Range(1, 1_000))
                    await channel.Writer.WriteAsync(value, token);
                channel.Writer.Complete();
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
    }

    [Fact]
    public async Task ProducerMultipleConsumers()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await using var group = new TaskGroup();

            // Producer
            var channel = group.RunSequence(token =>
            {
                return Impl();
                async IAsyncEnumerable<int> Impl()
                {
                    foreach (var value in Enumerable.Range(1, 1_000))
                    {
                        // Pretend to do asynchronous work that observes cancellation
                        await Task.Yield();
                        token.ThrowIfCancellationRequested();

                        yield return value;
                    }
                }
            });

            // Consumer
            group.Run(async token =>
            {
                await foreach (var value in channel.WithCancellation(token))
                {
                    if (value == 13)
                        throw new InvalidOperationException("Oh, no!");
                }
            });

            // Consumer
            group.Run(async token =>
            {
                await foreach (var value in channel.WithCancellation(token))
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
    }

    [Fact]
    public async Task Pipeline()
    {
        var resultTask = CalculateUsingTemporaryPipelineAsync();

        async Task<double> CalculateUsingTemporaryPipelineAsync()
        {
            await using var group = new TaskGroup();

            // All the channels and transformation methods are asynchronously
            // scoped to this "CalculateUsingTemporaryPipelineAsync" method.
            // If there are any exceptions in any of them, all of them are
            // cancelled and all cancellation is completed before rethrowing the
            // original exception.

            // Producer
            var channel1 = group.RunSequence(token =>
            {
                return Impl();
                async IAsyncEnumerable<int> Impl()
                {
                    foreach (var value in Enumerable.Range(1, 1_000))
                    {
                        // Pretend to do asynchronous work that observes cancellation
                        await Task.Yield();
                        token.ThrowIfCancellationRequested();

                        yield return value;
                    }
                }
            });

            // Transformer 1
            var channel2 = group.RunSequence(token =>
            {
                return Impl();
                async IAsyncEnumerable<int> Impl()
                {
                    await foreach (var value in channel1.WithCancellation(token))
                        yield return value / 2;
                }
            });

            // Transformer 2
            var channel3 = group.RunSequence(token =>
            {
                return Impl();
                async IAsyncEnumerable<double> Impl()
                {
                    await foreach (var value in channel2.WithCancellation(token))
                        yield return value * 3.0;
                }
            });

            return await group.Run(async token =>
            {
                var result = 0.0;
                await foreach (var value in channel3.WithCancellation(token))
                    result += value;
                return result;
            });
        }

        Assert.Equal(750000, await resultTask);
    }

    [Fact]
    public async Task ExplicitCancel()
    {
        var resultTask = CalculateUsingTemporaryPipelineAsync();

        async Task<double> CalculateUsingTemporaryPipelineAsync()
        {
            await using var group = new TaskGroup();

            // All the channels and transformation methods are asynchronously
            // scoped to this "CalculateUsingTemporaryPipelineAsync" method.
            // If there are any exceptions in any of them, all of them are
            // cancelled and all cancellation is completed before rethrowing the
            // original exception.

            // Producer
            var channel1 = group.RunSequence(token =>
            {
                return Impl();
                async IAsyncEnumerable<int> Impl()
                {
                    foreach (var value in Enumerable.Range(1, 1_000))
                    {
                        // Pretend to do asynchronous work that observes cancellation
                        await Task.Yield();
                        token.ThrowIfCancellationRequested();

                        yield return value;
                    }
                }
            });

            // Transformer 1
            var channel2 = group.RunSequence(token =>
            {
                return Impl();
                async IAsyncEnumerable<int> Impl()
                {
                    await foreach (var value in channel1.WithCancellation(token))
                        yield return value / 2;
                }
            });

            // Transformer 2
            var channel3 = group.RunSequence(token =>
            {
                return Impl();
                async IAsyncEnumerable<double> Impl()
                {
                    await foreach (var value in channel2.WithCancellation(token))
                        yield return value * 3.0;
                }
            });

            // Oh, hey, we don't need this pipeline after all.
            group.CancellationTokenSource.Cancel();
            return 42;
        }

        Assert.Equal(42, await resultTask);
    }
}

