using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;

partial class IdleBus<T>
{
    bool _threadStarted = false;
    object _threadStartedLock = new object();

    void ThreadScanWatch(ItemInfo item)
    {
        var startThread = false;
        if (_threadStarted == false)
            lock (_threadStartedLock)
                if (_threadStarted == false)
                    startThread = _threadStarted = true;

        if (startThread)
            new Thread(() =>
            {
                this.ThreadScanWatchHandler();
                lock (_threadStartedLock)
                    _threadStarted = false;
            }).Start();
    }

    void ThreadScanWatchHandler()
    {
        var couter = 0;
        while (isdisposed == false)
        {
            if (ThreadJoin(2) == false) return;
            this.InternalRemoveDelayHandler();

            if (_usageQuantity == 0)
            {
                if (++couter < 5) continue;
                break;
            }
            couter = 0;

            var keys = _dic.Keys;
            long keysIndex = 0;
            foreach (var key in keys)
            {
                if (++keysIndex % 512 == 0 && ThreadJoin(1) == false) return;

                if (_dic.TryGetValue(key, out var item) == false) continue;
                if (item.value == null) continue;
                if (DateTime.Now.Subtract(item.lastActiveTime) <= item.idle) continue;
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
    }

    bool ThreadJoin(int seconds)
    {
        for (var a = 0; a < seconds; a++)
        {
            Thread.CurrentThread.Join(TimeSpan.FromSeconds(1));
            if (isdisposed) return false;
        }
        return true;
    }
}