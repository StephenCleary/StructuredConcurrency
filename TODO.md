- Can we do something like Trio's cancel scopes for timeouts?
- Finish HappyEyeballs
- Write TCP/IP chat app for comparison.
  - Pipelines, etc.
- Move usage examples to readme.
- Do we need to hide advanced APIs?
- Clean up those exception stack traces!

- Do we actually need child task groups?
  - Do we need a way to ignore errors from child task groups?
  - Or actually, a way to catch errors from child task groups and *optionally* pass them up.

## Child Task Groups

Task groups may spawn child task groups.
These behave differently from work that is attached to a task group.

Cancellation flows "down" from parent task groups to child task groups.
If a parent task group is cancelled, that cancellation flows down and cancels all child task groups.

Exceptions also flow "up" from child task groups to parent task groups.
When a child task group's work faults, the exception will cancel the child task group and will cause the child task group to throw an exception at the end of its scope.
This exception from the child task group's scope will be ignored by the parent task group.



Naming!!!!

Work APIs:

- Add work to the task group:
  void X(Func<CancellationToken, ValueTask>); // cancellation is ignored
- Work with results:
  Task<T> X(Func<CancellationToken, ValueTask<T>>) // cancellation is ignored by group but cancels returned task
  IAsyncEnumerable<T> X(Func<CancellationToken, IAsyncEnumerable<T>>) // cancellation is ignored by group but cancels returned sequence
- Top-level work:
  Task X(Func<TaskGroup, ValueTask>); // cancellation is ignored
  Task X(Func<TaskGroup, ValueTask>); // cancellation cancels returned task
  Task<T> X(Func<TaskGroup, ValueTask<T>>); // cancellation cancels returned task
- Child groups:
  void X(Func<TaskGroup, ValueTask>); // cancellation is ignored
  // TODO: parallel for top-level work missing here
  Task<T> X(Func<TaskGroup, ValueTask<T>>); // cancellation cancels returned task
- Races:
  Task<T> X(Func<RacingTaskGroup<T>, ValueTask); // cancellation cancels returned task
  void X(Func<CancellationToken, ValueTask<T>>); // cancellation is ignored

Thought: "execute" means "with results":

- Add work to the task group:
  void Run(Func<CancellationToken, ValueTask>); // cancellation is ignored
- Work with results:
  Task<T> ExecuteAsync(Func<CancellationToken, ValueTask<T>>) // cancellation is ignored by group but cancels returned task
  IAsyncEnumerable<T> ExecuteAsync(Func<CancellationToken, IAsyncEnumerable<T>>) // cancellation is ignored by group but cancels returned sequence
- Top-level work:
  Task Run(Func<TaskGroup, ValueTask>); // cancellation is ignored
  Task ExecuteAsync(Func<TaskGroup, ValueTask>); // cancellation cancels returned task
  Task<T> ExecuteAsync(Func<TaskGroup, ValueTask<T>>); // cancellation cancels returned task
- Child groups:
  void RunChild(Func<TaskGroup, ValueTask>); // cancellation is ignored
  // TODO: parallel for top-level work missing here
  Task<T> ExecuteChildAsync(Func<TaskGroup, ValueTask<T>>); // cancellation cancels returned task
- Races:
  Task<T> ExecuteRaceAsync(Func<RacingTaskGroup<T>, ValueTask); // cancellation cancels returned task
  void Race(Func<CancellationToken, ValueTask<T>>); // cancellation is ignored

Different aspects:
- work spawning a child group vs work as part of this group.
- work returning results vs no results. (Note: both are necessary even for work without results).
- results that can be returned vs results that must be consumed.

- Add work to the task group:
  void Run(Func<CancellationToken, ValueTask>); // cancellation is ignored
- Work with results:
  Task<T> ExecuteAsync(Func<CancellationToken, ValueTask<T>>) // cancellation is ignored by group but cancels returned task
  IAsyncEnumerable<T> RunSequence(Func<CancellationToken, IAsyncEnumerable<T>>) // cancellation is ignored by group but cancels returned sequence
