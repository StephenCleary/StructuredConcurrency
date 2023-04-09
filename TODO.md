- TaskGroup should have a synchronous AddResource for IDisposable types.
  - Maybe a TryAddResource for both IDisposable and IAsyncDisposable?
- Finish HappyEyeballs
- Move usage examples to readme.

- Open question: is there value in an ITaskGroup interface?


### Results

Most work has no results, but it is possible for a work item to return a single value.
Work that returns a value is initiated by calling `RunAsync`, which returns an awaitable result.
Reminder: if you are returning these results outside the task group scope, then the task group must complete all its work before that scope is complete.

## Channels

## Child Task Groups

Task groups may spawn "child" task groups by starting a new task group and passing the parent group's cancellation to it. If a parent task group is cancelled, that cancellation flows down and cancels its child task groups.

Exceptions also flow "up" from child task groups to parent task groups.
When a child task group's work faults, the exception will cancel the child task group and will cause the child task group to throw an exception at the end of its scope.
This exception from the child task group's scope will be ignored by the parent task group.
