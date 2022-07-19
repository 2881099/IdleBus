using IdleScheduler;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace Examples_IdleScheduler_WinformNet40
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }

        class MyTaskHandler : IdleScheduler.TaskHandlers.FreeSqlHandler
        {
            public MyTaskHandler(IFreeSql fsql) : base(fsql) { }

            public override void OnExecuting(Scheduler scheduler, TaskInfo task)
            {
                Console.WriteLine($"[{DateTime.Now.ToString("HH:mm:ss.fff")}] {task.Topic} 被执行，还剩 {scheduler.QuantityTask} 个循环任务");

                if (task.CurrentRound > 5)
                    task.Status = TaskStatus.Completed;
            }
        }
        static IdleScheduler.Scheduler _scheduler;
        static IFreeSql _fsql;
        static Form1()
        {

            _fsql = new FreeSql.FreeSqlBuilder()
                .UseConnectionString(FreeSql.DataType.Sqlite, "data source=test.db;max pool size=5")
                .UseAutoSyncStructure(true)
                .UseNoneCommandParameter(true)
                .UseMonitorCommand(cmd => Console.WriteLine($"=========sql: {cmd.CommandText}\r\n"))
                .Build();
            _scheduler = new Scheduler(new MyTaskHandler(_fsql));
        }

        string taskId = "";
        private void Form1_Load(object sender, EventArgs e)
        {
            
        }

        private void button2_Click(object sender, EventArgs e)
        {
            button2.Enabled = false;

            if (string.IsNullOrEmpty(taskId))
                //taskId = _scheduler.AddTask($"test_task_{DateTime.Now.ToString("g")}", $"test_task01_body{DateTime.Now.ToString("g")}", new[] { 3, 3, 3, 3, 5, 5, 5, 5, 10, 10 });
                taskId = _scheduler.AddTask($"test_task_{DateTime.Now.ToString("g")}", $"test_task01_body{DateTime.Now.ToString("g")}", "0,5,10,15,20,25,30,35,40,45,50,55 0/1 * * * ? *");
            else
                MessageBox.Show(_scheduler.ResumeTask(taskId).ToString());
            button1.Enabled = true;
        }

        private void button1_Click(object sender, EventArgs e)
        {
            button1.Enabled = false;
            MessageBox.Show(_scheduler.PauseTask(taskId).ToString());
            button2.Enabled = true;
        }
    }
}
