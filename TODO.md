- Can we do something like Trio's cancel scopes for timeouts?
- Finish HappyEyeballs
- Write TCP/IP chat app for comparison.
  - Pipelines, etc.
- Move usage examples to readme.
- Do we need to hide advanced APIs?
- Clean up those exception stack traces!

- Do we actually need child task groups?
  - Do we need a way to ignore errors from child task groups?

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
  void X(Func<TaskGroup, ValueTask>); // cancellation is ignored
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
  void Run(Func<TaskGroup, ValueTask>); // cancellation is ignored
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
  void Run(Func<TaskGroup, ValueTask>); // cancellation is ignored
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
  void Run(Func<TaskGroup, ValueTask>); // cancellation is ignored
  Task EvaluateAsync(Func<TaskGroup, ValueTask>); // cancellation cancels returned task
  Task<T> EvaluateAsync(Func<TaskGroup, ValueTask<T>>); // cancellation cancels returned task
- Child groups:
  void SpawnRun(Func<TaskGroup, ValueTask>); // cancellation is ignored
  // TODO: parallel for top-level work missing here
  Task<T> SpawnEvaluateRunAsync(Func<TaskGroup, ValueTask<T>>); // cancellation cancels returned task
- Races:
  Task<T> SpawnEvaluateRaceAsync(Func<RacingTaskGroup<T>, ValueTask); // cancellation cancels returned task
  void Race(Func<CancellationToken, ValueTask<T>>); // cancellation is ignored
