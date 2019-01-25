
using System;
using System.Threading.Tasks;
using HoloLensCommander.Device;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ComponentTests
{
    [TestClass]
    public class UnitTest1
    {
        [TestMethod]
        public async Task SingleJob()
        {
            JobQueue queue = new JobQueue();
            CheckLog log = new CheckLog();

            Func<Job, Task> jobHandlerA = async (job) =>
            {
                log.Log("A-Start");
                await Task.Delay(10);
                log.Log("A-End");
            };

            queue.QueueJob("Job A", jobHandlerA);

            await Task.Delay(1000);

            log.AssertEquals("{A-Start}{A-End}");
        }

        [TestMethod]
        public async Task MultipleJobs()
        {
            JobQueue queue = new JobQueue();
            CheckLog log = new CheckLog();

            Func<Job, Task> jobHandlerA = async (job) =>
            {
                log.Log("A-Start");
                await Task.Delay(10);
                log.Log("A-End");
            };

            Func<Job, Task> jobHandlerB = async (job) =>
            {
                log.Log("B-Start");
                await Task.Delay(20);
                log.Log("B-End");
            };

            queue.QueueJob("Job A", jobHandlerA);
            queue.QueueJob("Job B", jobHandlerB);

            await Task.Delay(1000);

            log.AssertEquals("{A-Start}{A-End}{B-Start}{B-End}");
        }

        [TestMethod]
        public async Task OutOfBand()
        {
            JobQueue queue = new JobQueue();
            CheckLog log = new CheckLog();

            Func<Job, Task> jobHandlerA = async (job) =>
            {
                log.Log("A-Start");
                await Task.Delay(10);
                log.Log("A-End");
            };

            Func<Job, Task> jobHandlerB = async (job) =>
            {
                log.Log("B-Start");
                await Task.Delay(100);
                log.Log("B-End");
            };

            Func<Job, Task> jobHandlerC = async (job) =>
            {
                log.Log("C-Start");
                await Task.Delay(100);
                log.Log("C-End");
            };

            queue.QueueJob("Job A", jobHandlerA);
            queue.QueueJob("Job B", jobHandlerB);
            queue.QueueJob("Job C", jobHandlerC, true);

            await Task.Delay(1000);

            log.AssertEquals("{A-Start}{C-Start}{A-End}{C-End}{B-Start}{B-End}");
        }

        [TestMethod]
        public async Task CancelJob()
        {
            JobQueue queue = new JobQueue();
            CheckLog log = new CheckLog();

            Func<Job, Task> jobHandler = async (job) =>
            {
                log.Log($"{job.DisplayName}-Start");
                await Task.Delay(100, job.CancellationToken);
                log.Log($"{job.DisplayName}-End");
            };

            queue.QueueJob("1", jobHandler);
            queue.QueueJob("2", jobHandler);
            queue.QueueJob("3", jobHandler);

            var jobs = queue.GetJobs();
            foreach(var job in jobs)
            {
                if(job.DisplayName == "2")
                {
                    job.Cancel();
                    break;
                }
            }

            await Task.Delay(1000);

            log.AssertEquals("{1-Start}{1-End}{2-Start}{3-Start}{3-End}");
        }

        [TestMethod]
        public async Task Retry()
        {
            JobQueue queue = new JobQueue();
            CheckLog log = new CheckLog();

            Func<Job, Task> jobHandler = async (job) =>
            {
                log.Log($"{job.DisplayName}-Start");
                await Task.Delay(1);
                throw new InvalidOperationException();
            };

            queue.QueueJob("1", jobHandler, false, 5);

            await Task.Delay(1000);

            log.AssertEquals("{1-Start}{1-Start}{1-Start}{1-Start}{1-Start}");
        }

    }
}
