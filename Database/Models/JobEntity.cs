using Devil7Softwares.TeraboxDownloader.Terabox;
using Devil7Softwares.TeraboxDownloader.Enums;

namespace Devil7Softwares.TeraboxDownloader.Database.Models;

internal class JobEntity
{
    public Guid Id { get; set; }

    public long ChatId { get; set; }

    public virtual ChatEntity? Chat { get; set; }

    public int MessageId { get; set; }

    public int StatusMessageId { get; set; }

    public string Url { get; set; } = string.Empty;

    public JobStatus Status { get; set; }

    public int RetryCount { get; set; } = 0;

    public List<DownloadedFile> DownloadedFiles { get; set; } = new();
}
