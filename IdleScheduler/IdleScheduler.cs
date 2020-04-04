using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Text;
using static IdleScheduler.Internal;
using System.Threading;

public class IdleScheduler : IDisposable
{
	IdleBus _ib;
	int _quantityTempTask;
	int _quantityCycleTask;
	/// <summary>
	/// 临时任务数量
	/// </summary>
	public int QuantityTempTask => _quantityTempTask;
	/// <summary>
	/// 循环任务数量
	/// </summary>
	public int QuantityCycleTask => _quantityCycleTask;

	ICycleTask _cycleTaskImpl;
	WorkQueue _wq;
	ConcurrentDictionary<string, CycleTaskinfo> _cycleTasks = new ConcurrentDictionary<string, CycleTaskinfo>();

	#region Dispose
	~IdleScheduler() => Dispose();
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
		_ib?.Dispose();
		_wq?.Dispose();
		_cycleTasks?.Clear();
		Interlocked.Exchange(ref _quantityTempTask, 0);
		Interlocked.Exchange(ref _quantityCycleTask, 0);
		(_cycleTaskImpl as IDisposable)?.Dispose();
	}
	#endregion

	public IdleScheduler(ICycleTask cycleTaskImpl)
	{
		if (cycleTaskImpl == null) throw new ArgumentNullException("cycleTaskImpl 参数不能为  null");
		_cycleTaskImpl = cycleTaskImpl;

		_ib = new IdleBus();
		_ib.ScanOptions.IntervalSeconds = 1;
		_ib.ScanOptions.BatchQuantity = 1024;
		_ib.ScanOptions.BatchQuantityWaitSeconds = 0;
		_ib.ScanOptions.QuitWaitSeconds = 10;
		_ib.Notice += new EventHandler<IdleBus<IDisposable>.NoticeEventArgs>((s, e) =>
		{
		});
		_wq = new WorkQueue(30);

		var tasks = _cycleTaskImpl.LoadAll();
		foreach (var task in tasks)
			AddCycleTaskPriv(task, false);
	}

	/// <summary>
	/// 临时任务（程序重启会丢失）
	/// </summary>
	/// <param name="timeout"></param>
	/// <param name="handle"></param>
	/// <returns></returns>
	public string AddTempTask(TimeSpan timeout, Action handle)
	{
		var id = Guid.NewGuid().ToString();
		var bus = new IdleTimeout(() =>
		{
			_ib.TryRemove(id);
			Interlocked.Decrement(ref _quantityTempTask);
			if (handle != null)
				_wq.Enqueue(handle);
		});
		if (_ib.TryRegister(id, () => bus, timeout))
		{
			_ib.Get(id);
			Interlocked.Increment(ref _quantityTempTask);
		}
		return id;
	}

	/// <summary>
	/// 添加循环执行的任务
	/// </summary>
	/// <param name="topic">名称</param>
	/// <param name="body">数据</param>
	/// <param name="times">循环次数</param>
	/// <param name="seconds">秒数</param>
	/// <returns></returns>
	public string AddCycleTask(string topic, string body, int times, int seconds) => AddCycleTaskPriv(topic, body, times, Interval.SEC, string.Concat(seconds));
	/// <summary>
	/// 添加循环执行的任务（每天的什么时候执行）
	/// </summary>
	/// <returns></returns>
	public string AddCycleTaskRunOnDay(string topic, string body, int times, int hour, int minute, int second) => AddCycleTaskPriv(topic, body, times, Interval.RunOnDay, $"{hour}:{minute}:{second}");
	/// <summary>
	/// 添加循环执行的任务（每个星期的什么时候执行）
	/// </summary>
	/// <returns></returns>
	public string AddCycleTaskRunOnWeek(string topic, string body, int times, int week, int hour, int minute, int second) => AddCycleTaskPriv(topic, body, times, Interval.RunOnWeek, $"{week}:{hour}:{minute}:{second}");
	/// <summary>
	/// 添加循环执行的任务（每个月的什么时候执行）
	/// </summary>
	/// <returns></returns>
	public string AddCycleTaskRunOnMonth(string topic, string body, int times, int day, int hour, int minute, int second) => AddCycleTaskPriv(topic, body, times, Interval.RunOnMonth, $"{day}:{hour}:{minute}:{second}");

	string AddCycleTaskPriv(string topic, string body, int times, Interval interval, string intervalArgument)
	{
		var task = new CycleTaskinfo
		{
			Id = Guid.NewGuid().ToString(),
			Topic = topic,
			Body = body,
			CreateTime = DateTime.Now,
			MaxRunTimes = times,
			Interval = interval,
			IntervalArgument = intervalArgument
		};
		AddCycleTaskPriv(task, true);
		return task.Id;
	}
	
	void AddCycleTaskPriv(CycleTaskinfo task, bool isSave)
	{
		if (task.RunTimes >= task.MaxRunTimes) return;
		IdleTimeout bus = null;
		bus = new IdleTimeout(() =>
		{
			if (_ib.TryRemove(task.Id) == false) return;
			var times = task.IncrementRunTimes();
			var maxTimes = task.MaxRunTimes;
			if (times >= maxTimes)
			{
				if (_cycleTasks.TryRemove(task.Id, out var old))
					Interlocked.Decrement(ref _quantityCycleTask);
			}

			try
			{
				_wq.Enqueue(() =>
				{
					_cycleTaskImpl.Execute(this, task);
					if (times < maxTimes)
						if (_ib.TryRegister(task.Id, () => bus, task.GetInterval()))
							_ib.Get(task.Id);
				});
			}
			catch
			{
				task.ErrTimes++;
			}
			finally
			{
				task.LastRunTime = DateTime.Now;
				_cycleTaskImpl.Update(task);
			}
		});
		if (_cycleTasks.TryAdd(task.Id, task))
		{
			if (isSave)
			{
				try
				{
					_cycleTaskImpl.Add(task);
				}
				catch
				{
					_cycleTasks.TryRemove(task.Id, out var old);
					throw;
				}
			}
			Interlocked.Increment(ref _quantityCycleTask);
			if (_ib.TryRegister(task.Id, () => bus, task.GetInterval()))
				_ib.Get(task.Id);
		}
	}

	/// <summary>
	/// 删除任务
	/// </summary>
	/// <param name="id"></param>
	/// <returns></returns>
	public bool RemoveTask(string id)
	{
		if (_cycleTasks.TryRemove(id, out var old))
		{
			Interlocked.Decrement(ref _quantityCycleTask);
			_cycleTaskImpl.Remove(old);
		}
		return _ib.TryRemove(id);
	}

	public class Internal
	{
		public class IdleTimeout : IDisposable
		{
			Action _disposeHandle;
			public IdleTimeout(Action disposeHandle) => _disposeHandle = disposeHandle;
			public void Dispose() => _disposeHandle?.Invoke();
		}

		public interface ICycleTask
		{
			/// <summary>
			/// 加载正在运行中的任务
			/// </summary>
			/// <returns></returns>
			IEnumerable<CycleTaskinfo> LoadAll();
			/// <summary>
			/// 添加任务的时候触发
			/// </summary>
			/// <param name="task"></param>
			void Add(CycleTaskinfo task);
			/// <summary>
			/// 删除任务的时候触发
			/// </summary>
			/// <param name="task"></param>
			void Remove(CycleTaskinfo task);
			/// <summary>
			/// 更新任务的时候触发
			/// </summary>
			/// <param name="task"></param>
			void Update(CycleTaskinfo task);

			/// <summary>
			/// 任务执行的时候触发
			/// </summary>
			/// <param name="scheduler"></param>
			/// <param name="task"></param>
			void Execute(IdleScheduler scheduler, CycleTaskinfo task);
		}

		public class CycleTaskinfo
		{
			public string Id { get; set; }
			public string Topic { get; set; }
			public string Body { get; set; }
			public DateTime CreateTime { get; set; }
			public int MaxRunTimes { get; set; }
			public Interval Interval { get; set; }
			public string IntervalArgument { get; set; }

			public DateTime LastRunTime { get; set; }

			int _runTimes;
			public int RunTimes { get => _runTimes; set => _runTimes = value; }
			public int ErrTimes { get; set; }

			internal int IncrementRunTimes() => Interlocked.Increment(ref _runTimes);

			public TimeSpan GetInterval()
			{
				DateTime now = DateTime.Now;
				DateTime curt = DateTime.MinValue;
				TimeSpan ts = TimeSpan.Zero;
				uint ww = 0, dd = 0, hh = 0, mm = 0, ss = 0;
				double interval = -1;
				switch (Interval)
				{
					case Interval.SEC:
						double.TryParse(IntervalArgument, out interval);
						interval *= 1000;
						break;
					case Interval.RunOnDay:
						List<string> hhmmss = new List<string>(string.Concat(IntervalArgument).Split(':'));
						if (hhmmss.Count == 3)
							if (uint.TryParse(hhmmss[0], out hh) && hh < 24 &&
								uint.TryParse(hhmmss[1], out mm) && mm < 60 &&
								uint.TryParse(hhmmss[2], out ss) && ss < 60)
							{
								curt = now.Date.AddHours(hh).AddMinutes(mm).AddSeconds(ss);
								ts = curt.Subtract(now);
								while (!(ts.TotalMilliseconds > 0))
								{
									curt = curt.AddDays(1);
									ts = curt.Subtract(now);
								}
								interval = ts.TotalMilliseconds;
							}
						break;
					case Interval.RunOnWeek:
						string[] wwhhmmss = string.Concat(IntervalArgument).Split(':');
						if (wwhhmmss.Length == 4)
							if (uint.TryParse(wwhhmmss[0], out ww) && ww < 7 &&
								uint.TryParse(wwhhmmss[1], out hh) && hh < 24 &&
								uint.TryParse(wwhhmmss[2], out mm) && mm < 60 &&
								uint.TryParse(wwhhmmss[3], out ss) && ss < 60)
							{
								curt = now.Date.AddHours(hh).AddMinutes(mm).AddSeconds(ss);
								ts = curt.Subtract(now);
								while (!(ts.TotalMilliseconds > 0 && (int)curt.DayOfWeek == ww))
								{
									curt = curt.AddDays(1);
									ts = curt.Subtract(now);
								}
								interval = ts.TotalMilliseconds;
							}
						break;
					case Interval.RunOnMonth:
						string[] ddhhmmss = string.Concat(IntervalArgument).Split(':');
						if (ddhhmmss.Length == 4)
							if (uint.TryParse(ddhhmmss[0], out dd) && dd > 0 && dd < 32 &&
								uint.TryParse(ddhhmmss[1], out hh) && hh < 24 &&
								uint.TryParse(ddhhmmss[2], out mm) && mm < 60 &&
								uint.TryParse(ddhhmmss[3], out ss) && ss < 60)
							{
								curt = new DateTime(now.Year, now.Month, (int)dd).AddHours(hh).AddMinutes(mm).AddSeconds(ss);
								ts = curt.Subtract(now);
								while (!(ts.TotalMilliseconds > 0))
								{
									curt = curt.AddMonths(1);
									ts = curt.Subtract(now);
								}
								interval = ts.TotalMilliseconds;
							}
						break;
				}
				if (interval == 0) interval = 1;
				return TimeSpan.FromMilliseconds(interval);
			}
		}

		public enum Interval
		{
			/// <summary>
			/// 按秒触发
			/// </summary>
			SEC = 1,

			/// <summary>
			/// 每天 什么时间 触发<para></para>
			/// 如：15:55:59<para></para>
			/// 每天15点55分59秒
			/// </summary>
			RunOnDay = 11,
			/// <summary>
			/// 每星期几 什么时间 触发<para></para>
			/// 如：2:15:55:59<para></para>
			/// 每星期二15点55分59秒
			/// </summary>
			RunOnWeek = 12,
			/// <summary>
			/// 每月第几天 什么时间 触发<para></para>
			/// 如：5:15:55:59<para></para>
			/// 每月第五天15点55分59秒
			/// </summary>
			RunOnMonth = 13
		}
	}
}