- Top-level work:
  Task Run(Func<TaskGroup, ValueTask>); // cancellation is ignored
  Task ExecuteAsync(Func<TaskGroup, ValueTask>); // cancellation cancels returned task
  Task<T> ExecuteAsync(Func<TaskGroup, ValueTask<T>>); // cancellation cancels returned task
- Child groups:
  void SpawnRun(Func<TaskGroup, ValueTask>); // cancellation is ignored
  // TODO: parallel for top-level work missing here
  Task<T> SpawnExecuteRunAsync(Func<TaskGroup, ValueTask<T>>); // cancellation cancels returned task
- Races:
  Task<T> SpawnExecuteRaceAsync(Func<RacingTaskGroup<T>, ValueTask); // cancellation cancels returned task
  void Race(Func<CancellationToken, ValueTask<T>>); // cancellation is ignored

- Add work to the task group:
  void Run(Func<CancellationToken, ValueTask>); // cancellation is ignored
- Work with results:
  Task<T> EvaluateAsync(Func<CancellationToken, ValueTask<T>>) // cancellation is ignored by group but cancels returned task
  IAsyncEnumerable<T> RunSequence(Func<CancellationToken, IAsyncEnumerable<T>>) // cancellation is ignored by group but cancels returned sequence
- Top-level work:
  Task Run(Func<TaskGroup, ValueTask>); // cancellation is ignored
  Task EvaluateAsync(Func<TaskGroup, ValueTask>); // cancellation cancels returned task
  Task<T> EvaluateAsync(Func<TaskGroup, ValueTask<T>>); // cancellation cancels returned task
- Child groups:
  void SpawnRun(Func<TaskGroup, ValueTask>); // cancellation is ignored
  // TODO: parallel for top-level work missing here
  Task<T> SpawnEvaluateRunAsync(Func<TaskGroup, ValueTask<T>>); // cancellation cancels returned task
- Races:
  Task<T> SpawnEvaluateRaceAsync(Func<RacingTaskGroup<T>, ValueTask); // cancellation cancels returned task
  void Race(Func<CancellationToken, ValueTask<T>>); // cancellation is ignored

Drop Spawn for child groups:
- Add work to the task group:
  void Run(Func<CancellationToken, ValueTask>); // cancellation is ignored
- Work with results:
  Task<T> EvaluateAsync(Func<CancellationToken, ValueTask<T>>) // cancellation is ignored by group but cancels returned task
  IAsyncEnumerable<T> RunSequence(Func<CancellationToken, IAsyncEnumerable<T>>) // cancellation is ignored by group but cancels returned sequence
- Top-level work:
  Task RunAsync(Func<TaskGroup, ValueTask>); // cancellation is ignored
  Task EvaluateAsync(Func<TaskGroup, ValueTask>); // cancellation cancels returned task
  Task<T> EvaluateAsync(Func<TaskGroup, ValueTask<T>>); // cancellation cancels returned task
- Child groups:
  void RunChild(Func<TaskGroup, ValueTask>); // cancellation is ignored
  Task EvaluateRunAsync(Func<TaskGroup, ValueTask>); // cancellation cancels returned task
  Task<T> EvaluateRunAsync(Func<TaskGroup, ValueTask<T>>); // cancellation cancels returned task
- Races:
  Task<T> EvaluateRaceAsync(Func<RacingTaskGroup<T>, ValueTask); // cancellation cancels returned task
  void Race(Func<CancellationToken, ValueTask<T>>); // cancellation is ignored

Behaviors:
- Add work to this group that has no results.
  - Cancellation is ignored.
  - Exceptions cause group cancellation.
- Add work to this group that has a single result.
  - Cancellation is ignored by the group.
  - Cancellation cancels the result.
  - Exceptions cause group cancellation.
  - Exceptions fault the result.
  - Result can exit group.
