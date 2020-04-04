using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace Examples_IdleBus_WinformNet40
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            IdleBus.Test();
        }

        IdleBus ib;

        private void Form1_Load(object sender, EventArgs e)
        {
            
        }

        private void button4_Click(object sender, EventArgs e)
        {
            ib?.Dispose();
            ib = new IdleBus(TimeSpan.FromSeconds(10));
            ib.Notice += (_, e2) =>
            {
                var log = $"[{DateTime.Now.ToString("HH:mm:ss")}] 线程{Thread.CurrentThread.ManagedThreadId}：{e2.Log}";
                //Trace.WriteLine(log);
                Console.WriteLine(log);
            };

            ib
                .Register("key1", () => new ManualResetEvent(false))
                .Register("key2", () => new AutoResetEvent(false));

            for (var a = 3; a < 2000; a++)
                ib.Register("key" + a, () => new System.Data.SqlClient.SqlConnection());
        }
        private void button2_Click(object sender, EventArgs e)
        {
            label1.Text = ib.Get("key1")?.ToString();
        }
        private void button3_Click(object sender, EventArgs e)
        {
            label1.Text = ib.Get("key2")?.ToString();
        }

        private void button6_Click(object sender, EventArgs e)
        {
            var now = DateTime.Now;
            int counter = 100 * 1000;
            for (var k = 0; k < 100; k++)
            {
                new Thread(() =>
                {
                    var rnd = new Random();

                    for (var a = 0; a < 1000; a++)
                    {
                        for (var l = 0; l < 10; l++)
                        {
                            ib.Get("key" + rnd.Next(1, 2000));
                        }

                        if (Interlocked.Decrement(ref counter) <= 0)
                            MessageBox.Show($"测试完成，100线程并发 ib.Get() 获取100万次，耗时：{DateTime.Now.Subtract(now).TotalMilliseconds}ms");
                    }
                }).Start();
            }
        }

        private void button5_Click(object sender, EventArgs e)
        {
            ib?.Dispose();
        }

        
    }
}
