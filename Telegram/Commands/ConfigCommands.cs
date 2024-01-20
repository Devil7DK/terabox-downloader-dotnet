using Devil7Softwares.TeraboxDownloader.Enums;
using Telegram.Bot;
using Telegram.Bot.Types.ReplyMarkups;

namespace Devil7Softwares.TeraboxDownloader.Telegram.Commands;

internal static class ConfigCommands
{
    public static TelegramBotCommand DownloadMethod = new TelegramBotCommand("/download_method", "View/change download method", async (client, update, message, user, dataContext, chat, cancellationToken) =>
    {
        string[] downloadMethods = Enum.GetNames(typeof(DownloadMethod));

        await client.SendTextMessageAsync(
            chatId: message.Chat.Id,
            text: $"Current download method is set to `{chat.Config!.DownloadMethod}`. Select a download method:",
            replyMarkup: new ReplyKeyboardMarkup(downloadMethods.Select(x => new KeyboardButton[] { new KeyboardButton(x) }).ToArray())
        );
    });
}
