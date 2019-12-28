using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;

/// <summary>
/// 空闲对象容器管理，可实现自动创建、销毁、扩张收缩，解决【实例】长时间占用问题
/// </summary>
public partial class IdleBus<T> : IDisposable where T : class
{

    ConcurrentDictionary<string, ItemInfo> _dic;
    ConcurrentDictionary<string, ItemInfo> _removePending;
    int _usageQuantity;
    TimeSpan _defaultIdle;
    int _defaultIdleTimes;

    /// <summary>
    /// 按空闲时间1分钟，空闲2次，创建空闲容器
    /// </summary>
    public IdleBus() : this(TimeSpan.FromMinutes(1), 2) {}

    /// <summary>
    /// 指定空闲时间、空闲次数，创建空闲容器
    /// </summary>
    /// <param name="idle">空闲时间</param>
    /// <param name="idleTimes">空闲次数</param>
    public IdleBus(TimeSpan idle, int idleTimes)
    {
        if (typeof(IDisposable).IsAssignableFrom(typeof(T)) == false)
            throw new Exception($"无法为 {typeof(T).FullName} 创建 IdleBus，它必须实现 IDisposable 接口");

        _dic = new ConcurrentDictionary<string, ItemInfo>();
        _removePending = new ConcurrentDictionary<string, ItemInfo>();
        _usageQuantity = 0;
        _defaultIdle = idle;
        _defaultIdleTimes = idleTimes;
    }

    /// <summary>
    /// 根据 key 获得或创建【实例】（线程安全）
    /// </summary>
    /// <param name="key"></param>
    /// <returns></returns>
    public T Get(string key)
    {
        if (isdisposed) new Exception($"{key} 实例获取失败 ，{nameof(IdleBus<T>)} 对象已释放");
        if (_dic.TryGetValue(key, out var item) == false)
        {
            var error = new Exception($"{key} 实例获取失败，因为没有注册");
            this.OnNotice(new NoticeEventArgs(NoticeType.Get, key, error, error.Message));
            throw error;
        }

        var now = DateTime.Now;
        var ret = item.GetOrCreate();
        var tsms = DateTime.Now.Subtract(now).TotalMilliseconds;
        this.OnNotice(new NoticeEventArgs(NoticeType.Get, key, null, $"{key} 实例获取成功 {item.activeCounter}次{(tsms > 5 ? $"，耗时 {tsms}ms" : "")}"));
        _maxActiveTime = item.lastActiveTime;
        var startThread = false;
        if (_threadStarted == false)
            lock (_threadStartedLock)
                if (_threadStarted == false)
                    startThread = _threadStarted = true;
        if (startThread)
            new Thread(() =>
            {
                while (isdisposed == false && _usageQuantity > 0 && DateTime.Now.Subtract(_maxActiveTime) < TimeSpan.FromMinutes(20))
                {
                    //定时30秒检查，关闭不活跃的【实例】
                    for (var a = 0; a < 15; a++)
                    {
                        if (isdisposed) return;
                        Thread.CurrentThread.Join(TimeSpan.FromSeconds(2));
                        if (isdisposed) return;
                    }
                    TimerCleanCallback();
                }
                lock (_threadStartedLock)
                    _threadStarted = false;
            }).Start();
        return ret;
    }
    bool _threadStarted = false;
    object _threadStartedLock = new object();
    DateTime _maxActiveTime;

    /// <summary>
    /// 注册【实例】
    /// </summary>
    /// <param name="key"></param>
    /// <param name="create">实例创建方法</param>
    /// <returns></returns>
    public IdleBus<T> Register(string key, Func<T> create) => InternalRegister(key, create, null, null, true);
    public IdleBus<T> Register(string key, Func<T> create, TimeSpan idle) => InternalRegister(key, create, idle, null, true);
    public IdleBus<T> Register(string key, Func<T> create, TimeSpan idle, int idleTimes) => InternalRegister(key, create, idle, idleTimes, true);
    public IdleBus<T> TryRegister(string key, Func<T> create) => InternalRegister(key, create, null, null, false);
    public IdleBus<T> TryRegister(string key, Func<T> create, TimeSpan idle) => InternalRegister(key, create, idle, null, false);
    public IdleBus<T> TryRegister(string key, Func<T> create, TimeSpan idle, int idleTimes) => InternalRegister(key, create, idle, idleTimes, false);

    public void Remove(string key) => InternalRemove(key, true);
    public void TryRemove(string key) => InternalRemove(key, false);

    /// <summary>
    /// 已创建【实例】数量
    /// </summary>
    public int UsageQuantity => _usageQuantity;
    /// <summary>
    /// 注册数量
    /// </summary>
    public int Quantity => _dic.Count;
    /// <summary>
    /// 通知事件
    /// </summary>
    public event EventHandler<NoticeEventArgs> Notice;

