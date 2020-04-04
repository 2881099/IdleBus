using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using static IdleScheduler.Internal;

namespace ConsoleApp1
{
    class Program
    {

        class MyCycleTask : ICycleTask, IDisposable
        {
            IFreeSql _fsql;
            public MyCycleTask(IFreeSql fsql)
            {
                _fsql = fsql;
                _fsql.CodeFirst.ConfigEntity<CycleTaskinfo>(a => a.Property(b => b.Id).IsPrimary(true));
                _fsql.CodeFirst.SyncStructure<CycleTaskinfo>();
            }

            public IEnumerable<CycleTaskinfo> LoadAll() => _fsql.Select<CycleTaskinfo>().Where(a => a.RunTimes < a.MaxRunTimes).ToList();
            public void Add(CycleTaskinfo task) => _fsql.Insert(task).ExecuteAffrows();
            public void Remove(CycleTaskinfo task) => _fsql.Delete<CycleTaskinfo>().Where(a => a.Id == task.Id).ExecuteAffrows();
            public void Update(CycleTaskinfo task) => _fsql.Update<CycleTaskinfo>().SetSource(task).ExecuteAffrows();

            public void Execute(IdleScheduler scheduler, CycleTaskinfo task)
            {
                Console.WriteLine($"[{DateTime.Now.ToString("HH:mm:ss")}] {task.Topic} 被执行，还剩 {scheduler.QuantityCycleTask} 个循环任务");
            }

            public void Dispose()
            {
                
            }
        }

        static void Main(string[] args)
        {
            var fsql = new FreeSql.FreeSqlBuilder()
                .UseConnectionString(FreeSql.DataType.Sqlite, "data source=MyCycleTask.db;max pool size=5")
                .UseAutoSyncStructure(true)
                .UseNoneCommandParameter(true)
                .UseMonitorCommand(cmd => Console.Write(cmd.CommandText))
                .Build();

            IdleScheduler scheduler = null;
            scheduler = new IdleScheduler(new MyCycleTask(fsql));

            var dt = DateTime.Now;

            for (var a = 0; a < 100000; a++)
            {
                //一次性延时任务
                scheduler.AddTempTask(TimeSpan.FromSeconds(60), () =>
                {
                    //Console.WriteLine($"[{DateTime.Now.ToString("HH:mm:ss")}] 60秒后被执行，还剩 {scheduler.QuantityTempTask} 个临时任务");
                });
                scheduler.AddTempTask(TimeSpan.FromSeconds(70), () =>
                {
                    Console.WriteLine($"[{DateTime.Now.ToString("HH:mm:ss")}] 70秒后被执行，还剩 {scheduler.QuantityTempTask} 个临时任务");
                });

                //重复性任务，执行10次，每次间隔1小时
                //scheduler.AddCycleTask(text: "data1", times: 10, seconds: 60 * 15);
            }

            var dtts = DateTime.Now.Subtract(dt).TotalMilliseconds;
            Console.WriteLine($"[{DateTime.Now.ToString("HH:mm:ss")}] 注册耗时 {dtts}ms，共计 {scheduler.QuantityTempTask} 个临时任务，{scheduler.QuantityCycleTask} 个循环任务");

            Console.ReadKey();

            dtts = DateTime.Now.Subtract(dt).TotalMilliseconds;
            Console.WriteLine($"[{DateTime.Now.ToString("HH:mm:ss")}] 耗时 {dtts}ms，还剩 {scheduler.QuantityTempTask} 个任务，{scheduler.QuantityCycleTask} 个循环任务");
            Console.ReadKey();

            dtts = DateTime.Now.Subtract(dt).TotalMilliseconds;
            Console.WriteLine($"[{DateTime.Now.ToString("HH:mm:ss")}] 耗时 {dtts}ms，还剩 {scheduler.QuantityTempTask} 个任务，{scheduler.QuantityCycleTask} 个循环任务");
            Console.ReadKey();

            scheduler.Dispose();
            fsql.Dispose();
        }
    }
}
