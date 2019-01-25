using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.UI.Core;

namespace HoloLensCommander.Device
{
    public class ThreadAwareINotifyPropertyChanged : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        private CoreDispatcher dispatcher;
        private object defaultLock = new object();

        public ThreadAwareINotifyPropertyChanged()
        {
            //this.dispatcher = CoreWindow.GetForCurrentThread().Dispatcher;
            this.dispatcher = Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher;
        }

        protected bool PropertyChangedHelper<T>(T newValue, ref T storage, object lockThis = null, [System.Runtime.CompilerServices.CallerMemberName] string propertyName = null)
        {
            if(lockThis == null)
            {
                lockThis = this.defaultLock;
            }

            lock(lockThis)
            {
                if (IEquatable<T>.Equals(newValue, storage))
                {
                    return false;
                }
                storage = newValue;
            }

            this.SendPropertyChanged(propertyName);

            return true;
        }

        protected void SendPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string propertyName = null)
        {
            if(this.PropertyChanged == null)
            {
                return;
            }

            if (this.dispatcher.HasThreadAccess)
            {
                // We're on the thread we were created on so
                // just send the notification
                this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs(propertyName));
                return;
            }

            // We are on a different thread than the one we were created on.
            // Go back to that thread to do the work.
            var _ = dispatcher.RunAsync(
                CoreDispatcherPriority.Normal,
                () =>
                {
                    this.SendPropertyChanged(propertyName);
                });
        }
   
    }

    internal delegate void LogChangedEventHandler(ThreadAwareLog sender);

    internal class ThreadAwareLog
    {
        public event LogChangedEventHandler LogChanged;

        private CoreDispatcher dispatcher;
        private object defaultLock = new object();
        private List<string> stringList = new List<string>();

        public ThreadAwareLog()
        {
            this.dispatcher = CoreWindow.GetForCurrentThread().Dispatcher;
        }

        public void Log(string format, params object[] args)
        {
            string message = string.Format(format, args);
            lock(this.stringList)
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
            lock(this.stringList)
            {
                retval = this.stringList.ToArray();
            }
            return retval;
        }

        public string GetLogAsString()
        {
            var sb = new StringBuilder();

            lock(this.stringList)
            {
                foreach(var s in this.stringList)
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

    public enum JobStatus
    {
        None = 0,
        Queued,
        Running,
        Canceled,
        Succeeded,
        Failed,
    };

    public delegate void JobStatusChanged(Job job, JobStatus previousStatus, JobStatus newStatus);

    /// <summary>
    /// Class represeting a job in the JobQueue
    /// </summary>
    public class Job
    {
        public event JobStatusChanged StatusChanged;

        public string DisplayName { get; private set; } = string.Empty;
     
        public bool OutOfBand { get; private set; }

        public int RetrysLeft { get; private set; }

        public string DisplayStatus { get; private set; } = string.Empty;

        public JobStatus Status { get; private set; }

        public bool Completed { get { return this.Status == JobStatus.Canceled || this.Status == JobStatus.Failed || this.Status == JobStatus.Succeeded; } }

        public CancellationToken CancellationToken { get { return this.cancellationTokenSource.Token; } }

        private Func<Job, Task> handler;
        private CancellationTokenSource cancellationTokenSource;
        private Task task;

        internal Job(string displayName, Func<Job, Task> handler, bool outOfBand, int retryCount)
        {
            this.DisplayName = displayName;
            this.handler = handler;
            this.OutOfBand = outOfBand;
            this.RetrysLeft = retryCount;
            this.cancellationTokenSource = new CancellationTokenSource();
        }

        public void OnJobQueued()
        {
            this.ChangeStatus(JobStatus.Queued);
        }

        public void Run()
        {
            if(this.Status != JobStatus.Queued)
            {
                // probably an error
                return;
            }

            this.ChangeStatus(JobStatus.Running);

            this.task = this.handler(this);
            var _ = this.WatchTaskAsync(this.task);
        }

        private async Task WatchTaskAsync(Task task)
        {
            this.RetrysLeft--;

            try
            {
                await task;
            }
            catch
            {
                // If the task threw an exception the details
                // will be provided in the Exception property
            }

            // TODO: log what's happening below

            JobStatus newStatus = this.Status;
            string newDisplayStatus = null;

            if (this.task.IsCanceled)
            {
                newStatus = JobStatus.Canceled;
            }
            else if(this.task.IsFaulted)
            {
                if(this.RetrysLeft > 0)
                {
                    // Try again
                    newStatus = JobStatus.Queued;
                }
                else
                {
                    newStatus = JobStatus.Failed;
                    newDisplayStatus = $"Failed - Exception: {this.task.Exception?.InnerException?.Message}";
                }
            }
            else if(this.task.Status == TaskStatus.RanToCompletion)
            {
                newStatus = JobStatus.Succeeded;
            }
            else
            {
                newStatus = JobStatus.Failed;
                newDisplayStatus = "Unknown Completion";
            }

            this.task = null;
            this.cancellationTokenSource = null;
            this.ChangeStatus(newStatus, newDisplayStatus);

        }

        public void Cancel()
        {
            var tokenSource = this.cancellationTokenSource;
            if(tokenSource != null)
            {
                tokenSource.Cancel();
            }
        }

        private void ChangeStatus(JobStatus newStatus, string newDisplayStatus = null)
        {
            JobStatus previousStatus = this.Status;
            this.Status = newStatus;
            this.DisplayStatus = (newDisplayStatus != null) ? newDisplayStatus : newStatus.ToString();

            this.StatusChanged?.Invoke(this, previousStatus, newStatus);
        }

        public override string ToString()
        {
            return $"{this.DisplayName} - {this.DisplayStatus}";
        }

    }

    /// <summary>
    /// Job queues to organize requests to a device
    /// Jobs can have: auto-retry, be canceled, run out of band, 
    /// and be run out of order
    /// </summary>
    public class JobQueue
    {
        private List<Job> jobs = new List<Job>(4);

        public void QueueJob(string displayName, Func<Job, Task> handler, bool outOfBand = false, int retryCount = 1)
        {
            var job = new Job(displayName, handler, outOfBand, retryCount);
            job.StatusChanged += JobStatusChanged;
            this.jobs.Insert(0, job);
            job.OnJobQueued();
            this.ProcessQueue();
        }

        private void JobStatusChanged(Job job, JobStatus previousStatus, JobStatus newStatus)
        {
            this.ProcessQueue();
        }

        public Job[] GetJobs()
        {
            return this.jobs.ToArray();
        }

        public void CancelJob(Job job)
        {
            if(this.jobs.Contains(job))
            {
                job.Cancel();
            }
        }

        private void ProcessQueue()
        {
            bool haveRunningJob = false;
            Job nextJobToRun = null;

            for(var index = this.jobs.Count - 1; index >= 0; index--)
            {
                var job = this.jobs[index];

                if(job.Completed)
                {
                    this.jobs.RemoveAt(index);
                    job.StatusChanged -= this.JobStatusChanged;
                    continue;
                }

                if(job.Status == JobStatus.Running)
                {
                    haveRunningJob = true;
                }
                else if(job.Status == JobStatus.Queued)
                {
                    if(nextJobToRun == null)
                    {
                        nextJobToRun = job;
                    }
                    else if(!nextJobToRun.OutOfBand && job.OutOfBand)
                    {
                        nextJobToRun = job;
                    }
                }
            }

            if(nextJobToRun != null)
            {
                if(haveRunningJob)
                {
                    if (nextJobToRun.OutOfBand)
                    {
                        nextJobToRun.Run();
                    }
                }
                else
                {
                    nextJobToRun.Run();
                }
            }
        }
    }
}
