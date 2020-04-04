using System;
using System.Collections.Generic;
using System.Text;

namespace IdleScheduler
{
	public interface ICycleTaskHandler
	{
		/// <summary>
		/// 加载正在运行中的任务
		/// </summary>
		/// <returns></returns>
		IEnumerable<CycleTaskInfo> LoadAll();

		/// <summary>
		/// 添加任务的时候触发（落地保存）
		/// </summary>
		/// <param name="task"></param>
		void OnAdd(CycleTaskInfo task);
		/// <summary>
		/// 删除任务的时候触发（落地保存）
		/// </summary>
		/// <param name="task"></param>
		void OnRemove(CycleTaskInfo task);

		/// <summary>
		/// 执行任务完成的时候触发（落地保存）
		/// </summary>
		/// <param name="scheduler"></param>
		/// <param name="task"></param>
		/// <param name="result"></param>
		void OnExecuted(Scheduler scheduler, CycleTaskInfo task, CycleTaskExecuteResultInfo result);
		/// <summary>
		/// 执行任务的时候触发
		/// </summary>
		/// <param name="scheduler"></param>
		/// <param name="task"></param>
		void OnExecuting(Scheduler scheduler, CycleTaskInfo task);
	}
}
