//using System;
//using System.Collections.Concurrent;
//using System.Collections.Generic;
//using System.Diagnostics;
//using System.Text;
//using System.Threading;

//partial class IdleBus<T>
//{

//    readonly DateTime _dt1970 = new DateTime();
//    SortedDictionary<long, Dictionary<string, ItemInfo>> _releasePending = new SortedDictionary<long, Dictionary<string, ItemInfo>>();

//    void ThreadLiveWatch(ItemInfo item)
//    {
//        var oldTimeout = item.timeout;
//        var newTimeout = (long)item.lastActiveTime.Add(item.idle).Subtract(_dt1970).TotalSeconds;
//        lock (_usageLock)
//        {
//            if (_releasePending.TryGetValue(oldTimeout, out var oldItem))
//            {
//                oldItem.Remove(item.key);
//                if (oldItem.Count == 0)
//                    _releasePending.Remove(oldTimeout);
//            }
//            item.timeout = newTimeout;
//            if (_releasePending.TryGetValue(newTimeout, out var newItem))
//            {
//                if (newItem.ContainsKey(item.key) == false)
//                    newItem.Add(item.key, item);
//            }
//            else
//            {
//                _releasePending.Add(item.timeout, new Dictionary<string, ItemInfo> { [item.key] = item });
//            }
//        }

//        var startThread = false;
//        if (_threadStarted == false)
//            lock (_threadStartedLock)
//                if (_threadStarted == false)
//                    startThread = _threadStarted = true;

//        if (startThread)
//            new Thread(() =>
//            {
//                this.ThreadLiveWatchHandler();
//                lock (_threadStartedLock)
//                    _threadStarted = false;
//            }).Start();
//    }

//    void ThreadLiveWatchHandler()
//    {
//        var couter = 0;
//        while (isdisposed == false)
//        {
//            if (ThreadJoin(2) == false) return;
//            this.InternalRemoveDelayHandler();

//            if (_usageQuantity == 0)
//            {
//                if (++couter < 5) continue;
//                break;
//            }
//            couter = 0;

//            var timeoutKeys = new long[_releasePending.Count];
//            try
//            {
//                _releasePending.Keys.CopyTo(timeoutKeys, 0);
//            }
//            catch
//            {
//                lock (_usageLock)
//                {
//                    timeoutKeys = new long[_releasePending.Count];
//                    _releasePending.Keys.CopyTo(timeoutKeys, 0);
//                }
//            }
//            foreach (var timeoutKey in timeoutKeys)
//            {
//                var now = DateTime.Now;
//                var timeout = now.Subtract(_dt1970.AddSeconds(timeoutKey));
//                if (timeout <= TimeSpan.Zero) break;
//                ItemInfo[] releaseItems = null;
//                try
//                {
//                    if (_releasePending.TryGetValue(timeoutKey, out var timeoutItem) == false) continue;
//                    if (timeoutItem.Count == 0)
//                    {
//                        if (timeout.TotalSeconds > 30)
//                            _releasePending.Remove(timeoutKey);
//                        continue;
//                    }
//                    releaseItems = new ItemInfo[timeoutItem.Count];
//                    timeoutItem.Values.CopyTo(releaseItems, 0);
//                    foreach (var releaseItem in releaseItems)
//                        timeoutItem.Remove(releaseItem.key);
//                }
//                catch
//                {
//                    lock (_usageLock)
//                    {
//                        if (_releasePending.TryGetValue(timeoutKey, out var timeoutItem) == false) continue;
//                        if (timeoutItem.Count == 0)
//                        {
//                            if (timeout.TotalSeconds > 30)
//                                _releasePending.Remove(timeoutKey);
//                            continue;
//                        }
//                        releaseItems = new ItemInfo[timeoutItem.Count];
//                        timeoutItem.Values.CopyTo(releaseItems, 0);
//                        _releasePending.Remove(timeoutKey);
//                    }
//                }
//                foreach (var releaseItem in releaseItems)
//                {
//                    if (releaseItem.value == null) 
//                        continue;
//                    if (releaseItem.timeout != timeoutKey) 
//                        continue;
//                    try
//                    {
//                        var startTime = DateTime.Now;
//                        if (releaseItem.Release(() => releaseItem.timeout == timeoutKey && releaseItem.lastActiveTime >= releaseItem.createTime))
//                            //防止并发有其他线程创建，最后活动时间 > 创建时间
//                            this.OnNotice(new NoticeEventArgs(NoticeType.AutoRelease, releaseItem.key, null, $"{releaseItem.key} ---自动释放成功，耗时 {DateTime.Now.Subtract(startTime).TotalMilliseconds}ms，{_usageQuantity}/{Quantity}"));
//                        else
//                            ;
//                    }
//                    catch (Exception ex)
//                    {
//                        this.OnNotice(new NoticeEventArgs(NoticeType.AutoRelease, releaseItem.key, ex, $"{releaseItem.key} ---自动释放执行出错：{ex.Message}"));
//                    }
//                }
//            }
//        }

//    }
//}