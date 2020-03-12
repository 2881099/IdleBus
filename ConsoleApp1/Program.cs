using System;
using System.Linq;
using System.Threading;

namespace ConsoleApp1
{
    class Program
    {
        class WangTask : IDisposable
        {
            public string Name { get; private set; }
            public WangTask(string name)
            {
                this.Name = name;
            }

            public void Dispose()
            {
                //todo 到期执行

                Console.WriteLine($"[{DateTime.Now.ToString("HH:mm:ss")}] {this.Name} 被执行");
            }
        }

        static void Main(string[] args)
        {
            var ib = new IdleBus(TimeSpan.FromSeconds(10));
            ib.Notice += (_, e2) =>
            {
                if (e2.NoticeType == IdleBus<IDisposable>.NoticeType.AutoCreate ||
                    e2.NoticeType == IdleBus<IDisposable>.NoticeType.AutoRelease)
                {
                    //var log = $"[{DateTime.Now.ToString("HH:mm:ss")}] 线程{Thread.CurrentThread.ManagedThreadId}：{e2.Log}";
                    //Trace.WriteLine(log);
                    //Console.WriteLine(log);
                }
            };

            Enumerable.Range(0, 10000).ToList().ForEach(idx =>
            {
                var key = "wang_" + idx;
                ib.TryRegister(key, () => new WangTask(key), TimeSpan.FromSeconds(30));
                ib.Get(key);//开始计时
            });

            Console.ReadKey();
            ib.Dispose();
        }
    }
}
