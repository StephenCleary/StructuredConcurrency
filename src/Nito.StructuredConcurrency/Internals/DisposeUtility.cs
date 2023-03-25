using Nito.Disposables;

namespace Nito.StructuredConcurrency.Internals;

public static class DisposeUtility
{
    public static IAsyncDisposable? Wrap(IDisposable? disposable) => disposable == null ? null : new IgnoreExceptionsDisposeWrapper(disposable.ToAsyncDisposable());
    public static IAsyncDisposable? Wrap(IAsyncDisposable? disposable) => disposable == null ? null : new IgnoreExceptionsDisposeWrapper(disposable);

    public static IAsyncDisposable WrapStandalone(object? resource) => TryWrapStandalone(resource) ?? NoopDisposable.Instance;

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
            try
            {
                await _asyncDisposable.DisposeAsync().ConfigureAwait(false);
            }
            catch
            {
                // Ignore exceptions during disposal.
            }
        }

        private readonly IAsyncDisposable _asyncDisposable;
    }
}
