using Nito.Disposables;

namespace Nito.StructuredConcurrency.Internals;

/// <summary>
/// Utility methods for disposing resources.
/// </summary>
public static class DisposeUtility
{
    /// <summary>
    /// Wraps an <see cref="IDisposable"/> in an <see cref="IAsyncDisposable"/> that ignores exceptions.
    /// Returns <c>null</c> if <paramref name="disposable"/> is <c>null</c>.
    /// </summary>
    /// <param name="disposable">The disposable to wrap.</param>
    public static IAsyncDisposable? Wrap(IDisposable? disposable) => disposable == null ? null : new IgnoreExceptionsDisposeWrapper(disposable.ToAsyncDisposable());

    /// <summary>
    /// Wraps an <see cref="IDisposable"/> in an <see cref="IAsyncDisposable"/> that ignores exceptions.
    /// Returns <c>null</c> if <paramref name="disposable"/> is <c>null</c>.
    /// </summary>
    /// <param name="disposable">The disposable to wrap.</param>
    public static IAsyncDisposable? Wrap(IAsyncDisposable? disposable) => disposable == null ? null : new IgnoreExceptionsDisposeWrapper(disposable);

    /// <summary>
    /// Wraps a resource in an <see cref="IAsyncDisposable"/> that ignores exceptions.
    /// Returns <see cref="NoopDisposable"/> if <paramref name="resource"/> is <c>null</c>.
    /// </summary>
    /// <param name="resource">The resource to wrap.</param>
    public static IAsyncDisposable WrapStandalone(object? resource) => TryWrapStandalone(resource) ?? NoopDisposable.Instance;

    /// <summary>
    /// Wraps a resource in an <see cref="IAsyncDisposable"/> that ignores exceptions.
    /// Returns <c>null</c> if <paramref name="resource"/> is <c>null</c>.
    /// </summary>
    /// <param name="resource">The resource to wrap.</param>
    public static IAsyncDisposable? TryWrapStandalone(object? resource) =>
        resource is IDisposable disposable ? Wrap(disposable)! :
        resource is IAsyncDisposable asyncDisposable ? Wrap(asyncDisposable)! :
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
