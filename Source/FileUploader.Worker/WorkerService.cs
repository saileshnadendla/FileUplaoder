using FileUploader.Contracts;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Polly;
using StackExchange.Redis;
using System.Text.Json;

namespace FileUploader.Worker
{
    public class WorkerService : BackgroundService
    {
        private readonly ILogger<WorkerService> _logger;
        private readonly IConnectionMultiplexer _redis;
        private readonly AsyncPolicy _retry;

        public WorkerService(ILogger<WorkerService> logger, IConnectionMultiplexer redis)
        {
            _logger = logger;
            _redis = redis;
            _retry = Policy.Handle<Exception>()
                           .WaitAndRetryAsync(3, attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)),
                               (ex, ts, attempt, ctx) => _logger.LogWarning(ex, "Retry {Attempt}", attempt));
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
                    // Blocking pop: use RPOP (we used LPUSH in API)
                    var res = await db.ListRightPopAsync("upload:jobs");
                    if (res.IsNullOrEmpty)
                    {
                        await Task.Delay(500, stoppingToken);
                        continue;
                    }

                    var job = JsonSerializer.Deserialize<UploadJob>(res.ToString())!;
                    _logger.LogInformation("Picked job {JobId} for {FileName}", job.JobId, job.FileName);
                    await Publish(sub, new UploadUpdate(job.JobId, job.FileId, job.FileName, UploadStatusKind.InProgress, 0, null));

                    var success = await ProcessJob(job, sub, stoppingToken);

                    if (success)
                    {
                        await Publish(sub, new UploadUpdate(job.JobId, job.FileId, job.FileName, UploadStatusKind.Completed, 100, null));
                    }
                    else
                    {
                        if (job.Attempt < 3)
                        {
                            job.Attempt++;
                            await db.ListLeftPushAsync("upload:jobs", JsonSerializer.Serialize(job.Attempt));
                            await Publish(sub, new UploadUpdate(job.JobId, job.FileId, job.FileName, UploadStatusKind.Queued, 0, $"Retry {job.Attempt}/3"));
                        }
                        else
                        {
                            await db.ListLeftPushAsync("upload:jobs:dlq", JsonSerializer.Serialize(job));
                            await Publish(sub, new UploadUpdate(job.JobId, job.FileId, job.FileName, UploadStatusKind.Failed, 0, "Max retries exceeded"));
                        }
                    }
                }
                catch (TaskCanceledException) { }
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
                var temp = new FileInfo(job.TempPath);
                if (!temp.Exists) throw new FileNotFoundException("Staged file missing", job.TempPath);

                // Simulate cloud upload by copying to Uploads folder under worker base
                var uploadsRoot = Path.Combine(AppContext.BaseDirectory, "Uploads");
                Directory.CreateDirectory(uploadsRoot);
                var dest = Path.Combine(uploadsRoot, $"{job.FileId}_{job.FileName}");

                using var src = new FileStream(job.TempPath, FileMode.Open, FileAccess.Read, FileShare.Read);
                using var dst = new FileStream(dest, FileMode.Create, FileAccess.Write, FileShare.None);
                await CopyWithProgress(src, dst, job, sub, ct);

                // Delete staged file after successful copy
                try { File.Delete(job.TempPath); } catch { }

                return true;
            }
            catch (Exception ex)
            {
                await Publish(_redis.GetSubscriber(), new UploadUpdate(job.JobId, job.FileId, job.FileName, UploadStatusKind.Failed, 0, ex.Message));
                return false;
            }
        }

        private static async Task CopyWithProgress(Stream source, Stream target, UploadJob job, ISubscriber sub, CancellationToken ct)
        {
            byte[] buffer = new byte[256 * 1024];
            long total = 0;
            int read;
            while ((read = await source.ReadAsync(buffer.AsMemory(0, buffer.Length), ct)) > 0)
            {
                await target.WriteAsync(buffer.AsMemory(0, read), ct);
                total += read;
                var pct = (int)(total * 100 / Math.Max(1, job.FileSize));
                await Publish(sub, new UploadUpdate(job.JobId, job.FileId, job.FileName, UploadStatusKind.InProgress, pct, null));
            }
            await target.FlushAsync(ct);
        }

        private static Task<long> Publish(ISubscriber sub, UploadUpdate update)
            => sub.PublishAsync("upload:updates", JsonSerializer.Serialize(update));
    }
}
