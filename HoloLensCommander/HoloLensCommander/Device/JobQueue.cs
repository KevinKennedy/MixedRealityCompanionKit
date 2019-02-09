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

    public enum JobStatus
    {
        None = 0,
        Queued,
        Running,
        Canceled,
        Succeeded,
        Failed,
    };

    public delegate void JobStatusChanged(Job job, JobStatus previousStatus, JobStatus newStatus, string statusMessage);

    /// <summary>
    /// Class represeting a job in the JobQueue
    /// </summary>
    public class Job
    {
        public event JobStatusChanged StatusChanged;

        public string DisplayName { get; private set; } = string.Empty;
     
        public bool OutOfBand { get; private set; }

        public int RetrysLeft { get; private set; }

        public TimeSpan RepeatDelay { get; private set; }

        public string DisplayStatus { get; private set; } = string.Empty;

        private Task jobTask = null;
        public Task Task
        {
            get
            {
                if (this.jobTask == null)
                {
                    // Task with null action.  This is just to signal
                    // to the outside world that everything having to 
                    // do with this job is done.  We can't use the internal
                    // task because it gets created multiple times for jobs
                    // that retry or repeat.
                    this.jobTask = new Task(() => { });
                }

                return this.jobTask;
            }
        }

        public JobStatus Status { get; private set; }

        public bool Completed { get { return this.Status == JobStatus.Canceled || this.Status == JobStatus.Failed || this.Status == JobStatus.Succeeded; } }

        public CancellationToken CancellationToken { get { return this.cancellationTokenSource.Token; } }

        public object Result { get; set; }

        private Func<Job, Task> handler;
        private CancellationTokenSource cancellationTokenSource;
        private Task task;

        internal Job(string displayName, Func<Job, Task> handler, bool outOfBand, int retryCount, TimeSpan repeatDelay)
        {
            this.DisplayName = displayName;
            this.handler = handler;
            this.OutOfBand = outOfBand;
            this.RetrysLeft = retryCount;
            this.RepeatDelay = repeatDelay;
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
                if (this.RepeatDelay != TimeSpan.Zero)
                {
                    await Task.Delay(this.RepeatDelay, this.CancellationToken);
                }
            }
            catch
            {
                // If the task threw an exception the details
                // will be provided in the Exception property
            }

            // TODO: log what's happening below

            JobStatus newStatus = this.Status;
            string newDisplayStatus = null;

            if (this.task.IsCanceled || this.cancellationTokenSource.IsCancellationRequested)
            {
                newStatus = JobStatus.Canceled;
            }
            else if(this.task.IsFaulted)
            {
                if(this.RetrysLeft > 0 || this.RepeatDelay != TimeSpan.Zero)
                {
                    // Try again
                    newStatus = JobStatus.Queued;
                }
                else
                {
                    newStatus = JobStatus.Failed;
                }

                // Temporary status change.  It will go to the newStatus below.
                string failedStatus = $"Failed {(newStatus == JobStatus.Queued ? "- Retrying" : "")} - Exception: {this.task.Exception?.InnerException?.Message}";
                this.StatusChanged?.Invoke(this, this.Status, JobStatus.Failed, failedStatus);
            }
            else if (this.RepeatDelay != TimeSpan.Zero)
            {
                // This is a repeating job so just keep it in the queue
                newStatus = JobStatus.Queued;
            }
            else if (this.task.Status == TaskStatus.RanToCompletion)
            {
                newStatus = JobStatus.Succeeded;
            }
            else
            {
                newStatus = JobStatus.Failed;
                newDisplayStatus = "Unknown Completion";
            }

            this.task = null;
            this.ChangeStatus(newStatus, newDisplayStatus);

            if(this.Completed)
            {
                if(this.jobTask != null)
                {
                    this.jobTask.Start(); // is there a more direct way to do this?
                }
            }
        }

        public void Cancel()
        {
            var tokenSource = this.cancellationTokenSource;
            if(tokenSource != null)
            {
                tokenSource.Cancel();
            }
        }

        private void ChangeStatus(JobStatus newStatus, string statusMessage = null, bool reportStatus = true)
        {
            JobStatus previousStatus = this.Status;
            this.Status = newStatus;
            this.DisplayStatus = (statusMessage != null) ? statusMessage : newStatus.ToString();

            if (reportStatus)
            {
                this.StatusChanged?.Invoke(this, previousStatus, newStatus, statusMessage);
            }
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

        public event JobStatusChanged JobStatusChanged;

        public Job QueueJob(string displayName, Func<Job, Task> handler, bool outOfBand = false, int retryCount = 1)
        {
            return this.QueueJob(displayName, handler, outOfBand, retryCount, TimeSpan.Zero);
        }

        public Job QueueJob(string displayName, Func<Job, Task> handler, bool outOfBand, int retryCount, TimeSpan repeatDelay)
        {
            var job = new Job(displayName, handler, outOfBand, retryCount, repeatDelay);
            job.StatusChanged += QueuedJobStatusChanged;
            this.jobs.Insert(0, job);
            job.OnJobQueued();
            this.ProcessQueue();

            return job;
        }

        private void QueuedJobStatusChanged(Job job, JobStatus previousStatus, JobStatus newStatus, string statusMessage)
        {
            // Whenever something changes in the jobs list, re-process the call.
            // Calls to ProcessQueue should be idempotent.
            // We could be more strategis about this or just call it on a timer
            // or let the owner decide when this gets called.
            this.ProcessQueue();

            this.JobStatusChanged?.Invoke(job, previousStatus, newStatus, statusMessage);
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

        public void CancelAllJobs()
        {
            var jobArray = this.GetJobs();
            foreach(var job in jobArray)
            {
                job.Cancel();
            }
        }

        private void ProcessQueue()
        {
            Job firstRunningRegularJob = null;
            Job firstRunningOutOfBandJob = null;
            Job nextJobToRun = null;

            for(var index = this.jobs.Count - 1; index >= 0; index--)
            {
                var job = this.jobs[index];

                if(job.Completed)
                {
                    this.jobs.RemoveAt(index);
                    job.StatusChanged -= this.QueuedJobStatusChanged;
                    continue;
                }

                if(job.Status == JobStatus.Running)
                { 
                    if(!job.OutOfBand && firstRunningRegularJob == null)
                    {
                        firstRunningRegularJob = job;
                    }
                    else if(job.OutOfBand && firstRunningOutOfBandJob == null)
                    {
                        firstRunningOutOfBandJob = job;
                    }
                }
                else if(job.Status == JobStatus.Queued)
                {
                    if(nextJobToRun == null)
                    {
                        // Job at the head of the queue is the next job to run
                        nextJobToRun = job;
                    }
                    else if(!nextJobToRun.OutOfBand && job.OutOfBand)
                    {
                        // A queued job that's marked as out of band will be run
                        // in favor of one that's not out of band
                        nextJobToRun = job;
                    }
                }
            }

            if (nextJobToRun != null)
            {
                if (firstRunningOutOfBandJob == null && nextJobToRun.OutOfBand)
                {
                    // We don't have a running out of band job so run it
                    // (you could argue that we should let multiple out of band jobs running
                    // at the same time.)
                    nextJobToRun.Run();
                }
                else if (firstRunningRegularJob == null && !nextJobToRun.OutOfBand)
                {
                    // we have no regular job running so run this one
                    nextJobToRun.Run();
                }
            }
        }
    }
}
