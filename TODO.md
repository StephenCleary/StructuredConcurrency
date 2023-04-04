- TaskGroup should have a synchronous AddResource for IDisposable types.
  - Maybe a TryAddResource for both IDisposable and IAsyncDisposable?
- Finish HappyEyeballs
- Move usage examples to readme.

- Open question: is there value in an ITaskGroup interface?

## Child Task Groups

Task groups may spawn child task groups.
These behave differently from work that is attached to a task group.

Cancellation flows "down" from parent task groups to child task groups.
If a parent task group is cancelled, that cancellation flows down and cancels all child task groups.

Exceptions also flow "up" from child task groups to parent task groups.
When a child task group's work faults, the exception will cancel the child task group and will cause the child task group to throw an exception at the end of its scope.
This exception from the child task group's scope will be ignored by the parent task group.
