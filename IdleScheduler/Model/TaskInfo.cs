using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace IdleScheduler
{
	public class TaskInfo
	{
		/// <summary>
		/// 任务编号
		/// </summary>
		public string Id { get; set; }
		/// <summary>
		/// 任务标题，可用于查询
		/// </summary>
		public string Topic { get; set; }
		/// <summary>
		/// 任务数据
		/// </summary>
		public string Body { get; set; }
		/// <summary>
		/// 任务执行多少轮，-1为永久循环
		/// </summary>
		public int Round { get; set; }
		/// <summary>
		/// 定时类型
		/// </summary>
		public TaskInterval Interval { get; set; }
		/// <summary>
		/// 定时参数值
		/// </summary>
		public string IntervalArgument { get; set; }
		/// <summary>
		/// 创建时间
		/// </summary>
		public DateTime CreateTime { get; set; }
		/// <summary>
		/// 最后运行时间
		/// </summary>
		public DateTime LastRunTime { get; set; }

		int _currentRound, _errorTimes;
		/// <summary>
		/// 当前运行到第几轮
		/// </summary>
		public int CurrentRound { get => _currentRound; set => _currentRound = value; }
		/// <summary>
		/// 错次数
		/// </summary>
		public int ErrorTimes { get => _errorTimes; set => _errorTimes = value; }

		internal int IncrementCurrentRound() => Interlocked.Increment(ref _currentRound);
		internal int IncrementErrorTimes() => Interlocked.Increment(ref _errorTimes);

		public TimeSpan GetInterval()
		{
			DateTime now = DateTime.UtcNow;
			DateTime curt = DateTime.MinValue;
			TimeSpan ts = TimeSpan.Zero;
			uint ww = 0, dd = 0, hh = 0, mm = 0, ss = 0;
			double interval = -1;
			switch (Interval)
			{
				case TaskInterval.SEC:
					double.TryParse(IntervalArgument, out interval);
					interval *= 1000;
					break;
				case TaskInterval.RunOnDay:
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
				case TaskInterval.RunOnWeek:
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
				case TaskInterval.RunOnMonth:
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
}
