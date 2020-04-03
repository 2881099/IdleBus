using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Text;
using static IdleScheduler.Internal;
using System.Threading;

public class IdleScheduler : IDisposable
{
	IdleBus _ib;
	int _quantity;
	public int Quantity => _quantity;
	
	ICycleTaskStorage _taskStorage;
	Action<CycleTaskinfo> _taskExecution;
	WorkQueue _wq = new WorkQueue();
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
	}
	#endregion

	public IdleScheduler(ICycleTaskStorage taskStorage, Action<CycleTaskinfo> taskExecution)
	{
		if (taskExecution == null) throw new ArgumentNullException("taskExecution 参数不能为  null");
		if (taskStorage == null) throw new ArgumentNullException("taskStorage 参数不能为  null");
		_taskExecution = taskExecution;
		_taskStorage = taskStorage;

		_ib = new IdleBus();
		_ib.ScanOptions.IntervalSeconds = 1;
		_ib.ScanOptions.BatchQuantity = 1024;
		_ib.ScanOptions.BatchQuantityWaitSeconds = 0;
		_ib.ScanOptions.QuitWaitSeconds = 10;
		_ib.Notice += new EventHandler<IdleBus<IDisposable>.NoticeEventArgs>((s, e) =>
		{
		});
		_wq = new WorkQueue(30);

		var tasks = _taskStorage.LoadAll();
		foreach (var task in tasks)
			AddCycleTaskPriv(task);
	}

	/// <summary>
	/// 一次性临时任务（程序重启会丢失）
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
			Interlocked.Decrement(ref _quantity);
			if (handle != null)
				_wq.Enqueue(handle);
		});
		if (_ib.TryRegister(id, () => bus, timeout))
		{
			_ib.Get(id);
			Interlocked.Increment(ref _quantity);
		}
		return id;
	}
	
	/// <summary>
	/// 添加重复执行的任务
	/// </summary>
	/// <param name="text">任务数据</param>
	/// <param name="times">重复次数</param>
	/// <param name="seconds">秒数</param>
	/// <returns></returns>
	public string AddCycleTask(string text, int times, int seconds) => AddCycleTaskPriv(text, times, Interval.SEC, string.Concat(seconds));
	/// <summary>
	/// 添加重复执行的任务（每天的什么时候执行）
	/// </summary>
	/// <returns></returns>
	public string AddCycleTaskRunOnDay(string text, int times, int hour, int minute, int second) => AddCycleTaskPriv(text, times, Interval.RunOnDay, $"{hour}:{minute}:{second}");
	/// <summary>
	/// 添加重复执行的任务（每个星期的什么时候执行）
	/// </summary>
	/// <returns></returns>
	public string AddCycleTaskRunOnWeek(string text, int times, int week, int hour, int minute, int second) => AddCycleTaskPriv(text, times, Interval.RunOnWeek, $"{week}:{hour}:{minute}:{second}");
	/// <summary>
	/// 添加重复执行的任务（每个月的什么时候执行）
	/// </summary>
	/// <returns></returns>
	public string AddCycleTaskRunOnMonth(string text, int times, int day, int hour, int minute, int second) => AddCycleTaskPriv(text, times, Interval.RunOnMonth, $"{day}:{hour}:{minute}:{second}");

	string AddCycleTaskPriv(string text, int times, Interval interval, string intervalArgument)
	{
		var task = new CycleTaskinfo
		{
			Id = Guid.NewGuid().ToString(),
			Text = text,
			CreateTime = DateTime.Now,
			MaxRunTimes = times,
			Interval = interval,
			IntervalArgument = intervalArgument
		};
		AddCycleTaskPriv(task);
		return task.Id;
	}
	
	void AddCycleTaskPriv(CycleTaskinfo task)
	{
		IdleTimeout bus = null;
		bus = new IdleTimeout(() =>
		{
			if (_ib.TryRemove(task.Id) == false) return;
			var times = task.IncrementRunTimes();
			var maxTimes = task.MaxRunTimes;
			if (times >= maxTimes)
			{
				if (_cycleTasks.TryRemove(task.Id, out var old))
					Interlocked.Decrement(ref _quantity);
			}

			try
			{
				_wq.Enqueue(() =>
				{
					_taskExecution?.Invoke(task);
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
				_taskStorage.Update(task);
			}
		});
		if (_cycleTasks.TryAdd(task.Id, task))
		{
			Interlocked.Increment(ref _quantity);
			if (_ib.TryRegister(task.Id, () => bus, task.GetInterval()))
				_ib.Get(task.Id);
			_taskStorage.Add(task);
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
			Interlocked.Decrement(ref _quantity);
			_taskStorage.Remove(old);
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

		public interface ICycleTaskStorage
		{
			List<CycleTaskinfo> LoadAll();
			void Add(CycleTaskinfo task);
			void Remove(CycleTaskinfo task);
			void Update(CycleTaskinfo task);
		}

		public class CycleTaskinfo
		{
			public string Id { get; set; }
			public string Text { get; set; }
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
