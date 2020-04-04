using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Text;
using System.Threading;

namespace IdleScheduler
{
	public class Scheduler : IDisposable
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

		WorkQueue _wq;
		ICycleTaskHandler _cycleTaskImpl;
		ConcurrentDictionary<string, CycleTaskInfo> _cycleTasks = new ConcurrentDictionary<string, CycleTaskInfo>();

		#region Dispose
		~Scheduler() => Dispose();
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

		public Scheduler(ICycleTaskHandler cycleTaskImpl)
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
		/// 判断循环任务是否存在
		/// </summary>
		/// <param name="id"></param>
		/// <returns></returns>
		public bool ExistsCycleTask(string id) => _cycleTasks.ContainsKey(id);

		/// <summary>
		/// 添加循环执行的任务
		/// </summary>
		/// <param name="topic">名称</param>
		/// <param name="body">数据</param>
		/// <param name="round">循环次数</param>
		/// <param name="seconds">秒数</param>
		/// <returns></returns>
		public string AddCycleTask(string topic, string body, int round, int seconds) => AddCycleTaskPriv(topic, body, round, CycleTaskInterval.SEC, string.Concat(seconds));
		/// <summary>
		/// 添加循环执行的任务（每天的什么时候执行）
		/// </summary>
		/// <returns></returns>
		public string AddCycleTaskRunOnDay(string topic, string body, int round, int hour, int minute, int second) => AddCycleTaskPriv(topic, body, round, CycleTaskInterval.RunOnDay, $"{hour}:{minute}:{second}");
		/// <summary>
		/// 添加循环执行的任务（每个星期的什么时候执行）
		/// </summary>
		/// <returns></returns>
		public string AddCycleTaskRunOnWeek(string topic, string body, int round, int week, int hour, int minute, int second) => AddCycleTaskPriv(topic, body, round, CycleTaskInterval.RunOnWeek, $"{week}:{hour}:{minute}:{second}");
		/// <summary>
		/// 添加循环执行的任务（每个月的什么时候执行）
		/// </summary>
		/// <returns></returns>
		public string AddCycleTaskRunOnMonth(string topic, string body, int round, int day, int hour, int minute, int second) => AddCycleTaskPriv(topic, body, round, CycleTaskInterval.RunOnMonth, $"{day}:{hour}:{minute}:{second}");
		string AddCycleTaskPriv(string topic, string body, int round, CycleTaskInterval interval, string intervalArgument)
		{
			var task = new CycleTaskInfo
			{
				Id = $"{DateTime.UtcNow.ToString("yyyyMMdd")}.{Snowfake.Default.nextId().ToString()}",
				Topic = topic,
				Body = body,
				CreateTime = DateTime.UtcNow,
				Round = round,
				Interval = interval,
				IntervalArgument = intervalArgument,
				CurrentRound = 0,
				ErrorTimes = 0,
				LastRunTime = new DateTime(1970, 1, 1)
			};
			AddCycleTaskPriv(task, true);
			return task.Id;
		}
		void AddCycleTaskPriv(CycleTaskInfo task, bool isSave)
		{
			if (task.CurrentRound >= task.Round) return;
			IdleTimeout bus = null;
			bus = new IdleTimeout(() =>
			{
				if (_ib.TryRemove(task.Id) == false) return;
				var currentRound = task.IncrementCurrentRound();
				var round = task.Round;
				if (currentRound >= round)
				{
					if (_cycleTasks.TryRemove(task.Id, out var old))
						Interlocked.Decrement(ref _quantityCycleTask);
				}
				_wq.Enqueue(() =>
				{
					var result = new CycleTaskExecuteResultInfo
					{
						CreateTime = DateTime.UtcNow,
						CycleTaskId = task.Id,
						Round = currentRound,
						Success = true
					};
					var startdt = DateTime.UtcNow;
					try
					{
						_cycleTaskImpl.OnExecuting(this, task);
					}
					catch (Exception ex)
					{
						task.IncrementErrorTimes();
						result.Exception = ex.InnerException == null ? $"{ex.Message}\r\n{ex.StackTrace}" : $"{ex.Message}\r\n{ex.StackTrace}\r\n\r\nInnerException: {ex.InnerException.Message}\r\n{ex.InnerException.StackTrace}";
						result.Success = false;
					}
					finally
					{
						result.ElapsedMilliseconds = (long)DateTime.UtcNow.Subtract(startdt).TotalMilliseconds;
						task.LastRunTime = DateTime.UtcNow;
						_cycleTaskImpl.OnExecuted(this, task, result);
					}
					if (currentRound < round)
						if (_ib.TryRegister(task.Id, () => bus, task.GetInterval()))
							_ib.Get(task.Id);
				});
			});
			if (_cycleTasks.TryAdd(task.Id, task))
			{
				if (isSave)
				{
					try
					{
						_cycleTaskImpl.OnAdd(task);
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
		/// 删除临时任务或循环任务
		/// </summary>
		/// <param name="id"></param>
		/// <returns></returns>
		public bool RemoveTask(string id)
		{
			if (_cycleTasks.TryRemove(id, out var old))
			{
				Interlocked.Decrement(ref _quantityCycleTask);
				_cycleTaskImpl.OnRemove(old);
			}
			return _ib.TryRemove(id);
		}
	}
}