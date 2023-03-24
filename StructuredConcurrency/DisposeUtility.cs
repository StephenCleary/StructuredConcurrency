using Nito.Disposables;

namespace Nito.StructuredConcurrency.Internals;

public static class DisposeUtility
{
    public static IAsyncDisposable CreateAsyncDisposable(params object?[] resources)
    {
        if (resources.Length == 0)
            return NoopDisposable.Instance;
        var disposableResources = resources
            .Select(x => x is IAsyncDisposable asyncDisposable ? asyncDisposable : x is IDisposable disposable ? disposable.ToAsyncDisposable() : null)
            .Where(x => x != null)
            .Select(x => new IgnoreExceptionsDisposeWrapper(x!))
            .ToList();
        if (disposableResources.Count == 0)
            return NoopDisposable.Instance;

        return new CollectionAsyncDisposable(disposableResources);
    }

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
