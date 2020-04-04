IdleBus 空闲对象管理容器，有效组织对象重复利用，自动创建、销毁，解决【实例】过多且长时间占用的问题。

有时候想做一个单例对象重复使用提升性能，但是定义多了，有的又可能一直空闲着占用资源。

专门解决：又想重复利用，又想少占资源的场景。

## 快速开始

> dotnet add package IdleBus

```csharp
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
```

输出：

```shell
输入 >
aa1
    aa1 注册成功，0/1
    aa1 实例+++创建成功，耗时 0.1658ms，1/1
    aa1 实例获取成功 1次
    aa1 实例获取成功 2次
输入 >
aa2
    aa2 注册成功，1/2
    aa2 实例+++创建成功，耗时 0.0065ms，2/2
    aa2 实例获取成功 1次
    aa2 实例获取成功 2次
输入 >
aa3
    aa3 注册成功，2/3
    aa3 实例+++创建成功，耗时 0.0073ms，3/3
    aa3 实例获取成功 1次
    aa3 实例获取成功 2次
输入 >
    aa1 ---自动释放成功，耗时 0.7607ms，2/3
    [2020/1/7 14:22] aa1 空闲被回收
    aa2 ---自动释放成功，耗时 0.0289ms，1/3
    [2020/1/7 14:22] aa2 空闲被回收
    aa3 ---自动释放成功，耗时 0.0046ms，0/3
    [2020/1/7 14:22] aa3 空闲被回收
```

## API

new IdleBus 可使用任何 IDisposable 实现类【混合注入】

new IdleBus\<T\> 可【自定义类型】注入，如： new IdleBus\<IFreeSql\>()

| Method | 说明 |
| -- | -- |
| void Ctor() | 创建空闲容器 |
| void Ctor(TimeSpan idle) | 指定空闲时间，创建空闲容器 |
| IdleBus Register(string key, Func\<T\> create) | 注册（其类型必须实现 IDisposable） |
| IdleBus Register(string key, Func\<T\> create, TimeSpan idle) | 注册，单独设置空间时间 |
| T Get(string key) | 获取【实例】（线程安全），key 未注册时，抛出异常 |
| T TryGet(string key) | 获取【实例】（线程安全），key 未注册时，返回 null |
| void Remove(string key) | 删除已注册的 |
| int UsageQuantity | 已创建【实例】数量 |
| int Quantity | 注册数量 |
| event Notice | 容器内部的变化通知，如：自动释放、自动创建 |

> 注意：Register 参数 create 属于对象创建器，切莫直接返回外部创建好的对象

```csharp
var obj = new Xxx();
ib.Register("key01", () => obj); //错了，错了，错了

ib.Register("key01", () => new Xxx()); //正确
```

## 设计思路

IdleBus 和对象池不同，对象池是队列设计，我们是 KV 设计。

IdleBus 又像缓存，又像池，又像容器，无法具体描述。

### 1、注册

向容器中注册对象，指定 key + 创建器 + 空闲时间，可以注册和管理任何实现 IDisposable 接口对象

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
- 管理 1000个 rabbitmq 服务器的客户端；
- [IdleScheduler](https://github.com/2881099/IdleBus/tree/master/IdleScheduler) 利用 IdleBus 实现的轻量定时任务调度；
- 等等等。。。

举例：向 1000个 (不同的、不同的、不同的) rabbitmq 服务器发送消息，正常有两种做法：

1、定义 1000 个静态 client 一直 open 着，重复利用

缺点：1000个里面不是每个都活跃，然后也一直占用着资源

2、每次发送消息的时候 open，使用完再 close

```csharp
new client("mq_0001server").pub(...).close();
new client("mq_0002server").pub(...).close();
new client("mq_0003server").pub(...).close();
new client("mq_0004server").pub(...).close();
```

缺点：性能损耗会比较大，socket 没有有效的重复利用