    IdleBus<T> InternalRegister(string key, Func<T> create, TimeSpan? idle, int? idleTimes, bool isThrow)
    {
        if (isdisposed) new Exception($"{key} 注册失败 ，{nameof(IdleBus<T>)} 对象已释放");
        var error = new Exception($"{key} 注册失败，请勿重复注册");
        if (_dic.ContainsKey(key))
        {
            this.OnNotice(new NoticeEventArgs(NoticeType.Register, key, error, error.Message));
            if (isThrow) throw error;
            return this;
        }

        var added = _dic.TryAdd(key, new ItemInfo
        {
            ib = this,
            key = key,
            create = create,
            idle = idle ?? _defaultIdle,
            idleTimes = idleTimes ?? _defaultIdleTimes
        });
        if (added == false)
        {
            this.OnNotice(new NoticeEventArgs(NoticeType.Register, key, error, error.Message));
            if (isThrow) throw error;
            return this;
        }
        this.OnNotice(new NoticeEventArgs(NoticeType.Register, key, null, $"{key} 注册成功，{_usageQuantity}/{Quantity}"));
        return this;
    }
    void InternalRemove(string key, bool isThrow)
    {
        if (isdisposed) new Exception($"{key} 删除失败 ，{nameof(IdleBus<T>)} 对象已释放");
        if (_dic.TryRemove(key, out var item) == false)
        {
            var error = new Exception($"{key} 删除失败 ，因为没有注册");
            this.OnNotice(new NoticeEventArgs(NoticeType.Remove, key, error, error.Message));
            if (isThrow) throw error;
            return;
        }

        Interlocked.Exchange(ref item.releaseErrorCounter, 0);
        item.lastActiveTime = DateTime.Now; //延时删除
        _removePending.TryAdd(Guid.NewGuid().ToString(), item);
        this.OnNotice(new NoticeEventArgs(NoticeType.Remove, item.key, null, $"{key} 删除成功，并且已标记为延时释放，{_usageQuantity}/{Quantity}"));
    }
    void TimerCleanCallback()
    {
        //处理延时删除
        var keys = _removePending.Keys;
        foreach (var key in keys)
        {
            if (_dic.TryGetValue(key, out var item) == false) continue;
            if (DateTime.Now.Subtract(item.lastActiveTime) <= item.idle) continue;
            try
            {
                item.Release(() => true);
            }
            catch (Exception ex)
            {
                var tmp1 = Interlocked.Increment(ref item.releaseErrorCounter);
                this.OnNotice(new NoticeEventArgs(NoticeType.Remove, item.key, ex, $"{key} ---延时释放执行出错({tmp1}次)：{ex.Message}"));
                if (tmp1 < 3)
                    continue;
            }
            item.Dispose();
            _removePending.TryRemove(key, out var oldItem);
        }

        keys = _dic.Keys;
        foreach (var key in keys)
        {
            if (_dic.TryGetValue(key, out var item) == false) continue;
            if (item.value == null) continue;
            if (DateTime.Now.Subtract(item.lastActiveTime) <= item.idle) continue;
            if (Interlocked.Increment(ref item.idleCounter) < item.idleTimes) continue;
            //持续未活跃的【实例】，让它去死
            try
            {
                var now = DateTime.Now;
                if (item.Release(() => DateTime.Now.Subtract(item.lastActiveTime) > item.idle && item.lastActiveTime >= item.createTime))
                    //防止并发有其他线程创建，最后活动时间 > 创建时间
                    this.OnNotice(new NoticeEventArgs(NoticeType.AutoRelease, item.key, null, $"{key} ---自动释放成功，耗时 {DateTime.Now.Subtract(now).TotalMilliseconds}ms，{_usageQuantity}/{Quantity}"));
            }
            catch (Exception ex)
            {
                this.OnNotice(new NoticeEventArgs(NoticeType.AutoRelease, item.key, ex, $"{key} ---自动释放执行出错：{ex.Message}"));
            }
        }
    }

    void OnNotice(NoticeEventArgs e)
    {
        if (this.Notice != null) this.Notice(this, e);
        else Trace.WriteLine($"[{DateTime.Now.ToString("HH:mm:ss")}] 线程{Thread.CurrentThread.ManagedThreadId}：{e.Log}");
    }

    #region Dispose
    ~IdleBus() => Dispose();
    bool isdisposed = false;
    object isdisposedLock = new object();
    public void Dispose()
    {
        if (isdisposed) return;
        lock (isdisposedLock)
        {
            if (isdisposed) return;
            isdisposed = true;
        }
        foreach (var item in _dic.Values)
            item.Dispose();

        _dic.Clear();
        _usageQuantity = 0;
    }
    #endregion
}