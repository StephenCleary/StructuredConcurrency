![Logo](src/icon.png)

# Structured Concurrency [![Build status](https://github.com/StephenCleary/StructuredConcurrency/workflows/Build/badge.svg)](https://github.com/StephenCleary/StructuredConcurrency/actions?query=workflow%3ABuild) [![codecov](https://codecov.io/gh/StephenCleary/StructuredConcurrency/branch/master/graph/badge.svg)](https://codecov.io/gh/StephenCleary/StructuredConcurrency) [![NuGet version](https://badge.fury.io/nu/Nito.StructuredConcurrency.svg)](https://www.nuget.org/packages/Nito.StructuredConcurrency)
Structured Concurrency for C#.

## Task Groups

A task group provides a scope in which work is done.
At the end of that scope, the task group (asynchronously) waits for all of its work to complete.

A `TaskGroup` is started with `TaskGroup.RunGroupAsync`.
The delegate passed to `RunGroupAsync` is the first work item; it can run any other work items in that same group.
When all the work items have completed, then the group scope closes, and the task returned from `RunGroupAsync` completes.

Work may be added to a task group at any time by calling `Run`, as long as the scope has not completed.
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
Task groups also take a `CancellationToken` as parameter to the static `RunGroupAsync` methods to enable cancellation from "upstream"; e.g., if the application is shutting down.
Task groups can also be cancelled manually (via `TaskGroup.CancellationTokenSource`) if the program logic wishes to stop the task group for any reason.

### Resources

A task group can own resources.
These resources will be disposed by the task group after all its work is done.

All exceptions raised by disposal of any resource are ignored.

### Results

Most work has no results, but it is possible for a work item to return a single value.
Work that returns a value is initiated by calling `RunAsync`, which returns an awaitable result.
Reminder: if you are returning these results outside the task group scope, then the task group must complete all its work before that scope is complete.

Result values are not treated as resources; their lifetime is not scoped to the task group.

### Sequences

Work can return multiple values by returning `IAsyncEnumerable<T>`.
The work itself is queued as normal, and begins producing values immediately.
The values go into a bounded channel.

It's not possible to return sequences outside the task group, since the task group must complete (and thus the sequence must be complete).
It is possible to have task group work explicitly write to a channel, or collect all the results and return them once the sequence (and thus the work) is complete.

Sequence values that are produced after group cancellation are treated as resources and disposed immediately.

If your sequence values are resources, please ensure they are disposed properly, and do not cancel reading from the sequence.

### Races

The usual pattern for task groups is to cancel on failure and ignore success.
Sometimes, we want to "race" several work items to produce a result; in this case, we want the opposite: ignore failures and cancel on success.

The usual pattern is to create a race child group via `TaskGroup.RaceGroupAsync`.
This creates a separate group along with a race result that are used for races.
To race work, call `Race` instead of `Run`.
The first successful `Race` will cancel all the others.
Once all races have completed (i.e., the race child group's scope is complete), then the results of the race are returned from `RaceGroupAsync`.

Successful results that lose the race are treated as resources, but are disposed immediately rather than scoped to the race child group.
