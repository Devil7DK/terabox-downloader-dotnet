using Devil7Softwares.TeraboxDownloader.Database.Models;
using Devil7Softwares.TeraboxDownloader.Enums;
using Telegram.Bot;

namespace Devil7Softwares.TeraboxDownloader.Telegram.Commands;

internal static class CommonCommands
{
    public static TelegramBotCommand StartCommand = new TelegramBotCommand("/start", "Start the bot", async (client, update, message, user, dataContext, chat, cancellationToken) =>
    {
        string name = new string?[] { user.FirstName, user.LastName }.Where(x => !string.IsNullOrEmpty(x)).Aggregate((x, y) => $"{x} {y}") ?? user.Username ?? user.Id.ToString();

        if (chat is null)
        {
            chat = new ChatEntity()
            {
                Id = message.Chat.Id,
                Type = message.Chat.Type,
                UserId = user.Id,
                User = user,
                Config = new ChatConfigEntity()
                {
                    ChatId = message.Chat.Id,
                    DownloadMethod = DownloadMethod.TeraboxDownloaderDotNet,
                }
            };

            dataContext.Chats.Add(chat);

            await dataContext.SaveChangesAsync();
        }

        await client.SendTextMessageAsync(
            chatId: message.Chat.Id,
            replyToMessageId: message.MessageId,
            text: $"Hi {name}!\n\nWelcome to Terabox Downloader Bot.\n\nSend me a link to start downloading."
        );
    });
}