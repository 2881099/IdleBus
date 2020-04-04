using IdleScheduler;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace ConsoleApp1
{
    class Program
    {

        class MyCycleTaskHandler : IdleScheduler.CycleTaskHandlers.FreeSqlHandler, IDisposable
        {
            public MyCycleTaskHandler(IFreeSql fsql) : base(fsql) { }


            public override void OnExecuting(Scheduler scheduler, CycleTaskInfo task)
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
                .UseMonitorCommand(cmd => Console.WriteLine($"=========sql: {cmd.CommandText}\r\n"))
                .Build();

            Scheduler scheduler = new Scheduler(new MyCycleTaskHandler(fsql));

            var dt = DateTime.Now;

            for (var a = 0; a < 2; a++)
            {
                //临时任务
                scheduler.AddTempTask(TimeSpan.FromSeconds(20), () =>
                {
                    Console.WriteLine($"[{DateTime.Now.ToString("HH:mm:ss")}] 20秒后被执行，还剩 {scheduler.QuantityTempTask} 个临时任务");
                });

                //循环任务，执行10次，每次间隔1小时
                scheduler.AddCycleTask(topic: "test001", body: "data1", round: 10, seconds: 10);
            }

            var dtts = DateTime.Now.Subtract(dt).TotalMilliseconds;
            Console.WriteLine($"[{DateTime.Now.ToString("HH:mm:ss")}] 注册耗时 {dtts}ms，共计 {scheduler.QuantityTempTask} 个临时任务，{scheduler.QuantityCycleTask} 个循环任务");

            Console.ReadKey();

            dtts = DateTime.Now.Subtract(dt).TotalMilliseconds;
            Console.WriteLine($"[{DateTime.Now.ToString("HH:mm:ss")}] 耗时 {dtts}ms，还剩 {scheduler.QuantityTempTask} 个任务，{scheduler.QuantityCycleTask} 个循环任务");
            Console.ReadKey();

            scheduler.Dispose();
            fsql.Dispose();
        }
    }
}
