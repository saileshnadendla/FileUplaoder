using FileUploader.Client.Model;
using Microsoft.Win32;
using Prism.Commands;
using ServiceStack.Redis;
using StackExchange.Redis;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace FileUploader.Client.ViewModel
{
    internal class FileUploaderClientViewModel : IFileUploaderClientViewModel
    {
        public ObservableCollection<FileItem> Files { get; } = new ObservableCollection<FileItem>();

        public DelegateCommand OnSelectFiles { get; private set; }
        public DelegateCommand OnUploadFiles { get; private set; }

        public FileUploaderClientViewModel()
        {
            OnSelectFiles = new DelegateCommand(OnSelectFilesImpln, () => true);
            OnUploadFiles = new DelegateCommand(OnUploadFilesImpln, () => true);
        }

        private void OnSelectFilesImpln()
        {
            var dlg = new OpenFileDialog { Multiselect = true };
            if (dlg.ShowDialog() == true)
            {
                foreach (var p in dlg.FileNames)
                {
                    var fi = new FileInfo(p);
                    Files.Add(new FileItem
                    {
                        FilePath = p,
                        FileName = fi.Name,
                        FileSize = fi.Length,
                        SizeMB = (fi.Length / 1024d / 1024d).ToString("F2"),
                        Status = "Ready",
                        Progress = 0
                    });
                }
            }
        }

        private void OnUploadFilesImpln()
        {
            //await EnsureRedis();
            //var http = new HttpClient { BaseAddress = new Uri(ApiBaseText.Text) };

            foreach (var f in Files.Where(x => !x.IsDone && x.JobId == Guid.Empty))
            {
                try
                {
                    using (var form = new MultipartFormDataContent())
                    {
                        using (var stream = File.OpenRead(f.FilePath))
                        {
                            Debugger.Launch();
                            var streamContent = new StreamContent(stream);
                            streamContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
                            form.Add(streamContent, "file", f.FileName);

                            //var resp = await http.PostAsync("/api/upload", form);
                            //resp.EnsureSuccessStatusCode();
                            //var payload = await resp.Content.ReadAsStringAsync();
                            //var doc = JsonDocument.Parse(payload);
                            //var jobId = doc.RootElement.GetProperty("jobId").GetGuid();
                            //var fileId = doc.RootElement.GetProperty("fileId").GetGuid();
                            //f.JobId = jobId;
                            //f.FileId = fileId;
                            //f.Status = "Queued";
                            //f.Progress = 0;
                        }
                    }
                }
                catch (Exception ex)
                {
                    f.Status = "Failed to enqueue";
                    Console.WriteLine(ex);
                }
            }
        }


        private ConnectionMultiplexer _redis;
        private ISubscriber _sub;
        private async Task EnsureRedis()
        {
            if (_redis != null && _redis.IsConnected)
                return;

            _redis = await ConnectionMultiplexer.ConnectAsync(new RedisText().Text);
            _sub = _redis.GetSubscriber();
            await _sub.SubscribeAsync("upload:updates", (_, value) =>
            {
                try
                {
                    var upd = JsonSerializer.Deserialize<UploadUpdate>(value);
                    if (upd is null) return;
                    Dispatcher.Invoke(() =>
                    {
                        var item = Files.FirstOrDefault(f => f.JobId == upd.JobId);
                        if (item == null) return;
                        item.Status = upd.Status.ToString();
                        item.Progress = upd.ProgressPercent;
                        if (upd.Status == UploadStatusKind.Completed || upd.Status == UploadStatusKind.Failed)
                            item.IsDone = true;
                    });
                }
                catch { }
            });
        }
    }
}