- Add work to this group that has multiple results.
  - Cancellation is ignored by the group.
  - Cancellation cancels the result.
  - Exceptions cause group cancellation.
  - Exceptions fault the result.
  - Result must stay in group.
- Add race work to this group. (never has results)
  - Cancellation is ignored.
  - Success cause group cancellation.
  - Exceptions are usually ignored unless all races fault.
- Spawn child group and add work that has no results.
  - Cancellation is ignored. (TODO: ensure this is the case)
  - Faulted child group cancels parent group.
- Spawn child group and add work that has a single result.
  - Cancellation is ignored by the group.
  - Cancellation cancels the result.
  - Exceptions cancel parent group.
  - Exceptions fault the result.
  - Result can exit group.
- Start new group with work that has no results and ignores cancellation.
  - Cancellation is ignored.
  - Faulted group results in exception.
- Start new group with work that has no results and reports cancellation.
  - Cancelled group cancels Task.
  - Faulted group faults Task.
- Start new group with work that has results. (implied: reports cancellation)
  - Cancelled group cancels Task.
  - Faulted group faults Task.

- Add work to the task group:
  void Run(Func<CancellationToken, ValueTask>); // cancellation is ignored
- Work with results:
  Task<T> EvaluateAsync(Func<CancellationToken, ValueTask<T>>) // cancellation is ignored by group but cancels returned task
  IAsyncEnumerable<T> RunSequence(Func<CancellationToken, IAsyncEnumerable<T>>) // cancellation is ignored by group but cancels returned sequence
- Child groups:
  void RunChild(Func<TaskGroup, ValueTask>); // cancellation is ignored
  Task<T> EvaluateRunAsync(Func<TaskGroup, ValueTask<T>>); // cancellation cancels returned task
- Races:
  Task<T> EvaluateRaceAsync(Func<RacingTaskGroup<T>, ValueTask); // cancellation cancels returned task
  void Race(Func<CancellationToken, ValueTask<T>>); // cancellation is ignored
- Top-level work:
  Task RunAsync(Func<TaskGroup, ValueTask>); // cancellation is ignored
  Task EvaluateAsync(Func<TaskGroup, ValueTask>); // cancellation cancels returned task
  Task<T> EvaluateAsync(Func<TaskGroup, ValueTask<T>>); // cancellation cancels returned task

- Add work to the task group:
  void Run(Func<CancellationToken, ValueTask>); // cancellation is ignored
- Work with results:
  Task<T> EvaluateAsync(Func<CancellationToken, ValueTask<T>>) // cancellation is ignored by group but cancels returned task
  IAsyncEnumerable<T> RunSequence(Func<CancellationToken, IAsyncEnumerable<T>>) // cancellation is ignored by group but cancels returned sequence
- Child groups:
  void Spawn(Func<TaskGroup, ValueTask>); // cancellation is ignored
  Task<T> SpawnEvaluateAsync(Func<TaskGroup, ValueTask<T>>); // cancellation cancels returned task
- Races:
  Task<T> SpawnRaceAsync(Func<RacingTaskGroup<T>, ValueTask); // cancellation cancels returned task
  void Race(Func<CancellationToken, ValueTask<T>>); // cancellation is ignored
- Top-level work:
  Task RunAsync(Func<TaskGroup, ValueTask>); // cancellation is ignored
  Task EvaluateAsync(Func<TaskGroup, ValueTask>); // cancellation cancels returned task
  Task<T> EvaluateAsync(Func<TaskGroup, ValueTask<T>>); // cancellation cancels returned task

- Can a child group return multiple results? Is that useful?

- Add work to the task group:
  void Run(Func<CancellationToken, ValueTask>); // cancellation is ignored
- Work with results:
  Task<T> EvaluateAsync(Func<CancellationToken, ValueTask<T>>) // cancellation is ignored by group but cancels returned task
  IAsyncEnumerable<T> RunSequence(Func<CancellationToken, IAsyncEnumerable<T>>) // cancellation is ignored by group but cancels returned sequence
