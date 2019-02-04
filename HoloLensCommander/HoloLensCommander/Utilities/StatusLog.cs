using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Core;

namespace HoloLensCommander
{
    internal delegate void LogChangedEventHandler(StatusLog sender);

    internal class StatusLog
    {
        public event LogChangedEventHandler LogChanged;

        private CoreDispatcher dispatcher;
        private object defaultLock = new object();
        private List<string> stringList = new List<string>();

        public StatusLog()
        {
            this.dispatcher = CoreWindow.GetForCurrentThread().Dispatcher;
        }

        public void Clear()
        {
            lock(defaultLock)
            {
                this.stringList.Clear();
            }
        }

        public void Log(string format, params object[] args)
        {
            string message = string.Format(format, args);
            lock (defaultLock)
            {
                this.stringList.Add(message);
            }

            if (!this.dispatcher.HasThreadAccess)
            {
                // We are on a different thread than the one we were created on.
                // Do the notification on the creator thread
                var _ = dispatcher.RunAsync(
                    CoreDispatcherPriority.Normal,
                    () =>
                    {
                        this.LogChanged?.Invoke(this);
                    });
            }
            else
            {
                // We're on the thread we were created on so
                // just send the notification
                this.LogChanged?.Invoke(this);
            }
        }

        public string[] GetLogAsArray()
        {
            string[] retval;
            lock (defaultLock)
            {
                retval = this.stringList.ToArray();
            }
            return retval;
        }

        public string GetLogAsString()
        {
            var sb = new StringBuilder();

            lock (defaultLock)
            {
                foreach (var s in this.stringList)
                {
                    sb.Append(s);
                    sb.Append("\r\n");
                }
            }

            return sb.ToString();
        }

        public string GetTail(int entryCount)
        {
            var sb = new StringBuilder();

            lock (defaultLock)
            {
                for(int index = Math.Max(0, this.stringList.Count - entryCount); index < this.stringList.Count; index++)
                {
                    sb.Append(this.stringList[index]);
                    sb.Append("\r\n");
                }
            }

            return sb.ToString();
        }
    }
}
