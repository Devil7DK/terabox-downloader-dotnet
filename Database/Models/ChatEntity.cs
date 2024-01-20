using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace Devil7Softwares.TeraboxDownloader.Database.Models;

internal class ChatEntity
{
    public long Id { get; set; }

    public ChatType Type { get; set; }

    public long UserId { get; set; }

    public User? User { get; set; }

    public virtual ChatConfigEntity? Config { get; set; }

    public virtual ICollection<JobEntity>? Jobs { get; set; }
}
