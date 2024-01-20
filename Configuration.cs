using dotenv.net;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.Extensions.Logging;

internal interface IConfiguration
{
    public List<string> AllowedUsers { get; }
    public string DatabasePath { get; }
    public string TelegramBotToken { get; }
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