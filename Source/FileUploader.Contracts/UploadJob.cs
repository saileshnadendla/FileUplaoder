using System;

namespace FileUploader.Contracts
{
    public class UploadJob
    {
        public Guid JobId { get; }
        public Guid FileId { get; }
        public string FileName { get; }
        public string TempPath { get; }
        public long FileSize { get; }
        public int Attempt { get; }

        public UploadJob(Guid jobId, Guid fileId, string fileName, string tempPath, long fileSize, int attempt = 0)
        {
            JobId = jobId;
            FileId = fileId;
            FileName = fileName;
            TempPath = tempPath;
            FileSize = fileSize;
            Attempt = attempt;
        }

        public override string ToString()
        {
            return $"{FileName} ({FileSize} bytes), Attempt: {Attempt}";
        }

        public override bool Equals(object obj)
        {
            if (obj is UploadJob other)
            {
                return JobId == other.JobId &&
                       FileId == other.FileId &&
                       FileName == other.FileName &&
                       TempPath == other.TempPath &&
                       FileSize == other.FileSize &&
                       Attempt == other.Attempt;
            }
            return false;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(JobId, FileId, FileName, TempPath, FileSize, Attempt);
        }
    }
}
