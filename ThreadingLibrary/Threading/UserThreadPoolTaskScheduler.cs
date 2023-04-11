namespace Mericle.Threading;

using System;
using System.Collections.Generic;
using System.Threading.Tasks;


public class UserThreadPoolTaskScheduler : TaskScheduler
{
    protected override IEnumerable<Task>? GetScheduledTasks()
    {
        throw new NotImplementedException();
    }

    protected override void QueueTask(Task task)
    {
        throw new NotImplementedException();
    }

    protected override bool TryExecuteTaskInline(Task task, bool taskWasPreviouslyQueued)
    {
        throw new NotImplementedException();
    }
}
