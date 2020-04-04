IdleSchduler 是利用 IdleBus 实现的轻量定时任务调度，支持临时的延时任务和重复循环任务，可按秒，每天固定时间，每周固定时间，每月固定时间执行。

## API

| Method | 说明 |
| -- | -- |
| void Ctor(ICycleTask) | 指定任务调度器（单例） |
| string AddTempTask(TimeSpan, Action) | 创建临时的延时任务，返回 id |
| string AddCycleTask(string topic, string body, int times, int seconds) | 创建循环定时任务，返回 id |
| bool RemoveTask(string id) | 删除任务 |
| int QuantityTempTask | 临时任务数量 |
| int QuantityCycleTask | 循环任务数量 |

## 快速开始

> dotnet add package IdleSchduler

```csharp
class Program
{
    static void Main(string[] args)
    {
        IdleScheduler scheduler = null;
        scheduler = new IdleScheduler(new TaskStorage(), task =>
        {
            Console.WriteLine($"[{DateTime.Now.ToString("HH:mm:ss")}] {task.Text} 被执行，还剩 {scheduler.Quantity} 个任务");
        });

        var dt = DateTime.Now;

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
        scheduler.AddCycleTask(state: "data1", times: 10, seconds: 2);

        var dtts = DateTime.Now.Subtract(dt).TotalMilliseconds;
        Console.WriteLine($"[{DateTime.Now.ToString("HH:mm:ss")}] 注册耗时 {dtts}ms，共计 {scheduler.Quantity} 个任务");

        Console.ReadKey();
        scheduler.Dispose();
    }

    class TaskStorage : ICycleTaskStorage
    {
        public List<CycleTaskinfo> LoadAll() => new List<CycleTaskinfo>();
        public void Add(CycleTaskinfo task) { }
        public void Remove(CycleTaskinfo task) { }
        public void Update(CycleTaskinfo task) { }
    }
}
```

输出：

```shell
[19:05:40] 注册耗时 12.5844ms，共计 3 个任务
[19:05:42] data1 被执行，还剩 3 个任务
[19:05:44] data1 被执行，还剩 3 个任务
[19:05:46] data1 被执行，还剩 3 个任务
[19:05:48] data1 被执行，还剩 3 个任务
[19:05:50] 10秒后被执行，还剩 2 个任务
[19:05:50] data1 被执行，还剩 2 个任务
[19:05:53] data1 被执行，还剩 2 个任务
[19:05:55] data1 被执行，还剩 2 个任务
[19:05:57] data1 被执行，还剩 2 个任务
[19:06:00] data1 被执行，还剩 2 个任务
[19:06:01] 20秒后被执行，还剩 1 个任务
[19:06:02] data1 被执行，还剩 0 个任务
```
