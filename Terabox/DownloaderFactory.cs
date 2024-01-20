using System.ComponentModel;
using DebounceThrottle;
using Devil7Softwares.TeraboxDownloader.Database;
using Devil7Softwares.TeraboxDownloader.Database.Models;
using Devil7Softwares.TeraboxDownloader.Enums;
using Devil7Softwares.TeraboxDownloader.Telegram;
using Devil7Softwares.TeraboxDownloader.Utils;
using Downloader;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace Devil7Softwares.TeraboxDownloader.Terabox;

internal interface IJobDownloaderFactory
{
    public JobDownloader Create(JobEntity job, CancellationToken cancellationToken);
}

internal class JobDownloaderFactory : IJobDownloaderFactory
{
    private readonly IBot _bot;
    private readonly IConfiguration _configuration;
    private readonly DataContext _dataContext;
    private readonly ILogger<JobDownloader> _logger;
    private readonly UrlResolverFactory _urlResolverFactory;

    public JobDownloaderFactory(IBot bot, IConfiguration configuration, ILogger<JobDownloader> logger, UrlResolverFactory urlResolverFactory, DataContext dataContext)
    {
        _bot = bot;
        _configuration = configuration;
        _dataContext = dataContext;
        _logger = logger;
        _urlResolverFactory = urlResolverFactory;
    }

    public JobDownloader Create(JobEntity job, CancellationToken cancellationToken)
    {
        return new JobDownloader(_bot, _dataContext, _configuration, _logger, _urlResolverFactory, job, cancellationToken);
    }
}

internal class JobDownloader
{
    private readonly IBot _bot;
    private readonly DataContext _dataContext;
    private readonly IConfiguration _configuration;
    private readonly ILogger<JobDownloader> _logger;
    private readonly UrlResolverFactory _urlResolverFactory;

    private readonly JobEntity _job;
    private readonly CancellationToken _cancellationToken;

    private readonly DownloadService downloader;
    private readonly ThrottleDispatcher throttleDispatcher;

    public JobDownloader(IBot bot, DataContext dataContext, IConfiguration configuration, ILogger<JobDownloader> logger, UrlResolverFactory urlResolverFactory, JobEntity job, CancellationToken cancellationToken)
    {
        _bot = bot;
        _configuration = configuration;
        _dataContext = dataContext;
        _logger = logger;
        _urlResolverFactory = urlResolverFactory;

        _job = job;
        _cancellationToken = cancellationToken;

        DownloadConfiguration downloadConfiguration = new DownloadConfiguration()
        {
            ChunkCount = _configuration.ChunkedDownload ? _configuration.ChunkCount : 1,
            ParallelDownload = _configuration.ChunkedDownload,
            RequestConfiguration = new RequestConfiguration()
            {
                UserAgent = _configuration.UserAgent
            }
        };

        downloader = new DownloadService(downloadConfiguration);
        downloader.DownloadStarted += Downloader_DownloadStarted;
        downloader.DownloadProgressChanged += Downloader_DownloadProgressChanged;
        downloader.DownloadFileCompleted += Downloader_DownloadFileCompleted;

        throttleDispatcher = new ThrottleDispatcher(1000);
    }

