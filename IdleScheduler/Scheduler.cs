using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Text;
using System.Threading;
using System.Linq;

namespace IdleScheduler
{
	/// <summary>
	/// 调度管理临时任务(一次性)、循环任务(存储落地)
	/// </summary>
	public class Scheduler : IDisposable
	{
		IdleBus _ib;
		int _quantityTempTask;
		int _quantityTask;
		/// <summary>
		/// 临时任务数量
		/// </summary>
		public int QuantityTempTask => _quantityTempTask;
		/// <summary>
		/// 循环任务数量
		/// </summary>
		public int QuantityTask => _quantityTask;

		WorkQueue _wq;
		ITaskHandler _taskHandler;
		ConcurrentDictionary<string, TaskInfo> _tasks = new ConcurrentDictionary<string, TaskInfo>();

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
			_tasks?.Clear();
			Interlocked.Exchange(ref _quantityTempTask, 0);
			Interlocked.Exchange(ref _quantityTask, 0);
			(_taskHandler as IDisposable)?.Dispose();
		}
		#endregion

		public Scheduler(ITaskHandler taskHandler)
		{
			if (taskHandler == null) throw new ArgumentNullException("taskHandler 参数不能为  null");
			_taskHandler = taskHandler;

			_ib = new IdleBus();
			_ib.ScanOptions.Interval = TimeSpan.FromMilliseconds(200);
			_ib.ScanOptions.BatchQuantity = 100000;
			_ib.ScanOptions.BatchQuantityWait = TimeSpan.FromMilliseconds(100);
			_ib.ScanOptions.QuitWaitSeconds = 20;
			_ib.Notice += new EventHandler<IdleBus<IDisposable>.NoticeEventArgs>((s, e) =>
			{
			});
			_wq = new WorkQueue(30);

			var tasks = _taskHandler.LoadAll();
			foreach (var task in tasks)
				AddTaskPriv(task, false);
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
				if (isdisposed) return;
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
		/// 删除临时任务
		/// </summary>
		/// <param name="id"></param>
		/// <returns></returns>
		public bool RemoveTempTask(string id)
		{
			if (_tasks.ContainsKey(id)) return false;
			return _ib.TryRemove(id);
		}
		/// <summary>
		/// 判断临时任务是否存在
		/// </summary>
		/// <param name="id"></param>
		/// <returns></returns>
		public bool ExistsTempTask(string id) => _tasks.ContainsKey(id) == false && _ib.Exists(id);

		/// <summary>
		/// 添加循环执行的任务
		/// </summary>
		/// <param name="topic">名称</param>
		/// <param name="body">数据</param>
		/// <param name="round">循环次数，-1为永久循环</param>
		/// <param name="seconds">秒数</param>
		/// <returns></returns>
		public string AddTask(string topic, string body, int round, int seconds) => AddTaskPriv(topic, body, round, TaskInterval.SEC, string.Concat(seconds));
		/// <summary>
		/// 添加循环执行的任务（每天的什么时候执行）
		/// </summary>
		/// <returns></returns>
		public string AddTaskRunOnDay(string topic, string body, int round, int hour, int minute, int second) => AddTaskPriv(topic, body, round, TaskInterval.RunOnDay, $"{hour}:{minute}:{second}");
		/// <summary>
		/// 添加循环执行的任务（每个星期的什么时候执行）
		/// </summary>
		/// <returns></returns>
		public string AddTaskRunOnWeek(string topic, string body, int round, int week, int hour, int minute, int second) => AddTaskPriv(topic, body, round, TaskInterval.RunOnWeek, $"{week}:{hour}:{minute}:{second}");
		/// <summary>
		/// 添加循环执行的任务（每个月的什么时候执行）
		/// </summary>
		/// <returns></returns>
		public string AddTaskRunOnMonth(string topic, string body, int round, int day, int hour, int minute, int second) => AddTaskPriv(topic, body, round, TaskInterval.RunOnMonth, $"{day}:{hour}:{minute}:{second}");
		string AddTaskPriv(string topic, string body, int round, TaskInterval interval, string intervalArgument)
		{
			var task = new TaskInfo
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
			AddTaskPriv(task, true);
			return task.Id;
		}
		void AddTaskPriv(TaskInfo task, bool isSave)
		{
			if (task.Round != -1 && task.CurrentRound >= task.Round) return;
			IdleTimeout bus = null;
			bus = new IdleTimeout(() =>
			{
				if (_ib.TryRemove(task.Id) == false) return;
				var currentRound = task.IncrementCurrentRound();
				var round = task.Round;
				if (round != -1 && currentRound >= round)
				{
					if (_tasks.TryRemove(task.Id, out var old))
						Interlocked.Decrement(ref _quantityTask);
				}
				_wq.Enqueue(() =>
				{
					var result = new TaskLog
					{
						CreateTime = DateTime.UtcNow,
						TaskId = task.Id,
						Round = currentRound,
						Success = true
					};
					var startdt = DateTime.UtcNow;
					try
					{
						_taskHandler.OnExecuting(this, task);
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
						_taskHandler.OnExecuted(this, task, result);
					}
					if (round == -1 || currentRound < round)
						if (_ib.TryRegister(task.Id, () => bus, task.GetInterval()))
							_ib.Get(task.Id);
				});
			});
			if (_tasks.TryAdd(task.Id, task))
			{
				if (isSave)
				{
					try
					{
						_taskHandler.OnAdd(task);
					}
					catch
					{
						_tasks.TryRemove(task.Id, out var old);
						throw;
					}
				}
				Interlocked.Increment(ref _quantityTask);
				if (_ib.TryRegister(task.Id, () => bus, task.GetInterval()))
					_ib.Get(task.Id);
			}
		}
		/// <summary>
		/// 删除循环任务
		/// </summary>
		/// <param name="id"></param>
		/// <returns></returns>
		public bool RemoveTask(string id)
		{
			if (_tasks.TryRemove(id, out var old))
			{
				Interlocked.Decrement(ref _quantityTask);
				_taskHandler.OnRemove(old);
			}
			return _ib.TryRemove(id);
		}
		/// <summary>
		/// 判断循环任务是否存在
		/// </summary>
		/// <param name="id"></param>
		/// <returns></returns>
		public bool ExistsTask(string id) => _tasks.ContainsKey(id);

		/// <summary>
		/// 查询正在运行中的循环任务
		/// </summary>
		/// <param name="where"></param>
		/// <returns></returns>
		public TaskInfo[] FindTask(Func<TaskInfo, bool> where) => _tasks.Values.Where(where).ToArray();
	}
}