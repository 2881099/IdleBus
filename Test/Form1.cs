using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace Test
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
            ib = new IdleBus();
            ib.Notice += new EventHandler<IdleBus.NoticeEventArgs>((_, e2) =>
            {
                var log = $"[{DateTime.Now.ToString("HH:mm:ss")}] 线程{Thread.CurrentThread.ManagedThreadId}：{e2.Log}";
                //Trace.WriteLine(log);
                Console.WriteLine(log);
            });

            ib
                .Register("key1", () => new ManualResetEvent(false))
                .Register("key2", () => new AutoResetEvent(false));
        }
        private void button2_Click(object sender, EventArgs e)
        {
            MessageBox.Show(ib.Get("key1")?.ToString());
        }
        private void button3_Click(object sender, EventArgs e)
        {
            MessageBox.Show(ib.Get("key2")?.ToString());
        }
        private void button5_Click(object sender, EventArgs e)
        {
            ib?.Dispose();
        }
    }
}
