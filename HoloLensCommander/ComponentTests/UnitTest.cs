
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
                await Task.Delay(40);
                log.Log("A-End");
            };

            Func<Job, Task> jobHandlerB = async (job) =>
            {
                log.Log("B-Start");
                await Task.Delay(40);
                log.Log("B-End");
            };

            Func<Job, Task> jobHandlerC = async (job) =>
            {
                log.Log("C-Start");
                await Task.Delay(10);
                log.Log("C-End");
            };

            // Check that an out of band job will get run even though
            // there are other queued and running jobs
            queue.QueueJob("Job A", jobHandlerA);
            queue.QueueJob("Job B", jobHandlerB);
            queue.QueueJob("Job C", jobHandlerC, true);
            await Task.Delay(500);
            log.AssertEquals("{A-Start}{C-Start}{C-End}{A-End}{B-Start}{B-End}");

            // Check that normal jobs will pass up running out of band jobs
            queue.QueueJob("Job C", jobHandlerC, true);
            queue.QueueJob("Job A", jobHandlerA);
            queue.QueueJob("Job B", jobHandlerB);
            await Task.Delay(500);
            log.AssertEquals("{C-Start}{A-Start}{C-End}{A-End}{B-Start}{B-End}");
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

        static int JobRepeatCount = 0;

        [TestMethod]
        public async Task Repeating()
        {
            JobQueue queue = new JobQueue();
            CheckLog log = new CheckLog();
            DateTime timeoutTime = DateTime.UtcNow + TimeSpan.FromSeconds(3.0);

            Func<Job, Task> jobHandler = async (job) =>
            {
                log.Log($"{job.DisplayName}-{JobRepeatCount}");
                await Task.Delay(1, job.CancellationToken);
                JobRepeatCount++;
            };

            JobRepeatCount = 0;
            queue.QueueJob("1", jobHandler, false, 1, TimeSpan.FromSeconds(0.2));

            while(JobRepeatCount < 4)
            {
                await Task.Delay(50);
                if(DateTime.UtcNow >= timeoutTime)
                {
                    Assert.Fail("Test failed waiting for repeating job.");
                }
            }
            var jobs = queue.GetJobs();
            Assert.AreEqual(1, jobs.Length);
            queue.CancelAllJobs();
            await Task.Delay(50);
            jobs = queue.GetJobs();
            Assert.AreEqual(0, jobs.Length);


            log.AssertEquals("{1-0}{1-1}{1-2}{1-3}");
        }

        [TestMethod]
        public async Task AwaitOnJob()
        {
            JobQueue queue = new JobQueue();
            CheckLog log = new CheckLog();

            Func<Job, Task> jobHandler = async (job) =>
            {
                log.Log($"{job.DisplayName}-Start");
                await Task.Delay(1);
                throw new InvalidOperationException();
            };

            queue.QueueJob("0", jobHandler);
            var repeatingJob = queue.QueueJob("1", jobHandler, false, 5, TimeSpan.Zero);

            await repeatingJob.Task;
            log.AssertEquals("{0-Start}{1-Start}{1-Start}{1-Start}{1-Start}{1-Start}");
            var job2 = queue.QueueJob("2", jobHandler);
            await Task.Delay(10);
            log.AssertEquals("{2-Start}");
        }

    }
}
