IdleBus 空闲对象管理容器，有效组织对象重复利用，自动创建、销毁，解决【实例】过多且长时间占用的问题。

## 设计思路

### 1、注册

可以向窗口中注册对象，指定 key + 创建器 + 空闲时间，可以注册和管理任何实现 IDispose 接口对象

### 2、获取对象

开发人员使用 key 作为参数调用方法获得对象：

- 存在时，直接返回
- 不存在时，创建对象（保证线程安全）

### 3、过期销毁

当容器中有实例运行，会启用后台线程定时检查，超过空闲时间设置的将被释放（Dispose）

## 使用场景

- 多租户按数据库区分的场景，假设有1000个租户；
- Redis 客户端需要操作 N 个 服务器；
- Socket 长连接闲置后自动释放；

## Quick start

```csharp
//连续2次超过1分钟没有使用，就销毁【实例】
static IdbleBus ib = new IdleBus(TimeSpan.FromMinutes(1), 2);
ib.Notice += new EventHandler<NoticeEventArgs>((_, e) =>
{
    var log = $"[{DateTime.Now.ToString("HH:mm:ss")}] 线程{Thread.CurrentThread.ManagedThreadId}：{e.Log}";
    Console.WriteLine(log);
});

ib.Register("key1", () => new ManualResetEvent(false));
ib.Register("key2", () => new AutoResetEvent(false));

var item = ib.Get("key2") as AutoResetEvent;
item = ib.Get("key2") as AutoResetEvent;

ib.Dispose();
```

输出：

```shell
[23:32:04] 线程1：key1 注册成功，0/1
[23:32:04] 线程1：key2 注册成功，0/2
[23:32:04] 线程1：key2 实例+++创建成功，1/2
[23:32:04] 线程1：key2 实例获取成功 1次
[23:32:04] 线程1：key2 实例获取成功 2次
线程 0x1b0 已退出，返回值为 0 (0x0)。
```

## API

| Method | 说明 |
| -- | -- |
| void Ctor() | 创建空闲容器 |
| void Ctor(TimeSpan idle, int idleTimes) | 指定空闲时间、空闲次数，创建空闲容器 |
| IdleBus Register(string key, Func\<IDisposable\> create) | 注册（其类型必须实现 IDispose） |
| IdleBus Register(string key, Func\<IDisposable\> create, TimeSpan idle) | 注册，单独设置空间时间 |
| IDispose Get(string key) | 获取【实例】（线程安全） |
| void Remove(string key) | 删除已注册的 |
| int UsageQuantity | 已创建【实例】数量 |
| int Quantity | 注册数量 |
| event Notice | 容器内部的变化通知，如：自动释放、自动创建 |

注意：IdleBus 和对象池不同，对象池是队列设计，我们是 KV 设计。