- Child groups:
  void Spawn(Func<TaskGroup, ValueTask>); // cancellation is ignored
  Task<T> SpawnRunAsync(Func<TaskGroup, ValueTask<T>>); // cancellation cancels returned task
- Races:
  Task<T> SpawnRaceAsync(Func<RacingTaskGroup<T>, ValueTask); // cancellation cancels returned task
  void Race(Func<CancellationToken, ValueTask<T>>); // cancellation is ignored
- Top-level work:
  Task RunAsync(Func<TaskGroup, ValueTask>); // cancellation is ignored
  Task EvaluateAsync(Func<TaskGroup, ValueTask>); // cancellation cancels returned task
  Task<T> EvaluateAsync(Func<TaskGroup, ValueTask<T>>); // cancellation cancels returned task

Dropping the top-level work that ignores cancellation:
- Add work to the task group:
  void Run(Func<CancellationToken, ValueTask>); // cancellation is ignored
- Work with results:
  Task<T> RunAsync(Func<CancellationToken, ValueTask<T>>) // cancellation is ignored by group but cancels returned task
  IAsyncEnumerable<T> RunSequence(Func<CancellationToken, IAsyncEnumerable<T>>) // cancellation is ignored by group but cancels returned sequence
- Child groups:
  void Spawn(Func<TaskGroup, ValueTask>); // cancellation is ignored
  Task<T> SpawnAsync(Func<TaskGroup, ValueTask<T>>); // cancellation cancels returned task
- Races:
  Task<T> SpawnRaceAsync(Func<RacingTaskGroup<T>, ValueTask); // cancellation cancels returned task
  void Race(Func<CancellationToken, ValueTask<T>>); // cancellation is ignored
- Top-level work:
  Task RunAsync(Func<TaskGroup, ValueTask>); // cancellation cancels returned task
  Task<T> RunAsync(Func<TaskGroup, ValueTask<T>>); // cancellation cancels returned task

Try "Work" and "Race" instad of "Run" and "Race", and make "Run" mean "result" instead:
- Add work to the task group:
  void Work(Func<CancellationToken, ValueTask>); // cancellation is ignored
- Work with results:
  Task<T> RunAsync(Func<CancellationToken, ValueTask<T>>) // cancellation is ignored by group but cancels returned task
  IAsyncEnumerable<T> RunSequence(Func<CancellationToken, IAsyncEnumerable<T>>) // cancellation is ignored by group but cancels returned sequence
- Child groups:
  void SpawnWork(Func<TaskGroup, ValueTask>); // cancellation is ignored
  Task<T> SpawnRunAsync(Func<TaskGroup, ValueTask<T>>); // cancellation cancels returned task
- Races:
  Task<T> SpawnRaceAsync(Func<RacingTaskGroup<T>, ValueTask); // cancellation cancels returned task
  void Race(Func<CancellationToken, ValueTask<T>>); // cancellation is ignored
- Top-level work:
  Task WorkAsync(Func<TaskGroup, ValueTask>); // cancellation is ignored
  Task RunAsync(Func<TaskGroup, ValueTask>); // cancellation cancels returned task
  Task<T> RunAsync(Func<TaskGroup, ValueTask<T>>); // cancellation cancels returned task

Final meanings:
- "Work" adds work to the group. Cancellation is ignored; exceptions fault the group.
- "RunAsync" adds work with a result to the group. Cancellation and exceptions operate like Work and are also reported on the returned task.
- "RunSequence" adds work with multiple results to the group. Cancellation and exceptions operate like Work and are also reported on the returned sequence.
- "Spawn" starts a child task.
  - "Spawn" + "Work" starts a child group with "Work" semantics.
  - "Spawn" + "RunAsync" starts a child group with "RunAsync" semantics.
