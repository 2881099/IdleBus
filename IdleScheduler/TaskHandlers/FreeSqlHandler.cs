using System;
using System.Collections.Generic;
using System.Text;

namespace IdleScheduler.TaskHandlers
{
    public class FreeSqlHandler : ITaskHandler
    {
        IFreeSql _fsql;
        public FreeSqlHandler(IFreeSql fsql)
        {
            _fsql = fsql;
            _fsql.CodeFirst
                .ConfigEntity<TaskInfo>(a =>
                {
                    a.Name("idlescheduler_task");
                    a.Property(b => b.Id).IsPrimary(true);
                    a.Property(b => b.Body).StringLength(-1);
                    a.Property(b => b.Interval).MapType(typeof(string));
                })
                .ConfigEntity<TaskLog>(a =>
                {
                    a.Name("idlescheduler_tasklog");
                    a.Property(b => b.Exception).StringLength(-1);
                    a.Property(b => b.Remark).StringLength(-1);
                });
            _fsql.CodeFirst.SyncStructure<TaskInfo>();
            _fsql.CodeFirst.SyncStructure<TaskLog>();
        }

        public IEnumerable<TaskInfo> LoadAll() => _fsql.Select<TaskInfo>().Where(a => a.CurrentRound < a.Round).ToList();
        public void OnAdd(TaskInfo task) => _fsql.Insert<TaskInfo>().NoneParameter().AppendData(task).ExecuteAffrows();
        public void OnRemove(TaskInfo task) => _fsql.Delete<TaskInfo>().Where(a => a.Id == task.Id).ExecuteAffrows();
        public void OnExecuted(Scheduler scheduler, TaskInfo task, TaskLog result)
        {
            result.Remark = "testremark";
            _fsql.Transaction(() =>
            {
                _fsql.Update<TaskInfo>().NoneParameter().SetSource(task)
                    .UpdateColumns(a => new { a.CurrentRound, a.ErrorTimes, a.LastRunTime })
                    .ExecuteAffrows();
                _fsql.Insert<TaskLog>().NoneParameter().AppendData(result).ExecuteAffrows();
            });
        }

        public virtual void OnExecuting(Scheduler scheduler, TaskInfo task)
        {
            Console.WriteLine($"[{DateTime.Now.ToString("HH:mm:ss")}] {task.Topic} 被执行，还剩 {scheduler.QuantityTask} 个循环任务");
        }
    }
}
