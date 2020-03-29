using System;
using System.Linq;
using System.Threading;

namespace ConsoleApp1
{
    class Program
    {
        class WangTask : IDisposable
        {
            static Lazy<IdleBus> _ibLazy = new Lazy<IdleBus>(() =>
            {
                var ib = new IdleBus();
                ib.Notice += new EventHandler<IdleBus<IDisposable>.NoticeEventArgs>((s, e) =>
                {
                });
                return ib;
            });
            static IdleBus Ib => _ibLazy.Value;
            public static int Quantity => _ibLazy.Value.Quantity;

            public string Key { get; private set; }
            public TimeSpan Timeout { get; private set; }
            public Action Handle { get; private set; }

            private WangTask() { } //不允许 new WangTask()

            public static void Register(string key, TimeSpan timeout, Action handle)
            {
                var task = new WangTask
                {
                    Key = key,
                    Timeout = timeout,
                    Handle = handle
                };
                Ib.Register(key, () => task, timeout);
                Ib.Get(key);
            }
            public static void Remove(string key)
            {
                Ib.TryRemove(key);
            }

            public void Dispose()
            {
                //todo 到期执行
                Ib.TryRemove(this.Key);

                try
                {
                    this.Handle?.Invoke();
                }
                catch (Exception ex)
                {
                    Register(this.Key, this.Timeout, this.Handle); //出错了，重新放入调度
                }
            }
        }

        static void Main(string[] args)
        {
            var xxx = WangTask.Quantity;
            var dt = DateTime.Now;
            Enumerable.Range(0, 10000).ToList().ForEach(idx =>
            {
                var key = "wang_" + idx;
                WangTask.Register(key, TimeSpan.FromSeconds(30), () =>
                {
                    Console.WriteLine($"[{DateTime.Now.ToString("HH:mm:ss")}] {key} 被执行，还剩 {WangTask.Quantity} 个任务");
                });
            });
            var dtts = DateTime.Now.Subtract(dt).TotalMilliseconds;
            Console.WriteLine($"注册耗时 {dtts}ms，共计 {WangTask.Quantity} 个任务");

            Console.ReadKey();
        }
    }
}
