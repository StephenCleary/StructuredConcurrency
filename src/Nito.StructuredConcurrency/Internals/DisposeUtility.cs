using Nito.Disposables;

namespace Nito.StructuredConcurrency.Internals;

/// <summary>
/// Utility methods for disposing resources.
/// </summary>
public static class DisposeUtility
{
    /// <summary>
    /// Wraps a resource in an <see cref="IAsyncDisposable"/> that ignores exceptions.
    /// Returns <see cref="NoopDisposable"/> if <paramref name="resource"/> is <c>null</c>.
    /// </summary>
    /// <param name="resource">The resource to wrap.</param>
    public static IAsyncDisposable Wrap(object? resource) => TryWrap(resource) ?? NoopDisposable.Instance;

    /// <summary>
    /// Wraps a resource in an <see cref="IAsyncDisposable"/> that ignores exceptions.
    /// Returns <c>null</c> if <paramref name="resource"/> is <c>null</c>.
    /// </summary>
    /// <param name="resource">The resource to wrap.</param>
    public static IAsyncDisposable? TryWrap(object? resource) =>
        resource is IDisposable disposable ? new IgnoreExceptionsDisposeWrapper(disposable.ToAsyncDisposable()) :
        resource is IAsyncDisposable asyncDisposable ? new IgnoreExceptionsDisposeWrapper(asyncDisposable) :
        null;

    private sealed class IgnoreExceptionsDisposeWrapper : IAsyncDisposable
    {
        public IgnoreExceptionsDisposeWrapper(IAsyncDisposable asyncDisposable)
        {
            _asyncDisposable = asyncDisposable;
        }

        public async ValueTask DisposeAsync()
        {
#pragma warning disable CA1031 // Do not catch general exception types
            try
            {
                await _asyncDisposable.DisposeAsync().ConfigureAwait(false);
            }
            catch
            {
                // Ignore exceptions during disposal.
            }
#pragma warning restore CA1031 // Do not catch general exception types
        }

        private readonly IAsyncDisposable _asyncDisposable;
    }
}
