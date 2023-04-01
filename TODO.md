- The whole sequence-output-as-resources thing doesn't sit well.
  - E.g., TCP server will have its resources grow without bound over time.
  - Instead, can we drain sequences at group shutdown?
  => Problem: If we force items to be published, consumers cancel and we get a deadlock.
  => Can we just remove the channel / special behavior completely?
  => Or we can have the "sequence" returned from the producer just have a RunConsumer method to force them into a separate method?
     - That would allow for draining resource (and other) queues.
     - Natural API would use a captured group. Should we allow overriding the group?
- Can we do something like Trio's cancel scopes for timeouts?
- Finish HappyEyeballs
- Write TCP/IP chat app for comparison.
  - Pipelines, etc.
- Move usage examples to readme.

## Child Task Groups

Task groups may spawn child task groups.
These behave differently from work that is attached to a task group.

Cancellation flows "down" from parent task groups to child task groups.
If a parent task group is cancelled, that cancellation flows down and cancels all child task groups.

Exceptions also flow "up" from child task groups to parent task groups.
When a child task group's work faults, the exception will cancel the child task group and will cause the child task group to throw an exception at the end of its scope.
This exception from the child task group's scope will be ignored by the parent task group.
