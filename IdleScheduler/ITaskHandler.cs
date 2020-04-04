using System;
using System.Collections.Generic;
using System.Text;

namespace IdleScheduler
{
	public interface ITaskHandler
	{
		/// <summary>
		/// 加载正在运行中的任务
		/// </summary>
		/// <returns></returns>
		IEnumerable<TaskInfo> LoadAll();

		/// <summary>
		/// 添加任务的时候触发（落地保存）
		/// </summary>
		/// <param name="task"></param>
		void OnAdd(TaskInfo task);
		/// <summary>
		/// 删除任务的时候触发（落地保存）
		/// </summary>
		/// <param name="task"></param>
		void OnRemove(TaskInfo task);

		/// <summary>
		/// 执行任务完成的时候触发（落地保存）
		/// </summary>
		/// <param name="scheduler"></param>
		/// <param name="task"></param>
		/// <param name="result"></param>
		void OnExecuted(Scheduler scheduler, TaskInfo task, TaskLog result);
		/// <summary>
		/// 执行任务的时候触发
		/// </summary>
		/// <param name="scheduler"></param>
		/// <param name="task"></param>
		void OnExecuting(Scheduler scheduler, TaskInfo task);
	}
}