    private async Task UpdateStatus(JobStatus jobStatus, string? statusMessage = null)
    {
        try
        {
            string status = jobStatus switch
            {
                JobStatus.Queued => "Queued",
                JobStatus.InProgress => "Inprogress",
                JobStatus.Completed => "Completed",
                JobStatus.Failed => "Failed",
                _ => throw new NotSupportedException($"Job status {jobStatus} is not supported")
            };

            string message = $"URL: {_job.Url}\nStatus: {status}";

            if (!string.IsNullOrWhiteSpace(statusMessage))
            {
                message += $" - {statusMessage}";
            }

            await _bot.Client.EditMessageTextAsync(_job.ChatId, _job.StatusMessageId, message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to edit status message for {jobId}", _job.Id);
        }

        try
        {
            if (_job.Status != jobStatus)
            {
                _job.Status = jobStatus;
                await _dataContext.SaveChangesAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update job status for {jobId}", _job.Id);
        }
    }

    private void Downloader_DownloadStarted(object? sender, DownloadStartedEventArgs e)
    {
        _logger.LogInformation("Download started for {url} ({fileName} - {fileSize})", _job.Url, e.FileName, e.TotalBytesToReceive < 1 ? "Unknown size" : e.TotalBytesToReceive.ToSizeString());
    }

    private void Downloader_DownloadProgressChanged(object? sender, DownloadProgressChangedEventArgs e)
    {
        throttleDispatcher.Throttle(async () =>
        {
            _logger.LogDebug("Download progress for {url}: {progress}%", _job.Url, e.ProgressPercentage);

            long remainingBytes = e.TotalBytesToReceive - e.ReceivedBytesSize;
            TimeSpan remainingTime = TimeSpan.FromSeconds(remainingBytes / e.BytesPerSecondSpeed);

            await UpdateStatus(JobStatus.InProgress, $"{Math.Round(e.ProgressPercentage, 2)}% ({e.ReceivedBytesSize.ToSizeString()} / {e.TotalBytesToReceive.ToSizeString()}) {e.BytesPerSecondSpeed.ToSizeString()}/s {remainingTime.ToTimeString()} remaining");
        });
    }

    private async void Downloader_DownloadFileCompleted(object? sender, AsyncCompletedEventArgs e)
    {
        if (e.Error is not null)
        {
            _logger.LogError(e.Error, "Download failed for {url}", _job.Url);

            await UpdateStatus(JobStatus.Failed, e.Error.Message);
            return;
        }
        else if (e.Cancelled)
        {
            _logger.LogInformation("Download cancelled for {url}", _job.Url);

            await UpdateStatus(JobStatus.Failed, "Download cancelled");
            return;
        }
        else
        {
            _logger.LogInformation("Download completed for {url}", _job.Url);
        }
    }

    public async Task DownloadAsync()
    {
        await UpdateStatus(JobStatus.InProgress, "Starting download");

        IUrlResolver urlResolver = _urlResolverFactory(_job.Chat!.Config!.DownloadMethod);

        ResolvedUrl? resolvedUrl;

        try
        {
            resolvedUrl = await urlResolver.Resolve(_job.Url, _cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to resolve URL");
            await UpdateStatus(JobStatus.Failed, ex.Message);
            return;
        }

        string filePath = Path.Combine(_configuration.DownloadsPath, resolvedUrl.FileId);

        _logger.LogInformation($"Downloading {resolvedUrl.Url} to {filePath}");

        try
        {
            await downloader.DownloadFileTaskAsync(resolvedUrl.Url, filePath, _cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to download file");
            await UpdateStatus(JobStatus.Failed, ex.Message);
            return;
        }

        try
        {
            _job.DownloadedFiles = new List<DownloadedFile>() {
                new DownloadedFile(resolvedUrl.FileName, filePath)
            };

            await _dataContext.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to save downloaded file list");
        }

        await UpdateStatus(JobStatus.InProgress, "Uploading file");

        try
        {
            await using (Stream fileStream = System.IO.File.OpenRead(filePath))
            {
                await _bot.Client.SendDocumentAsync(_job.ChatId, InputFile.FromStream(fileStream, resolvedUrl.FileName), replyToMessageId: _job.MessageId, cancellationToken: _cancellationToken);
            }

            _logger.LogInformation("File uploaded successfully for {url}", _job.Url);

            try
            {
                System.IO.File.Delete(filePath);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to delete file after upload");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to upload file");
            await UpdateStatus(JobStatus.Failed, "Failed to upload file");
            return;
        }

        try
        {
            await _bot.Client.DeleteMessageAsync(_job.ChatId, _job.StatusMessageId, cancellationToken: _cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to delete status message");
        }


        try
        {
            _job.Status = JobStatus.Completed;

            await _dataContext.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to mark job as completed");
        }
    }
}
