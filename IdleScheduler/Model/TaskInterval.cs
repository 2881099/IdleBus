using System;
using System.Collections.Generic;
using System.Text;

namespace IdleScheduler
{
	public enum TaskInterval
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
