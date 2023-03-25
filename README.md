# Structured Concurrency

## Task Groups

A task group provides a scope in which work is done.
At the end of that scope, the task group (asynchronously) waits for all of its work to complete.

In code, this is structured as the `TaskGroup` implementing `IAsyncDisposable`, so it can be used in an `await using` block that has the same scope as the task group scope.

Work may be added to a task group at any time, as long as the scope has not completed.
Conceptually, the task group scope ends with a kind of `Task.WhenAll`, but with the important difference that more work may be added after the disposal begins.
As long as the work is added before all other work completes, the task group will "extend" its logical `WhenAll` to include the additional work.

### Exceptions

If any work throws an exception (except `OperationCanceledException`), then that work is considered "faulted".
The task group immediately enters a canceled state (see below), cancelling all of its other work.

At the end of the task group scope, the task group will still wait for all of its work to complete.
Once all of the work has completed, then the task group disposal will re-raise the first exception from its faulted work.

### Cancellation

Task groups always ignore any work that is cancelled (i.e., task groups catch and ignore `OperationCanceledException`).

Task groups provide `CancellationToken` parameters to all of their work, and it is the work's responsibility to respond to that cancellation.

The task group will cancel itself if any work item faults.
Task groups also take a `CancellationToken` in their constructor to enable cancellation from "upstream"; e.g., if the application is shutting down.
Task groups can also be cancelled manually (via `TaskGroup.Cancel()`) if the program logic wishes to stop the task group for any reason.

### Resources

A task group can own resources.
These resources will be disposed by the task group after all its work is done.

All exceptions raised by disposal of any resource are ignored.

### Results

Most work has no results, but it is possible for work toS return a single value.
Work that returns a value uses an overload of `TaskGroup.Run` that returns an awaitable result.
Reminder: if you are returning these results outside the task group scope, then the task group must complete all its work before that scope is complete.

Result values are not treated as resources; their lifetime is not scoped to the task group.

### Sequences

Work can return multiple values by returning `IAsyncEnumerable<T>`.
The work itself is queued as normal, and begins producing values immediately.
The values go into a bounded channel.

It's not possible to return sequences outside the task group, since the task group must complete (and thus the sequence must be complete).
It is possible to have task group work explicitly write to a channel, or collect all the results and return them once the sequence (and thus the work) is complete.

Sequence values are treated as resources and are scoped to the task group.
If you need a sequence value to outlast the task group, return a sequence of reference counted disposables.

### Races

The usual pattern for task groups is to cancel on failure and ignore success.
Sometimes, we want to "race" several work items to produce a result; in this case, we want the opposite: ignore failures and cancel on success.

The usual pattern is to create a race child group via `TaskGroup.RaceChildGroup`.
This creates a separate group along with a race result that are used for races.
To race work, call `Race` instead of `Run`.
The first successful `Race` will cancel all the others.
Once all races have completed (i.e., the race child group's scope is complete), then the results of the race are returned from `RaceChildGroup`.

Successful results that lose the race are treated as resources, but are disposed immediately rather than scoped to the race child group.

# Advanced

## Child Task Groups

Task groups may spawn child task groups.
These behave differently from work that is attached to a task group.

Cancellation flows "down" from parent task groups to child task groups.
If a parent task group is cancelled, that cancellation flows down and cancels all child task groups.

However, exceptions do not flow "up" from child task groups to parent task groups.
When a child task group's work faults, the exception will cancel the child task group and will cause the child task group to throw an exception at the end of its scope.
This exception from the child task group's scope will be ignored by the parent task group.
