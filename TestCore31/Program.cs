using System;
using System.Diagnostics;
using System.Threading;

namespace TestCore31
{
    class Program
    {
        static void Main(string[] args)
        {
            var ib = new IdleBus(TimeSpan.FromSeconds(10));
            ib.Notice += (_, e2) =>
            {
                var log = $"[{DateTime.Now.ToString("HH:mm:ss")}] 线程{Thread.CurrentThread.ManagedThreadId}：{e2.Log}";
                //Trace.WriteLine(log);
                Console.WriteLine(log);
            };

            ib
                .Register("key1", () => new ManualResetEvent(false))
                .Register("key2", () => new AutoResetEvent(false));

            for (var a = 3; a < 2000; a++)
                ib.Register("key" + a, () => new System.Data.SqlClient.SqlConnection());

            for (var a = 1; a < 2000; a++)
                ib.Get("key" + a);

            var now = DateTime.Now;
            int counter = 100 * 1000;
            for (var k = 0; k < 100; k++)
            {
                new Thread(() =>
                {
                    var rnd = new Random();

                    for (var a = 0; a < 1000; a++)
                    {
                        for (var l = 0; l < 10; l++)
                        {
                            ib.Get("key" + rnd.Next(1, 2000));
                        }

                        if (Interlocked.Decrement(ref counter) <= 0)
                            Console.WriteLine($"测试完成，100线程并发 ib.Get() 获取100万次，耗时：{DateTime.Now.Subtract(now).TotalMilliseconds}ms");
                    }
                }).Start();
            }

            Console.ReadKey();
            ib.Dispose();
        }
    }
}
