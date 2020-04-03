using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using static IdleScheduler.Internal;

namespace ConsoleApp1
{
    class Program
    {

        class TaskStorage : ICycleTaskStorage
        {
            public List<CycleTaskinfo> LoadAll() => new List<CycleTaskinfo>();
            public void Add(CycleTaskinfo task) { }
            public void Remove(CycleTaskinfo task) { }
            public void Update(CycleTaskinfo task) { }
        }

        static void Main(string[] args)
        {
            IdleScheduler scheduler = null;
            scheduler = new IdleScheduler(new TaskStorage(), task =>
            {
                Console.WriteLine($"[{DateTime.Now.ToString("HH:mm:ss")}] {task.Text} 被执行，还剩 {scheduler.Quantity} 个任务");
            });

            var dt = DateTime.Now;

            //for (var a = 0; a < 10000; a++)
            //{
                //一次性延时任务
                scheduler.AddTempTask(TimeSpan.FromSeconds(10), () =>
                {
                    Console.WriteLine($"[{DateTime.Now.ToString("HH:mm:ss")}] 10秒后被执行，还剩 {scheduler.Quantity} 个任务");
                });
                scheduler.AddTempTask(TimeSpan.FromSeconds(20), () =>
                {
                    Console.WriteLine($"[{DateTime.Now.ToString("HH:mm:ss")}] 20秒后被执行，还剩 {scheduler.Quantity} 个任务");
                });

                //重复性任务，执行10次，每次间隔1小时
                scheduler.AddCycleTask(text: "data1", times: 10, seconds: 2);
            //}

            var dtts = DateTime.Now.Subtract(dt).TotalMilliseconds;
            Console.WriteLine($"[{DateTime.Now.ToString("HH:mm:ss")}] 注册耗时 {dtts}ms，共计 {scheduler.Quantity} 个任务");

            Console.ReadKey();

            dtts = DateTime.Now.Subtract(dt).TotalMilliseconds;
            Console.WriteLine($"[{DateTime.Now.ToString("HH:mm:ss")}] 耗时 {dtts}ms，还剩 {scheduler.Quantity} 个任务");
            Console.ReadKey();

            dtts = DateTime.Now.Subtract(dt).TotalMilliseconds;
            Console.WriteLine($"[{DateTime.Now.ToString("HH:mm:ss")}] 耗时 {dtts}ms，还剩 {scheduler.Quantity} 个任务");
            Console.ReadKey();

            scheduler.Dispose();
        }
    }
}
