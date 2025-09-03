using System;

namespace FileUploader.Contracts
{
    public class UploadUpdate
    {
        public Guid JobId { get; }
        public Guid FileId { get; }
        public string FileName { get; }
        public UploadStatusKind Status { get; }
        public int ProgressPercent { get; }
        public string Error { get; }

        public UploadUpdate(Guid jobId, Guid fileId, string fileName, UploadStatusKind status, int progressPercent, string error = null)
        {
            JobId = jobId;
            FileId = fileId;
            FileName = fileName;
            Status = status;
            ProgressPercent = progressPercent;
            Error = error;
        }

        public override string ToString()
        {
            return $"{FileName} ({ProgressPercent}%): {Status}" + (string.IsNullOrEmpty(Error) ? "" : $" - Error: {Error}");
        }

        public override bool Equals(object obj)
        {
            if (obj is UploadUpdate other)
            {
                return JobId == other.JobId &&
                       FileId == other.FileId &&
                       FileName == other.FileName &&
                       Status == other.Status &&
                       ProgressPercent == other.ProgressPercent &&
                       Error == other.Error;
            }
            return false;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(JobId, FileId, FileName, Status, ProgressPercent, Error);
        }
    }

}
