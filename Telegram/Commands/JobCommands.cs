using Devil7Softwares.TeraboxDownloader.Enums;
using Microsoft.EntityFrameworkCore;
using Telegram.Bot;

namespace Devil7Softwares.TeraboxDownloader.Telegram.Commands;

internal static class JobCommands
{
    public static TelegramBotCommand JobStats = new TelegramBotCommand("/job_stats", "Get job stats for this chat", async (client, update, message, user, dataContext, chat, cancellationToken) =>
    {
        int queued = await dataContext.Jobs.CountAsync(x => x.ChatId == chat.Id && x.Status == JobStatus.Queued, cancellationToken);
        int failed = await dataContext.Jobs.CountAsync(x => x.ChatId == chat.Id && x.Status == JobStatus.Failed, cancellationToken);
        int completed = await dataContext.Jobs.CountAsync(x => x.ChatId == chat.Id && x.Status == JobStatus.Completed, cancellationToken);
        int inProgress = await dataContext.Jobs.CountAsync(x => x.ChatId == chat.Id && x.Status == JobStatus.InProgress, cancellationToken);

        await client.SendTextMessageAsync(
            chatId: message.Chat.Id,
            replyToMessageId: message.MessageId,
            text: $"Queued: {queued}\nFailed: {failed}\nCompleted: {completed}\nIn Progress: {inProgress}"
        );
    });
}
