using FileUploader.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StackExchange.Redis;
using System.Text.Json;

namespace FileUploader.API
{
    [ApiController]
    [Route("api/[controller]")]
    [AllowAnonymous]
    public class UploadController : ControllerBase
    {
        private readonly IConnectionMultiplexer _redis;
        private readonly string _inbox;

        public UploadController(IConnectionMultiplexer redis)
        {
            _redis = redis;
            _inbox = Path.Combine(AppContext.BaseDirectory, "Inbox");
            Directory.CreateDirectory(_inbox);
        }

        /// <summary>
        /// Upload a file to the server (staged in Inbox and queued in Redis)
        /// </summary>
        [HttpPost]
        [Consumes("multipart/form-data")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Upload([FromForm] IFormFile file)
        {
            if (file is null || file.Length == 0)
                return BadRequest("file missing");

            var jobId = Guid.NewGuid();
            var fileId = Guid.NewGuid();
            var fileName = file.FileName ?? "upload.bin";
            var tempFileName = $"{jobId}_{Path.GetFileName(fileName)}";
            var tempPath = Path.Combine(_inbox, tempFileName);

            // Save file to Inbox
            await using (var fs = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                await file.CopyToAsync(fs);
                await fs.FlushAsync();
            }

            var size = new FileInfo(tempPath).Length;

            // Create job payload
            var job = new UploadJob(jobId, fileId, fileName, tempPath, size, 0);
            var payload = JsonSerializer.Serialize(job);

            // Push job into Redis queue
            var db = _redis.GetDatabase();
            await db.ListLeftPushAsync("upload:jobs", payload);

            // Publish queued status update
            var update = new UploadUpdate(jobId, fileId, fileName, UploadStatusKind.Queued, 0, null);
            var sub = _redis.GetSubscriber();
            await sub.PublishAsync("upload:updates", JsonSerializer.Serialize(update));

            return Ok(new { jobId, fileId, fileName, size });
        }

        /// <summary>
        /// Health Check endpoint
        /// </summary>
        [HttpGet("/health")]
        public IActionResult Health()
        {
            return Ok(new { ok = true });
        }
    }
}
