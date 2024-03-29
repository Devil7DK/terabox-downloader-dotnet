using System.Text.RegularExpressions;
using Devil7Softwares.TeraboxDownloader.Telegram.Commands;
using Devil7Softwares.TeraboxDownloader.Database;
using Devil7Softwares.TeraboxDownloader.Database.Models;
using Devil7Softwares.TeraboxDownloader.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Quartz;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Devil7Softwares.TeraboxDownloader.Jobs;

namespace Devil7Softwares.TeraboxDownloader.Telegram;

internal class UpdateHandler : IUpdateHandler
{
    private readonly IConfiguration _configuration;
    private readonly IDbContextFactory<DataContext> _dbContextFactory;
    private readonly ILogger<UpdateHandler> _logger;
    private readonly ISchedulerFactory _schedulerFactory;

    public UpdateHandler(IConfiguration configuration, ILogger<UpdateHandler> logger, IDbContextFactory<DataContext> dbContextFactory, ISchedulerFactory schedulerFactory)
    {
        _configuration = configuration;
        _dbContextFactory = dbContextFactory;
        _logger = logger;
        _schedulerFactory = schedulerFactory;
    }

    public Task HandlePollingErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
    {
        var ErrorMessage = exception switch
        {
            ApiRequestException apiRequestException
                => $"Telegram API Error:\n[{apiRequestException.ErrorCode}]\n{apiRequestException.Message}",
            _ => exception.ToString()
        };

        _logger.LogError(exception, ErrorMessage);

        return Task.CompletedTask;
    }

    public async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
    {
        _logger.LogDebug($"Received update type: {update.Type}");

        if (update.Type == UpdateType.Message && update.Message is not null)
        {
            await HandleMessageAsync(botClient, update, update.Message, cancellationToken);
        }
        else if (update.Type == UpdateType.CallbackQuery && update.CallbackQuery is not null)
        {
            await HandleCallbackQueryAsync(botClient, update, update.CallbackQuery, cancellationToken);
        }
    }

