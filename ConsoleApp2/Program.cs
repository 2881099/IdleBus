using System;

namespace ConsoleApp2
{
    class Program
    {
        static void Main(string[] args)
        {
            //超过20秒没有使用，就销毁【实例】
            var ib = new IdleBus(TimeSpan.FromSeconds(20));
            ib.Notice += (_, e) =>
            {
                Console.WriteLine("    " + e.Log);

                if (e.NoticeType == IdleBus<IDisposable>.NoticeType.AutoRelease)
                    Console.WriteLine($"    [{DateTime.Now.ToString("g")}] {e.Key} 空闲被回收");
            };

            while (true)
            {
                Console.WriteLine("输入 > ");
                var line = Console.ReadLine().Trim();
                if (string.IsNullOrEmpty(line)) break;

                // 注册
                ib.TryRegister(line, () => new TestInfo());

                // 第一次：创建
                TestInfo item = ib.Get(line) as TestInfo;

                // 第二次：直接获取，长驻内存直到空闲销毁
                item = ib.Get(line) as TestInfo;
            }

            ib.Dispose();
        }

        class TestInfo : IDisposable
        {
            public void Dispose() { }
        }
    }
}
