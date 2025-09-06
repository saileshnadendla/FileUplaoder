using FileUploader.Contracts;
using FileUploader.Worker.Helpers;
using FileUploader.Worker.Service;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;
using System.Text.Json;

namespace FileUploader.Worker_uTest
{
    [TestFixture]
    internal class WorkerServiceTests
    {
        private Mock<ILogger<WorkerService>> _loggerMock;
        private Mock<IHttpClientHelper> _httpClientHelper;
        private Mock<IRedisHelper> _redisHelper;
        
        [Test]
        public async Task ExecuteAsync_JobSuccessful_JobPostedToRedis()
        {
            //Arrange
            UploadJob? completedJob = null;
            UploadJob? nullJob = null;
            _loggerMock = new Mock<ILogger<WorkerService>>();
            _httpClientHelper = new Mock<IHttpClientHelper>();
            _redisHelper = new Mock<IRedisHelper>();
            var progressJobs = new List<UploadUpdate>();
            var cancellationSource = new CancellationTokenSource();
            var uploadJob = new UploadJob(Guid.NewGuid(), Path.GetTempPath(), Path.GetTempFileName(), "100", 1);
            cancellationSource.CancelAfter(1000);
            var sut = new WorkerService(_loggerMock.Object, _httpClientHelper.Object, _redisHelper.Object);

            _redisHelper.SetupSequence(x => x.GetUploadJob()).Returns(Task.Run(() => uploadJob))
                                                            .Returns(Task.Run(() => nullJob))
                                                            .Returns(Task.Run(() => nullJob));
            
            _httpClientHelper.Setup(x => x.HttpClientPostAsync(It.IsAny<UploadJob>())).Returns(Task.Run(() => true));
            _redisHelper.Setup(x => x.PushToRedis("upload:completedjobs", It.IsAny<string>())).Callback((string key, string uploadjob) => completedJob = JsonSerializer.Deserialize<UploadJob>(uploadjob));
            _redisHelper.Setup(x => x.PublishToRedis(It.IsAny<UploadUpdate>())).Callback((UploadUpdate update) => progressJobs.Add(update));

            //Act
            await sut.StartAsync(cancellationSource.Token);

            //Assert
            Assert.That(progressJobs.Count, Is.EqualTo(2));
            Assert.True(progressJobs.All(x => x.JobId == uploadJob.JobId));
            Assert.That(progressJobs[0].Status == UploadStatusKind.InProgress);
            Assert.That(progressJobs[1].Status == UploadStatusKind.Completed);
            Assert.NotNull(completedJob);
            _redisHelper.Verify(x => x.PushToRedis("upload:completedjobs", It.IsAny<string>()), Times.Once);
            Assert.That(completedJob?.JobId == uploadJob.JobId);
        }

        [Test]
        public async Task ExecuteAsync_JobSuccessfulOnRetry_JobPostedToRedis()
        {
            //Arrange
            UploadJob? completedJob = null;
            UploadJob? failedJob = null;
            UploadJob? nullJob = null;
            _loggerMock = new Mock<ILogger<WorkerService>>();
            _httpClientHelper = new Mock<IHttpClientHelper>();
            _redisHelper = new Mock<IRedisHelper>();
            var progressJobs = new List<UploadUpdate>();
            var cancellationSource = new CancellationTokenSource();
            var uploadJob = new UploadJob(Guid.NewGuid(), Path.GetTempPath(), Path.GetTempFileName(), "100", 1);
            cancellationSource.CancelAfter(1000);
            var sut = new WorkerService(_loggerMock.Object, _httpClientHelper.Object, _redisHelper.Object);

            _redisHelper.SetupSequence(x => x.GetUploadJob())
                        .Returns(Task.Run(() => uploadJob))
                        .Returns(Task.Run(() => uploadJob))
                        .Returns(Task.Run(() => nullJob))
                        .Returns(Task.Run(() => nullJob));

            _httpClientHelper.SetupSequence(x => x.HttpClientPostAsync(It.IsAny<UploadJob>()))
                                .Returns(Task.Run(() => false))
                                .Returns(Task.Run(() => true));

            _redisHelper.Setup(x => x.PushToRedis("upload:jobs", It.IsAny<string>())).Callback((string key, string uploadjob) => failedJob = JsonSerializer.Deserialize<UploadJob>(uploadjob));
            _redisHelper.Setup(x => x.PushToRedis("upload:completedjobs", It.IsAny<string>())).Callback((string key, string uploadjob) => completedJob = JsonSerializer.Deserialize<UploadJob>(uploadjob));

            _redisHelper.Setup(x => x.PublishToRedis(It.IsAny<UploadUpdate>())).Callback((UploadUpdate update) => progressJobs.Add(update));

            //Act
            await sut.StartAsync(cancellationSource.Token);

            //Assert
            Assert.That(failedJob?.JobId == uploadJob.JobId);
            Assert.That(failedJob?.Attempt, Is.EqualTo(2));
            Assert.That(progressJobs.Count, Is.EqualTo(4));
            Assert.True(progressJobs.All(x => x.JobId == uploadJob.JobId));
            Assert.That(progressJobs[0].Status == UploadStatusKind.InProgress);
            Assert.That(progressJobs[1].Status == UploadStatusKind.Queued);
            Assert.That(progressJobs[2].Status == UploadStatusKind.InProgress);
            Assert.That(progressJobs[3].Status == UploadStatusKind.Completed);
            Assert.NotNull(completedJob);
            _redisHelper.Verify(x => x.PushToRedis("upload:completedjobs", It.IsAny<string>()), Times.Once);
            Assert.That(completedJob?.JobId == uploadJob.JobId);
        }

