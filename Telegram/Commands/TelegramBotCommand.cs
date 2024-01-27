using Devil7Softwares.TeraboxDownloader.Database;
using Devil7Softwares.TeraboxDownloader.Database.Models;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace Devil7Softwares.TeraboxDownloader.Telegram.Commands;

internal class TelegramBotCommand
{
    public static readonly TelegramBotCommand[] Commands = [
        CommonCommands.StartCommand,
        ConfigCommands.DownloadMethod,
        JobCommands.JobStats,
        JobCommands.Cleanup,
    ];

    public string Command { get; set; }

    public string Description { get; set; }

    public Func<ITelegramBotClient, Update, Message, User, DataContext, ChatEntity, CancellationToken, Task> Action { get; set; }

    public TelegramBotCommand(string command, string description, Func<ITelegramBotClient, Update, Message, User, DataContext, ChatEntity, CancellationToken, Task> action)
    {
        Command = command;
        Description = description;
        Action = action;
    }
}
