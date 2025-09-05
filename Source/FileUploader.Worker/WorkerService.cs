using FileUploader.Contracts;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using System.Net.Http.Headers;
using System.Text.Json;

namespace FileUploader.Worker
{
    public class WorkerService : BackgroundService
    {
        private readonly ILogger<WorkerService> _logger;
        private readonly IConnectionMultiplexer _redis;

        public WorkerService(ILogger<WorkerService> logger, IConnectionMultiplexer redis)
        {
            _logger = logger;
            _redis = redis;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var db = _redis.GetDatabase();
            var sub = _redis.GetSubscriber();

            _logger.LogInformation("Worker started. Waiting for jobs...");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var res = await db.ListRightPopAsync("upload:jobs");
                    if (res.IsNullOrEmpty)
                    {
                        await Task.Delay(500, stoppingToken);
                        continue;
                    }

                    var job = JsonSerializer.Deserialize<UploadJob>(res.ToString());
                    _logger.LogInformation("Picked job {JobId} for {FileName}", job.JobId, job.FileName);
                    await PublishToRedis(sub, new UploadUpdate(job.JobId, job.FileName, UploadStatusKind.InProgress, 0, job.FileSize, null));

                    var success = await ProcessJob(job, sub, stoppingToken);

                    if (success)
                    {
                        var uploadUpdate = new UploadUpdate(job.JobId, job.FileName, UploadStatusKind.Completed, 100, job.FileSize, null);
                        await PublishToRedis(sub, uploadUpdate);

                        await db.ListLeftPushAsync("upload:completedjobs", JsonSerializer.Serialize(uploadUpdate));
                    }
                    else
                    {
                        if (job.Attempt < 3)
                        {
                            job.Attempt++;
                            await db.ListLeftPushAsync("upload:jobs", JsonSerializer.Serialize(job));
                            await PublishToRedis(sub, new UploadUpdate(job.JobId, job.FileName, UploadStatusKind.Queued, 0, job.FileSize, $"Retry {job.Attempt}/3"));
                        }
                        else
                        {
                            var uploadUpdate = new UploadUpdate(job.JobId, job.FileName, UploadStatusKind.Failed, 0, job.FileSize, "Max retries exceeded");
                            await db.ListLeftPushAsync("upload:jobs:dlq", JsonSerializer.Serialize(job));
                            await PublishToRedis(sub, uploadUpdate);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Worker loop error");
                    await Task.Delay(1000, stoppingToken);
                }
            }
        }

        private async Task<bool> ProcessJob(UploadJob job, ISubscriber sub, CancellationToken ct)
        {
            try
            {
                using (var form = new MultipartFormDataContent())
                {
                    using (var stream = File.OpenRead(job.FilePath))
                    {
                        var http = new HttpClient { BaseAddress = new Uri("http://localhost:5000") };
                        var streamContent = new StreamContent(stream);
                        streamContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
                        form.Add(streamContent, "file", job.FileName);
                        form.Add(new StringContent(job.JobId.ToString()), "jobId");

                        var resp = await http.PostAsync("/api/upload", form);

                        return resp.IsSuccessStatusCode;
                    }
                }
            }
            catch (Exception ex)
            {
                await PublishToRedis(_redis.GetSubscriber(), new UploadUpdate(job.JobId, job.FileName, UploadStatusKind.Failed, 0, job.FileSize, ex.Message));
                return false;
            }
        }

        private static Task<long> PublishToRedis(ISubscriber sub, UploadUpdate update)
            => sub.PublishAsync("upload:updates", JsonSerializer.Serialize(update));
    }
}
