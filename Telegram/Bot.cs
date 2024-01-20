using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Polling;

namespace Devil7Softwares.TeraboxDownloader.Telegram;

internal interface IBot
{
    public TelegramBotClient Client { get; }

    public void Start();

    public void Stop();
}

internal class Bot : IBot
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<Bot> _logger;
    private readonly IUpdateHandler _updateHandler;

    private CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();

    public TelegramBotClient Client { get; }

    public Bot(IConfiguration configuration, ILogger<Bot> logger, IUpdateHandler updateHandler)
    {
        _configuration = configuration;
        _logger = logger;
        _updateHandler = updateHandler;

        _logger.LogDebug("Creating TelegramBotClient");
        Client = new TelegramBotClient(_configuration.TelegramBotToken);

        _logger.LogDebug("Getting bot info");
        var me = Client.GetMeAsync().Result;

        _logger.LogDebug($"Bot id: {me.Id}");
        _logger.LogDebug($"Bot name: {me.FirstName}");
        _logger.LogDebug($"Bot username: {me.Username}");
    }

    public void Start()
    {
        _cancellationTokenSource = new CancellationTokenSource();

        ReceiverOptions options = new ReceiverOptions();

        _logger.LogDebug("Starting bot");
        Client.StartReceiving(
            updateHandler: _updateHandler,
            receiverOptions: options,
            cancellationToken: _cancellationTokenSource.Token
        );
    }

    public void Stop()
    {
        _cancellationTokenSource.Cancel();
    }
}