        [Test]
        public async Task ExecuteAsync_JobExceededRetries_JobFailurePostedToRedis()
        {
            //Arrange
            UploadJob? completedJob = null;
            UploadJob? failedJob = null;
            UploadJob? nullJob = null;
            _loggerMock = new Mock<ILogger<WorkerService>>();
            _httpClientHelper = new Mock<IHttpClientHelper>();
            _redisHelper = new Mock<IRedisHelper>();
            var progressJobs = new List<UploadUpdate>();
            var cancellationSource = new CancellationTokenSource();
            var uploadJob = new UploadJob(Guid.NewGuid(), Path.GetTempPath(), Path.GetTempFileName(), "100", 3);
            cancellationSource.CancelAfter(1000);
            var sut = new WorkerService(_loggerMock.Object, _httpClientHelper.Object, _redisHelper.Object);

            _redisHelper.SetupSequence(x => x.GetUploadJob())
                        .Returns(Task.Run(() => uploadJob))
                        .Returns(Task.Run(() => nullJob))
                        .Returns(Task.Run(() => nullJob))
                        .Returns(Task.Run(() => nullJob));

            _httpClientHelper.SetupSequence(x => x.HttpClientPostAsync(It.IsAny<UploadJob>()))
                                .Returns(Task.Run(() => false))
                                .Returns(Task.Run(() => true));

            _redisHelper.Setup(x => x.PushToRedis("upload:jobs:dlq", It.IsAny<string>())).Callback((string key, string uploadjob) => failedJob = JsonSerializer.Deserialize<UploadJob>(uploadjob));
            _redisHelper.Setup(x => x.PushToRedis("upload:completedjobs", It.IsAny<string>())).Callback((string key, string uploadjob) => completedJob = JsonSerializer.Deserialize<UploadJob>(uploadjob));

            _redisHelper.Setup(x => x.PublishToRedis(It.IsAny<UploadUpdate>())).Callback((UploadUpdate update) => progressJobs.Add(update));

            //Act
            await sut.StartAsync(cancellationSource.Token);

            //Assert
            Assert.That(failedJob?.JobId == uploadJob.JobId);
            Assert.That(failedJob?.Attempt, Is.EqualTo(3));
            Assert.That(progressJobs.Count, Is.EqualTo(2));
            Assert.True(progressJobs.All(x => x.JobId == uploadJob.JobId));
            Assert.That(progressJobs[0].Status == UploadStatusKind.InProgress);
            Assert.That(progressJobs[1].Status == UploadStatusKind.Failed);
            Assert.NotNull(completedJob);
            _redisHelper.Verify(x => x.PushToRedis("upload:completedjobs", It.IsAny<string>()), Times.Once);
            Assert.That(completedJob?.JobId == uploadJob.JobId);
        }

        [Test]
        public async Task ExecuteAsync_HttpClientException_JobFailurePostedToRedis()
        {
            //Arrange
            UploadJob? completedJob = null;
            UploadJob? failedJob = null;
            UploadJob? nullJob = null;
            _loggerMock = new Mock<ILogger<WorkerService>>();
            _httpClientHelper = new Mock<IHttpClientHelper>();
            _redisHelper = new Mock<IRedisHelper>();
            var progressJobs = new List<UploadUpdate>();
            var cancellationSource = new CancellationTokenSource();
            var uploadJob = new UploadJob(Guid.NewGuid(), Path.GetTempPath(), Path.GetTempFileName(), "100", 1);
            cancellationSource.CancelAfter(1000);
            var sut = new WorkerService(_loggerMock.Object, _httpClientHelper.Object, _redisHelper.Object);

            _redisHelper.SetupSequence(x => x.GetUploadJob())
                        .Returns(Task.Run(() => uploadJob))
                        .Returns(Task.Run(() => uploadJob))
                        .Returns(Task.Run(() => nullJob))
                        .Returns(Task.Run(() => nullJob));

            _httpClientHelper.SetupSequence(x => x.HttpClientPostAsync(It.IsAny<UploadJob>()))
                                .Throws(new Exception())
                                .Returns(Task.Run(() => true));

            _redisHelper.Setup(x => x.PushToRedis("upload:jobs", It.IsAny<string>())).Callback((string key, string uploadjob) => failedJob = JsonSerializer.Deserialize<UploadJob>(uploadjob));
            _redisHelper.Setup(x => x.PushToRedis("upload:completedjobs", It.IsAny<string>())).Callback((string key, string uploadjob) => completedJob = JsonSerializer.Deserialize<UploadJob>(uploadjob));

            _redisHelper.Setup(x => x.PublishToRedis(It.IsAny<UploadUpdate>())).Callback((UploadUpdate update) => progressJobs.Add(update));

            //Act
            await sut.StartAsync(cancellationSource.Token);

            //Assert
            Assert.That(failedJob?.JobId == uploadJob.JobId);
            Assert.That(failedJob?.Attempt, Is.EqualTo(2));
            Assert.That(progressJobs.Count, Is.EqualTo(5));
            Assert.True(progressJobs.All(x => x.JobId == uploadJob.JobId));
            Assert.That(progressJobs[0].Status == UploadStatusKind.InProgress);
            Assert.That(progressJobs[1].Status == UploadStatusKind.Failed);
            Assert.That(progressJobs[2].Status == UploadStatusKind.Queued);
            Assert.That(progressJobs[3].Status == UploadStatusKind.InProgress);
            Assert.That(progressJobs[4].Status == UploadStatusKind.Completed);
            Assert.NotNull(completedJob);
            _redisHelper.Verify(x => x.PushToRedis("upload:completedjobs", It.IsAny<string>()), Times.Once);
            Assert.That(completedJob?.JobId == uploadJob.JobId);
        }
    }
}
