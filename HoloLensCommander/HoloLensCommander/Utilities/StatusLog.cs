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

        public void Log(string format, params object[] args)
        {
            string message = string.Format(format, args);
            lock (this.stringList)
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
            lock (this.stringList)
            {
                retval = this.stringList.ToArray();
            }
            return retval;
        }

        public string GetLogAsString()
        {
            var sb = new StringBuilder();

            lock (this.stringList)
            {
                foreach (var s in this.stringList)
                {
                    sb.Append(s);
                    sb.Append("\r\n");
                }

                // Remove the trailing /r/n
                if (this.stringList.Count > 0)
                {
                    sb.Remove(sb.Length - 1, 2);
                }
            }

            return sb.ToString();
        }
    }
}
