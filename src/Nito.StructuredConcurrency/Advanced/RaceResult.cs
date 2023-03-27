using System.Collections.Immutable;
using Nito.StructuredConcurrency.Internals;

namespace Nito.StructuredConcurrency.Advanced;

/// <summary>
/// This type allows multiple asynchronous methods to race to provide a result.
/// </summary>
public sealed class RaceResult<TResult>
{
    private State _state = new(ImmutableQueue<Exception>.Empty, default!, false);

    /// <summary>
    /// Reports a successful result. If this is the first result, it becomes the result of the race; otherwise, it is disposed (if possible) and discarded.
    /// </summary>
    /// <param name="result">The successful result.</param>
    public async Task ReportResultAsync(TResult result)
    {
        bool wonRace = false;
        InterlockedEx.Apply(ref _state, x => x switch
        {
            { Done: true } => InterlockedEx.SetAndReturn(out wonRace, false, x),
            _ => InterlockedEx.SetAndReturn(out wonRace, true, x with { Done = true, Result = result, Exceptions = ImmutableQueue<Exception>.Empty }),
        });
        if (!wonRace)
            await DisposeUtility.Wrap(result).DisposeAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Reports an exception. If a successful report exists, then this exception is ignored; otherwise, it is part of a collection of exceptions raised later.
    /// </summary>
    /// <param name="exception">The exception to report.</param>
    public void ReportException(Exception exception)
    {
        InterlockedEx.Apply(ref _state, x => x switch
        {
            { Done: true } => x,
            _ => x with { Exceptions = x.Exceptions.Enqueue(exception) },
        });
    }

    /// <summary>
    /// Retrieves the results of this race. This may only be called after all possible racers have completed.
    /// If no racers participated at all, then this throws <see cref="OperationCanceledException"/>.
    /// If all racers failed, then the returned task contains all of the racer exceptions, in timeline order.
    /// </summary>
    public TResult GetResult()
    {
        bool resultValid = false;
        var localState = InterlockedEx.Apply(ref _state, x => x switch
        {
            { Done: true } => InterlockedEx.SetAndReturn(out resultValid, true, x),
            _ => InterlockedEx.SetAndReturn(out resultValid, false, x with { Done = true }),
        });
        if (!localState.Exceptions.IsEmpty)
            throw new AggregateException(localState.Exceptions);
        if (resultValid)
            return localState.Result;
        throw new OperationCanceledException("No result from race.");
    }

    private record class State(ImmutableQueue<Exception> Exceptions, TResult Result, bool Done);
}
