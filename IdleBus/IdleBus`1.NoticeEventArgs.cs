using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;

partial class IdleBus<T>
{

    public class NoticeEventArgs : EventArgs
    {

        public NoticeType NoticeType { get; }
        public string Key { get; }
        public Exception Exception { get; }
        public string Log { get; }

        public NoticeEventArgs(NoticeType noticeType, string key, Exception exception, string log)
        {
            this.NoticeType = noticeType;
            this.Key = key;
            this.Exception = exception;
            this.Log = log;
        }

    }

}