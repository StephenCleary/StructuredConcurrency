namespace Nito.StructuredConcurrency.Internals;

public static class InterlockedEx
{
    public static T Apply<T>(ref T value, Func<T, T> transformation)
        where T : class
    {
        while (true)
        {
            var localValue = Interlocked.CompareExchange(ref value, null!, null!);
            var modified = transformation(localValue);
            if (Interlocked.CompareExchange(ref value, modified, localValue) == localValue)
                return modified;
        }
    }

    public static TResult SetAndReturn<TValue, TResult>(out TValue location, TValue value, TResult result)
    {
        location = value;
        return result;
    }
}
