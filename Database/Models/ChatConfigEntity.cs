using Devil7Softwares.TeraboxDownloader.Enums;

namespace Devil7Softwares.TeraboxDownloader.Database.Models;

internal class ChatConfigEntity
{
    public Guid Id { get; set; }

    public long ChatId { get; set; }

    public virtual ChatEntity? Chat { get; set; }

    public DownloadMethod DownloadMethod { get; set; }
}
