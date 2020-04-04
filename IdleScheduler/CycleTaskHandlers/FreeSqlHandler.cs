using System;
using System.Collections.Generic;
using System.Text;

namespace IdleScheduler.CycleTaskHandlers
{
    public class FreeSqlHandler : ICycleTaskHandler
    {
        IFreeSql _fsql;
        public FreeSqlHandler(IFreeSql fsql)
        {
            _fsql = fsql;
            _fsql.CodeFirst
                .ConfigEntity<CycleTaskInfo>(a =>
                {
                    a.Property(b => b.Id).IsPrimary(true);
                    a.Property(b => b.Body).StringLength(-1);
                    a.Property(b => b.Interval).MapType(typeof(string));
                })
                .ConfigEntity<CycleTaskExecuteResultInfo>(a =>
                {
                    a.Property(b => b.Exception).StringLength(-1);
                    a.Property(b => b.Remark).StringLength(-1);
                });
            _fsql.CodeFirst.SyncStructure<CycleTaskInfo>();
        }

        public IEnumerable<CycleTaskInfo> LoadAll() => _fsql.Select<CycleTaskInfo>().Where(a => a.CurrentRound < a.Round).ToList();
        public void OnAdd(CycleTaskInfo task) => _fsql.Insert<CycleTaskInfo>().NoneParameter().AppendData(task).ExecuteAffrows();
        public void OnRemove(CycleTaskInfo task) => _fsql.Delete<CycleTaskInfo>().Where(a => a.Id == task.Id).ExecuteAffrows();
        public void OnExecuted(Scheduler scheduler, CycleTaskInfo task, CycleTaskExecuteResultInfo result)
        {
            result.Remark = "testremark";
            _fsql.Transaction(() =>
            {
                _fsql.Update<CycleTaskInfo>().NoneParameter().SetSource(task)
                    .UpdateColumns(a => new { a.CurrentRound, a.ErrorTimes, a.LastRunTime })
                    .ExecuteAffrows();
                _fsql.Insert<CycleTaskExecuteResultInfo>().NoneParameter().AppendData(result).ExecuteAffrows();
            });
        }

        public virtual void OnExecuting(Scheduler scheduler, CycleTaskInfo task)
        {
            Console.WriteLine($"[{DateTime.Now.ToString("HH:mm:ss")}] {task.Topic} 被执行，还剩 {scheduler.QuantityCycleTask} 个循环任务");
        }
    }
}
