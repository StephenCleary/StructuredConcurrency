namespace Nito.StructuredConcurrency.Internals;

/// <summary>
/// Delegate helper methods.
/// </summary>
public static class DelegateExtensions
{
    /// <summary>
    /// Adds a default return value to the delegate.
    /// </summary>
    public static Func<TArg1, ValueTask<object>> WithResult<TArg1>(this Func<TArg1, ValueTask> d)
    {
        return async arg =>
        {
            await d(arg).ConfigureAwait(false);
            return null!;
        };
    }

    /// <summary>
    /// Converts a delegate to run asynchronously.
    /// </summary>
    public static Func<TArg1, ValueTask> AsAsync<TArg1>(this Action<TArg1> d)
    {
#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
        return async arg => d(arg);
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
    }

    /// <summary>
    /// Converts a delegate to run asynchronously.
    /// </summary>
    public static Func<TArg1, ValueTask<TResult>> AsAsync<TArg1, TResult>(this Func<TArg1, TResult> d)
    {
#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
        return async arg => d(arg);
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
    }
}
