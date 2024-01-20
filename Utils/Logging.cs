using Microsoft.Extensions.Logging;

namespace Devil7Softwares.TeraboxDownloader.Utils;

internal class Logging
{
    public static ILoggerFactory? LoggerFactory { get; set; }

    public static ILogger CreateLogger<T>() => LoggerFactory?.CreateLogger<T>() ?? throw new NullReferenceException("LoggerFactory is null");

    public static ILogger CreateLogger(string categoryName) => LoggerFactory?.CreateLogger(categoryName) ?? throw new NullReferenceException("LoggerFactory is null");
}
