namespace Nito.StructuredConcurrency.Internals;

/// <summary>
/// Interlocked helper methods.
/// </summary>
#pragma warning disable CA1711 // Identifiers should not have incorrect suffix
public static class InterlockedEx
#pragma warning restore CA1711 // Identifiers should not have incorrect suffix
{
    /// <summary>
    /// Executes a state transition from one state to another.
    /// </summary>
    /// <typeparam name="T">The type of the state; this is generally an immutable type.</typeparam>
    /// <param name="value">The location of the state.</param>
    /// <param name="transformation">The transformation to apply to the state. This may be invoked any number of times and should be a pure function.</param>
    /// <returns>The new state.</returns>
    public static T Apply<T>(ref T value, Func<T, T> transformation)
        where T : class
    {
        _ = transformation ?? throw new ArgumentNullException(nameof(transformation));

        while (true)
        {
            var localValue = Interlocked.CompareExchange(ref value, null!, null!);
            var modified = transformation(localValue);
            if (Interlocked.CompareExchange(ref value, modified, localValue) == localValue)
                return modified;
        }
    }

    /// <summary>
    /// Sets a value and then returns another value.
    /// This is useful in <c>switch</c> expressions used with <see cref="Apply"/> when the calling code needs to know which branch was taken in the <c>switch</c> expression.
    /// </summary>
    /// <typeparam name="TValue">The type of the value. This is usually an integer or boolean type.</typeparam>
    /// <typeparam name="TResult">The type of the return value. This is the same as the state type if used with <see cref="Apply"/>.</typeparam>
    /// <param name="location">The location of the value to set.</param>
    /// <param name="value">The value to write to <paramref name="location"/>.</param>
    /// <param name="result">The value returned from this method.</param>
    public static TResult SetAndReturn<TValue, TResult>(out TValue location, TValue value, TResult result)
    {
        location = value;
        return result;
    }
}
