IdleSchduler 是利用 IdleBus 实现的轻量定时任务调度，支持临时的延时任务和重复循环任务(可落地保存)，可按秒，每天固定时间，每周固定时间，每月固定时间执行。

## API

| Method | 说明 |
| -- | -- |
| void Ctor(ITaskHandler) | 指定任务调度器（单例） |
| string AddTask(string topic, string body, int times, int seconds) | 创建循环定时任务，返回 id |
| string AddTempTask(TimeSpan, Action) | 创建临时的延时任务，返回 id |
| bool RemoveTask(string id) | 删除任务(循环定时任务) |
| bool RemoveTempTask(string id) | 删除任务(临时任务) |
| bool ExistsTask(string id) | 判断任务是否存在(循环定时任务) |
| bool ExistsTempTask(string id) | 判断任务是否存在(临时任务) |
| int QuantityTask | 任务数量(循环定时任务) |
| int QuantityTempTask | 任务数量(临时任务) |

## Quick start

> dotnet add package IdleSchduler

```csharp
class Program
{
    static void Main(string[] args)
    {
        var fsql = new FreeSql.FreeSqlBuilder()
            .UseConnectionString(FreeSql.DataType.Sqlite, "data source=task.db;max pool size=5")
            .UseAutoSyncStructure(true)
            .UseNoneCommandParameter(true)
            .UseMonitorCommand(cmd => Console.WriteLine($"=========sql: {cmd.CommandText}\r\n"))
            .Build();

        Scheduler scheduler = new Scheduler(new IdleSchduler.TaskHandlers.FreeSqlHandler(fsql));

        var dt = DateTime.Now;
        for (var a = 0; a < 2; a++)
        {
            //临时任务
            scheduler.AddTempTask(TimeSpan.FromSeconds(20), () =>
            {
                Console.WriteLine($"[{DateTime.Now.ToString("HH:mm:ss")}] 20秒后被执行，还剩 {scheduler.QuantityTempTask} 个临时任务");
            });

            //循环任务，执行10次，每次间隔10秒
            scheduler.AddTask(topic: "test001", body: "data1", round: 10, seconds: 10);
        }
        var dtts = DateTime.Now.Subtract(dt).TotalMilliseconds;
        Console.WriteLine($"[{DateTime.Now.ToString("HH:mm:ss")}] 注册耗时 {dtts}ms，共计 {scheduler.QuantityTempTask} 个临时任务，{scheduler.QuantityTask} 个循环任务");

        Console.ReadKey();
        Console.WriteLine($"[{DateTime.Now.ToString("HH:mm:ss")}] 还剩 {scheduler.QuantityTempTask} 个任务，{scheduler.QuantityTask} 个循环任务");
        scheduler.Dispose();
        fsql.Dispose();
    }
}
```

输出：