- "Race" adds racing to the group. Cancellation is ignored; exceptions are ignored; success cancels the group.
- "Spawn" + "RaceAsync" starts a child group with "Race" semantics and returns the result.
- (top-level) "WorkAsync" starts a new group with "Work" semantics, ignoring cancellation.
- (top-level) "RunAsync" starts a new group with "RunAsync" semantics.


The whole point behind top-level RunAsync and friends is because:
await using (var group = new TaskGroup(...))
{
  // This code in here is not actually part of the group. E.g., exceptions don't cancel the group; cancellation isn't ignored.
}
so instead we do something like this:
await using (var group = new TaskGroup(...))
{
  group.Run(/* end-user code goes here */);
}

But with child groups, we want the ability to do a try/catch around the group disposal, i.e.,:
try
{
  await using (var childGroup = new TaskGroup(parentGroup.CancellationToken))
  {
    childGroup.Run(/* end-user code goes here */);
  }
}
catch
{
  /* end-user code also goes here, and may propagate the exception or do something completely different with it */
}
although, really, propagating exceptions is almost never useful. Child groups would always do something else with it instead.

Spawn(go => try { await go(); } catch { Console.WriteLine(ex); }, async childGroup => { ... });
or:
Work(_ => try { await group.SpawnAsync(async g => { ... }); } catch { Console.WriteLine(ex); });
or (with a new kind of "child controller" type with an API more similar to the top-level API):
Spawn(c => try { await c.RunAsync(async g => { ... }); } catch { Console.WriteLine(ex); }); // work semantics
SpawnAsync(c => try { return await c.RaceAsync(async g => { ... }); } catch { Console.WriteLine(ex); }); // runasync semantics
but if you *always* want a try/catch *anyway*, then really, this meh-seeming API may be best:
Spawn(async g => { ... }, async ex => Console.WriteLine(ex));

Or: what if child groups just use the top-level group API inside a work delegate?
Work(ct => await TaskGroup.RunAsync(async g => { ... }, ct));
Work(ct => try { await TaskGroup.RunAsync(async g => { ... }, ct); } catch { Console.WriteLine(ex); } );
"child groups" are no longer a special thing at all!

- Add work to the task group:
  void Work(Func<CancellationToken, ValueTask>); // cancellation is ignored
  Task<T> WorkAsync(Func<CancellationToken, ValueTask<T>>) // cancellation is ignored by group but cancels returned task
  IAsyncEnumerable<T> WorkSequence(Func<CancellationToken, IAsyncEnumerable<T>>) // cancellation is ignored by group but cancels returned sequence
- Races:
  void Race(Func<CancellationToken, ValueTask<T>>); // cancellation is ignored
- Top-level work: (require explicit CancellationToken)
  Task RunAsync(Func<TaskGroup, ValueTask>); // cancellation cancels returned task
  Task<T> RunAsync(Func<TaskGroup, ValueTask<T>>); // cancellation cancels returned task
  Task<T> RaceAsync(Func<RacingTaskGroup<T>, ValueTask); // cancellation cancels returned task

Use Run for instance methods:
- Add work to the task group:
  void Run(Func<CancellationToken, ValueTask>); // cancellation is ignored
  Task<T> RunAsync(Func<CancellationToken, ValueTask<T>>) // cancellation is ignored by group but cancels returned task
  IAsyncEnumerable<T> RunSequence(Func<CancellationToken, IAsyncEnumerable<T>>) // cancellation is ignored by group but cancels returned sequence
- Races:
  void Race(Func<CancellationToken, ValueTask<T>>); // cancellation is ignored
- Top-level work: (require explicit CancellationToken)
  Task RunGroupAsync(Func<TaskGroup, ValueTask>); // cancellation cancels returned task
  Task<T> RunGroupAsync(Func<TaskGroup, ValueTask<T>>); // cancellation cancels returned task
  Task<T> RaceGroupAsync(Func<RacingTaskGroup<T>, ValueTask); // cancellation cancels returned task
