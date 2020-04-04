using System;
using System.Collections.Generic;
using System.Text;

namespace IdleScheduler.CycleTaskHandlers
{
    public class MemoryHandler : ICycleTaskHandler
    {
        public IEnumerable<CycleTaskInfo> LoadAll() => new CycleTaskInfo[0];
        public void OnAdd(CycleTaskInfo task) { }
        public void OnRemove(CycleTaskInfo task) { }
        public virtual void OnExecuted(Scheduler scheduler, CycleTaskInfo task, CycleTaskExecuteResultInfo result) { }

        public virtual void OnExecuting(Scheduler scheduler, CycleTaskInfo task)
        {
            Console.WriteLine($"[{DateTime.Now.ToString("HH:mm:ss")}] {task.Topic} 被执行，还剩 {scheduler.QuantityCycleTask} 个循环任务");
        }
    }
}
