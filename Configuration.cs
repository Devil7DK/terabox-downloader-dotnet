using dotenv.net;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.Extensions.Logging;

internal interface IConfiguration
{
    public bool IsProduction { get; }
    public bool IsDevelopment { get; }
    public LogLevel LogLevel { get; }
    public List<string> AllowedUsers { get; }
    public string DatabasePath { get; }
    public string TelegramBotToken { get; }
    public int MaxConcurrentDownloads { get; }
    public string UserAgent { get; }
    public bool ChunkedDownload { get; }
    public int ChunkCount { get; }
    public string DownloadsPath { get; }
}

internal class Configuration : IConfiguration
{
    public bool IsProduction { get; }
    public bool IsDevelopment { get; }
    public LogLevel LogLevel { get; }
    public List<string> AllowedUsers { get; }
    public string DatabasePath { get; }
    public string TelegramBotToken { get; }
    public int MaxConcurrentDownloads { get; }
    public string UserAgent { get; }
    public bool ChunkedDownload { get; }
    public int ChunkCount { get; }
    public string DownloadsPath { get; }

    public Configuration()
    {
        DotEnv.Load();

        string environment = Environment.GetEnvironmentVariable("APP_ENVIRONMENT") ?? "Development";

        IsProduction = environment.Equals("Production");
        IsDevelopment = !IsProduction;
        LogLevel = Enum.TryParse(Environment.GetEnvironmentVariable("LOG_LEVEL") ?? "", out LogLevel logLevel) ? logLevel : IsProduction ? LogLevel.Information : LogLevel.Debug;

        AllowedUsers = (Environment.GetEnvironmentVariable("ALLOWED_USERS") ?? "").Split(',').ToList();
        DatabasePath = Environment.GetEnvironmentVariable("DATABASE_PATH") ?? "database.db";
        TelegramBotToken = Environment.GetEnvironmentVariable("TELEGRAM_BOT_TOKEN") ?? "";

        MaxConcurrentDownloads = int.TryParse(Environment.GetEnvironmentVariable("MAX_CONCURRENT_DOWNLOADS") ?? "", out int maxConcurrentDownloads) ? maxConcurrentDownloads : 1;
        UserAgent = Environment.GetEnvironmentVariable("USER_AGENT") ?? "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36 Edg/120.0.0.0";
        ChunkedDownload = bool.TryParse(Environment.GetEnvironmentVariable("CHUNKED_DOWNLOAD") ?? "", out bool chunkedDownload) ? chunkedDownload : false;
        ChunkCount = int.TryParse(Environment.GetEnvironmentVariable("CHUNK_COUNT") ?? "", out int chunkCount) ? chunkCount : 1;
        DownloadsPath = Environment.GetEnvironmentVariable("DOWNLOADS_PATH") ?? "downloads";

        if (!Directory.Exists(DownloadsPath))
        {
            Directory.CreateDirectory(DownloadsPath);
        }

        ValidationResult result = new ConfigurationValidator().Validate(this);

        if (!result.IsValid)
        {
            throw new Exception(string.Join("\n", result.Errors));
        }
    }
}

internal class ConfigurationValidator : AbstractValidator<Configuration>
{
    public ConfigurationValidator()
    {
        RuleFor(x => x.DatabasePath).NotEmpty();
        RuleFor(x => x.TelegramBotToken).NotEmpty();
    }
}