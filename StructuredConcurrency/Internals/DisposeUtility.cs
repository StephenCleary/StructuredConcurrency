using Nito.Disposables;

namespace Nito.StructuredConcurrency.Internals;

public static class DisposeUtility
{
    public static IAsyncDisposable? Wrap(IDisposable? disposable) => disposable == null ? null : new IgnoreExceptionsDisposeWrapper(disposable.ToAsyncDisposable());
    public static IAsyncDisposable? Wrap(IAsyncDisposable? disposable) => disposable == null ? null : new IgnoreExceptionsDisposeWrapper(disposable);

    public static IAsyncDisposable WrapStandalone(object? resource) =>
        resource is IDisposable disposable ? Wrap(disposable)! :
        resource is IAsyncDisposable asyncDisposable ? Wrap(asyncDisposable)! :
        NoopDisposable.Instance;

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