```shell
=========sql:  select 1 from main.sqlite_master where type='table' and name='idlescheduler_task'

=========sql: CREATE TABLE IF NOT EXISTS "main"."idlescheduler_task" (
  "Id" NVARCHAR(255),
  "Topic" NVARCHAR(255),
  "Body" TEXT,
  "Round" INTEGER NOT NULL,
  "Interval" NVARCHAR(255),
  "IntervalArgument" NVARCHAR(255),
  "CreateTime" DATETIME NOT NULL,
  "LastRunTime" DATETIME NOT NULL,
  "CurrentRound" INTEGER NOT NULL,
  "ErrorTimes" INTEGER NOT NULL,
  PRIMARY KEY ("Id")
)
;


=========sql:  select 1 from main.sqlite_master where type='table' and name='idlescheduler_tasklog'

=========sql: CREATE TABLE IF NOT EXISTS "main"."idlescheduler_tasklog" (
  "TaskId" NVARCHAR(255),
  "Round" INTEGER NOT NULL,
  "ElapsedMilliseconds" INTEGER NOT NULL,
  "Success" BOOLEAN NOT NULL,
  "Exception" TEXT,
  "Remark" TEXT,
  "CreateTime" DATETIME NOT NULL
)
;


=========sql: SELECT a."Id", a."Topic", a."Body", a."Round", a."Interval", a."IntervalArgument", a."CreateTime", a."LastRunTime", a."CurrentRound", a."ErrorTimes"
FROM "idlescheduler_task" a
WHERE (a."CurrentRound" < a."Round")

=========sql: INSERT INTO "idlescheduler_task"("Id", "Topic", "Body", "Round", "Interval", "IntervalArgument", "CreateTime", "LastRunTime", "CurrentRound", "ErrorTimes") VALUES('20200404.14714252113887232', 'test001', 'data1', 10, 'SEC', '10', '2020-04-04 04:31:17', '1970-01-01 00:00:00', 0, 0)

=========sql: INSERT INTO "idlescheduler_task"("Id", "Topic", "Body", "Round", "Interval", "IntervalArgument", "CreateTime", "LastRunTime", "CurrentRound", "ErrorTimes") VALUES('20200404.14714252114264064', 'test001', 'data1', 10, 'SEC', '10', '2020-04-04 04:31:17', '1970-01-01 00:00:00', 0, 0)

[12:31:17] 注册耗时 47.1874ms，共计 2 个临时任务，2 个循环任务
[12:31:28] test001 被执行，还剩 2 个循环任务
[12:31:28] test001 被执行，还剩 2 个循环任务
=========sql: UPDATE "idlescheduler_task" SET "LastRunTime" = '2020-04-04 04:31:28', "CurrentRound" = 1, "ErrorTimes" = 0
WHERE ("Id" = '20200404.14714252114264064')

=========sql: INSERT INTO "idlescheduler_tasklog"("TaskId", "Round", "ElapsedMilliseconds", "Success", "Exception", "Remark", "CreateTime") VALUES('20200404.14714252114264064', 1, 0, 1, NULL, 'testremark', '2020-04-04 04:31:28')

=========sql: UPDATE "idlescheduler_task" SET "LastRunTime" = '2020-04-04 04:31:28', "CurrentRound" = 1, "ErrorTimes" = 0
WHERE ("Id" = '20200404.14714252113887232')

=========sql: INSERT INTO "idlescheduler_tasklog"("TaskId", "Round", "ElapsedMilliseconds", "Success", "Exception", "Remark", "CreateTime") VALUES('20200404.14714252113887232', 1, 1, 1, NULL, 'testremark', '2020-04-04 04:31:28')

[12:31:37] 20秒后被执行，还剩 1 个临时任务
[12:31:38] 20秒后被执行，还剩 0 个临时任务
[12:31:39] test001 被执行，还剩 2 个循环任务
[12:31:39] test001 被执行，还剩 2 个循环任务
=========sql: UPDATE "idlescheduler_task" SET "LastRunTime" = '2020-04-04 04:31:39', "CurrentRound" = 2, "ErrorTimes" = 0
WHERE ("Id" = '20200404.14714252114264064')

=========sql: INSERT INTO "idlescheduler_tasklog"("TaskId", "Round", "ElapsedMilliseconds", "Success", "Exception", "Remark", "CreateTime") VALUES('20200404.14714252114264064', 2, 0, 1, NULL, 'testremark', '2020-04-04 04:31:39')

=========sql: UPDATE "idlescheduler_task" SET "LastRunTime" = '2020-04-04 04:31:39', "CurrentRound" = 2, "ErrorTimes" = 0
WHERE ("Id" = '20200404.14714252113887232')

=========sql: INSERT INTO "idlescheduler_tasklog"("TaskId", "Round", "ElapsedMilliseconds", "Success", "Exception", "Remark", "CreateTime") VALUES('20200404.14714252113887232', 2, 0, 1, NULL, 'testremark', '2020-04-04 04:31:39')

[12:31:50] test001 被执行，还剩 2 个循环任务
[12:31:50] test001 被执行，还剩 2 个循环任务
=========sql: UPDATE "idlescheduler_task" SET "LastRunTime" = '2020-04-04 04:31:50', "CurrentRound" = 3, "ErrorTimes" = 0
WHERE ("Id" = '20200404.14714252114264064')

=========sql: INSERT INTO "idlescheduler_tasklog"("TaskId", "Round", "ElapsedMilliseconds", "Success", "Exception", "Remark", "CreateTime") VALUES('20200404.14714252114264064', 3, 0, 1, NULL, 'testremark', '2020-04-04 04:31:50')

=========sql: UPDATE "idlescheduler_task" SET "LastRunTime" = '2020-04-04 04:31:50', "CurrentRound" = 3, "ErrorTimes" = 0
WHERE ("Id" = '20200404.14714252113887232')

=========sql: INSERT INTO "idlescheduler_tasklog"("TaskId", "Round", "ElapsedMilliseconds", "Success", "Exception", "Remark", "CreateTime") VALUES('20200404.14714252113887232', 3, 1, 1, NULL, 'testremark', '2020-04-04 04:31:50')

[12:32:01] test001 被执行，还剩 2 个循环任务
[12:32:01] test001 被执行，还剩 2 个循环任务
=========sql: UPDATE "idlescheduler_task" SET "LastRunTime" = '2020-04-04 04:32:01', "CurrentRound" = 4, "ErrorTimes" = 0
WHERE ("Id" = '20200404.14714252114264064')

=========sql: INSERT INTO "idlescheduler_tasklog"("TaskId", "Round", "ElapsedMilliseconds", "Success", "Exception", "Remark", "CreateTime") VALUES('20200404.14714252114264064', 4, 0, 1, NULL, 'testremark', '2020-04-04 04:32:01')

=========sql: UPDATE "idlescheduler_task" SET "LastRunTime" = '2020-04-04 04:32:01', "CurrentRound" = 4, "ErrorTimes" = 0
WHERE ("Id" = '20200404.14714252113887232')

=========sql: INSERT INTO "idlescheduler_tasklog"("TaskId", "Round", "ElapsedMilliseconds", "Success", "Exception", "Remark", "CreateTime") VALUES('20200404.14714252113887232', 4, 2, 1, NULL, 'testremark', '2020-04-04 04:32:01')

[12:32:12] test001 被执行，还剩 2 个循环任务
[12:32:12] test001 被执行，还剩 2 个循环任务
=========sql: UPDATE "idlescheduler_task" SET "LastRunTime" = '2020-04-04 04:32:12', "CurrentRound" = 5, "ErrorTimes" = 0
WHERE ("Id" = '20200404.14714252114264064')

=========sql: INSERT INTO "idlescheduler_tasklog"("TaskId", "Round", "ElapsedMilliseconds", "Success", "Exception", "Remark", "CreateTime") VALUES('20200404.14714252114264064', 5, 0, 1, NULL, 'testremark', '2020-04-04 04:32:12')

=========sql: UPDATE "idlescheduler_task" SET "LastRunTime" = '2020-04-04 04:32:12', "CurrentRound" = 5, "ErrorTimes" = 0
WHERE ("Id" = '20200404.14714252113887232')

=========sql: INSERT INTO "idlescheduler_tasklog"("TaskId", "Round", "ElapsedMilliseconds", "Success", "Exception", "Remark", "CreateTime") VALUES('20200404.14714252113887232', 5, 1, 1, NULL, 'testremark', '2020-04-04 04:32:12')

[12:32:23] test001 被执行，还剩 2 个循环任务
[12:32:23] test001 被执行，还剩 2 个循环任务
=========sql: UPDATE "idlescheduler_task" SET "LastRunTime" = '2020-04-04 04:32:23', "CurrentRound" = 6, "ErrorTimes" = 0
WHERE ("Id" = '20200404.14714252114264064')

=========sql: INSERT INTO "idlescheduler_tasklog"("TaskId", "Round", "ElapsedMilliseconds", "Success", "Exception", "Remark", "CreateTime") VALUES('20200404.14714252114264064', 6, 0, 1, NULL, 'testremark', '2020-04-04 04:32:23')

=========sql: UPDATE "idlescheduler_task" SET "LastRunTime" = '2020-04-04 04:32:23', "CurrentRound" = 6, "ErrorTimes" = 0
WHERE ("Id" = '20200404.14714252113887232')

=========sql: INSERT INTO "idlescheduler_tasklog"("TaskId", "Round", "ElapsedMilliseconds", "Success", "Exception", "Remark", "CreateTime") VALUES('20200404.14714252113887232', 6, 0, 1, NULL, 'testremark', '2020-04-04 04:32:23')

[12:32:34] test001 被执行，还剩 2 个循环任务
[12:32:34] test001 被执行，还剩 2 个循环任务
=========sql: UPDATE "idlescheduler_task" SET "LastRunTime" = '2020-04-04 04:32:34', "CurrentRound" = 7, "ErrorTimes" = 0
WHERE ("Id" = '20200404.14714252114264064')

=========sql: INSERT INTO "idlescheduler_tasklog"("TaskId", "Round", "ElapsedMilliseconds", "Success", "Exception", "Remark", "CreateTime") VALUES('20200404.14714252114264064', 7, 0, 1, NULL, 'testremark', '2020-04-04 04:32:34')

=========sql: UPDATE "idlescheduler_task" SET "LastRunTime" = '2020-04-04 04:32:34', "CurrentRound" = 7, "ErrorTimes" = 0
WHERE ("Id" = '20200404.14714252113887232')

=========sql: INSERT INTO "idlescheduler_tasklog"("TaskId", "Round", "ElapsedMilliseconds", "Success", "Exception", "Remark", "CreateTime") VALUES('20200404.14714252113887232', 7, 1, 1, NULL, 'testremark', '2020-04-04 04:32:34')

[12:32:45] test001 被执行，还剩 2 个循环任务
[12:32:45] test001 被执行，还剩 2 个循环任务
=========sql: UPDATE "idlescheduler_task" SET "LastRunTime" = '2020-04-04 04:32:45', "CurrentRound" = 8, "ErrorTimes" = 0
WHERE ("Id" = '20200404.14714252114264064')

=========sql: INSERT INTO "idlescheduler_tasklog"("TaskId", "Round", "ElapsedMilliseconds", "Success", "Exception", "Remark", "CreateTime") VALUES('20200404.14714252114264064', 8, 0, 1, NULL, 'testremark', '2020-04-04 04:32:45')

=========sql: UPDATE "idlescheduler_task" SET "LastRunTime" = '2020-04-04 04:32:45', "CurrentRound" = 8, "ErrorTimes" = 0
WHERE ("Id" = '20200404.14714252113887232')

=========sql: INSERT INTO "idlescheduler_tasklog"("TaskId", "Round", "ElapsedMilliseconds", "Success", "Exception", "Remark", "CreateTime") VALUES('20200404.14714252113887232', 8, 1, 1, NULL, 'testremark', '2020-04-04 04:32:45')

[12:32:56] test001 被执行，还剩 2 个循环任务
[12:32:56] test001 被执行，还剩 2 个循环任务
=========sql: UPDATE "idlescheduler_task" SET "LastRunTime" = '2020-04-04 04:32:56', "CurrentRound" = 9, "ErrorTimes" = 0
WHERE ("Id" = '20200404.14714252114264064')

=========sql: INSERT INTO "idlescheduler_tasklog"("TaskId", "Round", "ElapsedMilliseconds", "Success", "Exception", "Remark", "CreateTime") VALUES('20200404.14714252114264064', 9, 0, 1, NULL, 'testremark', '2020-04-04 04:32:56')

=========sql: UPDATE "idlescheduler_task" SET "LastRunTime" = '2020-04-04 04:32:56', "CurrentRound" = 9, "ErrorTimes" = 0
WHERE ("Id" = '20200404.14714252113887232')

=========sql: INSERT INTO "idlescheduler_tasklog"("TaskId", "Round", "ElapsedMilliseconds", "Success", "Exception", "Remark", "CreateTime") VALUES('20200404.14714252113887232', 9, 3, 1, NULL, 'testremark', '2020-04-04 04:32:56')

[12:33:07] test001 被执行，还剩 0 个循环任务
[12:33:07] test001 被执行，还剩 0 个循环任务
=========sql: UPDATE "idlescheduler_task" SET "LastRunTime" = '2020-04-04 04:33:07', "CurrentRound" = 10, "ErrorTimes" = 0
WHERE ("Id" = '20200404.14714252114264064')

=========sql: INSERT INTO "idlescheduler_tasklog"("TaskId", "Round", "ElapsedMilliseconds", "Success", "Exception", "Remark", "CreateTime") VALUES('20200404.14714252114264064', 10, 0, 1, NULL, 'testremark', '2020-04-04 04:33:07')

=========sql: UPDATE "idlescheduler_task" SET "LastRunTime" = '2020-04-04 04:33:07', "CurrentRound" = 10, "ErrorTimes" = 0
WHERE ("Id" = '20200404.14714252113887232')

=========sql: INSERT INTO "idlescheduler_tasklog"("TaskId", "Round", "ElapsedMilliseconds", "Success", "Exception", "Remark", "CreateTime") VALUES('20200404.14714252113887232', 10, 1, 1, NULL, 'testremark', '2020-04-04 04:33:07')

[12:33:11] 耗时 113186.9488ms，还剩 0 个任务，0 个循环任务

C:\Users\28810\Desktop\github\IdleBus\ConsoleApp1\bin\Debug\netcoreapp3.1\ConsoleApp1.exe (进程 27792)已退出，代码为 0。
要在调试停止时自动关闭控制台，请启用“工具”->“选项”->“调试”->“调试停止时自动关闭控制台”。
按任意键关闭此窗口. . .
```
