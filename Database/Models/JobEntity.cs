using Devil7Softwares.TeraboxDownloader.Downloader;
using Devil7Softwares.TeraboxDownloader.Enums;

namespace Devil7Softwares.TeraboxDownloader.Database.Models;

internal class JobEntity
{
    public Guid Id { get; set; }

    public long ChatId { get; set; }

    public virtual ChatEntity? Chat { get; set; }

    public long MessageId { get; set; }

    public long StatusMessageId { get; set; }

    public string Url { get; set; } = string.Empty;

    public JobStatus Status { get; set; }

    public int RetryCount { get; set; } = 0;

    public List<DownloadedFile> DownloadedFiles { get; set; } = new();
}
