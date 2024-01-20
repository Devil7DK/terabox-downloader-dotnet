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
    private readonly DataContext _dataContext;
    private readonly ILogger<UpdateHandler> _logger;
    private readonly ISchedulerFactory _schedulerFactory;

    public UpdateHandler(IConfiguration configuration, ILogger<UpdateHandler> logger, DataContext dataContext, ISchedulerFactory schedulerFactory)
    {
        _configuration = configuration;
        _dataContext = dataContext;
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
    }

    public async Task HandleMessageAsync(ITelegramBotClient botClient, Update update, Message message, CancellationToken cancellationToken)
    {
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
            ChatEntity? chat = _dataContext.Chats.Include(x => x.Config).FirstOrDefault(x => x.Id == message.Chat.Id);

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
                        await command.Action(botClient, update, message, user, _dataContext, chat!, cancellationToken);
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

            string[] urls = Regex.Matches(messageText, @"https?:\/\/.*?box.*?(?=\s|$)").Select(x => x.Value).ToArray();

            if (urls.Length == 0)
            {
                _logger.LogInformation("No links found in the message: {messageText}", messageText);

                await botClient.SendTextMessageAsync(
                    chatId: message.Chat.Id,
                    replyToMessageId: message.MessageId,
                    text: "No links found in the message."
                );

                return;
            }

            _logger.LogInformation("Found {count} links in the message", urls.Length);
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

                    _dataContext.Jobs.Add(job);

                    await _dataContext.SaveChangesAsync();

                    _logger.LogInformation($"Added job for url: {url}");

                    await DownloadJob.ScheduleJob(_schedulerFactory, job);
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
}