    public async Task HandleMessageAsync(ITelegramBotClient botClient, Update update, Message message, CancellationToken cancellationToken)
    {
        DataContext dataContext = _dbContextFactory.CreateDbContext();

        _logger.LogInformation($"Received message from {message.Chat.Id} ({message.Chat.Type})");

        User? user = update.Type switch
        {
            UpdateType.Message => message.From,
            _ => null
        };

        if (user is null || user.IsBot || !((user.Username != null && _configuration.AllowedUsers.Contains(user.Username)) || _configuration.AllowedUsers.Contains(user.Id.ToString())))
        {
            if (user is not null)
            {
                _logger.LogWarning($"User {user.Username ?? user.Id.ToString()} ({user.FirstName} {user.LastName}) is not allowed to use this bot.");
            }
            else
            {
                _logger.LogWarning("User is null");
            }

            try
            {
                await botClient.SendTextMessageAsync(message.Chat.Id, "You are not allowed to use this bot.", replyToMessageId: message.MessageId, cancellationToken: cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending message");
            }

            return;
        }

        if (message.Chat.Type == ChatType.Private)
        {
            _logger.LogDebug($"Fetching chat {message.Chat.Id}");
            ChatEntity? chat = dataContext.Chats.Include(x => x.Config).FirstOrDefault(x => x.Id == message.Chat.Id);

            if (chat is null && message.Text != "/start")
            {
                _logger.LogWarning($"Chat {message.Chat.Id} not found in database.");

                await botClient.SendTextMessageAsync(
                    chatId: message.Chat.Id,
                    text: "You haven't started the bot yet. Send /start to start the bot."
                );

                return;
            }

            if (message.Text is not null && message.Text.StartsWith('/'))
            {
                foreach (TelegramBotCommand command in TelegramBotCommand.Commands)
                {
                    if (message.Text == command.Command)
                    {
                        await command.Action(botClient, update, message, user, dataContext, chat!, cancellationToken);
                        return;
                    }
                }

                _logger.LogWarning($"Unknown command: {message.Text}");

                await botClient.SendTextMessageAsync(
                    chatId: message.Chat.Id,
                    replyToMessageId: message.MessageId,
                    text: "I don't understand this command."
                );

                return;
            }

            string? messageText = message.Type switch
            {
                MessageType.Text => message.Text,
                MessageType.Photo or MessageType.Video or MessageType.Video => message.Caption,
                _ => null
            };

            if (messageText is null)
            {
                _logger.LogWarning($"Unknown message type: {message.Type}");

                await botClient.SendTextMessageAsync(
                    chatId: message.Chat.Id,
                    replyToMessageId: message.MessageId,
                    text: "I don't understand this message type."
                );

                return;
            }

            List<string> urls = new(Regex.Matches(messageText, @"https?:\/\/.*?box.*?(?=\s|$)", RegexOptions.Multiline).Select(x => x.Value).ToArray());

            if (message.Entities is not null)
            {
                foreach (MessageEntity entity in message.Entities)
                {
                    if (entity.Type == MessageEntityType.Url)
                    {
                        string url = messageText.Substring(entity.Offset, entity.Length);

                        if (!url.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                        {
                            url = "https://" + url;
                        }

                        if (!urls.Contains(url) && url.Contains("box"))
                        {
                            urls.Add(url);
                        }
                    }
                }
            }

            urls = urls.Distinct().ToList();

            if (urls.Count == 0)
            {
                _logger.LogInformation("No links found in the message: {messageText}", messageText);

                await botClient.SendTextMessageAsync(
                    chatId: message.Chat.Id,
                    replyToMessageId: message.MessageId,
                    text: "No links found in the message."
                );

                return;
            }

            _logger.LogInformation("Found {count} links in the message", urls.Count);
            foreach (string url in urls)
            {
                string replyText = $"URL: {url}\nStatus: Queued";

                try
                {
                    Message statusMessage = await botClient.SendTextMessageAsync(
                        chatId: message.Chat.Id,
                        replyToMessageId: message.MessageId,
                        text: replyText
                    );

                    JobEntity job = new JobEntity()
                    {
                        ChatId = message.Chat.Id,
                        MessageId = message.MessageId,
                        StatusMessageId = statusMessage.MessageId,
                        Url = url,
                        Status = JobStatus.Queued,
                    };

                    dataContext.Jobs.Add(job);

                    await dataContext.SaveChangesAsync();

                    _logger.LogInformation($"Added job for url: {url}");

                    await DownloadJob.ScheduleJob(_schedulerFactory, job.Id);

                    await Task.Delay(200);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to send initial message for url: {url}", url);
                }
            }
        }
        else
        {
            try
            {
                await botClient.SendTextMessageAsync(
                chatId: message.Chat.Id,
                text: "I don't work in groups or channels."
            );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending message");
            }
        }
    }

    public async Task HandleCallbackQueryAsync(ITelegramBotClient botClient, Update update, CallbackQuery callbackQuery, CancellationToken cancellationToken)
    {
        DataContext dataContext = _dbContextFactory.CreateDbContext();

        _logger.LogInformation($"Received callback query from {callbackQuery.From.Id} ({callbackQuery.From.Username})");

        if (callbackQuery.Data is not null && callbackQuery.Message is not null)
        {
            string[] data = callbackQuery.Data.Split(':');

            if (data.Length == 2)
            {
                if (data[0] == "DownloadMethod")
                {
                    ChatEntity? chat = dataContext.Chats.Include(x => x.Config).FirstOrDefault(x => x.Id == callbackQuery.Message.Chat.Id);

                    if (chat is null)
                    {
                        _logger.LogWarning($"Chat {callbackQuery.Message.Chat.Id} not found in database.");

                        await botClient.AnswerCallbackQueryAsync(callbackQuery.Id, "Chat not found in database");
                        return;
                    }

                    try
                    {
                        if (Enum.TryParse(data[1], out DownloadMethod downloadMethod))
                        {
                            chat.Config!.DownloadMethod = downloadMethod;

                            await dataContext.SaveChangesAsync();

                            await botClient.EditMessageTextAsync(
                                chatId: callbackQuery.Message.Chat.Id,
                                messageId: callbackQuery.Message.MessageId,
                                text: $"Current download method is set to `{chat.Config!.DownloadMethod}`"
                            );

                            await botClient.AnswerCallbackQueryAsync(callbackQuery.Id, "Download method updated");
                            return;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to update download method");

                        await botClient.AnswerCallbackQueryAsync(callbackQuery.Id, "Failed to update download method");
                    }
                }
            }
        }


        await botClient.AnswerCallbackQueryAsync(callbackQuery.Id, "Invalid data");
    }
}
